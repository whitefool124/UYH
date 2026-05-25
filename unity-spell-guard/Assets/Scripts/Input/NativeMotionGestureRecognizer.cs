using System.Collections.Generic;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public class NativeMotionGestureRecognizer : MonoBehaviour
    {
        [SerializeField] private NativeMediapipeGestureProvider nativeProvider;
        [SerializeField] private GestureRecognitionProfile recognitionProfile;
        [SerializeField] private bool debugLogs = true;
        [SerializeField] private float historySeconds = 0.7f;
        [SerializeField] private float sampleJitterDeadZone = 0.015f;
        [SerializeField] private float swipeMinDistance = 0.09f;
        [SerializeField] private float swipeMaxVerticalDrift = 0.22f;
        [SerializeField] private float swipeMinSpeed = 0.2f;
        [SerializeField] private float swipeCooldownSeconds = 0.28f;
        [SerializeField] private float slapMinDistance = 0.11f;
        [SerializeField] private float slapMinOpenPalmRatio = 0.8f;
        [SerializeField] private float slapMinSpeed = 0.24f;
        [SerializeField] private float slapCooldownSeconds = 0.32f;
        [SerializeField] private float pointHoldMinDuration = 0.08f;
        [SerializeField] private float gestureTransitionMaxDuration = 0.4f;
        [SerializeField] private float gestureTransitionMaxTravel = 0.18f;
        [SerializeField] private float gestureTransitionCooldownSeconds = 0.45f;
        [SerializeField] private float snapCloseDistance = 0.09f;
        [SerializeField] private float snapReleaseDistance = 0.14f;
        [SerializeField] private float snapMaxDuration = 0.35f;
        [SerializeField] private float snapCooldownSeconds = 0.45f;

        private readonly MotionGestureDetector detector = new MotionGestureDetector();
        private int lastProcessedFrameVersion = -1;

        public void Configure(NativeMediapipeGestureProvider provider)
        {
            nativeProvider = provider;
            ApplyRecognitionProfile();
        }

        public void Configure(NativeMediapipeGestureProvider provider, GestureRecognitionProfile profile)
        {
            nativeProvider = provider;
            recognitionProfile = profile;
            ApplyRecognitionProfile();
        }

        private void Awake()
        {
            ApplyRecognitionProfile();
        }

        private void OnValidate()
        {
            ApplyRecognitionProfile();
        }

        private void ApplyRecognitionProfile()
        {
            if (recognitionProfile != null)
            {
                historySeconds = recognitionProfile.historySeconds;
                sampleJitterDeadZone = recognitionProfile.sampleJitterDeadZone;
                swipeMinDistance = recognitionProfile.swipeMinDistance;
                swipeMaxVerticalDrift = recognitionProfile.swipeMaxVerticalDrift;
                swipeMinSpeed = recognitionProfile.swipeMinSpeed;
                swipeCooldownSeconds = recognitionProfile.swipeCooldownSeconds;
                slapMinDistance = recognitionProfile.slapMinDistance;
                slapMinOpenPalmRatio = recognitionProfile.slapMinOpenPalmRatio;
                slapMinSpeed = recognitionProfile.slapMinSpeed;
                slapCooldownSeconds = recognitionProfile.slapCooldownSeconds;
                pointHoldMinDuration = recognitionProfile.pointHoldMinDuration;
                gestureTransitionMaxDuration = recognitionProfile.gestureTransitionMaxDuration;
                gestureTransitionMaxTravel = recognitionProfile.gestureTransitionMaxTravel;
                gestureTransitionCooldownSeconds = recognitionProfile.gestureTransitionCooldownSeconds;
                snapCloseDistance = recognitionProfile.snapCloseDistance;
                snapReleaseDistance = recognitionProfile.snapReleaseDistance;
                snapMaxDuration = recognitionProfile.snapMaxDuration;
                snapCooldownSeconds = recognitionProfile.snapCooldownSeconds;
            }

            detector.Configure(
                historySeconds,
                sampleJitterDeadZone,
                swipeMinDistance,
                swipeMaxVerticalDrift,
                swipeMinSpeed,
                swipeCooldownSeconds,
                slapMinDistance,
                slapMinOpenPalmRatio,
                slapMinSpeed,
                slapCooldownSeconds,
                pointHoldMinDuration,
                gestureTransitionMaxDuration,
                gestureTransitionMaxTravel,
                gestureTransitionCooldownSeconds,
                snapCloseDistance,
                snapReleaseDistance,
                snapMaxDuration,
                snapCooldownSeconds,
                0.1f,
                0.12f,
                0.28f,
                0.45f);
        }

        private void Update()
        {
            if (nativeProvider == null || nativeProvider.FrameVersion == lastProcessedFrameVersion)
            {
                return;
            }

            lastProcessedFrameVersion = nativeProvider.FrameVersion;
            ProcessCurrentFrame();
        }

        private void ProcessCurrentFrame()
        {
            var snapshot = nativeProvider.CurrentSnapshot;
            if (!snapshot.HandPresent)
            {
                detector.ResetAll();
                nativeProvider.ClearMotionGesture();
                return;
            }

            var sampleTime = nativeProvider.LastSampleTime > 0f ? nativeProvider.LastSampleTime : Time.time;
            var sample = BuildHandSample(nativeProvider.HandLandmarks, snapshot.ViewportPosition, snapshot.Gesture, sampleTime);
            if (!detector.AddHandSample(sample, true))
            {
                return;
            }

            if (detector.TryDetectGestureTransition(sample, out var transition))
            {
                PushMotion("native-gesture-transition", transition, sample.Palm, 0.88f);
                return;
            }

            if (detector.TryDetectOpenPalmSlap(out var slap))
            {
                PushMotion("native-open-palm-slap", slap, sample.Palm, 0.93f);
                detector.ResetHandHistoryKeepingLatest(sample);
                return;
            }

            if (detector.TryDetectSwipe(out var swipe))
            {
                PushMotion("native-hand-swipe", swipe, sample.Palm, 0.92f);
                detector.ResetHandHistoryKeepingLatest(sample);
                return;
            }

            if (detector.TryDetectSnap(sample, out var snap))
            {
                PushMotion("native-snap", snap, sample.Palm, 0.9f);
            }
        }

        private static MotionGestureDetector.HandSample BuildHandSample(IReadOnlyList<Vector2> landmarks, Vector2 fallbackPalm, GestureType gesture, float sampleTime)
        {
            var palm = fallbackPalm;
            if (landmarks != null && landmarks.Count > 17)
            {
                palm = (landmarks[0] + landmarks[5] + landmarks[17]) / 3f;
            }

            return new MotionGestureDetector.HandSample
            {
                Time = sampleTime,
                Palm = palm,
                ThumbTip = landmarks != null && landmarks.Count > 4 ? landmarks[4] : palm,
                MiddleTip = landmarks != null && landmarks.Count > 12 ? landmarks[12] : palm,
                StaticGesture = gesture,
                HasSnapData = landmarks != null && landmarks.Count > 12
            };
        }

        private void PushMotion(string source, MotionGestureType gesture, Vector2 position, float confidence)
        {
            LogMotionDecision(source, gesture, position, confidence);
            nativeProvider.PushMotionGesture(gesture, position, confidence);
        }

        private void LogMotionDecision(string source, MotionGestureType gesture, Vector2 position, float confidence)
        {
            if (!debugLogs)
            {
                return;
            }

            Debug.Log($"[Gesture][NativeMotionRecognizer] source={source} gesture={gesture} position={position} confidence={confidence:F2} handSamples={detector.HandSampleCount}", this);
        }
    }
}
