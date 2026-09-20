using System;
using System.Collections.Generic;
using System.IO;
using TreeGuardians.Core;
using UnityEngine;

namespace TreeGuardians.Save
{
    public interface ISaveMigration
    {
        int FromVersion { get; }
        void Apply(PlayerSaveData data);
    }

    sealed class Migration_0_to_1 : ISaveMigration
    {
        public int FromVersion => 0;

        public void Apply(PlayerSaveData d)
        {
            if (string.IsNullOrEmpty(d.playerId)) d.playerId = Guid.NewGuid().ToString("N");
            if (d.equippedGuardianIds == null || d.equippedGuardianIds.Length != 8) d.equippedGuardianIds = new string[8];
            if (d.equippedToolIds == null || d.equippedToolIds.Length != 3) d.equippedToolIds = new string[3];
            d.tree ??= new TreeSaveData();
            if (d.tree.upgradeLevels == null || d.tree.upgradeLevels.Length != 6) d.tree.upgradeLevels = new int[6];
            d.settings ??= new SettingsSaveData();
            d.tutorial ??= new TutorialSaveData();
            d.stats ??= new StatsSaveData();
            d.dailyReward ??= new DailyRewardSaveData();
        }
    }

    /// Local JSON save with atomic writes, backup, corruption recovery and versioned migrations.
    public sealed class SaveService : MonoBehaviour
    {
        public const int CurrentSchemaVersion = 1;
        const string FileName = "treeguardians_save.json";

        [SerializeField] bool logSaves = true;

        public PlayerSaveData Data { get; private set; }
        public bool LoadFailed { get; private set; }
        public bool RecoveredFromBackup { get; private set; }
        public bool IsNewSave { get; private set; }
        public bool ClockRolledBack { get; private set; }
        public bool IsInitialized { get; private set; }

        public event Action OnSaved;
        public event Action OnLoaded;

        readonly List<ISaveMigration> migrations = new List<ISaveMigration>();
        DateTime lastKnownUtc = DateTime.MinValue;
        string overridePath;

        public string MainPath => overridePath ?? Path.Combine(Application.persistentDataPath, FileName);
        public string BackupPath => MainPath + ".bak";
        string TempPath => MainPath + ".tmp";
        string CorruptPath => MainPath + ".corrupt";

        void Awake()
        {
            Services.Register(this);
            EnsureMigrations();
        }

        void EnsureMigrations()
        {
            if (migrations.Count > 0) return;
            migrations.Add(new Migration_0_to_1());
        }

        void OnDestroy()
        {
            Services.Unregister(this);
        }

        public void RegisterMigration(ISaveMigration migration)
        {
            if (migration != null) migrations.Add(migration);
        }

        /// Test hook: redirect file IO to a temp path.
        public void SetPathOverride(string path) => overridePath = path;

        public void Initialize()
        {
            Load();
            IsInitialized = true;
        }

        public void Load()
        {
            LoadFailed = false;
            RecoveredFromBackup = false;
            IsNewSave = false;

            if (TryRead(MainPath, out var data))
            {
                Data = data;
            }
            else if (TryRead(BackupPath, out data))
            {
                Data = data;
                RecoveredFromBackup = true;
                TGLog.Warn("Main save unreadable; recovered from backup.");
            }
            else
            {
                if (File.Exists(MainPath))
                {
                    LoadFailed = true;
                    SafeMove(MainPath, CorruptPath);
                    TGLog.Warn("Save file corrupt and no valid backup. A new save will be created.");
                }
                Data = CreateDefault();
                IsNewSave = true;
            }

            ApplyMigrations(Data);
            lastKnownUtc = Data.lastKnownUtcTicks > 0 ? new DateTime(Data.lastKnownUtcTicks, DateTimeKind.Utc) : DateTime.MinValue;
            ClockRolledBack = lastKnownUtc > DateTime.MinValue.AddDays(1) && DateTime.UtcNow < lastKnownUtc.AddMinutes(-5);
            if (ClockRolledBack) TGLog.Warn("Device clock appears to have moved backwards; timers will be clamped.");

            if (LoadFailed || RecoveredFromBackup)
                GameEventBus.Publish(new SaveLoadFailedEvent { recoveredFromBackup = RecoveredFromBackup });

            OnLoaded?.Invoke();
        }

        bool TryRead(string path, out PlayerSaveData data)
        {
            data = null;
            try
            {
                if (!File.Exists(path)) return false;
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return false;
                data = FromJson(json);
                return data != null && data.IsPlausible();
            }
            catch (Exception e)
            {
                TGLog.Warn($"Failed to read save at {path}: {e.Message}");
                data = null;
                return false;
            }
        }

        static void SafeMove(string from, string to)
        {
            try
            {
                if (File.Exists(to)) File.Delete(to);
                File.Move(from, to);
            }
            catch (Exception e)
            {
                TGLog.Warn($"Could not move {from}: {e.Message}");
            }
        }

        public PlayerSaveData CreateDefault()
        {
            return new PlayerSaveData
            {
                schemaVersion = CurrentSchemaVersion,
                playerId = Guid.NewGuid().ToString("N")
            };
        }

        public void ResetToNewSave()
        {
            Data = CreateDefault();
            IsNewSave = true;
            LoadFailed = false;
            RecoveredFromBackup = false;
            SaveNow();
            OnLoaded?.Invoke();
        }

        public void ApplyMigrations(PlayerSaveData d)
        {
            if (d == null) return;
            EnsureMigrations();
            int guard = 0;
            while (d.schemaVersion < CurrentSchemaVersion && guard++ < 64)
            {
                ISaveMigration m = null;
                for (int i = 0; i < migrations.Count; i++)
                    if (migrations[i].FromVersion == d.schemaVersion) { m = migrations[i]; break; }
                if (m == null)
                {
                    TGLog.Warn($"No migration from schema {d.schemaVersion}; bumping to {CurrentSchemaVersion}.");
                    d.schemaVersion = CurrentSchemaVersion;
                    break;
                }
                m.Apply(d);
                d.schemaVersion = m.FromVersion + 1;
            }
        }

        public bool SaveNow()
        {
            if (Data == null) return false;
            try
            {
                Data.schemaVersion = CurrentSchemaVersion;
                Data.lastSaveUtcTicks = DateTime.UtcNow.Ticks;
                Data.lastKnownUtcTicks = GetUtcNow().Ticks;
                var json = ToJson(Data, Application.isEditor);
                var dir = Path.GetDirectoryName(MainPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(TempPath, json);
                if (File.Exists(MainPath))
                {
                    File.Copy(MainPath, BackupPath, true);
                    File.Delete(MainPath);
                }
                File.Move(TempPath, MainPath);
                if (logSaves) TGLog.Info("Save written.");
                OnSaved?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                TGLog.Error($"Save failed: {e.Message}");
                return false;
            }
        }

        /// Monotonic UTC clock: never returns a time earlier than the last observed one.
        public DateTime GetUtcNow()
        {
            var now = DateTime.UtcNow;
            if (now < lastKnownUtc) return lastKnownUtc;
            lastKnownUtc = now;
            return now;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && IsInitialized) SaveNow();
        }

        void OnApplicationQuit()
        {
            if (IsInitialized) SaveNow();
        }

        public static string ToJson(PlayerSaveData data, bool pretty = false) => JsonUtility.ToJson(data, pretty);
        public static PlayerSaveData FromJson(string json) => JsonUtility.FromJson<PlayerSaveData>(json);
    }
}
