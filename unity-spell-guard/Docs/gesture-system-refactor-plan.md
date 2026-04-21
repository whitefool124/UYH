# Gesture System Refactor Plan

## Purpose

This document defines the technical refactor plan for the Unity gesture pipeline used by Spell Guard.

The target is not a small patch. The target is a full gesture runtime that remains compatible with the current YOLO plus MediaPipe direction while expanding the game from single-hand latest-state input into a richer system that supports precise finger-level dynamics, fast gesture groups, ordered combos, and dual-hand interaction.

## Refactor goals

The refactor must satisfy the following product and graduation-project goals:

1. Keep compatibility with the current Unity scene, gameplay loop, and input source switching.
2. Stay compatible with a YOLO plus MediaPipe vision stack.
3. Support single-hand static gestures, single-hand dynamic gestures, dual-hand gestures, and gesture sequences.
4. Preserve real-time gameplay responsiveness suitable for a Unity combat prototype.
5. Keep external bridge replay available for offline regression and dataset-driven validation.

## Current architecture summary

The current production pipeline is centered around a single snapshot plus a single latest motion event.

### Input side

- `GestureInputProviderBase` exposes one `CurrentSnapshot` and one `CurrentMotionGesture`.
- `GestureInputRouter` switches between `Mock`, `NativeMediapipe`, and `ExternalBridge`.
- `NativeMediapipeGestureProvider` stores one active snapshot and one latest motion event.
- `ExternalGestureBridgeProvider` stores one active snapshot, one latest motion event, and queued external frames.
- `NativeMotionGestureRecognizer` and `ExternalMotionGestureRecognizer` derive dynamic motion from short landmark histories.

### Consumer side

- `GestureSpellCaster` consumes the latest motion event first and then falls back to the latest static snapshot.
- `FpsGestureMotor` depends on the latest static snapshot and assumes one active pointing hand.
- `SpellGuardFlowController` uses the latest snapshot for menu focus and the latest motion event for menu shortcuts.
- `DebugHud` and `MotionGestureFeedbackBoard` display only the latest state rather than a richer event stream.

### Why the current model is insufficient

The current architecture is clean for an MVP, but its public contract is still too narrow:

- one hand-oriented snapshot
- one latest transient motion event
- no explicit sequence history model
- no public multi-hand state model
- no combo or dual-hand resolution layer

This is enough for point, fist, open palm, snap, swipe, and simple menu gestures. It is not enough for precise finger-level motion, ordered combos, or two-hand gesture composition.

## Compatibility with YOLO plus MediaPipe

This refactor remains compatible with a YOLO plus MediaPipe pipeline because it separates the system into two parts:

1. detection and landmark estimation
2. gesture interpretation and gameplay mapping

YOLO is responsible for object or hand detection, ROI stabilization, and multi-target management. MediaPipe is responsible for hand landmarks and low-level pose geometry. The Unity gesture runtime is responsible for gesture semantics, motion interpretation, combo parsing, and gameplay command mapping.

This split is suitable for a graduation project because the system can be presented as:

- YOLO for robust target detection
- MediaPipe for hand keypoint extraction
- a custom Unity-side gesture runtime for dynamic understanding and interactive control

## Target architecture

The target runtime should be organized into five layers.

### 1. Frame ingestion layer

All sources should emit the same normalized frame model.

Suggested core models:

- `GestureFrame`
- `TrackedHandState`
- `BodyState` if body-level motion remains relevant later

Each frame should include:

- frame id
- timestamp
- source kind
- all tracked hands
- optional body or pose context

Each tracked hand should include:

- stable track id
- handedness
- tracking confidence
- 2D landmarks
- palm center
- wrist position
- static pose label and confidence

### 2. Feature extraction layer

This layer converts raw landmarks into reusable features.

It should compute:

- finger bend values
- fingertip relative positions to the palm
- fingertip velocities and accelerations
- path length
- path curvature
- oscillation count
- circularity score
- open-palm spread
- inter-finger distance changes

This feature layer is the foundation required for index beckon, directed finger wave, pinky circles, and similar complex gestures.

### 3. Primitive gesture recognition layer

Primitive recognizers should work on the extracted features instead of directly mixing raw landmark checks into gameplay code.

Primitive output categories should include:

- single-hand static poses
- single-hand dynamic gestures
- dual-hand simultaneous primitives
- pose-transition primitives

Examples:

- Point
- Fist
- OpenPalm
- Snap
- SwipeLeftToRight
- SwipeRightToLeft
- IndexBeckon
- IndexDirectionalWave
- PinkyCircleCW
- PinkyCircleCCW
- PointToFist
- LeftPointRightPalm
- DualOpenPalm

### 4. Sequence and combo resolution layer

This layer consumes primitive gesture events over a short rolling window and resolves higher-level commands.

It should manage:

- event history buffer
- combo time windows
- ordering constraints
- cooldown policies
- simultaneous gesture windows
- conflict resolution between primitive and combo commands

Examples:

- `Point -> Fist -> Snap`
- `IndexBeckon x2 -> OpenPalm`
- `LeftPoint + RightOpenPalm`
- `PinkyCircleCW -> SwipeRight`

### 5. Gameplay command layer

Gameplay should stop reading low-level snapshots directly where possible.

Instead, gameplay and UI should consume:

- resolved gesture commands
- active hand states
- active primitive states
- current combo state for HUD display

This allows gesture semantics to evolve without repeatedly rewriting gameplay code.

## Data model changes

### Replace or deprecate current narrow models

The following models should be gradually deprecated as primary runtime contracts:

- `GestureSnapshot`
- `MotionGestureEvent`

They can remain as legacy adapter outputs during migration.

### Introduce richer models

Recommended new models:

- `GestureFrame`
- `TrackedHandState`
- `GesturePrimitiveEvent`
- `GestureCommand`
- `GestureHistoryBuffer`

Each event should include more than gesture kind alone. It should include:

- start time
- end time if applicable
- confidence
- participating hand ids
- handedness
- optional payload such as direction, repetition count, or rotation direction

## Consumer migration strategy

### `GestureSpellCaster`

Current coupling:

- motion first
- otherwise static pose

Target direction:

- consume resolved `GestureCommand`
- keep support for primitive fallback during migration
- map primitive, combo, and dual-hand commands to spells through one table

### `FpsGestureMotor`

Current coupling:

- assumes one pointing hand and one screen-space position

Target direction:

- designate a control hand
- read per-hand tracked state
- optionally fall back to best available hand if one hand is missing

### `SpellGuardFlowController`

Current coupling:

- latest snapshot for focus and dwell
- latest motion event for settings or return actions

Target direction:

- keep static pointing for focus
- allow command-driven menu shortcuts
- remain stable even while the runtime grows richer internally

### `DebugHud` and `MotionGestureFeedbackBoard`

Current coupling:

- latest static pose and latest motion event only

Target direction:

- display tracked hands count
- display left and right hand states separately
- display active primitive gestures
- display combo history and current resolved command

## Recommended file structure

Add a dedicated gesture-system area rather than expanding the old input scripts indefinitely.

Suggested structure:

```text
Assets/Scripts/GestureSystem/
  Core/
    GestureFrame.cs
    TrackedHandState.cs
    GesturePrimitiveEvent.cs
    GestureCommand.cs
    GestureEnums.cs
  Sources/
    NativeGestureFrameSource.cs
    ExternalGestureFrameSource.cs
  Features/
    HandFeatureExtractor.cs
    FingerTrajectoryAnalyzer.cs
    MotionFeatureWindow.cs
  Recognition/
    PrimitiveGestureRecognizer.cs
    SingleHandDynamicRecognizer.cs
    DualHandGestureRecognizer.cs
    GestureSequenceTracker.cs
    GestureComboResolver.cs
  Runtime/
    GestureRuntime.cs
    GestureEventBus.cs
    GestureSourceRouter.cs
  Adapters/
    LegacySnapshotAdapter.cs
    LegacyMotionAdapter.cs
```

## Phased implementation plan

### Phase 1. Add the new runtime beside the old one

Deliverables:

- new frame and event models
- a shared runtime root
- adapters that still expose `CurrentSnapshot` and `CurrentMotionGesture`

Goal:

- keep the project playable while the new runtime is introduced

### Phase 2. Move current primitive gestures into the new runtime

Deliverables:

- swipe
- slap
- snap
- point to fist
- existing static pose support

Goal:

- preserve current behavior inside the new architecture before adding new complexity

### Phase 3. Add richer single-hand dynamics

Deliverables:

- index beckon
- directional index wave
- pinky circle
- improved finger-level timing features

Goal:

- prove the new architecture can support finer dynamic gestures

### Phase 4. Add sequence and combo tracking

Deliverables:

- rolling event history
- command resolution rules
- combo windows and cooldown policies

Goal:

- support fast gesture groups and ordered combos for gameplay

### Phase 5. Add dual-hand support

Deliverables:

- multi-hand frame ingestion
- stable hand identity management
- dual-hand primitive and combo resolution

Goal:

- support simultaneous and relational two-hand gestures

### Phase 6. Migrate consumers fully

Deliverables:

- `GestureSpellCaster` updated to command-driven casting
- `FpsGestureMotor` updated to per-hand control state
- `SpellGuardFlowController` updated to use richer command input where appropriate
- HUD updated for multi-hand visibility and combo debugging

Goal:

- remove primary gameplay dependency on the old latest-snapshot and latest-motion model

## Real-time gameplay considerations

The architecture is compatible with real-time gameplay if the heavy vision work remains in YOLO and MediaPipe and the Unity-side runtime stays lightweight.

The Unity-side recognition path should focus on:

- normalized landmark ingestion
- short sliding windows
- feature extraction from recent frames
- deterministic rule and state-machine evaluation

This is more suitable for a real-time game than repeatedly running a second heavy model on top of the keypoints.

## Validation strategy

Validation should remain part of the design, not an afterthought.

Recommended validation channels:

- Mock input for deterministic gameplay smoke tests
- external bridge replay for dataset-driven regression
- real webcam play mode for latency and interaction checks
- focused PlayMode tests for primitive gestures and combo timing

Required regression coverage:

- source switching between mock, native, and external bridge
- no duplicate spell firing from hold states
- combo timing windows
- multi-hand identification stability
- menu interaction continuity

## Risks and mitigations

### Risk 1. False positives increase as gesture count grows

Mitigation:

- separate feature extraction from gesture resolution
- use family-specific cooldowns
- enforce confidence thresholds and precedence rules

### Risk 2. Dual-hand tracking instability

Mitigation:

- preserve handedness and track ids
- use temporal continuity when reassigning hands
- treat unstable frames conservatively

### Risk 3. Gameplay coupling slows refactor speed

Mitigation:

- keep legacy adapters during migration
- convert consumer systems one by one
- preserve current gameplay paths until equivalent command-driven behavior is verified

## Recommendation

The current project already has the right seams to support this refactor: input abstraction, separate providers, separate recognizers, bootstrap wiring, and isolated gameplay consumers.

The correct next step is not to keep extending `GestureSnapshot` and `MotionGestureEvent` with more special cases. The correct next step is to introduce a richer multi-hand frame and event runtime beside the current system, migrate the current gesture set into it, and then add the new dynamic, combo, and dual-hand capabilities in phases.

This approach fits both the engineering need of the Unity project and the graduation-project need to present a complete YOLO plus MediaPipe plus custom gesture-runtime architecture.
