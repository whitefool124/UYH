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
            SpawnInterval = 2.5f,
            MaxAliveEnemies = 6,
            SpawnRadius = 18f,
            Enemy = EnemyConfig.Default
        };
    }
}
