using UnityEngine;

namespace SpellGuard.InputSystem
{
    public abstract class GestureInputProviderBase : MonoBehaviour
    {
        public abstract GestureSnapshot CurrentSnapshot { get; }

        public virtual MotionGestureEvent CurrentMotionGesture => MotionGestureEvent.None;
    }
}
