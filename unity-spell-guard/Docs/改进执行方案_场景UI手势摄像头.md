# 《符印守卫》场景、UI、手势与摄像头稳定性改进执行方案

## 1. 背景与目标

当前版本已经具备主菜单、训练、战斗、结果、调试 HUD、Native MediaPipe、ExternalBridge 等完整演示链路，但多个系统集中在少数场景与少数输入消费者中，导致实际体验中出现以下问题：

- 场景职责过重：`SpellGuardPrototype.unity` 同时承载战斗、训练、暂停、结果、调试与部分菜单流程。
- UI 层级拥挤：`SpellGuardMenuOverlay`、`DebugHud`、手势反馈板与战斗视野互相竞争屏幕空间。
- 手势误触：同一个物理动作在不同系统中被解释为菜单确认、施法、移动或返回。
- 摄像头进程冲突：Native MediaPipe、Webcam 预览、ExternalBridge / Python 侧视觉链路可能争抢同一个摄像头设备，导致启动失败、黑屏、卡住或需要手动关闭进程。

本方案目标不是简单移动 UI 坐标，而是把“视觉层级、场景职责、输入上下文、摄像头生命周期”统一收束，让答辩演示链路稳定、可解释、可复现。

## 2. 当前结构判断

### 2.1 场景与流程

- `Assets/Scenes/SpellGuardStart.unity`：起始菜单场景。
- `Assets/Scenes/SpellGuardPrototype.unity`：主要原型场景，承担训练、战斗、暂停、结果、调试等多数功能。
- `Assets/Scripts/Core/SpellGuardStartMenuController.cs`：起始菜单选择与加载原型场景。
- `Assets/Scripts/Core/SpellGuardFlowController.cs`：原型场景内的主流程状态机，负责 `Menu / Settings / Tutorial / Training / Playing / Paused / Results` 切换。
- `Assets/Scripts/Core/SpellGuardStartSceneLaunch.cs`：跨场景传递启动模式，例如从起始场景进入训练或战斗。

### 2.2 UI 层级

- `Assets/Scripts/UI/SpellGuardMenuOverlay.cs` 使用 `OnGUI` 绘制菜单、设置、教程、训练、暂停、结果等界面。
- `Assets/Scripts/UI/DebugHud.cs` 使用 `OnGUI` 绘制输入模式、摄像头、桥接状态、性能、手势识别、战斗状态等调试信息。
- `Assets/Scripts/UI/MotionGestureFeedbackBoard.cs` 提供世界空间手势反馈。

当前 UI 不是清晰的 Canvas prefab 分层，而是多个脚本直接绘制或场景内嵌对象叠加，因此很容易出现遮挡和信息过载。

### 2.3 手势输入链路

- `Assets/Scripts/Input/GestureInputRouter.cs`：在 Mock、NativeMediapipe、ExternalBridge 之间切换。
- `Assets/Scripts/Input/GestureIntentMapper.cs`：把手势命令映射为菜单、施法、移动和训练意图。
- `Assets/Scripts/Player/GestureSpellCaster.cs`：消费施法意图。
- `Assets/Scripts/Player/FpsGestureMotor.cs`：消费移动意图。
- `Assets/Scripts/UI/SpellGuardMenuOverlay.cs` 与 `SpellGuardStartMenuController.cs`：消费菜单意图。

高风险复用包括：

| 手势 | 当前可能语义 | 风险 |
| --- | --- | --- |
| Fist | 菜单确认 / 火焰术 | 菜单与战斗切换时容易误触 |
| OpenPalm | 返回 / 护盾 / 后退 | 语义过多，误触概率高 |
| Snap / PointToFist | 菜单确认 / 火焰术 | 动态动作可同时像“确认”和“攻击” |
| Swipe | 菜单切换 / 战斗移动 / 训练计数 | 需要明确上下文，否则容易串场 |

## 3. 改进原则

1. **一个场景只服务一个主目标**：菜单、训练、战斗、结果尽量分离。
2. **一个 UI 层只承担一种信息密度**：玩家 HUD、交互面板、调试面板分层控制。
3. **一个手势命令只被消费一次**：由统一仲裁层决定命令属于菜单、训练、战斗还是录制。
4. **同一时刻只允许一个摄像头拥有者**：Native、External、预览、测试脚本不能同时争抢设备。
5. **答辩优先稳定性**：先保证可演示，再追求功能丰富。

## 4. 目标架构

### 4.1 场景结构

建议改为以下结构：

```text
SpellGuardStart.unity
  - 主菜单
  - 设置入口
  - 教程入口
  - 输入模式选择

SpellGuardTraining.unity
  - 基础手势训练
  - 自定义手势录制
  - 手势识别测试
  - 摄像头/输入健康检查

SpellGuardCombat.unity
  - 正式战斗
  - 最小 HUD
  - 暂停面板

SpellGuardResults.unity 或 Combat 内 ResultPanel
  - 本局结果
  - 命中率 / 得分 / 重开 / 返回
```

短期可以不立刻新建所有场景，但代码上应先把 UI 和输入上下文按上述边界拆开。

### 4.2 UI 分层

```text
CanvasRoot
  SafeAreaRoot
    GameplayHudLayer
      - 血量 / 护盾 / 得分
      - 当前识别手势
      - 当前施法确认进度

    InteractionLayer
      - 主菜单
      - 设置
      - 教程
      - 训练步骤卡片
      - 暂停 / 结果面板

    GestureFeedbackLayer
      - 手势状态
      - 误触拦截提示
      - 当前上下文

    DebugLayer
      - 摄像头预览
      - MediaPipe / Bridge 状态
      - FPS / 延迟 / 包统计
      - 默认隐藏，F2 开关
```

训练场 UI 改为步骤式，不再一次显示所有按钮：

1. Point 指向确认
2. Fist 火焰术
3. VSign 冰霜术
4. OpenPalm 护盾术
5. Swipe 移动
6. Snap / PointToFist 确认
7. 自定义手势录制与测试

### 4.3 手势上下文仲裁

新增统一输入仲裁概念：`GestureActionBroker`。

```text
GestureInputRouter
  -> GestureActionBroker
      -> MenuAction
      -> TrainingAction
      -> CombatAction
      -> RecordingAction
```

建议上下文规则：

| 上下文 | 允许动作 | 禁止动作 |
| --- | --- | --- |
| Menu | MenuNext / MenuPrevious / MenuConfirm / MenuBack | Cast / Move / Record |
| Training | 当前训练步骤需要的动作、训练导航 | 正式战斗伤害、敌人推进 |
| Combat | Cast / Move / Pause | MenuConfirm / MenuBack / Record |
| Paused | Resume / Restart / Return | Cast / Move |
| Recording | 只采样 landmark 序列 | 菜单确认、施法、移动、返回 |

命令消费规则：

- 每个 `GestureCommand` 带唯一 `CommandId` 或由 `(source, kind, triggeredTime, trackId)` 生成稳定键。
- Broker 记录最近已消费命令。
- 一条命令只允许被一个系统消费。
- 被拦截的命令进入 UI 反馈，例如“战斗中已忽略菜单确认手势”。

## 5. 摄像头与视觉进程稳定性方案

代码链路已确认：摄像头冲突不是单点 bug，而是 Unity 预览、Native MediaPipe、UDP 外部桥接和 Python 侧采集之间缺少统一所有权协议。当前应按“单一摄像头所有者 + 显式释放 + 模式切换前停旧链路 + 异常路径也释放”的原则处理。

### 5.1 问题判断

摄像头冲突来自以下已定位风险：

- `WebcamFeedController` 使用静态 `sharedTexture` 跨场景复用摄像头；`OnDisable()` 只调用 `ReleaseLocalReference()`，会清空本组件引用，但不会停止底层 `WebCamTexture`。
- `NativeMediapipeGestureRunner.Run()` 会在摄像头未就绪时调用 `webcamFeed.StartCamera()`，但自身 `OnDisable()` 只关闭 MediaPipe graph、stream 和 texture pool，不负责停止摄像头。
- `UdpGestureReceiver.StartReceiver()` 在 `externalBridgeOwnsCamera = true` 时会调用 `webcamFeed.StopCamera()`，这会让 ExternalBridge 模式接管摄像头，但其他组件如果还认为 Unity 摄像头存在，就会出现“状态显示还在、画面却断了”的错觉。
- `DebugHud` 和 `SpellGuardStartMenuController` 的“切换摄像头”会调用 `webcamFeed.TryStartNextPhysicalCamera()`，但没有先统一停掉 Native graph / UDP receiver / 外部桥接状态。
- `mediapipe_udp_bridge.py` 在正常退出路径会 `capture.release()`、`socket_client.close()`，但没有 `try/finally`；如果处理中途异常，仍可能残留摄像头或窗口资源。
- Unity 侧没有直接 `Process.Start` 管理 Python bridge；外部桥接进程主要由用户或脚本启动，因此 Unity 目前只能检测 UDP 状态，不能可靠关闭外部进程。

### 5.2 目标机制

新增 `CameraResourceManager` 或等价生命周期层：

```text
CameraResourceManager
  - CurrentOwner: None / UnityPreview / NativeMediapipe / ExternalBridge / Benchmark
  - Request(owner, deviceName/deviceIndex)
  - Release(owner)
  - ForceReleaseAll()
  - Restart(owner)
  - StatusText
```

基本规则：

- 切换输入模式前，必须先停止当前摄像头拥有者。
- 进入 ExternalBridge 时，Unity 不再直接打开摄像头，只接收 UDP 数据。
- 进入 NativeMediapipe 时，外部桥接脚本必须关闭或明确提示用户关闭。
- Debug 预览不得单独占用第二份摄像头流；它应复用当前拥有者的纹理或只显示桥接状态。
- 退出 Play Mode、切换场景、应用退出时统一释放摄像头。
- `WebcamFeedController` 增加“释放本地引用”和“强制停止共享摄像头”的明确区分，避免场景切换复用和模式切换释放混在一起。
- `NativeMediapipeGestureRunner` 增加显式 `StopRunner()`，由输入模式切换统一调用，而不是只依赖 `OnDisable()`。
- `UdpGestureReceiver` 增加 ExternalBridge 状态清理入口，停止接收时同步清空 `ExternalGestureBridgeProvider` 的 stale frame。
- Python bridge 增加 `try/finally`，保证 `capture.release()`、`socket_client.close()`、`cv2.destroyAllWindows()` 在异常和 Ctrl+C 路径都执行。

### 5.3 用户可见反馈

输入健康检查面板显示：

- 当前输入模式
- 当前摄像头拥有者
- 摄像头设备名
- 是否检测到帧
- 最近帧时间
- ExternalBridge UDP 包数与最后包时间
- 冲突提示与一键恢复按钮

一键恢复流程：

1. 停止 Unity WebCamTexture。
2. 停止 Native MediaPipe runner。
3. 清空 ExternalBridge 当前帧状态。
4. 提示关闭 Python bridge；如果后续引入受控启动脚本，再执行项目内 bridge stop。
5. 延迟 0.5 秒后按当前输入模式重启。

### 5.4 具体代码改造点

| 文件 | 改造点 | 目的 |
| --- | --- | --- |
| `WebcamFeedController.cs` | 增加 `ForceStopSharedCamera()` / `ReleaseForSceneReuse()` / `RestartCamera()` 语义区分 | 明确“场景复用”和“释放设备”不是一回事 |
| `NativeMediapipeGestureRunner.cs` | 抽出公开 `StopRunner()`，`OnDisable()` 复用它；空 `catch` 改为记录 warning | 输入模式切换可确定关闭 graph，异常可诊断 |
| `UdpGestureReceiver.cs` | `StopReceiver()` 后可选清空 bridge provider；启动前走统一 camera release | 避免 ExternalBridge 停止后保留旧帧 |
| `GestureInputRouter.cs` | `SetMode()` 改为“停旧模式 → 清 transient → 启新模式” | 输入模式切换不再只改 enum |
| `DebugHud.cs` / `SpellGuardStartMenuController.cs` | 摄像头切换按钮改走统一 camera manager / router | 避免 UI 绕过输入生命周期 |
| `mediapipe_udp_bridge.py` | 将主循环包进 `try/finally` | 异常退出也释放摄像头、socket、窗口 |

## 6. 分阶段执行计划

### Phase 0：稳定演示热修，优先级最高

目标：先让演示不乱触发、不遮挡、不频繁卡摄像头。

- Debug HUD 默认隐藏，增加 F2 开关。
- 训练场改为步骤式显示，减少同屏按钮。
- 自定义手势录制期间禁用菜单确认、施法和移动。
- `GestureIntentMapper` 增加上下文过滤，至少先做到 Menu / Training / Combat 分离。
- 输入模式切换时显式清空 transient 输入。
- 摄像头切换前先 stop 当前流，失败时显示明确错误而不是静默卡住。
- Python bridge 增加 `try/finally` 资源释放，降低重启桥接脚本后的占用概率。
- `NativeMediapipeGestureRunner` 暴露显式停止入口，供模式切换调用。

验收标准：

- 战斗中 Fist 只施法，不触发菜单确认。
- 菜单中 Fist / Snap 只确认，不施法。
- 录制自定义手势时不会开始战斗、返回菜单或释放法术。
- Debug HUD 关闭后不遮挡主要画面。
- 连续切换 Mock / Native / ExternalBridge 不导致 Unity 无响应或摄像头长时间占用。
- Python bridge 异常退出后可立即重新启动并重新打开摄像头。

### Phase 1：输入仲裁与摄像头生命周期重构

目标：解决误触与摄像头冲突的根因。

- 新增 `GestureActionBroker`。
- 给 `GestureCommand` 增加可追踪消费键。
- 将 `GestureSpellCaster`、`FpsGestureMotor`、菜单控制器改为从 Broker 获取动作。
- 新增 `CameraResourceManager`。
- 统一 `WebcamFeedController`、`NativeMediapipeGestureRunner`、`ExternalBridge` 的启动/停止顺序。
- 增加输入健康检查 UI。

验收标准：

- 一条手势命令最多被一个消费者处理。
- 输入模式切换 10 次无摄像头占用错误。
- 场景切换后摄像头状态正确释放或迁移。
- ExternalBridge 模式下 Unity 不主动打开摄像头。

### Phase 2：UI 与场景结构改造

目标：把当前 OnGUI 堆叠式界面改成可维护的 Canvas 分层。

- 新建 CanvasRoot prefab。
- 拆分 Gameplay HUD、Interaction Panel、Gesture Feedback、Debug Panel。
- 将训练场 UI 改为步骤卡片。
- 将战斗场景 UI 降到最低信息密度。
- 逐步将 `SpellGuardMenuOverlay` 的绘制职责迁移到 prefab / view 组件。

验收标准：

- 1280x720、1920x1080 下无关键按钮遮挡。
- 战斗场景中心区域不被大面积 UI 覆盖。
- Debug 信息默认不可见，但可一键打开。
- 训练流程用户不需要理解全部按钮即可完成。

### Phase 3：场景拆分与答辩包装

目标：让项目结构更符合论文与答辩展示。

- 新建或固化 `SpellGuardTraining.unity`。
- 新建或固化 `SpellGuardCombat.unity`。
- 起始场景只负责入口与设置。
- 结果页从战斗逻辑中分离为独立面板或场景。
- 文档同步更新项目结构图与演示流程。

验收标准：

- Build Settings 顺序清晰。
- 每个场景职责可一句话解释。
- 答辩演示路径固定：启动 → 输入检查 → 训练 → 战斗 → 结果。
- 任意场景返回主菜单不会残留敌人、摄像头、输入 transient 状态。

## 7. 建议实施顺序

建议先执行以下 7 个任务：

1. 关闭默认 Debug HUD，并加 F2 开关。
2. 在录制自定义手势期间禁止所有非录制输入消费。
3. 给菜单 / 训练 / 战斗增加最小上下文过滤。
4. 给 Python bridge 增加异常路径资源释放。
5. 梳理 `WebcamFeedController` 与 Native runner 的 stop/release 路径。
6. 输入模式切换前统一 stop 旧输入源。
7. 训练场 UI 步骤化，只显示当前步骤与下一步。

这 7 项完成后，再进入 Broker 和 Canvas 重构，风险最低。

## 8. 风险与回退

- 如果 Broker 改造范围过大，先在 `GestureIntentMapper` 外层增加轻量 `GestureContextFilter`。
- 如果 Canvas 迁移时间不够，先保留 OnGUI，但拆分 Debug 显示和训练步骤。
- 如果 Native MediaPipe 生命周期难以完全修复，答辩默认使用 Mock 或 ExternalBridge，并在文档中说明 Native 是可切换实验路径。
- 如果 ExternalBridge 进程无法由 Unity 安全管理，则只做检测和提示，不强杀外部进程。

## 9. 准备执行清单

### 9.1 立即执行的热修任务

1. 修改 `bridge/mediapipe_udp_bridge.py`：用 `try/finally` 包住 capture 循环，保证异常退出释放摄像头、socket 和 OpenCV 窗口。
2. 修改 `NativeMediapipeGestureRunner.cs`：抽出 `StopRunner()`，消除空 `catch`，确保图关闭失败时至少记录 warning。
3. 修改 `WebcamFeedController.cs`：增加强制停止共享摄像头的公开方法，保留场景复用能力但让模式切换能真正释放设备。
4. 修改 `GestureInputRouter.cs`：模式切换时调用统一 stop / clear 顺序，避免只切 enum。
5. 修改 `DebugHud.cs`：Debug HUD 默认隐藏，摄像头切换按钮不直接绕过生命周期。
6. 修改 `SpellGuardFlowController.cs` / `SpellGuardMenuOverlay.cs`：录制自定义手势期间阻断菜单确认、施法和移动。

### 9.2 热修验证命令与手动检查

- Unity PlayMode：从 StartScene 进入训练，再返回菜单，再进入战斗，确认摄像头状态不残留。
- 输入模式切换：Mock → Native → ExternalBridge → Mock，连续 10 次。
- Python bridge：启动、Ctrl+C、立即重启，确认摄像头可重新打开。
- UI 检查：Debug HUD 默认不可见，F2 后可显示，再按 F2 可隐藏。
- 手势检查：录制期间做 Fist / OpenPalm / Snap，不触发菜单、施法或移动。

### 9.3 暂不执行但保留的重构任务

- 完整 `GestureActionBroker`。
- Canvas prefab 分层替换 OnGUI。
- Training / Combat 独立场景拆分。
- Unity 内受控启动/停止 Python bridge 进程。
