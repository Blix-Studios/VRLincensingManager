using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRLicensing
{
    /// <summary>
    /// v1.12.0-pre.17 - helpers shared by the branding appliers:
    /// <list type="bullet">
    /// <item>which texture property a material really has (<c>_Base_Map</c>, <c>Base_Map</c> —
    /// the URP 17 decal Shader Graph —, <c>_BaseMap</c>, <c>_MainTex</c>);</item>
    /// <item>a copy of the client's logo that FITS the surface it goes on: whole logo visible,
    /// aspect ratio kept, the rest of the surface transparent (decals, alpha materials) or a
    /// background colour (opaque banners) — never the black that a transparent PNG shows on an
    /// opaque material, never a stretched or cropped logo.</item>
    /// </list>
    /// </summary>
    public static class BrandingTexture
    {
        /// <summary>Texture property names tried, in order, after the one configured on the applier.</summary>
        public static readonly string[] KnownProperties = { "_Base_Map", "Base_Map", "_BaseMap", "_MainTex" };

        /// <summary>Largest side of a fitted texture, in pixels.</summary>
        public const int MaxSize = 2048;

        // ───────────── property name ─────────────

        /// <summary>
        /// The texture property to use: <paramref name="preferred"/> when the material has it,
        /// otherwise the first of <see cref="KnownProperties"/> it has; null when it has none.
        /// </summary>
        public static string ResolveProperty(Func<string, bool> hasProperty, string preferred)
        {
            if (hasProperty == null) return null;
            if (!string.IsNullOrEmpty(preferred) && hasProperty(preferred)) return preferred;
            foreach (string name in KnownProperties)
                if (name != preferred && hasProperty(name)) return name;
            return null;
        }

        public static string ResolveProperty(Material material, string preferred)
        {
            return material == null ? null : ResolveProperty(material.HasProperty, preferred);
        }

        /// <summary>
        /// True when the material draws alpha (alpha-tested or transparent queue): a fitted logo
        /// can keep its transparency there. An opaque material shows the RGB of transparent
        /// pixels, so the logo is composited over a background colour instead.
        /// </summary>
        public static bool DrawsAlpha(Material material)
        {
            return material != null && material.renderQueue >= 2450;
        }

        // ───────────── fitting (pure) ─────────────

        /// <summary>
        /// Pixels of a canvas of aspect <paramref name="targetAspect"/> (width / height) with the
        /// whole source image centred in it, scaled uniformly (never cropped, never stretched).
        /// </summary>
        /// <param name="source">Source pixels, row by row (<paramref name="width"/> x <paramref name="height"/>).</param>
        /// <param name="targetAspect">Aspect of the surface. 0 or less = the source's own aspect.</param>
        /// <param name="padding">Margin around the logo as a fraction of the canvas (0 - 0.45).</param>
        /// <param name="background">Colour of everything that is not logo.</param>
        /// <param name="keepAlpha">
        /// True: the background is transparent (its RGB is still <paramref name="background"/>, so
        /// filtering does not darken the logo's edge) and the logo keeps its alpha.
        /// False: opaque result, the logo composited over the background.
        /// </param>
        public static Color32[] Fit(Color32[] source, int width, int height, float targetAspect, float padding,
            Color32 background, bool keepAlpha, int maxSize, out int outWidth, out int outHeight)
        {
            if (source == null || width <= 0 || height <= 0 || source.Length < width * height)
                throw new ArgumentException("source does not hold width x height pixels");

            float sourceAspect = (float)width / height;
            if (targetAspect <= 0f || float.IsNaN(targetAspect) || float.IsInfinity(targetAspect)) targetAspect = sourceAspect;
            padding = Mathf.Clamp(padding, 0f, 0.45f);
            float inner = 1f - 2f * padding;
            if (maxSize < 4) maxSize = 4;

            // Canvas in which the logo keeps its own resolution…
            float canvasW, canvasH;
            if (sourceAspect >= targetAspect)
            {
                canvasW = width / inner;
                canvasH = canvasW / targetAspect;
            }
            else
            {
                canvasH = height / inner;
                canvasW = canvasH * targetAspect;
            }

            // …scaled down as a whole when that would be too large.
            float scale = 1f;
            float largest = Mathf.Max(canvasW, canvasH);
            if (largest > maxSize) scale = maxSize / largest;

            outWidth = Mathf.Max(1, Mathf.RoundToInt(canvasW * scale));
            outHeight = Mathf.Max(1, Mathf.RoundToInt(canvasH * scale));
            int logoW = Mathf.Clamp(Mathf.RoundToInt(width * scale), 1, outWidth);
            int logoH = Mathf.Clamp(Mathf.RoundToInt(height * scale), 1, outHeight);
            int x0 = (outWidth - logoW) / 2;
            int y0 = (outHeight - logoH) / 2;

            var empty = new Color32(background.r, background.g, background.b, keepAlpha ? (byte)0 : (byte)255);
            var result = new Color32[outWidth * outHeight];
            for (int i = 0; i < result.Length; i++) result[i] = empty;

            bool copy = logoW == width && logoH == height;
            for (int y = 0; y < logoH; y++)
            {
                int row = (y0 + y) * outWidth + x0;
                for (int x = 0; x < logoW; x++)
                {
                    Color32 c = copy
                        ? source[y * width + x]
                        : Sample(source, width, height, (x + 0.5f) / logoW, (y + 0.5f) / logoH);
                    result[row + x] = Blend(c, background, keepAlpha);
                }
            }
            return result;
        }

        private static Color32 Blend(Color32 c, Color32 background, bool keepAlpha)
        {
            if (keepAlpha)
                return c.a == 0 ? new Color32(background.r, background.g, background.b, 0) : c;

            if (c.a == 255) return c;
            int a = c.a, na = 255 - a;
            return new Color32(
                (byte)((c.r * a + background.r * na + 127) / 255),
                (byte)((c.g * a + background.g * na + 127) / 255),
                (byte)((c.b * a + background.b * na + 127) / 255),
                255);
        }

        /// <summary>Bilinear sample at normalized (u, v); colours are weighted by alpha so transparent texels do not darken edges.</summary>
        private static Color32 Sample(Color32[] source, int width, int height, float u, float v)
        {
            float fx = Mathf.Clamp(u * width - 0.5f, 0f, width - 1);
            float fy = Mathf.Clamp(v * height - 0.5f, 0f, height - 1);
            int xa = (int)fx, ya = (int)fy;
            int xb = Mathf.Min(xa + 1, width - 1), yb = Mathf.Min(ya + 1, height - 1);
            float tx = fx - xa, ty = fy - ya;

            Color32 c00 = source[ya * width + xa], c10 = source[ya * width + xb];
            Color32 c01 = source[yb * width + xa], c11 = source[yb * width + xb];
            float w00 = (1 - tx) * (1 - ty), w10 = tx * (1 - ty), w01 = (1 - tx) * ty, w11 = tx * ty;

            float a = c00.a * w00 + c10.a * w10 + c01.a * w01 + c11.a * w11;
            if (a <= 0.0001f) return new Color32(0, 0, 0, 0);
            float r = (c00.r * c00.a * w00 + c10.r * c10.a * w10 + c01.r * c01.a * w01 + c11.r * c11.a * w11) / a;
            float g = (c00.g * c00.a * w00 + c10.g * c10.a * w10 + c01.g * c01.a * w01 + c11.g * c11.a * w11) / a;
            float b = (c00.b * c00.a * w00 + c10.b * c10.a * w10 + c01.b * c01.a * w01 + c11.b * c11.a * w11) / a;
            return new Color32((byte)Mathf.RoundToInt(r), (byte)Mathf.RoundToInt(g), (byte)Mathf.RoundToInt(b),
                (byte)Mathf.RoundToInt(a));
        }

        // ───────────── fitted textures (cached) ─────────────

        private static readonly Dictionary<string, Texture2D> fitted = new Dictionary<string, Texture2D>();
        private static int fittedSourceId;

        /// <summary>
        /// The logo fitted to a surface of aspect <paramref name="targetAspect"/>. Appliers that
        /// ask for the same result share one texture; a new logo releases the old ones. Returns
        /// <paramref name="logo"/> itself when it cannot be read (then nothing can be fitted).
        /// </summary>
        public static Texture Fitted(Texture2D logo, float targetAspect, float padding, Color background, bool keepAlpha)
        {
            if (logo == null) return null;
            if (!logo.isReadable)
            {
                Debug.LogWarning("[VR Licensing] Branding logo is not readable: applied as is (cannot be fitted).");
                return logo;
            }

            int id = logo.GetInstanceID();
            if (id != fittedSourceId)
            {
                ReleaseFitted();
                fittedSourceId = id;
            }

            Color32 bg = background;
            float sourceAspect = (float)logo.width / logo.height;
            if (targetAspect <= 0f) targetAspect = sourceAspect;
            string key = Mathf.RoundToInt(targetAspect * 1000f) + "|" + Mathf.RoundToInt(padding * 1000f) + "|" +
                         bg.r + "," + bg.g + "," + bg.b + "|" + (keepAlpha ? "a" : "o");

            Texture2D texture;
            if (fitted.TryGetValue(key, out texture) && texture != null) return texture;

            int w, h;
            Color32[] pixels = Fit(logo.GetPixels32(), logo.width, logo.height, targetAspect, padding, bg, keepAlpha,
                MaxSize, out w, out h);
            texture = new Texture2D(w, h, TextureFormat.RGBA32, true);
            texture.name = "BrandingLogo (fitted " + w + "x" + h + ")";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            fitted[key] = texture;
            return texture;
        }

        /// <summary>Destroys the fitted copies (a new logo arrived, or the app is done with branding).</summary>
        public static void ReleaseFitted()
        {
            foreach (var pair in fitted)
            {
                if (pair.Value == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(pair.Value);
                else UnityEngine.Object.DestroyImmediate(pair.Value);
            }
            fitted.Clear();
            fittedSourceId = 0;
        }
    }
}
