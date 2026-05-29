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
        [SerializeField] private float missingHandGraceSeconds = 0.18f;
        [SerializeField] private float sparseSwipeWindowSeconds = 0.48f;
        [SerializeField] private float sparseSwipeMinDistance = 0.16f;
        [SerializeField] private float sparseSwipeMinSpeed = 0.45f;
        [SerializeField] private float sparseSwipeAxisDominanceRatio = 1.25f;
        [SerializeField] private float sparseSwipeMaxDrift = 0.26f;
        [SerializeField] private float sparseSwipePointGraceSeconds = 0.35f;
        [SerializeField] private bool invertExternalHorizontalMotion;
        [SerializeField] private bool invertExternalVerticalMotion;

        private readonly MotionGestureDetector detector = new MotionGestureDetector();
        private readonly Queue<MotionGestureDetector.HandSample> sparseHandHistory = new Queue<MotionGestureDetector.HandSample>();
        private float lastHandSampleTime = -999f;
        private float lastSparsePointTime = -999f;
        private float lastSparseSwipeTime = -999f;
        private int sparseSwipeAcceptedCount;
        private int sparseSwipeRejectedCooldownCount;
        private int sparseSwipeRejectedNoPointCount;
        private int sparseSwipeRejectedDistanceCount;
        private int sparseSwipeRejectedAxisCount;
        private string lastSparseSwipeReason = "idle";
        private Vector2 lastSparseSwipeDelta;
        private float lastSparseSwipeSpeed;

        public int SparseSwipeAcceptedCount => sparseSwipeAcceptedCount;
        public int SparseSwipeRejectedCooldownCount => sparseSwipeRejectedCooldownCount;
        public int SparseSwipeRejectedNoPointCount => sparseSwipeRejectedNoPointCount;
        public int SparseSwipeRejectedDistanceCount => sparseSwipeRejectedDistanceCount;
        public int SparseSwipeRejectedAxisCount => sparseSwipeRejectedAxisCount;
        public string LastSparseSwipeReason => lastSparseSwipeReason;
        public Vector2 LastSparseSwipeDelta => lastSparseSwipeDelta;
        public float LastSparseSwipeSpeed => lastSparseSwipeSpeed;
        public int SparseHistoryCount => sparseHandHistory.Count;

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

            if (frame.predicted)
            {
                ResetHandStateIfMissingTooLong(ResolveSampleTime(frame));
                return;
            }

            if (!string.IsNullOrWhiteSpace(frame.motionGesture) &&
                ExternalGestureBridgeProvider.ParseMotionGesture(frame.motionGesture) != MotionGestureType.None)
            {
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
                var sample = BuildHandSample(landmarks, frame.ResolveViewportPosition(), frame, sampleTime, true);
                lastHandSampleTime = sample.Time;
                AddSparseHandSample(sample);
                detector.AddHandSample(sample, false);

                if (detector.TryDetectSwipe(out var swipe))
                {
                    swipe = CorrectExternalDirection(swipe);
                    PushMotion("hand-swipe", swipe, sample.Palm, 0.92f);
                    detector.ResetHandHistoryKeepingLatest(sample);
                    ResetSparseHistoryKeepingLatest(sample);
                    return;
                }

                if (TryDetectSparseSwipe(sample, out var sparseSwipe))
                {
                    sparseSwipe = CorrectExternalDirection(sparseSwipe);
                    PushMotion("sparse-hand-swipe", sparseSwipe, sample.SwipePoint, 0.84f);
                    detector.ResetHandHistoryKeepingLatest(sample);
                    ResetSparseHistoryKeepingLatest(sample);
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
                ResetHandStateIfMissingTooLong(sampleTime);
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
                    bodyShift = CorrectExternalDirection(bodyShift);
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
                ResetHandStateIfMissingTooLong(sampleTime);
                detector.ResetPoseState();
            }
        }

        private void ResetHandStateIfMissingTooLong(float sampleTime)
        {
            if (sampleTime - lastHandSampleTime < missingHandGraceSeconds)
            {
                return;
            }

            detector.ResetHandState();
            sparseHandHistory.Clear();
        }

        private void AddSparseHandSample(MotionGestureDetector.HandSample sample)
        {
            sparseHandHistory.Enqueue(sample);
            if (sample.StaticGesture == GestureType.Point)
            {
                lastSparsePointTime = sample.Time;
            }

            TrimSparseHandHistory(sample.Time);
        }

        private bool TryDetectSparseSwipe(MotionGestureDetector.HandSample latest, out MotionGestureType gesture)
        {
            gesture = MotionGestureType.None;
            if (sparseHandHistory.Count < 2)
            {
                lastSparseSwipeReason = "history";
                return false;
            }

            if (latest.Time - lastSparseSwipeTime < swipeCooldownSeconds)
            {
                sparseSwipeRejectedCooldownCount++;
                lastSparseSwipeReason = "cooldown";
                return false;
            }

            if (latest.Time - lastSparsePointTime > sparseSwipePointGraceSeconds)
            {
                sparseSwipeRejectedNoPointCount++;
                lastSparseSwipeReason = "not-point";
                return false;
            }

            var samples = sparseHandHistory.ToArray();
            var bestGesture = MotionGestureType.None;
            var bestScore = 0f;
            var bestDelta = Vector2.zero;
            var sawDistanceCandidate = false;
            for (var index = 0; index < samples.Length - 1; index++)
            {
                var start = samples[index];
                var duration = latest.Time - start.Time;
                if (duration <= 0.0001f || duration > sparseSwipeWindowSeconds)
                {
                    continue;
                }

                if (start.StaticGesture != GestureType.Point && latest.StaticGesture != GestureType.Point)
                {
                    continue;
                }

                var delta = latest.SwipePoint - start.SwipePoint;
                var horizontalDistance = Mathf.Abs(delta.x);
                var verticalDistance = Mathf.Abs(delta.y);
                var distance = Mathf.Max(horizontalDistance, verticalDistance);
                var speed = distance / duration;
                if (distance < sparseSwipeMinDistance || speed < sparseSwipeMinSpeed)
                {
                    continue;
                }

                sawDistanceCandidate = true;

                MotionGestureType candidate;
                if (horizontalDistance >= verticalDistance * sparseSwipeAxisDominanceRatio && verticalDistance <= sparseSwipeMaxDrift)
                {
                    candidate = delta.x > 0f ? MotionGestureType.SwipeLeftToRight : MotionGestureType.SwipeRightToLeft;
                }
                else if (verticalDistance >= horizontalDistance * sparseSwipeAxisDominanceRatio && horizontalDistance <= sparseSwipeMaxDrift)
                {
                    candidate = delta.y > 0f ? MotionGestureType.SwipeBottomToTop : MotionGestureType.SwipeTopToBottom;
                }
                else
                {
                    continue;
                }

                if (speed <= bestScore)
                {
                    continue;
                }

                bestScore = speed;
                bestGesture = candidate;
                bestDelta = delta;
            }

            if (bestGesture == MotionGestureType.None)
            {
                if (sawDistanceCandidate)
                {
                    sparseSwipeRejectedAxisCount++;
                    lastSparseSwipeReason = "axis";
                }
                else
                {
                    sparseSwipeRejectedDistanceCount++;
                    lastSparseSwipeReason = "distance";
                }

                return false;
            }

            lastSparseSwipeTime = latest.Time;
            sparseSwipeAcceptedCount++;
            lastSparseSwipeReason = $"accepted:{bestGesture}";
            lastSparseSwipeDelta = bestDelta;
            lastSparseSwipeSpeed = bestScore;
            gesture = bestGesture;
            return true;
        }

        private void TrimSparseHandHistory(float sampleTime)
        {
            var window = Mathf.Max(sparseSwipeWindowSeconds, historySeconds);
            while (sparseHandHistory.Count > 0 && sampleTime - sparseHandHistory.Peek().Time > window)
            {
                sparseHandHistory.Dequeue();
            }
        }

        private void ResetSparseHistoryKeepingLatest(MotionGestureDetector.HandSample latest)
        {
            sparseHandHistory.Clear();
            sparseHandHistory.Enqueue(latest);
        }

        private static MotionGestureDetector.HandSample BuildHandSample(IReadOnlyList<Vector2> landmarks, Vector2 fallbackPalm, ExternalVisionFrame frame, float sampleTime, bool preferRawGesture = false)
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
                StaticGesture = ExternalGestureBridgeProvider.ParseGesture(preferRawGesture && !string.IsNullOrWhiteSpace(frame?.rawGesture) ? frame.rawGesture : frame?.gesture),
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

        private MotionGestureType CorrectExternalDirection(MotionGestureType gesture)
        {
            if (invertExternalHorizontalMotion)
            {
                if (gesture == MotionGestureType.SwipeLeftToRight)
                {
                    return MotionGestureType.SwipeRightToLeft;
                }

                if (gesture == MotionGestureType.SwipeRightToLeft)
                {
                    return MotionGestureType.SwipeLeftToRight;
                }

                if (gesture == MotionGestureType.OpenPalmSlapLeftToRight)
                {
                    return MotionGestureType.OpenPalmSlapRightToLeft;
                }

                if (gesture == MotionGestureType.OpenPalmSlapRightToLeft)
                {
                    return MotionGestureType.OpenPalmSlapLeftToRight;
                }

                if (gesture == MotionGestureType.BodyShiftLeft)
                {
                    return MotionGestureType.BodyShiftRight;
                }

                if (gesture == MotionGestureType.BodyShiftRight)
                {
                    return MotionGestureType.BodyShiftLeft;
                }
            }

            if (invertExternalVerticalMotion)
            {
                if (gesture == MotionGestureType.SwipeBottomToTop)
                {
                    return MotionGestureType.SwipeTopToBottom;
                }

                if (gesture == MotionGestureType.SwipeTopToBottom)
                {
                    return MotionGestureType.SwipeBottomToTop;
                }
            }

            return gesture;
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
