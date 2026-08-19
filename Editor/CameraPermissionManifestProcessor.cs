#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace VRLicensing.Editor
{
    /// <summary>
    /// Injects the camera permissions the license QR scanner needs into the Gradle
    /// project's AndroidManifest.xml during every Android build.
    ///
    /// This lives in the package (not in each simulator) so that any project that
    /// integrates VR Licensing gets a working "Scan QR" button without having to
    /// maintain a custom manifest by hand. Without these entries the OS never grants
    /// passthrough-camera access, <see cref="LicenseQRScanner"/> reports unsupported,
    /// and the scan buttons silently hide on every headset — including Quest 3.
    ///
    /// Entries added (all idempotent — existing declarations are left untouched):
    ///   • android.permission.CAMERA              — standard Android camera permission
    ///   • horizonos.permission.HEADSET_CAMERA    — HorizonOS v74+ passthrough camera access
    ///   • uses-feature android.hardware.camera / camera2, required="false"
    ///
    /// The uses-feature entries matter for store filtering: declaring the CAMERA
    /// permission makes Android implicitly mark the camera feature as REQUIRED,
    /// which would hide the app from headsets without app-accessible cameras
    /// (e.g. Quest 2). required="false" keeps the app installable everywhere and
    /// lets the scanner degrade at runtime instead.
    /// </summary>
    public class CameraPermissionManifestProcessor : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNs = "http://schemas.android.com/apk/res/android";

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning($"[VR Licensing] AndroidManifest.xml not found at {manifestPath}; " +
                                 "camera permissions for QR scanning were NOT injected.");
                return;
            }

            var doc = new XmlDocument();
            doc.Load(manifestPath);

            XmlElement manifest = doc.DocumentElement;
            if (manifest == null || manifest.Name != "manifest")
            {
                Debug.LogWarning("[VR Licensing] Unexpected AndroidManifest.xml root; skipping injection.");
                return;
            }

            bool changed = false;
            changed |= EnsureElement(doc, manifest, "uses-permission", "android.permission.CAMERA", null);
            changed |= EnsureElement(doc, manifest, "uses-permission", "horizonos.permission.HEADSET_CAMERA", null);
            changed |= EnsureElement(doc, manifest, "uses-feature", "android.hardware.camera", "false");
            changed |= EnsureElement(doc, manifest, "uses-feature", "android.hardware.camera2", "false");

            // Without this feature request HorizonOS shows its system keyboard in a
            // DISABLED state: it renders, but key presses never reach the app and it
            // vanishes on session-state changes (e.g. toggling passthrough). Unity logs
            // "Oculus overlay keyboard is disabled" and license keys become untypeable.
            changed |= EnsureElement(doc, manifest, "uses-feature", "oculus.software.overlay_keyboard", "false");

            if (changed)
            {
                doc.Save(manifestPath);
                Debug.Log("[VR Licensing] Camera permissions for QR scanning injected into AndroidManifest.xml.");
            }
        }

        /// <summary>Adds the element unless an entry with the same android:name already exists.</summary>
        private static bool EnsureElement(XmlDocument doc, XmlElement manifest,
            string tag, string name, string requiredValue)
        {
            foreach (XmlNode node in manifest.GetElementsByTagName(tag))
            {
                var attr = node.Attributes?["name", AndroidNs];
                if (attr != null && attr.Value == name)
                    return false; // already declared (by the app or another plugin) — respect it
            }

            XmlElement element = doc.CreateElement(tag);
            element.SetAttribute("name", AndroidNs, name);
            if (requiredValue != null)
                element.SetAttribute("required", AndroidNs, requiredValue);

            manifest.AppendChild(element);
            return true;
        }
    }
}
#endif
