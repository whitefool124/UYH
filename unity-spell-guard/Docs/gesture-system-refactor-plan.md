# Gesture System Refactor Plan

## 1. Project goal

Spell Guard is being refactored into a Unity combat prototype that supports YOLO plus MediaPipe input, richer single-hand dynamics, ordered gesture sequences, and dual-hand composition while staying real-time and game-friendly.

The goal is not only to recognize more gestures, but to turn vision output into a stable gameplay command system that can be tested, replayed, and demonstrated in a graduation project.

## 2. Current status

The project has already moved beyond a single latest-state input model.

Current runtime pieces include:

- `GestureInputProviderBase`
- `GestureInputRouter`
- `MockGestureInputProvider`
- `NativeMediapipeGestureProvider`
- `ExternalGestureBridgeProvider`
- `GestureFrame`
- `TrackedHandState`
- `GestureCommand`
- `GestureCommandHistory`
- `GestureSequenceMatcher`

Gameplay consumers are already wired to the new layer:

- `GestureSpellCaster`
- `FpsGestureMotor`
- `SpellGuardFlowController`
- `DebugHud`
- `MotionGestureFeedbackBoard`

This means the project is already in the integration-hardening stage, not the scaffold stage.

## 3. Architecture summary

The current system is split into five layers.

### 3.1 Frame ingestion

All sources should emit a normalized `GestureFrame`.

Each frame contains:

- frame id
- timestamp
- source kind
- one or more tracked hands

Each tracked hand contains:

- track id
- handedness
- confidence
- 2D landmarks
- palm center
- static pose label

### 3.2 Feature extraction

The next layer converts landmarks into reusable features such as:

- finger bend
- fingertip position relative to palm
- velocity
- acceleration
- path length
- curvature
- circularity

These features are the basis for more precise gestures like beckon, directed finger wave, and circle-like motion.

### 3.3 Primitive recognition

Primitive recognizers turn features into gesture events such as:

- Point
- Fist
- OpenPalm
- Snap
- SwipeLeftToRight
- SwipeRightToLeft
- PointToFist

### 3.4 Sequence and combo resolution

`GestureCommandHistory` and `GestureSequenceMatcher` make it possible to detect short ordered patterns such as:

- `Point -> Fist -> Snap`
- `IndexBeckon x2 -> OpenPalm`
- `LeftPoint + RightOpenPalm`

### 3.5 Gameplay command layer

Gameplay systems should consume resolved commands rather than raw snapshots where possible.

Current consumers already do this in varying degrees:

- spell casting reads motion commands first
- player motion reads the primary hand frame
- flow control reads commands for menu shortcuts
- HUD shows runtime source, hand count, and command history

## 4. What the current runtime already proves

The refactor already proves the following:

- mock, native, and external inputs can all produce a usable runtime frame
- dynamic motion can be carried through the command layer
- command history can be queried for sequence detection
- the project can build and test cleanly in both runtime and editor assemblies

## 5. Training dataset support

The repository now also contains a dataset validation path for graduation-project self-testing.

The dataset structure under `训练集/` is:

- `annotations*.zip`
- `videos*.zip`

The annotations archive contains:

- `metadata.csv`
- `classIdx.txt`
- `Annot_TrainList.txt`
- `Annot_TestList.txt`
- `Video_TrainList.txt`
- `Video_TestList.txt`

The video archives contain `.tgz` bundles that include `.avi` clips.

An editor-only validator can now check the dataset structure and report missing files or malformed archives.

## 6. Graduation-project fit

This architecture is a good fit for a graduation project because it clearly separates:

1. visual detection and landmark estimation
2. gesture interpretation and command resolution
3. Unity gameplay mapping
4. dataset validation and regression testing

That makes the project easy to explain in a thesis, easy to demo in Unity, and easy to verify with tests.

## 7. Recommended next work

The highest-value next steps are:

1. use the command history to implement combo triggering
2. add more dual-hand runtime behavior
3. keep tuning native handedness support
4. extend dataset-driven validation from structure checks to replay-based sample checks

## 8. Verification status

Current verification status:

- Unity script diagnostics: clean
- runtime build: clean
- PlayMode tests: clean
- EditMode tests: clean
- dataset validator: available in-editor

## 9. Summary

Spell Guard is now a layered Unity gesture prototype with a real runtime command pipeline, dataset validation support, and enough test scaffolding to keep expanding toward richer single-hand, combo, and dual-hand gestures without falling back to the old single-snapshot model.
