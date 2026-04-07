# Spell Guard Unity MVP

这是基于现有浏览器原型迁移出来的 Unity 2022.3.62f2c1 项目骨架。

## 当前内容

- 第一人称 3D 战斗原型脚本
- Mock 手势输入层 + Unity原生MediaPipe输入插槽 + 兼容外部桥接接口
- 指向转向 / 指向抬手前进 / 三法术施放的基础逻辑
- 简易敌人生成、追击、护盾与生命值逻辑
- SceneContext + Bootstrap 架构装配层，便于后续替换输入、HUD 与战斗系统
- 菜单 / 设置 / 教程 / 训练场 / 结果页流程闭环
- 训练统计、战斗统计与手势停留确认 UI
- Editor 菜单一键生成原型场景

## 打开方式

1. 用 Unity Hub 打开 `unity-spell-guard/`
2. 等待 Unity 导入脚本
3. 菜单栏执行 `Spell Guard/Create Prototype Scene`
4. 打开生成的 `Assets/Scenes/SpellGuardPrototype.unity`
5. 点击 Play

现在场景内已经包含完整前端流程：

- 主菜单
- 设置页（结印确认时长 / 敌人节奏）
- 教程页
- 训练场
- 正式战斗
- 结果页

场景生成器现在会自动创建：

- `SpellGuardSceneContext`：统一收纳场景关键引用
- `SpellGuardBootstrap`：在 Awake 时完成系统装配

这层只负责架构接线，不改变当前玩法规则。

## 输入模式

- `F1`：切换输入模式（Mock / NativeMediapipe / ExternalBridge）
- `NativeMediapipe` 是主目标模式：全部在 Unity 里运行
- `ExternalBridge` 保留为兼容/过渡方案

## Mock 手势输入

- `Tab`：切换是否检测到手
- `1`：指向（Point）
- `2`：握拳（Fist / 火焰）
- `3`：V 手势（Ice）
- `4`：张掌（Shield）
- `0`：清空当前手势
- `I / J / K / L`：移动虚拟手在屏幕中的位置
- `Left Shift`：加快虚拟手移动速度

## 当前战斗映射

- Point：控制视角转向
- Point + 手位较高：向前移动
- Fist：火焰术
- VSign：冰霜术
- OpenPalm：护盾术

同一手势不会连续重复释放；切到另一种法术手势即可直接开始下一次施法确认。

## 当前流程控制

- 非战斗界面用 `Point` 手势指向 UI 项并停留确认
- 设置页可循环切换确认时长与敌人节奏
- 训练场会记录指向确认次数、三种法术训练次数和最近一次成功法术
- 结果页会显示得分、命中、施法次数与命中率
- 在菜单/设置/教程/训练/结果页中，`OpenPalm` 长按可返回主菜单

## Unity 内部运行方案（主路线）

当前项目已经内置了 `NativeMediapipeGestureProvider` 和 `NativeMediapipeGestureRunner`。

要让整个游戏真正全部在 Unity 里跑，需要你把 **homuler/MediaPipeUnityPlugin** 的预编译包导入当前工程。根据官方 README：

- Unity 2022.3 支持
- Windows Editor 支持手部 landmark detection / gesture recognition
- 推荐直接导入 release 中的预编译包，而不是只拉源码仓库

### 你需要做的最小步骤

1. 从 `homuler/MediaPipeUnityPlugin` 的 `v0.16.3` release 页面下载预编译包，推荐以下任一资源：
   - `MediaPipeUnity.0.16.3.unitypackage`
   - `com.github.homuler.mediapipe-0.16.3.tgz`
   - `MediaPipeUnityPlugin-all.zip`
2. 导入到 `unity-spell-guard` 工程
3. 执行 `Spell Guard/Create Prototype Scene`
4. 打开并运行 `Assets/Scenes/SpellGuardPrototype.unity`
5. 按 `F1` 切到 `NativeMediapipe`

当前确认可用的官方资源地址是：

- `https://github.com/homuler/MediaPipeUnityPlugin/releases/download/v0.16.3/MediaPipeUnity.0.16.3.unitypackage`
- `https://github.com/homuler/MediaPipeUnityPlugin/releases/download/v0.16.3/com.github.homuler.mediapipe-0.16.3.tgz`

当前工程已经把：

- 输入 provider 抽象
- 摄像头组件
- 场景装配层
- 玩法消费层

都拆开并接通了；当前仓库中的原生 runner 会直接使用插件包内的 `gesture_recognizer.bytes` 模型，把识别结果写入 `SetSnapshot(...)`。

当前这条路径默认使用 `LocalResourceManager` 读取包内模型，所以 **Unity Editor 内可直接运行**。如果后面要打包独立桌面版，再把资源加载切到 `StreamingAssets` 即可。

## 外部桥接接口（兼容路径）

项目现在已经内置了：

- `WebcamFeedController`：负责打开本机摄像头并在 HUD 上显示预览
- `ExternalGestureBridgeProvider`：负责接收外部识别结果
- `UdpGestureReceiver`：负责接收本地识别桥通过 UDP 发送的手势结果
- `GestureInputRouter`：在 Mock / NativeMediapipe / ExternalBridge 之间切换

你后续只要把外部识别结果推送到 `ExternalGestureBridgeProvider` 即可。可调用：

```csharp
PushGesture("point", 0.52f, 0.74f, 0.95f, true);
PushSnapshot(true, GestureType.Fist, new Vector2(0.48f, 0.61f), 0.93f);
ClearSnapshot();
```

其中 `x/y` 使用视口坐标（0~1）。

## 摄像头直接控制游玩（兼容桥接）

当前项目仍然支持“本地 Python 识别桥 -> Unity 实时控制”这一条兼容链路，但这不是最终目标路线。

### 1. 安装桥接依赖

建议使用 Python 3.10+ 创建虚拟环境，然后在 `unity-spell-guard/bridge/` 目录执行：

```bash
pip install -r requirements.txt
```

### 2. 在 Unity 中运行

1. 打开并运行 `SpellGuardPrototype.unity`
2. 按 `F1` 切到 `ExternalBridge`
3. 确认左上角 HUD 中看到 UDP 监听状态

### 3. 启动本地识别桥

```bash
cd unity-spell-guard/bridge
python mediapipe_udp_bridge.py --show-preview
```

默认会把识别结果发送到：

- `127.0.0.1:5053`

### 4. 当前真实游玩映射

- 指向（point）：控制转向
- 指向且手位较高：向前移动
- 握拳（fist）：火焰术
- V（v）：冰霜术
- 张掌（openPalm）：护盾术

说明：为了避免摄像头冲突，`UdpGestureReceiver` 默认会让外部识别桥独占摄像头，因此真实游玩时应以 Python 预览窗口作为调试画面。
