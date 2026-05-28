using SpellGuard.Core;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public class WebcamFeedController : MonoBehaviour
    {
        [SerializeField] private bool playOnAwake;
        [SerializeField] private bool useRequestedFormat = true;
        [SerializeField] private int requestedWidth = 320;
        [SerializeField] private int requestedHeight = 240;
        [SerializeField] private int requestedFps = 30;
        [SerializeField] private bool mirrorPreview = true;
        [SerializeField] private int preferredDeviceIndex;
        [SerializeField] private bool includeVirtualCameras;
        [SerializeField] private bool stopSharedCameraOnDisable = false;

        private WebCamTexture webcamTexture;
        private WebCamDevice activeDevice;
        private bool hasActiveDevice;
        private string preferredDeviceName;
        private static WebCamTexture sharedTexture;
        private static WebCamDevice sharedDevice;
        private static bool hasSharedDevice;
        private static bool sharedUseRequestedFormat;
        private static int sharedRequestedWidth;
        private static int sharedRequestedHeight;
        private static int sharedRequestedFps;
        private float lastUpdatedFrameAt = -1f;
        private float cameraFrameIntervalTotalMs;
        private int cameraFrameIntervalSamples;
        private int cameraFrameCount;
        private FormatMode currentFormatMode = FormatMode.Request320;
        private float formatAppliedAt = -999f;
        private bool fallbackPending;

        public WebCamTexture Texture => webcamTexture;
        public bool IsRunning => webcamTexture != null && webcamTexture.isPlaying;
        public bool HasTexture => webcamTexture != null;
        public bool HasReadyFrame => webcamTexture != null && webcamTexture.width > 16 && webcamTexture.height > 16;
        public bool MirrorPreview => mirrorPreview;
        public bool IsFrontFacing => hasActiveDevice && activeDevice.isFrontFacing;
        public bool IsVerticallyFlipped => webcamTexture != null && webcamTexture.videoVerticallyMirrored;
        public int RotationAngle => webcamTexture != null ? webcamTexture.videoRotationAngle : 0;
        public int ActualWidth => webcamTexture != null ? webcamTexture.width : 0;
        public int ActualHeight => webcamTexture != null ? webcamTexture.height : 0;
        public int CameraFrameCount => cameraFrameCount;
        public int RequestedWidth => requestedWidth;
        public int RequestedHeight => requestedHeight;
        public int RequestedFps => requestedFps;
        public bool UseRequestedFormat => useRequestedFormat;
        public string RequestedFormatLabel => GetFormatLabel();
        public float AverageCameraFrameIntervalMs => cameraFrameIntervalSamples > 0 ? cameraFrameIntervalTotalMs / cameraFrameIntervalSamples : 0f;
        public float EstimatedCameraFps => AverageCameraFrameIntervalMs > 0f ? 1000f / AverageCameraFrameIntervalMs : 0f;
        public string StatusText { get; private set; } = "摄像头未启动";
        public string ActiveDeviceName { get; private set; } = "无";

        public event System.Action CameraRestarting;
        public event System.Action CameraRestarted;

        private enum FormatMode
        {
            Request320,
            Request640,
            Request1280,
            DeviceDefault
        }

        private void Awake()
        {
            LoadPersistedPreferences();
            AdoptSharedTextureIfAvailable();
        }

        private void LoadPersistedPreferences()
        {
            preferredDeviceName = SpellGuardLocalProgress.LoadWebcamDeviceName();
        }

        private void Start()
        {
            if (playOnAwake)
            {
                StartCamera();
            }
        }

        private void Update()
        {
            if (fallbackPending && Time.unscaledTime - formatAppliedAt > 3f && !HasReadyFrame)
            {
                fallbackPending = false;
                currentFormatMode = FormatMode.Request320;
                ApplyFormatMode(currentFormatMode);
                return;
            }

            if (webcamTexture == null || !webcamTexture.didUpdateThisFrame)
            {
                return;
            }

            fallbackPending = false;
            cameraFrameCount++;
            if (lastUpdatedFrameAt > 0f)
            {
                cameraFrameIntervalTotalMs += Mathf.Max(0f, Time.unscaledTime - lastUpdatedFrameAt) * 1000f;
                cameraFrameIntervalSamples++;
            }

            lastUpdatedFrameAt = Time.unscaledTime;
        }

        private void OnDisable()
        {
            if (stopSharedCameraOnDisable)
            {
                ForceStopSharedCamera();
            }
            else
            {
                ReleaseForSceneReuse();
            }
        }

        public void StartCamera()
        {
            LoadPersistedPreferences();

            if (AdoptSharedTextureIfAvailable())
            {
                return;
            }

            StopCamera();

            if (WebCamTexture.devices == null || WebCamTexture.devices.Length == 0)
            {
                StatusText = "未找到可用摄像头";
                ActiveDeviceName = "无";
                return;
            }

            StartCameraAt(GetPreferredDeviceIndex());
        }

        public bool TryStartNextCamera()
        {
            return TryStartNextCamera(includeVirtualCameras);
        }

        public bool TryStartNextPhysicalCamera()
        {
            return TryStartNextCamera(false);
        }

        public void RestartCamera()
        {
            CameraRestarting?.Invoke();
            ForceStopSharedCamera();
            StartCamera();
            CameraRestarted?.Invoke();
        }

        public void ApplyRequestedFormat(bool useFormat, int width, int height, int fps)
        {
            useRequestedFormat = useFormat;
            requestedWidth = Mathf.Max(1, width);
            requestedHeight = Mathf.Max(1, height);
            requestedFps = Mathf.Max(1, fps);
            formatAppliedAt = Time.unscaledTime;
            fallbackPending = useRequestedFormat && (requestedWidth != 320 || requestedHeight != 240);
            RestartCamera();
        }

        public void CyclePerformanceFormat()
        {
            currentFormatMode = GetNextFormatMode();
            ApplyFormatMode(currentFormatMode);
        }

        private FormatMode GetNextFormatMode()
        {
            if (!useRequestedFormat)
            {
                return FormatMode.Request320;
            }

            if (requestedWidth <= 320 && requestedHeight <= 240)
            {
                return FormatMode.Request640;
            }

            if (requestedWidth <= 640 && requestedHeight <= 480)
            {
                return FormatMode.Request1280;
            }

            return FormatMode.DeviceDefault;
        }

        private void ApplyFormatMode(FormatMode mode)
        {
            switch (mode)
            {
                case FormatMode.Request640:
                    ApplyRequestedFormat(true, 640, 480, 30);
                    break;
                case FormatMode.Request1280:
                    ApplyRequestedFormat(true, 1280, 720, 30);
                    break;
                case FormatMode.DeviceDefault:
                    ApplyRequestedFormat(false, requestedWidth, requestedHeight, requestedFps);
                    break;
                default:
                    ApplyRequestedFormat(true, 320, 240, 30);
                    break;
            }
        }

        private bool TryStartNextCamera(bool allowVirtualDevices)
        {
            if (WebCamTexture.devices == null || WebCamTexture.devices.Length == 0)
            {
                StatusText = "未找到可用摄像头";
                ActiveDeviceName = "无";
                return false;
            }

            var nextIndex = GetNextAllowedDeviceIndex(preferredDeviceIndex, allowVirtualDevices);
            if (nextIndex < 0)
            {
                StatusText = "未找到可用物理摄像头";
                return false;
            }

            StartCameraAt(nextIndex);
            return webcamTexture != null;
        }

        public string GetDeviceListLabel()
        {
            if (WebCamTexture.devices == null || WebCamTexture.devices.Length == 0)
            {
                return "无摄像头设备";
            }

            var labels = new System.Text.StringBuilder();
            for (var index = 0; index < WebCamTexture.devices.Length; index++)
            {
                if (index > 0)
                {
                    labels.Append(" | ");
                }

                var device = WebCamTexture.devices[index];
                labels.Append(index).Append(':').Append(device.name);
                if (!IsAllowedDevice(device.name, includeVirtualCameras))
                {
                    labels.Append("(跳过虚拟)");
                }
            }

            return labels.ToString();
        }

        private void StartCameraAt(int deviceIndex)
        {
            StopCamera();
            preferredDeviceIndex = Mathf.Clamp(deviceIndex, 0, WebCamTexture.devices.Length - 1);
            var device = WebCamTexture.devices[preferredDeviceIndex];
            try
            {
                activeDevice = device;
                hasActiveDevice = true;
                webcamTexture = CreateWebcamTexture(device.name);
                ResetCameraMetrics();
                webcamTexture.Play();
                ActiveDeviceName = device.name;
                preferredDeviceName = device.name;
                sharedTexture = webcamTexture;
                sharedDevice = device;
                hasSharedDevice = true;
                sharedUseRequestedFormat = useRequestedFormat;
                sharedRequestedWidth = requestedWidth;
                sharedRequestedHeight = requestedHeight;
                sharedRequestedFps = requestedFps;
                SpellGuardLocalProgress.SaveWebcamDeviceName(device.name);
                StatusText = $"摄像头运行中：{device.name}（{GetFormatLabel()}）";
            }
            catch (System.Exception ex)
            {
                webcamTexture = null;
                hasActiveDevice = false;
                ActiveDeviceName = "无";
                StatusText = $"摄像头启动失败：{ex.GetType().Name}";
                Debug.LogWarning($"[Gesture][Webcam] start failed: {ex.Message}", this);
            }
        }

        private WebCamTexture CreateWebcamTexture(string deviceName)
        {
            if (!useRequestedFormat)
            {
                return new WebCamTexture(deviceName);
            }

            return new WebCamTexture(deviceName, Mathf.Max(1, requestedWidth), Mathf.Max(1, requestedHeight), Mathf.Max(1, requestedFps));
        }

        private int GetAllowedDeviceIndex(int candidateIndex)
        {
            if (IsAllowedDevice(WebCamTexture.devices[candidateIndex].name, includeVirtualCameras))
            {
                return candidateIndex;
            }

            var fallback = GetNextAllowedDeviceIndex(candidateIndex - 1, includeVirtualCameras);
            return fallback >= 0 ? fallback : candidateIndex;
        }

        private int GetPreferredDeviceIndex()
        {
            var devices = WebCamTexture.devices;
            if (!string.IsNullOrWhiteSpace(preferredDeviceName))
            {
                for (var index = 0; index < devices.Length; index++)
                {
                    if (devices[index].name == preferredDeviceName && IsAllowedDevice(devices[index].name, includeVirtualCameras))
                    {
                        preferredDeviceIndex = index;
                        return index;
                    }
                }
            }

            return GetAllowedDeviceIndex(Mathf.Clamp(preferredDeviceIndex, 0, devices.Length - 1));
        }

        private int GetNextAllowedDeviceIndex(int currentIndex, bool allowVirtualDevices)
        {
            var devices = WebCamTexture.devices;
            for (var offset = 1; offset <= devices.Length; offset++)
            {
                var index = (currentIndex + offset + devices.Length) % devices.Length;
                if (IsAllowedDevice(devices[index].name, allowVirtualDevices))
                {
                    return index;
                }
            }

            return -1;
        }

        private bool IsAllowedDevice(string deviceName, bool allowVirtualDevices)
        {
            if (allowVirtualDevices || string.IsNullOrWhiteSpace(deviceName))
            {
                return true;
            }

            var lower = deviceName.ToLowerInvariant();
            return !lower.Contains("virtual") &&
                   !lower.Contains("obs") &&
                   !lower.Contains("vtube") &&
                   !lower.Contains("studio") &&
                   !lower.Contains("snap") &&
                   !lower.Contains("manycam");
        }

        private string GetFormatLabel()
        {
            return useRequestedFormat
                ? $"请求 {requestedWidth}x{requestedHeight}@{requestedFps}"
                : "设备默认格式";
        }

        public void StopCamera()
        {
            if (webcamTexture == null)
            {
                return;
            }

            var textureToStop = webcamTexture;
            if (webcamTexture.isPlaying)
            {
                webcamTexture.Stop();
            }

            webcamTexture = null;
            if (sharedTexture == textureToStop)
            {
                sharedTexture = null;
                hasSharedDevice = false;
                ResetSharedFormat();
            }
            hasActiveDevice = false;
            ResetCameraMetrics();
            StatusText = "摄像头已停止";
            ActiveDeviceName = "无";
        }

        public void ForceStopSharedCamera()
        {
            StopCamera();
        }

        public void ReleaseForSceneReuse()
        {
            ReleaseLocalReference();
        }

        private bool AdoptSharedTextureIfAvailable()
        {
            if (sharedTexture == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(preferredDeviceName) && hasSharedDevice && sharedDevice.name != preferredDeviceName)
            {
                return false;
            }

            if (!SharedFormatMatchesRequest())
            {
                ForceStopSharedTexture();
                return false;
            }

            webcamTexture = sharedTexture;
            activeDevice = sharedDevice;
            hasActiveDevice = hasSharedDevice;
            ActiveDeviceName = hasSharedDevice ? sharedDevice.name : preferredDeviceName;
            ResetCameraMetrics();
            StatusText = $"摄像头运行中：{ActiveDeviceName}（跨场景复用）";
            return true;
        }

        private bool SharedFormatMatchesRequest()
        {
            if (!useRequestedFormat && !sharedUseRequestedFormat)
            {
                return true;
            }

            return useRequestedFormat == sharedUseRequestedFormat
                && requestedWidth == sharedRequestedWidth
                && requestedHeight == sharedRequestedHeight
                && requestedFps == sharedRequestedFps;
        }

        private static void ForceStopSharedTexture()
        {
            if (sharedTexture != null && sharedTexture.isPlaying)
            {
                sharedTexture.Stop();
            }

            sharedTexture = null;
            hasSharedDevice = false;
            ResetSharedFormat();
        }

        private static void ResetSharedFormat()
        {
            sharedUseRequestedFormat = false;
            sharedRequestedWidth = 0;
            sharedRequestedHeight = 0;
            sharedRequestedFps = 0;
        }

        private void ReleaseLocalReference()
        {
            webcamTexture = null;
            hasActiveDevice = false;
            ActiveDeviceName = "无";
            StatusText = "摄像头已交给场景切换复用";
        }

        private void ResetCameraMetrics()
        {
            lastUpdatedFrameAt = -1f;
            cameraFrameIntervalTotalMs = 0f;
            cameraFrameIntervalSamples = 0;
            cameraFrameCount = 0;
        }
    }
}
