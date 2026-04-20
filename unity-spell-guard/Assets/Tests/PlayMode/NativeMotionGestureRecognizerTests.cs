using System.Collections;
using NUnit.Framework;
using SpellGuard.InputSystem;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpellGuard.Tests.PlayMode
{
    public class NativeMotionGestureRecognizerTests
    {
        private GameObject root;
        private NativeMediapipeGestureProvider nativeProvider;
        private NativeMotionGestureRecognizer recognizer;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("NativeMotionRecognizerTestRoot");
            nativeProvider = root.AddComponent<NativeMediapipeGestureProvider>();
            recognizer = root.AddComponent<NativeMotionGestureRecognizer>();
            recognizer.Configure(nativeProvider);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [UnityTest]
        public IEnumerator DetectsSwipeFromNativeLandmarkFrames()
        {
            PushHandFrame(new Vector2(0.18f, 0.42f), 0.18f);
            yield return new WaitForSeconds(0.02f);
            PushHandFrame(new Vector2(0.31f, 0.44f), 0.18f);
            yield return new WaitForSeconds(0.02f);
            PushHandFrame(new Vector2(0.48f, 0.45f), 0.18f);
            yield return null;

            Assert.That(nativeProvider.CurrentMotionGesture.IsValid, Is.True);
            Assert.That(nativeProvider.CurrentMotionGesture.Gesture, Is.EqualTo(MotionGestureType.SwipeLeftToRight));
        }

        [UnityTest]
        public IEnumerator DetectsSnapFromNativeLandmarkFrames()
        {
            PushHandFrame(new Vector2(0.55f, 0.46f), 0.01f);
            yield return new WaitForSeconds(0.01f);
            PushHandFrame(new Vector2(0.55f, 0.46f), 0.24f);
            yield return null;

            Assert.That(nativeProvider.CurrentMotionGesture.IsValid, Is.True);
            Assert.That(nativeProvider.CurrentMotionGesture.Gesture, Is.EqualTo(MotionGestureType.Snap));
        }

        [UnityTest]
        public IEnumerator DetectsOpenPalmSlapFromNativeLandmarkFrames()
        {
            PushHandFrame(new Vector2(0.16f, 0.42f), 0.18f, GestureType.OpenPalm);
            yield return new WaitForSeconds(0.02f);
            PushHandFrame(new Vector2(0.31f, 0.43f), 0.18f, GestureType.OpenPalm);
            yield return new WaitForSeconds(0.02f);
            PushHandFrame(new Vector2(0.50f, 0.44f), 0.18f, GestureType.OpenPalm);
            yield return null;

            Assert.That(nativeProvider.CurrentMotionGesture.IsValid, Is.True);
            Assert.That(nativeProvider.CurrentMotionGesture.Gesture, Is.EqualTo(MotionGestureType.OpenPalmSlapLeftToRight));
        }

        [UnityTest]
        public IEnumerator DetectsPointToFistTransitionFromNativeFrames()
        {
            PushHandFrame(new Vector2(0.46f, 0.45f), 0.18f, GestureType.Point);
            yield return new WaitForSeconds(0.1f);
            PushHandFrame(new Vector2(0.47f, 0.45f), 0.18f, GestureType.Fist);
            yield return null;

            Assert.That(nativeProvider.CurrentMotionGesture.IsValid, Is.True);
            Assert.That(nativeProvider.CurrentMotionGesture.Gesture, Is.EqualTo(MotionGestureType.PointToFist));
        }

        [UnityTest]
        public IEnumerator DetectsOpenPalmSlapRightToLeftFromNativeLandmarkFrames()
        {
            PushHandFrame(new Vector2(0.56f, 0.42f), 0.18f, GestureType.OpenPalm);
            yield return new WaitForSeconds(0.02f);
            PushHandFrame(new Vector2(0.38f, 0.43f), 0.18f, GestureType.OpenPalm);
            yield return new WaitForSeconds(0.02f);
            PushHandFrame(new Vector2(0.19f, 0.44f), 0.18f, GestureType.OpenPalm);
            yield return null;

            Assert.That(nativeProvider.CurrentMotionGesture.IsValid, Is.True);
            Assert.That(nativeProvider.CurrentMotionGesture.Gesture, Is.EqualTo(MotionGestureType.OpenPalmSlapRightToLeft));
        }

        [UnityTest]
        public IEnumerator DoesNotPromoteGenericSwipeToSlapWhenOpenPalmHistoryIsInsufficient()
        {
            PushHandFrame(new Vector2(0.18f, 0.42f), 0.18f, GestureType.Point);
            yield return new WaitForSeconds(0.02f);
            PushHandFrame(new Vector2(0.31f, 0.43f), 0.18f, GestureType.OpenPalm);
            yield return new WaitForSeconds(0.02f);
            PushHandFrame(new Vector2(0.50f, 0.44f), 0.18f, GestureType.OpenPalm);
            yield return null;

            Assert.That(nativeProvider.CurrentMotionGesture.IsValid, Is.True);
            Assert.That(nativeProvider.CurrentMotionGesture.Gesture, Is.EqualTo(MotionGestureType.SwipeLeftToRight));
        }

        [UnityTest]
        public IEnumerator DoesNotDetectPointToFistWhenPointHoldIsTooShort()
        {
            PushHandFrame(new Vector2(0.46f, 0.45f), 0.18f, GestureType.Point);
            yield return new WaitForSeconds(0.02f);
            PushHandFrame(new Vector2(0.47f, 0.45f), 0.18f, GestureType.Fist);
            yield return null;

            Assert.That(nativeProvider.CurrentMotionGesture.IsValid, Is.False);
        }

        [UnityTest]
        public IEnumerator DoesNotDetectPointToFistWhenPalmTravelIsTooLarge()
        {
            PushHandFrame(new Vector2(0.20f, 0.45f), 0.18f, GestureType.Point);
            yield return new WaitForSeconds(0.1f);
            PushHandFrame(new Vector2(0.50f, 0.45f), 0.18f, GestureType.Fist);
            yield return null;

            Assert.That(nativeProvider.CurrentMotionGesture.IsValid, Is.False);
        }

        [UnityTest]
        public IEnumerator HonorsTransitionCooldownBetweenPointToFistEvents()
        {
            PushHandFrame(new Vector2(0.46f, 0.45f), 0.18f, GestureType.Point);
            yield return new WaitForSeconds(0.1f);
            PushHandFrame(new Vector2(0.47f, 0.45f), 0.18f, GestureType.Fist);
            yield return null;

            Assert.That(nativeProvider.CurrentMotionGesture.Gesture, Is.EqualTo(MotionGestureType.PointToFist));

            nativeProvider.ClearMotionGesture();
            PushHandFrame(new Vector2(0.46f, 0.45f), 0.18f, GestureType.Point);
            yield return new WaitForSeconds(0.1f);
            PushHandFrame(new Vector2(0.47f, 0.45f), 0.18f, GestureType.Fist);
            yield return null;

            Assert.That(nativeProvider.CurrentMotionGesture.IsValid, Is.False);
        }

        [UnityTest]
        public IEnumerator HonorsSlapPrecedenceOverGenericSwipe()
        {
            PushHandFrame(new Vector2(0.16f, 0.42f), 0.18f, GestureType.OpenPalm);
            yield return new WaitForSeconds(0.02f);
            PushHandFrame(new Vector2(0.31f, 0.43f), 0.18f, GestureType.OpenPalm);
            yield return new WaitForSeconds(0.02f);
            PushHandFrame(new Vector2(0.50f, 0.44f), 0.18f, GestureType.OpenPalm);
            yield return null;

            Assert.That(nativeProvider.CurrentMotionGesture.IsValid, Is.True);
            Assert.That(nativeProvider.CurrentMotionGesture.Gesture, Is.Not.EqualTo(MotionGestureType.SwipeLeftToRight));
            Assert.That(nativeProvider.CurrentMotionGesture.Gesture, Is.EqualTo(MotionGestureType.OpenPalmSlapLeftToRight));
        }

        private void PushHandFrame(Vector2 palm, float thumbMiddleDistance, GestureType gesture = GestureType.Point)
        {
            nativeProvider.SetHandLandmarks(CreateHandLandmarks(palm, thumbMiddleDistance));
            nativeProvider.SetSnapshot(true, gesture, palm, 0.95f);
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
    }
}
