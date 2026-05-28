from __future__ import annotations

import csv
from pathlib import Path
from statistics import mean

from PIL import Image, ImageDraw, ImageFont


DATA = Path("论文材料") / "实验补充数据" / "yolo_roi_expanded_eval.csv"
SUMMARY = Path("论文材料") / "实验补充数据" / "yolo_roi_expanded_summary.csv"
ASSET = Path("毕业设计提交归档-曹逸天") / "论文图表与实验数据" / "ThesisAssets" / "experiment_charts_png"
FONT = Path("C:/Windows/Fonts/msyh.ttc")
BOLD = Path("C:/Windows/Fonts/msyhbd.ttc")


def f(size: int, bold: bool = False):
    return ImageFont.truetype(str(BOLD if bold else FONT), size)


def read_rows() -> list[dict[str, str]]:
    with DATA.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def center(draw, xy, text, font, fill="#24313d"):
    box = draw.textbbox((0, 0), text, font=font)
    draw.text((xy[0] - (box[2] - box[0]) / 2, xy[1] - (box[3] - box[1]) / 2), text, font=font, fill=fill)


def bar_chart(path: Path, title: str, labels: list[str], values: list[float], color: str, ymax: float | None = None):
    width, height = 1500, 850
    img = Image.new("RGB", (width, height), "white")
    draw = ImageDraw.Draw(img)
    center(draw, (width / 2, 58), title, f(40, True))
    left, top, right, bottom = 150, 140, 80, 160
    cw, ch = width - left - right, height - top - bottom
    max_v = ymax if ymax is not None else max(values) * 1.18
    for i in range(5):
        y = top + ch - ch * i / 4
        val = max_v * i / 4
        draw.line([(left, y), (width - right, y)], fill="#d9e0e8", width=2)
        draw.text((left - 86, y - 14), f"{val:.2f}", font=f(21), fill="#52606d")
    group = cw / len(values)
    bw = min(group * 0.55, 120)
    for i, (label, value) in enumerate(zip(labels, values)):
        x = left + group * i + group / 2 - bw / 2
        h = ch * value / max_v if max_v else 0
        y = top + ch - h
        draw.rounded_rectangle([x, y, x + bw, top + ch], radius=8, fill=color)
        center(draw, (x + bw / 2, y - 24), f"{value:.3g}", f(19))
        center(draw, (x + bw / 2, top + ch + 45), label, f(20))
    img.save(path)
    print(path)


def summary_stats(rows):
    frames = sum(int(r["frames"]) for r in rows)
    detected = sum(int(r["yolo_person_frames"]) for r in rows)
    video_rows = [r for r in rows if r["source_kind"] == "video"]
    window_rows = [r for r in rows if r["source_kind"] == "frame_window"]
    return {
        "clips": len(rows),
        "video_clips": len(video_rows),
        "frame_window_clips": len(window_rows),
        "frames": frames,
        "yolo_person_frames": detected,
        "weighted_person_ratio": detected / frames if frames else 0.0,
        "avg_clip_person_ratio": mean(float(r["yolo_person_ratio"]) for r in rows),
        "avg_yolo_confidence": mean(float(r["avg_yolo_confidence"]) for r in rows if float(r["avg_yolo_confidence"]) > 0),
        "avg_person_area_ratio": mean(float(r["avg_person_area_ratio"]) for r in rows if float(r["avg_person_area_ratio"]) > 0),
        "avg_processing_ms": mean(float(r["avg_processing_ms"]) for r in rows),
        "avg_fps": mean(float(r["average_fps"]) for r in rows),
        "video_weighted_ratio": sum(int(r["yolo_person_frames"]) for r in video_rows) / sum(int(r["frames"]) for r in video_rows),
        "window_weighted_ratio": sum(int(r["yolo_person_frames"]) for r in window_rows) / sum(int(r["frames"]) for r in window_rows),
        "zero_detect_clips": sum(1 for r in rows if int(r["yolo_person_frames"]) == 0),
    }


def chart_source_ratio(rows, stats):
    labels = ["视频样本", "参考帧窗口", "总体"]
    values = [stats["video_weighted_ratio"], stats["window_weighted_ratio"], stats["weighted_person_ratio"]]
    bar_chart(ASSET / "yolo_expanded_source_ratio.png", "扩展 YOLO ROI 实验检出比例", labels, values, "#3a8f6b", 1.05)


def chart_clip_distribution(rows):
    buckets = {"0": 0, "(0,0.5)": 0, "[0.5,0.9)": 0, "[0.9,1.0]": 0}
    for r in rows:
        value = float(r["yolo_person_ratio"])
        if value == 0:
            buckets["0"] += 1
        elif value < 0.5:
            buckets["(0,0.5)"] += 1
        elif value < 0.9:
            buckets["[0.5,0.9)"] += 1
        else:
            buckets["[0.9,1.0]"] += 1
    bar_chart(
        ASSET / "yolo_expanded_clip_ratio_distribution.png",
        "扩展 YOLO ROI 实验 clip 检出比例分布",
        list(buckets.keys()),
        [float(v) for v in buckets.values()],
        "#6d7f9d",
    )


def write_summary(stats):
    with SUMMARY.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=["metric", "value"])
        writer.writeheader()
        for key, value in stats.items():
            writer.writerow({"metric": key, "value": round(value, 4) if isinstance(value, float) else value})
    print(SUMMARY)


def main():
    ASSET.mkdir(parents=True, exist_ok=True)
    rows = read_rows()
    stats = summary_stats(rows)
    chart_source_ratio(rows, stats)
    chart_clip_distribution(rows)
    write_summary(stats)


if __name__ == "__main__":
    main()
