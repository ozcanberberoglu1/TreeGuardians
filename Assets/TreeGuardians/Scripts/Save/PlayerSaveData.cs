using System;
using System.Collections.Generic;

namespace TreeGuardians.Save
{
    [Serializable]
    public sealed class TreeSaveData
    {
        public int[] upgradeLevels = new int[6];
        public string skinId = "";
    }

    [Serializable]
    public sealed class GuardianSaveData
    {
        public string id = "";
        public bool unlocked;
        public int level = 1;
        public int shards;
        public bool isNew;
    }

    [Serializable]
    public sealed class ToolSaveData
    {
        public string id = "";
        public bool unlocked;
        public int level = 1;
    }

    [Serializable]
    public sealed class ChestSlotSaveData
    {
        public string chestId = "";
        public bool unlocking;
        public long unlockStartUtcTicks;
        public long unlockEndUtcTicks;
        public int arenaIndex;

        public bool IsEmpty => string.IsNullOrEmpty(chestId);

        public void Clear()
        {
            chestId = "";
            unlocking = false;
            unlockStartUtcTicks = 0;
            unlockEndUtcTicks = 0;
            arenaIndex = 0;
        }
    }

    [Serializable]
    public sealed class QuestProgressSaveData
    {
        public string id = "";
        public int progress;
        public bool claimed;
        public long assignedUtcTicks;
    }

    [Serializable]
    public sealed class AchievementProgressSaveData
    {
        public string id = "";
        public int progress;
        public bool claimed;
    }

    [Serializable]
    public sealed class DailyRewardSaveData
    {
        public int nextDayIndex;
        public long lastClaimUtcTicks;
    }

    [Serializable]
    public sealed class SettingsSaveData
    {
        public int language = -1;
        public float musicVolume = 0.8f;
        public float sfxVolume = 1f;
        public bool vibration = true;
        public bool reduceHaptics;
        public bool reduceMotion;
        public int qualityTier = 1;
        public int aimMode;
    }

    [Serializable]
    public sealed class TutorialSaveData
    {
        public bool battleTutorialDone;
        public bool menuTutorialDone;
        public bool skipped;
        public int lastStep;
    }

    [Serializable]
    public sealed class StatsSaveData
    {
        public int battlesPlayed;
        public int wins;
        public int losses;
        public int draws;
        public int barkBroken;
        public int branchesBroken;
        public int chestsOpened;
        public int guardianUpgrades;
        public int toolsUsed;
        public int shotsFired;
        public int shotsHit;
        public float specialDamage;
        public float totalDamage;
    }

    [Serializable]
    public sealed class PlayerSaveData
    {
        public int schemaVersion = 0;
        public string playerId = "";
        public string displayName = "Guardian";
        public int coins;
        public int sap;
        public int gems;
        public int trophies;
        public int bestTrophies;
        public int playerLevel = 1;
        public int playerXp;
        public TreeSaveData tree = new TreeSaveData();
        public List<GuardianSaveData> guardians = new List<GuardianSaveData>();
        public string[] equippedGuardianIds = new string[8];
        public List<ToolSaveData> tools = new List<ToolSaveData>();
        public string[] equippedToolIds = new string[3];
        public List<ChestSlotSaveData> chestSlots = new List<ChestSlotSaveData>();
        public int chestsOpenedSinceEpic;
        public int currentArenaIndex;
        public int highestArenaIndex;
        public List<QuestProgressSaveData> quests = new List<QuestProgressSaveData>();
        public List<AchievementProgressSaveData> achievements = new List<AchievementProgressSaveData>();
        public long lastDailyQuestResetUtcTicks;
        public DailyRewardSaveData dailyReward = new DailyRewardSaveData();
        public SettingsSaveData settings = new SettingsSaveData();
        public TutorialSaveData tutorial = new TutorialSaveData();
        public StatsSaveData stats = new StatsSaveData();
        public long lastSaveUtcTicks;
        public long lastKnownUtcTicks;

        public bool IsPlausible()
        {
            return schemaVersion >= 0 && schemaVersion < 1000 && coins >= 0 && sap >= 0 && gems >= 0 && trophies >= 0;
        }
    }
}
