using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>
    /// CI entry points. CLI: Unity -batchmode -nographics -projectPath . -executeMethod CarWash.Editor.Ci.RunAll -quit -logFile -
    /// In-editor (menu CarWash/CI/...) the same steps run without exiting.
    /// </summary>
    public static class Ci
    {
        /// <summary>Registered build steps, in order. Builders add themselves here as they are implemented.</summary>
        static readonly List<(string name, Action run)> Steps = new List<(string, Action)>
        {
            ("ConfigureProject", SceneBuilder.ConfigureProject),
            ("DataBuilder", DataBuilder.BuildAll),
            ("Art", ArtBuilder.BuildAll),
            ("Prefabs", PrefabBuilder.BuildAll),
            ("Scenes", SceneBuilder.BuildCoreScenes),
            ("SudsyLot", LotBuilder.BuildSudsyLot),
            ("DirtTest", DirtTestBuilder.Build),
            ("BuildSettings", SceneBuilder.RegisterBuildScenes),
        };

        [MenuItem("CarWash/CI/Compile")]
        public static void Compile()
        {
            bool failed = EditorUtility.scriptCompilationFailed;
            Debug.Log(failed ? "[CI] Compile FAILED" : "[CI] Compile OK");
            Finish(failed ? 1 : 0);
        }

        [MenuItem("CarWash/CI/Run All")]
        public static void RunAll()
        {
            if (EditorUtility.scriptCompilationFailed) { Debug.LogError("[CI] Compile FAILED"); Finish(1); return; }
            Debug.Log("[CI] Compile OK");
            foreach (var (name, run) in Steps)
            {
                try { run(); Debug.Log("[CI] step OK: " + name); }
                catch (Exception e) { Debug.LogError($"[CI] step FAILED: {name}\n{e}"); Finish(1); return; }
            }
            int failedTests = RunEditModeTests();
            Debug.Log(failedTests == 0 ? "[CI] RunAll GREEN" : $"[CI] RunAll RED ({failedTests} failed tests)");
            Finish(failedTests == 0 ? 0 : 1);
        }

        /// <summary>Runs EditMode tests synchronously, returns failed count.</summary>
        public static int RunEditModeTests()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var cb = new Callbacks();
            api.RegisterCallbacks(cb);
            try
            {
                api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { "CarWash.Tests.EditMode" } })
                {
                    runSynchronously = true
                });
            }
            finally { api.UnregisterCallbacks(cb); }
            Debug.Log($"[CI] EditMode tests: {cb.Passed} passed, {cb.Failed} failed, {cb.Skipped} skipped");
            return cb.Failed;
        }

        static void Finish(int code)
        {
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        class Callbacks : ICallbacks
        {
            public int Passed, Failed, Skipped;
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void RunFinished(ITestResultAdaptor result) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor r)
            {
                if (r.Test.IsSuite) return;
                switch (r.TestStatus)
                {
                    case TestStatus.Passed: Passed++; break;
                    case TestStatus.Failed: Failed++; Debug.LogError($"[CI] FAIL {r.Test.FullName}: {r.Message}"); break;
                    default: Skipped++; break;
                }
            }
        }
    }
}
