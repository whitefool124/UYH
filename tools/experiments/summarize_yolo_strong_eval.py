from __future__ import annotations

import csv
from pathlib import Path
from statistics import mean

from PIL import Image, ImageDraw, ImageFont


DATA = Path("论文材料") / "实验补充数据" / "yolo_roi_strong_eval.csv"
SUMMARY = Path("论文材料") / "实验补充数据" / "yolo_roi_strong_summary.csv"
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


def chart_ratio(rows):
    labels = [r["video_name"].replace(".mp4", "") for r in rows]
    values = [float(r["yolo_person_ratio"]) for r in rows]
    width, height = 1600, 900
    img = Image.new("RGB", (width, height), "white")
    draw = ImageDraw.Draw(img)
    center(draw, (width / 2, 58), "YOLO 前置检测 person ROI 检出比例", f(42, True))
    left, top, right, bottom = 145, 145, 70, 210
    cw, ch = width - left - right, height - top - bottom
    for i in range(5):
        y = top + ch - ch * i / 4
        draw.line([(left, y), (width - right, y)], fill="#d9e0e8", width=2)
        draw.text((left - 78, y - 14), f"{i / 4:.2f}", font=f(22), fill="#52606d")
    group = cw / len(values)
    bw = group * 0.55
    for i, (label, value) in enumerate(zip(labels, values)):
        x = left + group * i + group / 2 - bw / 2
        h = ch * value
        y = top + ch - h
        draw.rounded_rectangle([x, y, x + bw, top + ch], radius=8, fill="#3a8f6b")
        center(draw, (x + bw / 2, y - 24), f"{value:.2f}", f(20))
        center(draw, (x + bw / 2, top + ch + 52), label[:16], f(18))
    path = ASSET / "yolo_roi_person_ratio.png"
    img.save(path)
    print(path)


def chart_speed_conf(rows):
    labels = [r["video_name"].replace(".mp4", "") for r in rows]
    fps = [float(r["average_fps"]) for r in rows]
    conf = [float(r["avg_yolo_confidence"]) * 20 for r in rows]
    width, height = 1600, 900
    img = Image.new("RGB", (width, height), "white")
    draw = ImageDraw.Draw(img)
    center(draw, (width / 2, 58), "YOLO 前置检测速度与置信度对比", f(42, True))
    left, top, right, bottom = 145, 145, 70, 210
    cw, ch = width - left - right, height - top - bottom
    max_v = max(max(fps), max(conf)) * 1.18
    for i in range(5):
        y = top + ch - ch * i / 4
        val = max_v * i / 4
        draw.line([(left, y), (width - right, y)], fill="#d9e0e8", width=2)
        draw.text((left - 78, y - 14), f"{val:.1f}", font=f(22), fill="#52606d")
    draw.rectangle([width - 390, 103, width - 365, 128], fill="#2f6f9f")
    draw.text((width - 355, 98), "FPS", font=f(22), fill="#24313d")
    draw.rectangle([width - 270, 103, width - 245, 128], fill="#d08a2e")
    draw.text((width - 235, 98), "置信度×20", font=f(22), fill="#24313d")
    group = cw / len(labels)
    bw = group * 0.22
    for i, label in enumerate(labels):
        cx = left + group * i + group / 2
        for dx, value, color in [(-bw * 0.65, fps[i], "#2f6f9f"), (bw * 0.65, conf[i], "#d08a2e")]:
            h = ch * value / max_v
            x = cx + dx - bw / 2
            y = top + ch - h
            draw.rounded_rectangle([x, y, x + bw, top + ch], radius=6, fill=color)
        center(draw, (cx, top + ch + 52), label[:16], f(18))
    path = ASSET / "yolo_roi_speed_confidence.png"
    img.save(path)
    print(path)


def write_summary(rows):
    frames = sum(int(r["frames"]) for r in rows)
    detected = sum(int(r["yolo_person_frames"]) for r in rows)
    summary = {
        "videos": len(rows),
        "frames": frames,
        "yolo_person_frames": detected,
        "weighted_person_ratio": detected / frames if frames else 0.0,
        "avg_video_person_ratio": mean(float(r["yolo_person_ratio"]) for r in rows),
        "avg_yolo_confidence": mean(float(r["avg_yolo_confidence"]) for r in rows),
        "avg_person_area_ratio": mean(float(r["avg_person_area_ratio"]) for r in rows),
        "avg_processing_ms": mean(float(r["avg_processing_ms"]) for r in rows),
        "avg_fps": mean(float(r["average_fps"]) for r in rows),
    }
    with SUMMARY.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=["metric", "value"])
        writer.writeheader()
        for key, value in summary.items():
            writer.writerow({"metric": key, "value": round(value, 4) if isinstance(value, float) else value})
    print(SUMMARY)


def main():
    ASSET.mkdir(parents=True, exist_ok=True)
    rows = read_rows()
    chart_ratio(rows)
    chart_speed_conf(rows)
    write_summary(rows)


if __name__ == "__main__":
    main()
