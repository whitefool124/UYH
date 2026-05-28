using UnityEngine;

namespace SpellGuard.Combat
{
    [CreateAssetMenu(fileName = "LevelConfig", menuName = "Spell Guard/Combat/Level Config")]
    public class LevelConfig : ScriptableObject
    {
        [SerializeField] private string levelId = "combat_demo";
        [SerializeField] private string displayName = "Combat Demo";
        [SerializeField] private float durationSeconds = 180f;
        [SerializeField] private int targetScore = 12;
        [SerializeField] private int playerHealth = 5;
        [SerializeField] private bool spawnEnemies = true;
        [SerializeField] private WaveConfig wave = WaveConfig.Default;
        [SerializeField] private SpellType[] allowedSpells = { SpellType.Fire, SpellType.Ice, SpellType.Shield };
        [SerializeField] private string tutorialHint = "教程：先看左上角提示，按 WASD 移动，左键或 1 发射火球，连续击败 3 个固定敌人。";

        public string LevelId => string.IsNullOrWhiteSpace(levelId) ? name : levelId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? LevelId : displayName;
        public float DurationSeconds => Mathf.Max(0f, durationSeconds);
        public int TargetScore => Mathf.Max(1, targetScore);
        public int PlayerHealth => Mathf.Max(1, playerHealth);
        public bool SpawnEnemies => spawnEnemies;
        public WaveConfig Wave => SanitizeWave(wave);
        public SpellType[] AllowedSpells => allowedSpells;
        public string TutorialHint => tutorialHint;

        private static WaveConfig SanitizeWave(WaveConfig value)
        {
            var fallback = WaveConfig.Default;
            if (value.SpawnInterval <= 0f)
            {
                value.SpawnInterval = fallback.SpawnInterval;
            }

            if (value.MaxAliveEnemies <= 0)
            {
                value.MaxAliveEnemies = fallback.MaxAliveEnemies;
            }

            if (value.SpawnRadius <= 0f)
            {
                value.SpawnRadius = fallback.SpawnRadius;
            }

            if (value.Enemy.Speed < 0f || value.Enemy.HitPoints <= 0 || value.Enemy.AttackDistance <= 0f)
            {
                value.Enemy = fallback.Enemy;
            }

            return value;
        }
    }
}
