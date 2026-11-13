using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRLicensing.Tests
{
    /// <summary>
    /// v1.12.0-pre.18: a branding applier never modifies a shared material asset.
    /// pre.17 wrote the client's logo into the decal projector's material; in the Editor that
    /// outlived Play mode and the asset was saved without its texture (VirtualChainsaw's
    /// LogoDecal_MAT). The appliers now work on runtime copies.
    /// The Play-mode test puts real material assets on disk, enters Play mode, applies a logo
    /// through every applier, leaves Play mode, saves, and compares the files.
    /// </summary>
    public class BrandingMaterialSafetyTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Folder = "Assets/__VRLBrandingSafetyTest";
        private const string TexturePath = Folder + "/Original.asset";
        private const string DecalPath = Folder + "/Decal_MAT.mat";
        private const string BannerCopyPath = Folder + "/BannerCopy_MAT.mat";
        private const string BannerBlockPath = Folder + "/BannerBlock_MAT.mat";
        private const string Session = "VRL.BrandingSafety.";

        // ───────────── the registry ─────────────

        private static Texture2D Logo(int w = 12, int h = 4)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "FixtureLogo" };
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 0, 0, 255);
            t.SetPixels32(px); t.Apply();
            return t;
        }

        private static Shader AnyTextureShader()
        {
            return Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("UI/Default");
        }

        [Test]
        public void Copies_AreShared_Counted_AndTheOriginalIsOnlyRead()
        {
            var original = new Material(AnyTextureShader());
            var before = new Texture2D(2, 2);
            var logo = Logo();
            original.SetTexture("_MainTex", before);
            try
            {
                int live = BrandingMaterials.LiveCount;
                Material a = BrandingMaterials.Acquire(original, "_MainTex", logo);
                Material b = BrandingMaterials.Acquire(original, "_MainTex", logo);

                Assert.AreSame(a, b, "appliers that need the same thing share one copy");
                Assert.AreNotSame(original, a);
                Assert.IsTrue(BrandingMaterials.IsCopy(a));
                Assert.IsFalse(BrandingMaterials.IsCopy(original));
                Assert.AreSame(logo, a.GetTexture("_MainTex"));
                Assert.AreSame(before, original.GetTexture("_MainTex"), "the original keeps its texture");
                Assert.AreEqual(live + 1, BrandingMaterials.LiveCount);
                Assert.AreNotEqual(HideFlags.None, a.hideFlags & HideFlags.DontSave, "a copy is never saved");

                BrandingMaterials.Release(a);
                Assert.IsTrue(a != null, "still used by the second applier");
                BrandingMaterials.Release(b);
                Assert.IsTrue(a == null, "destroyed with its last user");
                Assert.AreEqual(live, BrandingMaterials.LiveCount);

                Assert.DoesNotThrow(() => BrandingMaterials.Release(null));
                Assert.DoesNotThrow(() => BrandingMaterials.Release(original), "not a copy: left alone");
                Assert.IsTrue(original != null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(original);
                UnityEngine.Object.DestroyImmediate(before);
                UnityEngine.Object.DestroyImmediate(logo);
            }
        }

        [Test]
        public void MeshApplier_WithoutPropertyBlock_PutsACopyOnTheRenderer_AndGivesTheOriginalBack()
        {
            var original = new Material(AnyTextureShader()) { name = "Banner_MAT" };
            var before = new Texture2D(2, 2);
            original.SetTexture("_MainTex", before);
            var logo = Logo();
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            try
            {
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = original;
                var applier = go.AddComponent<BrandingMeshApplier>();
                typeof(BrandingMeshApplier).GetField("useMaterialPropertyBlock", Private).SetValue(applier, false);
                typeof(BrandingMeshApplier).GetMethod("Awake", Private).Invoke(applier, null); // not called in Edit mode

                applier.ApplyLogo(logo);

                Assert.AreNotSame(original, renderer.sharedMaterial, "the renderer shows a copy");
                Assert.IsTrue(BrandingMaterials.IsCopy(renderer.sharedMaterial));
                Assert.IsNotNull(renderer.sharedMaterial.GetTexture("_MainTex"));
                Assert.AreNotSame(before, renderer.sharedMaterial.GetTexture("_MainTex"));
                Assert.AreSame(before, original.GetTexture("_MainTex"), "the shared material was not written");
                Assert.AreSame(original, applier.OriginalMaterial);

                Material copy = renderer.sharedMaterial;
                typeof(BrandingMeshApplier).GetMethod("OnDestroy", Private).Invoke(applier, null);

                Assert.AreSame(original, renderer.sharedMaterial, "safety net: the original is back");
                Assert.IsTrue(copy == null, "and the copy is gone");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(original);
                UnityEngine.Object.DestroyImmediate(before);
                UnityEngine.Object.DestroyImmediate(logo);
                BrandingTexture.ReleaseFitted();
            }
        }

        [Test]
        public void MeshApplier_WithPropertyBlock_ChangesOnlyTheRenderer()
        {
            var original = new Material(AnyTextureShader());
            var before = new Texture2D(2, 2);
            original.SetTexture("_MainTex", before);
            var logo = Logo();
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            try
            {
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = original;
                var applier = go.AddComponent<BrandingMeshApplier>();
                typeof(BrandingMeshApplier).GetMethod("Awake", Private).Invoke(applier, null);

                applier.ApplyLogo(logo);

                Assert.AreSame(original, renderer.sharedMaterial, "no copy needed");
                Assert.AreSame(before, original.GetTexture("_MainTex"));
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block, 0);
                Assert.IsNotNull(block.GetTexture("_MainTex"), "the logo lives in the property block");

                applier.RestoreOriginal();
                renderer.GetPropertyBlock(block, 0);
                Assert.IsNull(block.GetTexture("_MainTex"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(original);
                UnityEngine.Object.DestroyImmediate(before);
                UnityEngine.Object.DestroyImmediate(logo);
                BrandingTexture.ReleaseFitted();
            }
        }

        // ───────────── Play mode and back: the files on disk ─────────────

        private static Type DecalProjectorType()
        {
            return Type.GetType("UnityEngine.Rendering.Universal.DecalProjector, Unity.RenderPipelines.Universal.Runtime");
        }

        /// <summary>The texture property of a material created with <paramref name="shader"/>.</summary>
        private static string TextureProperty(Material material)
        {
            return BrandingTexture.ResolveProperty(material, "_Base_Map");
        }

        private static void CreateAssets()
        {
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.CreateFolder("Assets", Folder.Substring("Assets/".Length));

            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "Original" };
            AssetDatabase.CreateAsset(texture, TexturePath);

            // The URP decal Shader Graph when the project has URP (property "Base_Map"), any textured shader otherwise.
            Shader decalShader = Shader.Find("Shader Graphs/Decal") ?? AnyTextureShader();
            foreach (var entry in new[] { (DecalPath, decalShader), (BannerCopyPath, AnyTextureShader()), (BannerBlockPath, AnyTextureShader()) })
            {
                var material = new Material(entry.Item2);
                string property = TextureProperty(material);
                Assert.IsNotNull(property, entry.Item1 + ": the fixture shader has no texture property");
                material.SetTexture(property, texture);
                AssetDatabase.CreateAsset(material, entry.Item1);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static readonly string[] MaterialPaths = { DecalPath, BannerCopyPath, BannerBlockPath };

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
            foreach (string path in MaterialPaths) SessionState.EraseString(Session + path);
        }

        [UnityTest]
        public IEnumerator PlayMode_WithAClientLogo_LeavesTheMaterialAssetsOnDiskUnchanged()
        {
            // Only the explicit assertions below decide this test: the project it runs in may log
            // errors of its own while assets are imported or Play mode starts (no LicenseConfig).
            LogAssert.ignoreFailingMessages = true;

            // ── Edit mode: real assets on disk ──
            CreateAssets();
            foreach (string path in MaterialPaths)
            {
                string text = File.ReadAllText(path);
                StringAssert.Contains("guid: " + AssetDatabase.AssetPathToGUID(TexturePath), text, path + " references the original texture");
                SessionState.SetString(Session + path, text);
            }

            // The test project has no LicenseConfig: the bootstrapper says so when Play mode starts.
            LogAssert.ignoreFailingMessages = true;
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;

            // ── Play mode (new domain: everything is looked up again) ──
            {
                var original = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
                var decalAsset = AssetDatabase.LoadAssetAtPath<Material>(DecalPath);
                var copyAsset = AssetDatabase.LoadAssetAtPath<Material>(BannerCopyPath);
                var blockAsset = AssetDatabase.LoadAssetAtPath<Material>(BannerBlockPath);
                Assert.IsNotNull(decalAsset); Assert.IsNotNull(copyAsset); Assert.IsNotNull(blockAsset);
                var logo = Logo(30, 10);

                // decal projector (URP projects)
                Type projectorType = DecalProjectorType();
                GameObject decalGo = null; BrandingDecalApplier decal = null; Component projector = null;
                PropertyInfo projectorMaterial = null;
                if (projectorType != null)
                {
                    decalGo = new GameObject("SafetyTest Decal");
                    decalGo.SetActive(false);
                    projector = decalGo.AddComponent(projectorType);
                    projectorMaterial = projectorType.GetProperty("material");
                    projectorMaterial.SetValue(projector, decalAsset);
                    decal = decalGo.AddComponent<BrandingDecalApplier>();
                    decalGo.SetActive(true);
                }

                // banner whose renderer gets a material copy
                var copyGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                copyGo.SetActive(false);
                copyGo.GetComponent<MeshRenderer>().sharedMaterial = copyAsset;
                var copyApplier = copyGo.AddComponent<BrandingMeshApplier>();
                typeof(BrandingMeshApplier).GetField("useMaterialPropertyBlock", Private).SetValue(copyApplier, false);
                copyGo.SetActive(true);

                // banner with the default property block
                var blockGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                blockGo.SetActive(false);
                blockGo.GetComponent<MeshRenderer>().sharedMaterial = blockAsset;
                var blockApplier = blockGo.AddComponent<BrandingMeshApplier>();
                blockGo.SetActive(true);

                yield return null; // Awake + Start

                if (decal != null) decal.ApplyLogo(logo);
                copyApplier.ApplyLogo(logo);
                blockApplier.ApplyLogo(logo);
                yield return null;

                if (decal != null)
                {
                    var shown = (Material)projectorMaterial.GetValue(projector);
                    Assert.AreNotSame(decalAsset, shown, "the projector shows a copy");
                    Assert.IsTrue(BrandingMaterials.IsCopy(shown));
                    Assert.IsNotNull(shown.GetTexture(TextureProperty(shown)));
                    Assert.AreSame(original, decalAsset.GetTexture(TextureProperty(decalAsset)), "decal asset untouched in memory");
                }
                Assert.AreNotSame(copyAsset, copyGo.GetComponent<MeshRenderer>().sharedMaterial);
                Assert.AreSame(original, copyAsset.GetTexture(TextureProperty(copyAsset)), "banner asset untouched in memory");
                Assert.AreSame(blockAsset, blockGo.GetComponent<MeshRenderer>().sharedMaterial);
                Assert.AreSame(original, blockAsset.GetTexture(TextureProperty(blockAsset)));
                SessionState.SetBool(Session + "decalTested", decal != null);

                // The scene goes away with Play mode: the appliers give everything back.
                if (decalGo != null) UnityEngine.Object.Destroy(decalGo);
                UnityEngine.Object.Destroy(copyGo);
                UnityEngine.Object.Destroy(blockGo);
                yield return null;
                Assert.AreEqual(0, BrandingMaterials.LiveCount, "every copy was released");
            }

            yield return new ExitPlayMode();
            LogAssert.ignoreFailingMessages = false;

            // ── Edit mode again: save everything and compare with what was on disk ──
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            foreach (string path in MaterialPaths)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.AreSame(texture, material.GetTexture(TextureProperty(material)), path + ": texture reference after Play mode");
                EditorUtility.SetDirty(material); // force the write: whatever is in memory goes to disk
            }
            AssetDatabase.SaveAssets();

            foreach (string path in MaterialPaths)
                Assert.AreEqual(SessionState.GetString(Session + path, "?"), File.ReadAllText(path), path + " changed on disk");

            if (!SessionState.GetBool(Session + "decalTested", false))
                Assert.Inconclusive("Banner materials verified; the decal projector part needs the Universal RP package in the project.");
        }
    }
}
