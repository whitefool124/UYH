using SpellGuard.InputSystem;
using UnityEngine;

namespace SpellGuard.Core
{
    public class SpellGuardGameSettings : MonoBehaviour
    {
        [SerializeField] private float[] confirmSecondsOptions = { 0.42f, 0.56f, 0.72f };
        [SerializeField] private SpellGuardDifficulty[] difficultyOptions =
        {
            SpellGuardDifficulty.Relaxed,
            SpellGuardDifficulty.Standard,
            SpellGuardDifficulty.Intense,
        };

        [SerializeField] private int confirmIndex = 1;
        [SerializeField] private int difficultyIndex = 1;
        [SerializeField] private GestureInputRouter.InputMode[] inputModeOptions =
        {
            GestureInputRouter.InputMode.Mock,
            GestureInputRouter.InputMode.NativeMediapipe,
            GestureInputRouter.InputMode.ExternalBridge,
        };

        [SerializeField] private int inputModeIndex = 2;
        [SerializeField] private float[] volumeOptions = { 0.25f, 0.5f, 0.75f, 1f };
        [SerializeField] private int musicVolumeIndex = 2;
        [SerializeField] private int sfxVolumeIndex = 3;
        [SerializeField] private float menuDwellSeconds = 0.82f;
        [SerializeField] private float menuBackHoldSeconds = 0.65f;
        [SerializeField] private bool fullscreen = true;

        public float ConfirmSeconds => confirmSecondsOptions[Mathf.Clamp(confirmIndex, 0, confirmSecondsOptions.Length - 1)];
        public SpellGuardDifficulty Difficulty => difficultyOptions[Mathf.Clamp(difficultyIndex, 0, difficultyOptions.Length - 1)];
        public GestureInputRouter.InputMode InputMode => inputModeOptions[Mathf.Clamp(inputModeIndex, 0, inputModeOptions.Length - 1)];
        public float MusicVolume => volumeOptions[Mathf.Clamp(musicVolumeIndex, 0, volumeOptions.Length - 1)];
        public float SfxVolume => volumeOptions[Mathf.Clamp(sfxVolumeIndex, 0, volumeOptions.Length - 1)];
        public float MenuDwellSeconds => menuDwellSeconds;
        public float MenuBackHoldSeconds => menuBackHoldSeconds;
        public string ConfirmLabel => $"{Mathf.RoundToInt(ConfirmSeconds * 1000f)} ms";
        public string MusicVolumeLabel => ToVolumeLabel(MusicVolume);
        public string SfxVolumeLabel => ToVolumeLabel(SfxVolume);
        public string FullscreenLabel => fullscreen ? "全屏" : "窗口";
        public string DifficultyLabel => Difficulty switch
        {
            SpellGuardDifficulty.Relaxed => "轻松",
            SpellGuardDifficulty.Intense => "紧张",
            _ => "标准",
        };
        public string InputModeLabel => InputMode switch
        {
            GestureInputRouter.InputMode.Mock => "Mock",
            GestureInputRouter.InputMode.NativeMediapipe => "Native MediaPipe",
            GestureInputRouter.InputMode.ExternalBridge => "ExternalBridge",
            _ => "Unknown",
        };

        private void Awake()
        {
            LoadPersistedSettings();
        }

        public void CycleConfirm()
        {
            confirmIndex = (confirmIndex + 1) % confirmSecondsOptions.Length;
            SpellGuardLocalProgress.SaveConfirmIndex(confirmIndex);
        }

        public void CycleDifficulty()
        {
            difficultyIndex = (difficultyIndex + 1) % difficultyOptions.Length;
            SpellGuardLocalProgress.SaveDifficultyIndex(difficultyIndex);
        }

        public GestureInputRouter.InputMode CycleInputMode()
        {
            inputModeIndex = (inputModeIndex + 1) % inputModeOptions.Length;
            SpellGuardLocalProgress.SaveInputModeIndex(inputModeIndex);
            return InputMode;
        }

        public void SetInputMode(GestureInputRouter.InputMode mode)
        {
            for (var index = 0; index < inputModeOptions.Length; index++)
            {
                if (inputModeOptions[index] == mode)
                {
                    inputModeIndex = index;
                    SpellGuardLocalProgress.SaveInputModeIndex(inputModeIndex);
                    return;
                }
            }
        }

        public void CycleMusicVolume()
        {
            musicVolumeIndex = (musicVolumeIndex + 1) % volumeOptions.Length;
            SpellGuardLocalProgress.SaveMusicVolumeIndex(musicVolumeIndex);
        }

        public void CycleSfxVolume()
        {
            sfxVolumeIndex = (sfxVolumeIndex + 1) % volumeOptions.Length;
            SpellGuardLocalProgress.SaveSfxVolumeIndex(sfxVolumeIndex);
        }

        public void ToggleFullscreen()
        {
            fullscreen = !fullscreen;
            UnityEngine.Screen.fullScreen = fullscreen;
            PlayerPrefs.SetInt("spellguard.fullscreen", fullscreen ? 1 : 0);
        }

        private void LoadPersistedSettings()
        {
            confirmIndex = Mathf.Clamp(SpellGuardLocalProgress.LoadConfirmIndex(confirmIndex), 0, confirmSecondsOptions.Length - 1);
            difficultyIndex = Mathf.Clamp(SpellGuardLocalProgress.LoadDifficultyIndex(difficultyIndex), 0, difficultyOptions.Length - 1);
            inputModeIndex = Mathf.Clamp(SpellGuardLocalProgress.LoadInputModeIndex(inputModeIndex), 0, inputModeOptions.Length - 1);
            musicVolumeIndex = Mathf.Clamp(SpellGuardLocalProgress.LoadMusicVolumeIndex(musicVolumeIndex), 0, volumeOptions.Length - 1);
            sfxVolumeIndex = Mathf.Clamp(SpellGuardLocalProgress.LoadSfxVolumeIndex(sfxVolumeIndex), 0, volumeOptions.Length - 1);
            fullscreen = PlayerPrefs.GetInt("spellguard.fullscreen", fullscreen ? 1 : 0) != 0;
            UnityEngine.Screen.fullScreen = fullscreen;
        }

        private static string ToVolumeLabel(float volume)
        {
            return $"{Mathf.RoundToInt(volume * 100f)}%";
        }
    }
}
