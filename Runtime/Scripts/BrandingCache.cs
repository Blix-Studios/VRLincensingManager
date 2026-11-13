using System;
using System.IO;
using UnityEngine;

namespace VRLicensing
{
    /// <summary>What came back from <c>get_license_branding</c>.</summary>
    public enum BrandingAnswer
    {
        /// <summary>No usable answer: no network, HTTP error, function missing, rate limited, unreadable body.</summary>
        Failed,
        /// <summary>The server answered: either a branding or "none configured".</summary>
        Answered,
    }

    /// <summary>
    /// v1.12.0-pre.17 - the client's branding as last answered by the server, kept on the device
    /// (brand name and logo URL in PlayerPrefs, the logo file under persistentDataPath) so a
    /// launch whose branding call fails still shows it. This replaces the old fallback that read
    /// the <c>license_branding</c> table with the anon key: the app only ever calls the RPC, and
    /// when the RPC fails it keeps what it has, or shows no branding.
    /// Branding is not a secret (a logo and a company name), so it is stored in the clear.
    /// </summary>
    public static class BrandingCache
    {
        private const string DataKey = "VRL_Branding_";
        private const string LogoUrlKey = "VRL_BrandingLogoUrl_";

        /// <summary>
        /// The branding to show. The server's answer always wins (a branding, or none); only when
        /// there is no answer does the cached one stand in.
        /// </summary>
        public static BrandingData Resolve(BrandingAnswer answer, BrandingData fresh, BrandingData cached)
        {
            BrandingData chosen = answer == BrandingAnswer.Answered ? fresh : cached;
            return chosen != null && chosen.HasBranding ? chosen : null;
        }

        /// <summary>
        /// Reads the body of a successful <c>get_license_branding</c> call. True = the server
        /// answered (<paramref name="branding"/> null when none is configured). False = not an
        /// answer about branding: an <c>error</c> member ("Too many requests.", "License not valid
        /// for this product and device.") or a body that cannot be read — the caller keeps its cache.
        /// </summary>
        public static bool TryParseAnswer(string body, out BrandingData branding, out string error)
        {
            branding = null;
            error = null;

            string text = body == null ? "" : body.Trim();
            if (text.Length == 0 || text == "null" || text == "{}") return true;

            RpcBrandingResult result;
            try
            {
                result = JsonUtility.FromJson<RpcBrandingResult>(text);
            }
            catch (Exception e)
            {
                error = "unreadable answer: " + e.Message;
                return false;
            }

            if (result == null)
            {
                error = "unreadable answer";
                return false;
            }
            if (!string.IsNullOrEmpty(result.error))
            {
                error = result.error;
                return false;
            }
            if (string.IsNullOrEmpty(result.brandName) && string.IsNullOrEmpty(result.logoUrl)) return true;

            branding = new BrandingData { brand_name = result.brandName, logo_url = result.logoUrl };
            return true;
        }

        // ───────────── storage ─────────────

        public static void Save(int productId, BrandingData branding)
        {
            if (branding == null || !branding.HasBranding)
            {
                Clear(productId);
                return;
            }
            PlayerPrefs.SetString(DataKey + productId, JsonUtility.ToJson(new Stored
            {
                brand_name = branding.brand_name ?? "",
                logo_url = branding.logo_url ?? "",
            }));
            // A different logo than the one on disk: that file no longer belongs to this branding.
            if (PlayerPrefs.GetString(LogoUrlKey + productId, "") != (branding.logo_url ?? "")) DeleteLogo(productId);
            PlayerPrefs.Save();
        }

        public static BrandingData Load(int productId)
        {
            string json = PlayerPrefs.GetString(DataKey + productId, "");
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var stored = JsonUtility.FromJson<Stored>(json);
                if (stored == null) return null;
                var data = new BrandingData { brand_name = stored.brand_name, logo_url = stored.logo_url };
                return data.HasBranding ? data : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Clear(int productId)
        {
            PlayerPrefs.DeleteKey(DataKey + productId);
            DeleteLogo(productId);
            PlayerPrefs.Save();
        }

        /// <summary>Keeps the downloaded logo file (PNG/JPG bytes as served) for <paramref name="logoUrl"/>.</summary>
        public static void SaveLogo(int productId, string logoUrl, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || string.IsNullOrEmpty(logoUrl)) return;
            try
            {
                File.WriteAllBytes(LogoPath(productId), bytes);
                PlayerPrefs.SetString(LogoUrlKey + productId, logoUrl);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VR Licensing] Could not cache the branding logo: " + e.Message);
            }
        }

        /// <summary>The cached logo file for exactly this URL, or null.</summary>
        public static byte[] LoadLogo(int productId, string logoUrl)
        {
            if (string.IsNullOrEmpty(logoUrl) || PlayerPrefs.GetString(LogoUrlKey + productId, "") != logoUrl) return null;
            try
            {
                string path = LogoPath(productId);
                return File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void DeleteLogo(int productId)
        {
            PlayerPrefs.DeleteKey(LogoUrlKey + productId);
            try
            {
                string path = LogoPath(productId);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception)
            {
                // nothing to do: the URL marker is gone, so the file is never read again
            }
        }

        private static string LogoPath(int productId)
        {
            return Path.Combine(Application.persistentDataPath, "vrl_branding_logo_" + productId + ".bin");
        }

        [Serializable]
        private class Stored
        {
            public string brand_name;
            public string logo_url;
        }
    }
}
