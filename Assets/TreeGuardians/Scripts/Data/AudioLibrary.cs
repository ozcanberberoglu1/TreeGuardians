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

        Dictionary<AudioEventId, SfxEntry> sfxMap;
        Dictionary<MusicTrackId, MusicEntry> musicMap;

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

        void BuildMaps()
        {
            sfxMap = new Dictionary<AudioEventId, SfxEntry>(sfx.Count);
            for (int i = 0; i < sfx.Count; i++) if (sfx[i] != null) sfxMap[sfx[i].id] = sfx[i];
            musicMap = new Dictionary<MusicTrackId, MusicEntry>(music.Count);
            for (int i = 0; i < music.Count; i++) if (music[i] != null) musicMap[music[i].id] = music[i];
        }

        void OnValidate()
        {
            sfxMap = null;
            musicMap = null;
        }
    }
}
