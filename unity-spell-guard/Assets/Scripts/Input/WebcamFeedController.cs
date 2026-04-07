using UnityEngine;

namespace SpellGuard.InputSystem
{
    public class WebcamFeedController : MonoBehaviour
    {
        [SerializeField] private bool playOnAwake = true;
        [SerializeField] private int requestedWidth = 640;
        [SerializeField] private int requestedHeight = 480;
        [SerializeField] private int requestedFps = 30;
        [SerializeField] private bool mirrorPreview = true;
        [SerializeField] private int preferredDeviceIndex;

        private WebCamTexture webcamTexture;
        private WebCamDevice activeDevice;
        private bool hasActiveDevice;

        public WebCamTexture Texture => webcamTexture;
        public bool IsRunning => webcamTexture != null && webcamTexture.isPlaying;
        public bool MirrorPreview => mirrorPreview;
        public bool IsFrontFacing => hasActiveDevice && activeDevice.isFrontFacing;
        public bool IsVerticallyFlipped => webcamTexture != null && webcamTexture.videoVerticallyMirrored;
        public int RotationAngle => webcamTexture != null ? webcamTexture.videoRotationAngle : 0;
        public string StatusText { get; private set; } = "摄像头未启动";
        public string ActiveDeviceName { get; private set; } = "无";

        private void Start()
        {
            if (playOnAwake)
            {
                StartCamera();
            }
        }

        private void OnDisable()
        {
            StopCamera();
        }

        public void StartCamera()
        {
            StopCamera();

            if (WebCamTexture.devices == null || WebCamTexture.devices.Length == 0)
            {
                StatusText = "未找到可用摄像头";
                ActiveDeviceName = "无";
                return;
            }

            preferredDeviceIndex = Mathf.Clamp(preferredDeviceIndex, 0, WebCamTexture.devices.Length - 1);
            var device = WebCamTexture.devices[preferredDeviceIndex];
            activeDevice = device;
            hasActiveDevice = true;
            webcamTexture = new WebCamTexture(device.name, requestedWidth, requestedHeight, requestedFps);
            webcamTexture.Play();
            ActiveDeviceName = device.name;
            StatusText = $"摄像头运行中：{device.name}";
        }

        public void StopCamera()
        {
            if (webcamTexture == null)
            {
                return;
            }

            if (webcamTexture.isPlaying)
            {
                webcamTexture.Stop();
            }

            webcamTexture = null;
            hasActiveDevice = false;
            StatusText = "摄像头已停止";
            ActiveDeviceName = "无";
        }
    }
}
