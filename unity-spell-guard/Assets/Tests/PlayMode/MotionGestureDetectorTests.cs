using NUnit.Framework;
using SpellGuard.InputSystem;
using UnityEngine;

namespace SpellGuard.Tests.PlayMode
{
    public class MotionGestureDetectorTests
    {
        [Test]
        public void DetectsShortFourWaySwipes()
        {
            AssertDetectsSwipe(new Vector2(0.40f, 0.50f), new Vector2(0.47f, 0.51f), MotionGestureType.SwipeLeftToRight);
            AssertDetectsSwipe(new Vector2(0.50f, 0.50f), new Vector2(0.43f, 0.49f), MotionGestureType.SwipeRightToLeft);
            AssertDetectsSwipe(new Vector2(0.50f, 0.42f), new Vector2(0.51f, 0.49f), MotionGestureType.SwipeBottomToTop);
            AssertDetectsSwipe(new Vector2(0.50f, 0.49f), new Vector2(0.49f, 0.42f), MotionGestureType.SwipeTopToBottom);
        }

        [Test]
        public void DetectsRecentSwipeSegmentWhenOlderHistoryWouldHideIt()
        {
            var detector = CreateDetector();
            AddHandSample(detector, 0.00f, new Vector2(0.70f, 0.50f));
            AddHandSample(detector, 0.18f, new Vector2(0.50f, 0.50f));
            AddHandSample(detector, 0.23f, new Vector2(0.57f, 0.51f));

            Assert.That(detector.TryDetectSwipe(out var gesture), Is.True);
            Assert.That(gesture, Is.EqualTo(MotionGestureType.SwipeLeftToRight));
        }

        [Test]
        public void RejectsSwipeWhenOrthogonalDriftIsTooLarge()
        {
            var detector = CreateDetector();
            AddHandSample(detector, 0.00f, new Vector2(0.40f, 0.30f));
            AddHandSample(detector, 0.08f, new Vector2(0.47f, 0.64f));

            Assert.That(detector.TryDetectSwipe(out _), Is.False);
        }

        private static void AssertDetectsSwipe(Vector2 start, Vector2 end, MotionGestureType expected)
        {
            var detector = CreateDetector();
            AddHandSample(detector, 0.00f, start);
            AddHandSample(detector, 0.05f, end);

            Assert.That(detector.TryDetectSwipe(out var gesture), Is.True);
            Assert.That(gesture, Is.EqualTo(expected));
        }

        private static MotionGestureDetector CreateDetector()
        {
            var detector = new MotionGestureDetector();
            detector.Configure(
                historySeconds: 0.5f,
                sampleJitterDeadZone: 0.008f,
                swipeMinDistance: 0.06f,
                swipeMaxVerticalDrift: 0.28f,
                swipeMinSpeed: 0.14f,
                swipeCooldownSeconds: 0.18f,
                slapMinDistance: 0.11f,
                slapMinOpenPalmRatio: 0.8f,
                slapMinSpeed: 0.24f,
                slapCooldownSeconds: 0.32f,
                pointHoldMinDuration: 0.08f,
                gestureTransitionMaxDuration: 0.4f,
                gestureTransitionMaxTravel: 0.18f,
                gestureTransitionCooldownSeconds: 0.45f,
                snapCloseDistance: 0.09f,
                snapReleaseDistance: 0.14f,
                snapMaxDuration: 0.35f,
                snapCooldownSeconds: 0.45f,
                bodyShiftMinDistance: 0.1f,
                bodyShiftMaxVerticalDrift: 0.12f,
                bodyShiftMinSpeed: 0.28f,
                bodyShiftCooldownSeconds: 0.45f);
            return detector;
        }

        private static void AddHandSample(MotionGestureDetector detector, float time, Vector2 palm)
        {
            detector.AddHandSample(new MotionGestureDetector.HandSample
            {
                Time = time,
                Palm = palm,
                ThumbTip = palm + Vector2.left * 0.08f,
                MiddleTip = palm + Vector2.right * 0.08f,
                StaticGesture = GestureType.Point,
                HasSnapData = true
            }, compareStaticGestureForJitter: false);
        }
    }
}
