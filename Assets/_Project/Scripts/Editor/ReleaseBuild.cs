using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>
    /// P7 release builds (menu CarWash/Release/...):
    ///  Release        → Builds/Release/CarWashChaos/CarWashChaos.exe — non-development, no autopilot, no dev yard, + README.txt.
    ///  ReleaseVerify  → Builds/ReleaseVerify/CarWashChaos/ — the same player with CARWASH_VERIFY (only the autopilot on)
    ///                   for the automated 2-exe loop check; never shipped.
    /// The zip (Builds/Release/CarWashChaos_v&lt;Version&gt;.zip) is made by the packaging step (Package()).
    /// </summary>
    public static class ReleaseBuild
    {
        public const string Product = "CarWashChaos";
        public const string Version = "0.5";
        public const string ReleaseDir = "Builds/Release/" + Product;
        public const string VerifyDir = "Builds/ReleaseVerify/" + Product;
        public const string ZipPath = "Builds/Release/" + Product + "_v" + Version + ".zip";
        public const string ReadmeSource = "docs/release/README.txt";

        /// <summary>Only the game scenes (no DirtTest / TPS / sample).</summary>
        public static string[] Scenes() => new[] { Core.SceneNames.Bootstrap, Core.SceneNames.MainMenu, Core.SceneNames.SudsyLot }
            .Select(Core.SceneNames.Path).ToArray();

        [MenuItem("CarWash/Release/Build Release (Windows)")]
        public static void BuildRelease() => Build(ReleaseDir, null);

        [MenuItem("CarWash/Release/Build Release-Verify (Windows)")]
        public static void BuildVerify() => Build(VerifyDir, new[] { "CARWASH_VERIFY" });

        static bool Build(string dir, string[] defines)
        {
            PlayerSettings.productName = Product;
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true; // a minimised host must keep simulating for everyone
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            var opts = new BuildPlayerOptions
            {
                scenes = Scenes(),
                locationPathName = dir + "/" + Product + ".exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
                extraScriptingDefines = defines,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[ReleaseBuild] {dir}: {s.result} in {s.totalTime.TotalSeconds:0}s, {s.totalErrors} errors, {s.totalSize / (1024 * 1024)} MB, cloudProjectId='{Application.cloudProjectId}'");
            if (s.result != BuildResult.Succeeded) return false;
            // the burst debug folder is not needed by players
            foreach (var d in Directory.GetDirectories(dir, "*_BurstDebugInformation_DoNotShip")) Directory.Delete(d, true);
            if (File.Exists(ReadmeSource)) File.Copy(ReadmeSource, dir + "/README.txt", true);
            else Debug.LogWarning("[ReleaseBuild] missing " + ReadmeSource);
            return true;
        }
    }
}
