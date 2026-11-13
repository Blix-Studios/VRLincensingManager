using NUnit.Framework;
using UnityEngine;

namespace VRLicensing.Tests
{
    /// <summary>
    /// v1.12.0-pre.17: when the host does not set LicenseManager.AppVersion the package reports
    /// "&lt;Application.version&gt; (&lt;versionCode of the installed APK&gt;)" by itself.
    /// </summary>
    public class AppVersionTests
    {
        private string before;

        [SetUp] public void SetUp() { before = LicenseManager.AppVersion; }
        [TearDown] public void TearDown() { LicenseManager.AppVersion = before; }

        [Test]
        public void Compose_VersionAndCode()
        {
            Assert.AreEqual("1.0.0 (31)", LicenseBuildInfo.Compose("1.0.0", 31));
            Assert.AreEqual("1.0 (7)", LicenseBuildInfo.Compose(" 1.0 ", 7));
        }

        [Test]
        public void Compose_NoCode_IsJustTheVersion()
        {
            Assert.AreEqual("1.0.0", LicenseBuildInfo.Compose("1.0.0", 0));
            Assert.AreEqual("1.0.0", LicenseBuildInfo.Compose("1.0.0", -1));
            Assert.AreEqual("(31)", LicenseBuildInfo.Compose("", 31));
            Assert.AreEqual("", LicenseBuildInfo.Compose(null, 0));
        }

        [Test]
        public void Compose_IsCappedAtTheServerLimit()
        {
            string text = LicenseBuildInfo.Compose(new string('9', 60), 123456);
            Assert.AreEqual(LicenseBuildInfo.MaxLength, text.Length);
            Assert.AreEqual(AppFeedback.MaxAppVersionLength, LicenseBuildInfo.MaxLength, "same cap as the feedback RPC");
        }

        [Test]
        public void HostNotSet_ThePackageDefaultIsReported()
        {
            LicenseManager.AppVersion = null;
            Assert.AreEqual(LicenseBuildInfo.DefaultAppVersion, LicenseManager.EffectiveAppVersion);
            LicenseManager.AppVersion = "  ";
            Assert.AreEqual(LicenseBuildInfo.DefaultAppVersion, LicenseManager.EffectiveAppVersion);

            // In the Editor there is no installed APK: version without a code.
            Assert.AreEqual(0, LicenseBuildInfo.InstalledVersionCode());
            Assert.AreEqual(LicenseBuildInfo.Compose(Application.version, 0), LicenseBuildInfo.DefaultAppVersion);
        }

        [Test]
        public void HostValue_Wins()
        {
            LicenseManager.AppVersion = "2.3.4 (99)";
            Assert.AreEqual("2.3.4 (99)", LicenseManager.EffectiveAppVersion);
            Assert.AreEqual("2.3.4 (99)", LicenseBuildInfo.Effective("2.3.4 (99)"));
        }
    }
}
