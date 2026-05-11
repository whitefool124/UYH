using UnityEngine;

namespace SpellGuard.Core
{
    public static class SpellGuardLocalProgress
    {
        private const string ConfirmIndexKey = "SpellGuard.Settings.ConfirmIndex";
        private const string DifficultyIndexKey = "SpellGuard.Settings.DifficultyIndex";
        private const string MusicVolumeIndexKey = "SpellGuard.Settings.MusicVolumeIndex";
        private const string SfxVolumeIndexKey = "SpellGuard.Settings.SfxVolumeIndex";
        private const string InputModeIndexKey = "SpellGuard.Settings.InputModeIndex";
        private const string BestScoreKey = "SpellGuard.Progress.BestScore";
        private const string TutorialSeenKey = "SpellGuard.Progress.TutorialSeen";

        public static int LoadConfirmIndex(int fallback)
        {
            return PlayerPrefs.GetInt(ConfirmIndexKey, fallback);
        }

        public static void SaveConfirmIndex(int value)
        {
            PlayerPrefs.SetInt(ConfirmIndexKey, value);
            PlayerPrefs.Save();
        }

        public static int LoadDifficultyIndex(int fallback)
        {
            return PlayerPrefs.GetInt(DifficultyIndexKey, fallback);
        }

        public static void SaveDifficultyIndex(int value)
        {
            PlayerPrefs.SetInt(DifficultyIndexKey, value);
            PlayerPrefs.Save();
        }

        public static int LoadMusicVolumeIndex(int fallback)
        {
            return PlayerPrefs.GetInt(MusicVolumeIndexKey, fallback);
        }

        public static void SaveMusicVolumeIndex(int value)
        {
            PlayerPrefs.SetInt(MusicVolumeIndexKey, value);
            PlayerPrefs.Save();
        }

        public static int LoadSfxVolumeIndex(int fallback)
        {
            return PlayerPrefs.GetInt(SfxVolumeIndexKey, fallback);
        }

        public static void SaveSfxVolumeIndex(int value)
        {
            PlayerPrefs.SetInt(SfxVolumeIndexKey, value);
            PlayerPrefs.Save();
        }

        public static int LoadInputModeIndex(int fallback)
        {
            return PlayerPrefs.GetInt(InputModeIndexKey, fallback);
        }

        public static void SaveInputModeIndex(int value)
        {
            PlayerPrefs.SetInt(InputModeIndexKey, value);
            PlayerPrefs.Save();
        }

        public static int LoadBestScore()
        {
            return Mathf.Max(0, PlayerPrefs.GetInt(BestScoreKey, 0));
        }

        public static void SaveBestScore(int value)
        {
            PlayerPrefs.SetInt(BestScoreKey, Mathf.Max(0, value));
            PlayerPrefs.Save();
        }

        public static bool LoadTutorialSeen()
        {
            return PlayerPrefs.GetInt(TutorialSeenKey, 0) == 1;
        }

        public static void SaveTutorialSeen(bool value)
        {
            PlayerPrefs.SetInt(TutorialSeenKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
