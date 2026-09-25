using System;
using System.Collections.Generic;
using UnityEngine;

namespace TreeGuardians.Data
{
    [Serializable]
    public sealed class SfxEntry
    {
        public AudioEventId id;
        [Tooltip("Birden fazla klip varsa rastgele seçilir. Boş bırakılabilir (sessiz, hatasız).")]
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0f, 0.5f)] public float pitchVariance = 0.05f;
        [Tooltip("UI grubundan mı çalınsın (aksi halde SFX).")] public bool isUi;
        [Tooltip("Aynı sesin tekrar çalınabilmesi için en az süre (saniye). Spam'i önler.")] [Range(0f, 1f)] public float minInterval = 0.03f;
        [Tooltip("Bu olaydan aynı anda en fazla kaç ses çalabilir. Dolunca en eski kopya yeniden kullanılır.")] [Range(1, 8)] public int maxInstances = 3;
        [Tooltip("Ses önceliği (0 = en önemli, 255 = en önemsiz). Havuz doluysa yalnızca daha önemsiz bir ses kesilir.")] [Range(0, 255)] public int priority = 128;
        [Tooltip("Çalınca müzik ve ambiyansı bu seviyeye indirir (1 = kısma yok).")] [Range(0f, 1f)] public float duckTo = 1f;
        [Tooltip("Kısmanın ne kadar süre tutulacağı (saniye).")] [Min(0f)] public float duckHold;
        [Tooltip("Savaş duraklatılınca bu ses de duraklatılsın mı (UI sesleri için kapalı).")] public bool pausable = true;
    }

    [Serializable]
    public sealed class AmbienceEntry
    {
        public AmbienceTrackId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.5f;
    }

    [Serializable]
    public sealed class MusicEntry
    {
        public MusicTrackId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.8f;
    }

    [CreateAssetMenu(menuName = "Tree Guardians/Audio/Audio Library", fileName = "AudioLibrary")]
    public sealed class AudioLibrary : ScriptableObject
    {
        public List<SfxEntry> sfx = new List<SfxEntry>();
        public List<MusicEntry> music = new List<MusicEntry>();
        public List<AmbienceEntry> ambience = new List<AmbienceEntry>();

        Dictionary<AudioEventId, SfxEntry> sfxMap;
        Dictionary<MusicTrackId, MusicEntry> musicMap;
        Dictionary<AmbienceTrackId, AmbienceEntry> ambienceMap;

        public SfxEntry GetSfx(AudioEventId id)
        {
            if (sfxMap == null) BuildMaps();
            return sfxMap.TryGetValue(id, out var e) ? e : null;
        }

        public MusicEntry GetMusic(MusicTrackId id)
        {
            if (musicMap == null) BuildMaps();
            return musicMap.TryGetValue(id, out var e) ? e : null;
        }

        public AmbienceEntry GetAmbience(AmbienceTrackId id)
        {
            if (ambienceMap == null) BuildMaps();
            return ambienceMap.TryGetValue(id, out var e) ? e : null;
        }

        void BuildMaps()
        {
            sfxMap = new Dictionary<AudioEventId, SfxEntry>(sfx.Count);
            for (int i = 0; i < sfx.Count; i++) if (sfx[i] != null) sfxMap[sfx[i].id] = sfx[i];
            musicMap = new Dictionary<MusicTrackId, MusicEntry>(music.Count);
            for (int i = 0; i < music.Count; i++) if (music[i] != null) musicMap[music[i].id] = music[i];
            ambienceMap = new Dictionary<AmbienceTrackId, AmbienceEntry>(ambience != null ? ambience.Count : 0);
            if (ambience != null) for (int i = 0; i < ambience.Count; i++) if (ambience[i] != null) ambienceMap[ambience[i].id] = ambience[i];
        }

        void OnValidate()
        {
            sfxMap = null;
            musicMap = null;
            ambienceMap = null;
        }
    }
}
