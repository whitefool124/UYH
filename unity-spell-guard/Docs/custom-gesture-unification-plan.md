# Custom Gesture Unification Plan

本文档是自定义动态手势系统的统一重构执行方案。后续 Codex 或人工开发应优先遵循本文档，而不是继续沿用早期只围绕 `PalmCenter` 轨迹的实现规划。

## 1. 目标

自定义动态手势不只识别手掌位置移动，还必须支持手指内部运动和手型变化序列，例如：

- swipe / wave / circle：手掌或整只手的空间轨迹
- point to fist / open to pinch：静态手型的短时切换
- finger snap：拇指与中指的接触、释放和速度峰值
- pinch zoom：拇指与食指或双指距离的缩放变化
- crab pinch：食指、中指、拇指的周期性开合

统一后的系统要做到：

1. Unity runtime recognizer 是唯一判定真相。
2. 浏览器工作台和外部工具只负责采集、导入、预览和调用 Unity 等价逻辑，不再拥有一套独立判定标准。
3. 每个模板显式声明动态模式，识别器按模式走不同通道。
4. 导入后必须自动自检，报告能说明为什么命中或失败。
5. 保持 `GestureAction / GestureIntent` 为玩法消费边界，玩法代码不得直接读取模板细节。

## 2. 当前问题

现状不是缺少算法，而是判定入口太多：

- `src/main.js` 在浏览器里用自己的 DTW 规则验证。
- `CustomGestureRecognizer` 在 Unity 里又使用轨迹模板、动态规则和特征序列。
- `tools/ExternalRegression/Program.cs` 会生成并放宽模板参数。
- `CustomGestureLibrary` 加载时会 sanitize、补模板、推断规则、钳制阈值。

结果是同一份 JSON 在不同位置可能得到不同结论。重构的第一原则是收束判定权。

## 3. 外部方案复审结论

本节用于记录 2026-05-25 复审后的决策。参考对象包括 MediaPipe Gesture Recognizer / Model Maker、Unity XR Hands 自定义手势、AnyGesture、$Q/$P stroke recognizer，以及基于 MediaPipe landmarks 的动态手势研究路线。

### 3.1 外部成熟方案的共同点

1. **输入先统一成 landmarks 或 hand tracking events。**  
   MediaPipe 自定义训练流程会先用 Hand Landmarker 从图片中抽取手部 landmarks，再训练/评估/导出模型；Unity XR Hands 自定义手势也先把手追踪数据抽象成 Hand Shape / Hand Pose 条件。我们的 `GestureFrame -> CustomGestureFrameSample` 方向正确。

2. **静态手势和动态手势不是同一个问题。**  
   Unity XR Hands 的官方自定义手势主要是 Hand Shape / Hand Pose / hold time / detection interval，适合静态姿态。动态手势需要额外时序层，不能只靠一个静态组件解决。

3. **可自定义方案必须有模板库或训练产物。**  
   MediaPipe Model Maker 输出 `.task` 模型；$Q/$P 系列保存用户定义模板；AnyGesture 保存动态手势对象。我们的 JSON template library 是合理的，但必须增加版本、模式、迁移和自检结果。

4. **复杂动态手势要使用手指级特征或序列模型。**  
   AnyGesture 明确强调可处理 individual finger movements；MediaPipe 自定义模型使用 landmark embedding；动态研究路线常使用 landmarks 序列 + LSTM/Transformer/CNN。响指、缩放、螃蟹不应被 palm trajectory 通道吸收。

5. **调试器和验证报告是系统的一部分。**  
   Unity XR Hands 提供 gesture debugger；MediaPipe 训练流程有 train/validation/test；$Q demo 支持把误识别样本追加为模板。我们的导入自检报告不是锦上添花，而是必须做。

### 3.2 对原方案的修正

原方案的大方向保留，但执行策略需要收紧：

- 保留多通道识别目标。
- 不把“重命名枚举”作为第一批破坏性动作。先新增兼容层和 version 字段，再迁移保存格式。
- 不直接废掉浏览器预览。浏览器可以继续做 preview score，但 UI 必须明确它不是最终判定。
- 不在第一阶段引入深度学习训练。MediaPipe Model Maker 是成熟路线，但它会引入数据量、训练环境、模型导出和 Unity 推理集成成本；当前项目先做 template / feature-sequence 系统，论文可把 `.task` 模型作为后续扩展。
- 不让 `CustomGestureLibrary.LoadAll` 悄悄改语义。迁移、补模板、阈值修正都要进入报告。

### 3.3 最终技术路线决策

采用 **rule-gated template sequence recognition**：

```text
landmarks -> normalized features -> pattern-specific gates -> DTW / sequence score -> ambiguity check -> GestureAction
```

这比纯规则更能支持自定义样本，比深度学习更容易在当前毕设工程里稳定落地，也比 palm-only DTW 更适合响指、缩放和螃蟹手势。

暂不采用纯 ML 作为主方案：

- MediaPipe Model Maker 适合静态或短姿态分类，不直接解决当前 Unity 内“用户即时导入动态模板并验证”的闭环。
- LSTM / Transformer 适合论文对比和后续版本，但需要稳定标注数据集与训练/部署管线。
- 当前项目的核心价值是可解释、可导入、可验证、可演示。

### 3.4 风险评估

| 风险 | 等级 | 处理 |
|---|---:|---|
| 一次性拆分识别器导致回归 | 高 | 使用阶段闸门，每阶段测试通过再继续 |
| 旧 JSON enum 名称不兼容 | 高 | 增加 schema version 和迁移层，旧值只读兼容 |
| FeatureSequence 误命中静态抖动 | 中 | 加 minimum feature path、velocity、still-negative 自检 |
| FingerDistanceChange 与 PalmTrajectory 混淆 | 中 | 手掌移动上限和模式专属 gate |
| 浏览器与 Unity 结论不一致 | 高 | 浏览器文案降级为 preview，Unity 自检为最终结果 |
| 复杂 UI 拖慢核心闭环 | 中 | 先做报告和测试，再做 UI polish |

## 4. 统一架构

```text
MediaPipe landmarks
  -> GestureFrame
  -> CustomGestureFrameSample
  -> CustomGestureFeatureExtractor
  -> CustomGestureRecognizer
     -> PalmTrajectory channel
     -> PoseTransition channel
     -> FingerFeature channel
     -> FeatureSequence channel
  -> GestureAction
  -> GestureIntent / gameplay
```

### 3.1 输入边界

统一输入只允许使用单手 21 个 hand landmarks、时间戳、置信度、左右手、静态手型标签。禁止把屏幕区域点选、光标命中、身体位移作为自定义动态手势条件。

### 3.2 模板边界

模板保存样本和派生特征，但识别判定必须由 Unity recognizer 执行。JSON 中允许保存预计算模板，以加速运行时匹配，但加载时不得悄悄改变语义。

### 3.3 输出边界

识别成功只输出 `GestureAction`。是否施放火焰、冰霜、护盾或仅作为验证，应由 `GestureIntent` 映射层决定。

## 5. 数据模型改造

在 `CustomGestureTemplate.cs` 中保留 `CustomGestureKind`，但将动态模式从宽泛枚举改成明确通道。

建议目标结构：

```csharp
public enum CustomGestureDynamicPattern
{
    PalmTrajectory,
    PoseTransition,
    FingerDistanceChange,
    FingerOscillation,
    FeatureSequence
}
```

兼容旧值：

- `Directional` -> `PalmTrajectory`
- `Repeat` -> `PalmTrajectory` 或 `FingerOscillation`，由样本特征推断
- `Loop` -> `PalmTrajectory`
- `FingerSpread` -> `FingerDistanceChange`
- `FeatureSequence` -> `FeatureSequence`

`CustomGestureDynamicRule` 应增加或明确以下字段：

```csharp
public int FingerAIndex;
public int FingerBIndex;
public int FingerCIndex;
public float MinimumFingerDistanceDelta;
public float MinimumFingerDistancePath;
public float MinimumFingerVelocity;
public int MinimumOscillationCount;
public GestureType StartPose;
public GestureType EndPose;
public float PoseTransitionMaxPalmMotion;
```

注意：不要依靠 `Kind == DynamicMotion` 这种粗粒度判断决定算法。必须按 `DynamicRule.Pattern` 分发。

## 6. 特征提取统一

`CustomGestureFeatureExtractor` 应成为所有动态识别通道的基础设施，输出可解释特征。

必须支持：

- 21 点归一化坐标，平移和尺度稳定
- `PalmCenter`
- fingertip relative positions
- finger curl: thumb, index, middle, ring, pinky
- finger spread distances:
  - thumb-index
  - thumb-middle
  - index-middle
  - index-ring
  - middle-ring
- per-frame feature vector
- feature delta and feature path

建议新增 `CustomGestureKinematicFeatureExtractor` 或在现有 extractor 中新增序列方法：

```csharp
public static bool TryExtractFrameFeatures(CustomGestureFrameSample frame, float minConfidence, out CustomGestureFrameFeatures features);
public static bool TryExtractSequenceFeatures(IReadOnlyList<CustomGestureFrameSample> frames, float minConfidence, out CustomGestureSequenceFeatures features);
```

`CustomGestureSequenceFeatures` 至少包含：

- duration
- palmNetDistance
- palmPathLength
- featureNetDistance
- featurePathLength
- selectedFingerDistanceDelta
- selectedFingerDistancePath
- selectedFingerPeakVelocity
- oscillationCount
- dominantStaticPose
- startPose
- endPose

## 7. 识别通道

### 6.1 PalmTrajectory

适用：横扫、上划、画圈、挥动。

逻辑：

1. 从窗口中提取 palm trajectory。
2. 通过运动门限过滤静止窗口。
3. 归一化重采样。
4. 使用 DTW 和方向门限评分。
5. 低于阈值且非冲突时触发。

保留现有 `CustomGestureTrajectoryMatcher`，但把模式名从 `Directional/Loop/Repeat` 收束到 `PalmTrajectory` 子规则。

### 6.2 PoseTransition

适用：指向变拳头、张手变捏合、拳头变张手。

逻辑：

1. 找到窗口起止稳定静态姿态。
2. 判断 `StartPose -> EndPose` 是否匹配。
3. 限制最大手掌位移，避免把 swipe 误判为姿态切换。
4. 限制最大持续时间和冷却。

这是稳定 MVP 通道，也可以解释为“两个静态手势的连续识别”。

### 6.3 FingerDistanceChange

适用：双指缩放、捏合打开、响指的接触释放部分。

逻辑：

1. 选择手指点对，默认 thumb-index 或 thumb-middle。
2. 计算距离序列。
3. 计算净变化、路径长度、峰值速度。
4. 匹配打开、闭合或闭合后释放。
5. 手掌移动过大时拒识。

`FingerSpread` 旧模式迁移到此通道。

### 6.4 FingerOscillation

适用：螃蟹夹动、连续开合、手指招手。

逻辑：

1. 选择一组距离或弯曲特征。
2. 对时间序列做平滑。
3. 统计方向翻转次数或峰谷次数。
4. 约束持续时间、振幅、手掌移动。
5. 满足最小开合次数后触发。

### 6.5 FeatureSequence

适用：响指、复杂手指序列、无法用单个距离解释的手势。

逻辑：

1. 每帧提取归一化特征向量。
2. 重采样到固定长度。
3. 使用 DTW 或序列距离对齐。
4. 加入手掌运动上限或下限，根据模板规则决定。
5. 使用模板内多个样本的最优分数。

响指建议走 `FeatureSequence` 主通道，并允许附加 `FingerDistanceChange` 门限作为前置条件。

## 8. 导入和验证统一

### 7.1 浏览器工作台

浏览器工作台继续负责：

- 摄像头采样
- landmarks 保存
- 模板预览
- JSON 导入导出
- 显示“估计动态模式”

浏览器工作台不再负责最终验证。界面文案应从“验证成功”改为：

- “本地预览匹配”
- “等待 Unity 自检”
- “Unity 自检通过/失败”

如果短期不能让浏览器直接调用 Unity 逻辑，则浏览器只能做弱提示，不能作为通过标准。

### 7.2 Unity 导入

`CustomGestureLibrary.LoadAll` 不应悄悄大幅放宽模板。导入时应该：

1. 读取 JSON。
2. 兼容旧字段。
3. 如果缺少预计算模板，则生成派生数据。
4. 如果缺少动态模式，则根据样本推断并写入 migration note 或日志。
5. 生成 `CustomGestureTemplateValidationReport`。
6. 自检失败的模板不进入运行时 active 列表，或进入列表但标记为 invalid。

### 7.3 自检标准

每个导入模板必须跑：

- self-positive：模板自己的每个样本应能命中自己。
- reverse-negative：明显反向的 palm trajectory 不应命中。
- still-negative：静止窗口不应命中动态模板。
- cross-template ambiguity：与其他模板分数过近时标记冲突。
- confidence-negative：低置信度帧不应命中。

自检报告字段：

```text
gesture_id
display_name
pattern
sample_id
matched
best_score
threshold
failure_reason
active
```

## 9. Codex 执行计划

以下步骤按顺序执行。每一步完成后运行相关测试，再进入下一步。

### Step 0: 冻结行为基线

目标文件：

- `CustomGestureSystemTests.cs`
- `CustomGestureBatchTesterTests.cs`
- `tools/ExternalRegression/Program.cs`
- 当前 active JSON 模板

任务：

- 记录现有能通过的自定义手势测试。
- 保存当前 `--self-check-library` 输出作为 before 报告。
- 新增最小 smoke tests：palm swipe positive、still negative、low confidence negative。
- 不改任何识别逻辑。

验收：

- 能清楚知道重构前哪些行为已经坏、哪些行为不能回归。

### Step 1: 修复诊断文本和失败原因

目标文件：

- `Assets/Scripts/Input/CustomGestureRecognizer.cs`
- `Assets/Scripts/Input/GestureInputRouter.cs`

任务：

- 把乱码中文替换成英文或 UTF-8 中文。
- `LastFailureReason` 必须覆盖主要失败路径。
- `TryResolve` 和 `TryResolveSingle` 都要更新失败原因。

验收：

- 代码编译。
- 现有自定义手势测试通过。
- 手动验证页能看到可读失败原因。

### Step 2: 增加 schema version 和动态模式兼容层

目标文件：

- `CustomGestureTemplate.cs`
- `CustomGestureDynamicRuleEvaluator.cs`
- `CustomGestureLibrary.cs`
- 相关测试

任务：

- 新增 `SchemaVersion`，默认旧模板为 1，新模板保存为 2。
- 新增明确模式枚举值，但旧枚举值只读兼容。
- 加 `CustomGestureDynamicPatternMigration` 辅助方法。
- 不破坏旧模板加载。

验收：

- 旧 `Directional/FingerSpread/FeatureSequence` JSON 能加载。
- 新模板保存为新模式名。

### Step 3: 建立统一特征序列模型

目标文件：

- `CustomGestureFeatureExtractor.cs`
- 新增 `CustomGestureSequenceFeatures.cs` 或等价类型
- `CustomGestureFeatureSequenceMatcher.cs`

任务：

- 增加 finger distance、curl、feature path、velocity、oscillation 计算。
- 让 feature sequence 匹配不依赖 palm 轨迹。

验收：

- 平移/缩放后的同一手型特征距离保持小。
- finger distance change 测试能区分打开和闭合。

### Step 4: 拆分识别通道，但保持统一入口

目标文件：

- `CustomGestureRecognizer.cs`
- 可新增：
  - `CustomGesturePalmTrajectoryRecognizer.cs`
  - `CustomGesturePoseTransitionRecognizer.cs`
  - `CustomGestureFingerFeatureRecognizer.cs`

任务：

- `ScoreDynamicTemplate` 改成按 `DynamicRule.Pattern` 分发。
- PalmTrajectory 不再吞掉所有有 trajectory templates 的动态模板。
- FingerDistanceChange / FingerOscillation / FeatureSequence 不被 palm motion gate 拦掉。
- ambiguous match 逻辑保留。
- `CustomGestureRecognizer.TryResolve` 仍然是唯一 runtime 入口。

验收：

- swipe 使用 PalmTrajectory 命中。
- pinch zoom 使用 FingerDistanceChange 命中。
- crab pinch 使用 FingerOscillation 命中。
- finger snap 使用 FeatureSequence 命中。
- 静止手不命中动态模板。

### Step 5: 导入自检和报告

目标文件：

- `CustomGestureLibrary.cs`
- `tools/ExternalRegression/Program.cs`
- `Assets/Scripts/Tools/CustomGestureBatchTester.cs`
- `Assets/Editor/Tools/CustomGestureBatchTestRunner.cs`

任务：

- 新增 `CustomGestureTemplateValidationReport`。
- 导入后跑 self-check。
- 报告 active/invalid 和 failure reason。
- ExternalRegression 使用同一套阈值，不再单独放宽到 0.78 除非显式参数开启。

验收：

- `--self-check-library` 输出可读 CSV。
- 自检失败时能定位 pattern、score、reason。

### Step 6: 浏览器工作台降级为采集/预览端

目标文件：

- `src/main.js`
- `index.html`

任务：

- UI 文案区分“本地预览”和“Unity 自检”。
- 导出 JSON 包含 `DynamicRule.Pattern` 明确值。
- 默认导出不要强制 `RequiredHandedness: Right`，应来自实际检测或允许 Unknown。
- 不再把浏览器 DTW 分数当成最终验证结果。

验收：

- 导出模板能被 Unity 加载。
- Unity 自检结果是最终通过标准。

### Step 7: 回归测试矩阵

新增或扩展测试：

- `CustomGestureSystemTests`
- `CustomGestureBatchTesterTests`
- `ExternalMotionGestureRecognizerTests`

必须覆盖：

| 手势 | Pattern | Positive | Negative |
|---|---|---:|---:|
| right swipe | PalmTrajectory | yes | reverse / still |
| point to fist | PoseTransition | yes | palm moved too far |
| pinch open | FingerDistanceChange | yes | palm-only swipe |
| crab pinch | FingerOscillation | yes | single open |
| finger snap | FeatureSequence | yes | slow pinch |

## 10. 分支和提交建议

建议创建分支：

```powershell
git switch -c codex/custom-gesture-unification
```

建议按步骤提交：

1. `docs: define custom gesture unification plan`
2. `fix: restore readable custom gesture diagnostics`
3. `feat: add explicit custom dynamic patterns`
4. `feat: add finger sequence features`
5. `refactor: split custom gesture dynamic scoring`
6. `feat: add custom gesture import self-check`
7. `chore: align browser lab with Unity validation`

## 11. 完成定义

重构完成必须满足：

1. Unity 编译 0 error。
2. 自定义手势相关 EditMode / PlayMode 测试通过。
3. 浏览器导出的模板能被 Unity 加载。
4. Unity 自检报告可读。
5. 至少四类动态手势均有测试样本：
   - palm trajectory
   - pose transition
   - finger distance change
   - feature sequence or oscillation
6. 玩法层只消费 `GestureAction / GestureIntent`。
7. `Assets/ProjectGestureLibrary/CustomGestures` 中的 active 模板能在开发者验证页逐个验证。

## 12. 执行结果记录

2026-05-25 已按 Step 0 到 Step 7 完成第一轮统一落地：

- Unity runtime recognizer 保持为最终识别入口，浏览器工作台已降级为采集、预览和导出端。
- 动态手势已按 `PalmTrajectory / PoseTransition / FingerDistanceChange / FingerOscillation / FeatureSequence` 分通道处理。
- `CustomGestureFeatureExtractor` 已统一输出手指距离、弯曲、路径、速度和振荡等序列特征。
- 导入时 `CustomGestureLibrary` 会生成 `CustomGestureTemplateValidationReport`，自检失败的模板不会进入 active runtime 列表。
- 外部回归工具 `--self-check-library` 输出 `failure_reason` 和 `active`，并使用同一套动态 pattern 迁移逻辑。
- 回归矩阵已覆盖 palm trajectory、still negative、low confidence negative、pinch open、crab pinch、finger snap-like feature sequence。

已通过检查：

```text
dotnet build unity-spell-guard/SpellGuard.PlayModeTests.csproj
dotnet build tools/ExternalRegression/ExternalRegression.csproj
node --check src/main.js
dotnet run --project tools/ExternalRegression/ExternalRegression.csproj -- --root . --self-check-library unity-spell-guard/Assets/ProjectGestureLibrary/CustomGestures --report build-temp/custom-gesture-self-check-final
```

当前项目手势库自检结果：

```text
Templates: 1
Checked samples: 5
Matched samples: 5
Report: build-temp/custom-gesture-self-check-final/template_self_check.csv
```

剩余边界：Unity Test Runner 在当前本机 batchmode 环境曾能完成脚本编译，但未稳定产出测试 XML；因此本轮以 `.csproj` 编译和外部自检作为自动化验收，后续如需 CI 级结论，应在 Unity Editor/Test Runner 环境中重新跑 PlayMode 测试。

## 13. 给 Codex 的执行提示

可在新会话中直接使用：

```text
继续开发 E:\bishe\gesture-game 的 Unity 项目。请严格按照 `unity-spell-guard/Docs/custom-gesture-unification-plan.md` 执行自定义动态手势统一重构。

目标不是只做稳定快速的 PalmCenter 动态手势，而是一步到位建立安全可行的多通道自定义动态手势系统：PalmTrajectory、PoseTransition、FingerDistanceChange、FingerOscillation、FeatureSequence。Unity `CustomGestureRecognizer` 必须成为唯一最终判定入口，浏览器工作台只负责采集、预览和导出，导入后由 Unity 自检报告决定模板是否有效。

请按文档 Step 0 到 Step 7 逐步改动。每一步都要保留现有游戏输入架构，玩法层只能消费 `GestureAction / GestureIntent`，不能直接依赖模板内部数据。不要删除用户已有数据集和模板文件。完成后运行可用的 Unity/测试/外部回归检查，并在最终回复中列出通过项、未能运行项和剩余风险。
```

## 14. 参考资料

- MediaPipe custom gesture recognizer / Model Maker: https://ai.google.dev/edge/mediapipe/solutions/customization/gesture_recognizer
- MediaPipe Gesture Recognizer task guide: https://ai.google.dev/edge/mediapipe/solutions/vision/gesture_recognizer
- Unity XR Hands custom gesture workflow: https://docs.unity.cn/Packages/com.unity.xr.hands@1.5/manual/gestures/define-a-gesture.html
- Unity XR Hands Static Hand Gesture component: https://docs.unity.cn/Packages/com.unity.xr.hands@1.5/manual/gestures/static-hand-gesture.html
- AnyGesture paper: https://www.mdpi.com/2076-3417/12/4/1888
- $Q recognizer: https://depts.washington.edu/acelab/proj/dollar/qdollar.html
- On-device real-time custom hand gesture recognition: https://openaccess.thecvf.com/content/ICCV2023W/CV4Metaverse/papers/Uboweja_On-Device_Real-Time_Custom_Hand_Gesture_Recognition_ICCVW_2023_paper.pdf
