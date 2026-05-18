using System.Collections.Generic;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public class ExternalMotionGestureRecognizer : MonoBehaviour
    {
        [SerializeField] private bool debugLogs = true;

        private struct HandSample
        {
            public float Time;
            public Vector2 Palm;
            public Vector2 ThumbTip;
            public Vector2 MiddleTip;
            public bool HasSnapData;
        }

        private struct PoseSample
        {
            public float Time;
            public Vector2 ShoulderCenter;
            public float ShoulderVisibility;
        }

        [SerializeField] private ExternalGestureBridgeProvider bridgeProvider;
        [SerializeField] private GestureRecognitionProfile recognitionProfile;
        [SerializeField] private float historySeconds = 0.7f;
        [SerializeField] private float sampleJitterDeadZone = 0.015f;
        [SerializeField] private float swipeMinDistance = 0.09f;
        [SerializeField] private float swipeMaxVerticalDrift = 0.22f;
        [SerializeField] private float swipeMinSpeed = 0.2f;
        [SerializeField] private float swipeCooldownSeconds = 0.28f;
        [SerializeField] private float snapCloseDistance = 0.09f;
        [SerializeField] private float snapReleaseDistance = 0.14f;
        [SerializeField] private float snapMaxDuration = 0.35f;
        [SerializeField] private float snapCooldownSeconds = 0.45f;
        [SerializeField] private float bodyShiftMinDistance = 0.1f;
        [SerializeField] private float bodyShiftMaxVerticalDrift = 0.12f;
        [SerializeField] private float bodyShiftMinSpeed = 0.28f;
        [SerializeField] private float bodyShiftCooldownSeconds = 0.45f;
        [SerializeField] private float minPoseVisibility = 0.45f;

        private readonly Queue<HandSample> handHistory = new Queue<HandSample>();
        private readonly Queue<PoseSample> poseHistory = new Queue<PoseSample>();
        private float lastSwipeTime = -999f;
        private float lastSnapTime = -999f;
        private float lastBodyShiftTime = -999f;
        private bool snapPrimed;
        private bool hasLastAcceptedHandSample;
        private Vector2 lastAcceptedPalm;
        private float lastAcceptedTipDistance;
        private float snapPrimedTime;

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
            if (recognitionProfile == null)
            {
                return;
            }

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
                ResetState();
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
                var sample = BuildHandSample(landmarks, frame.ResolveViewportPosition(), sampleTime);
                var tipDistance = sample.HasSnapData ? Vector2.Distance(sample.ThumbTip, sample.MiddleTip) : 0f;
                if (hasLastAcceptedHandSample
                    && Vector2.Distance(sample.Palm, lastAcceptedPalm) < sampleJitterDeadZone
                    && Mathf.Abs(tipDistance - lastAcceptedTipDistance) < sampleJitterDeadZone)
                {
                    TrimHistory(handHistory, sample.Time);
                }
                else
                {
                    handHistory.Enqueue(sample);
                    hasLastAcceptedHandSample = true;
                    lastAcceptedPalm = sample.Palm;
                    lastAcceptedTipDistance = tipDistance;
                    TrimHistory(handHistory, sample.Time);
                }

                if (TryDetectSwipe(out var swipe))
                {
                    LogMotionDecision("hand-swipe", swipe, sample.Palm, 0.92f);
                    bridgeProvider.PushMotionGesture(swipe, sample.Palm, 0.92f);
                    ResetHandHistoryKeepingLatest(sample);
                    return;
                }

                if (TryDetectSnap(sample, out var snap))
                {
                    LogMotionDecision("snap", snap, sample.Palm, 0.9f);
                    bridgeProvider.PushMotionGesture(snap, sample.Palm, 0.9f);
                    return;
                }
            }
            else
            {
                ResetHandState();
            }

            var poseLandmarks = frame.poseLandmarks != null && frame.poseLandmarks.Length > 0
                ? ConvertLandmarks(frame.poseLandmarks)
                : null;

            if (TryBuildPoseSample(poseLandmarks, frame, sampleTime, out var poseSample))
            {
                processedAnyInput = true;
                poseHistory.Enqueue(poseSample);
                TrimHistory(poseHistory, poseSample.Time);

                if (TryDetectBodyShift(out var bodyShift))
                {
                    LogMotionDecision("pose-shift", bodyShift, poseSample.ShoulderCenter, poseSample.ShoulderVisibility);
                    bridgeProvider.PushMotionGesture(bodyShift, poseSample.ShoulderCenter, poseSample.ShoulderVisibility);
                    ResetPoseHistoryKeepingLatest(poseSample);
                    return;
                }
            }
            else
            {
                poseHistory.Clear();
            }

            if (!processedAnyInput)
            {
                ResetState();
            }
        }

        private HandSample BuildHandSample(IReadOnlyList<Vector2> landmarks, Vector2 fallbackPalm, float sampleTime)
        {
            var palm = fallbackPalm;
            if (landmarks != null && landmarks.Count > 17)
            {
                palm = (landmarks[0] + landmarks[5] + landmarks[17]) / 3f;
            }

            var sample = new HandSample
            {
                Time = sampleTime,
                Palm = palm,
                ThumbTip = landmarks != null && landmarks.Count > 4 ? landmarks[4] : palm,
                MiddleTip = landmarks != null && landmarks.Count > 12 ? landmarks[12] : palm,
                HasSnapData = landmarks != null && landmarks.Count > 12
            };
            return sample;
        }

        private bool TryBuildPoseSample(IReadOnlyList<Vector2> landmarks, ExternalVisionFrame frame, float sampleTime, out PoseSample sample)
        {
            sample = default;
            if (landmarks == null || landmarks.Count <= 24 || frame?.poseLandmarks == null || frame.poseLandmarks.Length <= 24)
            {
                return false;
            }

            var leftShoulder = landmarks[11];
            var rightShoulder = landmarks[12];
            var leftHip = landmarks[23];
            var rightHip = landmarks[24];
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

            var shoulderCenter = (leftShoulder + rightShoulder) * 0.5f;
            var hipCenter = (leftHip + rightHip) * 0.5f;
            sample = new PoseSample
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

        private void TrimHistory<T>(Queue<T> history, float currentTime) where T : struct
        {
            while (history.Count > 0)
            {
                float time;
                var peek = history.Peek();
                if (peek is HandSample handSample)
                {
                    time = handSample.Time;
                }
                else if (peek is PoseSample poseSample)
                {
                    time = poseSample.Time;
                }
                else
                {
                    break;
                }

                if (currentTime - time <= historySeconds)
                {
                    break;
                }

                history.Dequeue();
            }
        }

        private bool TryDetectSwipe(out MotionGestureType gesture)
        {
            gesture = MotionGestureType.None;
            if (handHistory.Count < 3)
            {
                return false;
            }

            var samples = handHistory.ToArray();
            var first = samples[0];
            var last = samples[samples.Length - 1];
            var duration = Mathf.Max(0.0001f, last.Time - first.Time);
            var horizontalDelta = last.Palm.x - first.Palm.x;
            var verticalDelta = last.Palm.y - first.Palm.y;
            var verticalDrift = Mathf.Abs(last.Palm.y - first.Palm.y);
            var horizontalDrift = Mathf.Abs(last.Palm.x - first.Palm.x);
            var speed = Mathf.Abs(horizontalDelta) / duration;

            var horizontalSwipeDetected = verticalDrift <= swipeMaxVerticalDrift && Mathf.Abs(horizontalDelta) >= swipeMinDistance && speed >= swipeMinSpeed;
            var verticalSwipeDetected = horizontalDrift <= swipeMaxVerticalDrift && Mathf.Abs(verticalDelta) >= swipeMinDistance && Mathf.Abs(verticalDelta) / duration >= swipeMinSpeed;

            if (!horizontalSwipeDetected && !verticalSwipeDetected)
            {
                return false;
            }

            if (last.Time - lastSwipeTime < swipeCooldownSeconds)
            {
                return false;
            }

            lastSwipeTime = last.Time;
            if (horizontalSwipeDetected && Mathf.Abs(horizontalDelta) >= Mathf.Abs(verticalDelta))
            {
                gesture = horizontalDelta > 0f ? MotionGestureType.SwipeLeftToRight : MotionGestureType.SwipeRightToLeft;
                return true;
            }

            gesture = verticalDelta > 0f ? MotionGestureType.SwipeBottomToTop : MotionGestureType.SwipeTopToBottom;
            return true;
        }

        private bool TryDetectBodyShift(out MotionGestureType gesture)
        {
            gesture = MotionGestureType.None;
            if (poseHistory.Count < 3)
            {
                return false;
            }

            var samples = poseHistory.ToArray();
            var first = samples[0];
            var last = samples[samples.Length - 1];
            var duration = Mathf.Max(0.0001f, last.Time - first.Time);
            var horizontalDelta = last.ShoulderCenter.x - first.ShoulderCenter.x;
            var verticalDrift = Mathf.Abs(last.ShoulderCenter.y - first.ShoulderCenter.y);
            var speed = Mathf.Abs(horizontalDelta) / duration;

            if (verticalDrift > bodyShiftMaxVerticalDrift || Mathf.Abs(horizontalDelta) < bodyShiftMinDistance || speed < bodyShiftMinSpeed)
            {
                return false;
            }

            if (last.Time - lastBodyShiftTime < bodyShiftCooldownSeconds)
            {
                return false;
            }

            lastBodyShiftTime = last.Time;
            gesture = horizontalDelta > 0f ? MotionGestureType.BodyShiftRight : MotionGestureType.BodyShiftLeft;
            return true;
        }

        private bool TryDetectSnap(HandSample sample, out MotionGestureType gesture)
        {
            gesture = MotionGestureType.None;
            if (!sample.HasSnapData)
            {
                snapPrimed = false;
                return false;
            }

            var tipDistance = Vector2.Distance(sample.ThumbTip, sample.MiddleTip);
            if (!snapPrimed)
            {
                if (tipDistance <= snapCloseDistance)
                {
                    snapPrimed = true;
                    snapPrimedTime = sample.Time;
                }

                return false;
            }

            if (sample.Time - snapPrimedTime > snapMaxDuration)
            {
                snapPrimed = false;
                return false;
            }

            if (tipDistance < snapReleaseDistance || sample.Time - lastSnapTime < snapCooldownSeconds)
            {
                return false;
            }

            snapPrimed = false;
            lastSnapTime = sample.Time;
            gesture = MotionGestureType.Snap;
            return true;
        }

        private void ResetHandHistoryKeepingLatest(HandSample latest)
        {
            handHistory.Clear();
            handHistory.Enqueue(latest);
            hasLastAcceptedHandSample = true;
            lastAcceptedPalm = latest.Palm;
            lastAcceptedTipDistance = latest.HasSnapData ? Vector2.Distance(latest.ThumbTip, latest.MiddleTip) : 0f;
        }

        private void ResetPoseHistoryKeepingLatest(PoseSample latest)
        {
            poseHistory.Clear();
            poseHistory.Enqueue(latest);
        }

        private void ResetHandState()
        {
            handHistory.Clear();
            snapPrimed = false;
            hasLastAcceptedHandSample = false;
        }

        private void ResetState()
        {
            handHistory.Clear();
            poseHistory.Clear();
            snapPrimed = false;
        }

        private void LogMotionDecision(string source, MotionGestureType gesture, Vector2 position, float confidence)
        {
            if (!debugLogs)
            {
                return;
            }

            Debug.Log($"[Gesture][MotionRecognizer] source={source} gesture={gesture} position={position} confidence={confidence:F2} handSamples={handHistory.Count} poseSamples={poseHistory.Count}", this);
        }
    }
}
