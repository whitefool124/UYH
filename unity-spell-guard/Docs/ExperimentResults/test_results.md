# Test Results

本文件归档《符印守卫》Unity 项目的测试结果，供论文第 5 章“系统实现验证”和第 6 章“测试与结果分析”引用。正式截图应放入 `Docs/ThesisAssets/screenshots/`，并在下表中记录对应文件名。

## 自动化测试记录

| Date | Unity Version | Test Suite | Scope | Passed | Failed | Evidence / Screenshot | Notes |
|---|---|---|---|---:|---:|---|---|
| 2026-05-12 | 2022.3.62f2c1 | PlayMode focused fixtures | `FpsGestureMotorTests`, `DemoRunRecorderTests` | 13 | 0 | 待补：`Docs/ThesisAssets/screenshots/test_runner_playmode_focused.png` | 通过 Unity MCP 反射执行受影响 fixture，验证移动闭环和 DemoRunRecorder 导出逻辑。 |
| 2026-05-13 | 2022.3.62f2c1 | PlayMode focused fixture | `SpellGuardFlowControllerTests` | 命令行完成且无失败输出 | 0 | 待补：`Docs/ThesisAssets/screenshots/test_runner_playmode_flow.png` | `dotnet test SpellGuard.PlayModeTests.csproj --filter FullyQualifiedName~SpellGuardFlowControllerTests`，验证结果页法术构成统计和战斗重置。 |
| 待补 | 2022.3.62f2c1 | PlayMode full suite | 全量 PlayMode | 待填 | 待填 | 待补：`Docs/ThesisAssets/screenshots/test_runner_playmode_full.png` | 在 Unity Test Runner 中执行全量 PlayMode 后填写。 |
| 待补 | 2022.3.62f2c1 | EditMode full suite | 全量 EditMode | 待填 | 待填 | 待补：`Docs/ThesisAssets/screenshots/test_runner_editmode_full.png` | 在 Unity Test Runner 中执行全量 EditMode 后填写。 |

## 本轮受影响测试明细

| Test Fixture | Test Case | Result | 验证点 |
|---|---|---|---|
| `FpsGestureMotorTests` | `PointGestureExposesTrackedSnapshot` | PASS | Mock 输入帧能暴露 Point 快照与 Mock 来源。 |
| `FpsGestureMotorTests` | `DisablingInputStopsMovementState` | PASS | 禁用输入会清空移动状态。 |
| `FpsGestureMotorTests` | `SwipeBottomToTopStartsForwardStep` | PASS | 上滑/向前动态动作触发前进一步。 |
| `FpsGestureMotorTests` | `SwipeTopToBottomStartsBackwardStep` | PASS | 下滑/向后动态动作触发后退一步。 |
| `FpsGestureMotorTests` | `PointHoldDoesNotStartForwardStep` | PASS | `Point` 不再作为主移动输入，避免回到空中鼠标式控制。 |
| `FpsGestureMotorTests` | `OpenPalmHoldStartsBackwardStep` | PASS | `OpenPalm` 持续确认触发后退一步，且不误报前进。 |
| `FpsGestureMotorTests` | `StaticMoveHoldDoesNotRepeatBeforeGestureChanges` | PASS | 静态移动在手势未变化前不会连续重复触发。 |
| `FpsGestureMotorTests` | `BodyShiftLeftDoesNotStartStep` | PASS | BodyShift 保留为实验/外部桥接证据，不进入主线移动。 |
| `DemoRunRecorderTests` | `BuildCsvIncludesThesisEvidenceFields` | PASS | CSV 表头包含 P1 计划要求的流程、时间、命令与施法字段。 |
| `DemoRunRecorderTests` | `TracksFlowTransitionsAndSpellCounts` | PASS | 记录训练、战斗转场和火/冰/盾施法次数。 |
| `DemoRunRecorderTests` | `ExportsDemoRunCsvToConfiguredDirectory` | PASS | 可导出 `demo_run_<timestamp>.csv`。 |
| `DemoRunRecorderTests` | `UnsafeOutputDirectoryFallsBackToExperimentResults` | PASS | 非安全导出目录会回退到 `ExperimentResults`。 |
| `DemoRunRecorderTests` | `AutoExportRunsOnceWhenResultAppears` | PASS | 出现战斗结果后自动导出且不会重复覆盖多次触发。 |

## 复跑步骤

1. 打开 Unity Hub，载入 `unity-spell-guard/`。
2. 打开菜单 `Window > General > Test Runner`。
3. 先执行 EditMode 全量测试，再执行 PlayMode 全量测试。
4. 将 Test Runner 截图保存到：

```text
Docs/ThesisAssets/screenshots/test_runner_editmode_full.png
Docs/ThesisAssets/screenshots/test_runner_playmode_full.png
```

5. 回填上方“自动化测试记录”表中的 Passed / Failed 数量。

## 论文引用建议

可在论文中表述为：

> 项目使用 Unity Test Framework 对输入运行时、移动控制、流程记录、场景生成和数据验证模块进行自动化测试。本轮针对手势移动闭环与演示记录器的 13 个 PlayMode 用例均通过，说明核心输入到记录导出的链路具备可回归验证能力。
