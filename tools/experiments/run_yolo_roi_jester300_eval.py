from __future__ import annotations

import csv
import time
from pathlib import Path

import cv2
from ultralytics import YOLO


MODEL = Path("yolo11n.pt")
JESTER_ROOT = Path("build-temp") / "jester_clip_sample_300" / "20bn-jester-v1"
OUT = Path("论文材料") / "实验补充数据" / "yolo_roi_jester300_eval.csv"
CONF = 0.25
MAX_FRAMES_PER_CLIP = 8


def person_boxes(result):
    if result.boxes is None:
        return []
    boxes = []
    for box in result.boxes:
        cls = int(box.cls[0].item()) if box.cls is not None else -1
        conf = float(box.conf[0].item()) if box.conf is not None else 0.0
        if cls == 0 and conf >= CONF:
            x1, y1, x2, y2 = [float(v) for v in box.xyxy[0].tolist()]
            boxes.append((x1, y1, x2, y2, conf))
    return boxes


def sample_images(directory: Path) -> list[Path]:
    images = sorted(directory.glob("*.jpg"))
    if len(images) <= MAX_FRAMES_PER_CLIP:
        return images
    if MAX_FRAMES_PER_CLIP == 1:
        return [images[len(images) // 2]]
    indexes = [
        round(i * (len(images) - 1) / (MAX_FRAMES_PER_CLIP - 1))
        for i in range(MAX_FRAMES_PER_CLIP)
    ]
    return [images[i] for i in indexes]


def eval_clip(model: YOLO, directory: Path) -> dict[str, object]:
    image_paths = sample_images(directory)
    frame_count = 0
    detected_frames = 0
    conf_sum = 0.0
    area_ratio_sum = 0.0
    process_ms = []
    for image_path in image_paths:
        frame = cv2.imread(str(image_path))
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
        "clip_id": directory.name,
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
    clips = sorted([p for p in JESTER_ROOT.iterdir() if p.is_dir()])
    print(f"clips={len(clips)}")
    model = YOLO(str(MODEL))
    rows = []
    for idx, clip in enumerate(clips, start=1):
        if idx % 25 == 0:
            print(f"{idx}/{len(clips)}")
        rows.append(eval_clip(model, clip))
    OUT.parent.mkdir(parents=True, exist_ok=True)
    with OUT.open("w", encoding="utf-8", newline="") as handle:
        fieldnames = list(rows[0].keys()) if rows else []
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)
    print(OUT)


if __name__ == "__main__":
    main()
