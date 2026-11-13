using UnityEngine;

namespace VRLicensing
{
    /// <summary>
    /// Applies the client's branding logo to a MeshRenderer's material.
    /// Attach this to any GameObject with a MeshRenderer.
    /// When the branding logo is downloaded at runtime, this component
    /// sets the material's Base Map (_BaseMap for URP/HDRP or _MainTex
    /// for Built-in) to the branding texture.
    ///
    /// v1.12.0-pre.17: the logo is fitted to the surface (whole logo, aspect ratio kept). On an
    /// opaque material the rest of the surface takes <c>backgroundColor</c> — a transparent PNG no
    /// longer shows black — and on an alpha material it stays transparent. The texture property is
    /// looked up on the material when the configured one does not exist.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    [AddComponentMenu("VR Licensing/Branding Mesh Applier")]
    public class BrandingMeshApplier : MonoBehaviour
    {
        public enum Background
        {
            /// <summary>Transparent on alpha-tested / transparent materials, <c>backgroundColor</c> on opaque ones.</summary>
            Auto,
            /// <summary>Keep the logo's transparency (the material must draw alpha).</summary>
            Transparent,
            /// <summary>Opaque result: the logo over <c>backgroundColor</c>.</summary>
            Color,
        }

        [Header("Settings")]
        [Tooltip("Preferred material property. Use '_BaseMap' for URP/HDRP or '_MainTex' for Built-in RP. When the " +
                 "material does not have it, the applier uses the one it does have ('_Base_Map', 'Base_Map', " +
                 "'_BaseMap', '_MainTex').")]
        [SerializeField] private string texturePropertyName = "_BaseMap";

        [Tooltip("Material index to modify (0 = first material).")]
        [SerializeField] private int materialIndex = 0;

        [Tooltip("If true, uses a MaterialPropertyBlock instead of modifying the shared material. " +
                 "This avoids creating material instances and is more memory-efficient.")]
        [SerializeField] private bool useMaterialPropertyBlock = true;

        [Tooltip("If true, preserves the original texture when no branding logo is configured.")]
        [SerializeField] private bool keepOriginalIfNoBranding = true;

        [Header("Logo fit (v1.12.0-pre.17)")]
        [Tooltip("ON: the whole logo is shown with its own aspect ratio, centred on the surface. " +
                 "OFF: the logo is stretched over the surface (behaviour before pre.17).")]
        [SerializeField] private bool fitLogo = true;

        [Tooltip("Margin around the logo, as a fraction of the surface (0 = the logo touches the edge).")]
        [Range(0f, 0.45f)]
        [SerializeField] private float padding = 0.05f;

        [Tooltip("Width / height of the surface the texture covers. 0 = the shape of the texture the material " +
                 "had before (banners are normally textured with an image of their own shape).")]
        [SerializeField] private float aspectOverride = 0f;

        [Tooltip("What fills the surface around the logo and behind its transparent parts.")]
        [SerializeField] private Background background = Background.Auto;

        [Tooltip("Background colour on opaque materials (and with Background = Color).")]
        [SerializeField] private Color backgroundColor = Color.white;

        private MeshRenderer targetRenderer;
        private Texture originalTexture;
        private string resolvedProperty;
        private LicenseManager licenseManager;
        private MaterialPropertyBlock propertyBlock;

        private void Awake()
        {
            targetRenderer = GetComponent<MeshRenderer>();

            // Cache the original texture
            Material mat = SharedMaterial();
            resolvedProperty = BrandingTexture.ResolveProperty(mat, texturePropertyName);
            if (mat != null && resolvedProperty != null)
            {
                originalTexture = mat.GetTexture(resolvedProperty);
            }

            if (useMaterialPropertyBlock)
            {
                propertyBlock = new MaterialPropertyBlock();
            }
        }

        private Material SharedMaterial()
        {
            if (targetRenderer == null) return null;
            var materials = targetRenderer.sharedMaterials;
            return materialIndex >= 0 && materialIndex < materials.Length ? materials[materialIndex] : null;
        }

        private void Start()
        {
            licenseManager = FindObjectOfType<LicenseManager>();

            if (licenseManager == null)
            {
                Debug.LogWarning("[BrandingMeshApplier] LicenseManager not found in scene. " +
                    "Branding texture will not be applied.");
                return;
            }

            // Always listen: a later branding answer (or its removal) must reach this surface too.
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
            if (!fitLogo)
            {
                ApplyTexture(logo);
                return;
            }

            Material mat = SharedMaterial();
            bool keepAlpha = background == Background.Transparent ||
                             (background == Background.Auto && BrandingTexture.DrawsAlpha(mat));
            float aspect = aspectOverride > 0f
                ? aspectOverride
                : (originalTexture != null && originalTexture.height > 0
                    ? (float)originalTexture.width / originalTexture.height
                    : 0f); // 0 = the logo's own aspect

            ApplyTexture(BrandingTexture.Fitted(logo, aspect, padding, backgroundColor, keepAlpha));
        }

        private void ApplyTexture(Texture texture)
        {
            if (targetRenderer == null) return;

            resolvedProperty = BrandingTexture.ResolveProperty(SharedMaterial(), texturePropertyName);
            if (resolvedProperty == null)
            {
                Debug.LogWarning($"[BrandingMeshApplier] Material {materialIndex} on '{gameObject.name}' has none of " +
                    $"the texture properties '{texturePropertyName}', {string.Join(", ", BrandingTexture.KnownProperties)}.");
                return;
            }

            if (useMaterialPropertyBlock)
            {
                // Using MaterialPropertyBlock (recommended — no material instances created)
                targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);

                if (texture != null)
                {
                    propertyBlock.SetTexture(resolvedProperty, texture);
                }
                else
                {
                    propertyBlock.Clear();
                }

                targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
            }
            else
            {
                // Direct material modification (creates a material instance)
                if (materialIndex < targetRenderer.materials.Length)
                {
                    Material mat = targetRenderer.materials[materialIndex];
                    mat.SetTexture(resolvedProperty, texture);
                }
            }

            if (texture != null)
            {
                Debug.Log($"[BrandingMeshApplier] Branding texture applied to '{gameObject.name}' " +
                    $"(property: {resolvedProperty}, index: {materialIndex}, {texture.width}x{texture.height})");
            }
        }

        /// <summary>
        /// Restores the original texture that was on the material before branding was applied.
        /// </summary>
        public void RestoreOriginal()
        {
            ApplyTexture(useMaterialPropertyBlock ? null : originalTexture);
        }
    }
}
