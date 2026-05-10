using UnityEngine;

namespace SpellGuard.Combat
{
    [System.Serializable]
    public struct SpellConfig
    {
        public int Damage;
        public float FreezeDuration;
        public float ShieldDuration;

        public static SpellConfig Default(SpellType type)
        {
            return type switch
            {
                SpellType.Fire => new SpellConfig { Damage = 1, FreezeDuration = 0f, ShieldDuration = 0f },
                SpellType.Ice => new SpellConfig { Damage = 0, FreezeDuration = 2.5f, ShieldDuration = 0f },
                SpellType.Shield => new SpellConfig { Damage = 0, FreezeDuration = 0f, ShieldDuration = 3f },
                _ => new SpellConfig { Damage = 0, FreezeDuration = 0f, ShieldDuration = 0f }
            };
        }
    }
}
