# 符印守卫 UI 出图风格约束表（GPT-image-2）

## 1. 文档用途

- 本文档用于后续通过 GPT-image-2 生成《符印守卫》的 UI 概念图。
- 所有出图必须服从项目已确定的“全息试炼空间”美术方向。
- 本文档优先服务于：主菜单、设置页、结果页、HUD、纯手势 UI 界面。

---

## 2. 总风格结论

《符印守卫》的 UI 不是传统奇幻卷轴界面，也不是普通网页后台或赛博城市屏幕，而是：

**未来技术化施法的全息试炼空间 UI。**

核心感觉应当是：

- 战斗终端
- 试炼控制台
- 手势驱动的无接触操控界面
- 科技已经发展到近似魔法

---

## 3. 必须保留的风格关键词

- 全息试炼空间
- 未来感
- 全息投影
- 能量结界
- 几何符印
- 训练场 / 试炼空间
- 冷色调高对比
- 低复杂度高识别度
- 半透明投影面板
- 发光描边
- 切角结构
- 战斗系统界面
- 未来术式控制终端

英文关键词：

- Holographic UI
- Futuristic combat HUD
- Transparent sci-fi panel
- Glowing outline button
- Energy selection interface
- Gesture-driven interface concept
- Holographic control terminal
- Sci-fi ritual arena interface

---

## 4. 明确禁止的方向

以下方向都与项目既定风格冲突，禁止作为主 UI 方向：

- 羊皮纸、卷轴、木框
- 中世纪奇幻神殿菜单
- 普通网页 SaaS 控制台
- 手机 app 设置页感
- 二次元轻亮色活动页
- 赛博城市街景广告屏
- 写实军工 cockpit HUD
- 过度繁杂、低可读性的炫技界面

---

## 5. 颜色系统

### 主基调

- 深蓝黑
- 冷灰
- 深炭灰

### 高亮辅助色

- 青蓝
- 冷白
- 电紫

### 危险 / 强提示色

- 橙红
- 亮红

### 配色原则

- 大面积保持暗底
- 交互区域和可读信息高亮发光
- 橙红只用于危险、失败、火焰术或高优先级提示

---

## 6. UI 造型规则

### 面板

- 半透明深色底板
- 发光描边
- 细线框
- 切角边框
- 几何符号点缀
- 扫描线 / 能量纹理可少量加入

### 按钮

- 不做厚重拟物按钮
- 更像投影式战斗控制按钮
- 焦点靠描边、发光、填充、进度反馈来体现

### 文字与图标

- 信息优先可读性
- 偏系统终端与战斗 HUD 感
- 用发光文字、简洁图标、能量条替代过多装饰

---

## 7. 手势交互反馈要求

由于项目强调纯手势和停留确认，UI 必须强化“无接触操控”反馈：

- 停留选择要有充能式进度反馈
- 高亮焦点要明显
- 确认反馈要短、亮、清楚
- 误识别反馈要明确，不可模糊
- 反馈形式优先：外轮廓发光、进度填充、能量锁定感

---

## 8. 页面级风格要求

### 8.1 主菜单

定位：**试炼系统启动界面**

要求：

- 像进入试炼空间前的控制终端
- 有仪式启动感
- 可出现能量门、法阵、试炼空间背景结构
- 标题更像系统投影，而不是纯平海报字

### 8.2 设置页

定位：**术式参数控制面板**

要求：

- 像系统校准界面
- 不是普通 options menu
- 每项参数都像“控制 / 调整试炼系统”的一部分

### 8.3 结果页

定位：**试炼评估 / 战斗报告投影**

要求：

- 像战斗终端输出的结算报告
- 有评估、结果、诊断感
- 胜利与失败的状态差异清楚

### 8.4 HUD

定位：**低干扰战斗 HUD**

要求：

- 高信息密度
- 低视觉干扰
- 位置稳定
- 不使用厚重拟物装饰

### 8.5 纯手势 UI 界面

定位：**无接触全息操控界面**

要求：

- 强化焦点移动
- 强化停留确认
- 强化充能式进度表现
- 看起来像空中操控未来系统，而不是点按钮

---

## 9. GPT-image-2 通用 Prompt 母版

### 通用风格 Prompt

> Futuristic holographic ritual training chamber game UI, not medieval fantasy, not paper scroll, not web app, dark blue-black background, cold gray and deep charcoal base, glowing cyan white and electric purple accents, semi-transparent projection panels, thin angular frames, geometric sigils, energy barrier motifs, high readability, clean combat terminal aesthetic, gesture-driven interface, advanced technology that feels like magic, polished game UI concept art, high contrast, sharp hierarchy, premium sci-fi spellcasting interface

### 通用负面约束

> no parchment, no wood, no medieval ornaments, no realistic military cockpit, no casual mobile game style, no cute anime UI, no web dashboard, no overly cluttered details, no bright candy colors, no cyberpunk city ads

### 实装友好补充语句

> designed for Unity game UI implementation, clear panel separation, readable button hierarchy, suitable for slicing into reusable UI elements, not just a flat illustration

---

## 10. 分页面 Prompt 包

### 10.1 主菜单 Prompt

> Main menu screen for a first-person gesture spellcasting game, futuristic holographic ritual training chamber interface, central title "SPELL GUARD" as a glowing projection, semi-transparent dark panels, cyan white and electric purple energy outlines, angular geometric sigils, subtle ritual circle motifs, options for Start Trial, Tutorial, Training, Settings, clean visual hierarchy, premium sci-fi combat terminal, dark blue-black environment, high readability, controlled glow, elegant and minimal, advanced magic-through-technology atmosphere, polished game UI concept art

### 10.2 设置页 Prompt

> Settings screen for a futuristic holographic spellcasting trial system, semi-transparent projection control panel, dark background with glowing cyan and white interface lines, angular segmented panels, labeled controls for spell confirmation timing, enemy intensity, music volume, sound effects volume, gesture-driven focus states, charging-style selection feedback, clean sci-fi control terminal aesthetic, subtle geometric sigils and energy scan lines, highly readable, elegant, minimal, premium game UI concept art, advanced training system configuration screen

### 10.3 结果页 Prompt

> Results screen for a futuristic holographic combat trial, battle evaluation terminal, dark blue-black background, glowing cyan white electric purple UI, semi-transparent result panel, clear sections for victory or defeat, score, best score, accuracy, casts, hits, restart and return options, elegant geometric framing, projected system report style, subtle danger orange-red accents for warnings or defeat, clean premium game UI concept art, high readability, advanced spell trial assessment interface

### 10.4 HUD Prompt

> In-game HUD for a first-person futuristic gesture spellcasting combat game, minimal interference, high information density, holographic combat overlay, dark transparent panels, glowing cyan white electric purple accents, clean health and shield bars, enemy count, current gesture state, spell feedback, motion status indicators, elegant angular frames, geometric sigil micro-details, highly readable combat terminal style, not cluttered, premium sci-fi magic interface, polished game HUD concept art

### 10.5 纯手势 UI Prompt

> Gesture-driven holographic selection interface for a futuristic spell trial system, no-touch control terminal, charging focus feedback, glowing outline selection states, progress-based confirmation visuals, semi-transparent dark panel, cyan white electric purple energy highlights, geometric projection markers, clean readable layout, advanced combat system UI, elegant and minimal, premium sci-fi spell interface concept art

---

## 11. 推荐出图顺序

建议按以下顺序进行：

1. 主菜单
2. 结果页
3. 设置页
4. HUD
5. 纯手势 UI 界面

原因：

- 最能最快建立整体风格统一性
- 最适合先做答辩展示层升级
- 最容易转成后续可拆分可实装的 Unity UI 结构

---

## 12. 最终一句话风格定义

> 《符印守卫》的 UI 应表现为“未来技术化施法”的全息试炼空间界面：它不是传统魔法，也不是普通科幻，而是带有几何符印、能量结界和战斗终端感的高可读性投影式系统界面。
