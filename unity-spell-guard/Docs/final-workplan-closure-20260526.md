# Final workplan closure

Date: 2026-05-26

This note closes the repository cleanup workplan after the custom gesture, asset-boundary, Unity scene/UI, external tooling, and documentation passes.

## Completed commits

- `11bb424` - removed legacy gesture references.
- `9e3c49d` - documented project asset boundaries.
- `b29e3a8` - added the Unity custom gesture asset audit tool.
- `87c6025` - restored active custom gesture templates that have matching reference clips.
- `ee15ab8` - classified reference-only gesture clips.
- `fcfc83f` - unified custom gesture recognizer patterns and added feature-sequence/self-check support.
- `e0fb4a3` - clarified browser workbench and external regression validation boundaries.

## Current Unity scene and UI boundary

- `ProjectSettings/EditorBuildSettings.asset` keeps `SpellGuardStart` and `SpellGuardPrototype` enabled.
- `SpellGuardDeveloperTools` remains present but disabled in Build Settings.
- The start menu can explicitly open `SpellGuardDeveloperTools`; it is not part of the automatic combat launch path.
- Developer-only UI is gated behind `DeveloperToolsEnabled` in `SpellGuardMenuOverlay` and `DebugHud`.
- No scene or UI file changes were required in this pass.

## Custom gesture asset state

- Active templates: 16.
- Reference-frame folders: 18.
- Active template/reference matches: 16.
- Declared reference-only folders: 2.

Declared reference-only folders:

- `ext_any_motion_easy`
- `ext_finger_snap_video_template`

Current frame counts:

- `ext_horizontal_wave_easy`: 37
- `ext_motion_down_left_medium`: 8
- `ext_motion_down_left_short`: 18
- `ext_motion_down_right_short`: 5
- `ext_motion_left_right_long`: 8
- `ext_motion_right_left_long`: 5
- `ext_motion_right_left_short`: 15
- `ext_motion_right_right_long`: 10
- `ext_motion_right_right_medium`: 10
- `ext_motion_right_right_short`: 12
- `ext_motion_up_left_medium`: 5
- `ext_motion_up_right`: 34
- `ext_motion_up_right_long`: 8
- `ext_motion_up_right_medium`: 13
- `ext_motion_up_right_short`: 15
- `ext_two_finger_spread_easy`: 10

The large local reference-frame changes were not committed. They look like a regenerated or compressed frame set and should be reviewed in Unity before being accepted.

## Tooling boundary

- Browser workbench validation is now labelled as local preview only.
- Unity-side self-check remains the source of truth for template validity.
- `tools/ExternalRegression` defaults to strict Unity validation thresholds; `--relax-live-validation` is opt-in.

## Verification performed

- `npm run build` passed for the browser workbench.
- `git diff --check` passed for committed code/doc changes.
- Active template/reference counts were recalculated from the filesystem.
- Unity scene/build-settings boundary was inspected from project files.

## Verification not completed locally

- Unity Editor was not available from the checked locations, so the Unity menu audit and PlayMode/EditMode tests were not run.
- `dotnet build --no-restore` for Unity-linked projects fails because `unity-spell-guard/Temp/obj/SpellGuard.Runtime/project.assets.json` is missing. This is a Unity-generated build asset issue, not a confirmed source-code syntax error.

## Remaining local changes to handle deliberately

- `unity-spell-guard/Assets/ProjectGestureLibrary/CustomGestures/ext_motion_down_left_short.json`
- `unity-spell-guard/Assets/StreamingAssets/CustomGestureReferenceVideos/**`
- generated thesis screenshots, diagrams, experiment notes, and paper drafts

Do not stage these with broad Git commands. Review them as separate evidence/asset updates, preferably inside Unity for the gesture assets.
