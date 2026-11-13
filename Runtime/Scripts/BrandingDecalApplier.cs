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
    /// v1.12.0-pre.17: the texture property is found on the material itself (<c>_Base_Map</c>,
    /// <c>Base_Map</c> — URP 17's decal Shader Graph —, <c>_BaseMap</c>, <c>_MainTex</c>), and
    /// the logo is fitted to the projector's width x height: whole logo, aspect ratio kept, the
    /// rest of the decal transparent.
    ///
    /// Uses Reflection to access DecalProjector, so this script does NOT
    /// create a hard dependency on the Universal Render Pipeline package.
    /// </summary>
    [AddComponentMenu("VR Licensing/Branding Decal Applier")]
    public class BrandingDecalApplier : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Preferred material property for the base map texture. When the material does not have it, " +
                 "the applier uses the one it does have: '_Base_Map', 'Base_Map' (URP 17 decal shader), " +
                 "'_BaseMap' or '_MainTex'.")]
        [SerializeField] private string texturePropertyName = "_Base_Map";

        [Tooltip("If true, preserves the original texture when no branding logo is configured.")]
        [SerializeField] private bool keepOriginalIfNoBranding = true;

        [Header("Logo fit (v1.12.0-pre.17)")]
        [Tooltip("ON: the whole logo is shown with its own aspect ratio, centred in the decal; the rest of the " +
                 "decal stays transparent. OFF: the logo is stretched over the decal (behaviour before pre.17).")]
        [SerializeField] private bool fitLogo = true;

        [Tooltip("Margin around the logo, as a fraction of the decal (0 = the logo touches the edge).")]
        [Range(0f, 0.45f)]
        [SerializeField] private float padding = 0f;

        [Tooltip("Width / height of the decal. 0 = taken from the Decal Projector's size.")]
        [SerializeField] private float aspectOverride = 0f;

        private Component decalProjector;
        private PropertyInfo materialProperty;
        private PropertyInfo sizeProperty;
        private Material decalMaterial;
        private Texture originalTexture;
        private string resolvedProperty;
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

            // Cache the 'material' and 'size' properties via reflection
            materialProperty = decalType.GetProperty("material",
                BindingFlags.Public | BindingFlags.Instance);
            sizeProperty = decalType.GetProperty("size",
                BindingFlags.Public | BindingFlags.Instance);

            if (materialProperty == null)
            {
                Debug.LogWarning("[BrandingDecalApplier] Could not find 'material' property on DecalProjector.");
                return;
            }

            // Get the current material and cache the original texture
            decalMaterial = materialProperty.GetValue(decalProjector) as Material;
            resolvedProperty = BrandingTexture.ResolveProperty(decalMaterial, texturePropertyName);

            if (decalMaterial != null && resolvedProperty != null)
            {
                originalTexture = decalMaterial.GetTexture(resolvedProperty);
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

            // Always listen: a later branding answer (or its removal) must reach this decal too.
            licenseManager.OnBrandingLogoReady += HandleBrandingLogoReady;

            // If the logo is already available (late subscriber), apply immediately
            if (licenseManager.BrandingLogoTexture != null)
                ApplyLogo(licenseManager.BrandingLogoTexture);
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
                ApplyLogo(texture);
            }
            else if (!keepOriginalIfNoBranding)
            {
                ApplyTexture(null);
            }
        }

        private void ApplyLogo(Texture2D logo)
        {
            // A decal blends with what is under it: the logo keeps its transparency.
            Texture texture = fitLogo
                ? BrandingTexture.Fitted(logo, DecalAspect(logo), padding, Color.white, keepAlpha: true)
                : logo;
            ApplyTexture(texture);
        }

        /// <summary>Width / height of the projected rectangle.</summary>
        private float DecalAspect(Texture2D logo)
        {
            if (aspectOverride > 0f) return aspectOverride;
            if (sizeProperty != null && decalProjector != null)
            {
                try
                {
                    var size = (Vector3)sizeProperty.GetValue(decalProjector);
                    if (size.x > 0f && size.y > 0f) return size.x / size.y;
                }
                catch (Exception)
                {
                    // fall through to the original texture's shape
                }
            }
            if (originalTexture != null && originalTexture.height > 0)
                return (float)originalTexture.width / originalTexture.height;
            return 0f; // the logo's own aspect
        }

        private void ApplyTexture(Texture texture)
        {
            // Re-fetch the material in case it was changed at runtime
            if (decalProjector != null && materialProperty != null)
            {
                decalMaterial = materialProperty.GetValue(decalProjector) as Material;
            }

            if (decalMaterial == null) return;

            resolvedProperty = BrandingTexture.ResolveProperty(decalMaterial, texturePropertyName);
            if (resolvedProperty != null)
            {
                decalMaterial.SetTexture(resolvedProperty, texture);

                if (texture != null)
                {
                    Debug.Log($"[BrandingDecalApplier] Branding texture applied to decal on '{gameObject.name}' " +
                        $"(property: {resolvedProperty}, {texture.width}x{texture.height})");
                }
            }
            else
            {
                Debug.LogWarning($"[BrandingDecalApplier] Material '{decalMaterial.name}' on '{gameObject.name}' " +
                    $"has none of the texture properties '{texturePropertyName}', " +
                    $"{string.Join(", ", BrandingTexture.KnownProperties)}.");
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
