using SpellGuard.Core;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public class WebcamFeedController : MonoBehaviour
    {
        [SerializeField] private bool playOnAwake;
        [SerializeField] private bool useRequestedFormat;
        [SerializeField] private int requestedWidth = 640;
        [SerializeField] private int requestedHeight = 480;
        [SerializeField] private int requestedFps = 30;
        [SerializeField] private bool mirrorPreview = true;
        [SerializeField] private int preferredDeviceIndex;
        [SerializeField] private bool includeVirtualCameras;

        private WebCamTexture webcamTexture;
        private WebCamDevice activeDevice;
        private bool hasActiveDevice;
        private string preferredDeviceName;
        private static WebCamTexture sharedTexture;
        private static WebCamDevice sharedDevice;
        private static bool hasSharedDevice;

        public WebCamTexture Texture => webcamTexture;
        public bool IsRunning => webcamTexture != null && webcamTexture.isPlaying;
        public bool HasTexture => webcamTexture != null;
        public bool HasReadyFrame => webcamTexture != null && webcamTexture.width > 16 && webcamTexture.height > 16;
        public bool MirrorPreview => mirrorPreview;
        public bool IsFrontFacing => hasActiveDevice && activeDevice.isFrontFacing;
        public bool IsVerticallyFlipped => webcamTexture != null && webcamTexture.videoVerticallyMirrored;
        public int RotationAngle => webcamTexture != null ? webcamTexture.videoRotationAngle : 0;
        public string StatusText { get; private set; } = "摄像头未启动";
        public string ActiveDeviceName { get; private set; } = "无";

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

        private void OnDisable()
        {
            ReleaseLocalReference();
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
                webcamTexture.Play();
                ActiveDeviceName = device.name;
                preferredDeviceName = device.name;
                sharedTexture = webcamTexture;
                sharedDevice = device;
                hasSharedDevice = true;
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
            }
            hasActiveDevice = false;
            StatusText = "摄像头已停止";
            ActiveDeviceName = "无";
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

            webcamTexture = sharedTexture;
            activeDevice = sharedDevice;
            hasActiveDevice = hasSharedDevice;
            ActiveDeviceName = hasSharedDevice ? sharedDevice.name : preferredDeviceName;
            StatusText = $"摄像头运行中：{ActiveDeviceName}（跨场景复用）";
            return true;
        }

        private void ReleaseLocalReference()
        {
            webcamTexture = null;
            hasActiveDevice = false;
            ActiveDeviceName = "无";
            StatusText = "摄像头已交给场景切换复用";
        }
    }
}
