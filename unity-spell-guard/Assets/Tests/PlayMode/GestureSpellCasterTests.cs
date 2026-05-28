using NUnit.Framework;
using SpellGuard.Combat;
using SpellGuard.InputSystem;
using SpellGuard.Player;
using UnityEngine;

namespace SpellGuard.Tests.PlayMode
{
    public class GestureSpellCasterTests
    {
        private sealed class TestInputProvider : GestureInputProviderBase
        {
            public GestureSnapshot snapshot = GestureSnapshot.Missing;
            public MotionGestureEvent motion = MotionGestureEvent.None;

            public override GestureSnapshot CurrentSnapshot => snapshot;
            public override MotionGestureEvent CurrentMotionGesture => motion;
        }

        private GameObject playerRoot;
        private GameObject enemyRoot;
        private GestureSpellCaster caster;
        private TestInputProvider inputProvider;
        private PlayerHealth playerHealth;

        [SetUp]
        public void SetUp()
        {
            playerRoot = new GameObject("GestureSpellCasterTests_Player");
            playerHealth = playerRoot.AddComponent<PlayerHealth>();
            inputProvider = playerRoot.AddComponent<TestInputProvider>();
            caster = playerRoot.AddComponent<GestureSpellCaster>();
            SetPrivateField(caster, "inputProvider", inputProvider);
            SetPrivateField(caster, "playerHealth", playerHealth);
            SetPrivateField(caster, "shieldCounterRadius", 4.2f);
            SetPrivateField(caster, "shieldCounterDamage", 1);

            enemyRoot = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemyRoot.name = "GestureSpellCasterTests_Enemy";
            enemyRoot.transform.position = playerRoot.transform.position + Vector3.forward * 2f;
            var enemy = enemyRoot.AddComponent<SimpleEnemyController>();
            enemy.Initialize(playerRoot.transform, playerHealth);
            enemy.ApplyConfig(new EnemyConfig
            {
                HitPoints = 3,
                Speed = 0.1f,
                AttackDistance = 0.8f
            });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(enemyRoot);
            Object.DestroyImmediate(playerRoot);
        }

        [Test]
        public void OpenPalmSlapTriggersShieldCounter()
        {
            var resolved = false;
            var resolvedSpell = SpellType.None;
            var resolvedHits = 0;
            caster.SpellResolved += (spell, hits) =>
            {
                resolved = true;
                resolvedSpell = spell;
                resolvedHits = hits;
            };

            inputProvider.motion = new MotionGestureEvent
            {
                Gesture = MotionGestureType.OpenPalmSlapLeftToRight,
                ViewportPosition = new Vector2(0.5f, 0.5f),
                Confidence = 1f,
                TriggeredTime = Time.time
            };

            InvokePrivateUpdate(caster);

            Assert.That(resolved, Is.True);
            Assert.That(resolvedSpell, Is.EqualTo(SpellType.Shield));
            Assert.That(resolvedHits, Is.EqualTo(1));
            Assert.That(playerHealth.ShieldActive, Is.True);
            Assert.That(caster.StatusText, Does.Contain("\u62a4\u76fe\u53cd\u51fb"));
            Assert.That(caster.LastSpellFeedbackText, Does.Contain("\u547d\u4e2d 1 \u4e2a\u654c\u4eba"));
        }

        [Test]
        public void PointToFistTriggersQuickFireFeedback()
        {
            inputProvider.motion = new MotionGestureEvent
            {
                Gesture = MotionGestureType.PointToFist,
                ViewportPosition = new Vector2(0.5f, 0.5f),
                Confidence = 1f,
                TriggeredTime = Time.time
            };

            InvokePrivateUpdate(caster);

            Assert.That(caster.LastCastSpell, Is.EqualTo(SpellType.Fire));
            Assert.That(caster.StatusText, Does.Contain("\u5feb\u901f\u706b\u7130"));
        }

        private static void InvokePrivateUpdate(GestureSpellCaster target)
        {
            target.GetType().GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(target, null);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
