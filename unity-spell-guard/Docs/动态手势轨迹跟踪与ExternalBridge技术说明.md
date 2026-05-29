# 动态手势轨迹跟踪与 ExternalBridge 技术说明

## 定位

本项目面向体感游戏演示场景，核心目标是将摄像头中的手部轨迹稳定映射为游戏内移动、施法与 UI 控制指令。当前推荐演示链路为 ExternalBridge：Python 侧负责摄像头读取、MediaPipe Hands 推理、动态手势事件生成和 UDP 发送；Unity 侧负责输入路由、动作映射、反馈显示、性能采集与游戏逻辑响应。

## 现有技术路线

1. 摄像头采集：OpenCV 打开外接摄像头，默认 320x240、30fps 请求，优先 DirectShow 后端。
2. 手部关键点检测：MediaPipe Hands 输出 21 个手部关键点。
3. 静态手势分类：基于指尖、关节相对位置判断 point、fist、v、openPalm 等姿态。
4. 动态轨迹识别：基于食指指尖轨迹窗口计算位移、速度、轴向优势和冷却，识别上下左右扫。
5. 轻量断帧补偿：MediaPipe 短暂丢手时，通过 OpenCV 光流追踪上一帧指尖位置，只用于视觉连续和轨迹连续，不触发静态技能。
6. Unity 输入融合：ExternalGestureBridgeProvider 接收 UDP 帧并转换为 GestureFrame、GestureCommand 和 MotionGestureEvent。
7. 游戏反馈：GestureFeedbackHud 显示当前手势、动态动作、冷却条、捕捉点与性能采集按钮。

## ExternalBridge v2

早期桥接脚本采用单循环：

```text
capture.read -> MediaPipe.process -> UDP send
```

该结构的问题是 MediaPipe 某帧处理变慢时会阻塞摄像头读取，造成画面和识别共同变卡。v2 改为双线程结构：

```text
摄像头线程：持续读取最新帧，只保留最新一张
识别线程：处理最新帧，不排队旧帧
```

这样即使识别耗时波动，也不会积压过期帧。演示中手部捕捉率从约 70%-80% 区间提升到接近全程有手，最新采集中 external_hand_packets 为 713/714。

## 动态手势实现

上下左右扫采用“指向姿态 + 轨迹窗口”的组合判定，避免普通移动误触发：

- rawGesture 必须近期为 point。
- 在约 0.55 秒窗口内累计食指指尖轨迹。
- 位移需超过最小距离阈值。
- 速度需超过最小速度阈值。
- 主轴位移需明显大于副轴位移。
- 触发后进入 2 秒冷却，给玩家复位时间。

Python 侧直接生成 motionGesture，例如 swipeLeftToRight、swipeRightToLeft、swipeTopToBottom、swipeBottomToTop。Unity 收到后直接推送 MotionGestureEvent，减少 Unity 低采样导致的漏检。

## 丢帧与捕捉稳定性

项目尝试过 YOLO 人体 ROI 和手部 ROI 二次 MediaPipe 重试。实验结果显示，ROI 重试命中率低且显著拖慢包率，因此默认关闭，仅保留实验开关。

当前稳定策略为：

- MediaPipe model_complexity=0，降低推理耗时。
- 检测/跟踪阈值降低到 0.35/0.25，提高连续输出倾向。
- 摄像头采集线程独立运行。
- 光流 fallback 在短暂丢手时补偿指尖位置。
- hand hold 在短窗口内保持上一帧状态，减少 UI 闪断。
- Unity 捕捉点 HUD 增加限速、垂直死区和异常跳变抑制，避免瞬移和上下抽搐。

## 性能采集字段

GesturePerformanceMonitor 导出 CSV，核心字段包括：

- external_packets：ExternalBridge UDP 包数量。
- external_hand_packets：有手包数量，可计算真实捕捉率。
- external_predicted_packets：光流/保持补偿帧数量。
- external_motion_packets：Python 侧直接生成的动态手势数量。
- avg_hand_update_interval_ms / p95_hand_update_interval_ms：Unity 侧手部更新间隔。
- last_external_motion_debug：动态识别与 fallback 诊断 JSON。
- last_external_performance：Python 端性能 JSON。

last_external_performance 典型字段：

```text
captureFps        摄像头读取线程帧率
processFps        MediaPipe 处理线程帧率
captureReadMs     摄像头读取平均耗时
mediapipeMs       MediaPipe 平均处理耗时
udpMs             UDP 发送平均耗时
flowFallbackPackets 光流补偿次数
flowFailures      光流失败次数
```

## Unity 侧模块

- ExternalBridgeProcessLauncher：随 UDP 接收器启动 Python 桥，传入摄像头、阈值、线程采集和光流参数。
- UdpGestureReceiver：监听 UDP 包并推送到 ExternalGestureBridgeProvider。
- ExternalGestureBridgeProvider：维护外部手势快照、关键点、动态手势事件和输入命令历史。
- ExternalMotionGestureRecognizer：保留 Unity 侧稀疏轨迹识别作为兜底，不重复消费 Python motionGesture。
- GestureInputRouter：统一 Mock、NativeMediapipe、ExternalBridge 三种输入模式。
- GestureFeedbackHud：显示捕捉点、动态手势、技能冷却、性能采集按钮。
- GesturePerformanceMonitor：记录演示性能、捕捉率与手势触发统计。

## 演示默认配置

推荐使用 ExternalBridge 模式：

```text
camera-index 0
backend dshow
width 320
height 240
fps 30
model-complexity 0
min-detection-confidence 0.35
min-tracking-confidence 0.25
threaded capture enabled
optical flow enabled
YOLO disabled
hand ROI retry disabled
```

如果现场背景复杂，可以实验性开启 YOLO 低频人体框，但不建议作为默认演示配置。

## 论文表述建议

论文中可将该方法表述为“基于关键点轨迹窗口与实时补偿的动态手势轨迹跟踪方法”。核心贡献点包括：

1. 使用静态姿态门控约束动态轨迹触发，降低误触发。
2. 将动态手势事件前移到视觉桥接端生成，减少 Unity 采样频率对识别的影响。
3. 采用最新帧缓冲的双线程桥接结构，降低推理耗时对摄像头采集的阻塞。
4. 使用轻量光流作为短时丢帧补偿，兼顾实时性和视觉连续性。
5. 建立游戏内性能采集与 CSV 证据链，支持捕捉率、延迟、触发率的可复现实验分析。
