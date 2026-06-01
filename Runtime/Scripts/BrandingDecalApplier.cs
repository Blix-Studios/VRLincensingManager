using System;
using System.Reflection;
using UnityEngine;

namespace VRLicensing
{
    /// <summary>
    /// Applies the client's branding logo to a URP Decal Projector's material.
    /// Attach this to any GameObject with a DecalProjector component.
    /// When the branding logo is downloaded at runtime, this component
    /// sets the material's Base Map texture to the branding logo.
    ///
    /// Uses Reflection to access DecalProjector, so this script does NOT
    /// create a hard dependency on the Universal Render Pipeline package.
    /// </summary>
    [AddComponentMenu("VR Licensing/Branding Decal Applier")]
    public class BrandingDecalApplier : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("The material property name for the base map texture. " +
                 "For URP Shader Graphs/Decal this is typically '_Base_Map' or '_BaseMap'.")]
        [SerializeField] private string texturePropertyName = "_Base_Map";

        [Tooltip("If true, preserves the original texture when no branding logo is configured.")]
        [SerializeField] private bool keepOriginalIfNoBranding = true;

        private Component decalProjector;
        private PropertyInfo materialProperty;
        private Material decalMaterial;
        private Texture originalTexture;
        private LicenseManager licenseManager;

        private void Awake()
        {
            // Find the DecalProjector via reflection (avoids URP dependency)
            Type decalType = FindDecalProjectorType();

            if (decalType == null)
            {
                Debug.LogWarning("[BrandingDecalApplier] DecalProjector type not found. " +
                    "Ensure Universal Render Pipeline is installed.");
                return;
            }

            decalProjector = GetComponent(decalType);

            if (decalProjector == null)
            {
                Debug.LogWarning($"[BrandingDecalApplier] No DecalProjector component found on '{gameObject.name}'.");
                return;
            }

            // Cache the 'material' property via reflection
            materialProperty = decalType.GetProperty("material",
                BindingFlags.Public | BindingFlags.Instance);

            if (materialProperty == null)
            {
                Debug.LogWarning("[BrandingDecalApplier] Could not find 'material' property on DecalProjector.");
                return;
            }

            // Get the current material and cache the original texture
            decalMaterial = materialProperty.GetValue(decalProjector) as Material;

            if (decalMaterial != null && decalMaterial.HasProperty(texturePropertyName))
            {
                originalTexture = decalMaterial.GetTexture(texturePropertyName);
            }
        }

        private void Start()
        {
            if (decalProjector == null || materialProperty == null) return;

            licenseManager = FindObjectOfType<LicenseManager>();

            if (licenseManager == null)
            {
                Debug.LogWarning("[BrandingDecalApplier] LicenseManager not found in scene. " +
                    "Branding texture will not be applied.");
                return;
            }

            // If the logo is already available (late subscriber), apply immediately
            if (licenseManager.BrandingLogoTexture != null)
            {
                ApplyTexture(licenseManager.BrandingLogoTexture);
                return;
            }

            // Otherwise subscribe to the event
            licenseManager.OnBrandingLogoReady += HandleBrandingLogoReady;
        }

        private void OnDestroy()
        {
            if (licenseManager != null)
            {
                licenseManager.OnBrandingLogoReady -= HandleBrandingLogoReady;
            }
        }

        private void HandleBrandingLogoReady(Texture2D texture, Sprite sprite)
        {
            if (texture != null)
            {
                ApplyTexture(texture);
            }
            else if (!keepOriginalIfNoBranding)
            {
                ApplyTexture(null);
            }
        }

        private void ApplyTexture(Texture texture)
        {
            // Re-fetch the material in case it was changed at runtime
            if (decalProjector != null && materialProperty != null)
            {
                decalMaterial = materialProperty.GetValue(decalProjector) as Material;
            }

            if (decalMaterial == null) return;

            if (decalMaterial.HasProperty(texturePropertyName))
            {
                decalMaterial.SetTexture(texturePropertyName, texture);

                if (texture != null)
                {
                    Debug.Log($"[BrandingDecalApplier] Branding texture applied to decal on '{gameObject.name}' " +
                        $"(property: {texturePropertyName})");
                }
            }
            else
            {
                Debug.LogWarning($"[BrandingDecalApplier] Material on '{gameObject.name}' " +
                    $"does not have property '{texturePropertyName}'. " +
                    $"Try '_BaseMap' or '_Base_Map' depending on your shader.");
            }
        }

        /// <summary>
        /// Restores the original texture that was on the decal material before branding was applied.
        /// </summary>
        public void RestoreOriginal()
        {
            ApplyTexture(originalTexture);
        }

        /// <summary>
        /// Finds the DecalProjector type via reflection from multiple possible assembly locations.
        /// </summary>
        private static Type FindDecalProjectorType()
        {
            // URP 12+ (Unity 2021.2+)
            Type t = Type.GetType("UnityEngine.Rendering.Universal.DecalProjector, Unity.RenderPipelines.Universal.Runtime");
            if (t != null) return t;

            // Older URP versions
            t = Type.GetType("UnityEngine.Rendering.Universal.DecalProjector, UnityEngine.Rendering.Universal");
            if (t != null) return t;

            // Fallback: search all loaded assemblies
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType("UnityEngine.Rendering.Universal.DecalProjector");
                if (t != null) return t;
            }

            return null;
        }
    }
}
