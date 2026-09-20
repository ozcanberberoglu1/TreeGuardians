using System.Collections;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Save;
using UnityEngine;
using UnityEngine.Audio;

namespace TreeGuardians.Audio
{
    /// Null-safe audio playback through mixer groups Master/Music/SFX/UI.
    public sealed class AudioService : MonoBehaviour
    {
        [Header("Mixer (optional)")]
        [SerializeField] AudioMixer mixer;
        [SerializeField] AudioMixerGroup musicGroup;
        [SerializeField] AudioMixerGroup sfxGroup;
        [SerializeField] AudioMixerGroup uiGroup;
        [SerializeField] string musicParam = "MusicVol";
        [SerializeField] string sfxParam = "SfxVol";
        [SerializeField] string uiParam = "UiVol";

        [Header("Voices (pre-created by SceneBuilder; auto-filled if empty)")]
        [SerializeField] AudioSource musicSourceA;
        [SerializeField] AudioSource musicSourceB;
        [SerializeField] AudioSource[] sfxSources = new AudioSource[0];
        [SerializeField] AudioSource[] uiSources = new AudioSource[0];
        [SerializeField] int sfxVoiceCount = 8;
        [SerializeField] int uiVoiceCount = 3;
        [SerializeField] float musicFadeSeconds = 0.8f;

        AudioLibrary library;
        SettingsSaveData settings;
        int sfxIndex;
        int uiIndex;
        bool usingA = true;
        MusicTrackId currentTrack = MusicTrackId.None;
        Coroutine fadeRoutine;
        float musicLinear = 0.8f;
        float sfxLinear = 1f;
        float currentMusicEntryVolume = 1f;

        public MusicTrackId CurrentTrack => currentTrack;

        void Awake()
        {
            Services.Register(this);
            EnsureSources();
        }

        void OnDestroy()
        {
            Services.Unregister(this);
        }

        void EnsureSources()
        {
            if (musicSourceA == null) musicSourceA = CreateSource("Music_A", musicGroup, true);
            if (musicSourceB == null) musicSourceB = CreateSource("Music_B", musicGroup, true);
            if (sfxSources == null || sfxSources.Length < sfxVoiceCount)
            {
                var arr = new AudioSource[sfxVoiceCount];
                for (int i = 0; i < sfxVoiceCount; i++)
                    arr[i] = sfxSources != null && i < sfxSources.Length && sfxSources[i] != null ? sfxSources[i] : CreateSource("Sfx_" + i, sfxGroup, false);
                sfxSources = arr;
            }
            if (uiSources == null || uiSources.Length < uiVoiceCount)
            {
                var arr = new AudioSource[uiVoiceCount];
                for (int i = 0; i < uiVoiceCount; i++)
                    arr[i] = uiSources != null && i < uiSources.Length && uiSources[i] != null ? uiSources[i] : CreateSource("Ui_" + i, uiGroup, false);
                uiSources = arr;
            }
        }

        AudioSource CreateSource(string sourceName, AudioMixerGroup group, bool loop)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.outputAudioMixerGroup = group;
            s.ignoreListenerPause = true;
            return s;
        }

        public void Initialize(AudioLibrary audioLibrary, SettingsSaveData saveSettings)
        {
            library = audioLibrary;
            settings = saveSettings;
            ApplySettings();
        }

        public void ApplySettings()
        {
            if (settings == null) return;
            musicLinear = Mathf.Clamp01(settings.musicVolume);
            sfxLinear = Mathf.Clamp01(settings.sfxVolume);
            SetMixerVolume(musicParam, musicLinear);
            SetMixerVolume(sfxParam, sfxLinear);
            SetMixerVolume(uiParam, sfxLinear);
            if (mixer == null)
            {
                var active = usingA ? musicSourceA : musicSourceB;
                if (active != null && fadeRoutine == null) active.volume = musicLinear * currentMusicEntryVolume;
            }
        }

        void SetMixerVolume(string param, float linear)
        {
            if (mixer == null || string.IsNullOrEmpty(param)) return;
            float db = linear <= 0.0001f ? -80f : Mathf.Log10(linear) * 20f;
            mixer.SetFloat(param, db);
        }

        public void PlaySfx(AudioEventId id, float volumeScale = 1f)
        {
            if (id == AudioEventId.None || library == null) return;
            var e = library.GetSfx(id);
            if (e == null || e.clips == null || e.clips.Length == 0) return;
            var clip = e.clips[e.clips.Length == 1 ? 0 : Random.Range(0, e.clips.Length)];
            if (clip == null) return;
            AudioSource src;
            if (e.isUi)
            {
                if (uiSources.Length == 0) return;
                src = uiSources[uiIndex++ % uiSources.Length];
            }
            else
            {
                if (sfxSources.Length == 0) return;
                src = sfxSources[sfxIndex++ % sfxSources.Length];
            }
            if (src == null) return;
            src.pitch = 1f + (e.pitchVariance > 0f ? Random.Range(-e.pitchVariance, e.pitchVariance) : 0f);
            src.volume = (mixer != null ? 1f : sfxLinear) * e.volume * volumeScale;
            src.clip = clip;
            src.Play();
        }

        public void PlayUi(AudioEventId id) => PlaySfx(id);

        public void PlayMusic(MusicTrackId id, bool fade = true)
        {
            if (library == null) return;
            var active = usingA ? musicSourceA : musicSourceB;
            if (id == currentTrack && active != null && active.isPlaying) return;
            currentTrack = id;
            var entry = library.GetMusic(id);
            if (entry == null || entry.clip == null)
            {
                StopMusic(fade);
                return;
            }
            var next = usingA ? musicSourceB : musicSourceA;
            usingA = !usingA;
            currentMusicEntryVolume = entry.volume;
            next.clip = entry.clip;
            next.loop = true;
            next.volume = 0f;
            next.Play();
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(Crossfade(active, next, TargetMusicVolume(), fade ? musicFadeSeconds : 0f));
        }

        float TargetMusicVolume() => (mixer != null ? 1f : musicLinear) * currentMusicEntryVolume;

        public void StopMusic(bool fade = true)
        {
            var active = usingA ? musicSourceA : musicSourceB;
            currentTrack = MusicTrackId.None;
            if (active == null) return;
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(Crossfade(active, null, 0f, fade ? musicFadeSeconds : 0f));
        }

        IEnumerator Crossfade(AudioSource from, AudioSource to, float targetVolume, float duration)
        {
            float fromStart = from != null ? from.volume : 0f;
            float elapsed = 0f;
            if (duration <= 0f)
            {
                if (from != null) { from.volume = 0f; from.Stop(); }
                if (to != null) to.volume = targetVolume;
                fadeRoutine = null;
                yield break;
            }
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (from != null) from.volume = Mathf.Lerp(fromStart, 0f, t);
                if (to != null) to.volume = Mathf.Lerp(0f, targetVolume, t);
                yield return null;
            }
            if (from != null) { from.volume = 0f; from.Stop(); }
            if (to != null) to.volume = targetVolume;
            fadeRoutine = null;
        }
    }
}
