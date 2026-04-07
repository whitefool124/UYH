namespace SpellGuard.Combat
{
    public static class SpellLabels
    {
        public static string ToChinese(this SpellType spell)
        {
            return spell switch
            {
                SpellType.Fire => "火焰术",
                SpellType.Ice => "冰霜术",
                SpellType.Shield => "护盾术",
                _ => "无"
            };
        }
    }
}
