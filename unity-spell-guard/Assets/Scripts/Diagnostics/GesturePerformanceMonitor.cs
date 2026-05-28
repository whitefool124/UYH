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
        [SerializeField] private WebcamFeedController webcamFeed;
        [SerializeField] private NativeMediapipeGestureRunner nativeRunner;
        [SerializeField] private bool recordOnStart;
        [SerializeField] private KeyCode toggleRecordingKey = KeyCode.F8;
        [SerializeField] private KeyCode exportKey = KeyCode.F9;
        [SerializeField] private string outputDirectoryName = "ExperimentResults";
        [SerializeField] private bool createReadmeOnExport = true;
        [SerializeField] private float sampleIntervalSeconds = 1f;

        private readonly List<float> frameMsSamples = new List<float>();
        private readonly List<float> packetIntervalMsSamples = new List<float>();
        private readonly List<float> estimatedLatencyMsSamples = new List<float>();
        private readonly List<float> handUpdateIntervalMsSamples = new List<float>();
        private readonly List<GesturePerformanceSample> timelineSamples = new List<GesturePerformanceSample>();
        private readonly Dictionary<MotionGestureType, int> motionCounts = new Dictionary<MotionGestureType, int>();

        private string sessionId;
        private float recordingStartedAt;
        private float nextSampleAt;
        private float lastHandUpdateAt = -1f;
        private Vector2 lastHandPosition = new Vector2(-1f, -1f);
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
        public IReadOnlyList<GesturePerformanceSample> TimelineSamples => timelineSamples;

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
            RecordHandUpdateIfChanged();
            RecordCommandIfChanged();
            RecordTimelineSampleIfDue();
        }

        public void Configure(GestureInputRouter router, ExternalGestureBridgeProvider bridge)
        {
            Configure(router, bridge, null, null);
        }

        public void Configure(GestureInputRouter router, ExternalGestureBridgeProvider bridge, WebcamFeedController feed, NativeMediapipeGestureRunner runner)
        {
            inputRouter = router;
            externalBridge = bridge;
            webcamFeed = feed;
            nativeRunner = runner;
        }

        public void StartRecording()
        {
            ResetSamples();
            IsRecording = true;
            sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            recordingStartedAt = Time.unscaledTime;
            nextSampleAt = recordingStartedAt + Mathf.Max(0.1f, sampleIntervalSeconds);
        }

        public void StopRecording()
        {
            IsRecording = false;
            RecordTimelineSample();
        }

        public string ExportCsv()
        {
            var directory = ResolveOutputDirectory();
            Directory.CreateDirectory(directory);
            if (createReadmeOnExport)
            {
                EnsureResultsReadme(directory);
            }

            var timestamp = string.IsNullOrWhiteSpace(sessionId) ? DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) : sessionId;
            var mode = ResolveModeFileToken();
            var fileName = $"gesture_performance_{mode}_{timestamp}.csv";
            LastExportPath = Path.Combine(directory, fileName);
            File.WriteAllText(LastExportPath, BuildCsv(), Encoding.UTF8);
            return LastExportPath;
        }

        public string BuildCsv()
        {
            var summary = BuildSummary();
            var builder = new StringBuilder();
            builder.AppendLine("section,session_id,mode,source,elapsed_seconds,total_frames,average_fps,min_fps,average_frame_ms,p95_frame_ms,camera_device,camera_fps,camera_width,camera_height,camera_requested_width,camera_requested_height,camera_requested_fps,camera_uses_requested_format,native_fresh_frame_only,native_input_fps,native_result_fps,native_processing_latency_ms,external_packets,avg_packet_interval_ms,avg_estimated_latency_ms,p95_estimated_latency_ms,avg_hand_update_interval_ms,p95_hand_update_interval_ms,static_commands,motion_commands,swipe_lr,swipe_rl,swipe_bt,swipe_tb,snap,point_to_fist,body_shift_left,body_shift_right");
            builder.Append("summary,")
                .Append(sessionId).Append(',')
                .Append(summary.Mode).Append(',')
                .Append(EscapeCsv(summary.Source)).Append(',')
                .Append(Format(summary.ElapsedSeconds)).Append(',')
                .Append(summary.TotalFrames).Append(',')
                .Append(Format(summary.AverageFps)).Append(',')
                .Append(Format(summary.MinFps)).Append(',')
                .Append(Format(summary.AverageFrameMs)).Append(',')
                .Append(Format(summary.P95FrameMs)).Append(',')
                .Append(EscapeCsv(summary.CameraDevice)).Append(',')
                .Append(Format(summary.CameraFps)).Append(',')
                .Append(summary.CameraWidth).Append(',')
                .Append(summary.CameraHeight).Append(',')
                .Append(summary.CameraRequestedWidth).Append(',')
                .Append(summary.CameraRequestedHeight).Append(',')
                .Append(summary.CameraRequestedFps).Append(',')
                .Append(summary.CameraUsesRequestedFormat ? "1" : "0").Append(',')
                .Append(summary.NativeFreshFrameOnly ? "1" : "0").Append(',')
                .Append(Format(summary.NativeInputFps)).Append(',')
                .Append(Format(summary.NativeResultFps)).Append(',')
                .Append(Format(summary.NativeProcessingLatencyMs)).Append(',')
                .Append(summary.ExternalPackets).Append(',')
                .Append(Format(summary.AveragePacketIntervalMs)).Append(',')
                .Append(Format(summary.AverageEstimatedLatencyMs)).Append(',')
                .Append(Format(summary.P95EstimatedLatencyMs)).Append(',')
                .Append(Format(summary.AverageHandUpdateIntervalMs)).Append(',')
                .Append(Format(summary.P95HandUpdateIntervalMs)).Append(',')
                .Append(summary.StaticCommands).Append(',')
                .Append(summary.MotionCommands).Append(',')
                .Append(GetMotionCount(MotionGestureType.SwipeLeftToRight)).Append(',')
                .Append(GetMotionCount(MotionGestureType.SwipeRightToLeft)).Append(',')
                .Append(GetMotionCount(MotionGestureType.SwipeBottomToTop)).Append(',')
                .Append(GetMotionCount(MotionGestureType.SwipeTopToBottom)).Append(',')
                .Append(GetMotionCount(MotionGestureType.Snap)).Append(',')
                .Append(GetMotionCount(MotionGestureType.PointToFist)).Append(',')
                .Append(GetMotionCount(MotionGestureType.BodyShiftLeft)).Append(',')
                .Append(GetMotionCount(MotionGestureType.BodyShiftRight)).AppendLine();

            builder.AppendLine();
            builder.AppendLine("section,elapsed_seconds,average_fps,p95_frame_ms,camera_fps,native_input_fps,native_result_fps,native_processing_latency_ms,external_packets,avg_packet_interval_ms,avg_estimated_latency_ms,avg_hand_update_interval_ms,static_commands,motion_commands,hand_present,hand_x,hand_y");
            for (var index = 0; index < timelineSamples.Count; index++)
            {
                var sample = timelineSamples[index];
                builder.Append("timeseries,")
                    .Append(Format(sample.ElapsedSeconds)).Append(',')
                    .Append(Format(sample.AverageFps)).Append(',')
                    .Append(Format(sample.P95FrameMs)).Append(',')
                    .Append(Format(sample.CameraFps)).Append(',')
                    .Append(Format(sample.NativeInputFps)).Append(',')
                    .Append(Format(sample.NativeResultFps)).Append(',')
                    .Append(Format(sample.NativeProcessingLatencyMs)).Append(',')
                    .Append(sample.ExternalPackets).Append(',')
                    .Append(Format(sample.AveragePacketIntervalMs)).Append(',')
                    .Append(Format(sample.AverageEstimatedLatencyMs)).Append(',')
                    .Append(Format(sample.AverageHandUpdateIntervalMs)).Append(',')
                    .Append(sample.StaticCommands).Append(',')
                    .Append(sample.MotionCommands).Append(',')
                    .Append(sample.HandPresent ? "1" : "0").Append(',')
                    .Append(Format(sample.HandX)).Append(',')
                    .Append(Format(sample.HandY)).AppendLine();
            }

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

        private void RecordHandUpdateIfChanged()
        {
            if (inputRouter == null)
            {
                return;
            }

            var frame = inputRouter.CurrentGestureFrame;
            if (!frame.HasPrimaryHand)
            {
                return;
            }

            var palm = frame.PrimaryHand.PalmCenter;
            if (Vector2.Distance(palm, lastHandPosition) < 0.001f)
            {
                return;
            }

            if (lastHandUpdateAt > 0f)
            {
                handUpdateIntervalMsSamples.Add(Mathf.Max(0f, Time.unscaledTime - lastHandUpdateAt) * 1000f);
            }

            lastHandUpdateAt = Time.unscaledTime;
            lastHandPosition = palm;
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

        private void RecordTimelineSampleIfDue()
        {
            if (Time.unscaledTime < nextSampleAt)
            {
                return;
            }

            RecordTimelineSample();
            nextSampleAt = Time.unscaledTime + Mathf.Max(0.1f, sampleIntervalSeconds);
        }

        private void RecordTimelineSample()
        {
            var summary = BuildSummary();
            var frame = inputRouter != null ? inputRouter.CurrentGestureFrame : GestureFrame.Empty(GestureSourceKind.Unknown);
            var hand = frame.PrimaryHand;
            timelineSamples.Add(new GesturePerformanceSample
            {
                ElapsedSeconds = summary.ElapsedSeconds,
                AverageFps = summary.AverageFps,
                P95FrameMs = summary.P95FrameMs,
                CameraFps = summary.CameraFps,
                NativeInputFps = summary.NativeInputFps,
                NativeResultFps = summary.NativeResultFps,
                NativeProcessingLatencyMs = summary.NativeProcessingLatencyMs,
                ExternalPackets = summary.ExternalPackets,
                AveragePacketIntervalMs = summary.AveragePacketIntervalMs,
                AverageEstimatedLatencyMs = summary.AverageEstimatedLatencyMs,
                AverageHandUpdateIntervalMs = summary.AverageHandUpdateIntervalMs,
                StaticCommands = summary.StaticCommands,
                MotionCommands = summary.MotionCommands,
                HandPresent = hand.IsTracked,
                HandX = hand.IsTracked ? hand.PalmCenter.x : 0f,
                HandY = hand.IsTracked ? hand.PalmCenter.y : 0f
            });
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
                CameraDevice = webcamFeed != null ? webcamFeed.ActiveDeviceName : "None",
                CameraFps = webcamFeed != null ? webcamFeed.EstimatedCameraFps : 0f,
                CameraWidth = webcamFeed != null ? webcamFeed.ActualWidth : 0,
                CameraHeight = webcamFeed != null ? webcamFeed.ActualHeight : 0,
                CameraRequestedWidth = webcamFeed != null ? webcamFeed.RequestedWidth : 0,
                CameraRequestedHeight = webcamFeed != null ? webcamFeed.RequestedHeight : 0,
                CameraRequestedFps = webcamFeed != null ? webcamFeed.RequestedFps : 0,
                CameraUsesRequestedFormat = webcamFeed != null && webcamFeed.UseRequestedFormat,
                NativeInputFps = nativeRunner != null ? nativeRunner.EstimatedInputFps : 0f,
                NativeResultFps = nativeRunner != null ? nativeRunner.EstimatedResultFps : 0f,
                NativeProcessingLatencyMs = nativeRunner != null ? nativeRunner.AverageProcessingLatencyMs : 0f,
                NativeFreshFrameOnly = nativeRunner != null && nativeRunner.ProcessOnlyFreshCameraFrames,
                ExternalPackets = externalPacketCount,
                AveragePacketIntervalMs = Average(packetIntervalMsSamples),
                AverageEstimatedLatencyMs = Average(estimatedLatencyMsSamples),
                P95EstimatedLatencyMs = Percentile(estimatedLatencyMsSamples, 0.95f),
                AverageHandUpdateIntervalMs = Average(handUpdateIntervalMsSamples),
                P95HandUpdateIntervalMs = Percentile(handUpdateIntervalMsSamples, 0.95f),
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
            handUpdateIntervalMsSamples.Clear();
            timelineSamples.Clear();
            motionCounts.Clear();
            totalFrames = 0;
            externalPacketCount = 0;
            staticCommandCount = 0;
            motionCommandCount = 0;
            lastExternalFrameVersion = -1;
            lastExternalTimestamp = -1f;
            lastHandUpdateAt = -1f;
            lastHandPosition = new Vector2(-1f, -1f);
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

        private string ResolveModeFileToken()
        {
            if (inputRouter == null)
            {
                return "unbound";
            }

            return inputRouter.Mode switch
            {
                GestureInputRouter.InputMode.Mock => "mock",
                GestureInputRouter.InputMode.NativeMediapipe => "native",
                GestureInputRouter.InputMode.ExternalBridge => "external",
                _ => inputRouter.Mode.ToString().ToLowerInvariant()
            };
        }

        private static void EnsureResultsReadme(string directory)
        {
            var readmePath = Path.Combine(directory, "README.md");
            if (File.Exists(readmePath))
            {
                return;
            }

            File.WriteAllText(readmePath, BuildResultsReadme(), Encoding.UTF8);
        }

        private static string BuildResultsReadme()
        {
            return "# Spell Guard Experiment Results\n\n" +
                   "本目录用于归档论文实验 CSV 证据链。Unity 运行时由 `GesturePerformanceMonitor` 导出性能与手势统计，外部视觉链路由 Python benchmark 导出 YOLO / MediaPipe 对比数据。\n\n" +
                   "## 命名约定\n\n" +
                   "- `gesture_performance_mock_<timestamp>.csv`：Mock 输入模式性能记录。\n" +
                   "- `gesture_performance_native_<timestamp>.csv`：Native MediaPipe 输入模式性能记录。\n" +
                   "- `gesture_performance_external_<timestamp>.csv`：ExternalBridge / UDP 输入模式性能记录。\n" +
                   "- `yolo_mediapipe_benchmark_<timestamp>.csv`：Python 侧 YOLO + MediaPipe benchmark 记录。\n\n" +
                   "## Unity CSV 字段\n\n" +
                   "`session_id, mode, source, elapsed_seconds, total_frames, average_fps, min_fps, average_frame_ms, p95_frame_ms, external_packets, avg_packet_interval_ms, avg_estimated_latency_ms, p95_estimated_latency_ms, static_commands, motion_commands, swipe_lr, swipe_rl, snap, point_to_fist, body_shift_left, body_shift_right`。\n\n" +
                   "## 实验记录模板\n\n" +
                   "| 项目 | 记录 |\n" +
                   "|---|---|\n" +
                   $"| 实验日期 | {DateTime.Now:yyyy-MM-dd} |\n" +
                   $"| Unity 版本 | {Application.unityVersion} |\n" +
                   "| 输入模式 | Mock / Native MediaPipe / ExternalBridge |\n" +
                   "| 测试设备 | 待填写 CPU / GPU / 内存 |\n" +
                   "| 摄像头 | 待填写设备型号或 ExternalBridge 视频源 |\n" +
                   "| 运行时长 | 建议每组不少于 60 秒 |\n" +
                   "| 使用的视频样本 | ExternalBridge / benchmark 运行时填写 |\n";
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
        public string CameraDevice;
        public float CameraFps;
        public int CameraWidth;
        public int CameraHeight;
        public int CameraRequestedWidth;
        public int CameraRequestedHeight;
        public int CameraRequestedFps;
        public bool CameraUsesRequestedFormat;
        public float NativeInputFps;
        public float NativeResultFps;
        public float NativeProcessingLatencyMs;
        public bool NativeFreshFrameOnly;
        public int ExternalPackets;
        public float AveragePacketIntervalMs;
        public float AverageEstimatedLatencyMs;
        public float P95EstimatedLatencyMs;
        public float AverageHandUpdateIntervalMs;
        public float P95HandUpdateIntervalMs;
        public int StaticCommands;
        public int MotionCommands;
        public bool IsRecording;
        public string LastExportPath;
    }

    public struct GesturePerformanceSample
    {
        public float ElapsedSeconds;
        public float AverageFps;
        public float P95FrameMs;
        public float CameraFps;
        public float NativeInputFps;
        public float NativeResultFps;
        public float NativeProcessingLatencyMs;
        public int ExternalPackets;
        public float AveragePacketIntervalMs;
        public float AverageEstimatedLatencyMs;
        public float AverageHandUpdateIntervalMs;
        public int StaticCommands;
        public int MotionCommands;
        public bool HandPresent;
        public float HandX;
        public float HandY;
    }
}
