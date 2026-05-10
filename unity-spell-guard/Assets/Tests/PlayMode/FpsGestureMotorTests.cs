using NUnit.Framework;
using SpellGuard.InputSystem;
using SpellGuard.Player;
using UnityEngine;

namespace SpellGuard.Tests.PlayMode
{
    public class FpsGestureMotorTests
    {
        private sealed class TestInputProvider : GestureInputProviderBase
        {
            public GestureSnapshot snapshot = GestureSnapshot.Missing;
            public GestureFrame frame = GestureFrame.Empty(GestureSourceKind.Unknown);

            public override GestureSnapshot CurrentSnapshot => snapshot;
            public override GestureFrame CurrentGestureFrame => frame;
        }

        private GameObject root;
        private GameObject pivotObject;
        private MockGestureInputProvider mockProvider;
        private FpsGestureMotor motor;
        private TestInputProvider testInputProvider;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("FpsGestureMotorTestsRoot");
            root.AddComponent<CharacterController>();
            motor = root.AddComponent<FpsGestureMotor>();
            mockProvider = root.AddComponent<MockGestureInputProvider>();
            testInputProvider = root.AddComponent<TestInputProvider>();

            pivotObject = new GameObject("CameraPivot");
            pivotObject.transform.SetParent(root.transform, false);

            SetPrivateField(motor, "inputProvider", mockProvider);
            SetPrivateField(motor, "cameraPivot", pivotObject.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(pivotObject);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void PointGestureExposesTrackedSnapshot()
        {
            InvokePrivateUpdate(motor);
            var frame = motor.CurrentGestureFrame;

            Assert.That(motor.Snapshot.HandPresent, Is.True);
            Assert.That(motor.Snapshot.Gesture, Is.EqualTo(GestureType.Point));
            Assert.That(frame.Source, Is.EqualTo(GestureSourceKind.Unknown));
        }

        [Test]
        public void DisablingInputStopsMovementState()
        {
            motor.SetInputEnabled(false);

            Assert.That(motor.IsMovingForward, Is.False);
            Assert.That(motor.IsStepInProgress, Is.False);
        }

        [Test]
        public void SwipeBottomToTopStartsForwardStep()
        {
            SetPrivateField(motor, "inputProvider", testInputProvider);
            testInputProvider.snapshot = GestureSnapshot.Missing;
            testInputProvider.frame = GestureFrame.Empty(GestureSourceKind.Mock);
            testInputProvider.frame.LatestMotion = new MotionGestureEvent
            {
                Gesture = MotionGestureType.SwipeBottomToTop,
                ViewportPosition = new Vector2(0.5f, 0.5f),
                Confidence = 1f,
                TriggeredTime = Time.time
            };

            InvokePrivateUpdate(motor);

            Assert.That(motor.IsStepInProgress, Is.True);
            Assert.That(motor.CurrentStepDirection, Is.EqualTo(FpsGestureMotor.DiscreteMoveDirection.Forward));
        }

        [Test]
        public void SwipeTopToBottomStartsBackwardStep()
        {
            SetPrivateField(motor, "inputProvider", testInputProvider);
            testInputProvider.snapshot = GestureSnapshot.Missing;
            testInputProvider.frame = GestureFrame.Empty(GestureSourceKind.Mock);
            testInputProvider.frame.LatestMotion = new MotionGestureEvent
            {
                Gesture = MotionGestureType.SwipeTopToBottom,
                ViewportPosition = new Vector2(0.5f, 0.5f),
                Confidence = 1f,
                TriggeredTime = Time.time
            };

            InvokePrivateUpdate(motor);

            Assert.That(motor.IsStepInProgress, Is.True);
            Assert.That(motor.CurrentStepDirection, Is.EqualTo(FpsGestureMotor.DiscreteMoveDirection.Backward));
        }

        [Test]
        public void BodyShiftLeftStartsLeftStep()
        {
            SetPrivateField(motor, "inputProvider", testInputProvider);
            testInputProvider.snapshot = GestureSnapshot.Missing;
            testInputProvider.frame = GestureFrame.Empty(GestureSourceKind.Mock);
            testInputProvider.frame.LatestMotion = new MotionGestureEvent
            {
                Gesture = MotionGestureType.BodyShiftLeft,
                ViewportPosition = new Vector2(0.5f, 0.5f),
                Confidence = 1f,
                TriggeredTime = Time.time
            };

            InvokePrivateUpdate(motor);

            Assert.That(motor.IsStepInProgress, Is.True);
            Assert.That(motor.CurrentStepDirection, Is.EqualTo(FpsGestureMotor.DiscreteMoveDirection.Left));
        }

        private static void InvokePrivateUpdate(FpsGestureMotor target)
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
