# 符印守卫 Unity 项目技术文档

## 1. 文档定位

这份文档面向接手 `unity-spell-guard/` 子项目的人，目标是用最少的阅读成本回答四件事：

1. 这个 Unity 工程当前由哪些代码、场景、工具和依赖组成。
2. 运行时从启动到进入玩法的装配链路是什么。
3. 手势输入怎样经过抽象后驱动移动、施法、菜单与调试反馈。
4. 哪些结论已被代码、配置、测试直接验证，哪些只是基于当前实现的推断。

本文档优先以仓库内可直接核对的证据为准：`ProjectSettings/`、`Packages/`、`Assets/Scripts/`、`Assets/Editor/`、`Assets/Tests/`、`Assets/Scenes/`。旧文档和 README 仅在与代码一致时作为辅助说明使用。

## 2. 项目范围与仓库边界

- 仓库根目录同时包含浏览器端 MVP 与 Unity 原型。
- 本文只讨论 `unity-spell-guard/`。
- 根目录 `README.md` 主要对应浏览器端项目。
- `unity-spell-guard/README.md` 对应 Unity 原型的入口说明。

接手时如果只关注 Unity 原型，请直接从 `unity-spell-guard/` 子目录开始，不要把根目录前端内容混进技术判断。

## 3. 已验证基础信息

### 3.1 Unity 版本

已验证文件：`ProjectSettings/ProjectVersion.txt`

- Unity Editor 版本：`2022.3.62f2c1`

### 3.2 包依赖

已验证文件：`Packages/manifest.json`、`Packages/packages-lock.json`

关键依赖包括：

- `com.github.homuler.mediapipe`：以本地 tgz 形式接入的 MediaPipe Unity 包。
- `com.besty.unity-skills`：以本地文件路径接入的包。
- `com.unity.test-framework`：Unity 测试框架。
- `com.unity.ugui`：UGUI。
- `com.unity.timeline`：Timeline。
- `com.unity.visualscripting`：Visual Scripting。

这说明该项目不是单脚本 demo，而是一个明确依赖第三方视觉运行时与测试框架的 Unity 原型工程。

### 3.3 Build Settings 当前状态

已验证文件：`ProjectSettings/EditorBuildSettings.asset`

- `Assets/Scenes/SpellGuardStart.unity`
- `Assets/Scenes/SpellGuardPrototype.unity`

当前 Build Settings 已配置为“开始场景在前、战斗原型场景在后”。独立构建会先进入手势友好的开始菜单，再根据玩家选择加载战斗 / 训练原型场景。

## 4. 目录与程序集结构

已验证目录与文件：`Assets/Scripts/`、`Assets/Editor/`、`Assets/Tests/`、`Assets/Scenes/`、`*.asmdef`

| 路径 | 作用 |
|---|---|
| `Assets/Scripts/Core/` | 启动装配、流程控制、设置、界面状态 |
| `Assets/Scripts/Input/` | 输入抽象、多输入源路由、桥接、动作识别、命令历史 |
| `Assets/Scripts/Player/` | 玩家移动与施法 |
| `Assets/Scripts/Combat/` | 生命、敌人、伤害、刷新 |
| `Assets/Scripts/UI/` | 调试 HUD 与世界空间动态反馈 |
| `Assets/Editor/` | 原型场景生成与训练集验证工具 |
| `Assets/Tests/EditMode/` | Editor 工具与数据校验测试 |
| `Assets/Tests/PlayMode/` | 运行时输入与动作识别测试 |
| `Assets/Scenes/` | 当前原型场景 |

程序集边界：

- `SpellGuard.Runtime.asmdef`
- `SpellGuard.Editor.asmdef`
- `SpellGuard.EditModeTests.asmdef`
- `SpellGuard.PlayModeTests.asmdef`

这个结构已经把运行时代码、Editor 工具和测试拆成独立程序集，工程边界相对清晰。

## 5. 推荐阅读顺序

如果是第一次接手，建议按下面顺序理解项目：

1. `unity-spell-guard/README.md`
2. `ProjectSettings/ProjectVersion.txt`
3. `Packages/manifest.json`
4. `Assets/Editor/CreatePrototypeScene.cs`
5. `Assets/Scripts/Core/SpellGuardSceneContext.cs`
6. `Assets/Scripts/Core/SpellGuardBootstrap.cs`
7. `Assets/Scripts/Input/*`
8. `Assets/Scripts/Player/*`
9. `Assets/Scripts/Core/SpellGuardFlowController.cs`
10. `Assets/Scripts/UI/*`
11. `Assets/Tests/*`

## 6. 场景生成与启动方式

### 6.1 README 中给出的启动路径

已验证文件：`unity-spell-guard/README.md`

README 当前写明的推荐流程是：

1. 在 Unity Hub 中打开 `unity-spell-guard/`
2. 等待导入完成
3. 执行菜单 `Spell Guard/Create Start Scene` 或 `Spell Guard/Create Prototype Scene`
4. 打开 `Assets/Scenes/SpellGuardStart.unity`
5. 点击 Play

### 6.2 原型场景以工具生成优先

已验证文件：`Assets/Editor/CreatePrototypeScene.cs`

该工具会创建并装配：

- 场景环境：地面、通道、祭坛、墙体、灯光、标识
- 开始场景：`SpellGuardStart.unity`、开始相机、开始菜单运行时、输入路由与设置组件
- 玩家对象：`PlayerRoot`、`CharacterController`、相机 Pivot、主相机
- 输入对象：`WebcamFeedController`、`MockGestureInputProvider`、`NativeMediapipeGestureProvider`、`NativeMediapipeGestureRunner`、`NativeMotionGestureRecognizer`、`ExternalGestureBridgeProvider`、`ExternalMotionGestureRecognizer`、`UdpGestureReceiver`、`GestureInputRouter`
- 玩法对象：`PlayerHealth`、`FpsGestureMotor`、`GestureSpellCaster`
- 装配与流程对象：`SpellGuardSceneContext`、`SpellGuardBootstrap`、`SpellGuardGameSettings`、`SpellGuardFlowController`、`EnemySpawner`、`GameFlowManager`
- UI / 反馈对象：`DebugHud`、`MotionGestureFeedbackBoard`

结论：原型场景不是手工搭建优先，而是通过 Editor 工具稳定生成一套可运行骨架。

### 6.3 场景生成已有测试支撑

已验证文件：`Assets/Tests/EditMode/CreatePrototypeSceneTests.cs`

测试当前验证了：

- `PlayerRoot`、`RitualLane`、`SpellDais`、`ArenaSign`、`RitualGate` 是否存在。
- `NativeMotionGestureRecognizer` 与 `ExternalMotionGestureRecognizer` 是否接入 `SpellGuardSceneContext`。
- `MotionGestureFeedbackBoard` 是否存在并绑定输入路由器与主相机。

这意味着场景生成工具不仅用于省事，也承担了“原型场景可重复搭建”的职责。

## 7. 启动装配链路

### 7.1 `SpellGuardSceneContext`

已验证文件：`Assets/Scripts/Core/SpellGuardSceneContext.cs`

`SpellGuardSceneContext` 负责集中保管并暴露关键引用，覆盖：

- 输入侧：Provider、Router、原生识别、桥接识别、UDP、摄像头
- 玩家侧：玩家根节点、相机、移动器、施法器、生命值
- 战斗侧：敌人刷新器、流程管理器、设置、FlowController
- UI 侧：`DebugHud`、`MotionGestureFeedbackBoard`

两个关键方法：

- `AutoBindMissingReferences()`：自动查找或补挂缺失组件
- `IsValid(out reason)`：启动前校验关键引用是否完整

它本质上是场景级依赖容器，而不是业务逻辑本体。

### 7.2 `SpellGuardBootstrap`

已验证文件：`Assets/Scripts/Core/SpellGuardBootstrap.cs`

`Bootstrap()` 当前会：

1. 触发 `sceneContext.AutoBindMissingReferences()`
2. 调用 `sceneContext.IsValid(...)` 进行完整性检查
3. 配置 `FpsGestureMotor`
4. 配置 `GestureSpellCaster`
5. 配置 `EnemySpawner` 与 `GameFlowManager`
6. 配置 `SpellGuardFlowController`
7. 配置原生识别、外部桥接、UDP、HUD、反馈板
8. 订阅输入模式变化并同步输入后端生命周期

输入后端生命周期控制体现在 `SyncInputBackendLifecycle()`：

- 外部桥接模式下启动 `UdpGestureReceiver`
- 非外部桥接模式下启动 `NativeMediapipeGestureRunner`
- 非外部桥接且摄像头未运行时启动 `WebcamFeedController`

这部分已经体现出“按模式管理输入后端启停”的设计，不是所有后端永久常开。

## 8. 输入系统总览

当前输入系统的可验证结构可以概括为：

**输入提供者 -> 路由器 -> 帧/命令抽象 -> 动态动作识别 -> 玩法与 UI 消费**

### 8.1 `GestureInputProviderBase`

已验证文件：`Assets/Scripts/Input/GestureInputProviderBase.cs`

该基类统一暴露：

- `CurrentSnapshot`
- `CurrentMotionGesture`
- `CurrentGestureFrame`
- `CurrentGestureCommand`
- `RecentGestureCommands`

这为上层玩法屏蔽了底层输入源差异。

### 8.2 `GestureInputRouter`

已验证文件：`Assets/Scripts/Input/GestureInputRouter.cs`

支持三种输入模式：

- `Mock`
- `NativeMediapipe`
- `ExternalBridge`

当前用 `F1` 在运行时切换模式。路由器自身不做识别，只负责把当前模式对应的数据统一暴露给上层。

### 8.3 `MockGestureInputProvider`

已验证文件：`Assets/Scripts/Input/MockGestureInputProvider.cs`、`unity-spell-guard/README.md`

README 当前写明 Mock 模式支持：

- `Tab` 切换手是否存在
- `1/2/3/4/0` 切换静态手势
- `I/J/K/L` 移动虚拟手
- `Left Shift` 加速

它的主要价值是让菜单、移动、施法和流程逻辑可以在无真实识别链路时进行稳定验证。

### 8.4 原生 MediaPipe 链路

已验证文件：`Assets/Scripts/Input/NativeMediapipeGestureRunner.cs`、`Assets/Scripts/Input/NativeMediapipeGestureProvider.cs`

当前可直接确认的事实：

- `NativeMediapipeGestureRunner` 内含 MediaPipe graph 配置字符串。
- 它依赖 `WebcamFeedController` 提供图像输入。
- 它会准备 `hand_landmark_full.bytes`、`hand_recrop.bytes`、`handedness.txt`、`palm_detection_full.bytes` 等资源。
- 它会将 landmark 结果回写给 `NativeMediapipeGestureProvider`。
- `requiredStableFrames` 用于稳定帧过滤。

`NativeMediapipeGestureProvider` 更像运行时状态仓库：保存当前快照、当前动作事件、当前帧、当前命令与命令历史，供玩法与 HUD 读取。

### 8.5 外部桥接链路

已验证文件：`Assets/Scripts/Input/ExternalGestureBridgeProvider.cs`、`Assets/Scripts/Input/UdpGestureReceiver.cs`

当前可确认：

- 外部链路通过 `ExternalGestureBridgeProvider` 统一暴露快照、帧、动作事件和命令。
- `UdpGestureReceiver` 是桥接接收入口之一。
- `SpellGuardBootstrap` 会在外部桥接模式下启动 `UdpGestureReceiver`。

从代码命名与装配方式看，外部链路用于接收外部视觉结果或回放数据；但“具体上游进程的完整部署方式”不在仓库内完全自描述，属于待确认项。

## 9. 运行时抽象与命令层

### 9.1 `GestureFrame`

已验证文件：`Assets/Scripts/Input/GestureFrame.cs`

这是运行时帧级抽象，至少覆盖：

- 输入来源
- 手数量
- 主手状态
- 静态手势
- 关键点与掌心位置

### 9.2 `GestureCommand`

已验证文件：`Assets/Scripts/Input/GestureCommand.cs`

命令层把上层消费统一收束为两类：

- `StaticPose`
- `Motion`

这层比直接读快照更贴近玩法语义。

### 9.3 `GestureCommandHistory`

已验证文件：`Assets/Scripts/Input/GestureCommandHistory.cs`

这部分承担近期命令历史维护，供序列匹配使用。

### 9.4 `GestureSequenceMatcher`

已验证文件：`Assets/Scripts/Input/GestureSequenceMatcher.cs`、`Assets/Tests/PlayMode/GestureSequenceMatcherTests.cs`

测试已覆盖：

- Point -> Fist -> Snap 序列匹配
- 顺序错误拒绝
- 超出时间窗拒绝
- `PointToFist` 与 `Snap` 组合样例
- 包含双手静态姿态与动态动作的组合样例

可验证结论：项目已经具备时间窗内的命令序列匹配能力。

## 10. 动态动作识别

### 10.1 原生动作识别

已验证文件：`Assets/Scripts/Input/NativeMotionGestureRecognizer.cs`、`Assets/Tests/PlayMode/NativeMotionGestureRecognizerTests.cs`

测试当前覆盖的动作包括：

- `SwipeLeftToRight`
- `Snap`
- `OpenPalmSlapLeftToRight`
- `OpenPalmSlapRightToLeft`
- `PointToFist`

同时还验证了：

- slap 对 generic swipe 的优先级
- PointToFist 的最短停留要求
- PointToFist 的掌心位移限制
- 识别冷却约束

### 10.2 外部桥接动作识别

已验证文件：`Assets/Scripts/Input/ExternalMotionGestureRecognizer.cs`、`Assets/Tests/PlayMode/ExternalMotionGestureRecognizerTests.cs`

测试当前覆盖：

- 从外部帧检测 `SwipeLeftToRight`
- 从外部帧检测 `Snap`
- 同一 Unity 帧内缓冲多帧时的 swipe 检测
- 使用外部帧自带时间戳而不是单次接收时间进行检测

这说明原生链路和外部桥接链路都已接入动态动作识别，而不仅仅支持静态姿态。

## 11. 玩家与战斗逻辑

### 11.1 `FpsGestureMotor`

已验证文件：`Assets/Scripts/Player/FpsGestureMotor.cs`

当前可验证逻辑：

- 读取当前 `GestureFrame`
- 使用主手 `ViewportPosition` 控制 yaw 与 pitch
- 仅在主手被追踪且静态手势为 `Point` 时启用转向与前进判定
- 当 `ViewportPosition.y >= forwardThreshold` 时沿前方移动
- 使用 `CharacterController` 处理移动与重力

README 中旧版“Point 对应转向 / 高位前进”的说明已不再代表当前目标方向，当前策划与实现正向四向离散移动 + 辅助视角方案迁移。

### 11.2 `GestureSpellCaster`

已验证文件：`Assets/Scripts/Player/GestureSpellCaster.cs`

静态姿态施法映射：

- `Fist -> Fire`
- `VSign -> Ice`
- `OpenPalm -> Shield`

动态动作施法映射：

- `Snap` / `PointToFist -> Fire`
- `SwipeLeftToRight` / `OpenPalmSlapLeftToRight -> Ice`
- `SwipeRightToLeft` / `OpenPalmSlapRightToLeft -> Shield`

当前施法行为：

- 火焰术：射线命中敌人后造成伤害
- 冰霜术：射线命中敌人后施加冻结
- 护盾术：激活玩家护盾

静态姿态施法带确认时间；动态动作可直接触发施法。

### 11.3 战斗基础对象

已验证文件：`Assets/Scripts/Combat/PlayerHealth.cs`、`Assets/Scripts/Combat/EnemySpawner.cs`、`Assets/Scripts/Combat/SimpleEnemyController.cs`

从当前命名、调用关系和 HUD 消费方式可以确认：

- `PlayerHealth` 负责生命与护盾状态
- `EnemySpawner` 负责敌人生成与存活列表
- `SimpleEnemyController` 是敌人行为载体

更细的数值节奏应以具体代码和 Inspector 参数为准，本文不展开抄录。

### 11.4 `GameFlowManager`

已验证文件：`Assets/Scripts/Core/GameFlowManager.cs`

当前代码只直接处理两件事：

- 当玩家死亡时设置 `GameOver = true`
- 游戏结束后清空敌人，并允许按 `R` 通过 `SceneManager.LoadScene` 重载当前场景

因此它是较轻量的战斗结束控制器，而不是完整 UI 状态机。

## 12. 菜单、设置与流程控制

### 12.1 屏幕状态

已验证文件：`Assets/Scripts/Core/SpellGuardScreen.cs`

当前状态枚举为：

- `Menu`
- `Settings`
- `Tutorial`
- `Training`
- `Playing`
- `Results`

### 12.2 `SpellGuardFlowController`

已验证文件：`Assets/Scripts/Core/SpellGuardFlowController.cs`

从 `Update()`、`UpdateMenuLikeInput()` 和动作处理逻辑可以确认：

- 菜单态、设置态、教程态、训练态、结果态都走统一的 menu-like 输入更新。
- 菜单焦点通过手部视口位置映射到屏幕区域。
- 停留达到设定时长后触发区域激活。
- 非菜单/非战斗/非训练状态下，`OpenPalm` 长按可返回菜单。
- 部分动态动作可在设置页切换选项，也可在设置页、教程页、结果页返回菜单。
- 战斗结束后会把界面状态切到 `Results`。

### 12.3 设置项

已验证文件：`Assets/Scripts/Core/SpellGuardGameSettings.cs`、`Assets/Scripts/Core/SpellGuardFlowController.cs`

当前明确可调的设置项有：

- 施法确认时长：`confirmSecondsOptions`
- 难度：`difficultyOptions`
- 菜单停留确认时长：`menuDwellSeconds`
- 菜单返回长按时长：`menuBackHoldSeconds`

可直接确认的运行时行为：`SpellGuardFlowController` 在设置页可通过动态动作切换确认时长和敌人节奏。

## 13. 调试与展示反馈

### 13.1 `DebugHud`

已验证文件：`Assets/Scripts/UI/DebugHud.cs`

HUD 当前显示的信息包括：

- 当前界面状态
- 输入模式
- 动态状态
- 当前手势与置信度
- 运行时来源与手数
- 施法状态文本
- 生命、护盾、敌人数
- 前进状态
- 摄像头、设备、原生识别、桥接源、UDP 状态
- 手部与姿态骨架预览

此外，`DebugHud.OnGUI()` 会调用 `flowController.DrawOverlay()`，说明流程界面本身也通过 HUD / Overlay 绘制。

### 13.2 `MotionGestureFeedbackBoard`

已验证文件：`Assets/Scripts/UI/MotionGestureFeedbackBoard.cs`

该组件会：

- 始终朝向相机
- 读取 `CurrentGestureCommand`
- 在 idle / active / snap 状态之间切换文案、颜色与缩放
- 对 `Snap` 单独提供更强提示

这说明动态动作识别结果不只存在于日志或 HUD 文本里，还被做成了可见的世界空间反馈。

## 14. Editor 工具与数据集验证

### 14.1 训练集验证工具

已验证文件：`Assets/Editor/TrainingDatasetValidator.cs`

当前存在 Editor 菜单：`Spell Guard/Validate Training Dataset`

默认数据集根目录解析为：

- `Path.Combine(Application.dataPath, "..", "..", "训练集")`

也就是 Unity 项目目录上两级的 `训练集/` 文件夹。

工具当前检查：

- 数据集根目录是否存在
- `annotations*.zip` 是否存在且数量合理
- `videos*.zip` 是否存在
- 注释包内是否包含 `metadata.csv`、`classIdx.txt`、`Annot_TrainList.txt`、`Annot_TestList.txt`、`Video_TrainList.txt`、`Video_TestList.txt`
- 视频包内是否包含 `.tgz`
- 每个 `.tgz` 内是否包含 `.avi`

### 14.2 训练集验证测试

已验证文件：`Assets/Tests/EditMode/TrainingDatasetValidatorTests.cs`

测试当前覆盖：

- 真实默认数据集根结构验证
- 缺少 `metadata.csv` 时拒绝注释包
- 不含 `.tgz` 时拒绝视频包

这部分说明项目已把训练数据资源完整性纳入可执行校验，而不只是停留在说明文档中。

## 15. 自动化测试现状

### 15.1 PlayMode

已验证文件：`Assets/Tests/PlayMode/*`

当前 PlayMode 测试覆盖至少包括：

- Mock / Native / External provider 的运行时适配输出
- 命令历史读取
- 序列匹配
- 原生动态动作识别
- 外部桥接动态动作识别

### 15.2 EditMode

已验证文件：`Assets/Tests/EditMode/*`

当前 EditMode 测试覆盖至少包括：

- 原型场景生成
- 训练集结构验证

### 15.3 可验证结论

这个项目并非“只靠手动点 Play 验收”的原型，而是已经对场景生成、输入抽象、动作识别与数据集校验建立了基础自动化测试。

## 16. 当前工程状态总结

### 16.1 已验证事实

- Unity 版本已锁定为 `2022.3.62f2c1`。
- 项目存在一套 Editor 工具生成的原型场景骨架。
- 输入路由支持 `Mock`、`NativeMediapipe`、`ExternalBridge` 三种模式。
- 原生链路和外部桥接链路都支持动态动作识别。
- 静态姿态和动态动作都能驱动施法。
- 存在菜单、设置、教程、训练、战斗、结果六种屏幕状态。
- Debug HUD 与世界空间反馈板都已经接入运行时。
- 训练集校验工具与对应测试存在。
- Build Settings 当前包含 `SpellGuardStart.unity` 与 `SpellGuardPrototype.unity`，并以开始场景为第一场景。

### 16.2 基于代码的合理推断

- 这个项目的目标重心是“毕设演示可运行原型”，而不是完整商业化打包工程。
- `Assets/Scenes/SpellGuardStart.unity` 是当前正式入口场景；`Assets/Scenes/SpellGuardPrototype.unity` 是战斗 / 训练原型场景。
- 外部桥接链路应当面向外部视觉进程、回放或兼容性接入，但仓库内未完整记录其部署拓扑。

### 16.3 待确认项

- 外部桥接数据源的实际运行方式、启动命令和端口约定。
- MediaPipe 原生链路在目标答辩机器上的性能、驱动和设备兼容性。
- 是否存在仓库外的演示打包脚本、CI 流程或答辩专用场景配置。
- `SpellGuardStart.unity` 与 `SpellGuardPrototype.unity` 是否始终由 Editor 工具重新生成，还是允许人工继续编辑并提交。

## 17. 接手建议

1. 第一次导入时先确认 Unity 版本与本地包路径是否可解析。
2. 用 `Spell Guard/Create Start Scene` 和 `Spell Guard/Create Prototype Scene` 重新生成场景，优先验证场景生成链路是否正常。
3. 先用 `Mock` 模式验证开始场景菜单、训练入口、战斗入口、移动、施法、结果页切换。
4. 再分别验证 `NativeMediapipe` 与 `ExternalBridge` 模式。
5. 跑 `EditMode` 与 `PlayMode` 测试，确认当前环境下的基线状态。
6. 如果要做正式打包，复核 Build Settings 顺序与平台配置，再梳理是否需要独立构建脚本。

## 18. 快速检索表

| 需求 | 先看哪里 |
|---|---|
| 工程版本与依赖 | `ProjectSettings/ProjectVersion.txt`、`Packages/manifest.json` |
| 场景如何搭起来 | `Assets/Editor/CreatePrototypeScene.cs` |
| 场景如何装配 | `Assets/Scripts/Core/SpellGuardSceneContext.cs`、`SpellGuardBootstrap.cs` |
| 输入模式切换 | `Assets/Scripts/Input/GestureInputRouter.cs` |
| 原生识别链路 | `Assets/Scripts/Input/NativeMediapipeGestureRunner.cs` |
| 外部桥接链路 | `Assets/Scripts/Input/ExternalGestureBridgeProvider.cs`、`UdpGestureReceiver.cs` |
| 动作识别规则 | `Assets/Scripts/Input/*MotionGestureRecognizer*.cs` + 对应测试 |
| 施法映射 | `Assets/Scripts/Player/GestureSpellCaster.cs` |
| 菜单与状态页 | `Assets/Scripts/Core/SpellGuardFlowController.cs` |
| 调试显示 | `Assets/Scripts/UI/DebugHud.cs` |
| 数据集验证 | `Assets/Editor/TrainingDatasetValidator.cs` |

## 19. 论文实验支撑补充

### 19.1 性能采集器

已新增 `Assets/Scripts/Diagnostics/GesturePerformanceMonitor.cs`，用于补齐论文中“实时性”和“低延迟”论证所需的数据来源。该组件由 `CreatePrototypeScene` 自动挂载到 `PlayerRoot`，并通过 `SpellGuardSceneContext` 和 `SpellGuardBootstrap` 绑定 `GestureInputRouter` 与 `ExternalGestureBridgeProvider`。

当前采集内容包括：

- 平均 FPS 与最低 FPS。
- 平均帧耗时与 P95 帧耗时。
- ExternalBridge 外部包数量。
- UDP/外部帧平均包间隔。
- 基于外部 timestamp 的估算链路延迟与 P95 延迟。
- 静态命令数、动态命令数以及 Swipe / Snap / BodyShift 等动态动作计数。

运行时快捷键：

- `F8`：开始或停止采集。
- `F9`：导出 CSV。

编辑器中默认导出位置为：

```text
unity-spell-guard/ExperimentResults/gesture_performance_<timestamp>.csv
```

`DebugHud` 已显示性能、桥接延迟和实验记录状态，便于论文截图和答辩演示。

### 19.2 YOLO + MediaPipe 离线 benchmark

已新增 `bridge/offline_yolo_mediapipe_benchmark.py`，用于比较纯 MediaPipe 与 YOLO + MediaPipe 在离线视频上的表现。该脚本复用 `mediapipe_udp_bridge.py` 中的 YOLO 检测、MediaPipe 关键点处理和手势分类 helper。

基础命令示例：

```bash
python bridge/offline_yolo_mediapipe_benchmark.py --video bridge/samples/ipn_real/ipn_229_g05_throw_left.mp4 --max-frames 120
```

默认输出：

```text
bridge/outputs/yolo_mediapipe_benchmark.csv
```

CSV 字段包括：

- `video_name`
- `mode`
- `frame_count`
- `hand_present_frames`
- `hand_present_ratio`
- `avg_confidence`
- `avg_processing_ms`
- `p95_processing_ms`
- `average_fps`
- `yolo_detected_ratio`
- 各静态手势计数

这组数据可用于论文第 6 章比较“纯 MediaPipe”和“YOLO 前置检测 + MediaPipe 关键点”的检测连续性与运行开销。

2026-05-25 的一轮小样本实测表明，这条链路已经可以落地运行，但 YOLO 会明显抬高单帧处理开销。对同一段离线视频 `ipn_229_g05_throw_left.mp4`，30 帧小样本 benchmark 结果为：

- 纯 MediaPipe：`avg_processing_ms = 30.56`
- YOLO + MediaPipe：`avg_processing_ms = 130.06`
- 两种模式的 `hand_present_ratio` 都是 `1.0`

这说明 YOLO 在当前工程里更适合承担外部桥接链路的前置定位、裁剪或复杂背景兜底，而不适合作为 Unity 内部默认实时识别路径。

### 19.3 建议论文引用方式

论文中建议将该实验写成“小规模可复现实验”，而不是写成完整 YOLO 训练。推荐表述为：

> 本文在外部视觉桥接链路中提供 YOLO + MediaPipe 模式，并通过离线视频 benchmark 与纯 MediaPipe 模式进行对比，评估前置检测对手部检出连续性和实时性能的影响。

该表述与当前工程事实一致，也能回应任务书中关于 YOLO 与 MediaPipe 融合的要求。

## 20. 文档说明

本文档中的“已验证”仅表示当前仓库内存在直接证据，不表示所有运行环境都已实测通过。凡是需要外部设备、外部进程、答辩机器环境或仓库外脚本配合的结论，都应在交接时再次确认。
