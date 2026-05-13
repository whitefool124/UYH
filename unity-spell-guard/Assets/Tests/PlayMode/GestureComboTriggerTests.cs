using NUnit.Framework;
using SpellGuard.InputSystem;

namespace SpellGuard.Tests.PlayMode
{
    public class GestureComboTriggerTests
    {
        [Test]
        public void ResolvesExistingCommandsToComboAction()
        {
            var history = new[]
            {
                GestureTestSamples.Static(GestureType.Fist, 10f),
                GestureTestSamples.Motion(MotionGestureType.Snap, 10.4f)
            };

            var action = GestureComboTrigger.ResolveDefault(history);

            Assert.That(action.IsValid, Is.True);
            Assert.That(action.Intent, Is.EqualTo(GestureIntent.CastFire));
            Assert.That(action.IsTransient, Is.True);
        }

        [Test]
        public void RejectsPointPrimarySequenceByDefault()
        {
            var action = GestureComboTrigger.ResolveDefault(GestureTestSamples.PointFistSnapSequence());

            Assert.That(action.IsValid, Is.False);
        }

        [Test]
        public void RejectsBodyShiftSequenceByDefault()
        {
            var history = new[]
            {
                GestureTestSamples.Static(GestureType.OpenPalm, 10f),
                GestureTestSamples.Motion(MotionGestureType.BodyShiftLeft, 10.2f)
            };

            var action = GestureComboTrigger.ResolveDefault(history);

            Assert.That(action.IsValid, Is.False);
        }
    }
}
