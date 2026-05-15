using SpellGuard.Core;
using SpellGuard.Combat;
using UnityEngine;

namespace SpellGuard.Audio
{
    public class SpellGuardAudioController : MonoBehaviour
    {
        private const int SampleRate = 44100;

        private static SpellGuardAudioController instance;

        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;
        private AudioClip menuMusicClip;
        private AudioClip combatMusicClip;
        private AudioClip uiClickClip;
        private AudioClip fireCastClip;
        private AudioClip iceCastClip;
        private AudioClip shieldCastClip;
        private AudioClip enemyHitClip;
        private AudioClip freezeClip;
        private AudioClip playerHitClip;
        private AudioClip victoryClip;
        private AudioClip defeatClip;
        private AudioClip trainingPingClip;

        private SpellGuardMusicTrack currentMusicTrack = SpellGuardMusicTrack.None;
        private float musicVolume = 0.75f;
        private float sfxVolume = 0.85f;

        public static SpellGuardAudioController Instance => instance;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
            EnsureSources();
            BuildGeneratedClips();
        }

        public void ApplySettings(SpellGuardGameSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            musicVolume = settings.MusicVolume;
            sfxVolume = settings.SfxVolume;

            if (musicSource != null)
            {
                musicSource.volume = musicVolume;
            }

            if (sfxSource != null)
            {
                sfxSource.volume = sfxVolume;
            }
        }

        public void PlayMenuMusic()
        {
            PlayMusic(menuMusicClip, SpellGuardMusicTrack.Menu);
        }

        public void PlayCombatMusic()
        {
            PlayMusic(combatMusicClip, SpellGuardMusicTrack.Combat);
        }

        public void PauseMusic()
        {
            if (musicSource != null && musicSource.isPlaying)
            {
                musicSource.Pause();
            }
        }

        public void ResumeMusic()
        {
            if (musicSource != null)
            {
                musicSource.UnPause();
            }
        }

        public void PlayUiClickSfx()
        {
            PlaySfx(uiClickClip, 0.95f);
        }

        public void PlayTrainingPingSfx()
        {
            PlaySfx(trainingPingClip, 0.9f);
        }

        public void PlaySpellCastSfx(SpellType spell)
        {
            switch (spell)
            {
                case SpellType.Fire:
                    PlaySfx(fireCastClip, 1f);
                    break;
                case SpellType.Ice:
                    PlaySfx(iceCastClip, 1f);
                    break;
                case SpellType.Shield:
                    PlaySfx(shieldCastClip, 1f);
                    break;
            }
        }

        public void PlayEnemyHitSfx()
        {
            PlaySfx(enemyHitClip, 0.9f);
        }

        public void PlayFreezeSfx()
        {
            PlaySfx(freezeClip, 0.85f);
        }

        public void PlayPlayerHitSfx()
        {
            PlaySfx(playerHitClip, 0.95f);
        }

        public void PlayVictorySfx()
        {
            PlaySfx(victoryClip, 1f);
        }

        public void PlayDefeatSfx()
        {
            PlaySfx(defeatClip, 1f);
        }

        private void EnsureSources()
        {
            if (musicSource == null)
            {
                musicSource = GetComponent<AudioSource>();
                if (musicSource == null)
                {
                    musicSource = gameObject.AddComponent<AudioSource>();
                }
            }

            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
            }

            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;
        }

        private void PlayMusic(AudioClip clip, SpellGuardMusicTrack track)
        {
            if (musicSource == null || clip == null)
            {
                return;
            }

            musicSource.volume = musicVolume;
            if (currentMusicTrack == track && musicSource.clip == clip && musicSource.isPlaying)
            {
                return;
            }

            currentMusicTrack = track;
            musicSource.clip = clip;
            musicSource.loop = true;
            musicSource.Play();
        }

        private void PlaySfx(AudioClip clip, float volumeScale)
        {
            if (sfxSource == null || clip == null)
            {
                return;
            }

            sfxSource.volume = sfxVolume;
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        private void BuildGeneratedClips()
        {
            menuMusicClip = CreateLoopClip("MenuLoop", new[] { 220f, 277.18f, 329.63f }, 2.4f, 0.11f);
            combatMusicClip = CreateLoopClip("CombatLoop", new[] { 110f, 164.81f, 146.83f, 196f }, 2f, 0.13f);
            uiClickClip = CreateSweepClip("UiClick", 880f, 1320f, 0.08f, 0.18f);
            fireCastClip = CreateSweepClip("FireCast", 260f, 720f, 0.22f, 0.3f);
            iceCastClip = CreateSweepClip("IceCast", 720f, 320f, 0.24f, 0.28f);
            shieldCastClip = CreatePulseClip("ShieldCast", 410f, 0.22f, 0.25f);
            enemyHitClip = CreatePulseClip("EnemyHit", 180f, 0.12f, 0.35f);
            freezeClip = CreateSweepClip("Freeze", 560f, 240f, 0.18f, 0.22f);
            playerHitClip = CreatePulseClip("PlayerHit", 140f, 0.2f, 0.32f);
            victoryClip = CreateVictoryClip();
            defeatClip = CreateSweepClip("Defeat", 240f, 90f, 0.45f, 0.22f);
            trainingPingClip = CreatePulseClip("TrainingPing", 620f, 0.12f, 0.2f);
        }

        private static AudioClip CreateLoopClip(string clipName, float[] frequencies, float lengthSeconds, float amplitude)
        {
            var sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * lengthSeconds));
            var samples = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)SampleRate;
                var envelope = 0.65f + Mathf.Sin(t * Mathf.PI * 2f / Mathf.Max(0.2f, lengthSeconds)) * 0.08f;
                var value = 0f;
                for (var f = 0; f < frequencies.Length; f++)
                {
                    value += Mathf.Sin(t * frequencies[f] * Mathf.PI * 2f + f * 0.5f);
                }

                samples[i] = value / Mathf.Max(1, frequencies.Length) * amplitude * envelope;
            }

            return CreateClip(clipName, samples);
        }

        private static AudioClip CreateSweepClip(string clipName, float startFrequency, float endFrequency, float lengthSeconds, float amplitude)
        {
            var sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * lengthSeconds));
            var samples = new float[sampleCount];
            var phase = 0f;
            for (var i = 0; i < sampleCount; i++)
            {
                var progress = i / (float)Mathf.Max(1, sampleCount - 1);
                var frequency = Mathf.Lerp(startFrequency, endFrequency, progress);
                phase += frequency * Mathf.PI * 2f / SampleRate;
                var envelope = Mathf.Sin(progress * Mathf.PI);
                samples[i] = Mathf.Sin(phase) * amplitude * envelope;
            }

            return CreateClip(clipName, samples);
        }

        private static AudioClip CreatePulseClip(string clipName, float frequency, float lengthSeconds, float amplitude)
        {
            var sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * lengthSeconds));
            var samples = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var progress = i / (float)Mathf.Max(1, sampleCount - 1);
                var envelope = Mathf.Exp(-4f * progress);
                var sample = Mathf.Sin(i * frequency * Mathf.PI * 2f / SampleRate);
                samples[i] = sample * amplitude * envelope;
            }

            return CreateClip(clipName, samples);
        }

        private static AudioClip CreateVictoryClip()
        {
            var sampleCount = Mathf.Max(1, Mathf.RoundToInt(SampleRate * 0.5f));
            var samples = new float[sampleCount];
            var notes = new[] { 392f, 523.25f, 659.25f };
            var noteLength = sampleCount / notes.Length;
            for (var i = 0; i < sampleCount; i++)
            {
                var noteIndex = Mathf.Min(notes.Length - 1, i / Mathf.Max(1, noteLength));
                var localProgress = (i % Mathf.Max(1, noteLength)) / (float)Mathf.Max(1, noteLength - 1);
                var envelope = Mathf.Sin(localProgress * Mathf.PI);
                samples[i] = Mathf.Sin(i * notes[noteIndex] * Mathf.PI * 2f / SampleRate) * 0.22f * envelope;
            }

            return CreateClip("Victory", samples);
        }

        private static AudioClip CreateClip(string clipName, float[] samples)
        {
            var clip = AudioClip.Create(clipName, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private enum SpellGuardMusicTrack
        {
            None = 0,
            Menu = 1,
            Combat = 2,
        }
    }
}
