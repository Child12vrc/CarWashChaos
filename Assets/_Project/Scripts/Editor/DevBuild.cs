using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>Development Windows build used as the 2nd player for network verification (run with -join 127.0.0.1 -autotest).</summary>
    public static class DevBuild
    {
        public const string Output = "Builds/Dev/CarWash.exe";

        [MenuItem("CarWash/CI/Dev Build DirtTest (Windows)")]
        public static void BuildDirtTest()
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Core.SceneNames.Path(Core.SceneNames.DirtTest) },
                locationPathName = "Builds/DirtTest/DirtTest.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            });
            Debug.Log($"[DevBuild] DirtTest {report.summary.result} in {report.summary.totalTime.TotalSeconds:0}s");
        }

        [MenuItem("CarWash/CI/Dev Build (Windows)")]
        public static void BuildWindowsDev()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled && s.path.Contains("/_Project/Scenes/") && !s.path.Contains("TPS_")).Select(s => s.path).ToArray();
            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[DevBuild] {s.result} in {s.totalTime.TotalSeconds:0}s, {s.totalErrors} errors → {Output}");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
