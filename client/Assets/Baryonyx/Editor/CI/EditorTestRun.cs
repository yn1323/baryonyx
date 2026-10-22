using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Baryonyx.Editor.CI
{
    // Starts test runs for client/ci/run-editor-tests.py through the Unity CLI eval.
    // Saving the open scenes and starting the run happen in one call, so the Test Framework never
    // finds a modified scene and never shows its save dialog, which blocks the Editor until someone answers.
    [InitializeOnLoad]
    public static class EditorTestRun
    {
        public const string ResultPath = "Temp/editor-test-run.json";
        private const string RunIdKey = "Baryonyx.EditorTestRun.Id";
        private const string GuidKey = "Baryonyx.EditorTestRun.Guid";
        private static readonly Reporter reporter = new Reporter();
        private static TestRunnerApi api;

        static EditorTestRun()
        {
            // Callbacks do not survive a domain reload; register again while a run is in flight.
            if (!string.IsNullOrEmpty(SessionState.GetString(RunIdKey, "")))
                EditorApplication.delayCall += Register;
        }

        // Returns "started", or "error: <reason>" without starting anything.
        public static string Start(string runId, string mode, string assembly, string pattern)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "error: play mode is still on";
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return "error: the Editor is compiling or importing";
            if (!TryParseMode(mode, out var testMode))
                return $"error: unknown mode {mode}";

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty && string.IsNullOrEmpty(scene.path))
                    return "error: an untitled scene has unsaved changes; save or discard it by hand first";
            }
            if (!EditorSceneManager.SaveOpenScenes())
                return "error: the open scenes could not be saved";

            var filter = new Filter { testMode = testMode, assemblyNames = new[] { assembly } };
            if (!string.IsNullOrEmpty(pattern))
                filter.groupNames = new[] { pattern };

            Register();
            SessionState.SetString(RunIdKey, runId);
            Write(new RunResult { id = runId, status = "starting" });
            SessionState.SetString(GuidKey, api.Execute(new ExecutionSettings(filter)));
            return "started";
        }

        public static string Cancel()
        {
            var guid = SessionState.GetString(GuidKey, "");
            if (!string.IsNullOrEmpty(guid))
                TestRunnerApi.CancelTestRun(guid);
            Forget();
            if (EditorApplication.isPlaying)
                EditorApplication.ExitPlaymode();
            return "cancelled";
        }

        private static bool TryParseMode(string mode, out TestMode testMode)
        {
            testMode = mode == "editmode" ? TestMode.EditMode : TestMode.PlayMode;
            return mode == "editmode" || mode == "playmode";
        }

        private static void Register()
        {
            if (api != null)
                return;
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(reporter);
        }

        private static void Forget()
        {
            SessionState.EraseString(RunIdKey);
            SessionState.EraseString(GuidKey);
        }

        private static void Write(RunResult result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            var temporary = ResultPath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(result));
            if (File.Exists(ResultPath))
                File.Replace(temporary, ResultPath, null);
            else
                File.Move(temporary, ResultPath);
        }

        // Registered for every run in the Editor; reports only the run this class started.
        private sealed class Reporter : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                var runId = SessionState.GetString(RunIdKey, "");
                if (string.IsNullOrEmpty(runId))
                    return;
                var count = testsToRun.TestCaseCount;
                Debug.Log($"[EditorTestRun] Run started: {count} test(s)");
                Write(
                    new RunResult
                    {
                        id = runId,
                        status = "started",
                        started = count,
                    }
                );
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var runId = SessionState.GetString(RunIdKey, "");
                if (string.IsNullOrEmpty(runId))
                    return;
                var finished = new RunResult
                {
                    id = runId,
                    status = "finished",
                    passed = result.PassCount,
                    failed = result.FailCount,
                    skipped = result.SkipCount + result.InconclusiveCount,
                };
                finished.total = finished.passed + finished.failed + finished.skipped;
                Collect(result, finished.tests);
                Debug.Log(
                    $"[EditorTestRun] Run finished: {finished.total} total, {finished.passed} passed, {finished.failed} failed"
                );
                Write(finished);
                Forget();
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result) { }

            private static void Collect(ITestResultAdaptor result, List<TestResult> tests)
            {
                if (result.Test.IsSuite)
                {
                    foreach (var child in result.Children)
                        Collect(child, tests);
                    return;
                }
                var passed = result.TestStatus == TestStatus.Passed;
                tests.Add(
                    new TestResult
                    {
                        name = result.FullName,
                        status = result.TestStatus.ToString(),
                        message = passed ? "" : result.Message ?? "",
                    }
                );
            }
        }

        [Serializable]
        private sealed class RunResult
        {
            public string id;
            public string status;
            public int started;
            public int total;
            public int passed;
            public int failed;
            public int skipped;
            public List<TestResult> tests = new List<TestResult>();
        }

        [Serializable]
        private sealed class TestResult
        {
            public string name;
            public string status;
            public string message;
        }
    }
}
