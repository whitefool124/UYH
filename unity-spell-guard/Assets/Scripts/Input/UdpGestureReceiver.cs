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
        [SerializeField] private ExternalBridgeProcessLauncher processLauncher;
        [SerializeField] private bool autoStart = false;
        [SerializeField] private bool externalBridgeOwnsCamera = true;
        [SerializeField] private int listenPort = 5053;
        private readonly object packetLock = new object();
        private readonly System.Collections.Generic.Queue<ExternalVisionFrame> pendingPackets = new System.Collections.Generic.Queue<ExternalVisionFrame>();
        private UdpClient udpClient;
        private Thread receiveThread;
        private volatile bool running;
        private volatile bool stopRequested;
        private int packetCount;

        public bool IsRunning => running;
        public int ListenPort => listenPort;
        public int PacketCount => packetCount;
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
                        ? $"UDP已接收：#{packetCount} {packet.gesture} ({packet.confidence:F2})"
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
            Configure(bridge, feed, processLauncher);
        }

        public void Configure(ExternalGestureBridgeProvider bridge, WebcamFeedController feed, ExternalBridgeProcessLauncher launcher)
        {
            bridgeProvider = bridge;
            webcamFeed = feed;
            processLauncher = launcher;
        }

        [ContextMenu("Start Receiver")]
        public void StartReceiver()
        {
            StopReceiver();

            if (externalBridgeOwnsCamera && webcamFeed != null)
            {
                webcamFeed.StopCamera();
            }

            processLauncher?.StartBridge();

            try
            {
                udpClient = new UdpClient(listenPort);
                running = true;
                stopRequested = false;
                packetCount = 0;
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
            stopRequested = true;
            running = false;

            var client = udpClient;
            if (client != null)
            {
                client.Close();
                udpClient = null;
            }

            if (receiveThread != null && receiveThread.IsAlive)
            {
                if (!receiveThread.Join(500))
                {
                    Debug.LogWarning("UDP桥接收线程未能在关闭窗口内退出。", this);
                }
            }

            receiveThread = null;

            lock (packetLock)
            {
                pendingPackets.Clear();
            }

            bridgeProvider?.ClearSnapshot();
            processLauncher?.StopBridge();

            if (StatusText.StartsWith("UDP桥运行中") || StatusText.StartsWith("UDP已接收"))
            {
                StatusText = "UDP桥已停止";
            }
        }

        private void ReceiveLoop()
        {
            while (running && !stopRequested && udpClient != null)
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
                        packetCount++;
                        while (pendingPackets.Count > 180)
                        {
                            pendingPackets.Dequeue();
                        }
                    }
                }
                catch (SocketException)
                {
                    if (running && !stopRequested)
                    {
                        StatusText = "UDP桥连接中断";
                    }
                }
                catch (ObjectDisposedException)
                {
                    if (running && !stopRequested)
                    {
                        StatusText = "UDP桥连接已关闭";
                    }
                }
                catch (Exception exception)
                {
                    if (running && !stopRequested)
                    {
                        StatusText = $"UDP桥接收失败：{exception.Message}";
                    }
                }
            }
        }
    }
}
