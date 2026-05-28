# 第 6 章可补充的数据图表与结论

## 可新增图表

- 图 6-x 三种输入模式实时性能对比：`experiment_charts/input_mode_performance.svg`
- 图 6-x YOLO+MediaPipe 外部桥接有效检出分布：`experiment_charts/yolo_bridge_detection_distribution.svg`
- 图 6-x Jester 样本筛选拒绝原因统计：`experiment_charts/dataset_rejection_reasons.svg`
- 图 6-x 外部模板回放动态手势识别 F1：`experiment_charts/gesture_replay_f1.svg`
- 图 6-x 手指级变化与掌心轨迹比例示例：`experiment_charts/finger_to_palm_ratio_examples.svg`

## 可直接写入论文的结论文字

1. 数据集筛选方面，本次补充实验从 Jester 样本中挖掘 300 条记录，接受 215 条，接受率为 0.7167；AVI-200 扩展回放共采样 9600 帧，其中 MediaPipe 有效检出 7359 帧，有效帧比例为 0.7666。这说明离线回放实验具备一定样本规模，但仍受到手部检出连续性和动作标签质量影响。

2. 动态识别方面，外部模板回放验证共覆盖 16 个测试 clip，正确 16 个，Micro-F1 为 1.0。该结果说明在严格筛选和模板一致的离线回放子集上，基于时间窗与阈值的轨迹识别能够稳定工作；但该结论不等同于真实摄像头长期用户测试。

3. 性能方面，三种输入模式共统计 27 条运行记录。Mock 平均 FPS 为 59.501，Native MediaPipe 平均 FPS 为 29.65，ExternalBridge 平均 FPS 为 49.909，ExternalBridge 平均包间隔为 33.333 ms、估算链路延迟为 17.593 ms。结果表明外部桥接链路可以进入统一性能监控，并具备演示级实时性。

4. YOLO 外部桥接方面，18 个参考视频均完成处理，其中 4 个视频检出手部关键点，14 个视频未检出，平均 hand_ratio 为 0.1124，平均 FPS 为 7.5617。因此，YOLO+MediaPipe 当前应表述为外部桥接可行性和后续优化方向，不能表述为已完成高精度 YOLO 动态手势分类器。

5. 手指级特征方面，双指外滑、响指、模拟夹动等动作更依赖指尖距离变化、峰值速度和振荡次数，不能完全依靠掌心位移判断。该结果支持第 4 章中将动态手势分为掌心轨迹、姿态转换和手指级特征序列三类处理的设计。
