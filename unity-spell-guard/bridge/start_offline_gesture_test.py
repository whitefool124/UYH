import argparse
import subprocess
import sys
from pathlib import Path


def parse_args():
    parser = argparse.ArgumentParser(
        description="Launch the Unity UDP bridge against a prerecorded local video for offline gesture testing."
    )
    parser.add_argument("--video", required=True, help="Path to a local MP4/AVI/MOV video file.")
    parser.add_argument("--host", default="127.0.0.1", help="UDP destination host. Default: 127.0.0.1")
    parser.add_argument("--port", type=int, default=5053, help="UDP destination port. Default: 5053")
    parser.add_argument("--once", action="store_true", help="Play the video once instead of looping it.")
    parser.add_argument("--no-preview", action="store_true", help="Disable the OpenCV preview window.")
    parser.add_argument("--enable-pose", action="store_true", help="Also send pose landmarks for body-shift motion testing.")
    parser.add_argument("--enable-yolo", action="store_true", help="Enable YOLO person detection before MediaPipe.")
    parser.add_argument("--yolo-model", default="yolo11n.pt", help="YOLO model passed through to the bridge.")
    parser.add_argument("--yolo-conf", type=float, default=0.25, help="YOLO confidence passed through to the bridge.")
    return parser.parse_args()


def main():
    args = parse_args()
    bridge_path = Path(__file__).with_name("mediapipe_udp_bridge.py")
    video_path = Path(args.video).expanduser().resolve()

    if not video_path.exists() or not video_path.is_file():
        print(f"[offline-test] Video file not found: {video_path}", file=sys.stderr)
        return 1

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

    print("[offline-test] Starting offline gesture replay")
    print(f"[offline-test] Video: {video_path}")
    print(f"[offline-test] UDP target: {args.host}:{args.port}")
    print(f"[offline-test] Loop mode: {'off' if args.once else 'on'}")
    print(f"[offline-test] Preview: {'off' if args.no_preview else 'on'}")
    print(f"[offline-test] Command: {' '.join(command)}")

    try:
        completed = subprocess.run(command, check=False)
        return completed.returncode
    except KeyboardInterrupt:
        print("\n[offline-test] Interrupted by user.")
        return 130


if __name__ == "__main__":
    raise SystemExit(main())
