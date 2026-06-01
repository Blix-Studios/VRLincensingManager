using UnityEngine;

namespace VRLicensing
{
    /// <summary>
    /// Applies the client's branding logo to a MeshRenderer's material.
    /// Attach this to any GameObject with a MeshRenderer.
    /// When the branding logo is downloaded at runtime, this component
    /// sets the material's Base Map (_BaseMap for URP/HDRP or _MainTex
    /// for Built-in) to the branding texture.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    [AddComponentMenu("VR Licensing/Branding Mesh Applier")]
    public class BrandingMeshApplier : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("The material property name to set. Use '_BaseMap' for URP/HDRP or '_MainTex' for Built-in RP.")]
        [SerializeField] private string texturePropertyName = "_BaseMap";

        [Tooltip("Material index to modify (0 = first material).")]
        [SerializeField] private int materialIndex = 0;

        [Tooltip("If true, uses a MaterialPropertyBlock instead of modifying the shared material. " +
                 "This avoids creating material instances and is more memory-efficient.")]
        [SerializeField] private bool useMaterialPropertyBlock = true;

        [Tooltip("If true, preserves the original texture when no branding logo is configured.")]
        [SerializeField] private bool keepOriginalIfNoBranding = true;

        private MeshRenderer targetRenderer;
        private Texture originalTexture;
        private LicenseManager licenseManager;
        private MaterialPropertyBlock propertyBlock;

        private void Awake()
        {
            targetRenderer = GetComponent<MeshRenderer>();

            // Cache the original texture
            if (targetRenderer.sharedMaterials.Length > materialIndex)
            {
                var mat = targetRenderer.sharedMaterials[materialIndex];
                if (mat != null && mat.HasProperty(texturePropertyName))
                {
                    originalTexture = mat.GetTexture(texturePropertyName);
                }
            }

            if (useMaterialPropertyBlock)
            {
                propertyBlock = new MaterialPropertyBlock();
            }
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
            if (targetRenderer == null) return;

            if (useMaterialPropertyBlock)
            {
                // Using MaterialPropertyBlock (recommended — no material instances created)
                targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);

                if (texture != null)
                {
                    propertyBlock.SetTexture(texturePropertyName, texture);
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
                    if (mat.HasProperty(texturePropertyName))
                    {
                        mat.SetTexture(texturePropertyName, texture);
                    }
                    else
                    {
                        Debug.LogWarning($"[BrandingMeshApplier] Material on '{gameObject.name}' " +
                            $"does not have property '{texturePropertyName}'.");
                    }
                }
            }

            if (texture != null)
            {
                Debug.Log($"[BrandingMeshApplier] Branding texture applied to '{gameObject.name}' " +
                    $"(property: {texturePropertyName}, index: {materialIndex})");
            }
        }

        /// <summary>
        /// Restores the original texture that was on the material before branding was applied.
        /// </summary>
        public void RestoreOriginal()
        {
            ApplyTexture(originalTexture);
        }
    }
}
