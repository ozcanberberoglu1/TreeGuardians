using System.Collections.Generic;
using System.IO;
using TreeGuardians.Data;
using UnityEditor;
using UnityEngine;

namespace TreeGuardians.Editor
{
    /// Imports the procedurally synthesized audio (Tools/Audio/synth_audio.py -> Assets/TreeGuardians/Audio) with mobile
    /// import settings, fills the AudioLibrary (levels balanced from the measured short-term loudness of every clip) and
    /// maps the per-content sound ids (projectile launch/impact, tool use, guardian attack/hurt).
    public static class AudioContentBuilder
    {
        public const string Root = "Assets/TreeGuardians/Audio";
        public const string LibraryPath = "Assets/TreeGuardians/ScriptableObjects/Audio/AudioLibrary.asset";
        const string ContentRoot = "Assets/TreeGuardians/ScriptableObjects";
        const float LongSfxSeconds = 1.5f;

        // Priority: 0 = most important. Stingers 16, big destruction/flow 32-48, UI 64, hits 96-128, launches 144, ambient one-shots 180+.
        const int PStinger = 16, PBig = 40, PUi = 64, PHit = 128, PLaunch = 144, PAmbient = 200;

        struct Sfx
        {
            public AudioEventId id;
            public string[] files;
            public float vol, pitch, minInterval, duckTo, duckHold;
            public bool ui;
            public int maxInstances, priority;
        }

        static Sfx S(AudioEventId id, float vol, bool ui, float pitch, float minInterval, int maxInstances, int priority, params string[] files) =>
            new Sfx { id = id, files = files, vol = vol, ui = ui, pitch = pitch, minInterval = minInterval, maxInstances = maxInstances, priority = priority, duckTo = 1f, duckHold = 0f };

        /// Same as S(...) and also ducks music + ambience to 'duckTo' for 'duckHold' seconds whenever the event plays.
        static Sfx D(float duckTo, float duckHold, Sfx s) { s.duckTo = duckTo; s.duckHold = duckHold; return s; }

        // Target effective levels (short-term, phone-weighted): UI ~ -20 dB, launches ~ -18.5, hits ~ -16.5, big events ~ -12..-15.
        static readonly Sfx[] SfxMap =
        {
            // UI
            S(AudioEventId.UiClick, 0.5f, true, 0.05f, 0.04f, 2, PUi, "ui_click"),
            S(AudioEventId.UiPanelOpen, 0.25f, true, 0.04f, 0.08f, 1, PUi, "ui_panel_open"),
            S(AudioEventId.UiPanelClose, 0.33f, true, 0.04f, 0.08f, 1, PUi, "ui_panel_close"),
            S(AudioEventId.UiError, 0.35f, true, 0.02f, 0.15f, 1, PUi, "ui_error"),
            S(AudioEventId.CardSelect, 0.35f, true, 0.04f, 0.05f, 2, PUi, "ui_card_select"),
            S(AudioEventId.RewardPop, 0.4f, true, 0.06f, 0.04f, 3, PUi, "reward_pop"),
            S(AudioEventId.StarPop, 0.45f, true, 0f, 0.05f, 3, PUi - 8, "star_pop"),
            S(AudioEventId.CoinCount, 0.35f, true, 0.06f, 0.3f, 1, PUi + 16, "coin_shower"),
            S(AudioEventId.Equip, 0.4f, true, 0.05f, 0.05f, 2, PUi, "equip"),
            S(AudioEventId.ChestShake, 0.45f, true, 0.04f, 0.1f, 1, PUi, "chest_shake"),
            S(AudioEventId.Transition, 0.3f, true, 0.03f, 0.2f, 1, PUi, "transition"),
            D(0.35f, 2.0f, S(AudioEventId.Unlock, 0.7f, true, 0f, 0.5f, 1, PBig, "unlock")),
            D(0.5f, 1.2f, S(AudioEventId.ChestOpen, 1.0f, true, 0.03f, 0.2f, 1, PBig, "chest_open")),
            D(0.6f, 0.8f, S(AudioEventId.Upgrade, 0.85f, true, 0.02f, 0.2f, 1, PBig, "upgrade")),
            D(0.3f, 2.2f, S(AudioEventId.Victory, 0.85f, true, 0f, 0.5f, 1, PStinger, "victory")),
            D(0.3f, 2.6f, S(AudioEventId.Defeat, 0.85f, true, 0f, 0.5f, 1, PStinger, "defeat")),
            // battle flow
            S(AudioEventId.Countdown, 0.6f, true, 0f, 0.2f, 1, PUi, "countdown"),
            D(0.6f, 1.0f, S(AudioEventId.BattleStart, 0.75f, true, 0f, 1.0f, 1, PBig, "battle_start")),
            S(AudioEventId.TurnStart, 0.6f, true, 0f, 0.3f, 1, PUi, "turn_start"),
            S(AudioEventId.EnemyTurn, 0.45f, true, 0f, 0.3f, 1, PUi, "enemy_turn"),
            S(AudioEventId.TurnTimeout, 0.55f, true, 0f, 0.5f, 1, PUi, "turn_timeout"),
            S(AudioEventId.TimerTick, 0.6f, true, 0f, 0.5f, 1, PUi, "timer_tick"),
            S(AudioEventId.SpecialReady, 0.5f, true, 0.02f, 1.0f, 1, PUi, "special_ready"),
            // launches
            S(AudioEventId.AimStart, 0.6f, false, 0.06f, 0.05f, 1, PLaunch, "aim_start"),
            S(AudioEventId.ProjectileLaunch, 0.65f, false, 0.08f, 0.03f, 2, PLaunch, "launch_1", "launch_2", "launch_3"),
            S(AudioEventId.LaunchLight, 0.65f, false, 0.1f, 0.035f, 2, PLaunch, "launch_light_1", "launch_light_2", "launch_light_3"),
            S(AudioEventId.LaunchHeavy, 0.55f, false, 0.08f, 0.04f, 2, PLaunch, "launch_heavy_1", "launch_heavy_2"),
            S(AudioEventId.LaunchMagic, 0.45f, false, 0.08f, 0.04f, 2, PLaunch, "launch_magic_1", "launch_magic_2"),
            S(AudioEventId.BeeBuzz, 0.4f, false, 0.1f, 0.3f, 1, PLaunch, "bee_buzz"),
            S(AudioEventId.VineWhip, 0.5f, false, 0.08f, 0.05f, 2, PLaunch, "vine_whip"),
            // impacts
            S(AudioEventId.BarkHit, 0.85f, false, 0.1f, 0.03f, 3, PHit, "wall_hit_1", "wall_hit_2", "wall_hit_3"),
            S(AudioEventId.Debris, 0.45f, false, 0.12f, 0.08f, 2, PHit + 24, "debris"),
            D(0.7f, 0.3f, S(AudioEventId.Explosion, 0.6f, false, 0.08f, 0.05f, 2, PHit - 16, "explosion")),
            S(AudioEventId.CritHit, 0.65f, false, 0.05f, 0.05f, 2, PHit - 16, "crit"),
            S(AudioEventId.MagicImpact, 0.6f, false, 0.1f, 0.03f, 3, PHit, "magic_impact"),
            S(AudioEventId.PoisonHiss, 0.8f, false, 0.08f, 0.1f, 2, PHit, "poison_hiss"),
            S(AudioEventId.GroundThud, 0.75f, false, 0.1f, 0.05f, 2, PHit + 32, "ground_thud_1", "ground_thud_2"),
            S(AudioEventId.ShieldBlock, 0.55f, false, 0.08f, 0.06f, 2, PHit, "shield_block"),
            S(AudioEventId.GuardianHurt, 0.62f, false, 0.12f, 0.06f, 2, PHit - 8, "guardian_hurt"),
            S(AudioEventId.GuardianDeath, 1.0f, false, 0.06f, 0.1f, 2, PHit - 32, "guardian_death"),
            // destruction
            S(AudioEventId.WallCrack, 0.6f, false, 0.08f, 0.12f, 2, PHit - 24, "wall_crack_1", "wall_crack_2"),
            D(0.6f, 0.4f, S(AudioEventId.BranchBreak, 0.5f, false, 0.06f, 0.1f, 2, PBig + 8, "wall_break")),
            D(0.2f, 2.5f, S(AudioEventId.CastleCollapse, 0.9f, false, 0f, 1.0f, 1, PStinger, "castle_collapse")),
            // specials and tools
            S(AudioEventId.SpecialBuff, 0.55f, false, 0.04f, 0.2f, 1, PHit - 16, "special_buff"),
            S(AudioEventId.ShieldUp, 0.6f, false, 0.04f, 0.2f, 1, PHit - 16, "shield_up"),
            S(AudioEventId.WindChime, 0.55f, false, 0.03f, 0.3f, 1, PHit - 16, "wind_chime"),
            S(AudioEventId.ToolUse, 0.75f, false, 0.06f, 0.1f, 1, PHit - 16, "tool_use"),
            S(AudioEventId.Heal, 0.7f, false, 0.04f, 0.15f, 1, PHit - 16, "heal"),
            // weather
            S(AudioEventId.WindGust, 0.4f, false, 0.1f, 1.5f, 1, PAmbient, "wind_gust"),
            D(0.7f, 1.5f, S(AudioEventId.Thunder, 0.45f, false, 0.08f, 2f, 1, PAmbient - 20, "thunder")),
            // legacy tree mode (the castle duel never fires them): keep empty so nothing plays a wrong placeholder
            S(AudioEventId.CoreExposed, 0.6f, false, 0f, 0.2f, 1, PBig),
            S(AudioEventId.CoreHit, 0.8f, false, 0.05f, 0.05f, 2, PHit),
        };

        static readonly (MusicTrackId id, string file, float vol)[] MusicMap =
        {
            (MusicTrackId.MainMenu, "music_menu", 0.45f),
            (MusicTrackId.BattleSunny, "music_battle", 0.35f),
            (MusicTrackId.BattleSwamp, "music_battle_swamp", 0.37f),
            (MusicTrackId.BattleAutumn, "music_battle_autumn", 0.37f),
            (MusicTrackId.BattleMoon, "music_battle_moon", 0.38f),
            (MusicTrackId.Results, "music_results", 0.4f),
        };

        static readonly (AmbienceTrackId id, string file, float vol)[] AmbienceMap =
        {
            (AmbienceTrackId.Forest, "amb_forest", 0.3f),
            (AmbienceTrackId.Rain, "amb_rain", 0.45f),
        };

        [MenuItem("Tree Guardians/Audio/Import + Assign Generated Audio", priority = 40)]
        public static void ImportAndAssign()
        {
            ConfigureImporters();
            var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (lib == null) { Debug.LogError("[TG] AudioLibrary missing: " + LibraryPath); return; }
            if (lib.sfx == null) lib.sfx = new List<SfxEntry>();
            int assigned = 0;
            var missing = new List<string>();
            foreach (var s in SfxMap)
            {
                var clips = new List<AudioClip>();
                foreach (var f in s.files)
                {
                    var c = Load("SFX", f);
                    if (c != null) clips.Add(c); else missing.Add("SFX/" + f);
                }
                var e = lib.sfx.Find(x => x != null && x.id == s.id);
                if (e == null) { e = new SfxEntry { id = s.id }; lib.sfx.Add(e); }
                e.clips = clips.ToArray();
                e.volume = s.vol; e.pitchVariance = s.pitch; e.isUi = s.ui; e.minInterval = s.minInterval;
                e.maxInstances = Mathf.Clamp(s.maxInstances, 1, 8);
                e.priority = Mathf.Clamp(s.priority, 0, 255);
                e.duckTo = s.duckTo; e.duckHold = s.duckHold;
                e.pausable = !s.ui;
                if (clips.Count > 0) assigned++;
            }
            lib.sfx.Sort((a, b) => (a == null ? int.MaxValue : (int)a.id).CompareTo(b == null ? int.MaxValue : (int)b.id));
            if (lib.music == null) lib.music = new List<MusicEntry>();
            foreach (var m in MusicMap)
            {
                var e = lib.music.Find(x => x != null && x.id == m.id);
                if (e == null) { e = new MusicEntry { id = m.id }; lib.music.Add(e); }
                e.clip = Load("Music", m.file); e.volume = m.vol;
                if (e.clip == null) missing.Add("Music/" + m.file);
            }
            if (lib.ambience == null) lib.ambience = new List<AmbienceEntry>();
            foreach (var a in AmbienceMap)
            {
                var e = lib.ambience.Find(x => x != null && x.id == a.id);
                if (e == null) { e = new AmbienceEntry { id = a.id }; lib.ambience.Add(e); }
                e.clip = Load("Ambience", a.file); e.volume = a.vol;
                if (e.clip == null) missing.Add("Ambience/" + a.file);
            }
            EditorUtility.SetDirty(lib);
            ApplyDspBufferSize();
            int content = ApplyContentSfx();
            AssetDatabase.SaveAssets();
            if (missing.Count > 0) Debug.LogWarning("[TG] Audio clips missing (run Tools/Audio/synth_audio.py): " + string.Join(", ", missing));
            Debug.Log($"[TG] Audio assigned: {assigned}/{SfxMap.Length} SFX events, {MusicMap.Length} music, {AmbienceMap.Length} ambience, {content} content assets re-mapped.");
        }

        /// Project Settings > Audio > DSP Buffer Size = Good latency (512): taps and shots feel ~12 ms tighter than Best Performance (1024).
        [MenuItem("Tree Guardians/Audio/Set DSP Buffer (Good Latency)", priority = 42)]
        public static void ApplyDspBufferSize()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset");
            if (assets == null || assets.Length == 0 || assets[0] == null) return;
            var so = new SerializedObject(assets[0]);
            var p = so.FindProperty("m_DSPBufferSize");
            if (p == null || p.intValue == 512) return;
            p.intValue = 512;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[TG] DSP buffer size set to 512 (Good latency).");
        }

        static AudioClip Load(string folder, string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Root}/{folder}/{name}.wav");

        /// Levels are authored and balanced by the library, so the importer never normalizes.
        /// Short SFX: decompressed ADPCM (instant, cheap). SFX longer than 1.5 s: compressed in memory (Vorbis 0.6).
        /// Ambience: compressed in memory (Vorbis 0.6). Music: streamed (Vorbis 0.5). Everything mono.
        static void ConfigureImporters()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is AudioImporter imp)) continue;
                bool music = path.Contains("/Music/"), amb = path.Contains("/Ambience/");
                bool longSfx = !music && !amb && ClipSeconds(path) > LongSfxSeconds;
                var s = imp.defaultSampleSettings;
                var before = s;
                s.loadType = music ? AudioClipLoadType.Streaming : amb || longSfx ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = music || amb || longSfx ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
                s.quality = music ? 0.5f : 0.6f;
                s.preloadAudioData = !music;
                s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                bool changed = before.loadType != s.loadType || before.compressionFormat != s.compressionFormat || !Mathf.Approximately(before.quality, s.quality)
                               || before.preloadAudioData != s.preloadAudioData || before.sampleRateSetting != s.sampleRateSetting
                               || !imp.forceToMono || imp.loadInBackground != (music || amb) || ImporterNormalizes(imp);
                if (!changed) continue;
                imp.defaultSampleSettings = s;
                imp.forceToMono = true;
                imp.loadInBackground = music || amb;
                SetNormalize(imp, false);
                imp.SaveAndReimport();
            }
        }

        /// Duration from the WAV header (the clip may not be imported yet).
        static float ClipSeconds(string assetPath)
        {
            try
            {
                var full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
                using (var fs = File.OpenRead(full))
                using (var br = new BinaryReader(fs))
                {
                    if (new string(br.ReadChars(4)) != "RIFF") return 0f;
                    br.ReadInt32();
                    if (new string(br.ReadChars(4)) != "WAVE") return 0f;
                    int byteRate = 0;
                    while (fs.Position + 8 <= fs.Length)
                    {
                        var id = new string(br.ReadChars(4));
                        int size = br.ReadInt32();
                        if (id == "fmt ")
                        {
                            br.ReadInt16(); br.ReadInt16(); br.ReadInt32();
                            byteRate = br.ReadInt32();
                            fs.Position += size - 12;
                        }
                        else if (id == "data") return byteRate > 0 ? size / (float)byteRate : 0f;
                        else fs.Position += size + (size & 1);
                    }
                }
            }
            catch (IOException) { }
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            return clip != null ? clip.length : 0f;
        }

        // AudioImporter's 'normalize' has no public C# property; set the serialized field instead.
        static bool ImporterNormalizes(AudioImporter imp)
        {
            var p = new SerializedObject(imp).FindProperty("m_Normalize") ?? new SerializedObject(imp).FindProperty("normalize");
            return p != null && p.boolValue;
        }

        static void SetNormalize(AudioImporter imp, bool value)
        {
            var so = new SerializedObject(imp);
            var p = so.FindProperty("m_Normalize") ?? so.FindProperty("normalize");
            if (p == null) return;
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ content sound mapping
        /// Launch/impact per projectile type. The launch sound is played by ProjectileService.Fire, so guardians have no attack sound.
        public static void ApplyProjectileSfx(ProjectileDefinition p)
        {
            if (p == null) return;
            bool splash = p.splashRadius > 0f;
            bool heavy = p.motion == ProjectileMotion.Ballistic || p.motion == ProjectileMotion.AreaBomb || p.motion == ProjectileMotion.Bouncing || splash || p.id == "bark_slam";
            if (p.id == "bee") p.launchSfx = AudioEventId.BeeBuzz;
            else if (p.id == "vine_spear") p.launchSfx = AudioEventId.VineWhip;
            else if (p.motion == ProjectileMotion.Homing) p.launchSfx = AudioEventId.LaunchMagic;
            else if (heavy) p.launchSfx = AudioEventId.LaunchHeavy;
            else p.launchSfx = AudioEventId.LaunchLight;

            if (p.statusEffect.type == StatusEffectType.Poison) p.impactSfx = AudioEventId.PoisonHiss;
            else if (splash) p.impactSfx = AudioEventId.Explosion;
            else if (p.motion == ProjectileMotion.Homing && p.id != "bee") p.impactSfx = AudioEventId.MagicImpact;
            else p.impactSfx = AudioEventId.BarkHit;
        }

        public static void ApplyGuardianSfx(GuardianDefinition g)
        {
            if (g == null) return;
            g.attackSfx = AudioEventId.None; // kept for future animal vocals; the projectile's launchSfx is the shot sound
            g.hurtSfx = AudioEventId.GuardianHurt;
        }

        public static void ApplyToolSfx(ToolDefinition t)
        {
            if (t == null) return;
            switch (t.id)
            {
                case "dew_sprayer": t.useSfx = AudioEventId.None; break; // ToolController already plays Heal for HealArea
                case "root_catapult": t.useSfx = AudioEventId.ToolUse; break; // catapult release; the boulder adds LaunchHeavy
                case "bee_hive": t.useSfx = AudioEventId.BeeBuzz; break;
                case "seed_shield": t.useSfx = AudioEventId.ShieldUp; break;
                case "wind_bell": t.useSfx = AudioEventId.WindChime; break;
                case "vine_net": t.useSfx = AudioEventId.VineWhip; break;
                default: t.useSfx = AudioEventId.ToolUse; break;
            }
        }

        [MenuItem("Tree Guardians/Audio/Apply Content SFX Mapping", priority = 41)]
        public static void ApplyContentSfxMenu()
        {
            int n = ApplyContentSfx();
            AssetDatabase.SaveAssets();
            Debug.Log($"[TG] Content SFX mapping applied to {n} assets.");
        }

        static int ApplyContentSfx()
        {
            int n = 0;
            foreach (var p in LoadAll<ProjectileDefinition>(ContentRoot + "/Projectiles")) { ApplyProjectileSfx(p); EditorUtility.SetDirty(p); n++; }
            foreach (var g in LoadAll<GuardianDefinition>(ContentRoot + "/Guardians")) { ApplyGuardianSfx(g); EditorUtility.SetDirty(g); n++; }
            foreach (var t in LoadAll<ToolDefinition>(ContentRoot + "/Tools")) { ApplyToolSfx(t); EditorUtility.SetDirty(t); n++; }
            return n;
        }

        static IEnumerable<T> LoadAll<T>(string folder) where T : ScriptableObject
        {
            if (!AssetDatabase.IsValidFolder(folder)) yield break;
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder }))
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null) yield return a;
            }
        }
    }
}
