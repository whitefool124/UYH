# Bridge Benchmark Outputs

`offline_yolo_mediapipe_benchmark.py` writes CSV benchmark results here by default.

Example:

```bash
python bridge/offline_yolo_mediapipe_benchmark.py --video bridge/samples/ipn_real/ipn_229_g05_throw_left.mp4 --max-frames 120
```

The generated `yolo_mediapipe_benchmark.csv` compares pure MediaPipe and YOLO + MediaPipe on offline gesture videos.
