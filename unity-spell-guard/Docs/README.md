# Spell Guard Documentation Index

This folder keeps the graduation-project planning, implementation notes, and demo-delivery references for the Unity version of **Spell Guard**.

## Start here

- `符印守卫_Unity项目技术文档.md` - technical architecture and runtime notes.
- `Day1_项目收口与范围冻结.md` - current delivery scope and P0/P1/P2 boundaries.
- `20天冲刺任务表.md` - day-by-day delivery checklist.
- `Day2_演示主线设计.md` - 5-minute defense-demo route.
- `Day3_基线版本检查.md` - baseline status and known risks.
- `Day4_教学关结构搭建.md` - training/tutorial loop structure.
- `论文风险补全实验规划.md` - YOLO evidence and Unity performance metrics plan for thesis risk closure.
- `论文导向技术栈补全开发计划.md` - P0/P1/P2 engineering backlog for making the game project thesis-ready.
- `ExperimentResults/test_results.md` - Unity Test Runner and focused PlayMode result archive for thesis evidence.
- `ThesisAssets/README.md` - screenshot, diagram, and experiment-table archive checklist for thesis/presentation materials.

## Design references

- `符印守卫_新版游戏策划案_V3.md` - latest game design direction.
- `五关关卡执行表.md` - episode/level execution plan.
- `法术与敌人设计方案.md` - spell and enemy role design.
- `符印守卫_手势设计规范.md` - gesture design rules.
- `符印守卫_手势词典_V1.md` - gesture dictionary.
- `移动手势映射表.md` and `移动输入规则细则表.md` - movement input mapping.

## Art and presentation

- `美术资产需求表.md` - art asset priorities and current substitutes.
- `美术风格规范_全息试炼空间.md` - holographic trial-space style guide.
- `答辩版_vs_比赛版功能边界表.md` - defense version vs competition version scope.
- `ThesisAssets/` - canonical location for thesis screenshots, diagrams, and derived experiment tables.

## Current delivery status

The Unity project now has a stable defense-demo loop:

1. Main menu
2. Tutorial / training
3. Training completion gate
4. Combat run
5. Victory / defeat result screen
6. Restart or return to menu

The default input mode is `Mock` for safe local and defense playback. Native MediaPipe remains available through the runtime input switch, with fallback safeguards so camera or native initialization issues do not block the demo path.

## New beginner docs

- `手势技术深挖_数学与代码层.md` - deeper math and code-level breakdown of MediaPipe, YOLO, DTW, and the gesture pipeline.
- `手势技术原理详解.md` - MediaPipe, YOLO, DTW, and the full gesture pipeline explained for beginners.
- `手势识别与项目框架入门说明.md` - project structure and gesture-to-gameplay overview for beginners.
