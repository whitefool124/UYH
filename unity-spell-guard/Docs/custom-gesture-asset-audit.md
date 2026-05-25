# Custom gesture asset audit

Date: 2026-05-25

This audit captures the current custom gesture asset boundary after the runtime cleanup pass. It focuses on the active template library and the reference-frame folders used by the in-game developer validation page.

## Current active state

- Active template folder: `Assets/ProjectGestureLibrary/CustomGestures`
- Reference frame folder: `Assets/StreamingAssets/CustomGestureReferenceVideos`
- Active templates: 16
- Reference-frame folders with images: 18
- Active template/reference matches: 16
- Declared reference-only folders: 2
- Undeclared reference folders without active templates: 0

Matched active validation set:

- `ext_horizontal_wave_easy`
- `ext_motion_down_left_medium`
- `ext_motion_down_left_short`
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

## Declared reference-only folders

These folders have reference images, but their matching templates are not currently active and no matching archive template exists. They are intentionally retained as reference-only material, so the editor audit reports them separately instead of treating them as production template gaps.

- `ext_any_motion_easy`
- `ext_finger_snap_video_template`

## Recoverable from archives

The archive-backed reference folders have been restored to `CustomGestures`.

## Remaining template/reference gaps

None. Any new folder under `Assets/StreamingAssets/CustomGestureReferenceVideos` must either match an active template id or be explicitly declared as reference-only in `CustomGestureAssetAudit`.

## Unity audit tool

Run `Spell Guard > Custom Gestures > Audit Asset Boundaries` in the Unity Editor to regenerate this check from the current project files. The tool is read-only and logs:

- active templates
- reference clips
- active template/reference matches
- active templates missing clips
- declared reference-only clips
- undeclared reference clips missing active templates
- reference clips backed only by archived templates
- invalid or inactive template files
- empty reference folders
