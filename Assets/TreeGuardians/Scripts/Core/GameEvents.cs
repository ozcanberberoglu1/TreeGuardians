using TreeGuardians.Data;

namespace TreeGuardians.Core
{
    public struct ServicesReadyEvent { }
    public struct CurrencyChangedEvent { public CurrencyType type; public int oldValue; public int newValue; }
    public struct GuardianUpgradedEvent { public string guardianId; public int newLevel; }
    public struct GuardianUnlockedEvent { public string guardianId; }
    public struct GuardianCardsChangedEvent { public string guardianId; public int shards; }
    public struct LoadoutChangedEvent { }
    public struct ToolsChangedEvent { }
    public struct TreeUpgradedEvent { public TreeUpgradePath path; public int newLevel; }
    public struct ArenaChangedEvent { public int arenaIndex; }
    public struct ChestSlotsChangedEvent { }
    public struct ChestOpenedEvent { public string chestId; }
    public struct QuestProgressEvent { public string questId; public int progress; public int target; }
    public struct QuestClaimedEvent { public string questId; }
    public struct RewardAppliedEvent { public string source; }
    public struct LanguageChangedEvent { public Language language; }
    public struct SettingsChangedEvent { }
    public struct SaveLoadFailedEvent { public bool recoveredFromBackup; }

    public struct BattleStartedEvent { public string arenaId; }
    public struct BattleFinishedEvent { public BattleOutcome outcome; public BattleEndReason reason; }
    public struct SectionDestroyedEvent { public TreeSectionType type; public BattleSide side; public string sectionId; }
    public struct SectionDamagedEvent { public BattleSide side; public float amount; public bool isCore; }
    public struct GuardianDefeatedEvent { public BattleSide side; public string guardianId; }
    public struct SpecialDamageEvent { public float amount; public BattleSide side; }
    public struct ToolUsedEvent { public string toolId; public BattleSide side; }
    public struct ProjectileFiredEvent { public BattleSide side; public bool isSpecial; }
    public struct ProjectileHitEvent { public BattleSide side; public bool hitTarget; }
    public struct CoreExposedEvent { public BattleSide side; }
    public struct BattleMessageEvent { public string key; public float duration; }
    public struct ScreenShakeEvent { public float amplitude; public float duration; }
}
