using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace WhatLightRemains.Editor
{
    /// <summary>
    /// Lets the checked-in test suites be run from the already-open editor. This is useful
    /// when the project lock prevents a second batch-mode Unity process from opening it.
    /// Results are written in NUnit XML beneath TestResults.
    /// </summary>
    public static class FoundationTestRunner
    {
        [MenuItem("Tools/What Light Remains/Run EditMode Tests", priority = 30)]
        public static void RunEditModeTests()
        {
            Run(TestMode.EditMode, "EditMode");
        }

        [MenuItem("Tools/What Light Remains/Run PlayMode Tests", priority = 31)]
        public static void RunPlayModeTests()
        {
            Run(TestMode.PlayMode, "PlayMode");
        }

        private static void Run(TestMode mode, string label)
        {
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "../TestResults")));
            FoundationTestCallbacks callbacks = ScriptableObject.CreateInstance<FoundationTestCallbacks>();
            callbacks.hideFlags = HideFlags.HideAndDontSave;
            callbacks.Initialize(label, $"TestResults/{label}.xml");
            TestRunnerApi api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.hideFlags = HideFlags.HideAndDontSave;
            api.RegisterCallbacks(callbacks);
            string jobId = api.Execute(new ExecutionSettings(new Filter
            {
                testMode = mode,
                assemblyNames = new[] { $"WhatLightRemains.Tests.{label}" },
            }));
            Debug.Log($"What Light Remains {label} test run started ({jobId}).");
        }

        private sealed class FoundationTestCallbacks : ScriptableObject, ICallbacks
        {
            private string label;
            private string resultPath;

            public void Initialize(string runLabel, string xmlPath)
            {
                label = runLabel;
                resultPath = xmlPath;
            }

            public void RunStarted(ITestAdaptor testsToRun) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result, resultPath);
                Debug.Log($"WLR_TEST_RESULT {label}: passed={result.PassCount}, failed={result.FailCount}, "
                    + $"skipped={result.SkipCount}, inconclusive={result.InconclusiveCount}, duration={result.Duration:F3}s, "
                    + $"xml={resultPath}");
                TestRunnerApi.UnregisterTestCallback(this);
                EditorApplication.delayCall += () =>
                {
                    if (this != null) DestroyImmediate(this);
                };
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.HasChildren && result.FailCount > 0)
                {
                    Debug.LogError($"WLR_TEST_FAILURE {result.FullName}: {result.Message}\n{result.StackTrace}");
                }
            }
        }
    }
}
