# Spell Guard Experiment Results

本目录用于归档论文实验 CSV 证据链。Unity 运行时由 `GesturePerformanceMonitor` 导出性能与手势统计，外部视觉链路由 Python benchmark 导出 YOLO / MediaPipe 对比数据。

## 命名约定

- `gesture_performance_mock_<timestamp>.csv`：Mock 输入模式性能记录。
- `gesture_performance_native_<timestamp>.csv`：Native MediaPipe 输入模式性能记录。
- `gesture_performance_external_<timestamp>.csv`：ExternalBridge / UDP 输入模式性能记录。
- `yolo_mediapipe_benchmark_<timestamp>.csv`：Python 侧 YOLO + MediaPipe benchmark 记录。

## Unity CSV 字段

`session_id, mode, source, elapsed_seconds, total_frames, average_fps, min_fps, average_frame_ms, p95_frame_ms, external_packets, avg_packet_interval_ms, avg_estimated_latency_ms, p95_estimated_latency_ms, static_commands, motion_commands, swipe_lr, swipe_rl, snap, point_to_fist, body_shift_left, body_shift_right`。

## 实验记录模板

| 项目 | 记录 |
|---|---|
| 实验日期 | 2026-05-12 |
| Unity 版本 | 2022.3.62f2c1 |
| 输入模式 | Mock / Native MediaPipe / ExternalBridge |
| 测试设备 | 待填写 CPU / GPU / 内存 |
| 摄像头 | 待填写设备型号或 ExternalBridge 视频源 |
| 运行时长 | 建议每组不少于 60 秒 |
| 使用的视频样本 | ExternalBridge / benchmark 运行时填写 |
