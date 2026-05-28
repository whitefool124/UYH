namespace SpellGuard.Combat
{
    public static class SpellLabels
    {
        public static string ToChinese(this SpellType spell)
        {
            return spell switch
            {
                SpellType.Fire => "\u706b\u7130\u672f",
                SpellType.Ice => "\u51b0\u971c\u672f",
                SpellType.Shield => "\u62a4\u76fe\u672f",
                _ => "\u65e0"
            };
        }
    }
}
