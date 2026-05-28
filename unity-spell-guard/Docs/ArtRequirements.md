# Spell Guard 美术资源需求规格文档

> **目标**：提交给 AI 生图模型（Midjourney/Stable Diffusion/DALL-E 等）自动生成。
> **Unity 版本**：2022.3.62f2c1，Built-in Render Pipeline
> **UI 系统**：UGUI（Unity Canvas），所有 UI 精灵导入为 Sprite (2D and UI)，关闭 mipmaps

---

## 总体美术风格

- **主题**：近未来科幻 × 仪式感符文魔法
- **色调**：深空蓝黑底色 `#0A0E1A`，青蓝/电光蓝为主色 `#3D8BFF`，金橙为强调色 `#F5A623`，白色文字 `#E8ECF2`
- **质感**：半透明发光面板、细线条边框、能量光晕、科技符文装饰
- **参考**：全息投影 UI、Tron 光带、科幻仪式祭坛

---

# 一、UI 精灵 (UGUI Sprites)

> 所有精灵需出图 **2x 倍率**（比如实际用 256px，出图 512px），九宫格需标注 border。

---

## 1.1 面板背景

| 编号 | 资源名 | 规格 | 说明 |
|---|---|---|---|
| UI-01 | `ui_panel_main` | 512×512, 九宫格 border=24 | 主面板背景。圆角矩形，深色半透明填充 `#0D111A` α=0.92，边框 2px `#3D8BFF` α=0.7 发光，四角有细小的 L 形科技线装饰 |
| UI-02 | `ui_panel_overlay` | 512×512, 九宫格 border=20 | 覆盖层面板。比主面板更暗 `#080B14` α=0.95，边框 1px `#3D8BFF` α=0.5，四角有微型菱形节点 |
| UI-03 | `ui_panel_settings` | 512×512, 九宫格 border=22 | 设置面板。同主面板，但边框色偏青 `#4DC9F6` α=0.8 |
| UI-04 | `ui_panel_developer` | 512×512, 九宫格 border=20 | 开发者面板。最深色 `#060810` α=0.97，边框色 `#00E5FF` α=0.9，左侧有一条竖着的能量流线装饰 |
| UI-05 | `ui_panel_results_victory` | 512×512, 九宫格 border=28 | 胜利面板。底色偏金 `#0D111A`，边框 `#F5A623` α=0.9，顶部有一条金色光带，四角有符文角标 |
| UI-06 | `ui_panel_results_defeat` | 512×512, 九宫格 border=28 | 失败面板。底色 `#0D111A`，边框 `#E84040` α=0.8，顶部有暗红色带 |
| UI-07 | `ui_panel_hud` | 256×256, 九宫格 border=12 | HUD 面板。小型半透明面板 `#0D111A` α=0.78，边框 1px `#3D8BFF` α=0.4，仅左上角有 L 形线 |

---

## 1.2 按钮

| 编号 | 资源名 | 规格 | 说明 |
|---|---|---|---|
| UI-08 | `ui_btn_primary_normal` | 256×64, 九宫格 border=16 | 主按钮-常态。深色底 `#131B2A`，边框 2px `#3D8BFF` α=0.8，左右两端有两条短横线装饰，内部微微发光 |
| UI-09 | `ui_btn_primary_hover` | 256×64, 九宫格 border=16 | 主按钮-悬停/选中。同尺寸，边框变亮 `#5DAFFF` α=1.0，内部有从左到右的渐变亮光带 |
| UI-10 | `ui_btn_primary_active` | 256×64, 九宫格 border=16 | 主按钮-按下。边框 `#F5A623`，内部短暂高亮 |
| UI-11 | `ui_btn_secondary_normal` | 200×48, 九宫格 border=12 | 次按钮-常态。更淡的底 `#0D111A`，边框 1px `#3D8BFF` α=0.4 |
| UI-12 | `ui_btn_secondary_hover` | 200×48, 九宫格 border=12 | 次按钮-悬停。边框变亮到 α=0.9 |
| UI-13 | `ui_btn_danger` | 200×48, 九宫格 border=12 | 危险按钮（删除等）。边框 `#E84040` |
| UI-14 | `ui_btn_quick_action` | 180×40, 九宫格 border=10 | 快捷操作按钮。比主按钮小，边框 1px `#4DC9F6` α=0.6 |

---

## 1.3 图标 (Icon)

> 全部为 **单色白色/青色线条风格**，方便代码中调色。尺寸为实际使用尺寸 x2。

| 编号 | 资源名 | 规格 | 说明 |
|---|---|---|---|
| UI-15 | `ui_icon_fire` | 64×64 | 火焰图标：简化火焰形状，中心有一个小菱形 |
| UI-16 | `ui_icon_ice` | 64×64 | 冰霜图标：六角雪花/冰晶简化符号 |
| UI-17 | `ui_icon_shield` | 64×64 | 护盾图标：六边形边框内有一个感叹号/盾牌轮廓 |
| UI-18 | `ui_icon_health` | 48×48 | 生命图标：十字 + 外圆环，科技风格 |
| UI-19 | `ui_icon_energy` | 48×48 | 能量图标：菱形 + 上下两个小三角 |
| UI-20 | `ui_icon_pointer` | 48×48 | 指向手势图标：伸食指的手简化轮廓 |
| UI-21 | `ui_icon_fist` | 48×48 | 握拳手势图标：握拳手简化轮廓 |
| UI-22 | `ui_icon_vsign` | 48×48 | V 手势图标：V 字形手简化轮廓 |
| UI-23 | `ui_icon_openpalm` | 48×48 | 张掌手势图标：张开手掌简化轮廓 |
| UI-24 | `ui_icon_arrow_left` | 32×32 | 左箭头：< 形状 |
| UI-25 | `ui_icon_arrow_right` | 32×32 | 右箭头：> 形状 |
| UI-26 | `ui_icon_arrow_up` | 32×32 | 上箭头 |
| UI-27 | `ui_icon_arrow_down` | 32×32 | 下箭头 |
| UI-28 | `ui_icon_swipe` | 48×48 | 挥动手势图标：水平方向箭头 + 手部轮廓 |
| UI-29 | `ui_icon_confirm` | 48×48 | 确认图标：勾 ✓，科技风格 |
| UI-30 | `ui_icon_back` | 48×48 | 返回图标：← 箭头 |
| UI-31 | `ui_icon_settings` | 48×48 | 设置图标：齿轮简化 |
| UI-32 | `ui_icon_camera` | 48×48 | 摄像头图标：相机简化轮廓 |
| UI-33 | `ui_icon_fullscreen` | 32×32 | 全屏图标：四角向外箭头 |
| UI-34 | `ui_icon_recording` | 32×32 | 录制中图标：实心圆 + 外环脉冲效果 |
| UI-35 | `ui_icon_play` | 48×48 | 播放/开始图标：▶ 三角 |
| UI-36 | `ui_icon_pause` | 48×48 | 暂停图标：∥ 双竖线 |
| UI-37 | `ui_icon_restart` | 48×48 | 重开图标：↻ 环形箭头 |
| UI-38 | `ui_icon_exit` | 48×48 | 出口/门图标：拱门简化轮廓 |

---

## 1.4 进度条与指示器

| 编号 | 资源名 | 规格 | 说明 |
|---|---|---|---|
| UI-39 | `ui_progress_bar_bg` | 256×24, 九宫格 border=4 | 进度条底。半透明深色条 `#0D111A` α=0.8，圆角 |
| UI-40 | `ui_progress_bar_fill` | 256×24, 九宫格 border=4 | 进度条填充。青蓝渐变 `#3D8BFF` → `#4DC9F6`，有横向流动光效 |
| UI-41 | `ui_progress_bar_fill_gold` | 256×24, 九宫格 border=4 | 进度条填充-金色版。`#F5A623` → `#FFD700` |
| UI-42 | `ui_divider_horizontal` | 256×4 | 水平分割线。中心一条白线 α=0.3，两端有小菱形节点 |
| UI-43 | `ui_divider_vertical` | 4×256 | 垂直分割线。同上逻辑 |
| UI-44 | `ui_crosshair` | 64×64 | FPS 十字准星。中心小圆点 + 上下左右四条短线，科技风格细线 |

---

## 1.5 手势识别可视化

| 编号 | 资源名 | 规格 | 说明 |
|---|---|---|---|
| UI-45 | `ui_hand_skeleton_bg` | 256×256 | 手势骨架预览底框。深色透明底 + 细边框，内部有 2×2 的极淡参考网格线 |
| UI-46 | `ui_gesture_success_flash` | 128×128 | 验证成功瞬闪效果。绿色 `#00E676` 圆环扩散，外圈模糊 |
| UI-47 | `ui_gesture_pulse_ring` | 128×128 | 手势脉冲环。青蓝色圆环 `#3D8BFF`，从中心扩散消失（可用于序列帧动画 8帧） |

---

## 1.6 场景 (Screen) 背景

| 编号 | 资源名 | 规格 | 说明 |
|---|---|---|---|
| UI-48 | `ui_screen_bg_main_menu` | 1920×1080 | 主菜单全屏背景。深空蓝黑底色，右下有大型发光符文阵列（模糊虚化），左上到右下有微弱光带流动感。中央偏右有隐约的仪式核心剪影 |
| UI-49 | `ui_screen_bg_loading` | 1920×1080 | 加载过渡背景。纯深色底 `#060810`，中央有一个小型发光符文旋转暗示（可用代码实现） |

---

# 二、VFX 特效精灵

> VFX 精灵通常需要带 Alpha 通道的发光效果，白色或单色为主，代码中叠加颜色。

---

## 2.1 法术

| 编号 | 资源名 | 规格 | 说明 |
|---|---|---|---|
| VFX-01 | `vfx_fire_projectile` | 64×64 | 火焰弹。圆形发光体，中心亮白，向外橙→红渐变，边缘模糊。整体 α 从中心 1.0 到边缘 0 |
| VFX-02 | `vfx_fire_impact` | 128×128 | 火焰命中爆炸。中心白色闪光 + 橙红火花四散，不规则爆炸形状 |
| VFX-03 | `vfx_fire_trail` | 32×128 | 火焰拖尾。细长渐变光带，上端橙金、下端透明红 |
| VFX-04 | `vfx_ice_crystal` | 64×64 | 冰晶命中。六角冰晶形状，青蓝色 `#4DC9F6`，边缘有碎冰粒子 |
| VFX-05 | `vfx_ice_freeze_overlay` | 128×128 | 冻结覆盖。不规则的冰霜纹理，蓝白色，可用于敌人冻结时叠加 |
| VFX-06 | `vfx_shield_hex` | 256×256 | 护盾六边形能量屏障。六边形边框 `#3D8BFF`，内部有蜂巢网格线，整体半透明，上下有微弱的能量流动 |
| VFX-07 | `vfx_shield_break` | 128×128 | 护盾破碎。六边形碎片向四周散开，青蓝色碎片 |
| VFX-08 | `vfx_ritual_flame` | 64×128 | 火盆火焰。竖长火焰形状，底部亮白→中橙→上红→顶透明，有轻微摇曳感 |

---

## 2.2 通用

| 编号 | 资源名 | 规格 | 说明 |
|---|---|---|---|
| VFX-09 | `vfx_glow_soft` | 128×128 | 柔光晕。纯白到透明径向渐变，用于任何需要发光的地方 |
| VFX-10 | `vfx_ring_pulse` | 128×128 | 脉冲光环。细圆环，白→青蓝 `#3D8BFF`，外缘模糊 |

---

# 三、角色与敌人

---

## 3.1 敌人 —— 基础试炼单位

| 编号 | 资源名 | 规格 | 说明 |
|---|---|---|---|
| CH-01 | `enemy_body_diffuse` | 512×512 | 敌人身体贴图。深色底 `#1A0A20`（暗紫黑），表面有不规则的暗红色能量裂纹 `#D04040`，裂纹从中心向四周蔓延。整体金属/能量混合质感 |
| CH-02 | `enemy_body_emissive` | 512×512 | 敌人自发光贴图。裂纹部分为红色发光 `#FF4040`，其余全黑。用于 Emissive 通道让裂纹发光 |
| CH-03 | `enemy_healthbar_bg` | 128×16 | 敌人血条背景。深色条 |
| CH-04 | `enemy_healthbar_fill` | 128×16 | 敌人血条填充。红色→暗红渐变 |

> 敌人模型：Base Mesh 建议为人形轮廓简化体（无面部特征），也可使用程序化 Capsule + 贴图方案临时使用。

---

## 3.2 玩家 —— FPS 手部

| 编号 | 资源名 | 规格 | 说明 |
|---|---|---|---|
| CH-05 | `hand_idle` | 待定（需模型） | FPS 视角下的手部模型。右手，从手腕到指尖，自然半握姿态。材质偏科幻：深灰色底 + 指关节处青蓝色发光线条。**最低需求**：至少 4 张静态手势精灵图（见下方）作为 billboard 替代 |
| CH-06 | `hand_sprite_point` | 256×256 | 指向手势 FPS 视角精灵。从玩家视角看自己的右手，伸出食指指向屏幕中央 |
| CH-07 | `hand_sprite_fist` | 256×256 | 握拳手势 FPS 视角精灵。右手握拳 |
| CH-08 | `hand_sprite_vsign` | 256×256 | V 手势 FPS 视角精灵。右手 V 字形（食指+中指伸出） |
| CH-09 | `hand_sprite_openpalm` | 256×256 | 张掌手势 FPS 视角精灵。右手五指张开 |

---

# 四、环境纹理

> 环境主要使用程序化 Mesh（Cube/Capsule/Plane），可通过替换材质/贴图快速升级视觉。

| 编号 | 资源名 | 规格 | 说明 |
|---|---|---|---|
| ENV-01 | `env_floor_grid` | 1024×1024 | 地面网格纹理。深色底 `#0A0E1A`，细线网格（青色 `#3D8BFF` α=0.3），中心区域网格更亮更密。整体有微弱的径向渐变：中心稍亮，边缘渐暗 |
| ENV-02 | `env_wall_rune` | 512×512 | 墙面纹理。深色底，表面有竖直排列的发光符文线条 `#3D8BFF` α=0.25，符文简单几何化（菱形、三角、线段组合） |
| ENV-03 | `env_pillar_rune` | 256×512 | 柱子纹理。竖直条纹 + 间隔的发光符文环 `#4DC9F6` α=0.4 |
| ENV-04 | `env_ritual_core` | 512×512 | 仪式核心纹理。深色底，中心有密集的符文阵列 + 能量汇聚光点 `#F5A623` α=0.8，外围符文圈 |
| ENV-05 | `env_brazier_metal` | 256×256 | 火盆金属底座纹理。暗色金属拉丝 |
| ENV-06 | `env_gate_arch` | 512×256 | 仪式之门纹理。拱门形状的发光边框，内部有类似传送门的竖条纹能量场 `#4DC9F6` α=0.5 |

---

# 五、手势教学参考图

> 用于教程页面展示。静态手势 + 中文标签。

| 编号 | 资源名 | 规格 | 说明 |
|---|---|---|---|
| TUT-01 | `tutorial_gesture_point` | 256×256 | Point 手势教学图。真实/线稿风格的手部图片，伸出食指，下方标注"食指指向 · Point" |
| TUT-02 | `tutorial_gesture_fist` | 256×256 | Fist 手势教学图。握拳手，标注"握拳 · Fist" |
| TUT-03 | `tutorial_gesture_vsign` | 256×256 | V Sign 手势教学图。V 字手，标注"V 字手势 · V Sign" |
| TUT-04 | `tutorial_gesture_openpalm` | 256×256 | OpenPalm 手势教学图。张开手掌，标注"张掌 · OpenPalm" |
| TUT-05 | `tutorial_gesture_swipe_left` | 256×128 | 左挥教学图。手 + 左箭头，标注"向左挥动 · 左移" |
| TUT-06 | `tutorial_gesture_swipe_right` | 256×128 | 右挥教学图。手 + 右箭头，标注"向右挥动 · 右移" |
| TUT-07 | `tutorial_gesture_swipe_forward` | 256×128 | 前挥教学图。手 + 上箭头，标注"向前挥动 · 前进" |
| TUT-08 | `tutorial_gesture_swipe_backward` | 256×128 | 后挥教学图。手 + 下箭头，标注"向后挥动 · 后退" |

---

# 六、字体

| 编号 | 资源名 | 规格 | 说明 |
|---|---|---|---|
| FONT-01 | 标题字体 | TTF/OTF | 科技感无衬线字体，推荐: Orbitron / Rajdhani / Exo 2。用于主标题"SPELL GUARD"和面板标题 |
| FONT-02 | 正文字体 | TTF/OTF | 可读性好的等宽/无衬线字体，推荐: Source Han Sans SC / Noto Sans SC。用于正文、按钮文字、HUD 文字 |

---

# 七、出图优先级（AI 生图顺序建议）

## 第一批（1-2轮）：UI 核心 → 可替换现有 OnGUI
1. UI-01 `ui_panel_main`
2. UI-08/09/10 `ui_btn_primary_*` （一套三个状态）
3. UI-11/12 `ui_btn_secondary_*`
4. UI-48 `ui_screen_bg_main_menu`
5. UI-15/16/17 法术图标三件套
6. UI-18 生命图标
7. UI-39/40 进度条

## 第二批（3-4轮）：VFX + 环境
8. VFX-01 `vfx_fire_projectile`
9. VFX-02 `vfx_fire_impact`
10. VFX-06 `vfx_shield_hex`
11. ENV-01 `env_floor_grid`
12. CH-06~09 手势精灵四件

## 第三批（5-6轮）：辅助UI + 教学图 + 其他
13. 其余 UI 面板和图标
14. TUT-01~08 教学参考图
15. 敌人和环境剩余贴图

---

# 八、Unity 导入设置速查表

| 资源类型 | Texture Type | Alpha Source | Wrap Mode | Filter Mode | Max Size | Compression |
|---|---|---|---|---|---|---|
| UI 精灵 | Sprite (2D and UI) | Input Texture Alpha | Clamp | Bilinear | 实际需要 | Normal Quality |
| VFX 粒子 | Sprite (2D and UI) | Input Texture Alpha | Clamp | Bilinear | 实际需要 | Normal Quality |
| 环境贴图 | Default | None | Repeat | Bilinear | 1024 | Normal Quality |
| 自发光贴图 | Default | None | Clamp | Bilinear | 512 | Normal Quality |
| 图标 | Sprite (2D and UI) | Input Texture Alpha | Clamp | Bilinear | 实际需要 | Normal Quality |
| 教学参考图 | Sprite (2D and UI) | Input Texture Alpha | Clamp | Bilinear | 256 | Normal Quality |

---

> 文档版本 v1.0 · 生成日期 2025-05-24
