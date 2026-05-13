using NUnit.Framework;
using SpellGuard.Combat;
using SpellGuard.Core;
using SpellGuard.InputSystem;
using UnityEngine;

namespace SpellGuard.Tests.PlayMode
{
    public class LevelConfigTests
    {
        private GameObject root;
        private SpellGuardFlowController flowController;
        private EnemySpawner spawner;
        private PlayerHealth health;
        private GameFlowManager gameFlow;
        private LevelConfig tutorialLevel;
        private LevelConfig combatLevel;
        private LevelConfigLibrary library;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("LevelConfigTestsRoot");
            var inputProvider = root.AddComponent<TrackingInputProvider>();
            health = root.AddComponent<PlayerHealth>();
            spawner = root.AddComponent<EnemySpawner>();
            gameFlow = root.AddComponent<GameFlowManager>();
            flowController = root.AddComponent<SpellGuardFlowController>();

            tutorialLevel = ScriptableObject.CreateInstance<LevelConfig>();
            SetPrivateField(tutorialLevel, "levelId", "tutorial_test");
            SetPrivateField(tutorialLevel, "targetScore", 2);
            SetPrivateField(tutorialLevel, "playerHealth", 4);
            SetPrivateField(tutorialLevel, "spawnEnemies", false);
            SetPrivateField(tutorialLevel, "tutorialHint", "测试教学提示");

            combatLevel = ScriptableObject.CreateInstance<LevelConfig>();
            SetPrivateField(combatLevel, "levelId", "combat_test");
            SetPrivateField(combatLevel, "targetScore", 7);
            SetPrivateField(combatLevel, "playerHealth", 9);
            SetPrivateField(combatLevel, "spawnEnemies", true);
            SetPrivateField(combatLevel, "wave", new WaveConfig
            {
                SpawnInterval = 1.25f,
                MaxAliveEnemies = 3,
                SpawnRadius = 11f,
                Enemy = new EnemyConfig
                {
                    Speed = 3.4f,
                    HitPoints = 5,
                    AttackDistance = 1.8f
                }
            });

            library = ScriptableObject.CreateInstance<LevelConfigLibrary>();
            SetPrivateField(library, "tutorialLevel", tutorialLevel);
            SetPrivateField(library, "combatLevel", combatLevel);

            SetPrivateField(flowController, "inputProvider", inputProvider);
            SetPrivateField(flowController, "playerHealth", health);
            SetPrivateField(flowController, "enemySpawner", spawner);
            SetPrivateField(flowController, "gameFlow", gameFlow);
            SetPrivateField(flowController, "levelConfigLibrary", library);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(library);
            Object.DestroyImmediate(combatLevel);
            Object.DestroyImmediate(tutorialLevel);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void StartRunAppliesCombatLevelConfig()
        {
            flowController.StartRun();

            Assert.That(flowController.CurrentLevelConfig, Is.SameAs(combatLevel));
            Assert.That(gameFlow.TargetScoreToWin, Is.EqualTo(7));
            Assert.That(health.MaxHealth, Is.EqualTo(9));
            Assert.That(health.CurrentHealth, Is.EqualTo(9));
            Assert.That(spawner.SpawnInterval, Is.EqualTo(1.25f));
            Assert.That(spawner.MaxAliveEnemies, Is.EqualTo(3));
            Assert.That(spawner.SpawnRadius, Is.EqualTo(11f));
            Assert.That(spawner.EnemyConfig.HitPoints, Is.EqualTo(5));
        }

        [Test]
        public void StartTrainingAppliesTutorialLevelConfig()
        {
            flowController.StartTraining();

            Assert.That(flowController.CurrentLevelConfig, Is.SameAs(tutorialLevel));
            Assert.That(gameFlow.TargetScoreToWin, Is.EqualTo(2));
            Assert.That(health.MaxHealth, Is.EqualTo(4));
            Assert.That(flowController.HintText, Is.EqualTo("测试教学提示"));
        }

        [Test]
        public void LevelConfigSanitizesInvalidWaveValues()
        {
            var invalidLevel = ScriptableObject.CreateInstance<LevelConfig>();
            SetPrivateField(invalidLevel, "wave", new WaveConfig());

            var wave = invalidLevel.Wave;

            Assert.That(wave.SpawnInterval, Is.EqualTo(WaveConfig.Default.SpawnInterval));
            Assert.That(wave.MaxAliveEnemies, Is.EqualTo(WaveConfig.Default.MaxAliveEnemies));
            Assert.That(wave.SpawnRadius, Is.EqualTo(WaveConfig.Default.SpawnRadius));
            Assert.That(wave.Enemy.HitPoints, Is.EqualTo(EnemyConfig.Default.HitPoints));
            Object.DestroyImmediate(invalidLevel);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private sealed class TrackingInputProvider : GestureInputProviderBase
        {
            public override GestureSnapshot CurrentSnapshot => GestureSnapshot.Missing;
        }
    }
}
