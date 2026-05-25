using System.Collections.Generic;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public sealed class MotionGestureDetector
    {
        public struct HandSample
        {
            public float Time;
            public Vector2 Palm;
            public Vector2 ThumbTip;
            public Vector2 MiddleTip;
            public GestureType StaticGesture;
            public bool HasSnapData;
        }

        public struct PoseSample
        {
            public float Time;
            public Vector2 ShoulderCenter;
            public float ShoulderVisibility;
        }

        private readonly Queue<HandSample> handHistory = new Queue<HandSample>();
        private readonly Queue<PoseSample> poseHistory = new Queue<PoseSample>();
        private float historySeconds = 0.7f;
        private float sampleJitterDeadZone = 0.015f;
        private float swipeMinDistance = 0.09f;
        private float swipeMaxVerticalDrift = 0.22f;
        private float swipeMinSpeed = 0.2f;
        private float swipeCooldownSeconds = 0.28f;
        private float slapMinDistance = 0.11f;
        private float slapMinOpenPalmRatio = 0.8f;
        private float slapMinSpeed = 0.24f;
        private float slapCooldownSeconds = 0.32f;
        private float pointHoldMinDuration = 0.08f;
        private float gestureTransitionMaxDuration = 0.4f;
        private float gestureTransitionMaxTravel = 0.18f;
        private float gestureTransitionCooldownSeconds = 0.45f;
        private float snapCloseDistance = 0.09f;
        private float snapReleaseDistance = 0.14f;
        private float snapMaxDuration = 0.35f;
        private float snapCooldownSeconds = 0.45f;
        private float bodyShiftMinDistance = 0.1f;
        private float bodyShiftMaxVerticalDrift = 0.12f;
        private float bodyShiftMinSpeed = 0.28f;
        private float bodyShiftCooldownSeconds = 0.45f;

        private float lastSwipeTime = -999f;
        private float lastSlapTime = -999f;
        private float lastSnapTime = -999f;
        private float lastTransitionTime = -999f;
        private float lastBodyShiftTime = -999f;
        private bool snapPrimed;
        private bool hasLastAcceptedHandSample;
        private Vector2 lastAcceptedPalm;
        private float lastAcceptedTipDistance;
        private GestureType lastAcceptedGesture = GestureType.None;
        private float snapPrimedTime;
        private GestureType lastObservedGesture = GestureType.None;
        private float lastGestureChangeTime = -999f;
        private Vector2 lastGestureChangePalm = new Vector2(0.5f, 0.5f);

        public int HandSampleCount => handHistory.Count;
        public int PoseSampleCount => poseHistory.Count;

        public void Configure(
            float historySeconds,
            float sampleJitterDeadZone,
            float swipeMinDistance,
            float swipeMaxVerticalDrift,
            float swipeMinSpeed,
            float swipeCooldownSeconds,
            float slapMinDistance,
            float slapMinOpenPalmRatio,
            float slapMinSpeed,
            float slapCooldownSeconds,
            float pointHoldMinDuration,
            float gestureTransitionMaxDuration,
            float gestureTransitionMaxTravel,
            float gestureTransitionCooldownSeconds,
            float snapCloseDistance,
            float snapReleaseDistance,
            float snapMaxDuration,
            float snapCooldownSeconds,
            float bodyShiftMinDistance,
            float bodyShiftMaxVerticalDrift,
            float bodyShiftMinSpeed,
            float bodyShiftCooldownSeconds)
        {
            this.historySeconds = historySeconds;
            this.sampleJitterDeadZone = sampleJitterDeadZone;
            this.swipeMinDistance = swipeMinDistance;
            this.swipeMaxVerticalDrift = swipeMaxVerticalDrift;
            this.swipeMinSpeed = swipeMinSpeed;
            this.swipeCooldownSeconds = swipeCooldownSeconds;
            this.slapMinDistance = slapMinDistance;
            this.slapMinOpenPalmRatio = slapMinOpenPalmRatio;
            this.slapMinSpeed = slapMinSpeed;
            this.slapCooldownSeconds = slapCooldownSeconds;
            this.pointHoldMinDuration = pointHoldMinDuration;
            this.gestureTransitionMaxDuration = gestureTransitionMaxDuration;
            this.gestureTransitionMaxTravel = gestureTransitionMaxTravel;
            this.gestureTransitionCooldownSeconds = gestureTransitionCooldownSeconds;
            this.snapCloseDistance = snapCloseDistance;
            this.snapReleaseDistance = snapReleaseDistance;
            this.snapMaxDuration = snapMaxDuration;
            this.snapCooldownSeconds = snapCooldownSeconds;
            this.bodyShiftMinDistance = bodyShiftMinDistance;
            this.bodyShiftMaxVerticalDrift = bodyShiftMaxVerticalDrift;
            this.bodyShiftMinSpeed = bodyShiftMinSpeed;
            this.bodyShiftCooldownSeconds = bodyShiftCooldownSeconds;
        }

        public bool AddHandSample(HandSample sample, bool compareStaticGestureForJitter)
        {
            var tipDistance = sample.HasSnapData ? Vector2.Distance(sample.ThumbTip, sample.MiddleTip) : 0f;
            if (hasLastAcceptedHandSample
                && Vector2.Distance(sample.Palm, lastAcceptedPalm) < sampleJitterDeadZone
                && Mathf.Abs(tipDistance - lastAcceptedTipDistance) < sampleJitterDeadZone
                && (!compareStaticGestureForJitter || sample.StaticGesture == lastAcceptedGesture))
            {
                TrimHandHistory(sample.Time);
                return false;
            }

            handHistory.Enqueue(sample);
            hasLastAcceptedHandSample = true;
            lastAcceptedPalm = sample.Palm;
            lastAcceptedTipDistance = tipDistance;
            lastAcceptedGesture = sample.StaticGesture;
            TrimHandHistory(sample.Time);
            return true;
        }

        public void AddPoseSample(PoseSample sample)
        {
            poseHistory.Enqueue(sample);
            TrimPoseHistory(sample.Time);
        }

        public bool TryDetectGestureTransition(HandSample sample, out MotionGestureType gesture)
        {
            gesture = MotionGestureType.None;
            if (sample.StaticGesture == GestureType.None || sample.StaticGesture == GestureType.Unknown)
            {
                lastObservedGesture = GestureType.None;
                return false;
            }

            if (sample.StaticGesture == lastObservedGesture)
            {
                return false;
            }

            var previousGesture = lastObservedGesture;
            var previousChangeTime = lastGestureChangeTime;
            var previousPalm = lastGestureChangePalm;
            lastObservedGesture = sample.StaticGesture;
            lastGestureChangeTime = sample.Time;
            lastGestureChangePalm = sample.Palm;

            if (previousGesture == GestureType.Point
                && sample.StaticGesture == GestureType.Fist
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

        public bool TryDetectOpenPalmSlap(out MotionGestureType gesture)
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
                if (samples[i].StaticGesture == GestureType.OpenPalm)
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

            if (verticalDrift > swipeMaxVerticalDrift || Mathf.Abs(horizontalDelta) < slapMinDistance || speed < slapMinSpeed)
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

        public bool TryDetectSwipe(out MotionGestureType gesture)
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

        public bool TryDetectSnap(HandSample sample, out MotionGestureType gesture)
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

        public bool TryDetectBodyShift(out MotionGestureType gesture)
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

        public void ResetHandHistoryKeepingLatest(HandSample latest)
        {
            handHistory.Clear();
            handHistory.Enqueue(latest);
            hasLastAcceptedHandSample = true;
            lastAcceptedPalm = latest.Palm;
            lastAcceptedTipDistance = latest.HasSnapData ? Vector2.Distance(latest.ThumbTip, latest.MiddleTip) : 0f;
            lastAcceptedGesture = latest.StaticGesture;
        }

        public void ResetPoseHistoryKeepingLatest(PoseSample latest)
        {
            poseHistory.Clear();
            poseHistory.Enqueue(latest);
        }

        public void ResetPoseState()
        {
            poseHistory.Clear();
        }

        public void ResetHandState()
        {
            handHistory.Clear();
            snapPrimed = false;
            hasLastAcceptedHandSample = false;
            lastAcceptedGesture = GestureType.None;
            lastObservedGesture = GestureType.None;
        }

        public void ResetAll()
        {
            ResetHandState();
            ResetPoseState();
        }

        private void TrimHandHistory(float currentTime)
        {
            while (handHistory.Count > 0 && currentTime - handHistory.Peek().Time > historySeconds)
            {
                handHistory.Dequeue();
            }
        }

        private void TrimPoseHistory(float currentTime)
        {
            while (poseHistory.Count > 0 && currentTime - poseHistory.Peek().Time > historySeconds)
            {
                poseHistory.Dequeue();
            }
        }
    }
}
