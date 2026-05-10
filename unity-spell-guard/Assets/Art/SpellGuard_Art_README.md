# Spell Guard Art Assets

Generated art assets for the current Unity prototype. Style target: minimal, elegant, restrained holographic trial space.

## Environment

Path: `Assets/Art/Environment/SpellGuard/`

- `env_floor_grid.png` - floor / trial area material reference
- `env_energy_barrier.png` - holographic boundary wall reference
- `env_gate_exit.png` - entrance / exit gate reference
- `env_ritual_core.png` - altar / ritual core / control node reference
- `env_wall_column_module.png` - wall / pillar / doorway module reference

## VFX

Path: `Assets/Art/VFX/SpellGuard/`

- `vfx_fire_pulse.png` - fire spell high-energy pulse reference
- `vfx_ice_lattice.png` - ice spell crystal lattice / freeze reference
- `vfx_shield_hex.png` - shield spell hex energy barrier reference
- `vfx_gesture_pulse_ring.png` - Snap / Swipe gesture feedback reference

## Enemies

Path: `Assets/Art/Enemies/SpellGuard/`

- `enemy_basic_trial_unit.png` - basic synthetic trial enemy reference

## UI Screens

Path: `Assets/Art/UI/SpellGuard/Screens/`

- `screen_menu_gateway.png` - main menu background reference
- `screen_results_panel.png` - victory / defeat result background reference

## UI Sprite Pack

Path: `Assets/Art/UI/SpellGuard/Sprites/`

Contains reusable panel, button, progress, HUD, icon, and divider sprites.

## Unity import notes

- UI images: Texture Type `Sprite (2D and UI)`.
- Environment / enemy / VFX concept references can stay as default textures unless used in UI or billboards.
- For VFX billboards or UI overlays, import as Sprite and keep alpha transparency enabled.
- Keep mipmaps off for UI sprites.
