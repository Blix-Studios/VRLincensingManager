using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace VRLicensing
{
    /// <summary>
    /// Privacy "device code" (spec 06-LICENSING-V1.12 §2): the first 10 hex characters of
    /// SHA-256 over the UTF-8 bytes of the device id, uppercase, formatted XXXXX-XXXXX. Shown
    /// at the foot of the license panels so a user can request data deletion without ever
    /// seeing the raw id. The server computes the same expression over device_unique_id, so
    /// the input MUST be exactly the string sent to the RPCs: SystemInfo.deviceUniqueIdentifier.
    /// </summary>
    public static class DeviceCode
    {
        public const int HexLength = 10;

        /// <summary>The raw id every RPC sends as p_device_id.</summary>
        public static string RawDeviceId => SystemInfo.deviceUniqueIdentifier;

        /// <summary>Device code of this headset.</summary>
        public static string Current => Compute(RawDeviceId);

        public static string Compute(string deviceId)
        {
            if (deviceId == null) deviceId = "";
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(deviceId));
                var hex = new StringBuilder(HexLength);
                for (int i = 0; i < hash.Length && hex.Length < HexLength; i++)
                    hex.Append(hash[i].ToString("X2"));
                string h = hex.ToString(0, HexLength);
                return h.Substring(0, 5) + "-" + h.Substring(5, 5);
            }
        }
    }
}
