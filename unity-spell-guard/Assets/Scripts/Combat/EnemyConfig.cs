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
            Speed = 2.2f,
            HitPoints = 2,
            AttackDistance = 1.4f
        };
    }
}
