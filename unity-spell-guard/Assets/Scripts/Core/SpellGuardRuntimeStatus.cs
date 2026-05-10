namespace SpellGuard.Core
{
    public readonly struct SpellGuardRuntimeStatus
    {
        public SpellGuardRuntimeStatus(string title, string description)
        {
            Title = title;
            Description = description;
        }

        public string Title { get; }
        public string Description { get; }
    }
}
