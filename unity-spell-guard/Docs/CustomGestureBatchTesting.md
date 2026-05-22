# Custom Gesture Batch Testing

This tool tests the current custom gesture pipeline without opening a gameplay scene:

1. Load extracted hand-landmark clips from JSON.
2. Build dynamic `CustomGestureTemplate` objects from `train` clips.
3. Reuse `CustomGestureTrajectoryTemplateBuilder`, `CustomGestureRecognizer`, and DTW matching.
4. Evaluate `test` clips and export summary JSON plus per-clip CSV.

It is designed for datasets such as Jester after a separate MediaPipe Hand Landmarker extraction pass.

## Dataset JSON

```json
{
  "DatasetName": "jester_subset",
  "DefaultFps": 30,
  "Clips": [
    {
      "ClipId": "video_0001",
      "Label": "swipe_right",
      "Split": "train",
      "Handedness": "Right",
      "Fps": 30,
      "Frames": [
        {
          "Time": 0.0,
          "Confidence": 1.0,
          "StaticGesture": "OpenPalm",
          "HasPalmCenter": false,
          "Landmarks": [
            { "X": 0.50, "Y": 0.50 }
          ]
        }
      ]
    }
  ]
}
```

Each frame should contain 21 normalized hand landmarks in MediaPipe order. If `PalmCenter` is omitted, the tool derives it from landmarks 0, 5, and 17. Use `Split=train` for template enrollment and `Split=test` for evaluation.

## Unity Menu

Open Unity and run:

`Spell Guard > Custom Gestures > Run Batch Test From Json`

Choose the dataset JSON, then choose an output folder. The tool writes:

- `custom_gesture_batch_summary.json`
- `custom_gesture_batch_results.csv`

## Batch Mode

```powershell
Unity.exe -batchmode -quit -projectPath E:\姣曡\gesture-game\unity-spell-guard ^
  -executeMethod SpellGuard.EditorTools.CustomGestureBatchTestRunner.RunFromCommandLine ^
  -gestureDataset E:\datasets\jester_landmarks\subset.json ^
  -gestureOutput E:\datasets\jester_landmarks\reports
```

The command exits with `0` only when every evaluated clip is correct. This makes it suitable for regression checks before a demo or thesis experiment export.

## Jester Pipeline

The Jester package used here is the multi-part frame archive under `训练集/20bnjester-v1-00..02`. The local pipeline does not depend on the unrelated `训练集/old` test videos.

Run the complete local chain:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/gesture_batch/run_jester_spellguard_batch.ps1 -DatasetRoot ".\训练集"
```

This extracts a small unlabeled sample of Jester frame folders, mines MediaPipe hand trajectories, writes `build-temp/jester_mined_motion_subset.json`, writes diagnostics to `build-temp/jester_mined_motion_report.csv`, and runs the project PlayMode tests.

The tested chain is:

`Jester frame folders -> MediaPipe Hand Landmarker -> mined motion labels -> SpellGuard dataset JSON -> custom gesture enrollment -> DTW evaluation`

The mined labels are intentionally simple trajectory classes such as `motion_left`, `motion_right`, `motion_up`, and `motion_down`. They are used for local regression of the custom dynamic gesture flow, not as a replacement for a full supervised Jester benchmark.

If the first sample does not provide enough matching train/test labels, increase the extracted clip count:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/gesture_batch/run_jester_spellguard_batch.ps1 `
  -DatasetRoot ".\训练集" `
  -MaxDirectories 120 `
  -TrainPerLabel 2 `
  -TestPerLabel 2
```

For stability testing, keep mined labels handedness-specific so that a dynamic template is enrolled and validated with the same hand:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/gesture_batch/run_jester_spellguard_batch.ps1 `
  -DatasetRoot ".\unity-spell-guard\训练集" `
  -FramesOut build-temp/jester_clip_sample_40 `
  -DatasetJson build-temp/jester_mined_motion_subset_40_samehand.json `
  -ReportOut build-temp/jester_mined_motion_report_40_samehand.csv `
  -MaxDirectories 40 `
  -TrainPerLabel 2 `
  -TestPerLabel 3 `
  -SameHandedLabels `
  -SkipDotnetTests
```

Then run the external custom-flow regression harness:

```powershell
dotnet run --project tools/ExternalRegression/ExternalRegression.csproj -- `
  --root . `
  --dataset build-temp/jester_mined_motion_subset_40_samehand.json `
  --report build-temp/external-regression-report-40-samehand
```

This harness runs outside the Unity scene and mirrors the custom gesture flow:

`train clip -> CustomGestureRecorder sampling -> template build -> save template JSON -> reload library -> select target template -> held-out validation`

The report files are:

- `saved_templates.csv`
- `validation_results.csv`
- `library/*.json`

To run only the extraction and mining step:

```powershell
C:\Users\FNHF\AppData\Local\Programs\Python\Python311\python.exe scripts/gesture_batch/mine_jester_motion_dataset.py `
  --frames-root build-temp/jester_clip_sample/20bn-jester-v1 `
  --output build-temp/jester_mined_motion_subset.json `
  --report build-temp/jester_mined_motion_report.csv `
  --model build-temp/models/hand_landmarker.task `
  --train-per-label 1 `
  --test-per-label 1
```

The Unity batch runner can then be launched from:

`Spell Guard > Custom Gestures > Run Batch Test From Json`

Select `build-temp/jester_mined_motion_subset.json` and an output folder. The output includes `custom_gesture_batch_summary.json` and `custom_gesture_batch_results.csv`.
