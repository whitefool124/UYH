using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public class UdpGestureReceiver : MonoBehaviour
    {
        [SerializeField] private ExternalGestureBridgeProvider bridgeProvider;
        [SerializeField] private WebcamFeedController webcamFeed;
        [SerializeField] private bool autoStart = false;
        [SerializeField] private bool externalBridgeOwnsCamera = true;
        [SerializeField] private int listenPort = 5053;
        private readonly object packetLock = new object();
        private readonly System.Collections.Generic.Queue<ExternalVisionFrame> pendingPackets = new System.Collections.Generic.Queue<ExternalVisionFrame>();
        private UdpClient udpClient;
        private Thread receiveThread;
        private volatile bool running;

        public bool IsRunning => running;
        public int ListenPort => listenPort;
        public string StatusText { get; private set; } = "UDP桥未启动";

        private void Start()
        {
            if (autoStart)
            {
                StartReceiver();
            }
        }

        private void Update()
        {
            if (bridgeProvider == null)
            {
                return;
            }

            var processedAnyPacket = false;
            lock (packetLock)
            {
                while (pendingPackets.Count > 0)
                {
                    var packet = pendingPackets.Dequeue();
                    bridgeProvider.PushFrame(packet);
                    processedAnyPacket = true;
                    StatusText = packet.handPresent
                        ? $"UDP已接收：{packet.gesture} ({packet.confidence:F2})"
                        : "UDP已接收：无手";
                }
            }

            if (!processedAnyPacket && running && StatusText.StartsWith("UDP已接收"))
            {
                StatusText = $"UDP桥运行中：127.0.0.1:{listenPort}";
            }
        }

        private void OnDisable()
        {
            StopReceiver();
        }

        public void Configure(ExternalGestureBridgeProvider bridge, WebcamFeedController feed)
        {
            bridgeProvider = bridge;
            webcamFeed = feed;
        }

        [ContextMenu("Start Receiver")]
        public void StartReceiver()
        {
            StopReceiver();

            if (externalBridgeOwnsCamera && webcamFeed != null)
            {
                webcamFeed.StopCamera();
            }

            try
            {
                udpClient = new UdpClient(listenPort);
                running = true;
                receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "SpellGuardUdpReceiver"
                };
                receiveThread.Start();
                StatusText = $"UDP桥运行中：127.0.0.1:{listenPort}";
            }
            catch (Exception exception)
            {
                running = false;
                StatusText = $"UDP桥启动失败：{exception.Message}";
                Debug.LogError(StatusText, this);
            }
        }

        [ContextMenu("Stop Receiver")]
        public void StopReceiver()
        {
            running = false;

            if (udpClient != null)
            {
                udpClient.Close();
                udpClient = null;
            }

            if (receiveThread != null && receiveThread.IsAlive)
            {
                receiveThread.Join(150);
            }

            receiveThread = null;

            lock (packetLock)
            {
                pendingPackets.Clear();
            }

            if (StatusText.StartsWith("UDP桥运行中") || StatusText.StartsWith("UDP已接收"))
            {
                StatusText = "UDP桥已停止";
            }
        }

        private void ReceiveLoop()
        {
            while (running && udpClient != null)
            {
                try
                {
                    var endpoint = new IPEndPoint(IPAddress.Any, listenPort);
                    var bytes = udpClient.Receive(ref endpoint);
                    var json = Encoding.UTF8.GetString(bytes);
                    var packet = JsonUtility.FromJson<ExternalVisionFrame>(json);

                    if (packet == null)
                    {
                        continue;
                    }

                    lock (packetLock)
                    {
                        pendingPackets.Enqueue(packet);
                        while (pendingPackets.Count > 180)
                        {
                            pendingPackets.Dequeue();
                        }
                    }
                }
                catch (SocketException)
                {
                    if (running)
                    {
                        StatusText = "UDP桥连接中断";
                    }
                }
                catch (Exception exception)
                {
                    StatusText = $"UDP桥接收失败：{exception.Message}";
                }
            }
        }
    }
}
