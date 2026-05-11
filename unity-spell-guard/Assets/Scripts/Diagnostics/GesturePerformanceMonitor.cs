using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SpellGuard.InputSystem;
using UnityEngine;

namespace SpellGuard.Diagnostics
{
    public sealed class GesturePerformanceMonitor : MonoBehaviour
    {
        [SerializeField] private GestureInputRouter inputRouter;
        [SerializeField] private ExternalGestureBridgeProvider externalBridge;
        [SerializeField] private bool recordOnStart = true;
        [SerializeField] private KeyCode toggleRecordingKey = KeyCode.F8;
        [SerializeField] private KeyCode exportKey = KeyCode.F9;
        [SerializeField] private string outputDirectoryName = "ExperimentResults";

        private readonly List<float> frameMsSamples = new List<float>();
        private readonly List<float> packetIntervalMsSamples = new List<float>();
        private readonly List<float> estimatedLatencyMsSamples = new List<float>();
        private readonly Dictionary<MotionGestureType, int> motionCounts = new Dictionary<MotionGestureType, int>();

        private string sessionId;
        private float recordingStartedAt;
        private int totalFrames;
        private int externalPacketCount;
        private int staticCommandCount;
        private int motionCommandCount;
        private int lastExternalFrameVersion = -1;
        private float lastExternalTimestamp = -1f;
        private double unityToExternalTimeOffset;
        private bool hasUnityToExternalTimeOffset;
        private string lastCommandKey;

        public bool IsRecording { get; private set; }
        public string LastExportPath { get; private set; } = string.Empty;
        public GesturePerformanceSummary CurrentSummary => BuildSummary();

        private void Start()
        {
            if (recordOnStart)
            {
                StartRecording();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleRecordingKey))
            {
                if (IsRecording)
                {
                    StopRecording();
                }
                else
                {
                    StartRecording();
                }
            }

            if (Input.GetKeyDown(exportKey))
            {
                ExportCsv();
            }

            if (!IsRecording)
            {
                return;
            }

            RecordFrame(Time.unscaledDeltaTime);
            RecordExternalFrameIfChanged();
            RecordCommandIfChanged();
        }

        public void Configure(GestureInputRouter router, ExternalGestureBridgeProvider bridge)
        {
            inputRouter = router;
            externalBridge = bridge;
        }

        public void StartRecording()
        {
            ResetSamples();
            IsRecording = true;
            sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            recordingStartedAt = Time.unscaledTime;
        }

        public void StopRecording()
        {
            IsRecording = false;
        }

        public string ExportCsv()
        {
            var directory = ResolveOutputDirectory();
            Directory.CreateDirectory(directory);

            var fileName = $"gesture_performance_{(string.IsNullOrWhiteSpace(sessionId) ? DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) : sessionId)}.csv";
            LastExportPath = Path.Combine(directory, fileName);
            File.WriteAllText(LastExportPath, BuildCsv(), Encoding.UTF8);
            return LastExportPath;
        }

        public string BuildCsv()
        {
            var summary = BuildSummary();
            var builder = new StringBuilder();
            builder.AppendLine("session_id,mode,source,elapsed_seconds,total_frames,average_fps,min_fps,average_frame_ms,p95_frame_ms,external_packets,avg_packet_interval_ms,avg_estimated_latency_ms,p95_estimated_latency_ms,static_commands,motion_commands,swipe_lr,swipe_rl,snap,point_to_fist,body_shift_left,body_shift_right");
            builder.Append(sessionId).Append(',')
                .Append(summary.Mode).Append(',')
                .Append(EscapeCsv(summary.Source)).Append(',')
                .Append(Format(summary.ElapsedSeconds)).Append(',')
                .Append(summary.TotalFrames).Append(',')
                .Append(Format(summary.AverageFps)).Append(',')
                .Append(Format(summary.MinFps)).Append(',')
                .Append(Format(summary.AverageFrameMs)).Append(',')
                .Append(Format(summary.P95FrameMs)).Append(',')
                .Append(summary.ExternalPackets).Append(',')
                .Append(Format(summary.AveragePacketIntervalMs)).Append(',')
                .Append(Format(summary.AverageEstimatedLatencyMs)).Append(',')
                .Append(Format(summary.P95EstimatedLatencyMs)).Append(',')
                .Append(summary.StaticCommands).Append(',')
                .Append(summary.MotionCommands).Append(',')
                .Append(GetMotionCount(MotionGestureType.SwipeLeftToRight)).Append(',')
                .Append(GetMotionCount(MotionGestureType.SwipeRightToLeft)).Append(',')
                .Append(GetMotionCount(MotionGestureType.Snap)).Append(',')
                .Append(GetMotionCount(MotionGestureType.PointToFist)).Append(',')
                .Append(GetMotionCount(MotionGestureType.BodyShiftLeft)).Append(',')
                .Append(GetMotionCount(MotionGestureType.BodyShiftRight)).AppendLine();
            return builder.ToString();
        }

        private void RecordFrame(float unscaledDeltaTime)
        {
            totalFrames++;
            frameMsSamples.Add(Mathf.Max(0.0001f, unscaledDeltaTime) * 1000f);
        }

        private void RecordExternalFrameIfChanged()
        {
            if (externalBridge == null || externalBridge.CurrentFrame == null || externalBridge.FrameVersion == lastExternalFrameVersion)
            {
                return;
            }

            var frame = externalBridge.CurrentFrame;
            lastExternalFrameVersion = externalBridge.FrameVersion;
            externalPacketCount++;

            if (lastExternalTimestamp > 0f && frame.timestamp > 0f)
            {
                packetIntervalMsSamples.Add(Mathf.Max(0f, frame.timestamp - lastExternalTimestamp) * 1000f);
            }

            if (frame.timestamp > 0f)
            {
                var unityNow = Time.realtimeSinceStartupAsDouble;
                if (!hasUnityToExternalTimeOffset)
                {
                    unityToExternalTimeOffset = unityNow - frame.timestamp;
                    hasUnityToExternalTimeOffset = true;
                }

                var estimatedLatencyMs = (float)Math.Max(0.0, (unityNow - frame.timestamp - unityToExternalTimeOffset) * 1000.0);
                estimatedLatencyMsSamples.Add(estimatedLatencyMs);
                lastExternalTimestamp = frame.timestamp;
            }
        }

        private void RecordCommandIfChanged()
        {
            if (inputRouter == null)
            {
                return;
            }

            var command = inputRouter.CurrentGestureCommand;
            if (!command.IsValid)
            {
                lastCommandKey = string.Empty;
                return;
            }

            var key = $"{command.Kind}:{command.StaticGesture}:{command.MotionGesture}:{command.TriggeredTime:F3}";
            if (key == lastCommandKey)
            {
                return;
            }

            lastCommandKey = key;
            if (command.Kind == GestureCommandKind.Motion)
            {
                motionCommandCount++;
                if (!motionCounts.ContainsKey(command.MotionGesture))
                {
                    motionCounts[command.MotionGesture] = 0;
                }

                motionCounts[command.MotionGesture]++;
            }
            else if (command.Kind == GestureCommandKind.StaticPose)
            {
                staticCommandCount++;
            }
        }

        private GesturePerformanceSummary BuildSummary()
        {
            var elapsed = IsRecording ? Time.unscaledTime - recordingStartedAt : frameMsSamples.Sum() / 1000f;
            var averageFrameMs = Average(frameMsSamples);
            var p95FrameMs = Percentile(frameMsSamples, 0.95f);
            var maxFrameMs = frameMsSamples.Count > 0 ? frameMsSamples.Max() : 0f;
            return new GesturePerformanceSummary
            {
                Mode = inputRouter != null ? inputRouter.Mode.ToString() : "Unbound",
                Source = externalBridge != null ? externalBridge.SourceLabel : "None",
                ElapsedSeconds = Mathf.Max(0f, elapsed),
                TotalFrames = totalFrames,
                AverageFps = averageFrameMs > 0f ? 1000f / averageFrameMs : 0f,
                MinFps = maxFrameMs > 0f ? 1000f / maxFrameMs : 0f,
                AverageFrameMs = averageFrameMs,
                P95FrameMs = p95FrameMs,
                ExternalPackets = externalPacketCount,
                AveragePacketIntervalMs = Average(packetIntervalMsSamples),
                AverageEstimatedLatencyMs = Average(estimatedLatencyMsSamples),
                P95EstimatedLatencyMs = Percentile(estimatedLatencyMsSamples, 0.95f),
                StaticCommands = staticCommandCount,
                MotionCommands = motionCommandCount,
                IsRecording = IsRecording,
                LastExportPath = LastExportPath
            };
        }

        private void ResetSamples()
        {
            frameMsSamples.Clear();
            packetIntervalMsSamples.Clear();
            estimatedLatencyMsSamples.Clear();
            motionCounts.Clear();
            totalFrames = 0;
            externalPacketCount = 0;
            staticCommandCount = 0;
            motionCommandCount = 0;
            lastExternalFrameVersion = -1;
            lastExternalTimestamp = -1f;
            unityToExternalTimeOffset = 0.0;
            hasUnityToExternalTimeOffset = false;
            lastCommandKey = string.Empty;
        }

        private string ResolveOutputDirectory()
        {
            if (Application.isEditor)
            {
                return Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputDirectoryName));
            }

            return Path.Combine(Application.persistentDataPath, outputDirectoryName);
        }

        private int GetMotionCount(MotionGestureType gesture)
        {
            return motionCounts.TryGetValue(gesture, out var count) ? count : 0;
        }

        private static float Average(IReadOnlyCollection<float> values)
        {
            return values.Count > 0 ? values.Average() : 0f;
        }

        private static float Percentile(IReadOnlyCollection<float> values, float percentile)
        {
            if (values.Count == 0)
            {
                return 0f;
            }

            var sorted = values.OrderBy(value => value).ToArray();
            var index = Mathf.Clamp(Mathf.CeilToInt(percentile * sorted.Length) - 1, 0, sorted.Length - 1);
            return sorted[index];
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Contains(",") || value.Contains("\"") || value.Contains("\n")
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
        }
    }

    public struct GesturePerformanceSummary
    {
        public string Mode;
        public string Source;
        public float ElapsedSeconds;
        public int TotalFrames;
        public float AverageFps;
        public float MinFps;
        public float AverageFrameMs;
        public float P95FrameMs;
        public int ExternalPackets;
        public float AveragePacketIntervalMs;
        public float AverageEstimatedLatencyMs;
        public float P95EstimatedLatencyMs;
        public int StaticCommands;
        public int MotionCommands;
        public bool IsRecording;
        public string LastExportPath;
    }
}
