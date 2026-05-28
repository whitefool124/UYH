# Extended Experiment Summary

- Generated at: 2026-05-23T19:56:50
- Dataset clips: 39
- Total replay frames: 801
- Accepted mining rows: 113 / 160
- Detected frames: 2510 / 5744 (0.437)

## Dataset Scale

| metric | value |
| --- | --- |
| dataset_clips | 39 |
| total_frames | 801 |
| elapsed_seconds | 26.7 |
| mining_rows | 160 |
| accepted_rows | 113 |
| sampled_frames | 5744 |
| detected_frames | 2510 |
| detect_ratio | 0.437 |
| split_test | 13 |
| split_train | 26 |
| label_body_shift_left | 9 |
| label_body_shift_right | 12 |
| label_swipe_lr | 12 |
| label_swipe_rl | 6 |

## Repeated Performance Runs

| mode | runs | average_fps_mean | average_fps_std | p95_frame_ms_mean | min_fps_mean | avg_packet_interval_ms_mean | avg_estimated_latency_ms_mean | motion_commands_mean | static_commands_mean |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Mock | 9 | 59.502 | 0.32 | 16.927 | 59.079 | 0 | 0 | 13 | 26 |
| NativeMediapipe | 9 | 29.651 | 0.395 | 34.133 | 29.302 | 0 | 0 | 13 | 26 |
| ExternalBridge | 9 | 49.91 | 0.847 | 20.702 | 48.317 | 33.333 | 17.593 | 13 | 11 |

## Recognition Outcome

| gesture | attempts | correct | missed | false_match | success_rate |
| --- | --- | --- | --- | --- | --- |
| body_shift_left | 4 | 3 | 0 | 1 | 0.75 |
| body_shift_right | 5 | 4 | 0 | 1 | 0.8 |
| swipe_lr | 5 | 4 | 0 | 1 | 0.8 |
| swipe_rl | 2 | 2 | 0 | 0 | 1 |
| strict_passed_subset | 13 | 13 | 0 | 0 | 1 |

Note: This is an automated replay experiment based on mined Jester clips and local ExternalBridge regression artifacts. It supports thesis-scale evidence, but should be described as replay/offline validation rather than a long-term real-camera user study.