namespace SpellGuard.Combat
{
    [System.Serializable]
    public struct EnemyConfig
    {
        public float Speed;
        public int HitPoints;
        public float AttackDistance;

        public static EnemyConfig Default => new EnemyConfig
        {
            Speed = 0f,
            HitPoints = 2,
            AttackDistance = 0f
        };
    }
}
