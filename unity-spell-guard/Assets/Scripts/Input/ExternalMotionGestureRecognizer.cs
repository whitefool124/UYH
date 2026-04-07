using System.Collections.Generic;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public class ExternalMotionGestureRecognizer : MonoBehaviour
    {
        private struct HandSample
        {
            public float Time;
            public Vector2 Palm;
            public Vector2 ThumbTip;
            public Vector2 MiddleTip;
            public bool HasSnapData;
        }

        [SerializeField] private ExternalGestureBridgeProvider bridgeProvider;
        [SerializeField] private float historySeconds = 0.45f;
        [SerializeField] private float swipeMinDistance = 0.18f;
        [SerializeField] private float swipeMaxVerticalDrift = 0.16f;
        [SerializeField] private float swipeCooldownSeconds = 0.35f;
        [SerializeField] private float snapCloseDistance = 0.055f;
        [SerializeField] private float snapReleaseDistance = 0.11f;
        [SerializeField] private float snapMaxDuration = 0.18f;
        [SerializeField] private float snapCooldownSeconds = 0.45f;

        private readonly Queue<HandSample> history = new Queue<HandSample>();
        private int lastProcessedFrameVersion = -1;
        private float lastSwipeTime = -999f;
        private float lastSnapTime = -999f;
        private bool snapPrimed;
        private float snapPrimedTime;

        public void Configure(ExternalGestureBridgeProvider provider)
        {
            bridgeProvider = provider;
        }

        private void Update()
        {
            if (bridgeProvider == null || bridgeProvider.FrameVersion == lastProcessedFrameVersion)
            {
                return;
            }

            lastProcessedFrameVersion = bridgeProvider.FrameVersion;
            var frame = bridgeProvider.CurrentFrame;
            if (frame == null || !frame.handPresent || !bridgeProvider.HasHandLandmarks)
            {
                ResetState();
                return;
            }

            var landmarks = bridgeProvider.HandLandmarks;
            var sample = BuildSample(landmarks, frame.ResolveViewportPosition());
            history.Enqueue(sample);
            TrimHistory(sample.Time);

            if (TryDetectSwipe(out var swipe))
            {
                bridgeProvider.PushMotionGesture(swipe, sample.Palm, 0.92f);
                ResetHistoryKeepingLatest(sample);
                return;
            }

            if (TryDetectSnap(sample, out var snap))
            {
                bridgeProvider.PushMotionGesture(snap, sample.Palm, 0.9f);
            }
        }

        private HandSample BuildSample(IReadOnlyList<Vector2> landmarks, Vector2 fallbackPalm)
        {
            var palm = fallbackPalm;
            if (landmarks.Count > 17)
            {
                palm = (landmarks[0] + landmarks[5] + landmarks[17]) / 3f;
            }

            var sample = new HandSample
            {
                Time = Time.time,
                Palm = palm,
                ThumbTip = landmarks.Count > 4 ? landmarks[4] : palm,
                MiddleTip = landmarks.Count > 12 ? landmarks[12] : palm,
                HasSnapData = landmarks.Count > 12
            };
            return sample;
        }

        private void TrimHistory(float currentTime)
        {
            while (history.Count > 0 && currentTime - history.Peek().Time > historySeconds)
            {
                history.Dequeue();
            }
        }

        private bool TryDetectSwipe(out MotionGestureType gesture)
        {
            gesture = MotionGestureType.None;
            if (history.Count < 3)
            {
                return false;
            }

            var samples = history.ToArray();
            var first = samples[0];
            var last = samples[samples.Length - 1];
            var duration = Mathf.Max(0.0001f, last.Time - first.Time);
            var horizontalDelta = last.Palm.x - first.Palm.x;
            var verticalDrift = Mathf.Abs(last.Palm.y - first.Palm.y);
            var speed = Mathf.Abs(horizontalDelta) / duration;

            if (verticalDrift > swipeMaxVerticalDrift || Mathf.Abs(horizontalDelta) < swipeMinDistance || speed < 0.45f)
            {
                return false;
            }

            if (Time.time - lastSwipeTime < swipeCooldownSeconds)
            {
                return false;
            }

            lastSwipeTime = Time.time;
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

            if (tipDistance < snapReleaseDistance || Time.time - lastSnapTime < snapCooldownSeconds)
            {
                return false;
            }

            snapPrimed = false;
            lastSnapTime = Time.time;
            gesture = MotionGestureType.Snap;
            return true;
        }

        private void ResetHistoryKeepingLatest(HandSample latest)
        {
            history.Clear();
            history.Enqueue(latest);
        }

        private void ResetState()
        {
            history.Clear();
            snapPrimed = false;
        }
    }
}
