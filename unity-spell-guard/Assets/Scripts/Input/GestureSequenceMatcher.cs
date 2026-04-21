using System.Collections.Generic;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public readonly struct GestureCommandPattern
    {
        public GestureCommandKind Kind { get; }
        public GestureType StaticGesture { get; }
        public MotionGestureType MotionGesture { get; }

        public GestureCommandPattern(GestureCommandKind kind, GestureType staticGesture, MotionGestureType motionGesture)
        {
            Kind = kind;
            StaticGesture = staticGesture;
            MotionGesture = motionGesture;
        }

        public static GestureCommandPattern Static(GestureType gesture)
        {
            return new GestureCommandPattern(GestureCommandKind.StaticPose, gesture, MotionGestureType.None);
        }

        public static GestureCommandPattern Motion(MotionGestureType gesture)
        {
            return new GestureCommandPattern(GestureCommandKind.Motion, GestureType.None, gesture);
        }

        public bool Matches(GestureCommand command)
        {
            if (!command.IsValid || command.Kind != Kind)
            {
                return false;
            }

            return Kind switch
            {
                GestureCommandKind.StaticPose => command.StaticGesture == StaticGesture,
                GestureCommandKind.Motion => command.MotionGesture == MotionGesture,
                _ => false,
            };
        }
    }

    public static class GestureSequenceMatcher
    {
        public static bool EndsWith(IReadOnlyList<GestureCommand> history, IReadOnlyList<GestureCommandPattern> pattern, float maxWindowSeconds = 1.2f)
        {
            if (history == null || pattern == null || pattern.Count == 0 || history.Count < pattern.Count)
            {
                return false;
            }

            var historyStart = history.Count - pattern.Count;
            var firstTime = history[historyStart].TriggeredTime;
            var lastTime = history[history.Count - 1].TriggeredTime;
            if (lastTime - firstTime > Mathf.Max(0.1f, maxWindowSeconds))
            {
                return false;
            }

            for (var index = 0; index < pattern.Count; index++)
            {
                if (!pattern[index].Matches(history[historyStart + index]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
