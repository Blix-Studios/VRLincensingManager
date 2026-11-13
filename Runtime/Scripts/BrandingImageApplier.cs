using UnityEngine;
using UnityEngine.UI;

namespace VRLicensing
{
    /// <summary>
    /// Applies the client's branding logo to a UI Image component.
    /// Attach this to any GameObject with an Image component.
    /// When the branding logo is downloaded at runtime, this component
    /// automatically replaces the Image's sprite with the branding logo.
    /// v1.12.0-pre.17: the logo keeps its aspect ratio inside the Image's rectangle (fit, no
    /// crop, no stretch); its transparency is drawn as such by the UI.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("VR Licensing/Branding Image Applier")]
    public class BrandingImageApplier : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("If true, preserves the original Image sprite when no branding logo is configured.")]
        [SerializeField] private bool keepOriginalIfNoBranding = true;

        [Tooltip("If true, calls SetNativeSize() after applying the branding sprite.")]
        [SerializeField] private bool useNativeSize = false;

        [Tooltip("ON (default): the logo keeps its aspect ratio inside this Image's rectangle - the whole logo is " +
                 "visible, nothing is cropped or stretched. OFF: the logo fills the rectangle (behaviour before pre.17).")]
        [SerializeField] private bool preserveAspect = true;

        private Image targetImage;
        private Sprite originalSprite;
        private bool originalPreserveAspect;
        private Image.Type originalType;
        private LicenseManager licenseManager;

        private void Awake()
        {
            targetImage = GetComponent<Image>();
            originalSprite = targetImage.sprite;
            originalPreserveAspect = targetImage.preserveAspect;
            originalType = targetImage.type;
        }

        private void Start()
        {
            licenseManager = FindObjectOfType<LicenseManager>();

            if (licenseManager == null)
            {
                Debug.LogWarning("[BrandingImageApplier] LicenseManager not found in scene. " +
                    "Branding logo will not be applied.");
                return;
            }

            // Always listen: a later branding answer (or its removal) must reach this Image too.
            licenseManager.OnBrandingLogoReady += HandleBrandingLogoReady;

            // If the logo is already available (late subscriber), apply immediately
            if (licenseManager.BrandingLogoSprite != null)
                ApplySprite(licenseManager.BrandingLogoSprite);
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
            if (sprite != null)
            {
                ApplySprite(sprite);
            }
            else if (!keepOriginalIfNoBranding)
            {
                // Clear the image if no branding is configured
                targetImage.sprite = null;
                targetImage.enabled = false;
            }
        }

        private void ApplySprite(Sprite sprite)
        {
            if (targetImage == null) return;

            targetImage.sprite = sprite;
            targetImage.enabled = true;
            if (preserveAspect)
            {
                // Sliced / tiled / filled types ignore preserveAspect: a logo is a plain picture.
                targetImage.type = Image.Type.Simple;
                targetImage.preserveAspect = true;
            }

            if (useNativeSize)
            {
                targetImage.SetNativeSize();
            }

            Debug.Log($"[BrandingImageApplier] Logo applied to '{gameObject.name}'");
        }

        /// <summary>
        /// Restores the original sprite that was on the Image before branding was applied.
        /// </summary>
        public void RestoreOriginal()
        {
            if (targetImage != null)
            {
                targetImage.sprite = originalSprite;
                targetImage.type = originalType;
                targetImage.preserveAspect = originalPreserveAspect;
                targetImage.enabled = originalSprite != null;
            }
        }
    }
}
