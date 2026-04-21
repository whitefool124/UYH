using System.Collections.Generic;
using NUnit.Framework;
using SpellGuard.InputSystem;

namespace SpellGuard.Tests.PlayMode
{
    public class GestureSequenceMatcherTests
    {
        [Test]
        public void MatchesPointFistSnapSequence()
        {
            var history = GestureTestSamples.PointFistSnapSequence();
            var pattern = new List<GestureCommandPattern>
            {
                GestureCommandPattern.Static(GestureType.Point),
                GestureCommandPattern.Static(GestureType.Fist),
                GestureCommandPattern.Motion(MotionGestureType.Snap)
            };

            Assert.That(GestureSequenceMatcher.EndsWith(history, pattern), Is.True);
        }

        [Test]
        public void RejectsOutOfOrderSequence()
        {
            var history = GestureTestSamples.PointFistSnapSequence();
            var pattern = new List<GestureCommandPattern>
            {
                GestureCommandPattern.Static(GestureType.Fist),
                GestureCommandPattern.Static(GestureType.Point),
                GestureCommandPattern.Motion(MotionGestureType.Snap)
            };

            Assert.That(GestureSequenceMatcher.EndsWith(history, pattern), Is.False);
        }

        [Test]
        public void RejectsSequenceOutsideWindow()
        {
            var history = new List<GestureCommand>
            {
                GestureTestSamples.Static(GestureType.Point, 10f),
                GestureTestSamples.Static(GestureType.Fist, 12f),
                GestureTestSamples.Motion(MotionGestureType.Snap, 13.1f)
            };
            var pattern = new List<GestureCommandPattern>
            {
                GestureCommandPattern.Static(GestureType.Point),
                GestureCommandPattern.Static(GestureType.Fist),
                GestureCommandPattern.Motion(MotionGestureType.Snap)
            };

            Assert.That(GestureSequenceMatcher.EndsWith(history, pattern, 2.0f), Is.False);
        }

        [Test]
        public void MatchesIndexBeckonSample()
        {
            var history = GestureTestSamples.IndexBeckonSequence();
            var pattern = new List<GestureCommandPattern>
            {
                GestureCommandPattern.Static(GestureType.Point),
                GestureCommandPattern.Motion(MotionGestureType.PointToFist),
                GestureCommandPattern.Motion(MotionGestureType.Snap)
            };

            Assert.That(GestureSequenceMatcher.EndsWith(history, pattern), Is.True);
        }

        [Test]
        public void MatchesDualHandComboSample()
        {
            var history = GestureTestSamples.DualHandComboSequence();
            var pattern = new List<GestureCommandPattern>
            {
                GestureCommandPattern.Static(GestureType.Point),
                GestureCommandPattern.Static(GestureType.OpenPalm),
                GestureCommandPattern.Motion(MotionGestureType.SwipeLeftToRight)
            };

            Assert.That(GestureSequenceMatcher.EndsWith(history, pattern), Is.True);
        }
    }
}
