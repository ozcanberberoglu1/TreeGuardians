namespace TreeGuardians.Data
{
    public enum Rarity { Common = 0, Rare = 1, Epic = 2, Legendary = 3 }

    public enum CurrencyType { Coins = 0, Sap = 1, Gems = 2, Trophies = 3 }

    public enum Language { English = 0, Turkish = 1 }

    public enum QualityTier { Low = 0, Medium = 1, High = 2 }

    public enum BattleSide { Player = 0, Enemy = 1 }

    public enum BotDifficulty { Easy = 0, Normal = 1, Hard = 2 }

    public enum TreeSectionType
    {
        HeartwoodCore = 0,
        Trunk = 1,
        Branch = 2,
        BarkArmor = 3,
        CanopyShield = 4,
        RootStabilizer = 5
    }

    public enum ProjectileMotion
    {
        Straight = 0,
        Ballistic = 1,
        Homing = 2,
        Bouncing = 3,
        Piercing = 4,
        AreaBomb = 5
    }

    public enum StatusEffectType { None = 0, Poison = 1, Root = 2, Stun = 3, Slow = 4, Shield = 5, ArmorBuff = 6, Heal = 7 }

    public enum TargetPreference { Nearest = 0, LowestHealth = 1, Structure = 2, ExposedCore = 3, Random = 4, Support = 5 }

    public enum BranchBreakBehavior
    {
        Eliminate = 0,
        FallWithDamage = 1,
        FallToEmptySlot = 2,
        HoverIfFlying = 3
    }

    public enum ToolEffectType
    {
        Projectile = 0,
        HealArea = 1,
        HomingSwarm = 2,
        DeflectField = 3,
        SlowField = 4,
        Barrier = 5
    }

    public enum TreeUpgradePath
    {
        HeartwoodLevel = 0,
        BarkThickness = 1,
        BranchDurability = 2,
        RootStrength = 3,
        SapFlow = 4,
        CanopyGrowth = 5
    }

    public enum TreeVisualTier { Sprouting = 0, Strong = 1, Ancient = 2 }

    public enum ChestTier { Twig = 0, Grove = 1, Ancient = 2, Moon = 3, Sun = 4 }

    public enum BattleOutcome { None = 0, Victory = 1, Defeat = 2, Draw = 3 }

    public enum BattleEndReason { None = 0, CoreDestroyed = 1, AllGuardiansDown = 2, TimeUp = 3, Forfeit = 4 }

    public enum QuestType
    {
        PlayBattles = 0,
        WinBattles = 1,
        BreakBarkSections = 2,
        UpgradeGuardian = 3,
        OpenChest = 4,
        SpecialDamage = 5,
        BreakBranches = 6,
        UseTools = 7
    }

    public enum AimMode { PullBack = 0, DirectDrag = 1 }

    public enum AudioEventId
    {
        None = 0,
        UiClick = 1,
        UiPanelOpen = 2,
        UiPanelClose = 3,
        UiError = 4,
        AimStart = 5,
        ProjectileLaunch = 6,
        BarkHit = 7,
        BranchBreak = 8,
        GuardianHurt = 9,
        CoreExposed = 10,
        CoreHit = 11,
        Victory = 12,
        Defeat = 13,
        ChestOpen = 14,
        Upgrade = 15,
        RewardPop = 16,
        CoinCount = 17,
        ToolUse = 18,
        Heal = 19,
        Countdown = 20,
        Explosion = 21,
        Debris = 22,
        GuardianDeath = 23,
        CritHit = 24,
        TurnStart = 25,
        EnemyTurn = 26,
        TimerTick = 27,
        CardSelect = 28,
        WindGust = 29,
        Thunder = 30,
        TurnTimeout = 31,
        BattleStart = 32,
        CastleCollapse = 33,
        WallCrack = 34,
        GroundThud = 35,
        ShieldBlock = 36,
        LaunchLight = 37,
        LaunchHeavy = 38,
        LaunchMagic = 39,
        MagicImpact = 40,
        PoisonHiss = 41,
        SpecialReady = 42,
        SpecialBuff = 43,
        Equip = 44,
        ChestShake = 45,
        Transition = 46,
        Unlock = 47,
        StarPop = 48,
        BeeBuzz = 49,
        VineWhip = 50,
        WindChime = 51,
        ShieldUp = 52
    }

    public enum AmbienceTrackId { None = 0, Forest = 1, Rain = 2 }

    public enum MusicTrackId { None = 0, MainMenu = 1, BattleSunny = 2, BattleSwamp = 3, BattleAutumn = 4, BattleMoon = 5, Results = 6 }
}
