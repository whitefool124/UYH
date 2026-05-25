# Custom gesture asset audit

Date: 2026-05-25

This audit captures the current custom gesture asset boundary after the runtime cleanup pass. It focuses on the active template library and the reference-frame folders used by the in-game developer validation page.

## Current active state

- Active template folder: `Assets/ProjectGestureLibrary/CustomGestures`
- Reference frame folder: `Assets/StreamingAssets/CustomGestureReferenceVideos`
- Active templates: 1
- Reference-frame folders with images: 18
- Active template/reference matches: 1

Matched active validation set:

- `ext_motion_down_left_short`

## Reference folders without active templates

These folders have reference images, but their matching templates are not currently active. They will not appear as selectable validation targets unless the corresponding template is restored to `CustomGestures`.

- `ext_any_motion_easy`
- `ext_finger_snap_video_template`
- `ext_horizontal_wave_easy`
- `ext_motion_down_left_medium`
- `ext_motion_down_right_short`
- `ext_motion_left_right_long`
- `ext_motion_right_left_long`
- `ext_motion_right_left_short`
- `ext_motion_right_right_long`
- `ext_motion_right_right_medium`
- `ext_motion_right_right_short`
- `ext_motion_up_left_medium`
- `ext_motion_up_right`
- `ext_motion_up_right_long`
- `ext_motion_up_right_medium`
- `ext_motion_up_right_short`
- `ext_two_finger_spread_easy`

## Recoverable from archives

These reference folders have matching templates in `ArchivedCustomGestures_*`, so they are candidates for restoring into the active runtime library after validation:

- `ext_horizontal_wave_easy`
- `ext_motion_down_left_medium`
- `ext_motion_down_right_short`
- `ext_motion_left_right_long`
- `ext_motion_right_left_long`
- `ext_motion_right_left_short`
- `ext_motion_right_right_long`
- `ext_motion_right_right_medium`
- `ext_motion_right_right_short`
- `ext_motion_up_left_medium`
- `ext_motion_up_right`
- `ext_motion_up_right_long`
- `ext_motion_up_right_medium`
- `ext_motion_up_right_short`
- `ext_two_finger_spread_easy`

## Needs decision

These reference folders currently have no matching active or archived template:

- `ext_any_motion_easy`
- `ext_finger_snap_video_template`

Either restore/create matching templates, or move these folders out of the validation reference path.

## Unity audit tool

Run `Spell Guard > Custom Gestures > Audit Asset Boundaries` in the Unity Editor to regenerate this check from the current project files. The tool is read-only and logs:

- active templates
- reference clips
- active template/reference matches
- active templates missing clips
- reference clips missing active templates
- reference clips backed only by archived templates
- invalid or inactive template files
- empty reference folders
