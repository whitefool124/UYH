namespace SpellGuard.Core
{
    [System.Serializable]
    public struct DifficultySettings
    {
        public float SpawnInterval;
        public int MaxAliveEnemies;

        public static DifficultySettings Relaxed => new DifficultySettings
        {
            SpawnInterval = 3.1f,
            MaxAliveEnemies = 4
        };

        public static DifficultySettings Standard => new DifficultySettings
        {
            SpawnInterval = 2.5f,
            MaxAliveEnemies = 6
        };

        public static DifficultySettings Intense => new DifficultySettings
        {
            SpawnInterval = 1.8f,
            MaxAliveEnemies = 8
        };
    }
}
