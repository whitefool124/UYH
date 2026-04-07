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

        public InputMode Mode => mode;

        private void Update()
        {
            if (Input.GetKeyDown(toggleModeKey))
            {
                switch (mode)
                {
                    case InputMode.Mock:
                        mode = InputMode.NativeMediapipe;
                        break;
                    case InputMode.NativeMediapipe:
                        mode = InputMode.ExternalBridge;
                        break;
                    default:
                        mode = InputMode.Mock;
                        break;
                }
            }
        }
    }
}
