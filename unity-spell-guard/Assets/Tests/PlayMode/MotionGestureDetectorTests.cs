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
                new Vector2(0.30f, 0.50f),
                new Vector2(0.43f, 0.505f),
                new Vector2(0.58f, 0.51f)
            });
            AssertDetectsSwipe(MotionGestureType.SwipeRightToLeft, new[]
            {
                new Vector2(0.70f, 0.50f),
                new Vector2(0.57f, 0.495f),
                new Vector2(0.42f, 0.49f)
            });
            AssertDetectsSwipe(MotionGestureType.SwipeBottomToTop, new[]
            {
                new Vector2(0.50f, 0.30f),
                new Vector2(0.505f, 0.43f),
                new Vector2(0.51f, 0.58f)
            });
            AssertDetectsSwipe(MotionGestureType.SwipeTopToBottom, new[]
            {
                new Vector2(0.50f, 0.70f),
                new Vector2(0.495f, 0.57f),
                new Vector2(0.49f, 0.42f)
            });
        }

        [Test]
        public void DetectsRecentSwipeSegmentWhenOlderHistoryWouldHideIt()
        {
            var detector = CreateDetector();
            AddHandSample(detector, 0.00f, new Vector2(0.76f, 0.50f));
            AddHandSample(detector, 0.18f, new Vector2(0.30f, 0.50f));
            AddHandSample(detector, 0.22f, new Vector2(0.43f, 0.505f));
            AddHandSample(detector, 0.26f, new Vector2(0.58f, 0.51f));

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

        [Test]
        public void RejectsShortDownSwipeBelowStricterThreshold()
        {
            var detector = CreateDetector();
            AddHandSample(detector, 0.00f, new Vector2(0.50f, 0.70f));
            AddHandSample(detector, 0.04f, new Vector2(0.50f, 0.64f));
            AddHandSample(detector, 0.08f, new Vector2(0.50f, 0.57f));

            Assert.That(detector.TryDetectSwipe(out _), Is.False);
        }

        [Test]
        public void DetectsRelaxedLeftRightAndUpSwipes()
        {
            AssertDetectsSwipe(MotionGestureType.SwipeLeftToRight, new[]
            {
                new Vector2(0.35f, 0.50f),
                new Vector2(0.45f, 0.505f),
                new Vector2(0.55f, 0.51f)
            });
            AssertDetectsSwipe(MotionGestureType.SwipeRightToLeft, new[]
            {
                new Vector2(0.65f, 0.50f),
                new Vector2(0.55f, 0.495f),
                new Vector2(0.45f, 0.49f)
            });
            AssertDetectsSwipe(MotionGestureType.SwipeBottomToTop, new[]
            {
                new Vector2(0.50f, 0.35f),
                new Vector2(0.505f, 0.45f),
                new Vector2(0.51f, 0.55f)
            });
        }

        [Test]
        public void DetectsPointSwipeWithoutCrossingScreenCenter()
        {
            var detector = CreateDetector();
            AddHandSample(detector, 0.00f, new Vector2(0.43f, 0.50f));
            AddHandSample(detector, 0.04f, new Vector2(0.50f, 0.505f));
            AddHandSample(detector, 0.08f, new Vector2(0.61f, 0.51f));

            Assert.That(detector.TryDetectSwipe(out var gesture), Is.True);
            Assert.That(gesture, Is.EqualTo(MotionGestureType.SwipeLeftToRight));
        }

        [Test]
        public void DetectsPointSwipeWithoutStartingAtEdge()
        {
            var detector = CreateDetector();
            AddHandSample(detector, 0.00f, new Vector2(0.42f, 0.50f));
            AddHandSample(detector, 0.04f, new Vector2(0.50f, 0.505f));
            AddHandSample(detector, 0.08f, new Vector2(0.58f, 0.51f));

            Assert.That(detector.TryDetectSwipe(out var gesture), Is.True);
            Assert.That(gesture, Is.EqualTo(MotionGestureType.SwipeLeftToRight));
        }

        [Test]
        public void RejectsSwipeWhenHandIsNotPointing()
        {
            var detector = CreateDetector();
            AddHandSample(detector, 0.00f, new Vector2(0.30f, 0.50f), GestureType.OpenPalm);
            AddHandSample(detector, 0.04f, new Vector2(0.43f, 0.505f), GestureType.OpenPalm);
            AddHandSample(detector, 0.08f, new Vector2(0.58f, 0.51f), GestureType.OpenPalm);

            Assert.That(detector.TryDetectSwipe(out _), Is.False);
        }

        [Test]
        public void RejectsSwipeWhenPointIsOnlyBrieflySeenDuringMotion()
        {
            var detector = CreateDetector();
            AddHandSample(detector, 0.00f, new Vector2(0.30f, 0.50f), GestureType.Unknown);
            AddHandSample(detector, 0.04f, new Vector2(0.43f, 0.505f), GestureType.Point);
            AddHandSample(detector, 0.08f, new Vector2(0.58f, 0.51f), GestureType.Unknown);

            Assert.That(detector.TryDetectSwipe(out _), Is.False);
        }

        [Test]
        public void AllowsSwipeWhenPointIsHeldForMostOfMotion()
        {
            var detector = CreateDetector();
            AddHandSample(detector, 0.00f, new Vector2(0.30f, 0.50f), GestureType.Point);
            AddHandSample(detector, 0.04f, new Vector2(0.39f, 0.505f), GestureType.Point);
            AddHandSample(detector, 0.08f, new Vector2(0.48f, 0.51f), GestureType.Unknown);
            AddHandSample(detector, 0.12f, new Vector2(0.58f, 0.512f), GestureType.Point);

            Assert.That(detector.TryDetectSwipe(out var gesture), Is.True);
            Assert.That(gesture, Is.EqualTo(MotionGestureType.SwipeLeftToRight));
        }

        [Test]
        public void RejectsSwipeWhenPointDoesNotStartTheMotion()
        {
            var detector = CreateDetector();
            AddHandSample(detector, 0.00f, new Vector2(0.30f, 0.50f), GestureType.Unknown);
            AddHandSample(detector, 0.04f, new Vector2(0.39f, 0.505f), GestureType.Point);
            AddHandSample(detector, 0.08f, new Vector2(0.48f, 0.51f), GestureType.Point);
            AddHandSample(detector, 0.12f, new Vector2(0.58f, 0.512f), GestureType.Point);

            Assert.That(detector.TryDetectSwipe(out _), Is.False);
        }

        [Test]
        public void AppliesTwoSecondSwipeCooldown()
        {
            var detector = CreateDetector();
            AddHandSample(detector, 0.00f, new Vector2(0.30f, 0.50f));
            AddHandSample(detector, 0.04f, new Vector2(0.43f, 0.505f));
            AddHandSample(detector, 0.08f, new Vector2(0.58f, 0.51f));

            Assert.That(detector.TryDetectSwipe(out _), Is.True);

            detector.ResetHandHistoryKeepingLatest(new MotionGestureDetector.HandSample
            {
                Time = 1.00f,
                Palm = new Vector2(0.30f, 0.50f),
                SwipePoint = new Vector2(0.30f, 0.50f),
                ThumbTip = new Vector2(0.22f, 0.50f),
                MiddleTip = new Vector2(0.38f, 0.50f),
                StaticGesture = GestureType.Point,
                HasSnapData = true
            });
            AddHandSample(detector, 1.04f, new Vector2(0.43f, 0.505f));
            AddHandSample(detector, 1.08f, new Vector2(0.58f, 0.51f));

            Assert.That(detector.TryDetectSwipe(out _), Is.False);

            detector.ResetHandHistoryKeepingLatest(new MotionGestureDetector.HandSample
            {
                Time = 2.20f,
                Palm = new Vector2(0.30f, 0.50f),
                SwipePoint = new Vector2(0.30f, 0.50f),
                ThumbTip = new Vector2(0.22f, 0.50f),
                MiddleTip = new Vector2(0.38f, 0.50f),
                StaticGesture = GestureType.Point,
                HasSnapData = true
            });
            AddHandSample(detector, 2.24f, new Vector2(0.43f, 0.505f));
            AddHandSample(detector, 2.28f, new Vector2(0.58f, 0.51f));

            Assert.That(detector.TryDetectSwipe(out _), Is.True);
        }

        private static void AssertDetectsSwipe(MotionGestureType expected, Vector2[] points)
        {
            var detector = CreateDetector();
            for (var index = 0; index < points.Length; index++)
            {
                AddHandSample(detector, index * 0.04f, points[index], GestureType.Point);
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
                swipeMinDistance: 0.14f,
                swipeMaxVerticalDrift: 0.14f,
                swipeMinSpeed: 0.5f,
                swipeCooldownSeconds: 2f,
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

        private static void AddHandSample(MotionGestureDetector detector, float time, Vector2 palm, GestureType gesture = GestureType.Point)
        {
            detector.AddHandSample(new MotionGestureDetector.HandSample
            {
                Time = time,
                Palm = palm,
                SwipePoint = palm,
                ThumbTip = palm + Vector2.left * 0.08f,
                MiddleTip = palm + Vector2.right * 0.08f,
                StaticGesture = gesture,
                HasSnapData = true
            }, compareStaticGestureForJitter: false);
        }
    }
}
