using System.IO;
using NUnit.Framework;
using SpellGuard.Combat;
using SpellGuard.Core;
using SpellGuard.Diagnostics;
using SpellGuard.InputSystem;
using UnityEngine;

namespace SpellGuard.Tests.PlayMode
{
    public class DemoRunRecorderTests
    {
        private GameObject root;
        private SpellGuardFlowController flowController;
        private GestureInputRouter inputRouter;
        private GameFlowManager gameFlow;
        private DemoRunRecorder recorder;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("DemoRunRecorderTestsRoot");
            flowController = root.AddComponent<SpellGuardFlowController>();
            inputRouter = root.AddComponent<GestureInputRouter>();
            gameFlow = root.AddComponent<GameFlowManager>();
            recorder = root.AddComponent<DemoRunRecorder>();
            SetPrivateField(flowController, "inputRouter", inputRouter);
            SetPrivateField(flowController, "gameFlow", gameFlow);
            SetPrivateField(recorder, "recordOnStart", false);
            SetPrivateField(recorder, "exportOnResult", false);
            recorder.Configure(flowController, inputRouter);
            recorder.StartRecording();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void BuildCsvIncludesThesisEvidenceFields()
        {
            var csv = recorder.BuildCsv();

            Assert.That(csv, Does.Contain("session_id,start_time,input_mode,entered_start_menu,entered_tutorial,entered_training,entered_combat,combat_result"));
            Assert.That(csv, Does.Contain("fire_cast_count,ice_cast_count,shield_cast_count,static_command_count,motion_command_count"));
            Assert.That(csv, Does.Contain("training_transitions,combat_transitions,result_transitions"));
        }

        [Test]
        public void TracksFlowTransitionsAndSpellCounts()
        {
            flowController.StartTraining();
            InvokePrivateMethod(recorder, "Update");
            InvokeSpellResolved(SpellType.Fire, 0);
            InvokeSpellResolved(SpellType.Ice, 0);
            InvokeSpellResolved(SpellType.Shield, 0);
            InvokePrivateMethod(recorder, "Update");

            flowController.StartRun();
            InvokePrivateMethod(recorder, "Update");

            var summary = recorder.CurrentSummary;
            Assert.That(summary.TrainingTransitions, Is.EqualTo(1));
            Assert.That(summary.CombatTransitions, Is.EqualTo(1));
            Assert.That(summary.FireCasts, Is.EqualTo(1));
            Assert.That(summary.IceCasts, Is.EqualTo(1));
            Assert.That(summary.ShieldCasts, Is.EqualTo(1));
        }

        [Test]
        public void ExportsDemoRunCsvToConfiguredDirectory()
        {
            var directoryName = Path.Combine("Temp", "DemoRunRecorderTests");
            SetPrivateField(recorder, "outputDirectoryName", directoryName);

            var path = recorder.ExportCsv();

            Assert.That(File.Exists(path), Is.True);
            Assert.That(Path.GetFileName(path), Does.StartWith("demo_run_"));
            Assert.That(File.ReadAllText(path), Does.Contain("Mock"));
            File.Delete(path);
        }

        [Test]
        public void UnsafeOutputDirectoryFallsBackToExperimentResults()
        {
            SetPrivateField(recorder, "outputDirectoryName", "..\\UnsafeDemoRunRecorderTests");

            var path = recorder.ExportCsv();

            Assert.That(path, Does.Contain("ExperimentResults"));
            Assert.That(File.Exists(path), Is.True);
            File.Delete(path);
        }

        [Test]
        public void AutoExportRunsOnceWhenResultAppears()
        {
            var directoryName = Path.Combine("Temp", "DemoRunRecorderAutoExportTests");
            SetPrivateField(recorder, "outputDirectoryName", directoryName);
            SetPrivateField(recorder, "exportOnResult", true);
            flowController.StartRun();
            gameFlow.ReportCombatScore(gameFlow.TargetScoreToWin);
            InvokePrivateMethod(flowController, "Update");

            InvokePrivateMethod(recorder, "Update");
            var firstPath = recorder.LastExportPath;
            InvokePrivateMethod(recorder, "Update");

            Assert.That(firstPath, Is.Not.Empty);
            Assert.That(File.Exists(firstPath), Is.True);
            Assert.That(recorder.LastExportPath, Is.EqualTo(firstPath));
            File.Delete(firstPath);
        }

        private void InvokeSpellResolved(SpellType spell, int hitCount)
        {
            var method = typeof(SpellGuardFlowController).GetMethod("HandleSpellResolved", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(flowController, new object[] { spell, hitCount });
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, null);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
