from __future__ import annotations

import csv
import html
import math
from pathlib import Path


ROOT = Path("论文材料") / "实验补充数据"
OUT = Path("毕业设计提交归档-曹逸天") / "论文图表与实验数据" / "ThesisAssets" / "experiment_charts"
TABLE_OUT = Path("毕业设计提交归档-曹逸天") / "论文图表与实验数据" / "ThesisAssets" / "experiment_tables"


def rows(name: str) -> list[dict[str, str]]:
    with (ROOT / name).open("r", encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))


def write_svg(name: str, body: str, width: int = 920, height: int = 560) -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    svg = f"""<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">
<rect width="100%" height="100%" fill="#ffffff"/>
<style>
text {{ font-family: "Microsoft YaHei", "SimHei", Arial, sans-serif; fill: #24313d; }}
.title {{ font-size: 24px; font-weight: 700; }}
.label {{ font-size: 15px; }}
.small {{ font-size: 13px; fill: #52606d; }}
.axis {{ stroke: #9aa6b2; stroke-width: 1; }}
.grid {{ stroke: #d8dee6; stroke-width: 1; }}
</style>
{body}
</svg>
"""
    path = OUT / name
    path.write_text(svg, encoding="utf-8")
    print(path)


def esc(value: object) -> str:
    return html.escape(str(value))


def bar_chart(name: str, title: str, labels: list[str], values: list[float], ylabel: str, color: str) -> None:
    width, height = 920, 520
    left, top, right, bottom = 110, 78, 45, 90
    chart_w = width - left - right
    chart_h = height - top - bottom
    max_v = max(values) if values else 1
    max_v = max_v * 1.15 if max_v > 0 else 1
    bar_w = chart_w / len(values) * 0.58
    gap = chart_w / len(values)
    parts = [f'<text x="{width/2}" y="38" text-anchor="middle" class="title">{esc(title)}</text>']
    for i in range(5):
        y = top + chart_h - chart_h * i / 4
        val = max_v * i / 4
        parts.append(f'<line x1="{left}" y1="{y:.1f}" x2="{width-right}" y2="{y:.1f}" class="grid"/>')
        parts.append(f'<text x="{left-12}" y="{y+5:.1f}" text-anchor="end" class="small">{val:.1f}</text>')
    parts.append(f'<line x1="{left}" y1="{top}" x2="{left}" y2="{top+chart_h}" class="axis"/>')
    parts.append(f'<line x1="{left}" y1="{top+chart_h}" x2="{width-right}" y2="{top+chart_h}" class="axis"/>')
    parts.append(f'<text x="28" y="{top+chart_h/2}" transform="rotate(-90 28 {top+chart_h/2})" class="label">{esc(ylabel)}</text>')
    for i, (label, value) in enumerate(zip(labels, values)):
        x = left + gap * i + gap / 2 - bar_w / 2
        h = chart_h * value / max_v
        y = top + chart_h - h
        parts.append(f'<rect x="{x:.1f}" y="{y:.1f}" width="{bar_w:.1f}" height="{h:.1f}" fill="{color}" rx="3"/>')
        parts.append(f'<text x="{x+bar_w/2:.1f}" y="{y-8:.1f}" text-anchor="middle" class="small">{value:.3g}</text>')
        parts.append(f'<text x="{x+bar_w/2:.1f}" y="{top+chart_h+28}" text-anchor="middle" class="small">{esc(label)}</text>')
    write_svg(name, "\n".join(parts), width, height)


def chart_performance() -> None:
    data = rows("input_mode_performance_summary.csv")
    modes = [r["mode"] for r in data]
    fps = [float(r["average_fps_mean"]) for r in data]
    p95 = [float(r["p95_frame_ms_mean"]) for r in data]
    width, height = 920, 540
    left, top, right, bottom = 95, 82, 70, 92
    chart_w = width - left - right
    chart_h = height - top - bottom
    max_v = max(max(fps), max(p95)) * 1.25
    group_w = chart_w / len(modes)
    bar_w = group_w * 0.26
    parts = [f'<text x="{width/2}" y="38" text-anchor="middle" class="title">三种输入模式实时性能对比</text>']
    for i in range(5):
        y = top + chart_h - chart_h * i / 4
        val = max_v * i / 4
        parts.append(f'<line x1="{left}" y1="{y:.1f}" x2="{width-right}" y2="{y:.1f}" class="grid"/>')
        parts.append(f'<text x="{left-12}" y="{y+5:.1f}" text-anchor="end" class="small">{val:.1f}</text>')
    parts.append(f'<line x1="{left}" y1="{top+chart_h}" x2="{width-right}" y2="{top+chart_h}" class="axis"/>')
    for i, mode in enumerate(modes):
        cx = left + group_w * i + group_w / 2
        for offset, value, color, label in [(-bar_w / 1.8, fps[i], "#2f6f9f", "FPS"), (bar_w / 1.8, p95[i], "#d08a2e", "P95")]:
            h = chart_h * value / max_v
            x = cx + offset - bar_w / 2
            y = top + chart_h - h
            parts.append(f'<rect x="{x:.1f}" y="{y:.1f}" width="{bar_w:.1f}" height="{h:.1f}" fill="{color}" rx="3"/>')
            parts.append(f'<text x="{x+bar_w/2:.1f}" y="{y-7:.1f}" text-anchor="middle" class="small">{value:.1f}</text>')
        parts.append(f'<text x="{cx:.1f}" y="{top+chart_h+30}" text-anchor="middle" class="small">{esc(mode)}</text>')
    parts.append(f'<rect x="{width-210}" y="58" width="16" height="16" fill="#2f6f9f"/><text x="{width-188}" y="71" class="small">平均 FPS</text>')
    parts.append(f'<rect x="{width-110}" y="58" width="16" height="16" fill="#d08a2e"/><text x="{width-88}" y="71" class="small">P95(ms)</text>')
    write_svg("input_mode_performance.svg", "\n".join(parts), width, height)


def chart_yolo() -> None:
    summary = {r["metric"]: float(r["value"]) for r in rows("yolo_bridge_summary.csv")}
    positive = summary["hand_positive_videos"]
    zero = summary["zero_hand_videos"]
    total = positive + zero
    width, height = 760, 460
    cx, cy, r = 240, 245, 135
    angle = 2 * math.pi * positive / total
    x1, y1 = cx, cy - r
    x2, y2 = cx + r * math.sin(angle), cy - r * math.cos(angle)
    large = 1 if angle > math.pi else 0
    parts = [f'<text x="{width/2}" y="38" text-anchor="middle" class="title">YOLO+MediaPipe 外部桥接有效检出分布</text>']
    parts.append(f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="#b35f5f"/>')
    parts.append(f'<path d="M {cx} {cy} L {x1:.1f} {y1:.1f} A {r} {r} 0 {large} 1 {x2:.1f} {y2:.1f} Z" fill="#3a8f6b"/>')
    parts.append(f'<text x="{cx}" y="{cy+5}" text-anchor="middle" class="title">{int(total)} 个</text>')
    parts.append(f'<rect x="470" y="170" width="18" height="18" fill="#3a8f6b"/><text x="500" y="185" class="label">检出手部关键点：{int(positive)} 个 ({positive/total:.1%})</text>')
    parts.append(f'<rect x="470" y="220" width="18" height="18" fill="#b35f5f"/><text x="500" y="235" class="label">未检出手部关键点：{int(zero)} 个 ({zero/total:.1%})</text>')
    parts.append(f'<text x="470" y="285" class="small">平均 hand_ratio：{summary["avg_hand_ratio"]:.4f}</text>')
    parts.append(f'<text x="470" y="315" class="small">平均 FPS：{summary["avg_fps"]:.4f}</text>')
    write_svg("yolo_bridge_detection_distribution.svg", "\n".join(parts), width, height)


def chart_rejections() -> None:
    data = rows("dataset_rejection_reasons.csv")
    labels = [r["reason"] for r in data]
    values = [int(r["count"]) for r in data]
    bar_chart("dataset_rejection_reasons.svg", "Jester 样本筛选拒绝原因统计", labels, values, "样本数", "#6d7f9d")


def chart_recognition() -> None:
    data = rows("gesture_precision_recall_f1.csv")
    labels = [r["gesture"].replace("motion_", "") for r in data]
    values = [float(r["f1"]) for r in data]
    bar_chart("gesture_replay_f1.svg", "外部模板回放动态手势识别 F1", labels, values, "F1", "#4a7c59")


def chart_finger_features() -> None:
    data = rows("finger_level_feature_summary.csv")
    wanted = ["ext_two_finger_spread_easy", "ext_motion_right_right_short", "ext_motion_up_right_short", "ext_horizontal_wave_easy"]
    picked = []
    seen = set()
    for r in data:
        if r["gesture_id"] in wanted and r["gesture_id"] not in seen:
            picked.append(r)
            seen.add(r["gesture_id"])
    labels = [r["gesture_id"].replace("ext_", "").replace("_easy", "") for r in picked]
    values = [float(r["finger_to_palm_path_ratio"]) for r in picked]
    bar_chart("finger_to_palm_ratio_examples.svg", "手指级变化与掌心轨迹比例示例", labels, values, "指尖路径/掌心路径", "#8a6fb0")


def write_conclusions() -> None:
    overview = {r["metric"]: r["value"] for r in rows("experiment_overview.csv")}
    perf = rows("input_mode_performance_summary.csv")
    yolo = {r["metric"]: r["value"] for r in rows("yolo_bridge_summary.csv")}
    TABLE_OUT.mkdir(parents=True, exist_ok=True)
    text = f"""# 第 6 章可补充的数据图表与结论

## 可新增图表

- 图 6-x 三种输入模式实时性能对比：`experiment_charts/input_mode_performance.svg`
- 图 6-x YOLO+MediaPipe 外部桥接有效检出分布：`experiment_charts/yolo_bridge_detection_distribution.svg`
- 图 6-x Jester 样本筛选拒绝原因统计：`experiment_charts/dataset_rejection_reasons.svg`
- 图 6-x 外部模板回放动态手势识别 F1：`experiment_charts/gesture_replay_f1.svg`
- 图 6-x 手指级变化与掌心轨迹比例示例：`experiment_charts/finger_to_palm_ratio_examples.svg`

## 可直接写入论文的结论文字

1. 数据集筛选方面，本次补充实验从 Jester 样本中挖掘 {overview.get("mined_rows")} 条记录，接受 {overview.get("accepted_rows")} 条，接受率为 {overview.get("accepted_rate")}；AVI-200 扩展回放共采样 {overview.get("sampled_frames")} 帧，其中 MediaPipe 有效检出 {overview.get("detected_frames")} 帧，有效帧比例为 {overview.get("detected_frame_rate")}。这说明离线回放实验具备一定样本规模，但仍受到手部检出连续性和动作标签质量影响。

2. 动态识别方面，外部模板回放验证共覆盖 {overview.get("validation_clips")} 个测试 clip，正确 {overview.get("validation_correct")} 个，Micro-F1 为 {overview.get("micro_f1")}。该结果说明在严格筛选和模板一致的离线回放子集上，基于时间窗与阈值的轨迹识别能够稳定工作；但该结论不等同于真实摄像头长期用户测试。

3. 性能方面，三种输入模式共统计 {overview.get("performance_runs")} 条运行记录。Mock 平均 FPS 为 {perf[0]["average_fps_mean"]}，Native MediaPipe 平均 FPS 为 {perf[1]["average_fps_mean"]}，ExternalBridge 平均 FPS 为 {perf[2]["average_fps_mean"]}，ExternalBridge 平均包间隔为 {perf[2]["avg_packet_interval_ms_mean"]} ms、估算链路延迟为 {perf[2]["avg_estimated_latency_ms_mean"]} ms。结果表明外部桥接链路可以进入统一性能监控，并具备演示级实时性。

4. YOLO 外部桥接方面，18 个参考视频均完成处理，其中 {yolo.get("hand_positive_videos")} 个视频检出手部关键点，{yolo.get("zero_hand_videos")} 个视频未检出，平均 hand_ratio 为 {yolo.get("avg_hand_ratio")}，平均 FPS 为 {yolo.get("avg_fps")}。因此，YOLO+MediaPipe 当前应表述为外部桥接可行性和后续优化方向，不能表述为已完成高精度 YOLO 动态手势分类器。

5. 手指级特征方面，双指外滑、响指、模拟夹动等动作更依赖指尖距离变化、峰值速度和振荡次数，不能完全依靠掌心位移判断。该结果支持第 4 章中将动态手势分为掌心轨迹、姿态转换和手指级特征序列三类处理的设计。
"""
    path = TABLE_OUT / "thesis_chart_conclusions.md"
    path.write_text(text, encoding="utf-8")
    print(path)


def main() -> None:
    chart_performance()
    chart_yolo()
    chart_rejections()
    chart_recognition()
    chart_finger_features()
    write_conclusions()


if __name__ == "__main__":
    main()
