using UnityEngine;

namespace SpellGuard.InputSystem
{
    [CreateAssetMenu(fileName = "GestureRecognitionProfile", menuName = "Spell Guard/Gesture Recognition Profile")]
    public sealed class GestureRecognitionProfile : ScriptableObject
    {
        [Header("Shared History")]
        [Min(0.05f)] public float historySeconds = 0.7f;

        [Header("Swipe")]
        [Min(0f)] public float swipeMinDistance = 0.09f;
        [Min(0f)] public float swipeMaxVerticalDrift = 0.22f;
        [Min(0f)] public float swipeMinSpeed = 0.2f;
        [Min(0f)] public float swipeCooldownSeconds = 0.28f;

        [Header("Native Open Palm Slap")]
        [Min(0f)] public float slapMinDistance = 0.11f;
        [Range(0f, 1f)] public float slapMinOpenPalmRatio = 0.8f;
        [Min(0f)] public float slapMinSpeed = 0.24f;
        [Min(0f)] public float slapCooldownSeconds = 0.32f;

        [Header("Snap")]
        [Min(0f)] public float snapCloseDistance = 0.09f;
        [Min(0f)] public float snapReleaseDistance = 0.14f;
        [Min(0f)] public float snapMaxDuration = 0.35f;
        [Min(0f)] public float snapCooldownSeconds = 0.45f;

        [Header("Gesture Transition")]
        [Min(0f)] public float pointHoldMinDuration = 0.08f;
        [Min(0f)] public float gestureTransitionMaxDuration = 0.4f;
        [Min(0f)] public float gestureTransitionMaxTravel = 0.18f;
        [Min(0f)] public float gestureTransitionCooldownSeconds = 0.45f;

        [Header("Body Shift")]
        [Min(0f)] public float bodyShiftMinDistance = 0.1f;
        [Min(0f)] public float bodyShiftMaxVerticalDrift = 0.12f;
        [Min(0f)] public float bodyShiftMinSpeed = 0.28f;
        [Min(0f)] public float bodyShiftCooldownSeconds = 0.45f;
        [Range(0f, 1f)] public float minPoseVisibility = 0.45f;
    }
}
