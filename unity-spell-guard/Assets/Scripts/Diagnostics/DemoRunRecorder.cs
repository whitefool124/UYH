using System;
using System.Globalization;
using System.IO;
using System.Text;
using SpellGuard.Combat;
using SpellGuard.Core;
using SpellGuard.InputSystem;
using SpellGuard.Player;
using UnityEngine;

namespace SpellGuard.Diagnostics
{
    public sealed class DemoRunRecorder : MonoBehaviour
    {
        [SerializeField] private SpellGuardFlowController flowController;
        [SerializeField] private GestureInputRouter inputRouter;
        [SerializeField] private string outputDirectoryName = "ExperimentResults";
        [SerializeField] private bool recordOnStart = true;
        [SerializeField] private bool exportOnResult = true;

        private string sessionId;
        private string startTime;
        private float sessionStartedAt;
        private SpellGuardScreen lastScreen;
        private bool hasLastScreen;
        private bool enteredStartMenu;
        private bool enteredTutorial;
        private bool enteredTraining;
        private bool enteredCombat;
        private int menuTransitions;
        private int tutorialTransitions;
        private int settingsTransitions;
        private int trainingTransitions;
        private int combatTransitions;
        private int resultTransitions;
        private int restartCount;
        private int returnToMenuCount;
        private int fireCasts;
        private int iceCasts;
        private int shieldCasts;
        private int staticCommandCount;
        private int motionCommandCount;
        private string lastCommandKey;
        private SpellGuardFlowController subscribedFlowController;
        private SpellGuardRunResult exportedResult;

        public bool IsRecording { get; private set; }
        public string LastExportPath { get; private set; } = string.Empty;
        public DemoRunSummary CurrentSummary => BuildSummary();

        private void Start()
        {
            RefreshSubscription();
            if (recordOnStart)
            {
                StartRecording();
            }
        }

        private void OnEnable()
        {
            RefreshSubscription();
        }

        private void OnDisable()
        {
            if (subscribedFlowController != null)
            {
                subscribedFlowController.SpellResolvedForDiagnostics -= HandleSpellResolved;
                subscribedFlowController = null;
            }
        }

        private void Update()
        {
            if (!IsRecording || flowController == null)
            {
                return;
            }

            RecordScreenIfChanged(flowController.Screen);
            RecordCommandIfChanged();
            TryExportCompletedRun();
        }

        public void Configure(SpellGuardFlowController flow, GestureInputRouter router)
        {
            if (subscribedFlowController != null)
            {
                subscribedFlowController.SpellResolvedForDiagnostics -= HandleSpellResolved;
                subscribedFlowController = null;
            }

            flowController = flow;
            inputRouter = router;
            RefreshSubscription();
        }

        public void StartRecording()
        {
            ResetSession();
            IsRecording = true;
            sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            startTime = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            sessionStartedAt = Time.unscaledTime;
            if (flowController != null)
            {
                RecordScreenIfChanged(flowController.Screen);
            }
        }

        public void StopRecording()
        {
            IsRecording = false;
        }

        public string ExportCsv()
        {
            var directory = ResolveOutputDirectory();
            Directory.CreateDirectory(directory);
            var timestamp = string.IsNullOrWhiteSpace(sessionId) ? DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) : sessionId;
            LastExportPath = Path.Combine(directory, $"demo_run_{timestamp}.csv");
            File.WriteAllText(LastExportPath, BuildCsv(), Encoding.UTF8);
            return LastExportPath;
        }

        public string BuildCsv()
        {
            var summary = BuildSummary();
            var builder = new StringBuilder();
            builder.AppendLine("session_id,start_time,input_mode,entered_start_menu,entered_tutorial,entered_training,entered_combat,combat_result,elapsed_seconds,fire_cast_count,ice_cast_count,shield_cast_count,static_command_count,motion_command_count,restart_count,return_to_menu_count,current_screen,combat_score,combat_hits,combat_casts,training_casts,menu_transitions,tutorial_transitions,settings_transitions,training_transitions,combat_transitions,result_transitions");
            builder.Append(summary.SessionId).Append(',')
                .Append(EscapeCsv(summary.StartTime)).Append(',')
                .Append(summary.InputMode).Append(',')
                .Append(summary.EnteredStartMenu ? "true" : "false").Append(',')
                .Append(summary.EnteredTutorial ? "true" : "false").Append(',')
                .Append(summary.EnteredTraining ? "true" : "false").Append(',')
                .Append(summary.EnteredCombat ? "true" : "false").Append(',')
                .Append(summary.RunResult).Append(',')
                .Append(Format(summary.ElapsedSeconds)).Append(',')
                .Append(summary.FireCasts).Append(',')
                .Append(summary.IceCasts).Append(',')
                .Append(summary.ShieldCasts).Append(',')
                .Append(summary.StaticCommandCount).Append(',')
                .Append(summary.MotionCommandCount).Append(',')
                .Append(summary.RestartCount).Append(',')
                .Append(summary.ReturnToMenuCount).Append(',')
                .Append(summary.CurrentScreen).Append(',')
                .Append(summary.CombatScore).Append(',')
                .Append(summary.CombatHits).Append(',')
                .Append(summary.CombatCasts).Append(',')
                .Append(summary.TrainingCasts).Append(',')
                .Append(summary.MenuTransitions).Append(',')
                .Append(summary.TutorialTransitions).Append(',')
                .Append(summary.SettingsTransitions).Append(',')
                .Append(summary.TrainingTransitions).Append(',')
                .Append(summary.CombatTransitions).Append(',')
                .Append(summary.ResultTransitions).AppendLine();
            return builder.ToString();
        }

        private void RecordScreenIfChanged(SpellGuardScreen screen)
        {
            if (hasLastScreen && lastScreen == screen)
            {
                return;
            }

            if (hasLastScreen)
            {
                if (lastScreen == SpellGuardScreen.Results && screen == SpellGuardScreen.Playing)
                {
                    restartCount++;
                }

                if (screen == SpellGuardScreen.Menu && lastScreen != SpellGuardScreen.Menu)
                {
                    returnToMenuCount++;
                }
            }

            hasLastScreen = true;
            lastScreen = screen;
            switch (screen)
            {
                case SpellGuardScreen.Menu:
                    enteredStartMenu = true;
                    menuTransitions++;
                    break;
                case SpellGuardScreen.Tutorial:
                    enteredTutorial = true;
                    tutorialTransitions++;
                    break;
                case SpellGuardScreen.Settings:
                    settingsTransitions++;
                    break;
                case SpellGuardScreen.Training:
                    enteredTraining = true;
                    trainingTransitions++;
                    break;
                case SpellGuardScreen.Playing:
                    enteredCombat = true;
                    combatTransitions++;
                    break;
                case SpellGuardScreen.Results:
                    resultTransitions++;
                    break;
            }
        }

        private void HandleSpellResolved(SpellType spell, int hitCount, SpellGuardScreen screen)
        {
            AddSpell(spell, 1);
        }

        private void RecordCommandIfChanged()
        {
            if (inputRouter == null)
            {
                return;
            }

            var command = inputRouter.CurrentGestureCommand;
            if (!command.IsValid)
            {
                lastCommandKey = string.Empty;
                return;
            }

            var key = $"{command.Kind}:{command.StaticGesture}:{command.MotionGesture}:{command.TriggeredTime:F3}";
            if (key == lastCommandKey)
            {
                return;
            }

            lastCommandKey = key;
            if (command.Kind == GestureCommandKind.Motion)
            {
                motionCommandCount++;
            }
            else if (command.Kind == GestureCommandKind.StaticPose)
            {
                staticCommandCount++;
            }
        }

        private void AddSpell(SpellType spell, int count)
        {
            switch (spell)
            {
                case SpellType.Fire:
                    fireCasts += count;
                    break;
                case SpellType.Ice:
                    iceCasts += count;
                    break;
                case SpellType.Shield:
                    shieldCasts += count;
                    break;
            }
        }

        private void TryExportCompletedRun()
        {
            if (!exportOnResult || flowController.CurrentRunResult == SpellGuardRunResult.None || exportedResult == flowController.CurrentRunResult)
            {
                return;
            }

            exportedResult = flowController.CurrentRunResult;
            ExportCsv();
        }

        private DemoRunSummary BuildSummary()
        {
            return new DemoRunSummary
            {
                SessionId = sessionId,
                StartTime = startTime,
                InputMode = inputRouter != null ? inputRouter.Mode.ToString() : "Unbound",
                EnteredStartMenu = enteredStartMenu,
                EnteredTutorial = enteredTutorial,
                EnteredTraining = enteredTraining,
                EnteredCombat = enteredCombat,
                ElapsedSeconds = IsRecording ? Mathf.Max(0f, Time.unscaledTime - sessionStartedAt) : 0f,
                CurrentScreen = flowController != null ? flowController.Screen.ToString() : "Unbound",
                RunResult = flowController != null ? flowController.CurrentRunResult.ToString() : SpellGuardRunResult.None.ToString(),
                CombatScore = flowController != null ? flowController.CombatScore : 0,
                CombatHits = flowController != null ? flowController.CombatHits : 0,
                CombatCasts = flowController != null ? flowController.CombatCasts : 0,
                TrainingCasts = flowController != null ? flowController.TrainingCasts : 0,
                FireCasts = fireCasts,
                IceCasts = iceCasts,
                ShieldCasts = shieldCasts,
                StaticCommandCount = staticCommandCount,
                MotionCommandCount = motionCommandCount,
                MenuTransitions = menuTransitions,
                TutorialTransitions = tutorialTransitions,
                SettingsTransitions = settingsTransitions,
                TrainingTransitions = trainingTransitions,
                CombatTransitions = combatTransitions,
                ResultTransitions = resultTransitions,
                RestartCount = restartCount,
                ReturnToMenuCount = returnToMenuCount
            };
        }

        private void RefreshSubscription()
        {
            if (flowController == null || subscribedFlowController == flowController)
            {
                return;
            }

            if (subscribedFlowController != null)
            {
                subscribedFlowController.SpellResolvedForDiagnostics -= HandleSpellResolved;
            }

            subscribedFlowController = flowController;
            subscribedFlowController.SpellResolvedForDiagnostics += HandleSpellResolved;
        }

        private void ResetSession()
        {
            hasLastScreen = false;
            lastScreen = SpellGuardScreen.Menu;
            enteredStartMenu = false;
            enteredTutorial = false;
            enteredTraining = false;
            enteredCombat = false;
            menuTransitions = 0;
            tutorialTransitions = 0;
            settingsTransitions = 0;
            trainingTransitions = 0;
            combatTransitions = 0;
            resultTransitions = 0;
            restartCount = 0;
            returnToMenuCount = 0;
            fireCasts = 0;
            iceCasts = 0;
            shieldCasts = 0;
            staticCommandCount = 0;
            motionCommandCount = 0;
            lastCommandKey = string.Empty;
            exportedResult = SpellGuardRunResult.None;
            LastExportPath = string.Empty;
        }

        private string ResolveOutputDirectory()
        {
            var safeDirectoryName = ResolveSafeDirectoryName(outputDirectoryName);
            if (Application.isEditor)
            {
                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                var outputPath = Path.GetFullPath(Path.Combine(projectRoot, safeDirectoryName));
                return outputPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
                    ? outputPath
                    : Path.Combine(projectRoot, "ExperimentResults");
            }

            var persistentRoot = Path.GetFullPath(Application.persistentDataPath);
            var persistentOutputPath = Path.GetFullPath(Path.Combine(persistentRoot, safeDirectoryName));
            return persistentOutputPath.StartsWith(persistentRoot, StringComparison.OrdinalIgnoreCase)
                ? persistentOutputPath
                : Path.Combine(persistentRoot, "ExperimentResults");
        }

        private static string ResolveSafeDirectoryName(string directoryName)
        {
            if (string.IsNullOrWhiteSpace(directoryName) || Path.IsPathRooted(directoryName))
            {
                return "ExperimentResults";
            }

            if (directoryName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                directoryName.Contains("/") ||
                directoryName.Contains("\\") ||
                directoryName.Contains(".."))
            {
                return "ExperimentResults";
            }

            return directoryName;
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Contains(",") || value.Contains("\"") || value.Contains("\n")
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
        }
    }

    public struct DemoRunSummary
    {
        public string SessionId;
        public string StartTime;
        public string InputMode;
        public bool EnteredStartMenu;
        public bool EnteredTutorial;
        public bool EnteredTraining;
        public bool EnteredCombat;
        public float ElapsedSeconds;
        public string CurrentScreen;
        public string RunResult;
        public int CombatScore;
        public int CombatHits;
        public int CombatCasts;
        public int TrainingCasts;
        public int FireCasts;
        public int IceCasts;
        public int ShieldCasts;
        public int StaticCommandCount;
        public int MotionCommandCount;
        public int MenuTransitions;
        public int TutorialTransitions;
        public int SettingsTransitions;
        public int TrainingTransitions;
        public int CombatTransitions;
        public int ResultTransitions;
        public int RestartCount;
        public int ReturnToMenuCount;
    }
}
