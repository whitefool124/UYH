import argparse
import json
import socket
import sys
import time
from pathlib import Path

import numpy as np


GESTURE_STABILITY_FRAMES = 3


def clamp01(value):
    return max(0.0, min(1.0, value))


def distance(point_a, point_b):
    dx = point_a.x - point_b.x
    dy = point_a.y - point_b.y
    return (dx * dx + dy * dy) ** 0.5


def is_finger_extended(landmarks, tip_index, pip_index, mcp_index):
    tip = landmarks[tip_index]
    pip = landmarks[pip_index]
    mcp = landmarks[mcp_index]
    return tip.y < pip.y - 0.015 and pip.y < mcp.y - 0.005


def classify_gesture(landmarks):
    if not landmarks:
        return "none"

    index_extended = is_finger_extended(landmarks, 8, 6, 5)
    middle_extended = is_finger_extended(landmarks, 12, 10, 9)
    ring_extended = is_finger_extended(landmarks, 16, 14, 13)
    pinky_extended = is_finger_extended(landmarks, 20, 18, 17)
    spread = distance(landmarks[8], landmarks[20])
    thumb_spread = distance(landmarks[4], landmarks[5])
    fingers_up = sum(1 for value in [index_extended, middle_extended, ring_extended, pinky_extended] if value)

    if not index_extended and not middle_extended and not ring_extended and not pinky_extended:
        return "fist"

    if index_extended and middle_extended and not ring_extended and not pinky_extended and spread > 0.12:
        return "v"

    if index_extended and not middle_extended and not ring_extended and not pinky_extended:
        return "point"

    if fingers_up >= 4 and thumb_spread > 0.09:
        return "openPalm"

    return "unknown"


def serialize_landmarks(landmarks):
    if not landmarks:
        return []

    return [
        {
            "x": landmark.x,
            "y": landmark.y,
            "z": landmark.z,
            "visibility": 1.0,
        }
        for landmark in landmarks
    ]


def serialize_pose_landmarks(landmarks):
    if not landmarks:
        return []

    return [
        {
            "x": landmark.x,
            "y": landmark.y,
            "z": landmark.z,
            "visibility": getattr(landmark, "visibility", 1.0),
        }
        for landmark in landmarks
    ]


def clamp_box(x1, y1, x2, y2, width, height):
    x1 = max(0, min(width - 1, x1))
    y1 = max(0, min(height - 1, y1))
    x2 = max(x1 + 1, min(width, x2))
    y2 = max(y1 + 1, min(height, y2))
    return x1, y1, x2, y2


def expand_box(box, frame_width, frame_height, padding_ratio):
    x1, y1, x2, y2 = box
    pad_x = int((x2 - x1) * padding_ratio)
    pad_y = int((y2 - y1) * padding_ratio)
    return clamp_box(x1 - pad_x, y1 - pad_y, x2 + pad_x, y2 + pad_y, frame_width, frame_height)


def crop_frame(frame, box):
    x1, y1, x2, y2 = box
    return frame[y1:y2, x1:x2]


def remap_landmarks_to_frame(landmarks, box, frame_width, frame_height):
    if not landmarks:
        return []

    x1, y1, x2, y2 = box
    crop_width = max(1, x2 - x1)
    crop_height = max(1, y2 - y1)
    remapped = []
    for landmark in landmarks:
        remapped.append(
            type("FrameLandmark", (), {
                "x": clamp01((x1 + landmark.x * crop_width) / frame_width),
                "y": clamp01((y1 + landmark.y * crop_height) / frame_height),
                "z": landmark.z,
                "visibility": getattr(landmark, "visibility", 1.0),
            })()
        )
    return remapped


def remap_point_to_frame(point, box, frame_width, frame_height):
    x1, y1, x2, y2 = box
    crop_width = max(1, x2 - x1)
    crop_height = max(1, y2 - y1)
    return clamp01((x1 + point.x * crop_width) / frame_width), clamp01((y1 + point.y * crop_height) / frame_height)


class YoloPersonDetector:
    def __init__(self, model_name, conf_threshold):
        from ultralytics import YOLO

        self.model = YOLO(model_name)
        self.conf_threshold = conf_threshold

    def detect(self, frame):
        results = self.model.predict(frame, conf=self.conf_threshold, classes=[0], verbose=False)
        if not results:
            return None, 0.0

        result = results[0]
        if result.boxes is None or len(result.boxes) == 0:
            return None, 0.0

        boxes = result.boxes.xyxy.cpu().numpy().astype(int)
        confidences = result.boxes.conf.cpu().numpy()
        best_index = int(np.argmax(confidences))
        return boxes[best_index].tolist(), float(confidences[best_index])


class GestureStabilizer:
    def __init__(self):
        self.candidate = "none"
        self.stable = "none"
        self.frames = 0

    def push(self, raw):
        if self.candidate != raw:
            self.candidate = raw
            self.frames = 1
        else:
            self.frames += 1

        if self.frames >= GESTURE_STABILITY_FRAMES:
            self.stable = raw

        return self.stable


def parse_args():
    parser = argparse.ArgumentParser(description="Send MediaPipe hand gestures to Unity over UDP.")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5053)
    parser.add_argument("--camera-index", type=int, default=0)
    parser.add_argument("--input-video", default="", help="Read frames from a local video file instead of the camera.")
    parser.add_argument("--loop-video", action="store_true", help="Loop the input video when it reaches the end.")
    parser.add_argument("--width", type=int, default=960)
    parser.add_argument("--height", type=int, default=540)
    parser.add_argument("--min-detection-confidence", type=float, default=0.65)
    parser.add_argument("--min-tracking-confidence", type=float, default=0.55)
    parser.add_argument("--enable-yolo", action="store_true")
    parser.add_argument("--enable-pose", action="store_true")
    parser.add_argument("--yolo-model", default="yolo11n.pt")
    parser.add_argument("--yolo-conf", type=float, default=0.25)
    parser.add_argument("--yolo-padding", type=float, default=0.18)
    parser.add_argument("--show-preview", action="store_true")
    return parser.parse_args()


def build_packet(hand_present, gesture, x=0.5, y=0.5, confidence=0.0, landmarks=None, pose_landmarks=None, source="mediapipeHandsBridge", tracking_confidence=None):
    tracking_confidence = confidence if tracking_confidence is None else tracking_confidence
    return {
        "handPresent": hand_present,
        "gesture": gesture,
        "x": clamp01(x),
        "y": clamp01(y),
        "confidence": clamp01(confidence),
        "trackingConfidence": clamp01(tracking_confidence),
        "timestamp": time.time(),
        "source": source,
        "pointer": {
            "x": clamp01(x),
            "y": clamp01(y),
            "z": 0.0,
            "visibility": 1.0 if hand_present else 0.0,
        },
        "handLandmarks": serialize_landmarks(landmarks),
        "poseLandmarks": serialize_pose_landmarks(pose_landmarks),
    }


def describe_bridge_source(base_source, input_video_path):
    if not input_video_path:
        return base_source

    return f"{base_source} | offline:{Path(input_video_path).name}"


def main():
    args = parse_args()

    try:
        import cv2
        import mediapipe as mp
    except ImportError as exc:
        print("缺少依赖，请先安装：pip install -r requirements.txt", file=sys.stderr)
        print(str(exc), file=sys.stderr)
        return 1

    socket_client = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    using_video_file = bool(args.input_video)
    capture_source = args.input_video if using_video_file else args.camera_index
    capture = cv2.VideoCapture(capture_source)
    if not using_video_file:
        capture.set(cv2.CAP_PROP_FRAME_WIDTH, args.width)
        capture.set(cv2.CAP_PROP_FRAME_HEIGHT, args.height)

    if not capture.isOpened():
        input_source_label = args.input_video if using_video_file else f"摄像头 {args.camera_index}"
        print(f"无法打开输入源：{input_source_label}", file=sys.stderr)
        return 1

    stabilizer = GestureStabilizer()
    mp_hands = mp.solutions.hands
    mp_pose = mp.solutions.pose
    mp_draw = mp.solutions.drawing_utils
    yolo_detector = None
    bridge_source = "mediapipeHandsBridge"

    if args.enable_yolo:
        try:
            yolo_detector = YoloPersonDetector(args.yolo_model, args.yolo_conf)
            bridge_source = "yoloMediapipeBridge"
        except Exception as exc:
            print(f"YOLO 初始化失败，回退到纯 MediaPipe：{exc}", file=sys.stderr)
            yolo_detector = None

    if args.enable_pose and bridge_source == "mediapipeHandsBridge":
        bridge_source = "mediapipePoseHandsBridge"

    packet_source = describe_bridge_source(bridge_source, args.input_video if using_video_file else "")

    with mp_hands.Hands(
        max_num_hands=1,
        model_complexity=1,
        min_detection_confidence=args.min_detection_confidence,
        min_tracking_confidence=args.min_tracking_confidence,
    ) as hands, mp_pose.Pose(
        model_complexity=1,
        min_detection_confidence=args.min_detection_confidence,
        min_tracking_confidence=args.min_tracking_confidence,
    ) as pose:
        while True:
            success, frame = capture.read()
            if not success:
                if using_video_file and args.loop_video:
                    capture.set(cv2.CAP_PROP_POS_FRAMES, 0)
                    continue

                time.sleep(0.01)
                break

            if not using_video_file:
                frame = cv2.flip(frame, 1)
            frame_height, frame_width = frame.shape[:2]
            person_box = None
            yolo_confidence = 0.0

            if yolo_detector is not None:
                detected_box, yolo_confidence = yolo_detector.detect(frame)
                if detected_box is not None:
                    person_box = expand_box(detected_box, frame_width, frame_height, args.yolo_padding)

            processing_box = person_box or (0, 0, frame_width, frame_height)
            processing_frame = crop_frame(frame, processing_box)
            rgb_processing_frame = cv2.cvtColor(processing_frame, cv2.COLOR_BGR2RGB)
            hand_results = hands.process(rgb_processing_frame)
            pose_results = pose.process(rgb_processing_frame) if args.enable_pose else None

            packet = build_packet(False, "none", source=packet_source)
            label = "none"
            pose_landmarks = []

            if pose_results and pose_results.pose_landmarks:
                pose_landmarks = remap_landmarks_to_frame(
                    pose_results.pose_landmarks.landmark,
                    processing_box,
                    frame_width,
                    frame_height,
                )

            if hand_results.multi_hand_landmarks:
                landmarks = hand_results.multi_hand_landmarks[0].landmark
                frame_landmarks = remap_landmarks_to_frame(landmarks, processing_box, frame_width, frame_height)
                raw = classify_gesture(frame_landmarks)
                stable = stabilizer.push(raw)
                pointer_x, pointer_y = remap_point_to_frame(landmarks[8], processing_box, frame_width, frame_height)
                tracking_confidence = max(0.95 if stable != "unknown" else 0.5, yolo_confidence)
                packet = build_packet(
                    True,
                    stable,
                    pointer_x,
                    pointer_y,
                    0.95 if stable != "unknown" else 0.5,
                    frame_landmarks,
                    pose_landmarks,
                    packet_source,
                    tracking_confidence=tracking_confidence,
                )
                label = stable

                if args.show_preview:
                    for hand_landmarks in hand_results.multi_hand_landmarks:
                        preview_landmarks = remap_landmarks_to_frame(hand_landmarks.landmark, processing_box, frame_width, frame_height)
                        for point in preview_landmarks:
                            cv2.circle(frame, (int(point.x * frame_width), int(point.y * frame_height)), 3, (0, 255, 180), -1)
                    cv2.circle(frame, (int(pointer_x * frame_width), int(pointer_y * frame_height)), 10, (0, 255, 255), 2)
            else:
                stabilizer.push("none")

            if args.show_preview and pose_landmarks:
                for point in pose_landmarks:
                    if point.visibility > 0.2:
                        cv2.circle(frame, (int(point.x * frame_width), int(point.y * frame_height)), 2, (255, 180, 0), -1)

            if args.show_preview and person_box is not None:
                x1, y1, x2, y2 = person_box
                cv2.rectangle(frame, (x1, y1), (x2, y2), (80, 160, 255), 2)
                cv2.putText(frame, f"YOLO {yolo_confidence:.2f}", (x1, max(24, y1 - 8)), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (80, 160, 255), 2)

            socket_client.sendto(json.dumps(packet).encode("utf-8"), (args.host, args.port))

            if args.show_preview:
                cv2.putText(frame, f"Gesture: {label}", (20, 34), cv2.FONT_HERSHEY_SIMPLEX, 1.0, (0, 255, 180), 2)
                cv2.putText(frame, f"UDP: {args.host}:{args.port}", (20, 68), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 220, 120), 2)
                cv2.putText(frame, f"Source: {packet_source}", (20, 98), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (180, 220, 255), 2)
                input_label = args.input_video if using_video_file else f"camera:{args.camera_index}"
                cv2.putText(frame, f"Input: {input_label}", (20, 128), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (180, 255, 180), 2)
                cv2.imshow("Spell Guard MediaPipe Bridge", frame)

                key = cv2.waitKey(1) & 0xFF
                if key == 27 or key == ord("q"):
                    break

    capture.release()
    if args.show_preview:
        cv2.destroyAllWindows()
    socket_client.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
