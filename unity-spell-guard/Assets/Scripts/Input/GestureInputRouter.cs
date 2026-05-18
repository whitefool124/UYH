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

        [SerializeField] private InputMode mode = InputMode.Mock;
        [SerializeField] private KeyCode toggleModeKey = KeyCode.None;
        [SerializeField] private MockGestureInputProvider mockProvider;
        [SerializeField] private NativeMediapipeGestureProvider nativeMediapipeProvider;
        [SerializeField] private ExternalGestureBridgeProvider externalBridgeProvider;
        [Header("Custom Gesture")]
        [SerializeField] private bool customGesturesEnabled = true;
        [SerializeField] private float customGestureMinConfidence = 0.55f;
        [SerializeField] private float customGestureWindowSeconds = 1.6f;
        [SerializeField] private float customGestureCooldownSeconds = 0.85f;

        private readonly CustomGestureRecognizer customGestureRecognizer = new CustomGestureRecognizer();
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
            var name = string.IsNullOrWhiteSpace(template.DisplayName) ? template.GestureId : template.DisplayName;
            return $"{index + 1}/{customGestureLibrary.Templates.Count} {name} · {FormatKind(template.Kind)} · {FormatHandedness(template.RequiredHandedness)}";
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
            matched = customGestureRecognizer.TryResolveSingle(frame, template, now);
            return true;
        }

        public void ReloadCustomGestures()
        {
            EnsureCustomGestureLibraryCreated();
            customGestureLibrary.LoadAll();
            customGestureLibraryLoaded = true;
            customGestureRecognizer.Reset();
        }

        public void SaveCustomGesture(CustomGestureTemplate template)
        {
            EnsureCustomGestureLibraryCreated();
            if (customGestureLibrary.Save(template))
            {
                customGestureLibraryLoaded = true;
                customGestureRecognizer.Reset();
            }
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
        }

        private void Awake()
        {
            EnsureCustomGestureLibraryCreated();
            customGestureRecognizer.Configure(customGestureMinConfidence, customGestureWindowSeconds, customGestureCooldownSeconds);
            ReloadCustomGestures();
        }

        private void Update()
        {
            if (toggleModeKey != KeyCode.None && Input.GetKeyDown(toggleModeKey))
            {
                switch (mode)
                {
                    case InputMode.Mock:
                        SetMode(InputMode.NativeMediapipe);
                        break;
                    case InputMode.NativeMediapipe:
                        SetMode(InputMode.ExternalBridge);
                        break;
                    default:
                        SetMode(InputMode.Mock);
                        break;
                }
            }
        }

        public void SetMode(InputMode nextMode)
        {
            if (mode == nextMode)
            {
                return;
            }

            mode = nextMode;
            customGestureRecognizer.Reset();
            ModeChanged?.Invoke(mode);
        }

        public void SetCustomGesturesEnabled(bool enabled)
        {
            customGesturesEnabled = enabled;
            customGestureRecognizer.Reset();
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
