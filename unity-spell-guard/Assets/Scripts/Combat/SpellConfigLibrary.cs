using UnityEngine;

namespace SpellGuard.Combat
{
    public static class SpellConfigLibrary
    {
        public static SpellConfig Get(SpellType spellType)
        {
            return SpellConfig.Default(spellType);
        }
    }
}
