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

            SetPrivateField(motor, "inputProvider", mockProvider);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void PointGestureExposesTrackedSnapshot()
        {
            InvokePrivateUpdate(motor);
            var frame = motor.CurrentGestureFrame;

            Assert.That(motor.Snapshot.HandPresent, Is.True);
            Assert.That(motor.Snapshot.Gesture, Is.EqualTo(GestureType.Point));
            Assert.That(frame.Source, Is.EqualTo(GestureSourceKind.Mock));
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
        public void PointHoldStartsForwardStep()
        {
            SetPrivateField(motor, "inputProvider", testInputProvider);
            SetPrivateField(motor, "staticMoveHoldSeconds", 0f);
            testInputProvider.frame = CreateStaticGestureFrame(GestureType.Point);

            InvokePrivateUpdate(motor);

            Assert.That(motor.IsStepInProgress, Is.True);
            Assert.That(motor.CurrentStepDirection, Is.EqualTo(FpsGestureMotor.DiscreteMoveDirection.Forward));
        }

        [Test]
        public void OpenPalmHoldStartsBackwardStep()
        {
            SetPrivateField(motor, "inputProvider", testInputProvider);
            SetPrivateField(motor, "staticMoveHoldSeconds", 0f);
            testInputProvider.frame = CreateStaticGestureFrame(GestureType.OpenPalm);

            InvokePrivateUpdate(motor);

            Assert.That(motor.IsStepInProgress, Is.True);
            Assert.That(motor.CurrentStepDirection, Is.EqualTo(FpsGestureMotor.DiscreteMoveDirection.Backward));
            Assert.That(motor.IsMovingForward, Is.False);
        }

        [Test]
        public void StaticMoveHoldDoesNotRepeatBeforeGestureChanges()
        {
            SetPrivateField(motor, "inputProvider", testInputProvider);
            SetPrivateField(motor, "staticMoveHoldSeconds", 0f);
            SetPrivateField(motor, "moveStepDuration", 0.01f);
            SetPrivateField(motor, "moveInputCooldown", 0f);
            testInputProvider.frame = CreateStaticGestureFrame(GestureType.Point);

            InvokePrivateUpdate(motor);
            SetPrivateField(motor, "stepInProgress", false);
            SetPrivateField(motor, "currentStepDirection", FpsGestureMotor.DiscreteMoveDirection.None);
            InvokePrivateUpdate(motor);

            Assert.That(motor.IsStepInProgress, Is.False);
            Assert.That(motor.CurrentStepDirection, Is.EqualTo(FpsGestureMotor.DiscreteMoveDirection.None));
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

        private static GestureFrame CreateStaticGestureFrame(GestureType gesture)
        {
            return new GestureFrame
            {
                FrameId = 1,
                Timestamp = Time.time,
                Source = GestureSourceKind.Mock,
                Hands = new[]
                {
                    new TrackedHandState
                    {
                        TrackId = 1,
                        Handedness = GestureHandedness.Right,
                        IsTracked = true,
                        StaticGesture = gesture,
                        Confidence = 1f,
                        ViewportPosition = new Vector2(0.5f, 0.5f),
                        PalmCenter = new Vector2(0.5f, 0.5f),
                        Landmarks = System.Array.Empty<Vector2>()
                    }
                },
                LatestMotion = MotionGestureEvent.None
            };
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
