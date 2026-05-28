from pathlib import Path
from xml.sax.saxutils import escape


OUT = Path("unity-spell-guard/Docs/ThesisAssets/diagrams")


COLORS = {
    "bg": "#ffffff",
    "text": "#1f2937",
    "muted": "#6b7280",
    "line": "#64748b",
    "blue": "#dbeafe",
    "blue_stroke": "#2563eb",
    "green": "#dcfce7",
    "green_stroke": "#16a34a",
    "amber": "#fef3c7",
    "amber_stroke": "#d97706",
    "rose": "#ffe4e6",
    "rose_stroke": "#e11d48",
    "gray": "#f1f5f9",
    "gray_stroke": "#475569",
    "violet": "#ede9fe",
    "violet_stroke": "#7c3aed",
}


def svg_root(width, height, body):
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">
  <defs>
    <marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="8" markerHeight="8" orient="auto-start-reverse">
      <path d="M 0 0 L 10 5 L 0 10 z" fill="{COLORS['line']}"/>
    </marker>
    <style>
      .title {{ font: 700 24px "Microsoft YaHei", "Noto Sans CJK SC", Arial, sans-serif; fill: {COLORS['text']}; }}
      .subtitle {{ font: 400 13px "Microsoft YaHei", "Noto Sans CJK SC", Arial, sans-serif; fill: {COLORS['muted']}; }}
      .label {{ font: 700 15px "Microsoft YaHei", "Noto Sans CJK SC", Arial, sans-serif; fill: {COLORS['text']}; }}
      .small {{ font: 400 12px "Microsoft YaHei", "Noto Sans CJK SC", Arial, sans-serif; fill: {COLORS['muted']}; }}
      .tiny {{ font: 400 10px "Microsoft YaHei", "Noto Sans CJK SC", Arial, sans-serif; fill: {COLORS['muted']}; }}
      .box {{ rx: 8; ry: 8; stroke-width: 1.5; }}
      .line {{ stroke: {COLORS['line']}; stroke-width: 1.8; fill: none; marker-end: url(#arrow); }}
      .plainline {{ stroke: {COLORS['line']}; stroke-width: 1.4; fill: none; }}
    </style>
  </defs>
  <rect x="0" y="0" width="{width}" height="{height}" fill="{COLORS['bg']}"/>
{body}
</svg>
'''


def text(x, y, content, cls="label", anchor="middle"):
    return f'<text x="{x}" y="{y}" class="{cls}" text-anchor="{anchor}">{escape(content)}</text>'


def multiline(x, y, lines, cls="small", anchor="middle", line_height=16):
    return "\n".join(text(x, y + i * line_height, line, cls, anchor) for i, line in enumerate(lines))


def box(x, y, w, h, fill, stroke, title, lines=None):
    lines = lines or []
    cx = x + w / 2
    body = [
        f'<rect class="box" x="{x}" y="{y}" width="{w}" height="{h}" fill="{fill}" stroke="{stroke}"/>',
        text(cx, y + 28, title, "label"),
    ]
    if lines:
        body.append(multiline(cx, y + 50, lines, "small"))
    return "\n".join(body)


def arrow(x1, y1, x2, y2):
    return f'<path class="line" d="M {x1} {y1} L {x2} {y2}"/>'


def icon_camera(x, y, color="#2563eb"):
    return f'''<g transform="translate({x},{y})" stroke="{color}" fill="none" stroke-width="2">
  <rect x="2" y="8" width="30" height="20" rx="4"/>
  <path d="M10 8 L14 3 H22 L26 8"/>
  <circle cx="17" cy="18" r="6"/>
</g>'''


def icon_unity(x, y, color="#475569"):
    return f'''<g transform="translate({x},{y})" stroke="{color}" fill="none" stroke-width="2" stroke-linejoin="round">
  <path d="M17 3 L31 11 V27 L17 35 L3 27 V11 Z"/>
  <path d="M17 3 V18 L31 11"/>
  <path d="M17 18 L3 11"/>
  <path d="M17 18 V35"/>
</g>'''


def icon_udp(x, y, color="#d97706"):
    return f'''<g transform="translate({x},{y})" stroke="{color}" fill="none" stroke-width="2">
  <path d="M4 20 H34"/>
  <path d="M25 11 L34 20 L25 29"/>
  <path d="M4 10 H17"/>
  <path d="M4 30 H17"/>
</g>'''


def icon_chart(x, y, color="#16a34a"):
    return f'''<g transform="translate({x},{y})" stroke="{color}" fill="none" stroke-width="2">
  <path d="M5 32 V6"/>
  <path d="M5 32 H34"/>
  <rect x="10" y="20" width="5" height="12" fill="none"/>
  <rect x="19" y="14" width="5" height="18" fill="none"/>
  <rect x="28" y="9" width="5" height="23" fill="none"/>
</g>'''


def diagram_system_architecture():
    body = [
        text(520, 38, "系统总体架构图", "title"),
        text(520, 60, "从视觉输入到 Unity 游戏反馈的分层闭环", "subtitle"),
        box(40, 110, 180, 90, COLORS["blue"], COLORS["blue_stroke"], "输入层", ["RGB 摄像头", "Mock 键盘模拟", "外部视频/UDP"]),
        icon_camera(58, 132),
        box(270, 110, 190, 90, COLORS["green"], COLORS["green_stroke"], "视觉识别层", ["MediaPipe Hands", "ExternalVisionFrame", "手部/姿态关键点"]),
        box(510, 110, 190, 90, COLORS["amber"], COLORS["amber_stroke"], "手势抽象层", ["GestureFrame", "GestureCommand", "CommandHistory"]),
        box(750, 110, 190, 90, COLORS["rose"], COLORS["rose_stroke"], "玩法反馈层", ["菜单导航", "玩家移动", "施法/护盾/战斗"]),
        icon_unity(890, 137),
        arrow(220, 155, 270, 155),
        arrow(460, 155, 510, 155),
        arrow(700, 155, 750, 155),
        box(270, 285, 190, 80, COLORS["gray"], COLORS["gray_stroke"], "测试与工具", ["EditMode / PlayMode", "场景生成", "训练集校验"]),
        box(510, 285, 190, 80, COLORS["violet"], COLORS["violet_stroke"], "实验数据层", ["性能 CSV", "回放统计", "识别结果汇总"]),
        icon_chart(655, 306),
        arrow(365, 200, 365, 285),
        arrow(605, 200, 605, 285),
        arrow(460, 325, 510, 325),
    ]
    return svg_root(980, 420, "\n".join(body))


def diagram_input_pipeline():
    body = [
        text(520, 38, "多输入源统一链路图", "title"),
        text(520, 60, "Mock / Native MediaPipe / ExternalBridge 统一进入 GestureInputRouter", "subtitle"),
        box(60, 110, 180, 78, COLORS["gray"], COLORS["gray_stroke"], "Mock", ["键盘模拟", "开发与答辩兜底"]),
        box(60, 235, 180, 78, COLORS["blue"], COLORS["blue_stroke"], "Native MediaPipe", ["Unity 摄像头", "21 点手部关键点"]),
        box(60, 360, 180, 78, COLORS["amber"], COLORS["amber_stroke"], "ExternalBridge", ["Python / 离线回放", "UDP 视觉帧"]),
        box(330, 225, 210, 110, COLORS["green"], COLORS["green_stroke"], "GestureInputRouter", ["选择当前输入模式", "暴露统一快照", "输出统一命令"]),
        box(630, 155, 180, 78, COLORS["violet"], COLORS["violet_stroke"], "GestureFrame", ["hand present", "landmarks / pose", "confidence / time"]),
        box(630, 300, 180, 78, COLORS["rose"], COLORS["rose_stroke"], "GestureCommand", ["StaticPose", "MotionGesture", "triggered time"]),
        box(850, 225, 150, 110, COLORS["gray"], COLORS["gray_stroke"], "游戏系统", ["Player", "Combat", "UI", "HUD"]),
        icon_udp(103, 383),
        arrow(240, 149, 330, 260),
        arrow(240, 274, 330, 280),
        arrow(240, 399, 330, 300),
        arrow(540, 260, 630, 194),
        arrow(540, 300, 630, 339),
        arrow(810, 194, 850, 260),
        arrow(810, 339, 850, 300),
    ]
    return svg_root(1040, 500, "\n".join(body))


def diagram_motion_recognition():
    steps = [
        ("输入帧", ["GestureFrame", "关键点 / 手势 / 时间戳"], COLORS["blue"], COLORS["blue_stroke"]),
        ("历史窗口", ["0.7 秒队列", "过滤低置信度帧"], COLORS["green"], COLORS["green_stroke"]),
        ("特征计算", ["Δx / Δy / velocity", "指尖距离 / 状态转换"], COLORS["amber"], COLORS["amber_stroke"]),
        ("规则判断", ["Swipe / Snap", "PointToFist / BodyShift"], COLORS["rose"], COLORS["rose_stroke"]),
        ("冷却过滤", ["避免重复触发", "降低误触"], COLORS["gray"], COLORS["gray_stroke"]),
        ("生成命令", ["MotionGestureEvent", "GestureCommand"], COLORS["violet"], COLORS["violet_stroke"]),
    ]
    body = [text(560, 38, "动态手势识别流程图", "title"), text(560, 60, "基于时间窗口、位移阈值、状态变化和冷却机制", "subtitle")]
    x = 45
    y = 140
    w = 150
    h = 92
    for i, (title, lines, fill, stroke) in enumerate(steps):
        body.append(box(x + i * 178, y, w, h, fill, stroke, title, lines))
        if i < len(steps) - 1:
            body.append(arrow(x + i * 178 + w, y + h / 2, x + (i + 1) * 178, y + h / 2))
    body.extend(
        [
            f'<path class="plainline" d="M 138 250 C 138 310, 915 310, 915 250" stroke-dasharray="6 6"/>',
            text(525, 300, "所有规则均工作在统一手势帧与命令层，可复用 Mock / Native / ExternalBridge 输入", "small"),
        ]
    )
    return svg_root(1120, 360, "\n".join(body))


def diagram_unity_modules():
    body = [
        text(520, 38, "Unity 原型模块结构图", "title"),
        text(520, 60, "《符印守卫》运行时代码与工具链模块划分", "subtitle"),
        box(420, 105, 200, 76, COLORS["gray"], COLORS["gray_stroke"], "Core", ["Bootstrap", "SceneContext", "FlowController"]),
        box(80, 230, 180, 76, COLORS["blue"], COLORS["blue_stroke"], "Input", ["Provider", "Router", "Recognizer"]),
        box(310, 230, 180, 76, COLORS["green"], COLORS["green_stroke"], "Player", ["FpsGestureMotor", "SpellCaster"]),
        box(540, 230, 180, 76, COLORS["rose"], COLORS["rose_stroke"], "Combat", ["EnemySpawner", "Health", "Shield"]),
        box(770, 230, 180, 76, COLORS["amber"], COLORS["amber_stroke"], "UI", ["MenuOverlay", "DebugHud", "FeedbackBoard"]),
        box(260, 365, 200, 76, COLORS["violet"], COLORS["violet_stroke"], "Diagnostics", ["PerformanceMonitor", "DemoRunRecorder"]),
        box(590, 365, 200, 76, COLORS["gray"], COLORS["gray_stroke"], "Editor / Tests", ["Scene Generator", "Dataset Validator", "Test Suites"]),
        arrow(520, 181, 170, 230),
        arrow(520, 181, 400, 230),
        arrow(520, 181, 630, 230),
        arrow(520, 181, 860, 230),
        arrow(400, 306, 360, 365),
        arrow(860, 306, 690, 365),
        arrow(630, 306, 690, 365),
    ]
    return svg_root(1040, 500, "\n".join(body))


def diagram_experiment_pipeline():
    body = [
        text(540, 38, "扩展自动回放实验流程图", "title"),
        text(540, 60, "从视频归档到论文实验表格的自动化证据链", "subtitle"),
        box(50, 125, 160, 90, COLORS["gray"], COLORS["gray_stroke"], "视频归档", ["videos*.tgz", "AVI 候选视频"]),
        box(260, 125, 170, 90, COLORS["blue"], COLORS["blue_stroke"], "抽帧采样", ["200 个候选", "9600 帧"]),
        box(480, 125, 170, 90, COLORS["green"], COLORS["green_stroke"], "MediaPipe 挖掘", ["7359 检出帧", "192 accepted"]),
        box(700, 125, 170, 90, COLORS["amber"], COLORS["amber_stroke"], "回归与回放", ["48 评估 clips", "严格子集 13/13"]),
        box(920, 125, 160, 90, COLORS["violet"], COLORS["violet_stroke"], "论文表格", ["规模表", "性能表", "识别结果表"]),
        arrow(210, 170, 260, 170),
        arrow(430, 170, 480, 170),
        arrow(650, 170, 700, 170),
        arrow(870, 170, 920, 170),
        box(260, 300, 170, 72, COLORS["gray"], COLORS["gray_stroke"], "三模式性能", ["Mock", "Native", "ExternalBridge"]),
        box(480, 300, 170, 72, COLORS["gray"], COLORS["gray_stroke"], "重复实验", ["每模式 9 轮", "共 27 行 CSV"]),
        box(700, 300, 170, 72, COLORS["gray"], COLORS["gray_stroke"], "聚合统计", ["FPS / P95", "Latency / Commands"]),
        arrow(430, 336, 480, 336),
        arrow(650, 336, 700, 336),
        arrow(785, 300, 1000, 215),
    ]
    return svg_root(1120, 430, "\n".join(body))


def diagram_icon_legend():
    body = [
        text(430, 38, "论文图例与图标", "title"),
        text(430, 60, "用于图中标识输入、Unity、外部桥接和实验统计", "subtitle"),
        icon_camera(90, 120),
        text(170, 145, "摄像头 / 视觉输入", "label", "start"),
        icon_unity(90, 205),
        text(170, 232, "Unity 游戏系统", "label", "start"),
        icon_udp(90, 292),
        text(170, 318, "UDP / ExternalBridge", "label", "start"),
        icon_chart(90, 377),
        text(170, 405, "实验统计 / CSV", "label", "start"),
        box(480, 115, 220, 62, COLORS["blue"], COLORS["blue_stroke"], "蓝色", ["输入或视觉链路"]),
        box(480, 200, 220, 62, COLORS["green"], COLORS["green_stroke"], "绿色", ["识别、处理、通过状态"]),
        box(480, 285, 220, 62, COLORS["amber"], COLORS["amber_stroke"], "黄色", ["桥接、回放、转换"]),
        box(480, 370, 220, 62, COLORS["rose"], COLORS["rose_stroke"], "红色", ["玩法、战斗、输出反馈"]),
    ]
    return svg_root(860, 480, "\n".join(body))


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    diagrams = {
        "system_architecture.svg": diagram_system_architecture(),
        "input_pipeline.svg": diagram_input_pipeline(),
        "motion_recognition_flow.svg": diagram_motion_recognition(),
        "unity_module_structure.svg": diagram_unity_modules(),
        "experiment_pipeline.svg": diagram_experiment_pipeline(),
        "icon_legend.svg": diagram_icon_legend(),
    }
    for name, content in diagrams.items():
        (OUT / name).write_text(content, encoding="utf-8")
        print(OUT / name)


if __name__ == "__main__":
    main()
