using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mediapipe;
using Mediapipe.Unity;
using Mediapipe.Unity.CoordinateSystem;
using Mediapipe.Unity.Experimental;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public class NativeMediapipeGestureRunner : MonoBehaviour
    {
        private const string InputStreamName = "input_video";
        private const string OutputVideoStreamName = "output_video";
        private const string LandmarksStreamName = "landmarks";

        private const string GraphConfig = @"input_stream: ""input_video""
output_stream: ""output_video""

node {
  calculator: ""FlowLimiterCalculator""
  input_stream: ""input_video""
  input_stream: ""FINISHED:output_video""
  input_stream_info: {
    tag_index: ""FINISHED""
    back_edge: true
  }
  output_stream: ""throttled_input_video""
}

node: {
  calculator: ""ImageTransformationCalculator""
  input_stream: ""IMAGE:throttled_input_video""
  input_side_packet: ""ROTATION_DEGREES:input_rotation""
  input_side_packet: ""FLIP_HORIZONTALLY:input_horizontally_flipped""
  input_side_packet: ""FLIP_VERTICALLY:input_vertically_flipped""
  output_stream: ""IMAGE:transformed_input_video""
}

node {
  calculator: ""HandLandmarkTrackingCpu""
  input_stream: ""IMAGE:transformed_input_video""
  input_side_packet: ""NUM_HANDS:num_hands""
  output_stream: ""LANDMARKS:landmarks""
  output_stream: ""HANDEDNESS:handedness""
  output_stream: ""PALM_DETECTIONS:multi_palm_detections""
  output_stream: ""HAND_ROIS_FROM_LANDMARKS:multi_hand_rects""
  output_stream: ""HAND_ROIS_FROM_PALM_DETECTIONS:multi_palm_rects""
}

node {
  calculator: ""HandRendererSubgraph""
  input_stream: ""IMAGE:transformed_input_video""
  input_stream: ""DETECTIONS:multi_palm_detections""
  input_stream: ""LANDMARKS:landmarks""
  input_stream: ""HANDEDNESS:handedness""
  input_stream: ""NORM_RECTS:0:multi_palm_rects""
  input_stream: ""NORM_RECTS:1:multi_hand_rects""
  output_stream: ""IMAGE:output_video_raw""
}

node: {
  calculator: ""ImageTransformationCalculator""
  input_stream: ""IMAGE:output_video_raw""
  input_side_packet: ""ROTATION_DEGREES:output_rotation""
  output_stream: ""IMAGE:output_video""
  node_options: {
    [type.googleapis.com/mediapipe.ImageTransformationCalculatorOptions] {
      flip_vertically: true
    }
  }
}";

        [SerializeField] private NativeMediapipeGestureProvider targetProvider;
        [SerializeField] private WebcamFeedController webcamFeed;
        [SerializeField] private int maxNumHands = 1;
        [SerializeField] private int poolSize = 3;
        [SerializeField] private bool autoStart = false;
        [SerializeField] private int requiredStableFrames = 3;

        private TextureFramePool textureFramePool;
        private CalculatorGraph calculatorGraph;
        private OutputStream<ImageFrame> outputVideoStream;
        private OutputStream<List<NormalizedLandmarkList>> landmarksStream;
        private Coroutine runCoroutine;
        private bool mediapipeInitialized;
        private bool graphRunning;

        private readonly object resultLock = new object();
        private List<NormalizedLandmarkList> latestLandmarkLists;
        private bool latestLandmarksDirty;

        private GestureType candidateGesture = GestureType.None;
        private GestureType stableGesture = GestureType.None;
        private int candidateFrames;
        private long latestTimestamp;

        public string StatusText { get; private set; } = "原生识别未启动";

        public void Configure(NativeMediapipeGestureProvider provider, WebcamFeedController feed)
        {
            targetProvider = provider;
            webcamFeed = feed;
        }

        private void OnEnable()
        {
            if (autoStart)
            {
                StartRunner();
            }
        }

        public void StartRunner()
        {
            if (runCoroutine == null && targetProvider != null && webcamFeed != null)
            {
                runCoroutine = StartCoroutine(Run());
            }
        }

        private void OnDisable()
        {
            if (runCoroutine != null)
            {
                StopCoroutine(runCoroutine);
                runCoroutine = null;
            }

            outputVideoStream?.Dispose();
            outputVideoStream = null;
            landmarksStream?.Dispose();
            landmarksStream = null;

            if (calculatorGraph != null)
            {
                if (graphRunning)
                {
                    try
                    {
                        calculatorGraph.CloseAllPacketSources();
                        calculatorGraph.WaitUntilDone();
                    }
                    catch (Exception)
                    {
                    }
                }

                calculatorGraph.Dispose();
                calculatorGraph = null;
                graphRunning = false;
            }

            textureFramePool?.Dispose();
            textureFramePool = null;

            if (mediapipeInitialized)
            {
                GpuManager.Shutdown();
                Glog.Shutdown();
                Protobuf.ResetLogHandler();
                mediapipeInitialized = false;
            }
        }

        private void Update()
        {
            List<NormalizedLandmarkList> landmarkLists = null;
            lock (resultLock)
            {
                if (latestLandmarksDirty)
                {
                    latestLandmarksDirty = false;
                    landmarkLists = latestLandmarkLists;
                }
            }

            if (landmarkLists == null)
            {
                return;
            }

            ApplyResult(landmarkLists);
        }

        private IEnumerator Run()
        {
            if (targetProvider == null || webcamFeed == null)
            {
                StatusText = "原生识别缺少 Provider 或 WebcamFeed 引用";
                targetProvider?.SetStatusText(StatusText);
                yield break;
            }

            targetProvider.Configure(webcamFeed);
            targetProvider.SetStatusText("正在初始化原生识别");

            Protobuf.SetLogHandler(Protobuf.DefaultLogHandler);
            Glog.Initialize("SpellGuardLegacyMediapipe");
            IResourceManager resourceManager = new LocalResourceManager();
            yield return resourceManager.PrepareAssetAsync("hand_landmark_full.bytes", overwrite: true);
            yield return resourceManager.PrepareAssetAsync("hand_recrop.bytes", overwrite: true);
            yield return resourceManager.PrepareAssetAsync("handedness.txt", overwrite: true);
            yield return resourceManager.PrepareAssetAsync("palm_detection_full.bytes", overwrite: true);
            mediapipeInitialized = true;

            if (!webcamFeed.IsRunning)
            {
                webcamFeed.StartCamera();
                yield return new WaitUntil(() => webcamFeed.Texture != null && webcamFeed.Texture.width > 16);
            }

            textureFramePool = new TextureFramePool(webcamFeed.Texture.width, webcamFeed.Texture.height, TextureFormat.RGBA32, poolSize);
            calculatorGraph = new CalculatorGraph();

            outputVideoStream = new OutputStream<ImageFrame>(calculatorGraph, OutputVideoStreamName, true);
            landmarksStream = new OutputStream<List<NormalizedLandmarkList>>(calculatorGraph, LandmarksStreamName, true);

            var config = CalculatorGraphConfig.Parser.ParseFromTextFormat(GraphConfig);
            calculatorGraph.Initialize(config);
            outputVideoStream.StartPolling();
            landmarksStream.AddListener(OnLandmarksOutput);
            calculatorGraph.StartRun(BuildSidePacket());
            graphRunning = true;
            latestTimestamp = 0L;
            StatusText = "原生识别运行中";
            targetProvider.SetStatusText(StatusText);

            var waitForEndOfFrame = new WaitForEndOfFrame();

            while (enabled && calculatorGraph != null)
            {
                if (webcamFeed.Texture == null || webcamFeed.Texture.width <= 16)
                {
                    yield return null;
                    continue;
                }

                if (!textureFramePool.TryGetTextureFrame(out var textureFrame))
                {
                    yield return null;
                    continue;
                }

                yield return waitForEndOfFrame;

                var sourceTexture = webcamFeed.Texture;
                textureFrame.ReadTextureOnCPU(sourceTexture, false, webcamFeed.IsVerticallyFlipped);
                var imageFrame = textureFrame.BuildImageFrame();
                textureFrame.Release();

                latestTimestamp = GetCurrentTimestampMicrosec();
                calculatorGraph.AddPacketToInputStream(InputStreamName, Packet.CreateImageFrameAt(imageFrame, latestTimestamp));

                var outputTask = outputVideoStream.WaitNextAsync();
                yield return new WaitUntil(() => outputTask.IsCompleted);

                if (outputTask.Result.ok && outputTask.Result.packet != null)
                {
                    var image = outputTask.Result.packet.Get();
                    image?.Dispose();
                }
            }
        }

        private void OnLandmarksOutput(object stream, OutputStream<List<NormalizedLandmarkList>>.OutputEventArgs eventArgs)
        {
            var packet = eventArgs.packet;
            var value = packet == null ? null : packet.Get(NormalizedLandmarkList.Parser);
            lock (resultLock)
            {
                latestLandmarkLists = value;
                latestLandmarksDirty = true;
            }
        }

        private PacketMap BuildSidePacket()
        {
            var sidePacket = new PacketMap();

            var rawRotation = ToRotationAngle(webcamFeed.RotationAngle);
            var inputRotation = rawRotation.Reverse();
            var isInverted = ImageCoordinate.IsInverted(inputRotation);
            var shouldBeMirrored = false;
            var inputHorizontallyFlipped = isInverted ^ shouldBeMirrored;
            var inputVerticallyFlipped = !isInverted;

            if ((inputHorizontallyFlipped && inputVerticallyFlipped) || inputRotation == RotationAngle.Rotation180)
            {
                inputRotation = inputRotation.Add(RotationAngle.Rotation180);
                inputHorizontallyFlipped = !inputHorizontallyFlipped;
                inputVerticallyFlipped = !inputVerticallyFlipped;
            }

            sidePacket.Emplace("input_rotation", Packet.CreateInt((int)inputRotation));
            sidePacket.Emplace("input_horizontally_flipped", Packet.CreateBool(inputHorizontallyFlipped));
            sidePacket.Emplace("input_vertically_flipped", Packet.CreateBool(inputVerticallyFlipped));
            sidePacket.Emplace("output_rotation", Packet.CreateInt((int)rawRotation));
            sidePacket.Emplace("num_hands", Packet.CreateInt(maxNumHands));

            return sidePacket;
        }

        private void ApplyResult(List<NormalizedLandmarkList> landmarkLists)
        {
            if (landmarkLists == null || landmarkLists.Count == 0 || landmarkLists[0] == null || landmarkLists[0].Landmark.Count <= 8)
            {
                targetProvider.SetSnapshot(false, GestureType.None, new Vector2(0.5f, 0.5f), 0f);
                targetProvider.ClearHandLandmarks();
                targetProvider.SetStatusText("原生识别运行中，等待手进入镜头");
                ResetGestureStability();
                return;
            }

            var hand = landmarkLists[0];
            var pointerTip = hand.Landmark[8];
            var rawGesture = ClassifyGestureFromLandmarks(hand);
            var gesture = StabilizeGesture(rawGesture);
            var landmarks = new Vector2[hand.Landmark.Count];
            for (var index = 0; index < hand.Landmark.Count; index++)
            {
                var landmark = hand.Landmark[index];
                landmarks[index] = new Vector2(landmark.X, landmark.Y);
            }

            targetProvider.SetHandLandmarks(landmarks);
            targetProvider.SetSnapshot(true, gesture, new Vector2(pointerTip.X, pointerTip.Y), 1f);
            StatusText = $"原生识别：{gesture.ToChinese()}";
            targetProvider.SetStatusText(StatusText);
        }

        private GestureType StabilizeGesture(GestureType rawGesture)
        {
            if (rawGesture == GestureType.None)
            {
                ResetGestureStability();
                return GestureType.None;
            }

            if (rawGesture != candidateGesture)
            {
                candidateGesture = rawGesture;
                candidateFrames = 1;
            }
            else
            {
                candidateFrames += 1;
            }

            if (candidateFrames >= requiredStableFrames || stableGesture == GestureType.None)
            {
                stableGesture = candidateGesture;
            }

            return stableGesture;
        }

        private void ResetGestureStability()
        {
            candidateGesture = GestureType.None;
            stableGesture = GestureType.None;
            candidateFrames = 0;
        }

        private static GestureType ClassifyGestureFromLandmarks(NormalizedLandmarkList hand)
        {
            var landmarks = hand.Landmark;
            if (landmarks == null || landmarks.Count <= 20)
            {
                return GestureType.None;
            }

            var indexExtended = IsFingerExtended(landmarks, 8, 6, 5);
            var middleExtended = IsFingerExtended(landmarks, 12, 10, 9);
            var ringExtended = IsFingerExtended(landmarks, 16, 14, 13);
            var pinkyExtended = IsFingerExtended(landmarks, 20, 18, 17);
            var spread = Distance(landmarks[8], landmarks[20]);
            var thumbSpread = Distance(landmarks[4], landmarks[5]);
            var fingersUp = 0;

            if (indexExtended) fingersUp++;
            if (middleExtended) fingersUp++;
            if (ringExtended) fingersUp++;
            if (pinkyExtended) fingersUp++;

            if (!indexExtended && !middleExtended && !ringExtended && !pinkyExtended)
            {
                return GestureType.Fist;
            }

            if (indexExtended && middleExtended && !ringExtended && !pinkyExtended && spread > 0.12f)
            {
                return GestureType.VSign;
            }

            if (indexExtended && !middleExtended && !ringExtended && !pinkyExtended)
            {
                return GestureType.Point;
            }

            if (fingersUp >= 4 && thumbSpread > 0.09f)
            {
                return GestureType.OpenPalm;
            }

            return GestureType.Unknown;
        }

        private static bool IsFingerExtended(Google.Protobuf.Collections.RepeatedField<NormalizedLandmark> landmarks, int tipIndex, int pipIndex, int mcpIndex)
        {
            var tip = landmarks[tipIndex];
            var pip = landmarks[pipIndex];
            var mcp = landmarks[mcpIndex];
            return tip.Y < pip.Y - 0.015f && pip.Y < mcp.Y - 0.005f;
        }

        private static float Distance(NormalizedLandmark a, NormalizedLandmark b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private long GetCurrentTimestampMicrosec()
        {
            return (long)(Time.realtimeSinceStartup * 1000000f);
        }

        private static RotationAngle ToRotationAngle(int rotationDegrees)
        {
            var normalized = ((rotationDegrees % 360) + 360) % 360;
            return normalized switch
            {
                90 => RotationAngle.Rotation90,
                180 => RotationAngle.Rotation180,
                270 => RotationAngle.Rotation270,
                _ => RotationAngle.Rotation0,
            };
        }
    }
}
