from __future__ import annotations

import csv
from pathlib import Path
from statistics import mean

from PIL import Image, ImageDraw, ImageFont


DATA = Path("论文材料") / "实验补充数据" / "yolo_roi_jester300_eval.csv"
SUMMARY = Path("论文材料") / "实验补充数据" / "yolo_roi_jester300_summary.csv"
ASSET = Path("毕业设计提交归档-曹逸天") / "论文图表与实验数据" / "ThesisAssets" / "experiment_charts_png"
FONT = Path("C:/Windows/Fonts/msyh.ttc")
BOLD = Path("C:/Windows/Fonts/msyhbd.ttc")


def f(size: int, bold: bool = False):
    return ImageFont.truetype(str(BOLD if bold else FONT), size)


def rows() -> list[dict[str, str]]:
    with DATA.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))


def center(draw, xy, text, font, fill="#24313d"):
    box = draw.textbbox((0, 0), text, font=font)
    draw.text((xy[0] - (box[2] - box[0]) / 2, xy[1] - (box[3] - box[1]) / 2), text, font=font, fill=fill)


def bar(path: Path, title: str, labels: list[str], values: list[float], color: str, ymax=None):
    width, height = 1450, 830
    img = Image.new("RGB", (width, height), "white")
    draw = ImageDraw.Draw(img)
    center(draw, (width / 2, 58), title, f(40, True))
    left, top, right, bottom = 155, 145, 85, 155
    cw, ch = width - left - right, height - top - bottom
    max_v = ymax if ymax is not None else max(values) * 1.18
    for i in range(5):
        y = top + ch - ch * i / 4
        val = max_v * i / 4
        draw.line([(left, y), (width - right, y)], fill="#d9e0e8", width=2)
        draw.text((left - 88, y - 14), f"{val:.2f}", font=f(21), fill="#52606d")
    group = cw / len(values)
    bw = min(group * 0.55, 140)
    for i, (label, value) in enumerate(zip(labels, values)):
        x = left + group * i + group / 2 - bw / 2
        h = ch * value / max_v if max_v else 0
        y = top + ch - h
        draw.rounded_rectangle([x, y, x + bw, top + ch], radius=8, fill=color)
        center(draw, (x + bw / 2, y - 24), f"{value:.3g}", f(20))
        center(draw, (x + bw / 2, top + ch + 44), label, f(21))
    img.save(path)
    print(path)


def main():
    ASSET.mkdir(parents=True, exist_ok=True)
    data = rows()
    total_frames = sum(int(r["frames"]) for r in data)
    detected = sum(int(r["yolo_person_frames"]) for r in data)
    ratios = [float(r["yolo_person_ratio"]) for r in data]
    positive = [r for r in data if int(r["yolo_person_frames"]) > 0]
    stats = {
        "clips": len(data),
        "frames": total_frames,
        "sampled_frames_per_clip": 8,
        "yolo_person_frames": detected,
        "weighted_person_ratio": detected / total_frames if total_frames else 0.0,
        "positive_clips": len(positive),
        "zero_detect_clips": len(data) - len(positive),
        "positive_clip_rate": len(positive) / len(data) if data else 0.0,
        "avg_clip_person_ratio": mean(ratios),
        "avg_yolo_confidence_positive": mean(float(r["avg_yolo_confidence"]) for r in positive) if positive else 0.0,
        "avg_person_area_ratio_positive": mean(float(r["avg_person_area_ratio"]) for r in positive) if positive else 0.0,
        "avg_processing_ms": mean(float(r["avg_processing_ms"]) for r in data),
        "avg_fps": mean(float(r["average_fps"]) for r in data),
    }
    buckets = {
        "0": 0,
        "(0,0.25)": 0,
        "[0.25,0.5)": 0,
        "[0.5,0.75)": 0,
        "[0.75,1.0]": 0,
    }
    for value in ratios:
        if value == 0:
            buckets["0"] += 1
        elif value < 0.25:
            buckets["(0,0.25)"] += 1
        elif value < 0.5:
            buckets["[0.25,0.5)"] += 1
        elif value < 0.75:
            buckets["[0.5,0.75)"] += 1
        else:
            buckets["[0.75,1.0]"] += 1
    bar(
        ASSET / "yolo_jester300_clip_ratio_distribution.png",
        "Jester-300 YOLO ROI clip 检出比例分布",
        list(buckets.keys()),
        [float(v) for v in buckets.values()],
        "#6d7f9d",
    )
    bar(
        ASSET / "yolo_jester300_summary_rates.png",
        "Jester-300 YOLO ROI 总体检出指标",
        ["帧检出率", "clip阳性率", "平均clip检出率"],
        [stats["weighted_person_ratio"], stats["positive_clip_rate"], stats["avg_clip_person_ratio"]],
        "#3a8f6b",
        ymax=1.05,
    )
    with SUMMARY.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=["metric", "value"])
        writer.writeheader()
        for key, value in stats.items():
            writer.writerow({"metric": key, "value": round(value, 4) if isinstance(value, float) else value})
    print(SUMMARY)


if __name__ == "__main__":
    main()
