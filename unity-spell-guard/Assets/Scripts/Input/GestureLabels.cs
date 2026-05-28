namespace SpellGuard.InputSystem
{
    public static class GestureLabels
    {
        public static string ToChinese(this GestureType gesture)
        {
            return gesture switch
            {
                GestureType.Point => "\u6307\u5411",
                GestureType.Fist => "\u63e1\u62f3",
                GestureType.VSign => "V \u624b\u52bf",
                GestureType.OpenPalm => "\u5f20\u638c",
                GestureType.Unknown => "\u672a\u77e5\u624b\u52bf",
                _ => "\u65e0"
            };
        }
    }
}
