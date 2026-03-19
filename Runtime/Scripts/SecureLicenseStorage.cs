using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace VRLicensing
{
    /// <summary>
    /// Encrypted local storage for license data using AES-256.
    /// The encryption key is derived from the device's unique identifier,
    /// making the stored data non-portable between devices.
    /// </summary>
    public static class SecureLicenseStorage
    {
        private const string LICENSE_DATA_KEY = "vrl_license_data";
        private const string DEMO_USED_KEY = "vrl_demo_used";
        private const string HKT_KEY = "vrl_highest_known_time";
        private const string SESSION_USED_KEY = "vrl_session_used";

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
            PlayerPrefs.Save();
        }

        #endregion

        #region Demo Time

        public static float GetDemoUsedSeconds()
        {
            return PlayerPrefs.GetFloat(DEMO_USED_KEY, 0f);
        }

        public static void SetDemoUsedSeconds(float seconds)
        {
            PlayerPrefs.SetFloat(DEMO_USED_KEY, seconds);
            PlayerPrefs.Save();
        }

        #endregion

        #region Clock Guard

        public static long GetHighestKnownTime()
        {
            string stored = PlayerPrefs.GetString(HKT_KEY, "0");
            return long.TryParse(stored, out long val) ? val : 0;
        }

        public static void SetHighestKnownTime(long unixTimestamp)
        {
            PlayerPrefs.SetString(HKT_KEY, unixTimestamp.ToString());
            PlayerPrefs.Save();
        }

        #endregion

        #region Session Time

        public static float GetSessionUsedSeconds()
        {
            return PlayerPrefs.GetFloat(SESSION_USED_KEY, 0f);
        }

        public static void SetSessionUsedSeconds(float seconds)
        {
            PlayerPrefs.SetFloat(SESSION_USED_KEY, seconds);
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
