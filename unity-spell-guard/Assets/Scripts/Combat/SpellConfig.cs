using UnityEngine;

namespace SpellGuard.Combat
{
    [System.Serializable]
    public struct SpellConfig
    {
        public string SpellId;
        public string DisplayName;
        public SpellType Type;
        public int Damage;
        public float Cooldown;
        public float Range;
        public float HitRadius;
        public Color Color;
        public float FreezeDuration;
        public float ShieldDuration;

        public static SpellConfig Default(SpellType type)
        {
            return type switch
            {
                SpellType.Fire => CreateFireVariant(0),
                SpellType.Ice => new SpellConfig { SpellId = "ice", DisplayName = "\u51b0\u971c\u672f", Type = SpellType.Ice, Damage = 0, Cooldown = 1.2f, Range = 36f, HitRadius = 0.45f, Color = new Color(0.36f, 0.82f, 1f, 1f), FreezeDuration = 2.5f, ShieldDuration = 0f },
                SpellType.Shield => new SpellConfig { SpellId = "shield", DisplayName = "\u62a4\u76fe\u672f", Type = SpellType.Shield, Damage = 0, Cooldown = 3f, Range = 0f, HitRadius = 0f, Color = new Color(0.45f, 0.72f, 1f, 1f), FreezeDuration = 0f, ShieldDuration = 3f },
                _ => new SpellConfig { SpellId = "none", DisplayName = "\u65e0", Type = SpellType.None, Damage = 0, Cooldown = 0f, Range = 0f, HitRadius = 0f, Color = Color.white, FreezeDuration = 0f, ShieldDuration = 0f }
            };
        }

        public static SpellConfig CreateFireVariant(int index)
        {
            var colors = new[]
            {
                new Color(1f, 0.12f, 0.08f, 1f),
                new Color(1f, 0.42f, 0.05f, 1f),
                new Color(1f, 0.9f, 0.12f, 1f),
                new Color(0.25f, 1f, 0.22f, 1f),
                new Color(0.15f, 0.95f, 1f, 1f),
                new Color(0.2f, 0.38f, 1f, 1f),
                new Color(0.78f, 0.22f, 1f, 1f),
            };
            var names = new[] { "\u8d64\u7130", "\u6a59\u7130", "\u91d1\u7130", "\u7fe0\u7130", "\u9752\u7130", "\u84dd\u7130", "\u7d2b\u7130" };
            var safeIndex = Mathf.Clamp(index, 0, colors.Length - 1);
            return new SpellConfig
            {
                SpellId = $"fire_{safeIndex + 1}",
                DisplayName = names[safeIndex],
                Type = SpellType.Fire,
                Damage = 1,
                Cooldown = 0.45f,
                Range = 36f,
                HitRadius = 0.5f,
                Color = colors[safeIndex],
                FreezeDuration = 0f,
                ShieldDuration = 0f
            };
        }
    }
}
