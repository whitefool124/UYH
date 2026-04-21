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

        [SerializeField] private InputMode mode = InputMode.NativeMediapipe;
        [SerializeField] private KeyCode toggleModeKey = KeyCode.F1;
        [SerializeField] private MockGestureInputProvider mockProvider;
        [SerializeField] private NativeMediapipeGestureProvider nativeMediapipeProvider;
        [SerializeField] private ExternalGestureBridgeProvider externalBridgeProvider;

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
                        return nativeMediapipeProvider != null ? nativeMediapipeProvider.RecentGestureCommands : System.Array.Empty<GestureCommand>();
                    case InputMode.ExternalBridge:
                        return externalBridgeProvider != null ? externalBridgeProvider.RecentGestureCommands : System.Array.Empty<GestureCommand>();
                    case InputMode.Mock:
                    default:
                        return mockProvider != null ? mockProvider.RecentGestureCommands : System.Array.Empty<GestureCommand>();
                }
            }
        }

        public InputMode Mode => mode;

        private void Update()
        {
            if (Input.GetKeyDown(toggleModeKey))
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
            ModeChanged?.Invoke(mode);
        }
    }
}
