using System;
using System.IO;
using SpellGuard.InputSystem;
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

        [MenuItem("Spell Guard/Custom Gestures/Import Minimal Dataset To Project Library")]
        public static void ImportMinimalDatasetFromMenu()
        {
            var datasetPath = EditorUtility.OpenFilePanel("Select mined custom gesture dataset", Application.dataPath, "json");
            if (string.IsNullOrWhiteSpace(datasetPath))
            {
                return;
            }

            var reportDirectory = EditorUtility.OpenFolderPanel("Select import report directory", Application.dataPath, string.Empty);
            if (string.IsNullOrWhiteSpace(reportDirectory))
            {
                return;
            }

            var result = ImportDatasetToProjectLibrary(datasetPath, reportDirectory);
            var message = $"Imported {result.ImportedTemplateCount} template(s) into {result.LibraryFolder}.\n" +
                          $"Validated {result.Report.EvaluatedClips} held-out clip(s), correct {result.Report.CorrectClips}, accuracy {result.Report.Accuracy:P1}.";
            EditorUtility.DisplayDialog("Custom Gesture Import", message, "OK");
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

        public static void ImportMinimalDatasetFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var datasetPath = GetArg(args, "-gestureDataset");
            var reportDirectory = GetArg(args, "-gestureOutput");
            if (string.IsNullOrWhiteSpace(datasetPath))
            {
                Debug.LogError("[CustomGestureImport] Missing -gestureDataset <path>.");
                EditorApplication.Exit(2);
                return;
            }

            if (string.IsNullOrWhiteSpace(reportDirectory))
            {
                reportDirectory = "CustomGestureImportReports";
            }

            var result = ImportDatasetToProjectLibrary(datasetPath, reportDirectory);
            Debug.Log($"[CustomGestureImport] dataset={result.Report.DatasetName}, imported={result.ImportedTemplateCount}, library={result.LibraryFolder}, evaluated={result.Report.EvaluatedClips}, correct={result.Report.CorrectClips}, accuracy={result.Report.Accuracy:P1}");
            EditorApplication.Exit(result.ImportedTemplateCount > 0 && result.Report.FalseMatchedClips == 0 && result.Report.MissedClips == 0 ? 0 : 1);
        }

        private static CustomGestureBatchReport Run(string datasetPath, string outputDirectory)
        {
            var report = CustomGestureBatchTester.RunFromFile(datasetPath, outputDirectory);
            Debug.Log($"[CustomGestureBatch] dataset={report.DatasetName}, templates={report.TemplateCount}, evaluated={report.EvaluatedClips}, correct={report.CorrectClips}, missed={report.MissedClips}, falseMatched={report.FalseMatchedClips}, accuracy={report.Accuracy:P1}");
            return report;
        }

        public static CustomGestureImportResult ImportDatasetToProjectLibrary(string datasetPath, string reportDirectory)
        {
            var dataset = CustomGestureBatchDataset.LoadFromFile(datasetPath);
            var options = new CustomGestureBatchOptions();
            var templates = CustomGestureBatchTester.BuildTemplates(dataset, options);
            var library = new CustomGestureLibrary();
            library.LoadAll();
            for (var index = 0; index < templates.Count; index++)
            {
                library.Save(templates[index]);
            }

            var report = CustomGestureBatchTester.Run(dataset, options);
            if (!string.IsNullOrWhiteSpace(reportDirectory))
            {
                Directory.CreateDirectory(reportDirectory);
                File.WriteAllText(Path.Combine(reportDirectory, "custom_gesture_import_summary.json"), JsonUtility.ToJson(new CustomGestureImportResult
                {
                    DatasetPath = datasetPath,
                    LibraryFolder = library.FolderPath,
                    ImportedTemplateCount = templates.Count,
                    Report = report
                }, true));
                File.WriteAllText(Path.Combine(reportDirectory, "custom_gesture_import_validation.csv"), report.ToCsv());
            }

            AssetDatabase.Refresh();
            return new CustomGestureImportResult
            {
                DatasetPath = datasetPath,
                LibraryFolder = library.FolderPath,
                ImportedTemplateCount = templates.Count,
                Report = report
            };
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

    [Serializable]
    public sealed class CustomGestureImportResult
    {
        public string DatasetPath;
        public string LibraryFolder;
        public int ImportedTemplateCount;
        public CustomGestureBatchReport Report;
    }
}
