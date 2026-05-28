# Extended Experiment Run Notes

更新时间：2026-05-23

本轮目标是将论文实验从“最小闭环”扩展到更像完整毕设的自动回放实验。实验均基于本地 Jester 抽样帧、MediaPipe 手部关键点挖掘结果和 ExternalBridge 回归结果，不应描述为长期真实摄像头用户实验。

## 已生成结果目录

| 目录 | 数据来源 | 用途建议 |
|---|---|---|
| `extended_20260523_avi200_train2/` | `_training_unpack/videos/*.tgz` 中抽取的 200 个 AVI 视频，经抽帧后挖掘 | 推荐作为第 6 章主规模实验结果使用。该组包含 200 个候选视频、192 条 accepted mining rows、9600 采样帧、7359 检出帧、48 条最终评估 clip、9 轮三模式性能结果。 |
| `extended_20260523_train2/` | `jester_mined_motion_subset_160_buckets_train2.json` | 作为第 6 章主实验结果使用。该组包含 160 个候选目录、113 条 accepted mining rows、48 条最终评估 clip、9 轮三模式性能结果。 |
| `extended_20260523/` | `jester_mined_motion_subset_160_buckets_train2_passed13.json` | 作为严格通过子集使用。该组包含 39 条 clip，识别结果更稳，可用于说明严格筛选后 13 条 validation 全部通过。 |
| `extended_20260523_train1_wide/` | `jester_mined_motion_subset_160_buckets_train1.json` | 作为类别覆盖补充使用。该组包含 20 个动作桶标签、40 条 clip，可用于说明挖掘过程覆盖了更多方向/幅度组合。 |

## 主实验摘要

推荐正文优先采用 `extended_20260523_avi200_train2/extended_experiment_summary_*.md` 中的数据：

- 候选视频：200
- accepted mining rows：192
- sampled frames：9600
- detected frames：7359
- detect ratio：0.767
- 最终评估 clips：48
- 性能重复轮数：Mock / Native MediaPipe / ExternalBridge 各 9 轮，共 27 行性能记录
- 严格通过子集：13 / 13

三输入模式 9 轮性能均值：

| mode | runs | average_fps_mean | p95_frame_ms_mean | avg_estimated_latency_ms_mean | motion_commands_mean |
|---|---:|---:|---:|---:|---:|
| Mock | 9 | 59.501 | 16.927 | 0 | 13 |
| NativeMediapipe | 9 | 29.650 | 34.133 | 0 | 13 |
| ExternalBridge | 9 | 49.909 | 20.702 | 17.593 | 13 |

识别结果摘要：

| gesture | attempts | correct | missed | false_match | success_rate |
|---|---:|---:|---:|---:|---:|
| body_shift_left | 4 | 3 | 0 | 1 | 0.750 |
| body_shift_right | 5 | 4 | 0 | 1 | 0.800 |
| swipe_lr | 5 | 4 | 0 | 1 | 0.800 |
| swipe_rl | 2 | 2 | 0 | 0 | 1.000 |
| strict_passed_subset | 13 | 13 | 0 | 0 | 1.000 |

## 重要解释边界

1. 这组实验把候选规模扩展到了 200 个视频，已经达到最初 40 条挖掘报告的 5 倍。
2. 由于 MediaPipe 检出、位移阈值、同手标签和动作桶筛选，最终进入 Unity 风格模板评估的 clip 数为 48，而不是 200。
3. 论文中应写作“离线回放/训练集自动实验”，不要写成“大规模用户实验”或“真实摄像头长时间实验”。
4. 本轮已新增 `extract_avi_clip_frames.py`，能够从本地 `videos01.tgz`、`videos02.tgz` 等 AVI 归档中抽帧。若后续需要更大最终评估 clip 数，应继续增加抽样视频数，并调整每标签 train/test 配额或标签合并规则。
