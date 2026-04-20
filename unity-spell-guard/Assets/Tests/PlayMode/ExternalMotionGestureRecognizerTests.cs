using System.Collections;
using NUnit.Framework;
using SpellGuard.InputSystem;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpellGuard.Tests.PlayMode
{
    public class ExternalMotionGestureRecognizerTests
    {
        private GameObject root;
        private ExternalGestureBridgeProvider bridgeProvider;
        private ExternalMotionGestureRecognizer recognizer;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("MotionRecognizerTestRoot");
            bridgeProvider = root.AddComponent<ExternalGestureBridgeProvider>();
            recognizer = root.AddComponent<ExternalMotionGestureRecognizer>();
            recognizer.Configure(bridgeProvider);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [UnityTest]
        public IEnumerator DetectsSwipeFromExternalFrames()
        {
            yield return PushHandFrame(new Vector2(0.18f, 0.42f), 0.02f);
            yield return PushHandFrame(new Vector2(0.31f, 0.44f), 0.02f);
            yield return PushHandFrame(new Vector2(0.48f, 0.45f), 0.02f);

            Assert.That(bridgeProvider.CurrentMotionGesture.IsValid, Is.True);
            Assert.That(bridgeProvider.CurrentMotionGesture.Gesture, Is.EqualTo(MotionGestureType.SwipeLeftToRight));
        }

        [UnityTest]
        public IEnumerator DetectsSnapFromExternalFrames()
        {
            yield return PushHandFrame(new Vector2(0.55f, 0.46f), 0.01f, 0.012f);
            yield return PushHandFrame(new Vector2(0.55f, 0.46f), 0.01f, 0.24f);

            Assert.That(bridgeProvider.CurrentMotionGesture.IsValid, Is.True);
            Assert.That(bridgeProvider.CurrentMotionGesture.Gesture, Is.EqualTo(MotionGestureType.Snap));
        }

        [UnityTest]
        public IEnumerator DetectsSwipeFromBufferedFramesPushedInSameUnityFrame()
        {
            var baseTime = Time.time;
            bridgeProvider.PushFrame(CreateHandFrame(new Vector2(0.18f, 0.42f), 0.18f, baseTime + 0.00f));
            bridgeProvider.PushFrame(CreateHandFrame(new Vector2(0.31f, 0.44f), 0.18f, baseTime + 0.02f));
            bridgeProvider.PushFrame(CreateHandFrame(new Vector2(0.48f, 0.45f), 0.18f, baseTime + 0.04f));

            yield return null;

            Assert.That(bridgeProvider.CurrentMotionGesture.IsValid, Is.True);
            Assert.That(bridgeProvider.CurrentMotionGesture.Gesture, Is.EqualTo(MotionGestureType.SwipeLeftToRight));
        }

        [UnityTest]
        public IEnumerator UsesFrameTimestampInsteadOfSingleReceiptTimeForSwipeDetection()
        {
            var baseTime = 100f;
            bridgeProvider.PushFrame(CreateHandFrame(new Vector2(0.20f, 0.40f), 0.18f, baseTime + 0.00f));
            bridgeProvider.PushFrame(CreateHandFrame(new Vector2(0.34f, 0.41f), 0.18f, baseTime + 0.03f));
            bridgeProvider.PushFrame(CreateHandFrame(new Vector2(0.50f, 0.42f), 0.18f, baseTime + 0.06f));

            yield return null;

            Assert.That(bridgeProvider.CurrentMotionGesture.IsValid, Is.True);
            Assert.That(bridgeProvider.CurrentMotionGesture.Gesture, Is.EqualTo(MotionGestureType.SwipeLeftToRight));
        }

        private IEnumerator PushHandFrame(Vector2 palm, float timeStep, float thumbMiddleDistance = 0.18f)
        {
            bridgeProvider.PushFrame(CreateHandFrame(palm, thumbMiddleDistance, Time.time));
            yield return new WaitForSeconds(timeStep);
        }

        private static ExternalVisionFrame CreateHandFrame(Vector2 palm, float thumbMiddleDistance, float timestamp)
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
    }
}
