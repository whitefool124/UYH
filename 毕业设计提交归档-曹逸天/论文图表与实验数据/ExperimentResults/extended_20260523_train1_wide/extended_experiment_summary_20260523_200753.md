# Extended Experiment Summary

- Generated at: 2026-05-23T20:07:53
- Dataset clips: 40
- Total replay frames: 784
- Accepted mining rows: 113 / 160
- Detected frames: 2510 / 5744 (0.437)

## Dataset Scale

| metric | value |
| --- | --- |
| dataset_clips | 40 |
| total_frames | 784 |
| elapsed_seconds | 26.133 |
| mining_rows | 160 |
| accepted_rows | 113 |
| sampled_frames | 5744 |
| detected_frames | 2510 |
| detect_ratio | 0.437 |
| split_test | 20 |
| split_train | 20 |
| label_body_shift_left | 10 |
| label_body_shift_right | 10 |
| label_swipe_lr | 12 |
| label_swipe_rl | 8 |

## Repeated Performance Runs

| mode | runs | average_fps_mean | average_fps_std | p95_frame_ms_mean | min_fps_mean | avg_packet_interval_ms_mean | avg_estimated_latency_ms_mean | motion_commands_mean | static_commands_mean |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Mock | 9 | 59.501 | 0.32 | 16.927 | 59.079 | 0 | 0 | 15 | 20 |
| NativeMediapipe | 9 | 29.651 | 0.395 | 34.133 | 29.302 | 0 | 0 | 15 | 20 |
| ExternalBridge | 9 | 49.91 | 0.847 | 20.702 | 48.317 | 33.333 | 17.593 | 15 | 9 |

## Recognition Outcome

| gesture | attempts | correct | missed | false_match | success_rate |
| --- | --- | --- | --- | --- | --- |
| body_shift_left | 5 | 4 | 0 | 1 | 0.8 |
| body_shift_right | 5 | 4 | 0 | 1 | 0.8 |
| swipe_lr | 6 | 4 | 0 | 2 | 0.667 |
| swipe_rl | 4 | 3 | 0 | 1 | 0.75 |
| strict_passed_subset | 13 | 13 | 0 | 0 | 1 |

Note: This is an automated replay experiment based on mined Jester clips and local ExternalBridge regression artifacts. It supports thesis-scale evidence, but should be described as replay/offline validation rather than a long-term real-camera user study.