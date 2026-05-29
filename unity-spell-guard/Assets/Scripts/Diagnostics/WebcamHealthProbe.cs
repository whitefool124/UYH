using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SpellGuard.InputSystem;
using UnityEngine;

namespace SpellGuard.Diagnostics
{
    public sealed class WebcamHealthProbe : MonoBehaviour
    {
        [SerializeField] private WebcamFeedController webcamFeed;
        [SerializeField] private NativeMediapipeGestureRunner nativeRunner;
        [SerializeField] private string outputDirectoryName = "ExperimentResults";
        [SerializeField] private float warmupSeconds = 1.2f;
        [SerializeField] private float sampleSeconds = 3.5f;
        [SerializeField] private bool applyBestFormatOnComplete = true;
        [SerializeField] private int preferredMaxPixels = 320 * 240;
        [SerializeField] private float lowResolutionScoreBonus = 3.0f;

        private readonly List<WebcamFormatProbeResult> results = new List<WebcamFormatProbeResult>();
        private Coroutine probeCoroutine;
        private string sessionId;

        public bool IsRunning => probeCoroutine != null;
        public string StatusText { get; private set; } = "Camera probe idle";
        public string LastExportPath { get; private set; } = string.Empty;
        public WebcamFormatProbeResult BestResult { get; private set; }
        public IReadOnlyList<WebcamFormatProbeResult> Results => results;

        public void Configure(WebcamFeedController feed, NativeMediapipeGestureRunner runner)
        {
            webcamFeed = feed;
            nativeRunner = runner;
        }

        public void StartProbe()
        {
            if (probeCoroutine != null)
            {
                return;
            }

            if (webcamFeed == null)
            {
                webcamFeed = FindObjectOfType<WebcamFeedController>();
            }

            if (nativeRunner == null)
            {
                nativeRunner = FindObjectOfType<NativeMediapipeGestureRunner>();
            }

            probeCoroutine = StartCoroutine(RunProbe());
        }

        public void StopProbe()
        {
            if (probeCoroutine == null)
            {
                return;
            }

            StopCoroutine(probeCoroutine);
            probeCoroutine = null;
            StatusText = "Camera probe stopped";
        }

        public string ExportCsv()
        {
            var directory = ResolveOutputDirectory();
            Directory.CreateDirectory(directory);
            var timestamp = string.IsNullOrWhiteSpace(sessionId)
                ? DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)
                : sessionId;
            LastExportPath = Path.Combine(directory, $"webcam_probe_{timestamp}.csv");
            File.WriteAllText(LastExportPath, BuildCsv(), Encoding.UTF8);
            return LastExportPath;
        }

        public string BuildCsv()
        {
            var builder = new StringBuilder();
            builder.AppendLine("section,session_id,device,use_requested,requested_width,requested_height,requested_fps,actual_width,actual_height,frames,elapsed_seconds,average_fps,p95_interval_ms,max_interval_ms,stall_count,pixels,low_resolution_candidate,score,is_best");
            for (var index = 0; index < results.Count; index++)
            {
                var result = results[index];
                builder.Append("format,")
                    .Append(sessionId).Append(',')
                    .Append(EscapeCsv(result.DeviceName)).Append(',')
                    .Append(result.UseRequestedFormat ? "1" : "0").Append(',')
                    .Append(result.RequestedWidth).Append(',')
                    .Append(result.RequestedHeight).Append(',')
                    .Append(result.RequestedFps).Append(',')
                    .Append(result.ActualWidth).Append(',')
                    .Append(result.ActualHeight).Append(',')
                    .Append(result.Frames).Append(',')
                    .Append(Format(result.ElapsedSeconds)).Append(',')
                    .Append(Format(result.AverageFps)).Append(',')
                    .Append(Format(result.P95IntervalMs)).Append(',')
                    .Append(Format(result.MaxIntervalMs)).Append(',')
                    .Append(result.StallCount).Append(',')
                    .Append(result.Pixels).Append(',')
                    .Append(result.LowResolutionCandidate ? "1" : "0").Append(',')
                    .Append(Format(result.Score)).Append(',')
                    .Append(result.Equals(BestResult) ? "1" : "0")
                    .AppendLine();
            }

            return builder.ToString();
        }

        private IEnumerator RunProbe()
        {
            results.Clear();
            BestResult = default;
            sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            StatusText = "Camera probe starting";

            var runnerWasEnabled = nativeRunner != null && nativeRunner.enabled;
            if (nativeRunner != null)
            {
                nativeRunner.StopRunner();
                nativeRunner.enabled = false;
            }

            var candidates = BuildCandidates();
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                StatusText = $"Testing camera format {index + 1}/{candidates.Count}: {candidate.Label}";
                webcamFeed.ApplyRequestedFormat(candidate.UseRequestedFormat, candidate.Width, candidate.Height, candidate.Fps);

                var warmupStartedAt = Time.unscaledTime;
                while (Time.unscaledTime - warmupStartedAt < warmupSeconds || !webcamFeed.HasReadyFrame)
                {
                    if (Time.unscaledTime - warmupStartedAt > warmupSeconds + 2.5f)
                    {
                        break;
                    }

                    yield return null;
                }

                yield return SampleCandidate(candidate);
            }

            BestResult = PickBestResult();
            if (applyBestFormatOnComplete && BestResult.IsValid)
            {
                StatusText = $"Applying best camera format: {BestResult.FormatLabel}";
                webcamFeed.ApplyRequestedFormat(BestResult.UseRequestedFormat, BestResult.RequestedWidth, BestResult.RequestedHeight, BestResult.RequestedFps);
            }

            ExportCsv();

            if (nativeRunner != null)
            {
                nativeRunner.enabled = runnerWasEnabled;
                if (runnerWasEnabled)
                {
                    nativeRunner.StartRunner();
                }
            }

            StatusText = BestResult.IsValid
                ? $"Camera probe done: best {BestResult.FormatLabel}, {BestResult.AverageFps:0.0} FPS, P95 {BestResult.P95IntervalMs:0} ms"
                : "Camera probe done: no valid camera frames";
            probeCoroutine = null;
        }

        private IEnumerator SampleCandidate(WebcamFormatCandidate candidate)
        {
            var intervals = new List<float>();
            var sampleStartedAt = Time.unscaledTime;
            var lastFrameAt = -1f;
            var lastFrameCount = webcamFeed.CameraFrameCount;
            var frames = 0;
            var stallCount = 0;

            while (Time.unscaledTime - sampleStartedAt < sampleSeconds)
            {
                if (webcamFeed.CameraFrameCount != lastFrameCount)
                {
                    var now = Time.unscaledTime;
                    if (lastFrameAt > 0f)
                    {
                        var intervalMs = Mathf.Max(0f, now - lastFrameAt) * 1000f;
                        intervals.Add(intervalMs);
                        if (intervalMs > 120f)
                        {
                            stallCount++;
                        }
                    }

                    frames += Mathf.Max(1, webcamFeed.CameraFrameCount - lastFrameCount);
                    lastFrameCount = webcamFeed.CameraFrameCount;
                    lastFrameAt = now;
                }

                yield return null;
            }

            var elapsed = Mathf.Max(0.001f, Time.unscaledTime - sampleStartedAt);
            var p95 = Percentile(intervals, 0.95f);
            var max = intervals.Count > 0 ? intervals.Max() : 0f;
            var averageFps = frames / elapsed;
            var pixels = webcamFeed.ActualWidth * webcamFeed.ActualHeight;
            var lowResolutionCandidate = pixels > 0 && pixels <= preferredMaxPixels;
            var lowResolutionBonus = lowResolutionCandidate ? lowResolutionScoreBonus : 0f;
            var resolutionPenalty = pixels > preferredMaxPixels
                ? Mathf.Log(Mathf.Max(1f, pixels / (float)Mathf.Max(1, preferredMaxPixels)), 2f) * 1.25f
                : 0f;
            var result = new WebcamFormatProbeResult
            {
                DeviceName = webcamFeed.ActiveDeviceName,
                UseRequestedFormat = candidate.UseRequestedFormat,
                RequestedWidth = candidate.Width,
                RequestedHeight = candidate.Height,
                RequestedFps = candidate.Fps,
                ActualWidth = webcamFeed.ActualWidth,
                ActualHeight = webcamFeed.ActualHeight,
                Frames = frames,
                ElapsedSeconds = elapsed,
                AverageFps = averageFps,
                P95IntervalMs = p95,
                MaxIntervalMs = max,
                StallCount = stallCount,
                Pixels = pixels,
                LowResolutionCandidate = lowResolutionCandidate,
                Score = averageFps - p95 * 0.035f - stallCount * 1.5f - resolutionPenalty + lowResolutionBonus
            };
            results.Add(result);
        }

        private WebcamFormatProbeResult PickBestResult()
        {
            return results
                .Where(result => result.IsValid)
                .OrderByDescending(result => result.Score)
                .ThenByDescending(result => result.AverageFps)
                .ThenBy(result => result.Pixels)
                .FirstOrDefault();
        }

        private static List<WebcamFormatCandidate> BuildCandidates()
        {
            return new List<WebcamFormatCandidate>
            {
                new WebcamFormatCandidate(false, 0, 0, 30, "Device default"),
                new WebcamFormatCandidate(true, 320, 240, 30, "320x240@30"),
                new WebcamFormatCandidate(true, 424, 240, 30, "424x240@30"),
                new WebcamFormatCandidate(true, 640, 360, 30, "640x360@30"),
                new WebcamFormatCandidate(true, 640, 480, 30, "640x480@30"),
                new WebcamFormatCandidate(true, 1280, 720, 30, "1280x720@30")
            };
        }

        private string ResolveOutputDirectory()
        {
            if (Application.isEditor)
            {
                return Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputDirectoryName));
            }

            return Path.Combine(Application.persistentDataPath, outputDirectoryName);
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

        private readonly struct WebcamFormatCandidate
        {
            public WebcamFormatCandidate(bool useRequestedFormat, int width, int height, int fps, string label)
            {
                UseRequestedFormat = useRequestedFormat;
                Width = width;
                Height = height;
                Fps = fps;
                Label = label;
            }

            public bool UseRequestedFormat { get; }
            public int Width { get; }
            public int Height { get; }
            public int Fps { get; }
            public string Label { get; }
        }
    }

    public struct WebcamFormatProbeResult
    {
        public string DeviceName;
        public bool UseRequestedFormat;
        public int RequestedWidth;
        public int RequestedHeight;
        public int RequestedFps;
        public int ActualWidth;
        public int ActualHeight;
        public int Frames;
        public float ElapsedSeconds;
        public float AverageFps;
        public float P95IntervalMs;
        public float MaxIntervalMs;
        public int StallCount;
        public int Pixels;
        public bool LowResolutionCandidate;
        public float Score;

        public bool IsValid => Frames > 0 && ActualWidth > 16 && ActualHeight > 16;
        public string FormatLabel => UseRequestedFormat
            ? $"{RequestedWidth}x{RequestedHeight}@{RequestedFps}"
            : "Device default";
    }
}
