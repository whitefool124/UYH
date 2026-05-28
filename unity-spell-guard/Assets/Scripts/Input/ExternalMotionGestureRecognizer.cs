using System.Collections.Generic;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public class ExternalMotionGestureRecognizer : MonoBehaviour
    {
        [SerializeField] private bool debugLogs = true;
        [SerializeField] private ExternalGestureBridgeProvider bridgeProvider;
        [SerializeField] private GestureRecognitionProfile recognitionProfile;
        [SerializeField] private float historySeconds = 0.7f;
        [SerializeField] private float sampleJitterDeadZone = 0.015f;
        [SerializeField] private float swipeMinDistance = 0.09f;
        [SerializeField] private float swipeMaxVerticalDrift = 0.22f;
        [SerializeField] private float swipeMinSpeed = 0.2f;
        [SerializeField] private float swipeCooldownSeconds = 2f;
        [SerializeField] private float snapCloseDistance = 0.09f;
        [SerializeField] private float snapReleaseDistance = 0.14f;
        [SerializeField] private float snapMaxDuration = 0.35f;
        [SerializeField] private float snapCooldownSeconds = 0.45f;
        [SerializeField] private float bodyShiftMinDistance = 0.1f;
        [SerializeField] private float bodyShiftMaxVerticalDrift = 0.12f;
        [SerializeField] private float bodyShiftMinSpeed = 0.28f;
        [SerializeField] private float bodyShiftCooldownSeconds = 0.45f;
        [SerializeField] private float minPoseVisibility = 0.45f;

        private readonly MotionGestureDetector detector = new MotionGestureDetector();

        public void Configure(ExternalGestureBridgeProvider provider)
        {
            bridgeProvider = provider;
            ApplyRecognitionProfile();
        }

        public void Configure(ExternalGestureBridgeProvider provider, GestureRecognitionProfile profile)
        {
            bridgeProvider = provider;
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
                snapCloseDistance = recognitionProfile.snapCloseDistance;
                snapReleaseDistance = recognitionProfile.snapReleaseDistance;
                snapMaxDuration = recognitionProfile.snapMaxDuration;
                snapCooldownSeconds = recognitionProfile.snapCooldownSeconds;
                bodyShiftMinDistance = recognitionProfile.bodyShiftMinDistance;
                bodyShiftMaxVerticalDrift = recognitionProfile.bodyShiftMaxVerticalDrift;
                bodyShiftMinSpeed = recognitionProfile.bodyShiftMinSpeed;
                bodyShiftCooldownSeconds = recognitionProfile.bodyShiftCooldownSeconds;
                minPoseVisibility = recognitionProfile.minPoseVisibility;
            }

            detector.Configure(
                historySeconds,
                sampleJitterDeadZone,
                swipeMinDistance,
                swipeMaxVerticalDrift,
                swipeMinSpeed,
                swipeCooldownSeconds,
                0.11f,
                0.8f,
                0.24f,
                0.32f,
                0.08f,
                0.4f,
                0.18f,
                0.45f,
                snapCloseDistance,
                snapReleaseDistance,
                snapMaxDuration,
                snapCooldownSeconds,
                bodyShiftMinDistance,
                bodyShiftMaxVerticalDrift,
                bodyShiftMinSpeed,
                bodyShiftCooldownSeconds);
        }

        private void Update()
        {
            if (bridgeProvider == null)
            {
                return;
            }

            while (bridgeProvider.TryDequeuePendingFrame(out var frame))
            {
                ProcessFrame(frame);
            }
        }

        private void ProcessFrame(ExternalVisionFrame frame)
        {
            if (frame == null)
            {
                detector.ResetAll();
                return;
            }

            var sampleTime = ResolveSampleTime(frame);
            var processedAnyInput = false;

            if (frame.handPresent)
            {
                processedAnyInput = true;
                var landmarks = frame.handLandmarks != null && frame.handLandmarks.Length > 0
                    ? ConvertLandmarks(frame.handLandmarks)
                    : null;
                var sample = BuildHandSample(landmarks, frame.ResolveViewportPosition(), frame, sampleTime);
                detector.AddHandSample(sample, false);

                if (detector.TryDetectSwipe(out var swipe))
                {
                    PushMotion("hand-swipe", swipe, sample.Palm, 0.92f);
                    detector.ResetHandHistoryKeepingLatest(sample);
                    return;
                }

                if (detector.TryDetectSnap(sample, out var snap))
                {
                    PushMotion("snap", snap, sample.Palm, 0.9f);
                    return;
                }
            }
            else
            {
                detector.ResetHandState();
            }

            var poseLandmarks = frame.poseLandmarks != null && frame.poseLandmarks.Length > 0
                ? ConvertLandmarks(frame.poseLandmarks)
                : null;

            if (TryBuildPoseSample(poseLandmarks, frame, sampleTime, out var poseSample))
            {
                processedAnyInput = true;
                detector.AddPoseSample(poseSample);

                if (detector.TryDetectBodyShift(out var bodyShift))
                {
                    PushMotion("pose-shift", bodyShift, poseSample.ShoulderCenter, poseSample.ShoulderVisibility);
                    detector.ResetPoseHistoryKeepingLatest(poseSample);
                    return;
                }
            }
            else
            {
                detector.ResetPoseState();
            }

            if (!processedAnyInput)
            {
                detector.ResetAll();
            }
        }

        private static MotionGestureDetector.HandSample BuildHandSample(IReadOnlyList<Vector2> landmarks, Vector2 fallbackPalm, ExternalVisionFrame frame, float sampleTime)
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
                SwipePoint = landmarks != null && landmarks.Count > 8 ? landmarks[8] : palm,
                ThumbTip = landmarks != null && landmarks.Count > 4 ? landmarks[4] : palm,
                MiddleTip = landmarks != null && landmarks.Count > 12 ? landmarks[12] : palm,
                StaticGesture = ExternalGestureBridgeProvider.ParseGesture(frame?.gesture),
                HasSnapData = landmarks != null && landmarks.Count > 12
            };
        }

        private bool TryBuildPoseSample(IReadOnlyList<Vector2> landmarks, ExternalVisionFrame frame, float sampleTime, out MotionGestureDetector.PoseSample sample)
        {
            sample = default;
            if (landmarks == null || landmarks.Count <= 24 || frame?.poseLandmarks == null || frame.poseLandmarks.Length <= 24)
            {
                return false;
            }

            var framePose = frame.poseLandmarks;
            var visibility = Mathf.Min(
                framePose[11].visibility,
                framePose[12].visibility,
                framePose[23].visibility,
                framePose[24].visibility
            );

            if (visibility < minPoseVisibility)
            {
                return false;
            }

            var shoulderCenter = (landmarks[11] + landmarks[12]) * 0.5f;
            var hipCenter = (landmarks[23] + landmarks[24]) * 0.5f;
            sample = new MotionGestureDetector.PoseSample
            {
                Time = sampleTime,
                ShoulderCenter = (shoulderCenter + hipCenter) * 0.5f,
                ShoulderVisibility = visibility
            };
            return true;
        }

        private static Vector2[] ConvertLandmarks(ExternalVisionPoint[] source)
        {
            var converted = new Vector2[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                converted[index] = source[index].ToViewportPosition();
            }

            return converted;
        }

        private static float ResolveSampleTime(ExternalVisionFrame frame)
        {
            return frame != null && frame.timestamp > 0f ? frame.timestamp : Time.time;
        }

        private void PushMotion(string source, MotionGestureType gesture, Vector2 position, float confidence)
        {
            LogMotionDecision(source, gesture, position, confidence);
            bridgeProvider.PushMotionGesture(gesture, position, confidence);
        }

        private void LogMotionDecision(string source, MotionGestureType gesture, Vector2 position, float confidence)
        {
            if (!debugLogs)
            {
                return;
            }

            Debug.Log($"[Gesture][MotionRecognizer] source={source} gesture={gesture} position={position} confidence={confidence:F2} handSamples={detector.HandSampleCount} poseSamples={detector.PoseSampleCount}", this);
        }
    }
}
