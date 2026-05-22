using UnityEngine;

namespace SpellGuard.Combat
{
    public static class SpellConfigLibrary
    {
        private const int FireVariantCount = 7;

        public static SpellConfig Get(SpellType spellType)
        {
            return SpellConfig.Default(spellType);
        }

        public static SpellConfig GetFireVariant(int index)
        {
            return SpellConfig.CreateFireVariant(Mathf.Clamp(index, 0, FireVariantCount - 1));
        }

        public static int GetFireVariantCount()
        {
            return FireVariantCount;
        }
    }
}
