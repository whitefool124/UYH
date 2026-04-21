using UnityEngine;

namespace SpellGuard.InputSystem
{
    public abstract class GestureInputProviderBase : MonoBehaviour
    {
        public abstract GestureSnapshot CurrentSnapshot { get; }

        public virtual MotionGestureEvent CurrentMotionGesture => MotionGestureEvent.None;

        public virtual GestureFrame CurrentGestureFrame => LegacyGestureRuntimeAdapter.BuildSingleHandFrame(
            CurrentSnapshot,
            null,
            0,
            Time.time,
            GestureSourceKind.Unknown,
            CurrentMotionGesture);

        public virtual GestureCommand CurrentGestureCommand => LegacyGestureRuntimeAdapter.BuildCommand(CurrentSnapshot, CurrentMotionGesture);

        public virtual GestureCommand[] RecentGestureCommands => System.Array.Empty<GestureCommand>();

        protected static GestureCommand ChooseGestureCommand(GestureSnapshot snapshot, MotionGestureEvent motion)
        {
            return LegacyGestureRuntimeAdapter.BuildCommand(snapshot, motion);
        }
    }
}
