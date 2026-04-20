using System.Collections.Generic;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public class NativeMotionGestureRecognizer : MonoBehaviour
    {
        private struct HandSample
        {
            public float Time;
            public Vector2 Palm;
            public Vector2 ThumbTip;
            public Vector2 MiddleTip;
            public GestureType Gesture;
            public bool HasSnapData;
        }

        [SerializeField] private NativeMediapipeGestureProvider nativeProvider;
        [SerializeField] private bool debugLogs = true;
        [SerializeField] private float historySeconds = 0.7f;
        [SerializeField] private float swipeMinDistance = 0.09f;
        [SerializeField] private float swipeMaxVerticalDrift = 0.22f;
        [SerializeField] private float swipeCooldownSeconds = 0.28f;
        [SerializeField] private float slapMinDistance = 0.11f;
        [SerializeField] private float slapMinOpenPalmRatio = 0.8f;
        [SerializeField] private float slapCooldownSeconds = 0.32f;
        [SerializeField] private float pointHoldMinDuration = 0.08f;
        [SerializeField] private float gestureTransitionMaxDuration = 0.4f;
        [SerializeField] private float gestureTransitionMaxTravel = 0.18f;
        [SerializeField] private float gestureTransitionCooldownSeconds = 0.45f;
        [SerializeField] private float snapCloseDistance = 0.09f;
        [SerializeField] private float snapReleaseDistance = 0.14f;
        [SerializeField] private float snapMaxDuration = 0.35f;
        [SerializeField] private float snapCooldownSeconds = 0.45f;

        private readonly Queue<HandSample> handHistory = new Queue<HandSample>();
        private int lastProcessedFrameVersion = -1;
        private float lastSwipeTime = -999f;
        private float lastSlapTime = -999f;
        private float lastSnapTime = -999f;
        private float lastTransitionTime = -999f;
        private bool snapPrimed;
        private float snapPrimedTime;
        private GestureType lastObservedGesture = GestureType.None;
        private float lastObservedGestureStartTime = -999f;
        private float lastGestureChangeTime = -999f;
        private Vector2 lastGestureChangePalm = new Vector2(0.5f, 0.5f);

        public void Configure(NativeMediapipeGestureProvider provider)
        {
            nativeProvider = provider;
        }

        private void Update()
        {
            if (nativeProvider == null)
            {
                return;
            }

            if (nativeProvider.FrameVersion == lastProcessedFrameVersion)
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
                ResetState();
                nativeProvider.ClearMotionGesture();
                return;
            }

            var sampleTime = nativeProvider.LastSampleTime > 0f ? nativeProvider.LastSampleTime : Time.time;
            var sample = BuildHandSample(nativeProvider.HandLandmarks, snapshot.ViewportPosition, snapshot.Gesture, sampleTime);
            handHistory.Enqueue(sample);
            TrimHistory(sample.Time);

            if (TryDetectGestureTransition(sample, out var transition))
            {
                LogMotionDecision("native-gesture-transition", transition, sample.Palm, 0.88f);
                nativeProvider.PushMotionGesture(transition, sample.Palm, 0.88f);
                return;
            }

            if (TryDetectOpenPalmSlap(out var slap))
            {
                LogMotionDecision("native-open-palm-slap", slap, sample.Palm, 0.93f);
                nativeProvider.PushMotionGesture(slap, sample.Palm, 0.93f);
                ResetHandHistoryKeepingLatest(sample);
                return;
            }

            if (TryDetectSwipe(out var swipe))
            {
                LogMotionDecision("native-hand-swipe", swipe, sample.Palm, 0.92f);
                nativeProvider.PushMotionGesture(swipe, sample.Palm, 0.92f);
                ResetHandHistoryKeepingLatest(sample);
                return;
            }

            if (TryDetectSnap(sample, out var snap))
            {
                LogMotionDecision("native-snap", snap, sample.Palm, 0.9f);
                nativeProvider.PushMotionGesture(snap, sample.Palm, 0.9f);
            }
        }

        private static HandSample BuildHandSample(IReadOnlyList<Vector2> landmarks, Vector2 fallbackPalm, GestureType gesture, float sampleTime)
        {
            var palm = fallbackPalm;
            if (landmarks != null && landmarks.Count > 17)
            {
                palm = (landmarks[0] + landmarks[5] + landmarks[17]) / 3f;
            }

            return new HandSample
            {
                Time = sampleTime,
                Palm = palm,
                ThumbTip = landmarks != null && landmarks.Count > 4 ? landmarks[4] : palm,
                MiddleTip = landmarks != null && landmarks.Count > 12 ? landmarks[12] : palm,
                Gesture = gesture,
                HasSnapData = landmarks != null && landmarks.Count > 12
            };
        }

        private bool TryDetectGestureTransition(HandSample sample, out MotionGestureType gesture)
        {
            gesture = MotionGestureType.None;

            if (sample.Gesture == GestureType.None || sample.Gesture == GestureType.Unknown)
            {
                lastObservedGesture = GestureType.None;
                return false;
            }

            if (sample.Gesture == lastObservedGesture)
            {
                return false;
            }

            var previousGesture = lastObservedGesture;
            var previousChangeTime = lastGestureChangeTime;
            var previousPalm = lastGestureChangePalm;

            lastObservedGesture = sample.Gesture;
            lastObservedGestureStartTime = sample.Time;
            lastGestureChangeTime = sample.Time;
            lastGestureChangePalm = sample.Palm;

            if (previousGesture == GestureType.Point
                && sample.Gesture == GestureType.Fist
                && previousChangeTime > 0f
                && sample.Time - previousChangeTime >= pointHoldMinDuration
                && sample.Time - previousChangeTime <= gestureTransitionMaxDuration
                && Vector2.Distance(sample.Palm, previousPalm) <= gestureTransitionMaxTravel
                && sample.Time - lastTransitionTime >= gestureTransitionCooldownSeconds)
            {
                lastTransitionTime = sample.Time;
                gesture = MotionGestureType.PointToFist;
                return true;
            }

            return false;
        }

        private bool TryDetectOpenPalmSlap(out MotionGestureType gesture)
        {
            gesture = MotionGestureType.None;
            if (handHistory.Count < 3)
            {
                return false;
            }

            var samples = handHistory.ToArray();
            var openPalmSamples = 0;
            for (var i = 0; i < samples.Length; i++)
            {
                if (samples[i].Gesture == GestureType.OpenPalm)
                {
                    openPalmSamples += 1;
                }
            }

            if (openPalmSamples < Mathf.CeilToInt(samples.Length * slapMinOpenPalmRatio))
            {
                return false;
            }

            var first = samples[0];
            var last = samples[samples.Length - 1];
            var duration = Mathf.Max(0.0001f, last.Time - first.Time);
            var horizontalDelta = last.Palm.x - first.Palm.x;
            var verticalDrift = Mathf.Abs(last.Palm.y - first.Palm.y);
            var speed = Mathf.Abs(horizontalDelta) / duration;

            if (verticalDrift > swipeMaxVerticalDrift || Mathf.Abs(horizontalDelta) < slapMinDistance || speed < 0.24f)
            {
                return false;
            }

            if (last.Time - lastSlapTime < slapCooldownSeconds)
            {
                return false;
            }

            lastSlapTime = last.Time;
            gesture = horizontalDelta > 0f ? MotionGestureType.OpenPalmSlapLeftToRight : MotionGestureType.OpenPalmSlapRightToLeft;
            return true;
        }

        private void TrimHistory(float currentTime)
        {
            while (handHistory.Count > 0 && currentTime - handHistory.Peek().Time > historySeconds)
            {
                handHistory.Dequeue();
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
            var verticalDrift = Mathf.Abs(last.Palm.y - first.Palm.y);
            var speed = Mathf.Abs(horizontalDelta) / duration;

            if (verticalDrift > swipeMaxVerticalDrift || Mathf.Abs(horizontalDelta) < swipeMinDistance || speed < 0.2f)
            {
                return false;
            }

            if (last.Time - lastSwipeTime < swipeCooldownSeconds)
            {
                return false;
            }

            lastSwipeTime = last.Time;
            gesture = horizontalDelta > 0f ? MotionGestureType.SwipeLeftToRight : MotionGestureType.SwipeRightToLeft;
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
        }

        private void ResetState()
        {
            handHistory.Clear();
            snapPrimed = false;
            lastObservedGesture = GestureType.None;
            lastObservedGestureStartTime = -999f;
        }

        private void LogMotionDecision(string source, MotionGestureType gesture, Vector2 position, float confidence)
        {
            if (!debugLogs)
            {
                return;
            }

            Debug.Log($"[Gesture][NativeMotionRecognizer] source={source} gesture={gesture} position={position} confidence={confidence:F2} handSamples={handHistory.Count}", this);
        }
    }
}
