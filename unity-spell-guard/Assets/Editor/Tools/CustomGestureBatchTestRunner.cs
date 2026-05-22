using System;
using SpellGuard.Tools;
using UnityEditor;
using UnityEngine;

namespace SpellGuard.EditorTools
{
    public static class CustomGestureBatchTestRunner
    {
        [MenuItem("Spell Guard/Custom Gestures/Run Batch Test From Json")]
        public static void RunFromMenu()
        {
            var datasetPath = EditorUtility.OpenFilePanel("Select custom gesture batch dataset", Application.dataPath, "json");
            if (string.IsNullOrWhiteSpace(datasetPath))
            {
                return;
            }

            var outputDirectory = EditorUtility.OpenFolderPanel("Select output directory", Application.dataPath, string.Empty);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                return;
            }

            Run(datasetPath, outputDirectory);
        }

        public static void RunFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var datasetPath = GetArg(args, "-gestureDataset");
            var outputDirectory = GetArg(args, "-gestureOutput");
            if (string.IsNullOrWhiteSpace(datasetPath))
            {
                Debug.LogError("[CustomGestureBatch] Missing -gestureDataset <path>.");
                EditorApplication.Exit(2);
                return;
            }

            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                outputDirectory = "CustomGestureBatchReports";
            }

            var report = Run(datasetPath, outputDirectory);
            EditorApplication.Exit(report.FalseMatchedClips == 0 && report.MissedClips == 0 ? 0 : 1);
        }

        private static CustomGestureBatchReport Run(string datasetPath, string outputDirectory)
        {
            var report = CustomGestureBatchTester.RunFromFile(datasetPath, outputDirectory);
            Debug.Log($"[CustomGestureBatch] dataset={report.DatasetName}, templates={report.TemplateCount}, evaluated={report.EvaluatedClips}, correct={report.CorrectClips}, missed={report.MissedClips}, falseMatched={report.FalseMatchedClips}, accuracy={report.Accuracy:P1}");
            return report;
        }

        private static string GetArg(string[] args, string name)
        {
            if (args == null)
            {
                return null;
            }

            for (var index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return null;
        }
    }
}
