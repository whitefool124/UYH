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
| `screenshots/start_menu.png` | 已归档 | 第 5 章 系统界面实现 | 开始菜单主界面，能看到 Combat / Training / Settings 等入口。 | 2026-05-13 通过 Unity MCP Scene View 截图归档。 |
| `screenshots/start_menu_latest.png` | 已归档 | 第 5 章 系统界面实现 | 当前版本开始界面，展示系统主入口和界面风格。 | 2026-05-23 从 `运行截图/开始界面.png` 归档。 |
| `screenshots/gameplay_instruction.png` | 已归档 | 第 5 章 教学与玩法说明 | 玩法说明界面，展示手势操作说明和训练前引导。 | 2026-05-23 从 `运行截图/玩法说明界面.png` 归档。 |
| `screenshots/combat_gameplay.png` | 已归档 | 第 5 章 战斗原型实现 | 实际游玩界面，展示第一人称战斗、HUD 和交互反馈。 | 2026-05-23 从 `运行截图/实际游玩界面.png` 归档。 |
| `screenshots/camera_calibration.png` | 已归档 | 第 5 章 摄像头与输入校准 | 摄像头校准界面，展示 Native 输入前的摄像头校准与状态检查。 | 2026-05-24 从 `运行截图/摄像头校准界面.png` 归档。 |
| `screenshots/developer_lab.png` | 已归档 | 第 5 / 6 章 调试与验证 | 开发者实验室界面，展示输入模式、调试入口或实验辅助功能。 | 2026-05-24 从 `运行截图/开发者实验室.png` 归档。 |
| `screenshots/custom_gesture_validation.png` | 已归档 | 第 5 / 6 章 自定义手势验证 | 自定义手势验证界面，展示模板验证、手势状态和调试反馈。 | 2026-05-24 从 `运行截图/自定义手势验证.png` 归档。 |
| `screenshots/settings_input_mode.png` | 可选待补 | 第 5 章 输入模式配置 | 设置页输入模式选项，显示 Mock / Native MediaPipe / ExternalBridge。 | 若已有开发者实验室截图能覆盖，可不单独补。 |
| `screenshots/training_flow.png` | 可选待补 | 第 5 章 教学与训练流程 | 训练场目标、手势提示和训练完成条件。 | 若玩法说明和自定义验证截图能覆盖，可不单独补。 |
| `screenshots/debug_hud_gesture_status.png` | 可选待补 | 第 5 / 6 章 调试与验证 | HUD 中当前输入模式、手势状态、性能监控字段。 | 当前可用 `combat_gameplay.png` 和 `developer_lab.png` 部分覆盖。 |
| `screenshots/external_bridge_status.png` | 待补 | 第 6 章 外部视觉链路实验 | ExternalBridge 状态、source、packet、latency、last motion event。 | 切换 ExternalBridge 后截图。 |
| `screenshots/performance_monitor_export.png` | 待补 | 第 6 章 性能记录 | `ExperimentResults/` 中性能 CSV 或 Unity 导出提示。 | F9 导出后截图。 |
| `screenshots/demo_run_recorder_csv.png` | 待补 | 第 6 章 功能测试证据 | `demo_run_<timestamp>.csv` 字段与一条演示流程记录。 | 完整演示一轮后截图。 |
| `screenshots/test_runner_editmode_full.png` | 待补 | 第 6 章 自动化测试 | Unity Test Runner EditMode 结果。 | Test Runner 执行 EditMode 后截图。 |
| `screenshots/test_runner_playmode_full.png` | 待补 | 第 6 章 自动化测试 | Unity Test Runner PlayMode 结果。 | Test Runner 执行 PlayMode 后截图。 |

## 图表与表格清单

| 文件名 | 状态 | 论文位置 | 内容要求 | 数据来源 |
|---|---|---|---|---|
| `diagrams/system_architecture.svg` | 已生成 | 第 3 / 4 章 总体架构 | 从视觉输入到 Unity 游戏反馈的分层闭环。 | `tools/generate_thesis_diagrams.py` |
| `diagrams/input_pipeline.svg` | 已生成 | 第 4 / 5 章 输入链路 | Mock / Native MediaPipe / ExternalBridge 三输入源与 GestureFrame / GestureCommand 抽象。 | `tools/generate_thesis_diagrams.py` |
| `diagrams/motion_recognition_flow.svg` | 已生成 | 第 4 章 动态手势识别方法 | 历史窗口、特征计算、规则判断、冷却过滤、命令生成。 | `tools/generate_thesis_diagrams.py` |
| `diagrams/unity_module_structure.svg` | 已生成 | 第 5 章 Unity 系统实现 | Core、Input、Player、Combat、UI、Diagnostics、Editor/Tests 模块结构。 | `tools/generate_thesis_diagrams.py` |
| `diagrams/experiment_pipeline.svg` | 已生成 | 第 6 章 扩展自动回放实验 | 视频归档、抽帧、MediaPipe 挖掘、回归回放、论文表格证据链。 | `tools/generate_thesis_diagrams.py` |
| `diagrams/icon_legend.svg` | 已生成 | 图件统一风格 | 摄像头、Unity、UDP、CSV 统计图标与颜色说明。 | `tools/generate_thesis_diagrams.py` |
| `experiment_tables/performance_summary.xlsx` | 待补 | 第 6 章 性能分析 | Mock / Native / ExternalBridge FPS、延迟、命令数汇总。 | `ExperimentResults/extended_20260523_avi200_train2/*.csv` |
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
