using NUnit.Framework;
using SpellGuard.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace SpellGuard.Tests.EditMode
{
    public class SpellGuardBuildToolsTests
    {
        private EditorBuildSettingsScene[] originalScenes;

        [SetUp]
        public void SetUp()
        {
            originalScenes = EditorBuildSettings.scenes;
        }

        [TearDown]
        public void TearDown()
        {
            EditorBuildSettings.scenes = originalScenes;
            var startScene = SceneManager.GetSceneByPath(SpellGuardBuildTools.StartScenePath);
            if (startScene.IsValid() && startScene.isLoaded && SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(startScene, true);
            }

            var prototypeScene = SceneManager.GetSceneByPath(SpellGuardBuildTools.PrototypeScenePath);
            if (prototypeScene.IsValid() && prototypeScene.isLoaded && SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(prototypeScene, true);
            }
        }

        [Test]
        public void ValidateBuildSettingsPassesForGeneratedSceneOrder()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SpellGuardBuildTools.StartScenePath, true),
                new EditorBuildSettingsScene(SpellGuardBuildTools.PrototypeScenePath, true)
            };

            var report = SpellGuardBuildTools.ValidateBuildSettings();

            Assert.That(report.IsValid, Is.True, report.Message);
            Assert.That(report.Message, Does.Contain("[PASS] 第 1 个场景"));
            Assert.That(report.Message, Does.Contain("[PASS] 第 2 个场景"));
            Assert.That(report.Message, Does.Contain("MediaPipe"));
            Assert.That(report.Message, Does.Contain("ExternalBridge"));
            Assert.That(report.Message, Does.Contain("Developer Tools Scene 不进入正式构建"));
        }

        [Test]
        public void ValidateBuildSettingsFailsWhenDeveloperToolsSceneIsIncludedInBuild()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SpellGuardBuildTools.StartScenePath, true),
                new EditorBuildSettingsScene(SpellGuardBuildTools.PrototypeScenePath, true),
                new EditorBuildSettingsScene(SpellGuardBuildTools.DeveloperToolsScenePath, true)
            };

            var report = SpellGuardBuildTools.ValidateBuildSettings();

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Message, Does.Contain("[FAIL] Developer Tools Scene 不进入正式构建"));
        }

        [Test]
        public void ValidateBuildSettingsFailsForWrongSceneOrder()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SpellGuardBuildTools.PrototypeScenePath, true),
                new EditorBuildSettingsScene(SpellGuardBuildTools.StartScenePath, true)
            };

            var report = SpellGuardBuildTools.ValidateBuildSettings();

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Message, Does.Contain("[FAIL] 第 1 个场景"));
            Assert.That(report.Message, Does.Contain("[FAIL] 第 2 个场景"));
        }

        [Test]
        public void OpenStartSceneLoadsConfiguredStartScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            SpellGuardBuildTools.OpenStartScene();

            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(SpellGuardBuildTools.StartScenePath));
        }

        [Test]
        public void WindowsBuildPathUsesExpectedDemoFolderAndExecutable()
        {
            Assert.That(SpellGuardBuildTools.BuildFolder, Is.EqualTo("Builds/Windows"));
            Assert.That(SpellGuardBuildTools.WindowsBuildPath, Is.EqualTo("Builds/Windows/SpellGuardDemo.exe"));
        }
    }
}
