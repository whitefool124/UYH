using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public enum CustomGestureRecorderState
    {
        Idle,
        Countdown,
        Recording,
        Review,
        Saved
    }

    public sealed class CustomGestureRecorder
    {
        private readonly List<CustomGestureFrameSample> frames = new List<CustomGestureFrameSample>();
        private float countdownSeconds = 3f;
        private float recordSeconds = 1.2f;
        private float sampleIntervalSeconds = 0.06f;
        private float minimumConfidence = 0.55f;
        private float stateStartedAt;
        private float recordingStartedAt;
        private float nextSampleAt;
        private GestureHandedness handedness = GestureHandedness.Unknown;
        private GestureHandedness targetHandedness = GestureHandedness.Right;
        private int invalidFrameCount;

        public CustomGestureRecorderState State { get; private set; } = CustomGestureRecorderState.Idle;
        public CustomGestureSample LastSample { get; private set; }
        public string StatusText { get; private set; } = "自定义手势录制待命";
        public float Progress { get; private set; }
        public int CapturedFrameCount => frames.Count;
        public int InvalidFrameCount => invalidFrameCount;
        public bool IsBusy => State == CustomGestureRecorderState.Countdown || State == CustomGestureRecorderState.Recording;
        public GestureHandedness TargetHandedness => targetHandedness;

        public void Configure(float countdown, float duration, float sampleInterval, float minConfidence)
        {
            countdownSeconds = Mathf.Max(0f, countdown);
            recordSeconds = Mathf.Max(0.2f, duration);
            sampleIntervalSeconds = Mathf.Clamp(sampleInterval, 0.02f, 0.2f);
            minimumConfidence = Mathf.Clamp01(minConfidence);
        }

        public void SetTargetHandedness(GestureHandedness value)
        {
            targetHandedness = value == GestureHandedness.Left ? GestureHandedness.Left : GestureHandedness.Right;
        }

        public void Begin(float now)
        {
            frames.Clear();
            LastSample = null;
            invalidFrameCount = 0;
            handedness = GestureHandedness.Unknown;
            stateStartedAt = now;
            recordingStartedAt = 0f;
            nextSampleAt = 0f;
            Progress = 0f;
            State = countdownSeconds > 0f ? CustomGestureRecorderState.Countdown : CustomGestureRecorderState.Recording;
            StatusText = State == CustomGestureRecorderState.Countdown ? $"自定义手势倒计时：准备摆出{FormatHandedness(targetHandedness)}动作" : $"正在录制{FormatHandedness(targetHandedness)}自定义手势";
            if (State == CustomGestureRecorderState.Recording)
            {
                StartRecording(now);
            }
        }

        public void Cancel()
        {
            frames.Clear();
            LastSample = null;
            invalidFrameCount = 0;
            Progress = 0f;
            State = CustomGestureRecorderState.Idle;
            StatusText = "已取消自定义手势录制";
        }

        public void MarkSaved()
        {
            State = CustomGestureRecorderState.Saved;
            StatusText = "自定义手势样本已保存";
        }

        public bool Update(GestureFrame frame, float now)
        {
            if (State == CustomGestureRecorderState.Countdown)
            {
                Progress = countdownSeconds <= 0f ? 1f : Mathf.Clamp01((now - stateStartedAt) / countdownSeconds);
                var remaining = Mathf.CeilToInt(Mathf.Max(0f, countdownSeconds - (now - stateStartedAt)));
                StatusText = $"自定义手势倒计时：{remaining}";
                if (now - stateStartedAt >= countdownSeconds)
                {
                    StartRecording(now);
                }

                return false;
            }

            if (State != CustomGestureRecorderState.Recording)
            {
                return false;
            }

            Progress = Mathf.Clamp01((now - recordingStartedAt) / recordSeconds);
            if (now >= nextSampleAt)
            {
                CaptureFrame(frame, now);
                nextSampleAt = now + sampleIntervalSeconds;
            }

            StatusText = $"正在录制{FormatHandedness(targetHandedness)}自定义手势：{Mathf.RoundToInt(Progress * 100f)}%";
            if (now - recordingStartedAt < recordSeconds)
            {
                return false;
            }

            LastSample = BuildSample(now);
            State = CustomGestureRecorderState.Review;
            Progress = 1f;
            StatusText = LastSample != null
                ? $"样本有效：{LastSample.Frames.Count} 帧，可继续录制或保存"
                : $"样本无效：请确保{FormatHandedness(targetHandedness)}单手完整入镜且置信度足够";
            return LastSample != null;
        }

        private void StartRecording(float now)
        {
            frames.Clear();
            invalidFrameCount = 0;
            recordingStartedAt = now;
            nextSampleAt = now;
            State = CustomGestureRecorderState.Recording;
            Progress = 0f;
            StatusText = $"正在录制{FormatHandedness(targetHandedness)}自定义手势";
        }

        private void CaptureFrame(GestureFrame frame, float now)
        {
            if (!TryGetValidPrimaryHand(frame, out var hand))
            {
                invalidFrameCount += 1;
                return;
            }

            if (handedness == GestureHandedness.Unknown)
            {
                handedness = hand.Handedness;
            }

            var copied = new Vector2[CustomGestureFeatureExtractor.RequiredLandmarkCount];
            Array.Copy(hand.Landmarks, copied, copied.Length);
            frames.Add(new CustomGestureFrameSample
            {
                Time = Mathf.Max(0f, now - recordingStartedAt),
                Confidence = Mathf.Clamp01(hand.Confidence),
                Landmarks = copied
            });
        }

        private CustomGestureSample BuildSample(float now)
        {
            if (frames.Count < 3)
            {
                return null;
            }

            return new CustomGestureSample
            {
                SampleId = Guid.NewGuid().ToString("N"),
                Handedness = handedness,
                DurationSeconds = Mathf.Max(0.01f, now - recordingStartedAt),
                Frames = new List<CustomGestureFrameSample>(frames)
            };
        }

        private bool TryGetValidPrimaryHand(GestureFrame frame, out TrackedHandState hand)
        {
            hand = frame.PrimaryHand;
            return frame.HasPrimaryHand &&
                   hand.IsTracked &&
                   hand.Handedness == targetHandedness &&
                   hand.Confidence >= minimumConfidence &&
                   hand.Landmarks != null &&
                   hand.Landmarks.Length >= CustomGestureFeatureExtractor.RequiredLandmarkCount;
        }

        private static string FormatHandedness(GestureHandedness value)
        {
            return value == GestureHandedness.Left ? "左手" : "右手";
        }
    }
}
