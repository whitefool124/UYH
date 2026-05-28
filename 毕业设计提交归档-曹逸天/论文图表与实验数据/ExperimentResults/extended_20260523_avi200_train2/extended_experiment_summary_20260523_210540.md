# Extended Experiment Summary

- Generated at: 2026-05-23T21:05:40
- Dataset clips: 48
- Total replay frames: 1811
- Accepted mining rows: 192 / 200
- Detected frames: 7359 / 9600 (0.767)

## Dataset Scale

| metric | value |
| --- | --- |
| dataset_clips | 48 |
| total_frames | 1811 |
| elapsed_seconds | 60.367 |
| mining_rows | 200 |
| accepted_rows | 192 |
| sampled_frames | 9600 |
| detected_frames | 7359 |
| detect_ratio | 0.767 |
| split_test | 24 |
| split_train | 24 |
| label_body_shift_left | 4 |
| label_body_shift_right | 24 |
| label_swipe_lr | 20 |

## Repeated Performance Runs

| mode | runs | average_fps_mean | average_fps_std | p95_frame_ms_mean | min_fps_mean | avg_packet_interval_ms_mean | avg_estimated_latency_ms_mean | motion_commands_mean | static_commands_mean |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Mock | 9 | 59.501 | 0.32 | 16.927 | 59.079 | 0 | 0 | 13 | 24 |
| NativeMediapipe | 9 | 29.65 | 0.395 | 34.133 | 29.302 | 0 | 0 | 13 | 24 |
| ExternalBridge | 9 | 49.909 | 0.847 | 20.702 | 48.317 | 33.333 | 17.593 | 13 | 18 |

## Recognition Outcome

| gesture | attempts | correct | missed | false_match | success_rate |
| --- | --- | --- | --- | --- | --- |
| body_shift_left | 4 | 3 | 0 | 1 | 0.75 |
| body_shift_right | 5 | 4 | 0 | 1 | 0.8 |
| swipe_lr | 5 | 4 | 0 | 1 | 0.8 |
| swipe_rl | 2 | 2 | 0 | 0 | 1 |
| strict_passed_subset | 13 | 13 | 0 | 0 | 1 |

Note: This is an automated replay experiment based on mined Jester clips and local ExternalBridge regression artifacts. It supports thesis-scale evidence, but should be described as replay/offline validation rather than a long-term real-camera user study.