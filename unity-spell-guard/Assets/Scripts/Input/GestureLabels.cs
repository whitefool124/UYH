namespace SpellGuard.InputSystem
{
    public static class GestureLabels
    {
        public static string ToChinese(this GestureType gesture)
        {
            return gesture switch
            {
                GestureType.Point => "指向",
                GestureType.Fist => "握拳",
                GestureType.VSign => "V 手势",
                GestureType.OpenPalm => "张掌",
                GestureType.Unknown => "未知手势",
                _ => "无"
            };
        }
    }
}
