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
            yield return PushHandFrame(new Vector2(0.24f, 0.42f), 0.02f);
            yield return PushHandFrame(new Vector2(0.43f, 0.44f), 0.02f);
            yield return PushHandFrame(new Vector2(0.62f, 0.45f), 0.02f);

            Assert.That(bridgeProvider.CurrentMotionGesture.IsValid, Is.True);
            Assert.That(bridgeProvider.CurrentMotionGesture.Gesture, Is.EqualTo(MotionGestureType.SwipeLeftToRight));
        }

        [UnityTest]
        public IEnumerator DetectsVerticalSwipeUpFromExternalFrames()
        {
            yield return PushHandFrame(new Vector2(0.42f, 0.24f), 0.02f);
            yield return PushHandFrame(new Vector2(0.43f, 0.43f), 0.02f);
            yield return PushHandFrame(new Vector2(0.44f, 0.62f), 0.02f);

            Assert.That(bridgeProvider.CurrentMotionGesture.IsValid, Is.True);
            Assert.That(bridgeProvider.CurrentMotionGesture.Gesture, Is.EqualTo(MotionGestureType.SwipeBottomToTop));
        }

        [UnityTest]
        public IEnumerator DetectsVerticalSwipeDownFromExternalFrames()
        {
            yield return PushHandFrame(new Vector2(0.42f, 0.70f), 0.02f);
            yield return PushHandFrame(new Vector2(0.43f, 0.57f), 0.02f);
            yield return PushHandFrame(new Vector2(0.44f, 0.42f), 0.02f);

            Assert.That(bridgeProvider.CurrentMotionGesture.IsValid, Is.True);
            Assert.That(bridgeProvider.CurrentMotionGesture.Gesture, Is.EqualTo(MotionGestureType.SwipeTopToBottom));
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
            bridgeProvider.PushFrame(CreateHandFrame(new Vector2(0.24f, 0.42f), 0.18f, baseTime + 0.00f));
            bridgeProvider.PushFrame(CreateHandFrame(new Vector2(0.43f, 0.44f), 0.18f, baseTime + 0.02f));
            bridgeProvider.PushFrame(CreateHandFrame(new Vector2(0.62f, 0.45f), 0.18f, baseTime + 0.04f));

            yield return null;

            Assert.That(bridgeProvider.CurrentMotionGesture.IsValid, Is.True);
            Assert.That(bridgeProvider.CurrentMotionGesture.Gesture, Is.EqualTo(MotionGestureType.SwipeLeftToRight));
        }

        [UnityTest]
        public IEnumerator KeepsSwipeHistoryAcrossBriefTrackingDrop()
        {
            var baseTime = 100f;
            bridgeProvider.PushFrame(CreateHandFrame(new Vector2(0.24f, 0.42f), 0.18f, baseTime + 0.00f));
            bridgeProvider.PushFrame(CreateMissingHandFrame(baseTime + 0.03f));
            bridgeProvider.PushFrame(CreateHandFrame(new Vector2(0.43f, 0.44f), 0.18f, baseTime + 0.06f));
            bridgeProvider.PushFrame(CreateHandFrame(new Vector2(0.62f, 0.45f), 0.18f, baseTime + 0.09f));

            yield return null;

            Assert.That(bridgeProvider.CurrentMotionGesture.IsValid, Is.True);
            Assert.That(bridgeProvider.CurrentMotionGesture.Gesture, Is.EqualTo(MotionGestureType.SwipeLeftToRight));
        }

        [UnityTest]
        public IEnumerator DetectsSparseSwipeWhenOnlyTwoTrackedFramesSurvive()
        {
            var baseTime = 120f;
            bridgeProvider.PushFrame(CreateHandFrame(new Vector2(0.24f, 0.42f), 0.18f, baseTime + 0.00f));
            bridgeProvider.PushFrame(CreateMissingHandFrame(baseTime + 0.08f));
            bridgeProvider.PushFrame(CreateMissingHandFrame(baseTime + 0.16f));
            bridgeProvider.PushFrame(CreateHandFrame(new Vector2(0.62f, 0.45f), 0.18f, baseTime + 0.24f));

            yield return null;

            Assert.That(bridgeProvider.CurrentMotionGesture.IsValid, Is.True);
            Assert.That(bridgeProvider.CurrentMotionGesture.Gesture, Is.EqualTo(MotionGestureType.SwipeLeftToRight));
        }

        [UnityTest]
        public IEnumerator SparseSwipeCanUseRawPointBeforeStableGestureUpdates()
        {
            var baseTime = 140f;
            bridgeProvider.PushFrame(CreateHandFrame(new Vector2(0.24f, 0.42f), 0.18f, baseTime + 0.00f, "unknown", "point"));
            bridgeProvider.PushFrame(CreateMissingHandFrame(baseTime + 0.08f));
            bridgeProvider.PushFrame(CreateHandFrame(new Vector2(0.62f, 0.45f), 0.18f, baseTime + 0.24f, "unknown", "point"));

            yield return null;

            Assert.That(bridgeProvider.CurrentMotionGesture.IsValid, Is.True);
            Assert.That(bridgeProvider.CurrentMotionGesture.Gesture, Is.EqualTo(MotionGestureType.SwipeLeftToRight));
        }

        [UnityTest]
        public IEnumerator ConsumesMotionGestureSentByExternalBridgePacket()
        {
            bridgeProvider.PushFrame(CreateHandFrame(new Vector2(0.55f, 0.45f), 0.18f, Time.time, "point", "point", "swipeRightToLeft"));

            yield return null;

            Assert.That(bridgeProvider.CurrentMotionGesture.IsValid, Is.True);
            Assert.That(bridgeProvider.CurrentMotionGesture.Gesture, Is.EqualTo(MotionGestureType.SwipeRightToLeft));
        }

        [UnityTest]
        public IEnumerator UsesFrameTimestampInsteadOfSingleReceiptTimeForSwipeDetection()
        {
            var baseTime = 100f;
            bridgeProvider.PushFrame(CreateHandFrame(new Vector2(0.20f, 0.40f), 0.18f, baseTime + 0.00f));
            bridgeProvider.PushFrame(CreateHandFrame(new Vector2(0.34f, 0.41f), 0.18f, baseTime + 0.03f));
            bridgeProvider.PushFrame(CreateHandFrame(new Vector2(0.62f, 0.42f), 0.18f, baseTime + 0.06f));

            yield return null;

            Assert.That(bridgeProvider.CurrentMotionGesture.IsValid, Is.True);
            Assert.That(bridgeProvider.CurrentMotionGesture.Gesture, Is.EqualTo(MotionGestureType.SwipeLeftToRight));
        }

        [UnityTest]
        public IEnumerator ProfileCanRaiseSwipeThresholdForExternalRecognizer()
        {
            var profile = ScriptableObject.CreateInstance<GestureRecognitionProfile>();
            profile.swipeMinDistance = 0.5f;
            profile.swipeMinSpeed = 0.2f;
            recognizer.Configure(bridgeProvider, profile);

            yield return PushHandFrame(new Vector2(0.24f, 0.42f), 0.02f);
            yield return PushHandFrame(new Vector2(0.43f, 0.44f), 0.02f);
            yield return PushHandFrame(new Vector2(0.62f, 0.45f), 0.02f);

            Assert.That(bridgeProvider.CurrentMotionGesture.IsValid, Is.False);
            Object.DestroyImmediate(profile);
        }

        private IEnumerator PushHandFrame(Vector2 palm, float timeStep, float thumbMiddleDistance = 0.18f)
        {
            bridgeProvider.PushFrame(CreateHandFrame(palm, thumbMiddleDistance, Time.time));
            yield return new WaitForSeconds(timeStep);
        }

        private static ExternalVisionFrame CreateHandFrame(Vector2 palm, float thumbMiddleDistance, float timestamp, string gesture = "point", string rawGesture = "point", string motionGesture = "none")
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
                gesture = gesture,
                rawGesture = rawGesture,
                x = palm.x,
                y = palm.y,
                confidence = 0.95f,
                trackingConfidence = 0.95f,
                timestamp = timestamp,
                motionGesture = motionGesture,
                motionConfidence = motionGesture == "none" ? 0f : 0.9f,
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

        private static ExternalVisionFrame CreateMissingHandFrame(float timestamp)
        {
            return new ExternalVisionFrame
            {
                handPresent = false,
                gesture = "none",
                x = 0.5f,
                y = 0.5f,
                confidence = 0f,
                trackingConfidence = 0f,
                timestamp = timestamp,
                handLandmarks = new ExternalVisionPoint[0],
                poseLandmarks = new ExternalVisionPoint[0]
            };
        }
    }
}
