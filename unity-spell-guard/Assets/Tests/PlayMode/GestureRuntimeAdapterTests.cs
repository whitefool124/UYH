using NUnit.Framework;
using SpellGuard.InputSystem;
using UnityEngine;

namespace SpellGuard.Tests.PlayMode
{
    public class GestureRuntimeAdapterTests
    {
        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("GestureRuntimeAdapterTestsRoot");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void MockProviderExposesStaticGestureFrameAndCommand()
        {
            var provider = root.AddComponent<MockGestureInputProvider>();

            var frame = provider.CurrentGestureFrame;
            var command = provider.CurrentGestureCommand;

            Assert.That(frame.Source, Is.EqualTo(GestureSourceKind.Mock));
            Assert.That(frame.HandCount, Is.EqualTo(1));
            Assert.That(frame.HasPrimaryHand, Is.True);
            Assert.That(frame.Hands[0].StaticGesture, Is.EqualTo(GestureType.Point));
            Assert.That(frame.PrimaryHand.StaticGesture, Is.EqualTo(GestureType.Point));
            Assert.That(command.IsValid, Is.True);
            Assert.That(command.Kind, Is.EqualTo(GestureCommandKind.StaticPose));
            Assert.That(command.StaticGesture, Is.EqualTo(GestureType.Point));
        }

        [Test]
        public void NativeProviderExposesMotionCommandAndHandFrame()
        {
            var provider = root.AddComponent<NativeMediapipeGestureProvider>();
            var palm = new Vector2(0.42f, 0.61f);

            provider.SetHandLandmarks(CreateHandLandmarks(palm, 0.16f));
            provider.SetSnapshot(true, GestureType.Fist, palm, 0.95f);
            provider.PushMotionGesture(MotionGestureType.Snap, palm, 0.9f);

            var frame = provider.CurrentGestureFrame;
            var command = provider.CurrentGestureCommand;

            Assert.That(frame.Source, Is.EqualTo(GestureSourceKind.NativeMediapipe));
            Assert.That(frame.HandCount, Is.EqualTo(1));
            Assert.That(frame.Hands[0].StaticGesture, Is.EqualTo(GestureType.Fist));
            Assert.That(frame.Hands[0].Landmarks.Length, Is.EqualTo(21));
            Assert.That(command.IsValid, Is.True);
            Assert.That(command.Kind, Is.EqualTo(GestureCommandKind.Motion));
            Assert.That(command.MotionGesture, Is.EqualTo(MotionGestureType.Snap));
        }

        [Test]
        public void ExternalProviderExposesFrameAndPrefersMotionCommand()
        {
            var provider = root.AddComponent<ExternalGestureBridgeProvider>();
            var palm = new Vector2(0.36f, 0.52f);

            provider.PushFrame(CreateExternalFrame(palm, 0.18f, Time.time));
            provider.PushMotionGesture(MotionGestureType.SwipeLeftToRight, palm, 0.88f);

            var frame = provider.CurrentGestureFrame;
            var command = provider.CurrentGestureCommand;

            Assert.That(frame.Source, Is.EqualTo(GestureSourceKind.ExternalBridge));
            Assert.That(frame.HandCount, Is.EqualTo(1));
            Assert.That(frame.PrimaryHand.Handedness, Is.EqualTo(GestureHandedness.Right));
            Assert.That(frame.Hands[0].StaticGesture, Is.EqualTo(GestureType.Point));
            Assert.That(frame.Hands[0].Landmarks.Length, Is.EqualTo(21));
            Assert.That(command.IsValid, Is.True);
            Assert.That(command.Kind, Is.EqualTo(GestureCommandKind.Motion));
            Assert.That(command.MotionGesture, Is.EqualTo(MotionGestureType.SwipeLeftToRight));
        }

        [Test]
        public void RouterExposesRecentCommandHistory()
        {
            var router = root.AddComponent<GestureInputRouter>();
            var mock = root.AddComponent<MockGestureInputProvider>();
            SetPrivateField(router, "mockProvider", mock);
            SetPrivateField(router, "mode", GestureInputRouter.InputMode.Mock);

            _ = router.CurrentGestureCommand;
            _ = router.CurrentGestureCommand;

            Assert.That(router.RecentGestureCommands.Length, Is.GreaterThanOrEqualTo(1));
        }

        private static Vector2[] CreateHandLandmarks(Vector2 palm, float thumbMiddleDistance)
        {
            var landmarks = new Vector2[21];
            for (var index = 0; index < landmarks.Length; index++)
            {
                landmarks[index] = palm;
            }

            landmarks[0] = palm;
            landmarks[5] = palm + new Vector2(-0.01f, 0f);
            landmarks[17] = palm + new Vector2(0.01f, 0f);
            landmarks[4] = new Vector2(Mathf.Clamp01(palm.x - thumbMiddleDistance * 0.5f), palm.y);
            landmarks[12] = new Vector2(Mathf.Clamp01(palm.x + thumbMiddleDistance * 0.5f), palm.y);
            return landmarks;
        }

        private static ExternalVisionFrame CreateExternalFrame(Vector2 palm, float thumbMiddleDistance, float timestamp)
        {
            var landmarks = new ExternalVisionPoint[21];
            for (var index = 0; index < landmarks.Length; index++)
            {
                landmarks[index] = new ExternalVisionPoint
                {
                    x = palm.x,
                    y = palm.y,
                    z = 0f,
                    visibility = 1f
                };
            }

            landmarks[4] = new ExternalVisionPoint
            {
                x = Mathf.Clamp01(palm.x - thumbMiddleDistance * 0.5f),
                y = palm.y,
                z = 0f,
                visibility = 1f
            };
            landmarks[12] = new ExternalVisionPoint
            {
                x = Mathf.Clamp01(palm.x + thumbMiddleDistance * 0.5f),
                y = palm.y,
                z = 0f,
                visibility = 1f
            };

            return new ExternalVisionFrame
            {
                handPresent = true,
                gesture = "point",
                x = palm.x,
                y = palm.y,
                confidence = 0.95f,
                trackingConfidence = 0.95f,
                timestamp = timestamp,
                handedness = "Right",
                pointer = new ExternalVisionPoint
                {
                    x = palm.x,
                    y = palm.y,
                    z = 0f,
                    visibility = 1f
                },
                handLandmarks = landmarks,
                poseLandmarks = new ExternalVisionPoint[0]
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
