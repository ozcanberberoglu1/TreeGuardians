using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Save;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace TreeGuardians.Audio
{
    /// Null-safe audio playback through mixer groups Master/Music/SFX/UI.
    /// One-shots: pre-authored SFX/UI voice pools with per-event instance caps and priority stealing.
    /// Loops: 2 crossfading music voices + 2 ambience layers, driven every frame (unscaled time) by a gain stage:
    /// volume = fade x entry volume x settings x duck x pause.
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

        [Header("Voices (pre-created by BootSceneBuilder; auto-filled if empty)")]
        [SerializeField] AudioSource musicSourceA;
        [SerializeField] AudioSource musicSourceB;
        [SerializeField] AudioSource[] sfxSources = new AudioSource[0];
        [SerializeField] AudioSource[] uiSources = new AudioSource[0];
        [SerializeField] int sfxVoiceCount = 12;
        [SerializeField] int uiVoiceCount = 5;
        [SerializeField] float musicFadeSeconds = 0.8f;
        [Tooltip("Ambiyans katmanları: 0 = ortam (orman), 1 = hava (yağmur).")] [SerializeField] AudioSource[] ambienceSources = new AudioSource[0];

        [Header("Mix")]
        [Tooltip("Kısmanın (duck) inme süresi, saniye.")] [SerializeField] float duckAttackSeconds = 0.05f;
        [Tooltip("Kısma bitince eski seviyeye dönüş süresi, saniye.")] [SerializeField] float duckReleaseSeconds = 0.5f;
        [Tooltip("Savaş duraklatılınca müzik çarpanı.")] [SerializeField] [Range(0f, 1f)] float pausedMusicGain = 0.5f;
        [Tooltip("Savaş duraklatılınca ambiyans çarpanı.")] [SerializeField] [Range(0f, 1f)] float pausedAmbienceGain = 0.3f;
        [Tooltip("Duraklatma kısmasının geçiş süresi, saniye.")] [SerializeField] float pauseFadeSeconds = 0.2f;
        [Tooltip("Dünya konumlu seslerde en fazla stereo kaydırma (0 = hep ortada).")] [SerializeField] [Range(0f, 1f)] float worldPanAmount = 0.45f;

        const int AmbienceLayers = 2;
        const int MusicVoicePriority = 0;
        const int AmbienceVoicePriority = 32;
        const int UiVoicePriority = 64;
        const int SfxVoicePriority = 128;
        const float InstantFade = 1000000f;

        struct Voice
        {
            public AudioEventId id;
            public int priority;
            public float startTime;
            public bool pausable;
            public bool paused;
        }

        AudioLibrary library;
        SettingsSaveData settings;
        float musicLinear = 0.8f;
        float sfxLinear = 1f;

        Voice[] sfxVoices = new Voice[0];
        Voice[] uiVoices = new Voice[0];
        readonly System.Collections.Generic.Dictionary<AudioEventId, float> lastPlayed = new System.Collections.Generic.Dictionary<AudioEventId, float>(64);
        int clickSuppressFrame = -1;
        int lastClickFrame = -1;
        int lastClickVoice = -1;
        bool lastClickOnUiPool;

        // Music gain stage: index 0 = musicSourceA, 1 = musicSourceB.
        MusicTrackId currentTrack = MusicTrackId.None;
        int activeMusic;
        readonly float[] musicFade = new float[2];
        readonly float[] musicFadeTarget = new float[2];
        readonly float[] musicFadeSpeed = new float[2];
        readonly float[] musicEntryVolume = { 1f, 1f };
        float musicResumeTime;

        // Ambience gain stage per layer.
        readonly AmbienceTrackId[] ambienceIds = new AmbienceTrackId[AmbienceLayers];
        readonly float[] ambFade = new float[AmbienceLayers];
        readonly float[] ambFadeTarget = new float[AmbienceLayers];
        readonly float[] ambFadeSpeed = new float[AmbienceLayers];
        readonly float[] ambEntryVolume = new float[AmbienceLayers];
        readonly float[] ambResumeTime = new float[AmbienceLayers];

        // Duck (music + ambience) and gameplay pause.
        float duckGain = 1f;
        float heldDuck = 1f;
        float timedDuck = 1f;
        float timedDuckUntil;
        bool gameplayPaused;
        float pauseMusic = 1f;
        float pauseAmbience = 1f;
        float resumeCheckAt = -1f;

        public MusicTrackId CurrentTrack => currentTrack;
        public bool GameplayPaused => gameplayPaused;

        void Awake()
        {
            Services.Register(this);
            EnsureSources();
            AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        void OnDestroy()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            Services.Unregister(this);
        }

        // ------------------------------------------------------------------ voices
        void EnsureSources()
        {
            if (musicSourceA == null) musicSourceA = CreateSource("Music_A", musicGroup, true);
            if (musicSourceB == null) musicSourceB = CreateSource("Music_B", musicGroup, true);
            sfxSources = FillPool(sfxSources, sfxVoiceCount, "Sfx_", sfxGroup, false);
            uiSources = FillPool(uiSources, uiVoiceCount, "Ui_", uiGroup, false);
            ambienceSources = FillPool(ambienceSources, AmbienceLayers, "Ambience_", sfxGroup, true);

            // Pause behaviour and engine priority are set explicitly (the flags are not guaranteed by the prefab).
            Configure(musicSourceA, musicGroup, true, true, MusicVoicePriority);
            Configure(musicSourceB, musicGroup, true, true, MusicVoicePriority);
            for (int i = 0; i < sfxSources.Length; i++) Configure(sfxSources[i], sfxGroup, false, false, SfxVoicePriority);
            for (int i = 0; i < uiSources.Length; i++) Configure(uiSources[i], uiGroup, false, true, UiVoicePriority);
            for (int i = 0; i < ambienceSources.Length; i++) Configure(ambienceSources[i], sfxGroup, true, false, AmbienceVoicePriority);
            sfxVoices = new Voice[sfxSources.Length];
            uiVoices = new Voice[uiSources.Length];
        }

        AudioSource[] FillPool(AudioSource[] pool, int count, string prefix, AudioMixerGroup group, bool loop)
        {
            if (pool != null && pool.Length >= count) return pool;
            var arr = new AudioSource[count];
            for (int i = 0; i < count; i++)
                arr[i] = pool != null && i < pool.Length && pool[i] != null ? pool[i] : CreateSource(prefix + i, group, loop);
            return arr;
        }

        /// Fallback only: the voices are pre-authored on Boot_Root by BootSceneBuilder.
        AudioSource CreateSource(string sourceName, AudioMixerGroup group, bool loop)
        {
            var go = new GameObject(sourceName);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.outputAudioMixerGroup = group;
            return s;
        }

        static void Configure(AudioSource s, AudioMixerGroup group, bool loop, bool ignoreListenerPause, int priority)
        {
            if (s == null) return;
            s.playOnAwake = false;
            s.loop = loop;
            s.ignoreListenerPause = ignoreListenerPause;
            s.priority = priority;
            s.spatialBlend = 0f;
            if (group != null && s.outputAudioMixerGroup == null) s.outputAudioMixerGroup = group;
        }

        AudioSource MusicSource(int index) => index == 0 ? musicSourceA : musicSourceB;

        // ------------------------------------------------------------------ settings
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
            ApplyLoopVolumes();
        }

        void SetMixerVolume(string param, float linear)
        {
            if (mixer == null || string.IsNullOrEmpty(param)) return;
            float db = linear <= 0.0001f ? -80f : Mathf.Log10(linear) * 20f;
            mixer.SetFloat(param, db);
        }

        static float FadeSpeed(float from, float to, float seconds) =>
            seconds <= 0f ? InstantFade : Mathf.Max(0.0001f, Mathf.Abs(to - from)) / seconds;

        // ------------------------------------------------------------------ ambience
        /// Looping ambience per layer (0 = environment, 1 = weather). intensity 0..1 scales the entry volume; None/0 fades out.
        /// Re-calling with the same id only retargets the level, so frequent calls (weather ramps) never restart the loop.
        public void SetAmbience(int layer, AmbienceTrackId id, float intensity = 1f, float fadeSeconds = 1.5f)
        {
            if (library == null || ambienceSources == null || layer < 0 || layer >= AmbienceLayers || layer >= ambienceSources.Length) return;
            var src = ambienceSources[layer];
            if (src == null) return;
            var entry = id != AmbienceTrackId.None ? library.GetAmbience(id) : null;
            if (entry == null || entry.clip == null) { id = AmbienceTrackId.None; intensity = 0f; }
            if (id != AmbienceTrackId.None)
            {
                if (src.clip != entry.clip || !src.isPlaying)
                {
                    bool resume = src.clip == entry.clip && ambFade[layer] > 0.0001f;
                    src.clip = entry.clip;
                    src.loop = true;
                    if (!resume) ambFade[layer] = 0f;
                    src.Play();
                    src.time = resume ? Mathf.Min(ambResumeTime[layer], entry.clip.length - 0.05f) : Random.Range(0f, Mathf.Max(0f, entry.clip.length - 0.1f));
                }
                ambEntryVolume[layer] = entry.volume;
            }
            ambienceIds[layer] = id;
            ambFadeTarget[layer] = Mathf.Clamp01(intensity);
            ambFadeSpeed[layer] = FadeSpeed(ambFade[layer], ambFadeTarget[layer], fadeSeconds);
            ApplyLoopVolumes();
        }

        public AmbienceTrackId GetAmbience(int layer) => layer >= 0 && layer < AmbienceLayers ? ambienceIds[layer] : AmbienceTrackId.None;

        // ------------------------------------------------------------------ one-shots
        public void PlaySfx(AudioEventId id, float volumeScale = 1f) => PlaySfx(id, volumeScale, 1f, 0f);

        /// World-positioned one-shot: pans by the x offset from the main camera (player castle left, enemy right).
        public void PlaySfxAt(AudioEventId id, Vector2 world, float volumeScale = 1f, float pitchScale = 1f)
        {
            PlaySfx(id, volumeScale, pitchScale, WorldPan(world));
        }

        float WorldPan(Vector2 world)
        {
            var cam = Camera.main;
            if (cam == null) return 0f;
            float halfW = cam.orthographic ? cam.orthographicSize * cam.aspect : 10f;
            return Mathf.Clamp((world.x - cam.transform.position.x) / Mathf.Max(1f, halfW), -1f, 1f) * worldPanAmount;
        }

        public void PlayUi(AudioEventId id) => PlaySfx(id, 1f, 1f, 0f);

        public void PlayUi(AudioEventId id, float volumeScale, float pitchScale = 1f) => PlaySfx(id, volumeScale, pitchScale, 0f);

        /// pitchScale multiplies the entry's random pitch variance; pan -1..1 (UI entries always play centered).
        public void PlaySfx(AudioEventId id, float volumeScale, float pitchScale, float pan = 0f)
        {
            if (id == AudioEventId.None || library == null) return;
            var e = library.GetSfx(id);
            if (e == null || e.clips == null || e.clips.Length == 0) return;
            int frame = Time.frameCount;
            bool isClick = id == AudioEventId.UiClick;
            if (e.isUi && !isClick) SuppressClick(frame);
            else if (isClick && frame == clickSuppressFrame) return; // a specific UI sound already answered this tap
            if (gameplayPaused && e.pausable && !e.isUi) return;
            float now = Time.unscaledTime;
            if (e.minInterval > 0f && lastPlayed.TryGetValue(id, out var last) && now - last < e.minInterval) return;
            var clip = e.clips[e.clips.Length == 1 ? 0 : Random.Range(0, e.clips.Length)];
            if (clip == null) return;

            var pool = e.isUi ? uiSources : sfxSources;
            var voices = e.isUi ? uiVoices : sfxVoices;
            int v = AllocateVoice(pool, voices, id, e);
            if (v < 0) return;
            var src = pool[v];
            lastPlayed[id] = now;
            float variance = e.pitchVariance > 0f ? Random.Range(-e.pitchVariance, e.pitchVariance) : 0f;
            src.clip = clip;
            src.loop = false;
            src.pitch = Mathf.Clamp(pitchScale * (1f + variance), 0.1f, 3f);
            src.panStereo = e.isUi ? 0f : Mathf.Clamp(pan, -1f, 1f);
            src.volume = (mixer != null ? 1f : sfxLinear) * e.volume * Mathf.Max(0f, volumeScale);
            src.priority = Mathf.Clamp(e.priority, 0, 256);
            src.Play();
            voices[v] = new Voice { id = id, priority = e.priority, startTime = now, pausable = e.pausable && !e.isUi, paused = false };
            if (isClick) { lastClickFrame = frame; lastClickVoice = v; lastClickOnUiPool = e.isUi; }
            if (e.duckTo < 0.999f) Duck(e.duckTo, Mathf.Max(0.05f, e.duckHold));
        }

        /// A non-click UI sound in the same frame replaces the generic click (Button.onClick sounds run before
        /// UIButtonFeedback's click, or after it; both orders end with only the specific sound).
        void SuppressClick(int frame)
        {
            clickSuppressFrame = frame;
            if (lastClickFrame != frame || lastClickVoice < 0) return;
            var pool = lastClickOnUiPool ? uiSources : sfxSources;
            var voices = lastClickOnUiPool ? uiVoices : sfxVoices;
            if (lastClickVoice < pool.Length && lastClickVoice < voices.Length && pool[lastClickVoice] != null && voices[lastClickVoice].id == AudioEventId.UiClick)
            {
                pool[lastClickVoice].Stop();
                voices[lastClickVoice] = default;
            }
            lastClickVoice = -1;
        }

        /// Free voice first; at the instance cap the oldest copy of the same event is reused; otherwise the least
        /// important voice (highest priority number, oldest first) is stolen, never one more important than the new sound.
        int AllocateVoice(AudioSource[] pool, Voice[] voices, AudioEventId id, SfxEntry e)
        {
            if (pool == null || voices == null) return -1;
            int n = Mathf.Min(pool.Length, voices.Length);
            int count = 0, oldestSame = -1, free = -1;
            float oldestSameTime = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var s = pool[i];
                if (s == null) continue;
                if (!s.isPlaying && !voices[i].paused) { if (free < 0) free = i; continue; }
                if (voices[i].id != id) continue;
                count++;
                if (voices[i].startTime < oldestSameTime) { oldestSameTime = voices[i].startTime; oldestSame = i; }
            }
            if (count >= Mathf.Max(1, e.maxInstances) && oldestSame >= 0) return oldestSame;
            if (free >= 0) return free;
            int victim = -1, worstPriority = -1;
            float victimTime = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (pool[i] == null) continue;
                int p = voices[i].priority;
                if (p < e.priority) continue;
                if (p > worstPriority || (p == worstPriority && voices[i].startTime < victimTime))
                {
                    victim = i; worstPriority = p; victimTime = voices[i].startTime;
                }
            }
            return victim;
        }

        // ------------------------------------------------------------------ pause + duck
        /// Battle pause: pausable gameplay voices pause (UI keeps playing), new gameplay one-shots are skipped,
        /// ambience dips to x0.3 and music to x0.5 over a short unscaled fade.
        public void SetGameplayPaused(bool paused)
        {
            if (gameplayPaused == paused) return;
            gameplayPaused = paused;
            for (int i = 0; i < sfxSources.Length && i < sfxVoices.Length; i++)
            {
                var s = sfxSources[i];
                if (s == null) continue;
                if (paused)
                {
                    if (s.isPlaying && sfxVoices[i].pausable) { s.Pause(); sfxVoices[i].paused = true; }
                }
                else if (sfxVoices[i].paused)
                {
                    s.UnPause();
                    sfxVoices[i].paused = false;
                }
            }
        }

        /// Lowers music + ambience to 'to' (0..1). hold > 0: for 'hold' seconds (overlapping ducks keep the deepest level and
        /// the latest end). hold <= 0: held until the next Duck(x, 0) call; Duck(1, 0) releases it.
        public void Duck(float to, float hold)
        {
            to = Mathf.Clamp01(to);
            if (hold <= 0f) { heldDuck = to; return; }
            float now = Time.unscaledTime;
            bool active = timedDuckUntil > now;
            timedDuck = active ? Mathf.Min(timedDuck, to) : to;
            timedDuckUntil = Mathf.Max(active ? timedDuckUntil : 0f, now + hold);
        }

        // ------------------------------------------------------------------ music
        public void PlayMusic(MusicTrackId id, bool fade = true)
        {
            if (library == null) return;
            var active = MusicSource(activeMusic);
            if (id == currentTrack && active != null && active.isPlaying && musicFadeTarget[activeMusic] > 0f) return;
            var entry = id != MusicTrackId.None ? library.GetMusic(id) : null;
            if (entry == null || entry.clip == null)
            {
                StopMusic(fade);
                return;
            }
            currentTrack = id;
            float seconds = fade ? musicFadeSeconds : 0f;
            if (active != null && active.clip == entry.clip && active.isPlaying)
            {
                // Same loop under another id (the arena battle ids can share one clip): keep playing, only retarget the level.
                musicEntryVolume[activeMusic] = entry.volume;
                musicFadeTarget[activeMusic] = 1f;
                musicFadeSpeed[activeMusic] = FadeSpeed(musicFade[activeMusic], 1f, seconds);
                return;
            }
            int next = 1 - activeMusic;
            var src = MusicSource(next);
            if (src == null) return;
            src.clip = entry.clip;
            src.loop = true;
            musicFade[next] = 0f;
            musicEntryVolume[next] = entry.volume;
            musicFadeTarget[next] = 1f;
            musicFadeSpeed[next] = FadeSpeed(0f, 1f, seconds);
            src.volume = 0f;
            src.Play();
            src.time = 0f;
            musicFadeTarget[activeMusic] = 0f;
            musicFadeSpeed[activeMusic] = FadeSpeed(musicFade[activeMusic], 0f, seconds);
            activeMusic = next;
            musicResumeTime = 0f;
            if (!fade) { musicFade[next] = 1f; musicFade[1 - next] = 0f; }
            ApplyLoopVolumes();
        }

        public void StopMusic(bool fade = true)
        {
            currentTrack = MusicTrackId.None;
            float seconds = fade ? musicFadeSeconds : 0f;
            for (int i = 0; i < 2; i++)
            {
                musicFadeTarget[i] = 0f;
                musicFadeSpeed[i] = FadeSpeed(musicFade[i], 0f, seconds);
                if (!fade) musicFade[i] = 0f;
            }
            ApplyLoopVolumes();
        }

        // ------------------------------------------------------------------ per-frame gain stage
        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;
            if (timedDuckUntil > 0f && now >= timedDuckUntil) { timedDuckUntil = 0f; timedDuck = 1f; }
            float duckTarget = Mathf.Min(heldDuck, timedDuck);
            if (duckGain != duckTarget)
            {
                float seconds = duckTarget < duckGain ? duckAttackSeconds : duckReleaseSeconds;
                duckGain = Mathf.MoveTowards(duckGain, duckTarget, dt / Mathf.Max(0.001f, seconds));
            }
            float pauseStep = dt / Mathf.Max(0.001f, pauseFadeSeconds);
            pauseMusic = Mathf.MoveTowards(pauseMusic, gameplayPaused ? pausedMusicGain : 1f, pauseStep);
            pauseAmbience = Mathf.MoveTowards(pauseAmbience, gameplayPaused ? pausedAmbienceGain : 1f, pauseStep);
            for (int i = 0; i < 2; i++) musicFade[i] = Mathf.MoveTowards(musicFade[i], musicFadeTarget[i], musicFadeSpeed[i] * dt);
            for (int i = 0; i < AmbienceLayers; i++) ambFade[i] = Mathf.MoveTowards(ambFade[i], ambFadeTarget[i], ambFadeSpeed[i] * dt);
            ApplyLoopVolumes();
            TrackLoopPositions();
            if (resumeCheckAt > 0f && now >= resumeCheckAt)
            {
                resumeCheckAt = -1f;
                ResumeLoops();
            }
        }

        void ApplyLoopVolumes()
        {
            float musicSetting = mixer != null ? 1f : musicLinear;
            float ambienceSetting = mixer != null ? 1f : sfxLinear;
            for (int i = 0; i < 2; i++)
            {
                var src = MusicSource(i);
                if (src == null) continue;
                src.volume = musicFade[i] * musicEntryVolume[i] * musicSetting * duckGain * pauseMusic;
                if (musicFade[i] <= 0f && musicFadeTarget[i] <= 0f && src.isPlaying) src.Stop();
            }
            if (ambienceSources == null) return;
            for (int i = 0; i < AmbienceLayers && i < ambienceSources.Length; i++)
            {
                var src = ambienceSources[i];
                if (src == null) continue;
                src.volume = ambFade[i] * ambEntryVolume[i] * ambienceSetting * duckGain * pauseAmbience;
                if (ambFade[i] <= 0f && ambFadeTarget[i] <= 0f && src.isPlaying) { src.Stop(); ambienceIds[i] = AmbienceTrackId.None; }
            }
        }

        void TrackLoopPositions()
        {
            var active = MusicSource(activeMusic);
            if (active != null && active.isPlaying && active.clip != null) musicResumeTime = active.time;
            if (ambienceSources == null) return;
            for (int i = 0; i < AmbienceLayers && i < ambienceSources.Length; i++)
            {
                var src = ambienceSources[i];
                if (src != null && src.isPlaying && src.clip != null) ambResumeTime[i] = src.time;
            }
        }

        // ------------------------------------------------------------------ audio route changes / app resume
        void OnAudioConfigurationChanged(bool deviceWasChanged) => resumeCheckAt = Time.unscaledTime + 0.1f;

        void OnApplicationPause(bool paused)
        {
            if (!paused) resumeCheckAt = Time.unscaledTime + 0.25f;
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused) resumeCheckAt = Time.unscaledTime + 0.25f;
        }

        /// Headphone/Bluetooth route changes reset the audio system and stop every source; restart the loops where they were.
        void ResumeLoops()
        {
            var active = MusicSource(activeMusic);
            if (currentTrack != MusicTrackId.None && active != null && active.clip != null && !active.isPlaying && musicFadeTarget[activeMusic] > 0f)
            {
                active.Play();
                active.time = Mathf.Min(Mathf.Repeat(musicResumeTime, active.clip.length), Mathf.Max(0f, active.clip.length - 0.05f));
            }
            if (ambienceSources == null) return;
            for (int i = 0; i < AmbienceLayers && i < ambienceSources.Length; i++)
            {
                var src = ambienceSources[i];
                if (ambienceIds[i] == AmbienceTrackId.None || src == null || src.clip == null || src.isPlaying || ambFadeTarget[i] <= 0f) continue;
                src.Play();
                src.time = Mathf.Min(Mathf.Repeat(ambResumeTime[i], src.clip.length), Mathf.Max(0f, src.clip.length - 0.05f));
            }
            ApplyLoopVolumes();
        }

        /// Safety net: a scene change never leaves gameplay sounds paused (e.g. leaving the battle from the pause menu).
        void OnActiveSceneChanged(Scene from, Scene to)
        {
            if (gameplayPaused) SetGameplayPaused(false);
        }
    }
}
