using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEngine;

namespace VRLicensing.Editor
{
    /// <summary>
    /// Keeps the VRL_META_PLATFORM scripting define in sync with whether the Meta XR Platform SDK
    /// (com.meta.xr.sdk.platform) is installed. The package's own VRLicensing.MetaStore assembly is
    /// gated by the same symbol through its asmdef (versionDefines + defineConstraints), so this
    /// sync only matters for project code that wants `#if VRL_META_PLATFORM` and as a visible
    /// signal in Player Settings. Runs on editor load and after every package change.
    /// </summary>
    [InitializeOnLoad]
    public static class MetaPlatformDefineSync
    {
        public const string Define = "VRL_META_PLATFORM";
        public const string PackageName = "com.meta.xr.sdk.platform";

        private static readonly NamedBuildTarget[] Targets =
        {
            NamedBuildTarget.Android,
            NamedBuildTarget.Standalone
        };

        static MetaPlatformDefineSync()
        {
            EditorApplication.delayCall += () => Sync(logWhenUnchanged: false);
            Events.registeredPackages += _ => Sync(logWhenUnchanged: false);
        }

        /// <summary>True when the Meta XR Platform SDK package is part of the project.</summary>
        public static bool IsMetaPlatformInstalled()
        {
            try
            {
                return UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
                    .Any(p => p != null && p.name == PackageName);
            }
            catch (Exception)
            {
                return false;
            }
        }

        [MenuItem("Tools/Blix Studios/Licensing/Sync VRL_META_PLATFORM define")]
        public static void SyncFromMenu() => Sync(logWhenUnchanged: true);

        public static void Sync(bool logWhenUnchanged)
        {
            bool installed = IsMetaPlatformInstalled();
            bool changed = false;

            foreach (var target in Targets)
            {
                string current;
                try { current = PlayerSettings.GetScriptingDefineSymbols(target); }
                catch (Exception) { continue; } // target module not installed

                var defines = current.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(d => d.Trim()).Where(d => d.Length > 0).Distinct().ToList();
                bool has = defines.Contains(Define);

                if (installed && !has) { defines.Add(Define); changed = true; }
                else if (!installed && has) { defines.Remove(Define); changed = true; }
                else continue;

                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
            }

            if (changed)
                Debug.Log($"[VR Licensing] {Define} {(installed ? "added to" : "removed from")} the Android/Standalone " +
                          $"scripting defines ({PackageName} {(installed ? "installed" : "not installed")}).");
            else if (logWhenUnchanged)
                Debug.Log($"[VR Licensing] {Define} already in sync ({PackageName} {(installed ? "installed" : "not installed")}).");
        }
    }

    /// <summary>
    /// Build-time checks for store builds. Warnings for things that silently degrade; a hard
    /// failure for a RELEASE Meta Horizon build without enforceEntitlement (spec §1.4: it must
    /// be ON in every store build, VRC.Quest.Security.1). Development builds only warn.
    /// </summary>
    public class StorePlatformBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            var config = Resources.Load<LicenseConfig>("LicenseConfig");
            if (config == null) return;

            bool development = (report.summary.options & BuildOptions.Development) != 0;

            if (config.storePlatform == StorePlatform.MetaHorizon && !config.enforceEntitlement)
            {
                string msg = "[VR Licensing] RELEASE CHECK: LicenseConfig.storePlatform = MetaHorizon but " +
                             "enforceEntitlement is OFF. A Meta Horizon Store build must lock the app when the " +
                             "app-ownership (entitlement) check fails (VRC.Quest.Security.1). Turn ON " +
                             "'Enforce Entitlement' in Assets/Resources/LicenseConfig.asset (Tools > Blix Studios > " +
                             "Licensing Setup) before building the store release. Development builds only warn.";
                if (development) Debug.LogWarning(msg + " (development build — continuing)");
                else throw new BuildFailedException(msg);
            }

            if (config.storePlatform == StorePlatform.MetaHorizon && !MetaPlatformDefineSync.IsMetaPlatformInstalled())
            {
                Debug.LogWarning("[VR Licensing] BUILD CHECK: LicenseConfig.storePlatform = MetaHorizon but " +
                                 $"{MetaPlatformDefineSync.PackageName} is not installed. The build will behave as " +
                                 "storePlatform = None (no entitlement check, no in-app subscriptions).");
            }

            if (config.storePlatform == StorePlatform.MetaHorizon && string.IsNullOrWhiteSpace(config.storeSubscriptionSku))
                Debug.LogWarning("[VR Licensing] BUILD CHECK: storeSubscriptionSku is empty; the plans column will stay hidden.");

            if (config.storePlatform == StorePlatform.Steam)
                Debug.LogWarning("[VR Licensing] BUILD CHECK: storePlatform = Steam is reserved for phase 2; the build behaves as None.");

            if (config.storePlatform != StorePlatform.None && config.showPurchaseCta)
                Debug.LogWarning("[VR Licensing] BUILD CHECK: showPurchaseCta is ON in a store build. Store policies forbid " +
                                 "off-platform purchase prompts; the MetaHorizon panel ignores it, but turn it OFF to be explicit.");
        }
    }
}
