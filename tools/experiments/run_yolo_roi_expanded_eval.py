from __future__ import annotations

import csv
import time
from pathlib import Path

import cv2
from ultralytics import YOLO


MODEL = Path("yolo11n.pt")
VIDEO_ROOT = Path("unity-spell-guard") / "bridge" / "samples"
FRAME_ROOT = Path("unity-spell-guard") / "Assets" / "StreamingAssets" / "CustomGestureReferenceVideos"
OUT = Path("论文材料") / "实验补充数据" / "yolo_roi_expanded_eval.csv"
CONF = 0.25
MAX_VIDEO_FRAMES = 120
WINDOWS_PER_DIR = 3


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


def eval_frames(model: YOLO, frames: list, clip_id: str, source_kind: str) -> dict[str, object]:
    frame_count = 0
    detected_frames = 0
    conf_sum = 0.0
    area_ratio_sum = 0.0
    process_ms = []
    for frame in frames:
        if frame is None:
            continue
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
    avg_ms = sum(process_ms) / frame_count if frame_count else 0.0
    sorted_ms = sorted(process_ms)
    p95 = sorted_ms[min(len(sorted_ms) - 1, int(0.95 * len(sorted_ms)))] if sorted_ms else 0.0
    return {
        "clip_id": clip_id,
        "source_kind": source_kind,
        "frames": frame_count,
        "yolo_person_frames": detected_frames,
        "yolo_person_ratio": detected_frames / frame_count if frame_count else 0.0,
        "avg_yolo_confidence": conf_sum / detected_frames if detected_frames else 0.0,
        "avg_person_area_ratio": area_ratio_sum / detected_frames if detected_frames else 0.0,
        "avg_processing_ms": avg_ms,
        "p95_processing_ms": p95,
        "average_fps": 1000.0 / avg_ms if avg_ms > 0 else 0.0,
    }


def video_rows(model: YOLO) -> list[dict[str, object]]:
    rows = []
    videos = sorted(
        [p for ext in ("*.mp4", "*.avi", "*.mov") for p in VIDEO_ROOT.rglob(ext)]
    )
    # Exclude Unity package demo videos; VIDEO_ROOT is already project sample-only.
    for video in videos:
        cap = cv2.VideoCapture(str(video))
        frames = []
        while len(frames) < MAX_VIDEO_FRAMES:
            ok, frame = cap.read()
            if not ok:
                break
            frames.append(frame)
        cap.release()
        rows.append(eval_frames(model, frames, video.relative_to(VIDEO_ROOT).as_posix(), "video"))
    return rows


def frame_window_rows(model: YOLO) -> list[dict[str, object]]:
    rows = []
    for directory in sorted([p for p in FRAME_ROOT.iterdir() if p.is_dir()]):
        images = sorted(directory.glob("*.jpg"))
        if not images:
            continue
        n = len(images)
        if n <= 6:
            starts = [0]
        else:
            starts = sorted(set([0, max(0, n // 2 - 4), max(0, n - 8)]))[:WINDOWS_PER_DIR]
        for idx, start in enumerate(starts):
            window = images[start : min(n, start + 12)]
            frames = [cv2.imread(str(path)) for path in window]
            clip_id = f"{directory.name}_window{idx+1:02d}_{start+1:03d}-{start+len(window):03d}"
            rows.append(eval_frames(model, frames, clip_id, "frame_window"))
    return rows


def main() -> None:
    model = YOLO(str(MODEL))
    rows = video_rows(model) + frame_window_rows(model)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    with OUT.open("w", encoding="utf-8", newline="") as handle:
        fieldnames = list(rows[0].keys()) if rows else []
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)
    print(f"rows={len(rows)}")
    print(OUT)


if __name__ == "__main__":
    main()
