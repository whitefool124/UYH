# Thesis Assets

本目录用于统一归档论文、答辩和演示材料中使用的图片、结构图与实验表格。所有素材应使用可读文件名，并在本 README 中登记“素材用途 + 论文位置 + 生成方式”，避免截图散落在桌面或临时目录。

## 目录结构

```text
Docs/ThesisAssets/
  screenshots/        # Unity / Test Runner / HUD / ExternalBridge 截图
  diagrams/           # 系统架构图、输入链路图、流程图
  experiment_tables/  # 从 CSV 整理出的论文表格或图表源文件
```

## 必备截图清单

| 文件名 | 状态 | 论文位置 | 内容要求 | 生成方式 |
|---|---|---|---|---|
| `screenshots/start_menu.png` | 待补 | 第 5 章 系统界面实现 | 开始菜单主界面，能看到 Combat / Training / Settings 等入口。 | 打开 `SpellGuardStart.unity` 后截图。 |
| `screenshots/settings_input_mode.png` | 待补 | 第 5 章 输入模式配置 | 设置页输入模式选项，显示 Mock / Native MediaPipe / ExternalBridge。 | Start Scene 设置页截图。 |
| `screenshots/training_flow.png` | 待补 | 第 5 章 教学与训练流程 | 训练场目标、手势提示和训练完成条件。 | 进入 Training 后截图。 |
| `screenshots/combat_scene.png` | 待补 | 第 5 章 战斗原型实现 | 第一人称战斗场景、敌人、HUD 与施法反馈。 | 进入 Combat 后截图。 |
| `screenshots/debug_hud_gesture_status.png` | 待补 | 第 5 / 6 章 调试与验证 | HUD 中当前输入模式、手势状态、性能监控字段。 | 开启 DebugHud 后截图。 |
| `screenshots/external_bridge_status.png` | 待补 | 第 6 章 外部视觉链路实验 | ExternalBridge 状态、source、packet、latency、last motion event。 | 切换 ExternalBridge 后截图。 |
| `screenshots/performance_monitor_export.png` | 待补 | 第 6 章 性能记录 | `ExperimentResults/` 中性能 CSV 或 Unity 导出提示。 | F9 导出后截图。 |
| `screenshots/demo_run_recorder_csv.png` | 待补 | 第 6 章 功能测试证据 | `demo_run_<timestamp>.csv` 字段与一条演示流程记录。 | 完整演示一轮后截图。 |
| `screenshots/test_runner_editmode_full.png` | 待补 | 第 6 章 自动化测试 | Unity Test Runner EditMode 结果。 | Test Runner 执行 EditMode 后截图。 |
| `screenshots/test_runner_playmode_full.png` | 待补 | 第 6 章 自动化测试 | Unity Test Runner PlayMode 结果。 | Test Runner 执行 PlayMode 后截图。 |

## 图表与表格清单

| 文件名 | 状态 | 论文位置 | 内容要求 | 数据来源 |
|---|---|---|---|---|
| `diagrams/runtime_architecture.png` | 待补 | 第 4 章 系统设计 | Unity 运行时模块关系：输入源、路由、识别、玩法、HUD、记录器。 | `Docs/符印守卫_Unity项目技术文档.md` |
| `diagrams/gesture_pipeline.png` | 待补 | 第 4 / 5 章 手势识别链路 | Mock / Native / ExternalBridge 三输入源与 GestureFrame / GestureCommand 抽象。 | `Assets/Scripts/Input/` |
| `diagrams/demo_flow.png` | 待补 | 第 5 章 流程实现 | Start Menu → Tutorial / Training → Combat → Results → Restart/Menu。 | `SpellGuardFlowController` |
| `experiment_tables/performance_summary.xlsx` | 待补 | 第 6 章 性能分析 | Mock / Native / ExternalBridge FPS、延迟、命令数汇总。 | `ExperimentResults/gesture_performance_*.csv` |
| `experiment_tables/demo_run_summary.xlsx` | 待补 | 第 6 章 功能测试 | 完整演示流程节点和施法次数统计。 | `ExperimentResults/demo_run_*.csv` |
| `experiment_tables/yolo_mediapipe_benchmark.xlsx` | 待补 | 第 6 章 外部视觉实验 | YOLO + MediaPipe benchmark 汇总。 | `ExperimentResults/yolo_mediapipe_benchmark_*.csv` |

## 命名规范

- 使用英文小写和下划线：`external_bridge_status.png`。
- 截图优先保存为 PNG；表格源文件可使用 XLSX / CSV。
- 文件名应表达用途，不使用 `screenshot1.png`、`new.png` 等临时名称。
- 若同一素材有多版，使用日期或版本后缀：`combat_scene_20260512.png`。

## 复核要求

素材进入论文前至少检查：

1. 是否与当前 Unity 实现一致；
2. 是否能对应到论文具体章节；
3. 是否没有暴露本机隐私路径、账号信息或无关窗口；
4. 是否足够清晰，关键文字可读；
5. 是否已在本 README 中登记。
