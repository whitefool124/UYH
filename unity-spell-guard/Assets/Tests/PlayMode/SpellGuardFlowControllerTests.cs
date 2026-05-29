using NUnit.Framework;
using SpellGuard.Combat;
using SpellGuard.Core;
using SpellGuard.InputSystem;
using UnityEngine;

namespace SpellGuard.Tests.PlayMode
{
    public class SpellGuardFlowControllerTests
    {
        private sealed class TrackingInputProvider : GestureInputProviderBase
        {
            public int ClearCalls { get; private set; }

            public override GestureSnapshot CurrentSnapshot => GestureSnapshot.Missing;

            public override void ClearTransientInputs()
            {
                ClearCalls += 1;
            }
        }

        private GameObject root;
        private SpellGuardFlowController flowController;
        private TrackingInputProvider inputProvider;
        private GestureInputRouter inputRouter;
        private GameFlowManager gameFlow;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("SpellGuardFlowControllerTestsRoot");
            flowController = root.AddComponent<SpellGuardFlowController>();
            inputProvider = root.AddComponent<TrackingInputProvider>();
            inputRouter = root.AddComponent<GestureInputRouter>();
            gameFlow = root.AddComponent<GameFlowManager>();
            SetPrivateField(flowController, "inputProvider", inputProvider);
            SetPrivateField(flowController, "inputRouter", inputRouter);
            SetPrivateField(flowController, "gameFlow", gameFlow);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void MenuScreenStatusReportsMenuTitle()
        {
            var status = flowController.GetScreenStatus();

            Assert.That(status.Title, Is.EqualTo("主菜单"));
        }

        [Test]
        public void ViewDataCarriesHintText()
        {
            var viewData = flowController.GetViewData();

            Assert.That(viewData.HintText, Is.Not.Empty);
        }

        [Test]
        public void StartRunClearsTransientInputs()
        {
            flowController.StartRun();

            Assert.That(inputProvider.ClearCalls, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void ReturnToMenuClearsTransientInputs()
        {
            flowController.ReturnToMenu();

            Assert.That(inputProvider.ClearCalls, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void StartTrainingDoesNotAllowRunUntilTrainingGoalsAreComplete()
        {
            flowController.StartTraining();

            flowController.StartRunFromTraining();

            Assert.That(flowController.Screen, Is.EqualTo(SpellGuardScreen.Training));
            Assert.That(flowController.HintText, Does.Contain("训练目标未完成"));
        }

        [Test]
        public void CompletedTrainingCanEnterRun()
        {
            flowController.StartTraining();
            flowController.RecordTrainingPointerCheck();
            InvokeSpellResolved(SpellType.Fire, 0);
            InvokeSpellResolved(SpellType.Ice, 0);
            InvokeSpellResolved(SpellType.Shield, 0);
            flowController.RecordTrainingAction(TransientAction(GestureIntent.TrainingSwipe));
            flowController.RecordTrainingAction(TransientAction(GestureIntent.TrainingSpecialConfirm));

            flowController.StartRunFromTraining();

            Assert.That(flowController.Screen, Is.EqualTo(SpellGuardScreen.Playing));
            Assert.That(inputProvider.ClearCalls, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void TrainingStepsAdvanceInRequiredOrder()
        {
            flowController.StartTraining();

            Assert.That(flowController.TrainingStep, Is.EqualTo(TrainingGestureStep.Point));
            flowController.RecordTrainingAction(TransientAction(GestureIntent.TrainingSwipe));
            Assert.That(flowController.TrainingStep, Is.EqualTo(TrainingGestureStep.Point));

            flowController.RecordTrainingPointerCheck();
            Assert.That(flowController.TrainingStep, Is.EqualTo(TrainingGestureStep.Fist));
            InvokeSpellResolved(SpellType.Fire, 0);
            Assert.That(flowController.TrainingStep, Is.EqualTo(TrainingGestureStep.VSign));
            InvokeSpellResolved(SpellType.Ice, 0);
            Assert.That(flowController.TrainingStep, Is.EqualTo(TrainingGestureStep.OpenPalm));
            InvokeSpellResolved(SpellType.Shield, 0);
            Assert.That(flowController.TrainingStep, Is.EqualTo(TrainingGestureStep.Swipe));
            flowController.RecordTrainingAction(TransientAction(GestureIntent.TrainingSwipe));
            Assert.That(flowController.TrainingStep, Is.EqualTo(TrainingGestureStep.SnapOrPointToFist));
            flowController.RecordTrainingAction(TransientAction(GestureIntent.TrainingSpecialConfirm));

            Assert.That(flowController.TrainingStep, Is.EqualTo(TrainingGestureStep.Complete));
            Assert.That(flowController.TrainingComplete, Is.True);
            Assert.That(flowController.TrainingStepFeedback, Does.Contain("训练流程已完成"));
        }

        [Test]
        public void CycleInputModeSettingUpdatesRouterMode()
        {
            Assert.That(inputRouter.Mode, Is.EqualTo(GestureInputRouter.InputMode.ExternalBridge));

            flowController.CycleInputModeSetting();

            Assert.That(inputRouter.Mode, Is.EqualTo(GestureInputRouter.InputMode.Mock));
            Assert.That(flowController.InputModeLabel, Is.EqualTo("Mock"));
        }

        [Test]
        public void PlayingRunMovesToResultsAfterVictoryScore()
        {
            flowController.StartRun();

            gameFlow.ReportCombatScore(gameFlow.TargetScoreToWin);
            InvokePrivateMethod(flowController, "Update");

            Assert.That(flowController.Screen, Is.EqualTo(SpellGuardScreen.Results));
            Assert.That(flowController.CurrentRunResult, Is.EqualTo(SpellGuardRunResult.Victory));
        }

        [Test]
        public void CombatViewDataCarriesSpellBreakdown()
        {
            flowController.StartRun();

            InvokeSpellResolved(SpellType.Fire, 1);
            InvokeSpellResolved(SpellType.Ice, 1);
            InvokeSpellResolved(SpellType.Shield, 0);

            var viewData = flowController.GetViewData();
            Assert.That(viewData.CombatCasts, Is.EqualTo(3));
            Assert.That(viewData.CombatHits, Is.EqualTo(2));
            Assert.That(viewData.CombatFireCasts, Is.EqualTo(1));
            Assert.That(viewData.CombatIceCasts, Is.EqualTo(1));
            Assert.That(viewData.CombatShieldCasts, Is.EqualTo(1));
            Assert.That(viewData.HitRate, Is.EqualTo(67));
        }

        [Test]
        public void StartRunResetsCombatSpellBreakdown()
        {
            flowController.StartRun();
            InvokeSpellResolved(SpellType.Fire, 1);
            InvokeSpellResolved(SpellType.Shield, 0);

            flowController.StartRun();

            Assert.That(flowController.CombatCasts, Is.EqualTo(0));
            Assert.That(flowController.CombatFireCasts, Is.EqualTo(0));
            Assert.That(flowController.CombatIceCasts, Is.EqualTo(0));
            Assert.That(flowController.CombatShieldCasts, Is.EqualTo(0));
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private void InvokeSpellResolved(SpellType spell, int hitCount)
        {
            var method = typeof(SpellGuardFlowController).GetMethod("HandleSpellResolved", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(flowController, new object[] { spell, hitCount });
        }

        private static GestureAction TransientAction(GestureIntent intent)
        {
            return new GestureAction
            {
                Intent = intent,
                Confidence = 1f,
                TriggeredTime = Time.time,
                SourceKind = GestureCommandKind.Motion,
                Handedness = GestureHandedness.Unknown,
                TrackId = -1
            };
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, null);
        }
    }
}
