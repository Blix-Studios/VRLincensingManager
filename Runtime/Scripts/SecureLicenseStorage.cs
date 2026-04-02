using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace VRLicensing
{
    /// <summary>
    /// Encrypted local storage for license data using AES-256.
    /// All values are encrypted and signed with HMAC-SHA256 integrity seal.
    /// The encryption key is derived from the device's unique identifier,
    /// making the stored data non-portable between devices.
    /// </summary>
    public static class SecureLicenseStorage
    {
        private const string LICENSE_DATA_KEY = "vrl_license_data";
        private const string DEMO_USED_KEY = "vrl_demo_used";
        private const string HKT_KEY = "vrl_highest_known_time";
        private const string SESSION_USED_KEY = "vrl_session_used";
        private const string INTEGRITY_SEAL_KEY = "vrl_seal";
        private const string FIRST_RUN_KEY = "vrl_init";

        private static byte[] GetEncryptionKey()
        {
            // Derive a 256-bit key from the device unique identifier
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes(deviceId + "_vrl_salt_2026"));
            }
        }

        private static byte[] GetIV()
        {
            // Fixed IV derived from device ID (acceptable since key is also device-specific)
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            using (var md5 = MD5.Create())
            {
                return md5.ComputeHash(Encoding.UTF8.GetBytes(deviceId + "_vrl_iv"));
            }
        }

        private static byte[] GetHMACKey()
        {
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes(deviceId + "_vrl_hmac_key_2026"));
            }
        }

        #region Integrity Seal

        /// <summary>
        /// Returns true if this is the very first time the app runs on this device.
        /// Uses an encrypted marker that cannot be forged.
        /// </summary>
        public static bool IsFirstRun()
        {
            string marker = PlayerPrefs.GetString(FIRST_RUN_KEY, "");
            if (string.IsNullOrEmpty(marker)) return true;

            try
            {
                string decrypted = Decrypt(marker);
                return decrypted != "initialized";
            }
            catch
            {
                // If decryption fails, marker was tampered — not first run
                return false;
            }
        }

        /// <summary>
        /// Marks the device as initialized (no longer first run).
        /// </summary>
        public static void MarkInitialized()
        {
            string encrypted = Encrypt("initialized");
            PlayerPrefs.SetString(FIRST_RUN_KEY, encrypted);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Verifies the integrity of all stored data using HMAC-SHA256.
        /// Returns false if any value has been modified or deleted externally.
        /// </summary>
        public static bool VerifyIntegrity()
        {
            string storedSeal = PlayerPrefs.GetString(INTEGRITY_SEAL_KEY, "");
            if (string.IsNullOrEmpty(storedSeal))
            {
                // No seal exists — data was wiped or never saved
                return false;
            }

            string computedSeal = ComputeIntegritySeal();
            return storedSeal == computedSeal;
        }

        /// <summary>
        /// Updates the HMAC integrity seal after any write operation.
        /// Must be called after every Save operation.
        /// </summary>
        private static void UpdateIntegritySeal()
        {
            string seal = ComputeIntegritySeal();
            PlayerPrefs.SetString(INTEGRITY_SEAL_KEY, seal);
            // Note: caller must call PlayerPrefs.Save()
        }

        private static string ComputeIntegritySeal()
        {
            // Concatenate all encrypted stored values
            string payload = string.Join("|",
                PlayerPrefs.GetString(LICENSE_DATA_KEY, ""),
                PlayerPrefs.GetString(DEMO_USED_KEY, ""),
                PlayerPrefs.GetString(HKT_KEY, ""),
                PlayerPrefs.GetString(SESSION_USED_KEY, ""),
                PlayerPrefs.GetString(FIRST_RUN_KEY, "")
            );

            byte[] hmacKey = GetHMACKey();
            using (var hmac = new HMACSHA256(hmacKey))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                return Convert.ToBase64String(hash);
            }
        }

        #endregion

        #region License Data

        /// <summary>
        /// Saves license data encrypted to PlayerPrefs.
        /// </summary>
        public static void SaveLicenseData(LicenseData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data);
                string encrypted = Encrypt(json);
                PlayerPrefs.SetString(LICENSE_DATA_KEY, encrypted);
                UpdateIntegritySeal();
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogError($"[VR Licensing] Error saving license data: {e.Message}");
            }
        }

        /// <summary>
        /// Loads and decrypts license data from PlayerPrefs.
        /// Returns null if no data exists or decryption fails.
        /// </summary>
        public static LicenseData LoadLicenseData()
        {
            try
            {
                string encrypted = PlayerPrefs.GetString(LICENSE_DATA_KEY, "");
                if (string.IsNullOrEmpty(encrypted)) return null;

                string json = Decrypt(encrypted);
                return JsonUtility.FromJson<LicenseData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VR Licensing] Error loading license data (may be first run): {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Clears all stored license data and resets demo/session counters.
        /// </summary>
        public static void ClearAll()
        {
            PlayerPrefs.DeleteKey(LICENSE_DATA_KEY);
            PlayerPrefs.DeleteKey(DEMO_USED_KEY);
            PlayerPrefs.DeleteKey(HKT_KEY);
            PlayerPrefs.DeleteKey(SESSION_USED_KEY);
            PlayerPrefs.DeleteKey(INTEGRITY_SEAL_KEY);
            PlayerPrefs.DeleteKey(FIRST_RUN_KEY);
            PlayerPrefs.Save();
        }

        #endregion

        #region Demo Time (Encrypted)

        public static float GetDemoUsedSeconds()
        {
            try
            {
                string encrypted = PlayerPrefs.GetString(DEMO_USED_KEY, "");
                if (string.IsNullOrEmpty(encrypted)) return 0f;
                string decrypted = Decrypt(encrypted);
                return float.TryParse(decrypted, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float val) ? val : 0f;
            }
            catch
            {
                return 0f;
            }
        }

        public static void SetDemoUsedSeconds(float seconds)
        {
            string encrypted = Encrypt(seconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            PlayerPrefs.SetString(DEMO_USED_KEY, encrypted);
            UpdateIntegritySeal();
            PlayerPrefs.Save();
        }

        #endregion

        #region Clock Guard (Encrypted)

        public static long GetHighestKnownTime()
        {
            try
            {
                string encrypted = PlayerPrefs.GetString(HKT_KEY, "");
                if (string.IsNullOrEmpty(encrypted)) return 0;
                string decrypted = Decrypt(encrypted);
                return long.TryParse(decrypted, out long val) ? val : 0;
            }
            catch
            {
                return 0;
            }
        }

        public static void SetHighestKnownTime(long unixTimestamp)
        {
            string encrypted = Encrypt(unixTimestamp.ToString());
            PlayerPrefs.SetString(HKT_KEY, encrypted);
            UpdateIntegritySeal();
            PlayerPrefs.Save();
        }

        #endregion

        #region Session Time (Encrypted)

        public static float GetSessionUsedSeconds()
        {
            try
            {
                string encrypted = PlayerPrefs.GetString(SESSION_USED_KEY, "");
                if (string.IsNullOrEmpty(encrypted)) return 0f;
                string decrypted = Decrypt(encrypted);
                return float.TryParse(decrypted, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float val) ? val : 0f;
            }
            catch
            {
                return 0f;
            }
        }

        public static void SetSessionUsedSeconds(float seconds)
        {
            string encrypted = Encrypt(seconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            PlayerPrefs.SetString(SESSION_USED_KEY, encrypted);
            UpdateIntegritySeal();
            PlayerPrefs.Save();
        }

        #endregion

        #region AES Encryption

        private static string Encrypt(string plainText)
        {
            byte[] key = GetEncryptionKey();
            byte[] iv = GetIV();

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                        cs.Write(plainBytes, 0, plainBytes.Length);
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        private static string Decrypt(string cipherText)
        {
            byte[] key = GetEncryptionKey();
            byte[] iv = GetIV();
            byte[] cipherBytes = Convert.FromBase64String(cipherText);

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(cipherBytes))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var reader = new StreamReader(cs, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        #endregion
    }
}
