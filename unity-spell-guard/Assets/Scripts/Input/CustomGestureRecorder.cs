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
        private CustomGestureKind recordingKind = CustomGestureKind.DynamicMotion;
        private float stateStartedAt;
        private float recordingStartedAt;
        private float nextSampleAt;
        private GestureHandedness handedness = GestureHandedness.Unknown;
        private GestureHandedness targetHandedness = GestureHandedness.Right;
        private int invalidFrameCount;
        private string lastFailureReason = "无";

        public CustomGestureRecorderState State { get; private set; } = CustomGestureRecorderState.Idle;
        public CustomGestureSample LastSample { get; private set; }
        public string StatusText { get; private set; } = "自定义手势录制待命";
        public float Progress { get; private set; }
        public int CapturedFrameCount => frames.Count;
        public int InvalidFrameCount => invalidFrameCount;
        public string LastFailureReason => lastFailureReason;
        public bool IsBusy => State == CustomGestureRecorderState.Countdown || State == CustomGestureRecorderState.Recording;
        public GestureHandedness TargetHandedness => targetHandedness;

        public void Configure(float countdown, float duration, float sampleInterval, float minConfidence, CustomGestureKind kind)
        {
            countdownSeconds = Mathf.Max(0f, countdown);
            recordSeconds = Mathf.Max(0.2f, kind == CustomGestureKind.StaticPose ? Mathf.Min(duration, 1.2f) : duration);
            sampleIntervalSeconds = Mathf.Clamp(sampleInterval, 0.02f, 0.2f);
            minimumConfidence = Mathf.Clamp01(minConfidence);
            recordingKind = kind;
        }

        public void Configure(float countdown, float duration, float sampleInterval, float minConfidence)
        {
            Configure(countdown, duration, sampleInterval, minConfidence, CustomGestureKind.DynamicMotion);
        }

        public void SetTargetHandedness(GestureHandedness value)
        {
            targetHandedness = value == GestureHandedness.Left ? GestureHandedness.Left : GestureHandedness.Right;
        }

        public bool CanBegin(GestureFrame frame, out string reason)
        {
            return TryGetValidPrimaryHand(frame, out _, out reason);
        }

        public void Begin(float now)
        {
            frames.Clear();
            LastSample = null;
            invalidFrameCount = 0;
            lastFailureReason = "无";
            handedness = GestureHandedness.Unknown;
            stateStartedAt = now;
            recordingStartedAt = 0f;
            nextSampleAt = 0f;
            Progress = 0f;
            State = countdownSeconds > 0f ? CustomGestureRecorderState.Countdown : CustomGestureRecorderState.Recording;
            StatusText = State == CustomGestureRecorderState.Countdown ? $"{FormatKind(recordingKind)}倒计时：准备录制手部动作" : $"正在录制{FormatKind(recordingKind)}";
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
            lastFailureReason = "已取消";
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
                StatusText = $"{FormatKind(recordingKind)}倒计时：{remaining}";
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

            StatusText = $"正在录制{FormatKind(recordingKind)}：{Mathf.RoundToInt(Progress * 100f)}% · 有效 {frames.Count} / 无效 {invalidFrameCount}";
            if (now - recordingStartedAt < recordSeconds)
            {
                return false;
            }

            LastSample = BuildSample(now);
            State = CustomGestureRecorderState.Review;
            Progress = 1f;
            StatusText = LastSample != null
                ? $"样本有效：{LastSample.Frames.Count} 帧，可继续录制或保存"
                : $"样本无效：{lastFailureReason}（有效 {frames.Count} / 无效 {invalidFrameCount}）";
            return LastSample != null;
        }

        private void StartRecording(float now)
        {
            frames.Clear();
            invalidFrameCount = 0;
            lastFailureReason = "无";
            recordingStartedAt = now;
            nextSampleAt = now;
            State = CustomGestureRecorderState.Recording;
            Progress = 0f;
            StatusText = $"正在录制{FormatKind(recordingKind)}";
        }

        private void CaptureFrame(GestureFrame frame, float now)
        {
            if (!TryGetValidPrimaryHand(frame, out var hand, out var reason))
            {
                invalidFrameCount += 1;
                lastFailureReason = reason;
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
            var requiredFrames = recordingKind == CustomGestureKind.StaticPose ? 1 : 3;
            if (frames.Count < requiredFrames)
            {
                lastFailureReason = frames.Count == 0 ? lastFailureReason : $"有效帧不足：{frames.Count}/{requiredFrames}";
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

        private bool TryGetValidPrimaryHand(GestureFrame frame, out TrackedHandState hand, out string reason)
        {
            hand = frame.PrimaryHand;
            if (!frame.HasPrimaryHand)
            {
                reason = "未检测到主手";
                return false;
            }

            if (!hand.IsTracked)
            {
                reason = "主手未稳定追踪";
                return false;
            }

            if (hand.Handedness == GestureHandedness.Unknown)
            {
                reason = "正在等待左右手识别";
                return false;
            }

            if (hand.Confidence < minimumConfidence)
            {
                reason = $"追踪置信度不足：{hand.Confidence:F2} < {minimumConfidence:F2}";
                return false;
            }

            if (hand.Landmarks == null || hand.Landmarks.Length < CustomGestureFeatureExtractor.RequiredLandmarkCount)
            {
                reason = "landmark 不完整";
                return false;
            }

            reason = "无";
            return true;
        }

        private static string FormatHandedness(GestureHandedness value)
        {
            if (value == GestureHandedness.Left)
            {
                return "左手";
            }

            if (value == GestureHandedness.Right)
            {
                return "右手";
            }

            return "未知手";
        }

        private static string FormatKind(CustomGestureKind value)
        {
            return value == CustomGestureKind.StaticPose ? "静态手势" : "动态手势";
        }
    }
}
