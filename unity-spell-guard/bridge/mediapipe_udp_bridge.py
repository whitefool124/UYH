import argparse
import json
import socket
import sys
import time


GESTURE_STABILITY_FRAMES = 3


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
    parser.add_argument("--width", type=int, default=960)
    parser.add_argument("--height", type=int, default=540)
    parser.add_argument("--min-detection-confidence", type=float, default=0.65)
    parser.add_argument("--min-tracking-confidence", type=float, default=0.55)
    parser.add_argument("--show-preview", action="store_true")
    return parser.parse_args()


def build_packet(hand_present, gesture, x=0.5, y=0.5, confidence=0.0, landmarks=None):
    return {
        "handPresent": hand_present,
        "gesture": gesture,
        "x": max(0.0, min(1.0, x)),
        "y": max(0.0, min(1.0, y)),
        "confidence": max(0.0, min(1.0, confidence)),
        "trackingConfidence": max(0.0, min(1.0, confidence)),
        "timestamp": time.time(),
        "source": "mediapipeHandsBridge",
        "pointer": {
            "x": max(0.0, min(1.0, x)),
            "y": max(0.0, min(1.0, y)),
            "z": 0.0,
            "visibility": 1.0 if hand_present else 0.0,
        },
        "handLandmarks": serialize_landmarks(landmarks),
        "poseLandmarks": [],
    }


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
    capture = cv2.VideoCapture(args.camera_index)
    capture.set(cv2.CAP_PROP_FRAME_WIDTH, args.width)
    capture.set(cv2.CAP_PROP_FRAME_HEIGHT, args.height)

    if not capture.isOpened():
        print("无法打开摄像头", file=sys.stderr)
        return 1

    stabilizer = GestureStabilizer()
    mp_hands = mp.solutions.hands
    mp_draw = mp.solutions.drawing_utils

    with mp_hands.Hands(
        max_num_hands=1,
        model_complexity=1,
        min_detection_confidence=args.min_detection_confidence,
        min_tracking_confidence=args.min_tracking_confidence,
    ) as hands:
        while True:
            success, frame = capture.read()
            if not success:
                time.sleep(0.01)
                continue

            frame = cv2.flip(frame, 1)
            rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            results = hands.process(rgb_frame)

            packet = build_packet(False, "none")
            label = "none"

            if results.multi_hand_landmarks:
                landmarks = results.multi_hand_landmarks[0].landmark
                raw = classify_gesture(landmarks)
                stable = stabilizer.push(raw)
                pointer = landmarks[8]
                packet = build_packet(True, stable, pointer.x, pointer.y, 0.95 if stable != "unknown" else 0.5, landmarks)
                label = stable

                if args.show_preview:
                    for hand_landmarks in results.multi_hand_landmarks:
                        mp_draw.draw_landmarks(frame, hand_landmarks, mp_hands.HAND_CONNECTIONS)
                    cv2.circle(frame, (int(pointer.x * frame.shape[1]), int(pointer.y * frame.shape[0])), 10, (0, 255, 255), 2)
            else:
                stabilizer.push("none")

            socket_client.sendto(json.dumps(packet).encode("utf-8"), (args.host, args.port))

            if args.show_preview:
                cv2.putText(frame, f"Gesture: {label}", (20, 34), cv2.FONT_HERSHEY_SIMPLEX, 1.0, (0, 255, 180), 2)
                cv2.putText(frame, f"UDP: {args.host}:{args.port}", (20, 68), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 220, 120), 2)
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
