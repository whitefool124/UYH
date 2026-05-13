using System.Collections.Generic;

namespace SpellGuard.InputSystem
{
    public readonly struct GestureComboRule
    {
        public GestureIntent Intent { get; }
        public GestureCommandPattern[] Pattern { get; }
        public float MaxWindowSeconds { get; }

        public GestureComboRule(GestureIntent intent, GestureCommandPattern[] pattern, float maxWindowSeconds = 1.2f)
        {
            Intent = intent;
            Pattern = pattern;
            MaxWindowSeconds = maxWindowSeconds;
        }
    }

    public static class GestureComboTrigger
    {
        private static readonly GestureComboRule[] defaultRules =
        {
            new GestureComboRule(
                GestureIntent.CastFire,
                new[]
                {
                    GestureCommandPattern.Static(GestureType.Fist),
                    GestureCommandPattern.Motion(MotionGestureType.Snap)
                }),
            new GestureComboRule(
                GestureIntent.CastShield,
                new[]
                {
                    GestureCommandPattern.Static(GestureType.OpenPalm),
                    GestureCommandPattern.Motion(MotionGestureType.SwipeLeftToRight)
                })
        };

        public static GestureAction ResolveDefault(IReadOnlyList<GestureCommand> history)
        {
            return Resolve(history, defaultRules);
        }

        public static GestureAction Resolve(IReadOnlyList<GestureCommand> history, IReadOnlyList<GestureComboRule> rules)
        {
            if (history == null || history.Count == 0 || rules == null)
            {
                return GestureAction.None;
            }

            for (var index = 0; index < rules.Count; index++)
            {
                var rule = rules[index];
                if (rule.Intent == GestureIntent.None || rule.Pattern == null || rule.Pattern.Length == 0)
                {
                    continue;
                }

                if (GestureSequenceMatcher.EndsWith(history, rule.Pattern, rule.MaxWindowSeconds) && !HasPointLeadInSameWindow(history, rule))
                {
                    return GestureAction.FromCommand(rule.Intent, history[history.Count - 1]);
                }
            }

            return GestureAction.None;
        }

        private static bool HasPointLeadInSameWindow(IReadOnlyList<GestureCommand> history, GestureComboRule rule)
        {
            var patternStart = history.Count - rule.Pattern.Length;
            if (patternStart <= 0)
            {
                return false;
            }

            var previous = history[patternStart - 1];
            var last = history[history.Count - 1];
            return previous.Kind == GestureCommandKind.StaticPose
                && previous.StaticGesture == GestureType.Point
                && last.TriggeredTime - previous.TriggeredTime <= rule.MaxWindowSeconds;
        }
    }
}
