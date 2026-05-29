using System.Collections.Generic;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public sealed class MotionGestureDetector
    {
        private const int SwipeMinSamples = 3;
        private const float SwipeAxisDominanceRatio = 1.45f;
        private const float SwipeMinimumDuration = 0.035f;
        private const float SwipeMinimumPathEfficiency = 0.72f;
        private const float SwipeMaximumOppositeTravelRatio = 0.22f;
        private const float SwipeRelaxedDistanceMultiplier = 0.78f;
        private const float SwipeRelaxedSpeedMultiplier = 0.82f;
        private const float SwipeDownDistanceMultiplier = 1f;
        private const float SwipeDownSpeedMultiplier = 1f;
        private const float SwipeDownAxisDominanceRatio = 1.5f;
        private const float SwipeHorizontalPointSampleRatio = 0.5f;
        private const float SwipeVerticalPointSampleRatio = 0.7f;

        public struct HandSample
        {
            public float Time;
            public Vector2 Palm;
            public Vector2 SwipePoint;
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
        private float swipeCooldownSeconds = 2f;
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
            if (handHistory.Count < SwipeMinSamples)
            {
                return false;
            }

            var samples = handHistory.ToArray();
            var last = samples[samples.Length - 1];
            var bestGesture = MotionGestureType.None;
            var bestScore = 0f;

            for (var startIndex = samples.Length - 2; startIndex >= 0; startIndex--)
            {
                var start = samples[startIndex];
                var sampleCount = samples.Length - startIndex;
                if (sampleCount < SwipeMinSamples)
                {
                    continue;
                }

                var horizontalDirection = Mathf.Sign(GetSwipePosition(last, true).x - GetSwipePosition(start, true).x);
                var verticalDirection = Mathf.Sign(GetSwipePosition(last, false).y - GetSwipePosition(start, false).y);
                if (!HasRequiredPointGesture(samples, startIndex, true)
                    && !HasRequiredPointGesture(samples, startIndex, false))
                {
                    continue;
                }

                var duration = last.Time - start.Time;
                if (duration < SwipeMinimumDuration)
                {
                    continue;
                }

                var horizontalDelta = GetSwipePosition(last, true) - GetSwipePosition(start, true);
                var verticalDelta = GetSwipePosition(last, false) - GetSwipePosition(start, false);
                var delta = verticalDelta;
                delta.x = horizontalDelta.x;
                var horizontalDistance = Mathf.Abs(delta.x);
                var verticalDistance = Mathf.Abs(delta.y);
                var horizontalSwipeDetected = horizontalDistance >= swipeMinDistance * GetSwipeDistanceMultiplier(true, horizontalDirection)
                    && HasRequiredPointGesture(samples, startIndex, true)
                    && verticalDistance <= swipeMaxVerticalDrift
                    && horizontalDistance / duration >= swipeMinSpeed * GetSwipeSpeedMultiplier(true, horizontalDirection)
                    && horizontalDistance >= verticalDistance * GetSwipeAxisDominanceRatio(true, horizontalDirection)
                    && HasStableSwipePath(samples, startIndex, true, horizontalDirection, horizontalDistance);
                var verticalSwipeDetected = verticalDistance >= swipeMinDistance * GetSwipeDistanceMultiplier(false, verticalDirection)
                    && HasRequiredPointGesture(samples, startIndex, false)
                    && horizontalDistance <= swipeMaxVerticalDrift
                    && verticalDistance / duration >= swipeMinSpeed * GetSwipeSpeedMultiplier(false, verticalDirection)
                    && verticalDistance >= horizontalDistance * GetSwipeAxisDominanceRatio(false, verticalDirection)
                    && HasStableSwipePath(samples, startIndex, false, verticalDirection, verticalDistance);

                if (!horizontalSwipeDetected && !verticalSwipeDetected)
                {
                    continue;
                }

                var score = Mathf.Max(horizontalDistance, verticalDistance) / duration;
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                if (horizontalSwipeDetected)
                {
                    bestGesture = delta.x > 0f ? MotionGestureType.SwipeLeftToRight : MotionGestureType.SwipeRightToLeft;
                    continue;
                }

                bestGesture = delta.y > 0f ? MotionGestureType.SwipeBottomToTop : MotionGestureType.SwipeTopToBottom;
            }

            if (bestGesture == MotionGestureType.None)
            {
                return false;
            }

            if (last.Time - lastSwipeTime < swipeCooldownSeconds)
            {
                return false;
            }

            lastSwipeTime = last.Time;
            gesture = bestGesture;
            return true;
        }

        private static float GetSwipeDistanceMultiplier(bool horizontalAxis, float direction)
        {
            return IsDownSwipe(horizontalAxis, direction) ? SwipeDownDistanceMultiplier : SwipeRelaxedDistanceMultiplier;
        }

        private static float GetSwipeSpeedMultiplier(bool horizontalAxis, float direction)
        {
            return IsDownSwipe(horizontalAxis, direction) ? SwipeDownSpeedMultiplier : SwipeRelaxedSpeedMultiplier;
        }

        private static float GetSwipeAxisDominanceRatio(bool horizontalAxis, float direction)
        {
            return IsDownSwipe(horizontalAxis, direction) ? SwipeDownAxisDominanceRatio : SwipeAxisDominanceRatio;
        }

        private static bool IsDownSwipe(bool horizontalAxis, float direction)
        {
            return !horizontalAxis && direction < 0f;
        }

        private static Vector2 GetSwipePosition(HandSample sample, bool horizontalAxis)
        {
            return horizontalAxis && sample.HasSnapData ? sample.SwipePoint : sample.Palm;
        }

        private static bool HasRequiredPointGesture(HandSample[] samples, int startIndex, bool horizontalAxis)
        {
            var pointSamples = 0;
            var sampleCount = samples.Length - startIndex;
            for (var index = startIndex; index < samples.Length; index++)
            {
                if (samples[index].StaticGesture == GestureType.Point)
                {
                    pointSamples += 1;
                }
            }

            var requiredRatio = horizontalAxis ? SwipeHorizontalPointSampleRatio : SwipeVerticalPointSampleRatio;
            var requiredPointSamples = Mathf.CeilToInt(sampleCount * requiredRatio);
            return pointSamples >= requiredPointSamples
                && samples[startIndex].StaticGesture == GestureType.Point;
        }

        private bool HasStableSwipePath(HandSample[] samples, int startIndex, bool horizontalAxis, float direction, float dominantDistance)
        {
            if (Mathf.Approximately(direction, 0f))
            {
                return false;
            }

            var progressSteps = 0;
            var pathDistance = 0f;
            var oppositeTravel = 0f;
            var stepDeadZone = Mathf.Max(sampleJitterDeadZone * 0.5f, 0.003f);
            for (var index = startIndex + 1; index < samples.Length; index++)
            {
                var delta = GetSwipePosition(samples[index], horizontalAxis) - GetSwipePosition(samples[index - 1], horizontalAxis);
                var axisDelta = horizontalAxis ? delta.x : delta.y;
                var directedDelta = axisDelta * direction;
                var absAxisDelta = Mathf.Abs(axisDelta);
                pathDistance += absAxisDelta;

                if (directedDelta >= stepDeadZone)
                {
                    progressSteps += 1;
                    continue;
                }

                if (directedDelta <= -stepDeadZone)
                {
                    oppositeTravel += -directedDelta;
                }
            }

            if (progressSteps < SwipeMinSamples - 1 || pathDistance <= 0.0001f)
            {
                return false;
            }

            var pathEfficiency = dominantDistance / pathDistance;
            return pathEfficiency >= SwipeMinimumPathEfficiency
                && oppositeTravel <= dominantDistance * SwipeMaximumOppositeTravelRatio;
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
