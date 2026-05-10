using NUnit.Framework;
using SpellGuard.Combat;

namespace SpellGuard.Tests.PlayMode
{
    public class SpellConfigTests
    {
        [Test]
        public void FireSpellConfigHasDamage()
        {
            var config = SpellConfigLibrary.Get(SpellType.Fire);

            Assert.That(config.Damage, Is.GreaterThan(0));
            Assert.That(config.FreezeDuration, Is.EqualTo(0f));
            Assert.That(config.ShieldDuration, Is.EqualTo(0f));
        }

        [Test]
        public void IceSpellConfigHasFreezeDuration()
        {
            var config = SpellConfigLibrary.Get(SpellType.Ice);

            Assert.That(config.Damage, Is.EqualTo(0));
            Assert.That(config.FreezeDuration, Is.GreaterThan(0f));
            Assert.That(config.ShieldDuration, Is.EqualTo(0f));
        }

        [Test]
        public void ShieldSpellConfigHasShieldDuration()
        {
            var config = SpellConfigLibrary.Get(SpellType.Shield);

            Assert.That(config.Damage, Is.EqualTo(0));
            Assert.That(config.FreezeDuration, Is.EqualTo(0f));
            Assert.That(config.ShieldDuration, Is.GreaterThan(0f));
        }
    }
}
