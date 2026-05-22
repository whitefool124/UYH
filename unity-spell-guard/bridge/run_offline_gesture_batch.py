import argparse
import csv
import json
import subprocess
import sys
from pathlib import Path


def parse_args():
    parser = argparse.ArgumentParser(description="Run multiple prerecorded videos through the offline gesture bridge.")
    parser.add_argument("--videos", nargs="+", required=True, help="Paths to local MP4/AVI/MOV files.")
    parser.add_argument("--host", default="127.0.0.1", help="UDP destination host. Default: 127.0.0.1")
    parser.add_argument("--port", type=int, default=5053, help="UDP destination port. Default: 5053")
    parser.add_argument("--once", action="store_true", help="Play each video once instead of looping it.")
    parser.add_argument("--no-preview", action="store_true", help="Disable the OpenCV preview window.")
    parser.add_argument("--enable-pose", action="store_true", help="Also send pose landmarks for body-shift motion testing.")
    parser.add_argument("--enable-yolo", action="store_true", help="Enable YOLO person detection before MediaPipe.")
    parser.add_argument("--yolo-model", default="yolo11n.pt", help="YOLO model passed through to the bridge.")
    parser.add_argument("--yolo-conf", type=float, default=0.25, help="YOLO confidence passed through to the bridge.")
    parser.add_argument("--report", default="", help="Optional CSV report path.")
    return parser.parse_args()


def run_video(bridge_path, args, video_path):
    command = [
        sys.executable,
        str(bridge_path),
        "--host",
        args.host,
        "--port",
        str(args.port),
        "--input-video",
        str(video_path),
    ]

    if not args.once:
        command.append("--loop-video")

    if not args.no_preview:
        command.append("--show-preview")

    if args.enable_pose:
        command.append("--enable-pose")

    if args.enable_yolo:
        command.extend([
            "--enable-yolo",
            "--yolo-model",
            args.yolo_model,
            "--yolo-conf",
            str(args.yolo_conf),
        ])

    print(f"[offline-batch] Video: {video_path.name}")
    completed = subprocess.run(command, check=False, capture_output=True, text=True)
    summary = {}
    for line in completed.stdout.splitlines():
        if line.startswith("[bridge-summary] "):
            try:
                summary = json.loads(line.removeprefix("[bridge-summary] "))
            except json.JSONDecodeError:
                summary = {}
    if completed.stdout:
        print(completed.stdout.rstrip())
    if completed.stderr:
        print(completed.stderr.rstrip(), file=sys.stderr)
    print(f"[offline-batch] Exit: {completed.returncode}")
    return completed.returncode, summary


def main():
    args = parse_args()
    bridge_path = Path(__file__).with_name("mediapipe_udp_bridge.py")
    video_paths = [Path(path).expanduser().resolve() for path in args.videos]

    for video_path in video_paths:
        if not video_path.exists() or not video_path.is_file():
            print(f"[offline-batch] Video file not found: {video_path}", file=sys.stderr)
            return 1

    print(f"[offline-batch] Running {len(video_paths)} videos")
    failures = 0
    rows = []
    for index, video_path in enumerate(video_paths, start=1):
        print(f"[offline-batch] {index}/{len(video_paths)}")
        code, summary = run_video(bridge_path, args, video_path)
        rows.append({
            "index": index,
            "video": str(video_path),
            "name": video_path.name,
            "exit_code": code,
            "success": code == 0,
            "frames": summary.get("frames", 0),
            "hand_frames": summary.get("handFrames", 0),
            "hand_ratio": round(summary.get("handRatio", 0.0), 4),
            "avg_confidence": round(summary.get("avgConfidence", 0.0), 4),
            "elapsed_seconds": round(summary.get("elapsedSeconds", 0.0), 3),
            "fps": round(summary.get("fps", 0.0), 2),
            "last_gesture": summary.get("lastGesture", "none"),
            "source": summary.get("source", ""),
        })
        failures += 1 if code != 0 else 0

    if args.report:
        report_path = Path(args.report).expanduser().resolve()
        report_path.parent.mkdir(parents=True, exist_ok=True)
        with report_path.open("w", newline="", encoding="utf-8") as handle:
            writer = csv.DictWriter(handle, fieldnames=["index", "name", "video", "exit_code", "success", "frames", "hand_frames", "hand_ratio", "avg_confidence", "elapsed_seconds", "fps", "last_gesture", "source"])
            writer.writeheader()
            writer.writerows(rows)
        print(f"[offline-batch] Report saved: {report_path}")

    print(f"[offline-batch] Completed with {failures} failure(s)")
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
