import argparse
import csv
import math
import time
from pathlib import Path


MODES = ("mediapipe", "yolo_mediapipe")


def parse_args():
    parser = argparse.ArgumentParser(
        description="Benchmark pure MediaPipe vs YOLO + MediaPipe on offline gesture videos."
    )
    parser.add_argument("--video", action="append", default=[], help="Video file to benchmark. Can be passed multiple times.")
    parser.add_argument("--video-dir", default="", help="Directory containing mp4/avi/mov videos.")
    parser.add_argument("--output", default="", help="CSV output path. Default: bridge/outputs/yolo_mediapipe_benchmark.csv")
    parser.add_argument("--max-frames", type=int, default=0, help="Limit frames per video/mode. 0 means all frames.")
    parser.add_argument("--yolo-model", default="yolo11n.pt")
    parser.add_argument("--yolo-conf", type=float, default=0.25)
    parser.add_argument("--yolo-padding", type=float, default=0.18)
    parser.add_argument("--min-detection-confidence", type=float, default=0.65)
    parser.add_argument("--min-tracking-confidence", type=float, default=0.55)
    return parser.parse_args()


def collect_videos(args):
    videos = [Path(video).expanduser().resolve() for video in args.video]
    if args.video_dir:
        video_dir = Path(args.video_dir).expanduser().resolve()
        for pattern in ("*.mp4", "*.avi", "*.mov"):
            videos.extend(sorted(video_dir.glob(pattern)))

    unique = []
    seen = set()
    for video in videos:
        if video in seen:
            continue
        seen.add(video)
        if video.exists() and video.is_file():
            unique.append(video)
        else:
            print(f"[benchmark] Skipping missing video: {video}")
    return unique


def resolve_output_path(path_text):
    if path_text:
        return Path(path_text).expanduser().resolve()
    return Path(__file__).with_name("outputs") / "yolo_mediapipe_benchmark.csv"


def benchmark_video(video_path, mode, args, yolo_detector):
    import cv2
    import mediapipe as mp

    from mediapipe_udp_bridge import (
        GestureStabilizer,
        classify_gesture,
        crop_frame,
        expand_box,
        remap_landmarks_to_frame,
    )

    capture = cv2.VideoCapture(str(video_path))
    if not capture.isOpened():
        raise RuntimeError(f"Failed to open video: {video_path}")

    stabilizer = GestureStabilizer()
    frame_count = 0
    hand_present_frames = 0
    confidence_sum = 0.0
    yolo_detected_frames = 0
    yolo_confidence_sum = 0.0
    processing_times_ms = []
    gesture_counts = {}

    mp_hands = mp.solutions.hands
    with mp_hands.Hands(
        max_num_hands=1,
        model_complexity=1,
        min_detection_confidence=args.min_detection_confidence,
        min_tracking_confidence=args.min_tracking_confidence,
    ) as hands:
        while True:
            if args.max_frames > 0 and frame_count >= args.max_frames:
                break

            success, frame = capture.read()
            if not success:
                break

            frame_start = time.perf_counter()
            frame_height, frame_width = frame.shape[:2]
            person_box = None
            yolo_confidence = 0.0

            if mode == "yolo_mediapipe" and yolo_detector is not None:
                detected_box, yolo_confidence = yolo_detector.detect(frame)
                if detected_box is not None:
                    yolo_detected_frames += 1
                    yolo_confidence_sum += yolo_confidence
                    person_box = expand_box(detected_box, frame_width, frame_height, args.yolo_padding)

            processing_box = person_box or (0, 0, frame_width, frame_height)
            processing_frame = crop_frame(frame, processing_box)
            rgb_processing_frame = cv2.cvtColor(processing_frame, cv2.COLOR_BGR2RGB)
            hand_results = hands.process(rgb_processing_frame)

            if hand_results.multi_hand_landmarks:
                landmarks = hand_results.multi_hand_landmarks[0].landmark
                frame_landmarks = remap_landmarks_to_frame(landmarks, processing_box, frame_width, frame_height)
                raw = classify_gesture(frame_landmarks)
                stable = stabilizer.push(raw)
                confidence = 0.95 if stable != "unknown" else 0.5
                hand_present_frames += 1
                confidence_sum += confidence
                gesture_counts[stable] = gesture_counts.get(stable, 0) + 1
            else:
                stabilizer.push("none")
                gesture_counts["none"] = gesture_counts.get("none", 0) + 1

            processing_times_ms.append((time.perf_counter() - frame_start) * 1000.0)
            frame_count += 1

    capture.release()

    elapsed_ms = sum(processing_times_ms)
    average_processing_ms = elapsed_ms / frame_count if frame_count else 0.0
    average_fps = 1000.0 / average_processing_ms if average_processing_ms > 0 else 0.0
    p95_processing_ms = percentile(processing_times_ms, 0.95)
    hand_present_ratio = hand_present_frames / frame_count if frame_count else 0.0
    yolo_detected_ratio = yolo_detected_frames / frame_count if frame_count else 0.0

    return {
        "video_name": video_path.name,
        "mode": mode,
        "frame_count": frame_count,
        "hand_present_frames": hand_present_frames,
        "hand_present_ratio": hand_present_ratio,
        "avg_confidence": confidence_sum / hand_present_frames if hand_present_frames else 0.0,
        "avg_processing_ms": average_processing_ms,
        "p95_processing_ms": p95_processing_ms,
        "average_fps": average_fps,
        "yolo_detected_frames": yolo_detected_frames,
        "yolo_detected_ratio": yolo_detected_ratio,
        "avg_yolo_confidence": yolo_confidence_sum / yolo_detected_frames if yolo_detected_frames else 0.0,
        "point_count": gesture_counts.get("point", 0),
        "fist_count": gesture_counts.get("fist", 0),
        "v_count": gesture_counts.get("v", 0),
        "open_palm_count": gesture_counts.get("openPalm", 0),
        "unknown_count": gesture_counts.get("unknown", 0),
        "none_count": gesture_counts.get("none", 0),
    }


def percentile(values, percentile_value):
    if not values:
        return 0.0
    sorted_values = sorted(values)
    index = int(math.ceil(percentile_value * len(sorted_values))) - 1
    index = max(0, min(index, len(sorted_values) - 1))
    return sorted_values[index]


def write_csv(output_path, rows):
    output_path.parent.mkdir(parents=True, exist_ok=True)
    fieldnames = [
        "video_name",
        "mode",
        "frame_count",
        "hand_present_frames",
        "hand_present_ratio",
        "avg_confidence",
        "avg_processing_ms",
        "p95_processing_ms",
        "average_fps",
        "yolo_detected_frames",
        "yolo_detected_ratio",
        "avg_yolo_confidence",
        "point_count",
        "fist_count",
        "v_count",
        "open_palm_count",
        "unknown_count",
        "none_count",
    ]
    with output_path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        for row in rows:
            writer.writerow(row)


def main():
    args = parse_args()
    videos = collect_videos(args)
    if not videos:
        print("[benchmark] No input videos found. Use --video or --video-dir.")
        return 1

    print(f"[benchmark] Videos: {len(videos)}")
    from mediapipe_udp_bridge import YoloPersonDetector

    yolo_detector = YoloPersonDetector(args.yolo_model, args.yolo_conf)
    rows = []
    for video in videos:
        for mode in MODES:
            print(f"[benchmark] {video.name} / {mode}")
            rows.append(benchmark_video(video, mode, args, yolo_detector))

    output_path = resolve_output_path(args.output)
    write_csv(output_path, rows)
    print(f"[benchmark] Wrote CSV: {output_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
