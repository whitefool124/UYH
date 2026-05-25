# Project asset boundaries

This note defines which parts of the repository are production project assets, which are support tools, and which outputs should stay local unless they are deliberately curated.

## Commit by default

- `unity-spell-guard/Assets/Scripts/` - Unity runtime, editor, diagnostics, input, UI, combat, and tool scripts.
- `unity-spell-guard/Assets/Scenes/` - playable scenes and the retained developer-tools scene. `SpellGuardDeveloperTools` is allowed to exist in the project but should remain disabled in build settings unless a debug build needs it.
- `unity-spell-guard/Assets/ProjectGestureLibrary/CustomGestures/` - the active runtime custom gesture library. Changes here affect gameplay and validation directly, so commit only reviewed templates.
- `unity-spell-guard/Assets/ProjectGestureLibrary/ArchivedCustomGestures_*` - curated historical gesture sets used for regression or thesis comparison.
- `unity-spell-guard/Docs/` - design notes, thesis support notes, diagrams, and curated screenshots.
- `tools/ExternalRegression/` source files - external regression harness source. Build outputs under `bin/` and `obj/` stay ignored.

## Review before committing

- `unity-spell-guard/Assets/StreamingAssets/CustomGestureReferenceVideos/` - reference-frame sets used by the in-game custom gesture validation page. These can be large and easy to partially regenerate, so commit only complete frame folders with matching `.meta` files.
- `unity-spell-guard/ExperimentResults/` - keep only selected evidence artifacts that are referenced by documentation or thesis text. Raw repeated runs and timestamped CSV files should stay local.
- `论文材料/` - commit final or reviewed thesis materials only. Temporary recovery, sanitized, generated, or duplicate drafts should stay local unless they are part of the submitted package.
- Root Vite app files (`index.html`, `src/`, `package.json`) - this is an external browser custom-gesture workbench, not the Unity runtime. Keep it only as support tooling unless the project scope changes back to a browser game.

## Keep local

- ML weight files such as `*.pt`.
- Generated probe output under `unity-spell-guard/bridge/outputs/`.
- Unity generated folders such as `Library`, `Temp`, `Obj`, `Build`, `Logs`, and `UserSettings`.
- .NET and web build outputs such as `tools/**/bin/`, `tools/**/obj/`, `node_modules/`, and `dist/`.
- Bulk raw experiment outputs unless they have been selected and documented as final evidence.

## Current cleanup rule

When the worktree is dirty, stage by exact path instead of broad commands. Gesture templates, reference-video frames, generated thesis assets, and experiment CSVs should not be swept into a commit together with runtime code changes.
