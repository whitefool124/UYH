using System;
using UnityEngine;

namespace SpellGuard.InputSystem
{
    public class GestureInputRouter : GestureInputProviderBase
    {
        public enum InputMode
        {
            Mock,
            NativeMediapipe,
            ExternalBridge
        }

        [SerializeField] private InputMode mode = InputMode.ExternalBridge;
        [SerializeField] private MockGestureInputProvider mockProvider;
        [SerializeField] private NativeMediapipeGestureProvider nativeMediapipeProvider;
        [SerializeField] private ExternalGestureBridgeProvider externalBridgeProvider;
        [Header("Custom Gesture")]
        [SerializeField] private bool customGesturesEnabled = true;
        [SerializeField] private float customGestureMinConfidence = 0.55f;
        [SerializeField] private float customGestureValidationMinConfidence = 0.2f;
        [SerializeField] private float customGestureWindowSeconds = 1.6f;
        [SerializeField] private float customGestureValidationWindowSeconds = 3.0f;
        [SerializeField] private float customGestureCooldownSeconds = 0.85f;

        private readonly CustomGestureRecognizer customGestureRecognizer = new CustomGestureRecognizer();
        private readonly CustomGestureRecognizer customGestureValidationRecognizer = new CustomGestureRecognizer();
        private CustomGestureLibrary customGestureLibrary;
        private bool customGestureLibraryLoaded;

        public event Action<InputMode> ModeChanged;

        public override GestureSnapshot CurrentSnapshot
        {
            get
            {
                switch (mode)
                {
                    case InputMode.NativeMediapipe:
                        return nativeMediapipeProvider != null ? nativeMediapipeProvider.CurrentSnapshot : GestureSnapshot.Missing;
                    case InputMode.ExternalBridge:
                        return externalBridgeProvider != null ? externalBridgeProvider.CurrentSnapshot : GestureSnapshot.Missing;
                    case InputMode.Mock:
                    default:
                        return mockProvider != null ? mockProvider.CurrentSnapshot : GestureSnapshot.Missing;
                }
            }
        }

        public override MotionGestureEvent CurrentMotionGesture
        {
            get
            {
                switch (mode)
                {
                    case InputMode.NativeMediapipe:
                        return nativeMediapipeProvider != null ? nativeMediapipeProvider.CurrentMotionGesture : MotionGestureEvent.None;
                    case InputMode.ExternalBridge:
                        return externalBridgeProvider != null ? externalBridgeProvider.CurrentMotionGesture : MotionGestureEvent.None;
                    case InputMode.Mock:
                    default:
                        return MotionGestureEvent.None;
                }
            }
        }

        public override GestureFrame CurrentGestureFrame
        {
            get
            {
                switch (mode)
                {
                    case InputMode.NativeMediapipe:
                        return nativeMediapipeProvider != null ? nativeMediapipeProvider.CurrentGestureFrame : GestureFrame.Empty(GestureSourceKind.NativeMediapipe);
                    case InputMode.ExternalBridge:
                        return externalBridgeProvider != null ? externalBridgeProvider.CurrentGestureFrame : GestureFrame.Empty(GestureSourceKind.ExternalBridge);
                    case InputMode.Mock:
                    default:
                        return mockProvider != null ? mockProvider.CurrentGestureFrame : GestureFrame.Empty(GestureSourceKind.Mock);
                }
            }
        }

        public override GestureCommand CurrentGestureCommand
        {
            get
            {
                switch (mode)
                {
                    case InputMode.NativeMediapipe:
                        return nativeMediapipeProvider != null ? nativeMediapipeProvider.CurrentGestureCommand : GestureCommand.None;
                    case InputMode.ExternalBridge:
                        return externalBridgeProvider != null ? externalBridgeProvider.CurrentGestureCommand : GestureCommand.None;
                    case InputMode.Mock:
                    default:
                        return mockProvider != null ? mockProvider.CurrentGestureCommand : GestureCommand.None;
                }
            }
        }

        public override GestureCommand[] RecentGestureCommands
        {
            get
            {
                switch (mode)
                {
                    case InputMode.NativeMediapipe:
                        return nativeMediapipeProvider != null ? nativeMediapipeProvider.RecentGestureCommands : Array.Empty<GestureCommand>();
                    case InputMode.ExternalBridge:
                        return externalBridgeProvider != null ? externalBridgeProvider.RecentGestureCommands : Array.Empty<GestureCommand>();
                    case InputMode.Mock:
                    default:
                        return mockProvider != null ? mockProvider.RecentGestureCommands : Array.Empty<GestureCommand>();
                }
            }
        }

        public override GestureAction CurrentCustomAction
        {
            get
            {
                if (!customGesturesEnabled)
                {
                    return GestureAction.None;
                }

                EnsureCustomGestureLibraryLoaded();
                return customGestureRecognizer.TryResolve(CurrentGestureFrame, customGestureLibrary.Templates, Time.time, out var action)
                    ? action
                    : GestureAction.None;
            }
        }

        public InputMode Mode => mode;
        public CustomGestureLibrary CustomGestures
        {
            get
            {
                EnsureCustomGestureLibraryCreated();
                return customGestureLibrary;
            }
        }
        public string LastCustomGestureName => customGestureRecognizer.LastMatchedName;
        public float LastCustomGestureScore => customGestureRecognizer.LastScore;
        public float LastCustomGestureValidationScore => customGestureValidationRecognizer.LastScore;
        public string LastCustomGestureValidationFailureReason => customGestureValidationRecognizer.LastFailureReason;
        public int CustomGestureValidationWindowFrameCount => customGestureValidationRecognizer.WindowFrameCount;
        public float CustomGestureValidationWindowDurationSeconds => customGestureValidationRecognizer.WindowDurationSeconds;
        public int CustomGestureTemplateCount
        {
            get
            {
                EnsureCustomGestureLibraryLoaded();
                return customGestureLibrary.Templates.Count;
            }
        }

        public string GetCustomGestureTemplateLabel(int index)
        {
            EnsureCustomGestureLibraryLoaded();
            if (index < 0 || index >= customGestureLibrary.Templates.Count)
            {
                return "无";
            }

            var template = customGestureLibrary.Templates[index];
            return FormatTemplateLabel(index, template);
        }

        public string GetCustomGestureTemplateListText(int selectedIndex)
        {
            EnsureCustomGestureLibraryLoaded();
            if (customGestureLibrary.Templates.Count <= 0)
            {
                return "模板库为空";
            }

            var lines = new System.Text.StringBuilder();
            for (var index = 0; index < customGestureLibrary.Templates.Count; index++)
            {
                if (index > 0)
                {
                    lines.Append('\n');
                }

                lines.Append(index == selectedIndex ? "▶ " : "  ");
                lines.Append(FormatTemplateLabel(index, customGestureLibrary.Templates[index]));
            }

            return lines.ToString();
        }

        public int GetCustomGestureTemplateIndex(string gestureId)
        {
            EnsureCustomGestureLibraryLoaded();
            for (var index = 0; index < customGestureLibrary.Templates.Count; index++)
            {
                if (string.Equals(customGestureLibrary.Templates[index].GestureId, gestureId, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        public bool TryEvaluateCustomGestureTemplate(int index, GestureFrame frame, float now, out string targetLabel, out GestureHandedness requiredHandedness, out bool matched)
        {
            EnsureCustomGestureLibraryLoaded();
            targetLabel = "无";
            requiredHandedness = GestureHandedness.Unknown;
            matched = false;
            if (index < 0 || index >= customGestureLibrary.Templates.Count)
            {
                return false;
            }

            var template = customGestureLibrary.Templates[index];
            targetLabel = string.IsNullOrWhiteSpace(template.DisplayName) ? template.GestureId : template.DisplayName;
            requiredHandedness = template.RequiredHandedness;
            matched = customGestureValidationRecognizer.TryResolveSingle(frame, template, now);
            return true;
        }

        public bool TryGetCustomGestureTemplate(int index, out CustomGestureTemplate template)
        {
            EnsureCustomGestureLibraryLoaded();
            if (index < 0 || index >= customGestureLibrary.Templates.Count)
            {
                template = null;
                return false;
            }

            template = customGestureLibrary.Templates[index];
            return true;
        }

        public void ReloadCustomGestures()
        {
            EnsureCustomGestureLibraryCreated();
            customGestureLibrary.LoadAll();
            customGestureLibraryLoaded = true;
            customGestureRecognizer.Reset();
            customGestureValidationRecognizer.Reset();
        }

        public void SaveCustomGesture(CustomGestureTemplate template)
        {
            EnsureCustomGestureLibraryCreated();
            if (customGestureLibrary.Save(template))
            {
                customGestureLibraryLoaded = true;
                customGestureRecognizer.Reset();
                customGestureValidationRecognizer.Reset();
            }
        }

        public void ResetCustomGestureValidationRecognizer()
        {
            customGestureValidationRecognizer.Reset();
        }

        public bool DeleteCustomGestureTemplate(int index)
        {
            EnsureCustomGestureLibraryLoaded();
            if (index < 0 || index >= customGestureLibrary.Templates.Count)
            {
                return false;
            }

            var gestureId = customGestureLibrary.Templates[index].GestureId;
            if (!customGestureLibrary.Delete(gestureId))
            {
                return false;
            }

            customGestureLibrary.LoadAll();
            customGestureLibraryLoaded = true;
            customGestureRecognizer.Reset();
            customGestureValidationRecognizer.Reset();
            return true;
        }

        public override void ClearTransientInputs()
        {
            switch (mode)
            {
                case InputMode.NativeMediapipe:
                    nativeMediapipeProvider?.ClearTransientInputs();
                    break;
                case InputMode.ExternalBridge:
                    externalBridgeProvider?.ClearTransientInputs();
                    break;
                case InputMode.Mock:
                default:
                    mockProvider?.ClearTransientInputs();
                    break;
            }

            customGestureRecognizer.Reset();
            customGestureValidationRecognizer.Reset();
        }

        private void Awake()
        {
            EnsureCustomGestureLibraryCreated();
            customGestureRecognizer.Configure(customGestureMinConfidence, customGestureWindowSeconds, customGestureCooldownSeconds);
            customGestureValidationRecognizer.Configure(customGestureValidationMinConfidence, customGestureValidationWindowSeconds, customGestureCooldownSeconds);
            ReloadCustomGestures();
        }

        public void SetMode(InputMode nextMode)
        {
            if (mode == nextMode)
            {
                return;
            }

            mode = nextMode;
            customGestureRecognizer.Reset();
            customGestureValidationRecognizer.Reset();
            ModeChanged?.Invoke(mode);
        }

        public void SetCustomGesturesEnabled(bool enabled)
        {
            if (customGesturesEnabled == enabled)
            {
                return;
            }

            customGesturesEnabled = enabled;
            customGestureRecognizer.Reset();
            customGestureValidationRecognizer.Reset();
        }

        private void EnsureCustomGestureLibraryLoaded()
        {
            if (customGestureLibraryLoaded)
            {
                return;
            }

            ReloadCustomGestures();
        }

        private void EnsureCustomGestureLibraryCreated()
        {
            customGestureLibrary ??= new CustomGestureLibrary();
        }

        private static string FormatKind(CustomGestureKind kind)
        {
            return kind == CustomGestureKind.StaticPose ? "静态" : "动态";
        }

        private static string FormatTemplateLabel(int index, CustomGestureTemplate template)
        {
            var name = string.IsNullOrWhiteSpace(template.DisplayName) ? template.GestureId : template.DisplayName;
            return $"{index + 1}号 {name} · {FormatKind(template.Kind)} · {FormatHandedness(template.RequiredHandedness)}";
        }

        private static string FormatHandedness(GestureHandedness handedness)
        {
            return handedness switch
            {
                GestureHandedness.Left => "左手",
                GestureHandedness.Right => "右手",
                _ => "未知手"
            };
        }
    }
}
