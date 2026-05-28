using NUnit.Framework;
using SpellGuard.InputSystem;
using UnityEngine;

namespace SpellGuard.Tests.PlayMode
{
    public class MotionGestureDetectorTests
    {
        [Test]
        public void DetectsStableFourWaySwipes()
        {
            AssertDetectsSwipe(MotionGestureType.SwipeLeftToRight, new[]
            {
                new Vector2(0.40f, 0.50f),
                new Vector2(0.45f, 0.505f),
                new Vector2(0.49f, 0.51f)
            });
            AssertDetectsSwipe(MotionGestureType.SwipeRightToLeft, new[]
            {
                new Vector2(0.50f, 0.50f),
                new Vector2(0.45f, 0.495f),
                new Vector2(0.41f, 0.49f)
            });
            AssertDetectsSwipe(MotionGestureType.SwipeBottomToTop, new[]
            {
                new Vector2(0.50f, 0.42f),
                new Vector2(0.505f, 0.47f),
                new Vector2(0.51f, 0.51f)
            });
            AssertDetectsSwipe(MotionGestureType.SwipeTopToBottom, new[]
            {
                new Vector2(0.50f, 0.51f),
                new Vector2(0.495f, 0.46f),
                new Vector2(0.49f, 0.42f)
            });
        }

        [Test]
        public void DetectsRecentSwipeSegmentWhenOlderHistoryWouldHideIt()
        {
            var detector = CreateDetector();
            AddHandSample(detector, 0.00f, new Vector2(0.70f, 0.50f));
            AddHandSample(detector, 0.18f, new Vector2(0.50f, 0.50f));
            AddHandSample(detector, 0.22f, new Vector2(0.55f, 0.505f));
            AddHandSample(detector, 0.26f, new Vector2(0.60f, 0.51f));

            Assert.That(detector.TryDetectSwipe(out var gesture), Is.True);
            Assert.That(gesture, Is.EqualTo(MotionGestureType.SwipeLeftToRight));
        }

        [Test]
        public void RejectsSwipeWhenOrthogonalDriftIsTooLarge()
        {
            var detector = CreateDetector();
            AddHandSample(detector, 0.00f, new Vector2(0.40f, 0.30f));
            AddHandSample(detector, 0.04f, new Vector2(0.44f, 0.47f));
            AddHandSample(detector, 0.08f, new Vector2(0.49f, 0.64f));

            Assert.That(detector.TryDetectSwipe(out _), Is.False);
        }

        [Test]
        public void RejectsSingleFrameDropThatLooksLikeDownSwipe()
        {
            var detector = CreateDetector();
            AddHandSample(detector, 0.00f, new Vector2(0.50f, 0.52f));
            AddHandSample(detector, 0.06f, new Vector2(0.50f, 0.43f));

            Assert.That(detector.TryDetectSwipe(out _), Is.False);
        }

        [Test]
        public void RejectsJitteryDownwardMotionWithOppositeTravel()
        {
            var detector = CreateDetector();
            AddHandSample(detector, 0.00f, new Vector2(0.50f, 0.54f));
            AddHandSample(detector, 0.04f, new Vector2(0.50f, 0.48f));
            AddHandSample(detector, 0.08f, new Vector2(0.50f, 0.51f));
            AddHandSample(detector, 0.12f, new Vector2(0.50f, 0.43f));

            Assert.That(detector.TryDetectSwipe(out _), Is.False);
        }

        private static void AssertDetectsSwipe(MotionGestureType expected, Vector2[] points)
        {
            var detector = CreateDetector();
            for (var index = 0; index < points.Length; index++)
            {
                AddHandSample(detector, index * 0.04f, points[index]);
            }

            Assert.That(detector.TryDetectSwipe(out var gesture), Is.True);
            Assert.That(gesture, Is.EqualTo(expected));
        }

        private static MotionGestureDetector CreateDetector()
        {
            var detector = new MotionGestureDetector();
            detector.Configure(
                historySeconds: 0.5f,
                sampleJitterDeadZone: 0.01f,
                swipeMinDistance: 0.075f,
                swipeMaxVerticalDrift: 0.16f,
                swipeMinSpeed: 0.18f,
                swipeCooldownSeconds: 0.22f,
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
