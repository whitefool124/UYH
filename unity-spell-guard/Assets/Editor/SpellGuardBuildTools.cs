#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using SpellGuard.InputSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpellGuard.EditorTools
{
    public static class SpellGuardBuildTools
    {
        public const string StartScenePath = "Assets/Scenes/SpellGuardStart.unity";
        public const string PrototypeScenePath = "Assets/Scenes/SpellGuardPrototype.unity";
        public const string BuildFolder = "Builds/Windows";
        public const string WindowsBuildPath = BuildFolder + "/SpellGuardDemo.exe";
        private const string MediapipePackageKey = "com.github.homuler.mediapipe";
        private const string ManifestPath = "Packages/manifest.json";

        [MenuItem("Spell Guard/Build/Validate Build Settings")]
        public static void ValidateBuildSettingsMenu()
        {
            var report = ValidateBuildSettings();
            if (report.IsValid)
            {
                Debug.Log(report.Message);
            }
            else
            {
                Debug.LogError(report.Message);
            }
        }

        [MenuItem("Spell Guard/Build/Open Start Scene")]
        public static void OpenStartScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(StartScenePath);
        }

        [MenuItem("Spell Guard/Build/Build Windows Demo")]
        public static void BuildWindowsDemo()
        {
            var report = ValidateBuildSettings();
            if (!report.IsValid)
            {
                Debug.LogError(report.Message);
                return;
            }

            Directory.CreateDirectory(BuildFolder);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { StartScenePath, PrototypeScenePath },
                locationPathName = WindowsBuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var buildReport = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[SpellGuardBuild] Windows demo build finished: {buildReport.summary.result} -> {WindowsBuildPath}");
        }

        [MenuItem("Spell Guard/Build/Open Build Folder")]
        public static void OpenBuildFolder()
        {
            Directory.CreateDirectory(BuildFolder);
            EditorUtility.RevealInFinder(BuildFolder);
        }

        public static SpellGuardBuildValidationReport ValidateBuildSettings()
        {
            var builder = new StringBuilder();
            var isValid = true;
            var scenes = EditorBuildSettings.scenes;

            AppendCheck(builder, scenes.Length >= 2, "Build Settings 至少包含两个场景。", ref isValid);
            AppendCheck(builder, HasSceneAt(scenes, 0, StartScenePath), $"第 1 个场景为 {StartScenePath} 且 enabled。", ref isValid);
            AppendCheck(builder, HasSceneAt(scenes, 1, PrototypeScenePath), $"第 2 个场景为 {PrototypeScenePath} 且 enabled。", ref isValid);
            AppendCheck(builder, AssetDatabase.LoadAssetAtPath<SceneAsset>(StartScenePath) != null, "Start Scene 资产存在。", ref isValid);
            AppendCheck(builder, AssetDatabase.LoadAssetAtPath<SceneAsset>(PrototypeScenePath) != null, "Prototype Scene 资产存在。", ref isValid);
            AppendCheck(builder, ManifestContainsMediapipePackage(), "Packages/manifest.json 包含 MediaPipe Unity 包。", ref isValid);
            AppendCheck(builder, DefaultInputModeIsMock(StartScenePath), "Start Scene 默认输入模式为 Mock。", ref isValid);
            AppendCheck(builder, DefaultInputModeIsMock(PrototypeScenePath), "Prototype Scene 默认输入模式为 Mock。", ref isValid);

            return new SpellGuardBuildValidationReport(isValid, builder.ToString().TrimEnd());
        }

        private static bool HasSceneAt(EditorBuildSettingsScene[] scenes, int index, string expectedPath)
        {
            return scenes.Length > index && scenes[index].enabled && string.Equals(scenes[index].path, expectedPath, StringComparison.Ordinal);
        }

        private static bool ManifestContainsMediapipePackage()
        {
            if (!File.Exists(ManifestPath))
            {
                return false;
            }

            var manifest = File.ReadAllText(ManifestPath);
            return manifest.Contains(MediapipePackageKey) && manifest.Contains(".tgz");
        }

        private static bool DefaultInputModeIsMock(string scenePath)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                return false;
            }

            var wasLoaded = false;
            var scene = SceneManager.GetSceneByPath(scenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                wasLoaded = true;
            }
            else
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }

            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    var routers = root.GetComponentsInChildren<GestureInputRouter>(true);
                    foreach (var router in routers)
                    {
                        var serializedRouter = new SerializedObject(router);
                        var modeProperty = serializedRouter.FindProperty("mode");
                        if (modeProperty != null && modeProperty.enumValueIndex == (int)GestureInputRouter.InputMode.Mock)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            finally
            {
                if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void AppendCheck(StringBuilder builder, bool passed, string label, ref bool isValid)
        {
            builder.Append(passed ? "[PASS] " : "[FAIL] ").AppendLine(label);
            if (!passed)
            {
                isValid = false;
            }
        }
    }

    public readonly struct SpellGuardBuildValidationReport
    {
        public SpellGuardBuildValidationReport(bool isValid, string message)
        {
            IsValid = isValid;
            Message = message;
        }

        public bool IsValid { get; }
        public string Message { get; }
    }
}
#endif
