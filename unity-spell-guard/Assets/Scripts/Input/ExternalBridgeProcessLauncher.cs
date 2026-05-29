using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SpellGuard.InputSystem
{
    public sealed class ExternalBridgeProcessLauncher : MonoBehaviour
    {
        [SerializeField] private bool launchWithReceiver = true;
        [SerializeField] private string pythonExecutable = "py";
        [SerializeField] private string pythonVersionArgument = "-3.11";
        [SerializeField] private string bridgeScriptRelativePath = "bridge/mediapipe_udp_bridge.py";
        [SerializeField] private int cameraIndex = 0;
        [SerializeField] private string backend = "dshow";
        [SerializeField] private bool noFourcc = true;
        [SerializeField] private int width = 320;
        [SerializeField] private int height = 240;
        [SerializeField] private int fps = 30;
        [SerializeField] private int port = 5053;
        [SerializeField] private bool showPreview;
        [SerializeField] private float minDetectionConfidence = 0.35f;
        [SerializeField] private float minTrackingConfidence = 0.25f;
        [SerializeField] private int modelComplexity = 0;
        [SerializeField] private bool enableYolo;
        [SerializeField] private int yoloEveryN = 12;
        [SerializeField] private float yoloPadding = 0.45f;
        [SerializeField] private float handHoldSeconds = 0.6f;
        [SerializeField] private float handRoiPadding = 1.8f;
        [SerializeField] private int handRoiRetryEveryN = 3;
        [SerializeField] private bool enableHandRoiRetry;
        [SerializeField] private bool disableThreadedCapture;
        [SerializeField] private bool disableOpticalFlow;
        [SerializeField] private float opticalFlowSeconds = 0.35f;

        private Process process;

        public bool LaunchWithReceiver => launchWithReceiver;
        public bool IsRunning => process != null && !process.HasExited;
        public string StatusText { get; private set; } = "ExternalBridge process idle";

        public void StartBridge()
        {
            if (!launchWithReceiver || IsRunning)
            {
                return;
            }

            var projectRoot = ResolveUnityProjectRoot();
            var scriptPath = Path.GetFullPath(Path.Combine(projectRoot, bridgeScriptRelativePath));
            if (!File.Exists(scriptPath))
            {
                StatusText = $"Bridge script not found: {scriptPath}";
                Debug.LogWarning($"[Gesture][ExternalBridge] {StatusText}", this);
                return;
            }

            var arguments = BuildArguments(scriptPath);
            try
            {
                process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = pythonExecutable,
                        Arguments = arguments,
                        WorkingDirectory = projectRoot,
                        UseShellExecute = false,
                        CreateNoWindow = !showPreview,
                    },
                    EnableRaisingEvents = true
                };
                process.Exited += HandleProcessExited;
                process.Start();
                StatusText = $"ExternalBridge process running: {pythonExecutable} {arguments}";
                Debug.Log($"[Gesture][ExternalBridge] started process {process.Id}", this);
            }
            catch (Exception exception)
            {
                process = null;
                StatusText = $"ExternalBridge process failed: {exception.Message}";
                Debug.LogError($"[Gesture][ExternalBridge] {StatusText}", this);
            }
        }

        public void StopBridge()
        {
            if (process == null)
            {
                StatusText = "ExternalBridge process stopped";
                return;
            }

            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(1000);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Gesture][ExternalBridge] stop process warning: {exception.Message}", this);
            }
            finally
            {
                process.Dispose();
                process = null;
                StatusText = "ExternalBridge process stopped";
            }
        }

        private void OnDisable()
        {
            StopBridge();
        }

        private void OnApplicationQuit()
        {
            StopBridge();
        }

        private void HandleProcessExited(object sender, EventArgs args)
        {
            StatusText = "ExternalBridge process exited";
        }

        private string BuildArguments(string scriptPath)
        {
            var args = string.Empty;
            if (!string.IsNullOrWhiteSpace(pythonVersionArgument))
            {
                args += $"{pythonVersionArgument} ";
            }

            args += $"\"{scriptPath}\" --camera-index {cameraIndex} --backend {backend} --width {width} --height {height} --fps {fps} --port {port}";
            args += $" --min-detection-confidence {minDetectionConfidence:0.###} --min-tracking-confidence {minTrackingConfidence:0.###}";
            args += $" --model-complexity {Mathf.Clamp(modelComplexity, 0, 1)}";
            args += $" --hand-hold-seconds {Mathf.Max(0f, handHoldSeconds):0.###} --hand-roi-padding {Mathf.Max(0f, handRoiPadding):0.###}";
            args += $" --hand-roi-retry-every-n {Mathf.Max(1, handRoiRetryEveryN)}";
            args += $" --optical-flow-seconds {Mathf.Max(0f, opticalFlowSeconds):0.###}";
            if (enableYolo)
            {
                args += $" --enable-yolo --yolo-every-n {Mathf.Max(1, yoloEveryN)} --yolo-padding {Mathf.Max(0f, yoloPadding):0.###}";
            }

            if (enableHandRoiRetry)
            {
                args += " --enable-hand-roi-retry";
            }

            if (disableThreadedCapture)
            {
                args += " --disable-threaded-capture";
            }

            if (disableOpticalFlow)
            {
                args += " --disable-optical-flow";
            }

            if (noFourcc)
            {
                args += " --no-fourcc";
            }

            if (showPreview)
            {
                args += " --show-preview";
            }

            return args;
        }

        private static string ResolveUnityProjectRoot()
        {
            if (!Application.isEditor)
            {
                return Application.dataPath;
            }

            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }
    }
}
