from __future__ import annotations

import csv
import time
from pathlib import Path

import cv2
from ultralytics import YOLO


ROOT = Path(".")
VIDEO_ROOT = Path("unity-spell-guard") / "bridge" / "samples"
MODEL = Path("yolo11n.pt")
OUT = Path("论文材料") / "实验补充数据" / "yolo_roi_strong_eval.csv"
MAX_FRAMES = 120
CONF = 0.25


def collect_videos() -> list[Path]:
    videos = []
    for pattern in ("*.mp4", "*.avi", "*.mov"):
        videos.extend(VIDEO_ROOT.rglob(pattern))
    return sorted(videos)


def person_boxes(result):
    boxes = []
    if result.boxes is None:
        return boxes
    for box in result.boxes:
        cls = int(box.cls[0].item()) if box.cls is not None else -1
        conf = float(box.conf[0].item()) if box.conf is not None else 0.0
        if cls == 0 and conf >= CONF:
            x1, y1, x2, y2 = [float(v) for v in box.xyxy[0].tolist()]
            boxes.append((x1, y1, x2, y2, conf))
    return boxes


def eval_video(model: YOLO, video: Path) -> dict[str, object]:
    cap = cv2.VideoCapture(str(video))
    if not cap.isOpened():
        raise RuntimeError(f"Cannot open {video}")
    frame_count = 0
    detected_frames = 0
    conf_sum = 0.0
    area_ratio_sum = 0.0
    process_ms = []
    while frame_count < MAX_FRAMES:
        ok, frame = cap.read()
        if not ok:
            break
        h, w = frame.shape[:2]
        start = time.perf_counter()
        result = model.predict(frame, conf=CONF, verbose=False)[0]
        process_ms.append((time.perf_counter() - start) * 1000.0)
        boxes = person_boxes(result)
        if boxes:
            best = max(boxes, key=lambda b: b[4])
            x1, y1, x2, y2, conf = best
            detected_frames += 1
            conf_sum += conf
            area_ratio_sum += max(0.0, (x2 - x1) * (y2 - y1)) / max(1.0, w * h)
        frame_count += 1
    cap.release()
    avg_ms = sum(process_ms) / frame_count if frame_count else 0.0
    sorted_ms = sorted(process_ms)
    p95 = sorted_ms[min(len(sorted_ms) - 1, int(0.95 * len(sorted_ms)))] if sorted_ms else 0.0
    return {
        "video_name": video.name,
        "relative_path": str(video.relative_to(VIDEO_ROOT)),
        "frames": frame_count,
        "yolo_person_frames": detected_frames,
        "yolo_person_ratio": detected_frames / frame_count if frame_count else 0.0,
        "avg_yolo_confidence": conf_sum / detected_frames if detected_frames else 0.0,
        "avg_person_area_ratio": area_ratio_sum / detected_frames if detected_frames else 0.0,
        "avg_processing_ms": avg_ms,
        "p95_processing_ms": p95,
        "average_fps": 1000.0 / avg_ms if avg_ms > 0 else 0.0,
    }


def main() -> None:
    videos = collect_videos()
    print(f"videos={len(videos)}")
    model = YOLO(str(MODEL))
    rows = []
    for video in videos:
        print(video)
        rows.append(eval_video(model, video))
    OUT.parent.mkdir(parents=True, exist_ok=True)
    with OUT.open("w", encoding="utf-8", newline="") as handle:
        fieldnames = list(rows[0].keys()) if rows else []
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)
    print(OUT)


if __name__ == "__main__":
    main()
