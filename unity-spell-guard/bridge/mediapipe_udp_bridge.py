import argparse
import json
import socket
import sys
import threading
import time
from pathlib import Path

import numpy as np


GESTURE_STABILITY_FRAMES = 3
SWIPE_WINDOW_SECONDS = 0.55
SWIPE_MIN_DISTANCE = 0.12
SWIPE_MIN_SPEED = 0.34
SWIPE_AXIS_DOMINANCE_RATIO = 1.12
SWIPE_MAX_DRIFT = 0.30
SWIPE_COOLDOWN_SECONDS = 2.0
SWIPE_POINT_GRACE_SECONDS = 0.35
DEFAULT_HAND_HOLD_SECONDS = 0.60
DEFAULT_HAND_ROI_PADDING = 1.8
DEFAULT_HAND_ROI_RETRY_EVERY_N = 3
DEFAULT_OPTICAL_FLOW_SECONDS = 0.35


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


def translate_serialized_landmarks(landmarks, delta_x, delta_y):
    if not landmarks:
        return []

    translated = []
    for landmark in landmarks:
        if not isinstance(landmark, dict):
            translated.append(landmark)
            continue

        moved = dict(landmark)
        moved["x"] = clamp01(float(moved.get("x", 0.5)) + delta_x)
        moved["y"] = clamp01(float(moved.get("y", 0.5)) + delta_y)
        translated.append(moved)

    return translated


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


def landmarks_to_pixel_box(landmarks, frame_width, frame_height):
    if not landmarks:
        return None

    xs = [clamp01(landmark.x) * frame_width for landmark in landmarks]
    ys = [clamp01(landmark.y) * frame_height for landmark in landmarks]
    return clamp_box(int(min(xs)), int(min(ys)), int(max(xs)) + 1, int(max(ys)) + 1, frame_width, frame_height)


def expand_box_absolute(box, frame_width, frame_height, padding_scale):
    x1, y1, x2, y2 = box
    width = max(1, x2 - x1)
    height = max(1, y2 - y1)
    pad_x = int(width * padding_scale)
    pad_y = int(height * padding_scale)
    return clamp_box(x1 - pad_x, y1 - pad_y, x2 + pad_x, y2 + pad_y, frame_width, frame_height)


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


class SwipeMotionDetector:
    def __init__(self):
        self.history = []
        self.last_point_time = -999.0
        self.last_swipe_time = -999.0
        self.accepted_count = 0
        self.rejected_cooldown = 0
        self.rejected_no_point = 0
        self.rejected_distance = 0
        self.rejected_axis = 0
        self.last_reason = "idle"
        self.last_delta_x = 0.0
        self.last_delta_y = 0.0
        self.last_speed = 0.0

    def push(self, now, raw_gesture, x, y, hand_present):
        if not hand_present:
            self._trim(now)
            return None

        if raw_gesture == "point":
            self.last_point_time = now

        self.history.append((now, raw_gesture, x, y))
        self._trim(now)
        return self._detect(now, raw_gesture, x, y)

    def diagnostics(self):
        return {
            "history": len(self.history),
            "accepted": self.accepted_count,
            "rejectCooldown": self.rejected_cooldown,
            "rejectNoPoint": self.rejected_no_point,
            "rejectDistance": self.rejected_distance,
            "rejectAxis": self.rejected_axis,
            "lastReason": self.last_reason,
            "lastDeltaX": self.last_delta_x,
            "lastDeltaY": self.last_delta_y,
            "lastSpeed": self.last_speed,
        }

    def _detect(self, now, raw_gesture, x, y):
        if now - self.last_swipe_time < SWIPE_COOLDOWN_SECONDS:
            self.rejected_cooldown += 1
            self.last_reason = "cooldown"
            return None

        if raw_gesture != "point" or now - self.last_point_time > SWIPE_POINT_GRACE_SECONDS:
            self.rejected_no_point += 1
            self.last_reason = "not_point"
            return None

        best = None
        best_speed = 0.0
        saw_distance = False
        for start_time, start_gesture, start_x, start_y in self.history[:-1]:
            duration = now - start_time
            if duration <= 0.0001 or duration > SWIPE_WINDOW_SECONDS:
                continue

            if start_gesture != "point":
                continue

            delta_x = x - start_x
            delta_y = y - start_y
            horizontal = abs(delta_x)
            vertical = abs(delta_y)
            distance_value = max(horizontal, vertical)
            speed = distance_value / duration
            if distance_value < SWIPE_MIN_DISTANCE or speed < SWIPE_MIN_SPEED:
                continue

            saw_distance = True
            gesture = None
            if horizontal >= vertical * SWIPE_AXIS_DOMINANCE_RATIO and vertical <= SWIPE_MAX_DRIFT:
                gesture = "swipeLeftToRight" if delta_x > 0.0 else "swipeRightToLeft"
            elif vertical >= horizontal * SWIPE_AXIS_DOMINANCE_RATIO and horizontal <= SWIPE_MAX_DRIFT:
                gesture = "swipeTopToBottom" if delta_y > 0.0 else "swipeBottomToTop"

            if gesture is None or speed <= best_speed:
                continue

            best = (gesture, delta_x, delta_y, speed)
            best_speed = speed

        if best is None:
            if saw_distance:
                self.rejected_axis += 1
                self.last_reason = "axis"
            else:
                self.rejected_distance += 1
                self.last_reason = "distance"
            return None

        gesture, delta_x, delta_y, speed = best
        self.last_swipe_time = now
        self.accepted_count += 1
        self.last_delta_x = delta_x
        self.last_delta_y = delta_y
        self.last_speed = speed
        self.last_reason = f"accepted:{gesture}"
        return gesture

    def _trim(self, now):
        min_time = now - SWIPE_WINDOW_SECONDS
        self.history = [sample for sample in self.history if sample[0] >= min_time]


class HandHoldState:
    def __init__(self):
        self.last_packet = None
        self.last_seen_time = -999.0
        self.held_packets = 0
        self.roi_hits = 0
        self.roi_misses = 0
        self.missing_frames = 0
        self.last_hand_box = None

    def update_tracked(self, packet, landmarks, frame_width, frame_height, now, used_roi):
        self.last_packet = dict(packet)
        self.last_packet["motionGesture"] = "none"
        self.last_packet["motionConfidence"] = 0.0
        self.last_seen_time = now
        hand_box = landmarks_to_pixel_box(landmarks, frame_width, frame_height)
        if hand_box is not None:
            self.last_hand_box = hand_box

        if used_roi:
            self.roi_hits += 1

        self.missing_frames = 0

    def record_roi_miss(self):
        self.roi_misses += 1

    def record_missing_frame(self):
        self.missing_frames += 1

    def try_build_held_packet(self, now, hold_seconds, source, motion_debug):
        if self.last_packet is None or now - self.last_seen_time > hold_seconds:
            return None

        packet = dict(self.last_packet)
        packet["timestamp"] = time.time()
        packet["source"] = source
        packet["confidence"] = min(float(packet.get("confidence", 0.0)), 0.42)
        packet["trackingConfidence"] = min(float(packet.get("trackingConfidence", 0.0)), 0.35)
        packet["motionGesture"] = "none"
        packet["motionConfidence"] = 0.0
        debug = dict(motion_debug)
        debug["held"] = True
        debug["heldAgeMs"] = int((now - self.last_seen_time) * 1000)
        debug["heldPackets"] = self.held_packets + 1
        debug["roiHits"] = self.roi_hits
        debug["roiMisses"] = self.roi_misses
        packet["motionDebug"] = json.dumps(debug, separators=(",", ":"))
        packet["predicted"] = True
        self.held_packets += 1
        return packet

    def try_build_flow_packet(self, now, source, x, y, motion_debug):
        if self.last_packet is None:
            return None

        packet = dict(self.last_packet)
        previous_pointer = packet.get("pointer") or {}
        previous_x = float(previous_pointer.get("x", packet.get("x", x)))
        previous_y = float(previous_pointer.get("y", packet.get("y", y)))
        delta_x = x - previous_x
        delta_y = y - previous_y
        packet["timestamp"] = time.time()
        packet["source"] = source
        packet["x"] = x
        packet["y"] = y
        packet["confidence"] = min(float(packet.get("confidence", 0.0)), 0.36)
        packet["trackingConfidence"] = min(float(packet.get("trackingConfidence", 0.0)), 0.32)
        packet["motionGesture"] = "none"
        packet["motionConfidence"] = 0.0
        packet["predicted"] = True
        packet["pointer"] = {
            "x": x,
            "y": y,
            "z": 0.0,
            "visibility": 1.0,
        }
        packet["handLandmarks"] = translate_serialized_landmarks(packet.get("handLandmarks", []), delta_x, delta_y)
        debug = dict(motion_debug)
        debug["flow"] = True
        packet["motionDebug"] = json.dumps(debug, separators=(",", ":"))
        return packet

    def diagnostics(self):
        return {
            "heldPackets": self.held_packets,
            "roiHits": self.roi_hits,
            "roiMisses": self.roi_misses,
            "missingFrames": self.missing_frames,
        }

    def get_search_box(self, frame_width, frame_height, padding_scale):
        if self.last_hand_box is None:
            return None

        return expand_box_absolute(self.last_hand_box, frame_width, frame_height, padding_scale)


class LatestFrameBuffer:
    def __init__(self, capture, flip_horizontal):
        self.capture = capture
        self.flip_horizontal = flip_horizontal
        self.lock = threading.Lock()
        self.running = False
        self.thread = None
        self.latest_frame = None
        self.latest_id = 0
        self.latest_time = 0.0
        self.read_count = 0
        self.read_ms_total = 0.0
        self.read_ms_max = 0.0
        self.start_time = time.time()

    def start(self):
        self.running = True
        self.thread = threading.Thread(target=self._run, name="SpellGuardCameraReader", daemon=True)
        self.thread.start()

    def stop(self):
        self.running = False
        if self.thread is not None:
            self.thread.join(timeout=1.0)
            self.thread = None

    def get_latest(self, last_id):
        with self.lock:
            if self.latest_frame is None or self.latest_id == last_id:
                return None, last_id, self.latest_time

            return self.latest_frame.copy(), self.latest_id, self.latest_time

    def diagnostics(self):
        elapsed = max(0.001, time.time() - self.start_time)
        diagnostics = {
            "captureFps": self.read_count / elapsed,
            "captureReadMs": self.read_ms_total / self.read_count if self.read_count else 0.0,
            "captureReadMaxMs": self.read_ms_max,
        }
        self.read_ms_max = 0.0
        return diagnostics

    def _run(self):
        while self.running:
            started_at = time.time()
            success, frame = self.capture.read()
            elapsed_ms = (time.time() - started_at) * 1000.0
            if not success:
                time.sleep(0.002)
                continue

            if self.flip_horizontal:
                frame = cv2_flip(frame)

            with self.lock:
                self.latest_frame = frame
                self.latest_id += 1
                self.latest_time = time.time()
                self.read_count += 1
                self.read_ms_total += elapsed_ms
                self.read_ms_max = max(self.read_ms_max, elapsed_ms)


class OpticalFlowHandTracker:
    def __init__(self):
        self.previous_gray = None
        self.previous_point = None
        self.previous_time = -999.0
        self.fallback_packets = 0
        self.failures = 0

    def update_tracked(self, cv2, frame, x, y):
        frame_height, frame_width = frame.shape[:2]
        self.previous_gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
        self.previous_point = np.array([[[clamp01(x) * frame_width, clamp01(y) * frame_height]]], dtype=np.float32)
        self.previous_time = time.time()

    def try_track(self, cv2, frame, max_age_seconds):
        if self.previous_gray is None or self.previous_point is None:
            return None

        now = time.time()
        if now - self.previous_time > max_age_seconds:
            return None

        gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
        next_points, status, _ = cv2.calcOpticalFlowPyrLK(
            self.previous_gray,
            gray,
            self.previous_point,
            None,
            winSize=(21, 21),
            maxLevel=2,
            criteria=(cv2.TERM_CRITERIA_EPS | cv2.TERM_CRITERIA_COUNT, 12, 0.03),
        )
        if next_points is None or status is None or int(status[0][0]) == 0:
            self.failures += 1
            self.previous_gray = gray
            return None

        frame_height, frame_width = frame.shape[:2]
        point = next_points[0][0]
        x = clamp01(float(point[0]) / max(1, frame_width))
        y = clamp01(float(point[1]) / max(1, frame_height))
        self.previous_gray = gray
        self.previous_point = next_points
        self.previous_time = now
        self.fallback_packets += 1
        return x, y

    def diagnostics(self):
        return {
            "flowFallbackPackets": self.fallback_packets,
            "flowFailures": self.failures,
        }


class BridgeRuntimeStats:
    def __init__(self):
        self.start_time = time.time()
        self.processed_frames = 0
        self.mediapipe_ms_total = 0.0
        self.mediapipe_ms_max = 0.0
        self.udp_ms_total = 0.0
        self.udp_ms_max = 0.0

    def record_mediapipe(self, elapsed_ms):
        self.processed_frames += 1
        self.mediapipe_ms_total += elapsed_ms
        self.mediapipe_ms_max = max(self.mediapipe_ms_max, elapsed_ms)

    def record_udp(self, elapsed_ms):
        self.udp_ms_total += elapsed_ms
        self.udp_ms_max = max(self.udp_ms_max, elapsed_ms)

    def diagnostics(self):
        elapsed = max(0.001, time.time() - self.start_time)
        diagnostics = {
            "processFps": self.processed_frames / elapsed,
            "mediapipeMs": self.mediapipe_ms_total / self.processed_frames if self.processed_frames else 0.0,
            "mediapipeMaxMs": self.mediapipe_ms_max,
            "udpMs": self.udp_ms_total / self.processed_frames if self.processed_frames else 0.0,
            "udpMaxMs": self.udp_ms_max,
        }
        self.mediapipe_ms_max = 0.0
        self.udp_ms_max = 0.0
        return diagnostics


def cv2_flip(frame):
    import cv2

    return cv2.flip(frame, 1)


def parse_args():
    parser = argparse.ArgumentParser(description="Send MediaPipe hand gestures to Unity over UDP.")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5053)
    parser.add_argument("--camera-index", type=int, default=-1, help="Camera index. Use -1 to auto-select a usable camera.")
    parser.add_argument("--skip-camera-indices", default="", help="Comma-separated camera indices to ignore, for example 2 to skip OBS Virtual Camera.")
    parser.add_argument("--input-video", default="", help="Read frames from a local video file instead of the camera.")
    parser.add_argument("--loop-video", action="store_true", help="Loop the input video when it reaches the end.")
    parser.add_argument("--width", type=int, default=320)
    parser.add_argument("--height", type=int, default=240)
    parser.add_argument("--fps", type=float, default=30.0)
    parser.add_argument("--backend", choices=["auto", "dshow", "msmf"], default="auto")
    parser.add_argument("--fourcc", default="MJPG", help="Camera codec request, for example MJPG or YUY2. Use empty string to skip.")
    parser.add_argument("--no-fourcc", action="store_true", help="Do not request a camera codec.")
    parser.add_argument("--min-detection-confidence", type=float, default=0.65)
    parser.add_argument("--min-tracking-confidence", type=float, default=0.55)
    parser.add_argument("--model-complexity", type=int, choices=[0, 1], default=0)
    parser.add_argument("--enable-yolo", action="store_true")
    parser.add_argument("--yolo-every-n", type=int, default=12)
    parser.add_argument("--enable-pose", action="store_true")
    parser.add_argument("--yolo-model", default="yolo11n.pt")
    parser.add_argument("--yolo-conf", type=float, default=0.25)
    parser.add_argument("--yolo-padding", type=float, default=0.18)
    parser.add_argument("--hand-hold-seconds", type=float, default=DEFAULT_HAND_HOLD_SECONDS)
    parser.add_argument("--hand-roi-padding", type=float, default=DEFAULT_HAND_ROI_PADDING)
    parser.add_argument("--hand-roi-retry-every-n", type=int, default=DEFAULT_HAND_ROI_RETRY_EVERY_N)
    parser.add_argument("--disable-hand-roi-retry", action="store_true")
    parser.add_argument("--enable-hand-roi-retry", action="store_true")
    parser.add_argument("--disable-threaded-capture", action="store_true")
    parser.add_argument("--disable-optical-flow", action="store_true")
    parser.add_argument("--optical-flow-seconds", type=float, default=DEFAULT_OPTICAL_FLOW_SECONDS)
    parser.add_argument("--show-preview", action="store_true")
    return parser.parse_args()


def build_packet(
    hand_present,
    gesture,
    x=0.5,
    y=0.5,
    confidence=0.0,
    landmarks=None,
    pose_landmarks=None,
    source="mediapipeHandsBridge",
    tracking_confidence=None,
    raw_gesture=None,
    motion_gesture=None,
    motion_confidence=0.0,
    motion_debug=None,
    predicted=False,
    performance=None,
):
    tracking_confidence = confidence if tracking_confidence is None else tracking_confidence
    raw_gesture = gesture if raw_gesture is None else raw_gesture
    motion_debug = {} if motion_debug is None else motion_debug
    return {
        "handPresent": hand_present,
        "gesture": gesture,
        "rawGesture": raw_gesture,
        "x": clamp01(x),
        "y": clamp01(y),
        "confidence": clamp01(confidence),
        "trackingConfidence": clamp01(tracking_confidence),
        "timestamp": time.time(),
        "source": source,
        "motionGesture": motion_gesture or "none",
        "motionConfidence": clamp01(motion_confidence),
        "motionDebug": json.dumps(motion_debug, separators=(",", ":")),
        "predicted": predicted,
        "performance": json.dumps({} if performance is None else performance, separators=(",", ":")),
        "pointer": {
            "x": clamp01(x),
            "y": clamp01(y),
            "z": 0.0,
            "visibility": 1.0 if hand_present else 0.0,
        },
        "handLandmarks": serialize_landmarks(landmarks),
        "poseLandmarks": serialize_pose_landmarks(pose_landmarks),
    }


def build_summary(frame_count, hand_frame_count, confidence_sum, start_time, last_gesture, source):
    elapsed_seconds = max(0.001, time.time() - start_time)
    average_confidence = confidence_sum / hand_frame_count if hand_frame_count else 0.0
    return {
        "frames": frame_count,
        "handFrames": hand_frame_count,
        "handRatio": hand_frame_count / frame_count if frame_count else 0.0,
        "avgConfidence": average_confidence,
        "elapsedSeconds": elapsed_seconds,
        "fps": frame_count / elapsed_seconds if elapsed_seconds > 0 else 0.0,
        "lastGesture": last_gesture,
        "source": source,
    }


def describe_bridge_source(base_source, input_video_path):
    if not input_video_path:
        return base_source

    return f"{base_source} | offline:{Path(input_video_path).name}"


def resolve_capture_backend(cv2, backend_name):
    if backend_name == "dshow":
        return cv2.CAP_DSHOW
    if backend_name == "msmf":
        return cv2.CAP_MSMF
    return 0


def configure_capture(cv2, capture, width, height, fps, fourcc):
    if fourcc:
        codec = fourcc[:4].ljust(4)
        capture.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc(*codec))

    capture.set(cv2.CAP_PROP_FRAME_WIDTH, width)
    capture.set(cv2.CAP_PROP_FRAME_HEIGHT, height)
    capture.set(cv2.CAP_PROP_FPS, fps)
    capture.set(cv2.CAP_PROP_BUFFERSIZE, 1)


def looks_like_decoded_camera_frame(frame):
    if frame is None or frame.size == 0:
        return False

    if len(frame.shape) != 3 or frame.shape[2] < 3:
        return False

    mean = float(frame.mean())
    std = float(frame.std())
    if mean < 3.0 or std < 5.0:
        return False

    sample = frame[:: max(1, frame.shape[0] // 120), :: max(1, frame.shape[1] // 160), :3].astype(np.int16)
    horizontal_noise = np.mean(np.abs(np.diff(sample, axis=1)))
    vertical_noise = np.mean(np.abs(np.diff(sample, axis=0)))
    channel_gap = np.mean(np.abs(sample[:, :, 0] - sample[:, :, 1])) + np.mean(np.abs(sample[:, :, 1] - sample[:, :, 2]))
    luminance = sample.mean(axis=2)
    luminance_noise = np.mean(np.abs(np.diff(luminance, axis=1))) + np.mean(np.abs(np.diff(luminance, axis=0)))

    # Random-looking color snow from a bad backend/FourCC negotiation has very high
    # adjacent-pixel deltas across both axes. Real webcam frames can be textured,
    # but usually not this uniformly noisy.
    color_snow = horizontal_noise > 32 and vertical_noise > 32 and channel_gap > 35
    monochrome_snow = luminance_noise > 95 and horizontal_noise > 28 and vertical_noise > 28
    return not (color_snow or monochrome_snow)


def probe_capture_quality(capture, sample_count=20):
    valid_frame = None
    ok_count = 0
    started_at = time.time()
    for _ in range(sample_count):
        success, frame = capture.read()
        if success and frame is not None:
            ok_count += 1
            valid_frame = frame

    elapsed = max(0.001, time.time() - started_at)
    if valid_frame is None or not looks_like_decoded_camera_frame(valid_frame):
        return False, 0.0

    return True, ok_count / elapsed


def warmup_and_validate_capture(capture):
    valid, _ = probe_capture_quality(capture, sample_count=10)
    return valid


def make_camera_capture(cv2, camera_index, backend_name, width, height, fps, fourcc):
    backend = resolve_capture_backend(cv2, backend_name)
    capture = cv2.VideoCapture(camera_index, backend) if backend else cv2.VideoCapture(camera_index)
    configure_capture(cv2, capture, width, height, fps, fourcc)
    return capture


def open_capture(cv2, args, using_video_file):
    if using_video_file:
        return cv2.VideoCapture(args.input_video), "video"

    requested_fourcc = "" if args.no_fourcc else args.fourcc
    if args.backend != "auto" and args.camera_index >= 0:
        capture = make_camera_capture(cv2, args.camera_index, args.backend, args.width, args.height, args.fps, requested_fourcc)
        return capture, f"{args.backend}/{requested_fourcc or 'default'}"

    skipped_indices = parse_index_set(args.skip_camera_indices)
    indices = [args.camera_index] if args.camera_index >= 0 else [index for index in range(6) if index not in skipped_indices]
    backend_attempts = [args.backend] if args.backend != "auto" else ["dshow", "msmf", "auto"]
    fourcc_attempts = [requested_fourcc] if requested_fourcc else [""]
    if args.backend == "auto":
        fourcc_attempts = ["", "MJPG", "YUY2"]
    attempts = [(index, backend_name, fourcc) for index in indices for backend_name in backend_attempts for fourcc in fourcc_attempts]
    last_capture = None
    best = None
    for index, backend_name, fourcc in attempts:
        if last_capture is not None:
            last_capture.release()

        capture = make_camera_capture(cv2, index, backend_name, args.width, args.height, args.fps, fourcc)
        if capture.isOpened():
            valid, probe_fps = probe_capture_quality(capture)
            if valid:
                score = probe_fps - index * 2.0
                if best is None or score > best[0]:
                    best = (score, index, backend_name, fourcc)

        last_capture = capture

    if best is not None:
        _, index, backend_name, fourcc = best
        if last_capture is not None:
            last_capture.release()

        capture = make_camera_capture(cv2, index, backend_name, args.width, args.height, args.fps, fourcc)
        warmup_and_validate_capture(capture)
        return capture, f"{backend_name}/{fourcc or 'default'} index={index}"

    return last_capture if last_capture is not None else cv2.VideoCapture(args.camera_index), "fallback"


def parse_index_set(value):
    if not value:
        return set()

    result = set()
    for token in value.split(","):
        token = token.strip()
        if not token:
            continue

        try:
            result.add(int(token))
        except ValueError:
            pass

    return result


def flush_capture(capture, count=3):
    for _ in range(count):
        capture.read()


def print_capture_status(cv2, capture, args, using_video_file, selected_backend):
    if using_video_file:
        print(f"[bridge-camera] video={args.input_video}")
        return

    width = capture.get(cv2.CAP_PROP_FRAME_WIDTH)
    height = capture.get(cv2.CAP_PROP_FRAME_HEIGHT)
    fps = capture.get(cv2.CAP_PROP_FPS)
    fourcc_value = int(capture.get(cv2.CAP_PROP_FOURCC) or 0)
    fourcc = "".join(chr((fourcc_value >> 8 * i) & 0xFF) for i in range(4)).strip()
    print(
        "[bridge-camera] "
        f"index={args.camera_index} backend={selected_backend} requested={args.width}x{args.height}@{args.fps:g} "
        f"fourcc={'' if args.no_fourcc else args.fourcc or 'default'} actual={width:.0f}x{height:.0f}@{fps:.1f} actual_fourcc={fourcc or 'unknown'}"
    )


def process_hands_in_box(cv2, hands, frame, box):
    frame_height, frame_width = frame.shape[:2]
    processing_frame = crop_frame(frame, box)
    rgb_processing_frame = cv2.cvtColor(processing_frame, cv2.COLOR_BGR2RGB)
    hand_results = hands.process(rgb_processing_frame)
    if not hand_results.multi_hand_landmarks:
        return None, None, None

    landmarks = hand_results.multi_hand_landmarks[0].landmark
    frame_landmarks = remap_landmarks_to_frame(landmarks, box, frame_width, frame_height)
    pointer_x, pointer_y = remap_point_to_frame(landmarks[8], box, frame_width, frame_height)
    return hand_results, frame_landmarks, (pointer_x, pointer_y)


def merge_diagnostics(*sources):
    merged = {}
    for source in sources:
        if source:
            merged.update(source)
    return merged


def main():
    args = parse_args()
    if not args.enable_hand_roi_retry:
        args.disable_hand_roi_retry = True

    try:
        import cv2
        import mediapipe as mp
    except ImportError as exc:
        print("缺少依赖，请先安装：pip install -r requirements.txt", file=sys.stderr)
        print(str(exc), file=sys.stderr)
        return 1

    socket_client = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    using_video_file = bool(args.input_video)
    capture, selected_backend = open_capture(cv2, args, using_video_file)

    if not capture.isOpened():
        input_source_label = args.input_video if using_video_file else f"摄像头 {args.camera_index}"
        print(f"无法打开输入源：{input_source_label}", file=sys.stderr)
        return 1
    print_capture_status(cv2, capture, args, using_video_file, selected_backend)
    flush_capture(capture)

    stabilizer = GestureStabilizer()
    swipe_detector = SwipeMotionDetector()
    hand_hold = HandHoldState()
    flow_tracker = OpticalFlowHandTracker()
    runtime_stats = BridgeRuntimeStats()
    yolo_detector = None
    bridge_source = "mediapipeHandsBridge"

    mp_hands = None
    mp_pose = None
    mp_draw = None
    hands_context = None
    pose_context = None

    if hasattr(mp, "solutions"):
        mp_hands = mp.solutions.hands
        mp_pose = mp.solutions.pose
        mp_draw = mp.solutions.drawing_utils
        hands_context = mp_hands.Hands(
            max_num_hands=1,
            model_complexity=args.model_complexity,
            min_detection_confidence=args.min_detection_confidence,
            min_tracking_confidence=args.min_tracking_confidence,
        )
        pose_context = mp_pose.Pose(
            model_complexity=1,
            min_detection_confidence=args.min_detection_confidence,
            min_tracking_confidence=args.min_tracking_confidence,
        )
    else:
        from mediapipe.tasks import python as mp_tasks
        from mediapipe.tasks.python import vision as mp_vision

        base_options = mp_tasks.BaseOptions
        hand_options = mp_vision.HandLandmarkerOptions
        pose_options = mp_vision.PoseLandmarkerOptions
        running_mode = mp_vision.RunningMode
        hands_context = mp_vision.HandLandmarker.create_from_options(
            hand_options(
                base_options=base_options(model_asset_path=""),
                running_mode=running_mode.VIDEO,
                num_hands=1,
                min_hand_detection_confidence=args.min_detection_confidence,
                min_hand_presence_confidence=args.min_tracking_confidence,
                min_tracking_confidence=args.min_tracking_confidence,
            )
        )
        pose_context = mp_vision.PoseLandmarker.create_from_options(
            pose_options(
                base_options=base_options(model_asset_path=""),
                running_mode=running_mode.VIDEO,
                min_pose_detection_confidence=args.min_detection_confidence,
                min_pose_presence_confidence=args.min_tracking_confidence,
                min_tracking_confidence=args.min_tracking_confidence,
            )
        )

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
    frame_count = 0
    hand_frame_count = 0
    confidence_sum = 0.0
    start_time = time.time()
    last_gesture = "none"
    threaded_capture = None
    last_threaded_frame_id = 0

    with hands_context as hands, pose_context as pose:
        if not args.disable_threaded_capture and not using_video_file:
            threaded_capture = LatestFrameBuffer(capture, True)
            threaded_capture.start()

        try:
            while True:
                if threaded_capture is not None:
                    frame, frame_id, _ = threaded_capture.get_latest(last_threaded_frame_id)
                    if frame is None:
                        time.sleep(0.001)
                        continue

                    last_threaded_frame_id = frame_id
                else:
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

                if yolo_detector is not None and (frame_count % max(1, args.yolo_every_n) == 0 or hand_hold.last_hand_box is None):
                    detected_box, yolo_confidence = yolo_detector.detect(frame)
                    if detected_box is not None:
                        person_box = expand_box(detected_box, frame_width, frame_height, args.yolo_padding)

                processing_box = person_box or (0, 0, frame_width, frame_height)
                mediapipe_started_at = time.time()
                hand_results, frame_landmarks, pointer = process_hands_in_box(cv2, hands, frame, processing_box)
                used_roi_retry = False
                if hand_results is None:
                    hand_hold.record_missing_frame()

                should_retry_roi = (
                    hand_results is None
                    and not args.disable_hand_roi_retry
                    and hand_hold.missing_frames % max(1, args.hand_roi_retry_every_n) == 0
                )
                if should_retry_roi:
                    retry_box = hand_hold.get_search_box(frame_width, frame_height, args.hand_roi_padding)
                    if retry_box is not None:
                        retry_results, retry_landmarks, retry_pointer = process_hands_in_box(cv2, hands, frame, retry_box)
                        if retry_results is not None:
                            hand_results = retry_results
                            frame_landmarks = retry_landmarks
                            pointer = retry_pointer
                            processing_box = retry_box
                            used_roi_retry = True
                        else:
                            hand_hold.record_roi_miss()

                processing_frame = crop_frame(frame, processing_box)
                rgb_processing_frame = cv2.cvtColor(processing_frame, cv2.COLOR_BGR2RGB)
                pose_results = pose.process(rgb_processing_frame) if args.enable_pose else None
                runtime_stats.record_mediapipe((time.time() - mediapipe_started_at) * 1000.0)

                performance = runtime_stats.diagnostics()
                if threaded_capture is not None:
                    performance.update(threaded_capture.diagnostics())

                motion_debug = merge_diagnostics(swipe_detector.diagnostics(), hand_hold.diagnostics(), flow_tracker.diagnostics())
                packet = build_packet(False, "none", source=packet_source, motion_debug=motion_debug, performance=performance)
                label = "none"
                pose_landmarks = []
                frame_count += 1

                if pose_results and pose_results.pose_landmarks:
                    pose_landmarks = remap_landmarks_to_frame(
                        pose_results.pose_landmarks.landmark,
                        processing_box,
                        frame_width,
                        frame_height,
                    )

                if hand_results is not None and hand_results.multi_hand_landmarks:
                    raw = classify_gesture(frame_landmarks)
                    stable = stabilizer.push(raw)
                    pointer_x, pointer_y = pointer
                    flow_tracker.update_tracked(cv2, frame, pointer_x, pointer_y)
                    motion_gesture = swipe_detector.push(time.time(), raw, pointer_x, pointer_y, True)
                    tracking_confidence = max(0.95 if stable != "unknown" else 0.5, yolo_confidence)
                    motion_debug = merge_diagnostics(swipe_detector.diagnostics(), hand_hold.diagnostics(), flow_tracker.diagnostics())
                    motion_debug["roiRetry"] = used_roi_retry
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
                        raw_gesture=raw,
                        motion_gesture=motion_gesture,
                        motion_confidence=0.9 if motion_gesture else 0.0,
                        motion_debug=motion_debug,
                        performance=performance,
                    )
                    hand_hold.update_tracked(packet, frame_landmarks, frame_width, frame_height, time.time(), used_roi_retry)
                    label = stable
                    hand_frame_count += 1
                    confidence_sum += packet["confidence"]
                    last_gesture = stable

                    if args.show_preview:
                        for hand_landmarks in hand_results.multi_hand_landmarks:
                            preview_landmarks = remap_landmarks_to_frame(hand_landmarks.landmark, processing_box, frame_width, frame_height)
                            for point in preview_landmarks:
                                cv2.circle(frame, (int(point.x * frame_width), int(point.y * frame_height)), 3, (0, 255, 180), -1)
                        cv2.circle(frame, (int(pointer_x * frame_width), int(pointer_y * frame_height)), 10, (0, 255, 255), 2)
                else:
                    stabilizer.push("none")
                    swipe_detector.push(time.time(), "none", 0.5, 0.5, False)
                    flow_point = None if args.disable_optical_flow else flow_tracker.try_track(cv2, frame, args.optical_flow_seconds)
                    if flow_point is not None:
                        flow_debug = merge_diagnostics(swipe_detector.diagnostics(), hand_hold.diagnostics(), flow_tracker.diagnostics())
                        flow_debug["flow"] = True
                        packet = hand_hold.try_build_flow_packet(time.time(), packet_source, flow_point[0], flow_point[1], flow_debug) or packet
                        packet["performance"] = json.dumps(performance, separators=(",", ":"))
                        label = "flow"
                    else:
                        held_packet = hand_hold.try_build_held_packet(time.time(), args.hand_hold_seconds, packet_source, merge_diagnostics(swipe_detector.diagnostics(), flow_tracker.diagnostics()))
                        if held_packet is not None:
                            packet = held_packet
                            packet["performance"] = json.dumps(performance, separators=(",", ":"))
                            label = f"held:{packet.get('gesture', 'none')}"

                if args.show_preview and pose_landmarks:
                    for point in pose_landmarks:
                        if point.visibility > 0.2:
                            cv2.circle(frame, (int(point.x * frame_width), int(point.y * frame_height)), 2, (255, 180, 0), -1)

                if args.show_preview and person_box is not None:
                    x1, y1, x2, y2 = person_box
                    cv2.rectangle(frame, (x1, y1), (x2, y2), (80, 160, 255), 2)
                    cv2.putText(frame, f"YOLO {yolo_confidence:.2f}", (x1, max(24, y1 - 8)), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (80, 160, 255), 2)

                udp_started_at = time.time()
                socket_client.sendto(json.dumps(packet).encode("utf-8"), (args.host, args.port))
                runtime_stats.record_udp((time.time() - udp_started_at) * 1000.0)

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
        finally:
            if threaded_capture is not None:
                threaded_capture.stop()

    if False:
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

            if yolo_detector is not None and (frame_count % max(1, args.yolo_every_n) == 0 or hand_hold.last_hand_box is None):
                detected_box, yolo_confidence = yolo_detector.detect(frame)
                if detected_box is not None:
                    person_box = expand_box(detected_box, frame_width, frame_height, args.yolo_padding)

            processing_box = person_box or (0, 0, frame_width, frame_height)
            hand_results, frame_landmarks, pointer = process_hands_in_box(cv2, hands, frame, processing_box)
            used_roi_retry = False
            if hand_results is None:
                hand_hold.record_missing_frame()

            should_retry_roi = (
                hand_results is None
                and not args.disable_hand_roi_retry
                and hand_hold.missing_frames % max(1, args.hand_roi_retry_every_n) == 0
            )
            if should_retry_roi:
                retry_box = hand_hold.get_search_box(frame_width, frame_height, args.hand_roi_padding)
                if retry_box is not None:
                    retry_results, retry_landmarks, retry_pointer = process_hands_in_box(cv2, hands, frame, retry_box)
                    if retry_results is not None:
                        hand_results = retry_results
                        frame_landmarks = retry_landmarks
                        pointer = retry_pointer
                        processing_box = retry_box
                        used_roi_retry = True
                    else:
                        hand_hold.record_roi_miss()

            processing_frame = crop_frame(frame, processing_box)
            rgb_processing_frame = cv2.cvtColor(processing_frame, cv2.COLOR_BGR2RGB)
            pose_results = pose.process(rgb_processing_frame) if args.enable_pose else None

            motion_debug = swipe_detector.diagnostics()
            motion_debug.update(hand_hold.diagnostics())
            packet = build_packet(False, "none", source=packet_source, motion_debug=motion_debug)
            label = "none"
            pose_landmarks = []
            frame_count += 1

            if pose_results and pose_results.pose_landmarks:
                pose_landmarks = remap_landmarks_to_frame(
                    pose_results.pose_landmarks.landmark,
                    processing_box,
                    frame_width,
                    frame_height,
                )

            if hand_results is not None and hand_results.multi_hand_landmarks:
                landmarks = hand_results.multi_hand_landmarks[0].landmark
                raw = classify_gesture(frame_landmarks)
                stable = stabilizer.push(raw)
                pointer_x, pointer_y = pointer
                motion_gesture = swipe_detector.push(time.time(), raw, pointer_x, pointer_y, True)
                tracking_confidence = max(0.95 if stable != "unknown" else 0.5, yolo_confidence)
                motion_debug = swipe_detector.diagnostics()
                motion_debug.update(hand_hold.diagnostics())
                motion_debug["roiRetry"] = used_roi_retry
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
                    raw_gesture=raw,
                    motion_gesture=motion_gesture,
                    motion_confidence=0.9 if motion_gesture else 0.0,
                    motion_debug=motion_debug,
                )
                hand_hold.update_tracked(packet, frame_landmarks, frame_width, frame_height, time.time(), used_roi_retry)
                label = stable
                hand_frame_count += 1
                confidence_sum += packet["confidence"]
                last_gesture = stable

                if args.show_preview:
                    for hand_landmarks in hand_results.multi_hand_landmarks:
                        preview_landmarks = remap_landmarks_to_frame(hand_landmarks.landmark, processing_box, frame_width, frame_height)
                        for point in preview_landmarks:
                            cv2.circle(frame, (int(point.x * frame_width), int(point.y * frame_height)), 3, (0, 255, 180), -1)
                    cv2.circle(frame, (int(pointer_x * frame_width), int(pointer_y * frame_height)), 10, (0, 255, 255), 2)
            else:
                stabilizer.push("none")
                swipe_detector.push(time.time(), "none", 0.5, 0.5, False)
                held_packet = hand_hold.try_build_held_packet(time.time(), args.hand_hold_seconds, packet_source, swipe_detector.diagnostics())
                if held_packet is not None:
                    packet = held_packet
                    label = f"held:{packet.get('gesture', 'none')}"

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
    summary = build_summary(frame_count, hand_frame_count, confidence_sum, start_time, last_gesture, packet_source)
    print(f"[bridge-summary] {json.dumps(summary, ensure_ascii=False)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
