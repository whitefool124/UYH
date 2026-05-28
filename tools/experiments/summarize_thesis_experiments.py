from __future__ import annotations

import csv
import json
import math
from collections import Counter, defaultdict
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from statistics import mean, pstdev


ROOT = Path(__file__).resolve().parents[2]
BUILD_TEMP = ROOT / "build-temp"
UNITY_RESULTS = ROOT / "unity-spell-guard" / "ExperimentResults"
CUSTOM_GESTURES = ROOT / "unity-spell-guard" / "Assets" / "ProjectGestureLibrary" / "CustomGestures"
OUT = ROOT / "论文材料" / "实验补充数据"

JESTER_MINED = BUILD_TEMP / "jester_mined_motion_report_300_samehand.csv"
JESTER_SUBSET = BUILD_TEMP / "jester_mined_motion_subset_300_samehand.json"
REGRESSION_DIR = BUILD_TEMP / "external-regression-report-300-samehand"
VALIDATION = REGRESSION_DIR / "validation_results.csv"
YOLO_REFERENCE = BUILD_TEMP / "yolo_reference_batch.csv"
EXTENDED_DIR = UNITY_RESULTS / "extended_20260523_avi200_train2"
PERFORMANCE_SUMMARY = EXTENDED_DIR / "extended_performance_summary_20260523_210540.csv"
PERFORMANCE_RUNS = EXTENDED_DIR / "extended_performance_runs_20260523_210540.csv"
RECOGNITION_SUMMARY = EXTENDED_DIR / "extended_recognition_summary_20260523_210540.csv"
DATASET_SCALE = EXTENDED_DIR / "extended_dataset_scale_20260523_210540.csv"


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def write_csv(path: Path, rows: list[dict[str, object]], fieldnames: list[str] | None = None) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if fieldnames is None:
        keys: list[str] = []
        for row in rows:
            for key in row:
                if key not in keys:
                    keys.append(key)
        fieldnames = keys
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


def fnum(value: object, default: float = 0.0) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return default


def safe_div(num: float, den: float) -> float:
    return num / den if den else 0.0


def round4(value: float) -> float:
    return round(value, 4)


def summarize_dataset() -> dict[str, object]:
    mined_rows = read_csv(JESTER_MINED)
    accepted = [r for r in mined_rows if str(r.get("accepted", "")).lower() == "true"]
    rejected = [r for r in mined_rows if str(r.get("accepted", "")).lower() != "true"]
    label_counts = Counter(r.get("label") or "unlabeled" for r in accepted)
    reject_reasons = Counter()
    for row in rejected:
        detected = fnum(row.get("detected"))
        score = fnum(row.get("score"))
        if detected <= 0:
            reject_reasons["no_hand_detected"] += 1
        elif not row.get("label"):
            reject_reasons["no_motion_label"] += 1
        elif score < 0.05:
            reject_reasons["low_motion_score"] += 1
        else:
            reject_reasons["filtered_by_rule"] += 1

    with JESTER_SUBSET.open("r", encoding="utf-8") as handle:
        subset = json.load(handle)
    clips = subset.get("Clips", [])
    split_counts = Counter(c.get("Split", "unknown") for c in clips)
    split_label_counts: Counter[tuple[str, str]] = Counter((c.get("Split", "unknown"), c.get("Label", "unknown")) for c in clips)

    dataset_scale = {row["metric"]: row["value"] for row in read_csv(DATASET_SCALE)}

    label_rows = [
        {
            "label": label,
            "accepted_clips": count,
            "share": round4(safe_div(count, len(accepted))),
        }
        for label, count in sorted(label_counts.items())
    ]
    write_csv(OUT / "dataset_label_distribution.csv", label_rows)

    split_rows = [
        {
            "split": split,
            "label": label,
            "clips": count,
        }
        for (split, label), count in sorted(split_label_counts.items())
    ]
    write_csv(OUT / "dataset_split_distribution.csv", split_rows)

    reject_rows = [{"reason": key, "count": value} for key, value in sorted(reject_reasons.items())]
    write_csv(OUT / "dataset_rejection_reasons.csv", reject_rows)

    return {
        "mined_rows": len(mined_rows),
        "accepted_rows": len(accepted),
        "rejected_rows": len(rejected),
        "accepted_rate": round4(safe_div(len(accepted), len(mined_rows))),
        "subset_clips": len(clips),
        "train_clips": split_counts.get("train", 0),
        "test_clips": split_counts.get("test", 0),
        "sampled_frames": int(fnum(dataset_scale.get("sampled_frames"))),
        "detected_frames": int(fnum(dataset_scale.get("detected_frames"))),
        "detected_frame_rate": round4(safe_div(fnum(dataset_scale.get("detected_frames")), fnum(dataset_scale.get("sampled_frames")))),
        "dataset_clips": int(fnum(dataset_scale.get("dataset_clips"))),
        "total_replay_frames": int(fnum(dataset_scale.get("total_frames"))),
        "elapsed_seconds": round4(fnum(dataset_scale.get("elapsed_seconds"))),
    }


def summarize_validation() -> dict[str, object]:
    rows = read_csv(VALIDATION)
    labels = sorted({r["label"] for r in rows} | {r["matched_label"] for r in rows if r.get("matched_label")})
    by_label: dict[str, dict[str, int]] = {label: {"tp": 0, "fp": 0, "fn": 0, "support": 0} for label in labels}
    confusion: Counter[tuple[str, str]] = Counter()

    for row in rows:
        true_label = row["label"]
        pred_label = row.get("matched_label") or "none"
        matched = str(row.get("matched", "")).lower() == "true"
        correct = str(row.get("correct", "")).lower() == "true"
        by_label.setdefault(true_label, {"tp": 0, "fp": 0, "fn": 0, "support": 0})
        by_label[true_label]["support"] += 1
        confusion[(true_label, pred_label)] += 1

        if correct:
            by_label[true_label]["tp"] += 1
        else:
            by_label[true_label]["fn"] += 1
            if matched and pred_label != "none":
                by_label.setdefault(pred_label, {"tp": 0, "fp": 0, "fn": 0, "support": 0})
                by_label[pred_label]["fp"] += 1

    metric_rows = []
    total_tp = total_fp = total_fn = total_support = 0
    for label in sorted(by_label):
        stat = by_label[label]
        tp, fp, fn, support = stat["tp"], stat["fp"], stat["fn"], stat["support"]
        precision = safe_div(tp, tp + fp)
        recall = safe_div(tp, tp + fn)
        f1 = safe_div(2 * precision * recall, precision + recall)
        metric_rows.append(
            {
                "gesture": label,
                "support": support,
                "tp": tp,
                "fp": fp,
                "fn": fn,
                "precision": round4(precision),
                "recall": round4(recall),
                "f1": round4(f1),
                "miss_rate": round4(safe_div(fn, support)),
            }
        )
        total_tp += tp
        total_fp += fp
        total_fn += fn
        total_support += support

    write_csv(OUT / "gesture_precision_recall_f1.csv", metric_rows)
    confusion_rows = [
        {
            "true_label": true_label,
            "predicted_label": pred_label,
            "count": count,
        }
        for (true_label, pred_label), count in sorted(confusion.items())
    ]
    write_csv(OUT / "gesture_confusion_matrix_long.csv", confusion_rows)

    recognition_rows = read_csv(RECOGNITION_SUMMARY)
    normalized_recognition = []
    for row in recognition_rows:
        attempts = fnum(row.get("attempts"))
        correct = fnum(row.get("correct"))
        false_match = fnum(row.get("false_match"))
        missed = fnum(row.get("missed"))
        precision = safe_div(correct, correct + false_match)
        recall = safe_div(correct, correct + missed)
        f1 = safe_div(2 * precision * recall, precision + recall)
        normalized_recognition.append(
            {
                "gesture": row.get("gesture"),
                "attempts": int(attempts),
                "correct": int(correct),
                "missed": int(missed),
                "false_match": int(false_match),
                "precision": round4(precision),
                "recall": round4(recall),
                "f1": round4(f1),
                "success_rate": round4(fnum(row.get("success_rate"))),
            }
        )
    write_csv(OUT / "extended_recognition_metrics.csv", normalized_recognition)

    micro_precision = safe_div(total_tp, total_tp + total_fp)
    micro_recall = safe_div(total_tp, total_tp + total_fn)
    micro_f1 = safe_div(2 * micro_precision * micro_recall, micro_precision + micro_recall)
    return {
        "validation_clips": len(rows),
        "validation_correct": total_tp,
        "validation_accuracy": round4(safe_div(total_tp, len(rows))),
        "micro_precision": round4(micro_precision),
        "micro_recall": round4(micro_recall),
        "micro_f1": round4(micro_f1),
    }


def summarize_yolo() -> dict[str, object]:
    rows = read_csv(YOLO_REFERENCE)
    success_rows = [r for r in rows if str(r.get("success", "")).lower() == "true"]
    hand_positive = [r for r in rows if fnum(r.get("hand_frames")) > 0]
    zero_hand = [r for r in rows if fnum(r.get("hand_frames")) <= 0]
    fps_values = [fnum(r.get("fps")) for r in rows]
    ratio_values = [fnum(r.get("hand_ratio")) for r in rows]
    confidence_values = [fnum(r.get("avg_confidence")) for r in hand_positive]

    summary_rows = [
        {
            "metric": "videos",
            "value": len(rows),
        },
        {
            "metric": "success_videos",
            "value": len(success_rows),
        },
        {
            "metric": "hand_positive_videos",
            "value": len(hand_positive),
        },
        {
            "metric": "zero_hand_videos",
            "value": len(zero_hand),
        },
        {
            "metric": "avg_hand_ratio",
            "value": round4(mean(ratio_values) if ratio_values else 0.0),
        },
        {
            "metric": "avg_fps",
            "value": round4(mean(fps_values) if fps_values else 0.0),
        },
        {
            "metric": "avg_confidence_on_hand_positive",
            "value": round4(mean(confidence_values) if confidence_values else 0.0),
        },
    ]
    write_csv(OUT / "yolo_bridge_summary.csv", summary_rows)

    per_video = [
        {
            "video": Path(row["video"]).name,
            "success": row["success"],
            "frames": row["frames"],
            "hand_frames": row["hand_frames"],
            "hand_ratio": row["hand_ratio"],
            "avg_confidence": row["avg_confidence"],
            "fps": row["fps"],
            "last_gesture": row["last_gesture"],
        }
        for row in rows
    ]
    write_csv(OUT / "yolo_bridge_per_video.csv", per_video)

    return {
        "yolo_videos": len(rows),
        "yolo_success_videos": len(success_rows),
        "yolo_hand_positive_videos": len(hand_positive),
        "yolo_zero_hand_videos": len(zero_hand),
        "yolo_avg_hand_ratio": round4(mean(ratio_values) if ratio_values else 0.0),
        "yolo_avg_fps": round4(mean(fps_values) if fps_values else 0.0),
    }


def summarize_performance() -> dict[str, object]:
    rows = read_csv(PERFORMANCE_SUMMARY)
    write_csv(
        OUT / "input_mode_performance_summary.csv",
        [
            {
                "mode": row["mode"],
                "runs": int(fnum(row["runs"])),
                "average_fps_mean": round4(fnum(row["average_fps_mean"])),
                "average_fps_std": round4(fnum(row["average_fps_std"])),
                "p95_frame_ms_mean": round4(fnum(row["p95_frame_ms_mean"])),
                "min_fps_mean": round4(fnum(row["min_fps_mean"])),
                "avg_packet_interval_ms_mean": round4(fnum(row["avg_packet_interval_ms_mean"])),
                "avg_estimated_latency_ms_mean": round4(fnum(row["avg_estimated_latency_ms_mean"])),
                "motion_commands_mean": round4(fnum(row["motion_commands_mean"])),
                "static_commands_mean": round4(fnum(row["static_commands_mean"])),
            }
            for row in rows
        ],
    )

    runs = read_csv(PERFORMANCE_RUNS)
    mode_counts = Counter(row["mode"] for row in runs)
    return {
        "performance_runs": len(runs),
        "performance_modes": ", ".join(f"{mode}:{count}" for mode, count in sorted(mode_counts.items())),
    }


@dataclass
class Point:
    x: float
    y: float


def dist(a: Point, b: Point) -> float:
    return math.hypot(a.x - b.x, a.y - b.y)


def parse_point(raw: dict[str, object]) -> Point:
    return Point(fnum(raw.get("x", raw.get("X"))), fnum(raw.get("y", raw.get("Y"))))


def frame_landmarks(frame: dict[str, object]) -> list[Point]:
    raw = frame.get("Landmarks") or frame.get("landmarks") or []
    return [parse_point(p) for p in raw if isinstance(p, dict)]


def path(values: list[float]) -> float:
    return sum(abs(values[i] - values[i - 1]) for i in range(1, len(values)))


def peak_velocity(values: list[float], times: list[float]) -> float:
    peaks = []
    for i in range(1, len(values)):
        dt = times[i] - times[i - 1]
        if dt > 0:
            peaks.append(abs(values[i] - values[i - 1]) / dt)
    return max(peaks) if peaks else 0.0


def oscillations(values: list[float]) -> int:
    if len(values) < 3:
        return 0
    signs = []
    for i in range(1, len(values)):
        delta = values[i] - values[i - 1]
        if abs(delta) < 1e-5:
            continue
        signs.append(1 if delta > 0 else -1)
    return sum(1 for i in range(1, len(signs)) if signs[i] != signs[i - 1])


def palm_center(points: list[Point]) -> Point:
    if len(points) >= 18:
        indexes = [0, 5, 9, 13, 17]
        return Point(mean(points[i].x for i in indexes), mean(points[i].y for i in indexes))
    return Point(0.0, 0.0)


def summarize_custom_gesture_features() -> dict[str, object]:
    rows = []
    for path_obj in sorted(CUSTOM_GESTURES.glob("*.json")):
        with path_obj.open("r", encoding="utf-8-sig") as handle:
            try:
                data = json.load(handle)
            except json.JSONDecodeError:
                continue
        gesture_id = data.get("GestureId") or path_obj.stem
        display = data.get("DisplayName") or gesture_id
        rule = data.get("DynamicRule") or {}
        finger_a = int(fnum(rule.get("FingerAIndex"), 4))
        finger_b = int(fnum(rule.get("FingerBIndex"), 8))
        samples = data.get("Samples") or []
        for sample in samples:
            frames = sample.get("Frames") or []
            times: list[float] = []
            selected_distances: list[float] = []
            thumb_index: list[float] = []
            thumb_middle: list[float] = []
            palms: list[Point] = []
            for idx, frame in enumerate(frames):
                points = frame_landmarks(frame)
                if len(points) <= max(finger_a, finger_b, 12):
                    continue
                times.append(fnum(frame.get("Time"), idx / 30.0))
                selected_distances.append(dist(points[finger_a], points[finger_b]))
                thumb_index.append(dist(points[4], points[8]))
                thumb_middle.append(dist(points[4], points[12]))
                palms.append(palm_center(points))
            if len(selected_distances) < 2:
                continue
            palm_net = dist(palms[0], palms[-1]) if palms else 0.0
            palm_path = sum(dist(palms[i], palms[i - 1]) for i in range(1, len(palms)))
            rows.append(
                {
                    "gesture_id": gesture_id,
                    "display_name": display,
                    "sample_id": sample.get("SampleId", ""),
                    "frames": len(selected_distances),
                    "duration": round4((times[-1] - times[0]) if len(times) > 1 else fnum(sample.get("DurationSeconds"))),
                    "selected_finger_pair": f"{finger_a}-{finger_b}",
                    "selected_distance_start": round4(selected_distances[0]),
                    "selected_distance_end": round4(selected_distances[-1]),
                    "selected_distance_delta": round4(selected_distances[-1] - selected_distances[0]),
                    "selected_distance_path": round4(path(selected_distances)),
                    "selected_peak_velocity": round4(peak_velocity(selected_distances, times)),
                    "selected_oscillations": oscillations(selected_distances),
                    "thumb_index_delta": round4(thumb_index[-1] - thumb_index[0]),
                    "thumb_middle_delta": round4(thumb_middle[-1] - thumb_middle[0]),
                    "palm_net_distance": round4(palm_net),
                    "palm_path_length": round4(palm_path),
                    "finger_to_palm_path_ratio": round4(safe_div(path(selected_distances), palm_path)),
                }
            )
    write_csv(OUT / "finger_level_feature_summary.csv", rows)

    by_gesture: dict[str, list[dict[str, object]]] = defaultdict(list)
    for row in rows:
        by_gesture[str(row["gesture_id"])].append(row)
    gesture_rows = []
    for gesture_id, group in sorted(by_gesture.items()):
        gesture_rows.append(
            {
                "gesture_id": gesture_id,
                "samples": len(group),
                "mean_abs_selected_distance_delta": round4(mean(abs(fnum(r["selected_distance_delta"])) for r in group)),
                "mean_selected_distance_path": round4(mean(fnum(r["selected_distance_path"]) for r in group)),
                "mean_palm_path_length": round4(mean(fnum(r["palm_path_length"]) for r in group)),
                "mean_finger_to_palm_path_ratio": round4(mean(fnum(r["finger_to_palm_path_ratio"]) for r in group)),
                "max_selected_peak_velocity": round4(max(fnum(r["selected_peak_velocity"]) for r in group)),
                "mean_oscillations": round4(mean(fnum(r["selected_oscillations"]) for r in group)),
            }
        )
    write_csv(OUT / "finger_level_feature_by_gesture.csv", gesture_rows)
    return {
        "custom_gesture_feature_samples": len(rows),
        "custom_gesture_feature_gestures": len(by_gesture),
    }


def write_static_dynamic_comparison() -> dict[str, object]:
    rows = [
        {
            "gesture": "swipe_lr / swipe_rl",
            "single_static_frame": "low",
            "two_static_sequence": "medium",
            "dynamic_trajectory": "high",
            "finger_level_features": "low",
            "recommended_method": "PalmTrajectory + time window + cooldown",
            "reason": "The discriminative cue is palm displacement direction and duration.",
        },
        {
            "gesture": "point_to_fist",
            "single_static_frame": "low",
            "two_static_sequence": "high",
            "dynamic_trajectory": "medium",
            "finger_level_features": "medium",
            "recommended_method": "PoseTransition + max palm motion constraint",
            "reason": "The key cue is a stable pose transition rather than a long palm path.",
        },
        {
            "gesture": "finger_snap",
            "single_static_frame": "low",
            "two_static_sequence": "medium",
            "dynamic_trajectory": "low",
            "finger_level_features": "high",
            "recommended_method": "FingerDistanceChange / FeatureSequence",
            "reason": "The cue is fast thumb-middle or thumb-index distance change with limited palm movement.",
        },
        {
            "gesture": "two_finger_spread",
            "single_static_frame": "low",
            "two_static_sequence": "medium",
            "dynamic_trajectory": "low",
            "finger_level_features": "high",
            "recommended_method": "FingerDistanceChange",
            "reason": "The cue is continuous increase of the selected fingertip distance.",
        },
        {
            "gesture": "crab_pinch_simulation",
            "single_static_frame": "low",
            "two_static_sequence": "medium",
            "dynamic_trajectory": "low",
            "finger_level_features": "high",
            "recommended_method": "FingerOscillation + oscillation count",
            "reason": "The cue is repeated opening and closing of two fingers, which requires temporal oscillation features.",
        },
    ]
    write_csv(OUT / "static_vs_dynamic_comparison.csv", rows)
    return {"static_dynamic_comparison_items": len(rows)}


def write_paper_tables() -> None:
    overview = {row["metric"]: row["value"] for row in read_csv(OUT / "experiment_overview.csv")}
    table_lines = [
        "# 论文可用实验表格摘录",
        "",
        "## 表 A 数据集构成与筛选结果",
        "",
        "| 数据项 | 数值 | 说明 |",
        "|---|---:|---|",
        f"| Jester-300 挖掘记录 | {overview.get('mined_rows')} | 从 Jester 样本目录中进行动态位移挖掘 |",
        f"| 接受样本 | {overview.get('accepted_rows')} | 满足手部检测和动态位移规则的样本 |",
        f"| 接受率 | {overview.get('accepted_rate')} | accepted / mined |",
        f"| 外部模板回放子集 | {overview.get('subset_clips')} | 8 类方向性动态手势，训练/测试各 {overview.get('train_clips')}/{overview.get('test_clips')} |",
        f"| AVI-200 扩展回放 clips | {overview.get('dataset_clips')} | 用于端到端回放和性能统计 |",
        f"| 采样帧 | {overview.get('sampled_frames')} | AVI-200 采样总帧数 |",
        f"| 有效检测帧 | {overview.get('detected_frames')} | MediaPipe 检测到手部关键点的帧 |",
        f"| 有效帧比例 | {overview.get('detected_frame_rate')} | detected / sampled |",
        "",
        "## 表 B 外部模板动态手势识别结果",
        "",
        "| 手势 | 样本数 | Precision | Recall | F1 | 漏检率 |",
        "|---|---:|---:|---:|---:|---:|",
    ]
    for row in read_csv(OUT / "gesture_precision_recall_f1.csv"):
        table_lines.append(
            f"| {row['gesture']} | {row['support']} | {row['precision']} | {row['recall']} | {row['f1']} | {row['miss_rate']} |"
        )

    table_lines.extend(
        [
            "",
            "## 表 C 输入模式性能对比",
            "",
            "| 模式 | 运行次数 | 平均 FPS | P95 帧时(ms) | 最小 FPS | 平均包间隔(ms) | 估计链路延迟(ms) |",
            "|---|---:|---:|---:|---:|---:|---:|",
        ]
    )
    for row in read_csv(OUT / "input_mode_performance_summary.csv"):
        table_lines.append(
            f"| {row['mode']} | {row['runs']} | {row['average_fps_mean']} | {row['p95_frame_ms_mean']} | {row['min_fps_mean']} | {row['avg_packet_interval_ms_mean']} | {row['avg_estimated_latency_ms_mean']} |"
        )

    table_lines.extend(
        [
            "",
            "## 表 D YOLO 外部桥接可行性统计",
            "",
            "| 指标 | 数值 | 解释 |",
            "|---|---:|---|",
            f"| 参考视频数 | {overview.get('yolo_videos')} | 从自定义手势参考视频生成 |",
            f"| 进程成功数 | {overview.get('yolo_success_videos')} | 桥接程序完成处理的视频数 |",
            f"| 检测到手部关键点的视频数 | {overview.get('yolo_hand_positive_videos')} | 后续可进入关键点/规则识别的视频 |",
            f"| 未检测到手部关键点的视频数 | {overview.get('yolo_zero_hand_videos')} | 说明该路线仍需改进裁剪、阈值或样本质量 |",
            f"| 平均 hand_ratio | {overview.get('yolo_avg_hand_ratio')} | hand_frames / frames |",
            f"| 平均 FPS | {overview.get('yolo_avg_fps')} | 离线桥接处理速度 |",
            "",
            "## 表 E 静态连续识别与动态特征方案对照",
            "",
            "| 手势 | 单帧静态 | 两静态连续 | 手掌轨迹 | 手指级特征 | 推荐方案 |",
            "|---|---|---|---|---|---|",
        ]
    )
    for row in read_csv(OUT / "static_vs_dynamic_comparison.csv"):
        table_lines.append(
            f"| {row['gesture']} | {row['single_static_frame']} | {row['two_static_sequence']} | {row['dynamic_trajectory']} | {row['finger_level_features']} | {row['recommended_method']} |"
        )

    (OUT / "论文可用实验表格.md").write_text("\n".join(table_lines) + "\n", encoding="utf-8")


def write_markdown(summary: dict[str, object]) -> None:
    lines = [
        "# 论文实验补充汇总",
        "",
        f"生成时间：{datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
        "",
        "## 一、数据集与样本筛选",
        "",
        f"- Jester-300 挖掘记录：{summary['mined_rows']} 条，接受 {summary['accepted_rows']} 条，接受率 {summary['accepted_rate']}。",
        f"- Jester-300 外部模板回放子集：{summary['subset_clips']} 个 clip，其中训练 {summary['train_clips']} 个、测试 {summary['test_clips']} 个，覆盖 8 类方向性动态手势。",
        f"- AVI-200 扩展回放集：生成 {summary['dataset_clips']} 个 clip，总回放帧 {summary['total_replay_frames']} 帧，用于性能与端到端回放验证。",
        f"- AVI-200 采样帧 {summary['sampled_frames']} 帧，MediaPipe 有效检测帧 {summary['detected_frames']} 帧，有效率 {summary['detected_frame_rate']}。",
        "",
        "## 二、动态手势识别准确性",
        "",
        f"- 外部模板回放验证 clip：{summary['validation_clips']} 个，正确 {summary['validation_correct']} 个，准确率 {summary['validation_accuracy']}。",
        f"- Micro Precision={summary['micro_precision']}，Micro Recall={summary['micro_recall']}，Micro F1={summary['micro_f1']}。",
        "- 详细结果见 gesture_precision_recall_f1.csv 与 gesture_confusion_matrix_long.csv。",
        "",
        "## 三、YOLO 外部桥接",
        "",
        f"- 参考视频 {summary['yolo_videos']} 个，进程成功 {summary['yolo_success_videos']} 个。",
        f"- 检测到手部关键点的视频 {summary['yolo_hand_positive_videos']} 个，零手部关键点视频 {summary['yolo_zero_hand_videos']} 个。",
        f"- 平均 hand_ratio={summary['yolo_avg_hand_ratio']}，平均 FPS={summary['yolo_avg_fps']}。",
        "- 结论：YOLO + MediaPipe 路线可作为外部桥接可行性证据，但当前不能表述为已经完成高精度 YOLO 动态手势分类器。",
        "",
        "## 四、输入模式性能",
        "",
        f"- 共统计性能运行 {summary['performance_runs']} 条，模式分布：{summary['performance_modes']}。",
        "- 详细结果见 input_mode_performance_summary.csv。",
        "",
        "## 五、手指级动态特征",
        "",
        f"- 从自定义手势库提取 {summary['custom_gesture_feature_samples']} 个样本，覆盖 {summary['custom_gesture_feature_gestures']} 个手势。",
        "- 输出包含拇指-食指距离、拇指-中指距离、选定指尖对距离变化、峰值速度、振荡次数、手掌路径长度等指标。",
        "- 这些结果用于支撑响指、双指缩放、模拟夹动等手指级动态手势，而不只是手掌位移。",
        "",
        "## 六、静态连续识别与动态特征对照",
        "",
        f"- 已生成 {summary['static_dynamic_comparison_items']} 个典型手势的方案对照项。",
        "- 结论：状态切换明显的动作可以使用两静态手势连续识别；响指、双指缩放、模拟夹动等细粒度动作需要指尖距离、速度或振荡次数等动态特征。",
        "",
        "## 论文引用建议",
        "",
        "1. 第 6 章新增“数据集构成与样本筛选”表，引用 dataset_label_distribution.csv 和 dataset_split_distribution.csv。",
        "2. 第 6 章新增“动态手势识别准确性”表，引用 gesture_precision_recall_f1.csv。",
        "3. 第 4 章或第 6 章新增“手指级动态特征”说明，引用 finger_level_feature_by_gesture.csv。",
        "4. YOLO 部分应定位为外部桥接可行性实验，避免写成完整 YOLO 训练成果。",
        "5. 可将 论文可用实验表格.md 中的表格直接转入第 6 章。",
        "",
    ]
    (OUT / "实验补充汇总.md").write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    summary: dict[str, object] = {}
    summary.update(summarize_dataset())
    summary.update(summarize_validation())
    summary.update(summarize_yolo())
    summary.update(summarize_performance())
    summary.update(summarize_custom_gesture_features())
    summary.update(write_static_dynamic_comparison())
    write_csv(OUT / "experiment_overview.csv", [{"metric": key, "value": value} for key, value in summary.items()])
    write_paper_tables()
    write_markdown(summary)
    print(OUT)


if __name__ == "__main__":
    main()
