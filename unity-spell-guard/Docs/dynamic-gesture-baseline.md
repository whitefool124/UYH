# Dynamic Gesture Offline Baseline

## Purpose

This document records the current offline verification baseline for Unity-side dynamic gesture recognition driven by external UDP replay.

It exists to answer three questions reliably:

1. Is the external replay pipeline alive?
2. Is Unity receiving enough landmark history for dynamic recognition?
3. Which IPN Hand clips currently trigger swipe/snap style motion events?

## Current runtime design

- Python bridge (`bridge/mediapipe_udp_bridge.py`) sends static gesture labels plus landmarks over UDP.
- Unity `UdpGestureReceiver` now queues incoming packets instead of overwriting a single latest packet.
- Unity `ExternalGestureBridgeProvider` now queues pending frames for downstream motion recognition.
- Unity `ExternalMotionGestureRecognizer` now processes queued frames and uses packet `timestamp` for motion timing.

This means dynamic motion recognition is based on landmark sequences, not on the Python static label.

## Verified local IPN sources

Local source archives were found under `E:\毕设\`:

- `annotations-20260417T110615Z-3-001.zip`
- `videos-20260417T110637Z-3-001.zip`
- `videos-20260417T110637Z-3-002.zip`
- `videos-20260417T110637Z-3-003.zip`

The following labeled clips were extracted from `videos01.tgz` using `Annot_List.txt` frame ranges:

- `bridge/samples/ipn_real/ipn_229_g05_throw_left.mp4`
- `bridge/samples/ipn_real/ipn_229_g06_throw_right.mp4`
- `bridge/samples/ipn_real/ipn_230_g05_throw_left.mp4`
- `bridge/samples/ipn_real/ipn_230_g06_throw_right.mp4`

All four clips are 640x480, 30 FPS.

## Observed bridge-layer static outputs

These are the Python bridge static labels observed during replay. They are not the final dynamic verdict.

| Clip | Main static outputs |
|---|---|
| `ipn_229_g05_throw_left.mp4` | `v` |
| `ipn_229_g06_throw_right.mp4` | `point` |
| `ipn_230_g05_throw_left.mp4` | `point -> fist` |
| `ipn_230_g06_throw_right.mp4` | `point` |

## Observed dynamic events from real UDP landmark stream

These values were computed by replaying the actual UDP packets and applying the same motion thresholds used by `ExternalMotionGestureRecognizer`.

### Representative results

| Clip | Packet count | Swipe count | Snap count | Notes |
|---|---:|---:|---:|---|
| `ipn_229_g05_throw_left.mp4` | 91 | 4 | 2 | Mixed left/right swipes, plus snap-like thumb-middle separation events |
| `ipn_229_g06_throw_right.mp4` | 83 | 6 | 0 | Strong swipe-like motion from real UDP packet landmarks |

## What this baseline proves

- Offline video replay into the Python bridge works.
- UDP landmark transport works.
- Unity-side motion logic can be driven from real external replay data.
- Dynamic events can be derived from actual replay packets, not only from synthetic PlayMode tests.

## What this baseline does not prove

- It does not prove semantic one-to-one mapping between IPN labels like `throw left/right` and your game labels.
- It does not prove that every clip should trigger only one dynamic event type.
- It does not yet include a large curated benchmark across many IPN classes.

## Recommended smoke-test commands

Use the ASCII-path Python environment for stable MediaPipe runtime:

```bash
C:\mp310\venv\Scripts\python.exe bridge/start_offline_gesture_test.py --video "E:/毕设/gesture-game/unity-spell-guard/bridge/samples/ipn_real/ipn_229_g05_throw_left.mp4" --once --no-preview
```

```bash
C:\mp310\venv\Scripts\python.exe bridge/start_offline_gesture_test.py --video "E:/毕设/gesture-game/unity-spell-guard/bridge/samples/ipn_real/ipn_229_g06_throw_right.mp4" --once --no-preview
```

## Recommended next tuning targets

If dynamic recognition still feels noisy in live play, tune in this order:

1. `ExternalMotionGestureRecognizer`
   - `swipeMinDistance`
   - `swipeMaxVerticalDrift`
   - `swipeCooldownSeconds`
   - `snapCloseDistance`
   - `snapReleaseDistance`
2. `ExternalGestureBridgeProvider`
   - `snapshotTimeout`
   - `motionEventTimeout`
3. Curate a larger IPN validation set
   - more `G05/G06`
   - negative clips
   - clips that should not trigger snap

## Regression expectation after current fix

Future changes should not regress the following properties:

- queued UDP packets are not silently overwritten before motion recognition
- queued bridge frames are processed in order
- motion timing uses packet timestamps when available
- real IPN replay remains capable of producing swipe-like events from UDP landmark data
