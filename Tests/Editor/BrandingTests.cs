using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace VRLicensing.Tests
{
    /// <summary>
    /// v1.12.0-pre.17 branding:
    /// the client reads branding ONLY through the get_license_branding RPC (no table access in
    /// the whole runtime); a failed call keeps the cached branding or shows none; the appliers
    /// find the texture property the material really has ("Base_Map" on the URP 17 decal shader)
    /// and fit the logo without cropping, stretching or a black background.
    /// </summary>
    public class BrandingTests
    {
        private const int TestProduct = -990117; // never a real product id

        private GameObject go;
        private SupabaseClient client;
        private readonly List<string> calls = new List<string>();

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("BrandingTests");
            client = go.AddComponent<SupabaseClient>();
            calls.Clear();
            BrandingCache.Clear(TestProduct);
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
            BrandingCache.Clear(TestProduct);
            BrandingTexture.ReleaseFitted();
        }

        private static void Run(IEnumerator routine)
        {
            while (routine.MoveNext())
                if (routine.Current is IEnumerator inner) Run(inner);
        }

        private void UseServer(Func<RpcResponse> answer)
        {
            client.Transport = (function, body, done) => Fake(function, done, answer);
        }

        private IEnumerator Fake(string function, Action<RpcResponse> done, Func<RpcResponse> answer)
        {
            calls.Add(function);
            done(answer());
            yield break;
        }

        private static RpcResponse Http(long code, string body) =>
            new RpcResponse { transportOk = true, httpCode = code, body = body };

        // ───────────── RPC only ─────────────

        [Test]
        public void RuntimeNeverTouchesATable()
        {
            // Contract §2: the app may only call SECURITY DEFINER RPCs. Any "/rest/v1/<table>"
            // in the runtime sources is a direct table access with the anon key.
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(LicenseManager).Assembly);
            Assert.IsNotNull(info);
            string runtime = Path.Combine(info.resolvedPath, "Runtime");
            var rest = new Regex(@"/rest/v1/(?<what>[A-Za-z_{][^""\s?]*)");
            var offenders = new List<string>();
            int files = 0;

            foreach (string file in Directory.GetFiles(runtime, "*.cs", SearchOption.AllDirectories))
            {
                files++;
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line.TrimStart().StartsWith("//")) continue; // comments may name the old paths
                    foreach (Match m in rest.Matches(line))
                        if (!m.Groups["what"].Value.StartsWith("rpc/"))
                            offenders.Add(Path.GetFileName(file) + ":" + (i + 1) + " " + m.Value);
                    if (Regex.IsMatch(line, @"\b(license_branding|device_telemetry|user_licenses|device_registry|store_entitlements)\b")
                        && line.Contains("\""))
                        offenders.Add(Path.GetFileName(file) + ":" + (i + 1) + " names a table in a string");
                }
            }

            Assert.Greater(files, 20, "the runtime folder was not found");
            Assert.IsEmpty(offenders, "direct table access: " + string.Join(" | ", offenders));
        }

        [Test]
        public void ClientHasNoDirectBrandingOrTelemetryMethod()
        {
            var names = typeof(SupabaseClient).GetMethods().Select(m => m.Name).ToList();
            CollectionAssert.DoesNotContain(names, "FetchBranding", "the direct license_branding read is gone");
            CollectionAssert.DoesNotContain(names, "SendTelemetry", "the direct device_telemetry insert is gone");
            CollectionAssert.Contains(names, "FetchBrandingRpc");
            CollectionAssert.Contains(names, "SendTelemetryRpc");
        }

        [Test]
        public void Branding_Answered_OneRpcCall()
        {
            UseServer(() => Http(200, "{\"brandName\":\"ACME\",\"logoUrl\":\"https://x/logo.png\"}"));
            BrandingData got = null; string error = null;
            Run(client.FetchBrandingRpc("KEY", 1, "dev", b => got = b, e => error = e));

            Assert.IsNull(error);
            Assert.AreEqual("ACME", got.brand_name);
            Assert.AreEqual("https://x/logo.png", got.logo_url);
            CollectionAssert.AreEqual(new[] { "get_license_branding" }, calls);
        }

        [TestCase(404, "{\"code\":\"PGRST202\",\"message\":\"Could not find the function\"}")]
        [TestCase(500, "internal error")]
        [TestCase(401, "{\"message\":\"permission denied for table license_branding\"}")]
        public void Branding_RpcFails_ErrorAndNothingElseIsCalled(int code, string body)
        {
            UseServer(() => Http(code, body));
            bool success = false; string error = null;
            Run(client.FetchBrandingRpc("KEY", 1, "dev", b => success = true, e => error = e));

            Assert.IsFalse(success);
            Assert.IsNotNull(error);
            CollectionAssert.AreEqual(new[] { "get_license_branding" }, calls,
                "a failed or missing RPC must not be followed by any other request");
        }

        [Test]
        public void Branding_NoNetwork_Error()
        {
            UseServer(() => new RpcResponse { transportOk = false, httpCode = 0, error = "Cannot resolve destination host" });
            bool success = false; string error = null;
            Run(client.FetchBrandingRpc("KEY", 1, "dev", b => success = true, e => error = e));

            Assert.IsFalse(success);
            StringAssert.Contains("Cannot resolve destination host", error);
            Assert.AreEqual(1, calls.Count);
        }

        [Test]
        public void Telemetry_OnlyTheRpc_EvenWhenItIsMissing()
        {
            UseServer(() => Http(404, "{\"code\":\"PGRST202\"}"));
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new Regex("report_telemetry failed"));
            Run(client.SendTelemetryRpc("KEY", 1, "dev", new TelemetryPayload()));
            CollectionAssert.AreEqual(new[] { "report_telemetry" }, calls);
        }

        // ───────────── answers ─────────────

        [TestCase("{}")]
        [TestCase("null")]
        [TestCase("")]
        [TestCase("{\"brandName\":\"\",\"logoUrl\":\"\"}")]
        public void Answer_NoBrandingConfigured(string body)
        {
            Assert.IsTrue(BrandingCache.TryParseAnswer(body, out var branding, out var error));
            Assert.IsNull(branding);
            Assert.IsNull(error);
        }

        [TestCase("{\"error\":\"Too many requests.\"}")]
        [TestCase("{\"error\":\"License not valid for this product and device.\"}")]
        [TestCase("<html>gateway</html>")]
        public void Answer_ThatIsNotAboutBranding_IsAFailure(string body)
        {
            // Otherwise a rate-limited call would be read as "the client removed its branding".
            Assert.IsFalse(BrandingCache.TryParseAnswer(body, out var branding, out var error));
            Assert.IsNull(branding);
            Assert.IsNotNull(error);
        }

        // ───────────── keep the cache, or none ─────────────

        private static BrandingData Brand(string name, string url = "") =>
            new BrandingData { brand_name = name, logo_url = url };

        [Test]
        public void Failure_KeepsTheCachedBranding()
        {
            var cached = Brand("ACME", "https://x/logo.png");
            Assert.AreSame(cached, BrandingCache.Resolve(BrandingAnswer.Failed, null, cached));
        }

        [Test]
        public void Failure_WithNoCache_IsNoBranding()
        {
            Assert.IsNull(BrandingCache.Resolve(BrandingAnswer.Failed, null, null));
            Assert.IsNull(BrandingCache.Resolve(BrandingAnswer.Failed, Brand("ignored"), null),
                "a failed call has no fresh value to trust");
        }

        [Test]
        public void ServerAnswer_AlwaysWins()
        {
            var cached = Brand("OLD");
            var fresh = Brand("NEW");
            Assert.AreSame(fresh, BrandingCache.Resolve(BrandingAnswer.Answered, fresh, cached));
            Assert.IsNull(BrandingCache.Resolve(BrandingAnswer.Answered, null, cached),
                "the client removed its branding: the cache must not bring it back");
        }

        [Test]
        public void Cache_RoundTrip_AndClear()
        {
            Assert.IsNull(BrandingCache.Load(TestProduct));

            BrandingCache.Save(TestProduct, Brand("ACME", "https://x/logo.png"));
            var loaded = BrandingCache.Load(TestProduct);
            Assert.AreEqual("ACME", loaded.brand_name);
            Assert.AreEqual("https://x/logo.png", loaded.logo_url);

            byte[] png = { 1, 2, 3, 4 };
            BrandingCache.SaveLogo(TestProduct, "https://x/logo.png", png);
            CollectionAssert.AreEqual(png, BrandingCache.LoadLogo(TestProduct, "https://x/logo.png"));
            Assert.IsNull(BrandingCache.LoadLogo(TestProduct, "https://x/other.png"), "the file belongs to another URL");

            // A new logo URL invalidates the stored file.
            BrandingCache.Save(TestProduct, Brand("ACME", "https://x/new.png"));
            Assert.IsNull(BrandingCache.LoadLogo(TestProduct, "https://x/logo.png"));
            Assert.IsNull(BrandingCache.LoadLogo(TestProduct, "https://x/new.png"));

            BrandingCache.Clear(TestProduct);
            Assert.IsNull(BrandingCache.Load(TestProduct));
        }

        // ───────────── texture property ─────────────

        private static Func<string, bool> MaterialWith(params string[] properties) =>
            name => properties.Contains(name);

        [Test]
        public void Property_Urp17Decal_BaseMapWithoutUnderscore()
        {
            // Chainsaw's decals: the applier is configured with "_Base_Map", the URP 17 decal
            // Shader Graph only has "Base_Map". Before pre.17 nothing was applied.
            Assert.AreEqual("Base_Map", BrandingTexture.ResolveProperty(MaterialWith("Base_Map", "Normal_Map"), "_Base_Map"));
        }

        [Test]
        public void Property_OlderDecal_UnderscoreBaseMap()
        {
            Assert.AreEqual("_Base_Map", BrandingTexture.ResolveProperty(MaterialWith("_Base_Map"), "_Base_Map"));
            Assert.AreEqual("_Base_Map", BrandingTexture.ResolveProperty(MaterialWith("_Base_Map"), "Base_Map"));
        }

        [Test]
        public void Property_ConfiguredOneWinsWhenPresent()
        {
            Assert.AreEqual("_MainTex", BrandingTexture.ResolveProperty(MaterialWith("_MainTex", "_BaseMap", "Base_Map"), "_MainTex"));
            Assert.AreEqual("_Custom", BrandingTexture.ResolveProperty(MaterialWith("_Custom", "_BaseMap"), "_Custom"));
        }

        [Test]
        public void Property_NoneKnown_IsNull()
        {
            Assert.IsNull(BrandingTexture.ResolveProperty(MaterialWith("_Color"), "_Base_Map"));
            Assert.IsNull(BrandingTexture.ResolveProperty((Material)null, "_Base_Map"));
        }

        [Test]
        public void Property_OnARealMaterial()
        {
            var shader = Shader.Find("Sprites/Default");
            Assume.That(shader != null, "Sprites/Default is a built-in shader");
            var material = new Material(shader);
            try
            {
                Assert.AreEqual("_MainTex", BrandingTexture.ResolveProperty(material, "_Base_Map"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        // ───────────── fit: aspect kept, nothing cropped, no black ─────────────

        private static readonly Color32 Red = new Color32(255, 0, 0, 255);
        private static readonly Color32 White = new Color32(255, 255, 255, 255);

        private static Color32[] Solid(int w, int h, Color32 c) => Enumerable.Repeat(c, w * h).ToArray();

        [Test]
        public void Fit_WideLogoOnASquareSurface_IsLetterboxed_NotStretchedOrCropped()
        {
            // 8x2 logo on a 1:1 decal.
            Color32[] result = BrandingTexture.Fit(Solid(8, 2, Red), 8, 2, 1f, 0f, White, true, 2048, out int w, out int h);

            Assert.AreEqual(8, w);
            Assert.AreEqual(8, h, "the canvas has the surface's aspect");
            int logoPixels = result.Count(c => c.a == 255 && c.r == 255 && c.g == 0);
            Assert.AreEqual(16, logoPixels, "every logo pixel is there, none added: same aspect, no crop");
            // rows 3 and 4 hold the logo, the rest is transparent
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    Assert.AreEqual(y == 3 || y == 4 ? 255 : 0, result[y * w + x].a, $"alpha at {x},{y}");
        }

        [Test]
        public void Fit_TallLogoOnAWideSurface_IsPillarboxed()
        {
            Color32[] result = BrandingTexture.Fit(Solid(2, 4, Red), 2, 4, 2f, 0f, White, true, 2048, out int w, out int h);
            Assert.AreEqual(8, w);
            Assert.AreEqual(4, h);
            Assert.AreEqual(8, result.Count(c => c.a == 255));
            Assert.AreEqual(255, result[0 * w + 3].a);
            Assert.AreEqual(0, result[0 * w + 0].a);
        }

        [Test]
        public void Fit_SameAspect_IsTheLogoItself()
        {
            Color32[] src = Solid(4, 2, Red);
            Color32[] result = BrandingTexture.Fit(src, 4, 2, 2f, 0f, White, true, 2048, out int w, out int h);
            Assert.AreEqual(4, w); Assert.AreEqual(2, h);
            CollectionAssert.AreEqual(src, result);
        }

        [Test]
        public void Fit_Transparent_NoBlackAnywhere()
        {
            // A PNG whose transparent pixels are stored as black (0,0,0,0) — the usual export.
            var src = Solid(4, 4, new Color32(0, 0, 0, 0));
            src[5] = Red; src[6] = Red;

            Color32[] kept = BrandingTexture.Fit(src, 4, 4, 2f, 0f, White, true, 2048, out int w, out int h);
            Assert.IsFalse(kept.Any(c => c.a == 0 && (c.r != 255 || c.g != 255 || c.b != 255)),
                "transparent texels carry the background colour, so filtering cannot pull black into the edge");
            Assert.AreEqual(2, kept.Count(c => c.a == 255));

            // Opaque surface (banner): composited over the background — no alpha left, no black.
            Color32[] opaque = BrandingTexture.Fit(src, 4, 4, 2f, 0f, White, false, 2048, out w, out h);
            Assert.IsTrue(opaque.All(c => c.a == 255));
            Assert.AreEqual(2, opaque.Count(c => c.r == 255 && c.g == 0 && c.b == 0));
            Assert.AreEqual(opaque.Length - 2, opaque.Count(c => c.r == 255 && c.g == 255 && c.b == 255),
                "everything that is not logo is the background colour");
        }

        [Test]
        public void Fit_HalfTransparentPixel_IsBlendedOnOpaqueSurfaces()
        {
            var src = new[] { new Color32(0, 0, 0, 128) };
            Color32[] opaque = BrandingTexture.Fit(src, 1, 1, 1f, 0f, White, false, 2048, out _, out _);
            Assert.AreEqual(255, opaque[0].a);
            Assert.That(opaque[0].r, Is.InRange(126, 128), "black at 50% over white is mid grey");
        }

        [Test]
        public void Fit_Padding_LeavesAMargin()
        {
            Color32[] result = BrandingTexture.Fit(Solid(8, 8, Red), 8, 8, 1f, 0.25f, White, true, 2048, out int w, out int h);
            Assert.AreEqual(16, w); Assert.AreEqual(16, h);
            Assert.AreEqual(64, result.Count(c => c.a == 255));
            Assert.AreEqual(0, result[0].a);
            Assert.AreEqual(255, result[8 * w + 8].a);
        }

        [Test]
        public void Fit_LargeCanvas_IsScaledDownKeepingTheAspect()
        {
            // 100x100 logo on a 1:4 banner would need 100x400: capped at 64 on the long side.
            Color32[] result = BrandingTexture.Fit(Solid(100, 100, Red), 100, 100, 0.25f, 0f, White, true, 64, out int w, out int h);
            Assert.AreEqual(16, w); Assert.AreEqual(64, h);
            int logo = result.Count(c => c.a > 0);
            Assert.AreEqual(16 * 16, logo, "the logo is still square");
        }

        [Test]
        public void Fitted_Texture_IsSharedBetweenAppliersAndReleasedWithANewLogo()
        {
            var logo = new Texture2D(8, 2, TextureFormat.RGBA32, false);
            logo.SetPixels32(Solid(8, 2, Red)); logo.Apply();
            var other = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Texture a = BrandingTexture.Fitted(logo, 1f, 0f, Color.white, true);
                Texture b = BrandingTexture.Fitted(logo, 1f, 0f, Color.white, true);
                Assert.AreSame(a, b, "two decals of the same shape share one texture");
                Assert.AreEqual(8, a.width); Assert.AreEqual(8, a.height);
                Assert.AreNotSame(a, BrandingTexture.Fitted(logo, 2f, 0f, Color.white, true));

                BrandingTexture.Fitted(other, 1f, 0f, Color.white, true);
                Assert.IsTrue(a == null, "the copies of the previous logo were destroyed");
            }
            finally
            {
                BrandingTexture.ReleaseFitted();
                UnityEngine.Object.DestroyImmediate(logo);
                UnityEngine.Object.DestroyImmediate(other);
            }
        }
    }
}
