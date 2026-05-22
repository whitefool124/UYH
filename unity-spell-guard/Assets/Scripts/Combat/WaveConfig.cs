using System;

namespace SpellGuard.Combat
{
    [Serializable]
    public struct WaveConfig
    {
        public float SpawnInterval;
        public int MaxAliveEnemies;
        public float SpawnRadius;
        public EnemyConfig Enemy;

        public static WaveConfig Default => new WaveConfig
        {
            SpawnInterval = 0f,
            MaxAliveEnemies = 3,
            SpawnRadius = 0f,
            Enemy = EnemyConfig.Default
        };
    }
}
