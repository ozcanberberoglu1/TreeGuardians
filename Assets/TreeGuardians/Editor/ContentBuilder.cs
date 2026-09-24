using System;
using System.Collections.Generic;
using System.IO;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Rewards;
using UnityEditor;
using UnityEngine;

namespace TreeGuardians.Editor
{
    /// Creates/updates every ScriptableObject asset with balanced seed data and wires the GameDatabase.
    public static class ContentBuilder
    {
        const string SO = "Assets/TreeGuardians/ScriptableObjects";
        public const string GameConfigPath = SO + "/Balance/GameConfig.asset";
        public const string BalancePath = SO + "/Balance/GameBalanceConfig.asset";
        public const string DatabasePath = SO + "/GameDatabase.asset";
        public const string LocalizationPath = SO + "/Localization/LocalizationTable.asset";
        public const string AudioLibraryPath = SO + "/Audio/AudioLibrary.asset";
        public const string RarityPalettePath = SO + "/Balance/RarityPalette.asset";
        public const string DefaultProjectilePrefabPath = "Assets/TreeGuardians/Prefabs/Projectiles/Projectile_Default.prefab";
        public const string DefaultGuardianPrefabPath = "Assets/TreeGuardians/Prefabs/Characters/Guardian_Default.prefab";

        static readonly Dictionary<string, ProjectileDefinition> projectiles = new Dictionary<string, ProjectileDefinition>();

        [MenuItem("Tree Guardians/2. Build Content Assets", priority = 2)]
        public static void BuildAll()
        {
            projectiles.Clear();
            var db = GetOrCreate<GameDatabase>(DatabasePath);
            var config = BuildGameConfig();
            var balance = BuildBalance();
            db.projectiles = BuildProjectiles();
            db.guardians = BuildGuardians();
            db.tools = BuildTools();
            db.chests = BuildChests();
            db.arenas = BuildArenas();
            db.quests = BuildQuests();
            db.achievements = BuildAchievements();
            db.dailyRewards = BuildDailyRewards();
            db.playerTree = BuildTree("tree_player", SO + "/Trees/Tree_Player.asset", "tree_player_name");
            db.enemyTree = BuildTree("tree_enemy", SO + "/Trees/Tree_Enemy.asset", "tree_enemy_name");
            db.rarityPalette = BuildRarityPalette();
            db.audioLibrary = BuildAudioLibrary();
            db.localization = BuildLocalization();
            db.defaultProjectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultProjectilePrefabPath);
            db.defaultGuardianPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultGuardianPrefabPath);
            EditorUtility.SetDirty(db);
            EditorUtility.SetDirty(config);
            EditorUtility.SetDirty(balance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TG] Content built: {db.guardians.Count} guardians, {db.projectiles.Count} projectiles, {db.tools.Count} tools, {db.arenas.Count} arenas, {db.chests.Count} chests, {db.quests.Count} quests, {db.localization.entries.Count} strings.");
        }

        // ------------------------------------------------------------------ helpers
        public static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        static Sprite S(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        static RewardBundle R(int coins = 0, int sap = 0, int gems = 0, string chest = null, string cardId = null, int cards = 0)
        {
            var b = new RewardBundle { coins = coins, sap = sap, gems = gems };
            if (!string.IsNullOrEmpty(chest)) b.chestIds.Add(chest);
            if (!string.IsNullOrEmpty(cardId) && cards > 0) b.AddCards(cardId, cards);
            return b;
        }

        // ------------------------------------------------------------------ config
        static GameConfig BuildGameConfig()
        {
            var c = GetOrCreate<GameConfig>(GameConfigPath);
            c.gameName = "Tree Guardians";
            c.studioName = "Leke Games";
            c.version = "0.1.0";
            c.bootScene = "00_Boot"; c.loadingScene = "01_Loading"; c.mainMenuScene = "02_MainMenu";
            c.battleScene = "03_Battle"; c.resultsScene = "04_Results"; c.sandboxScene = "05_Sandbox";
            c.referenceResolution = new Vector2(2340, 1080);
            c.canvasMatchWidthOrHeight = 0.5f;
            c.targetFrameRate = 60;
            c.minLoadingSeconds = 1.0f;
            EditorUtility.SetDirty(c);
            return c;
        }

        static GameBalanceConfig BuildBalance()
        {
            var b = GetOrCreate<GameBalanceConfig>(BalancePath);
            b.startingCoins = 1500; b.startingSap = 120; b.startingGems = 20; b.startingTrophies = 0;
            b.starterGuardianIds = new[] { "thorn_archer", "cone_bomber", "bark_knight", "dew_fairy" };
            b.starterToolIds = new[] { "root_catapult", "dew_sprayer" };
            b.starterChestId = "twig";
            b.guardianMaxLevel = 30;
            b.guardianSlotCount = 8; b.toolSlotCount = 3; b.chestSlotCount = 6;
            b.battleDurationSeconds = 150f; b.readyCountdownSeconds = 3f;
            b.branchBreakDamagePercent = 0.35f; b.branchBreakStunSeconds = 1.5f;
            b.autoAttackDamageMultiplier = 0.5f; b.autoAttackCooldownMultiplier = 1.6f;
            b.trophiesOnWin = 30; b.trophiesOnLoss = 15; b.trophiesOnDraw = 5;
            b.winCoinsBase = 120; b.loseCoinsBase = 30; b.winSapBase = 12; b.loseSapBase = 3; b.winCardDraws = 2;
            b.dailyRewardCooldownHours = 20f;
            EditorUtility.SetDirty(b);
            return b;
        }

        // ------------------------------------------------------------------ projectiles
        static ProjectileDefinition Proj(string id, string sprite, Color tint, ProjectileMotion motion, float speed, float damage,
            float structMul, float guardMul, float radius = 0.22f, float lifetime = 5f, Action<ProjectileDefinition> extra = null)
        {
            var p = GetOrCreate<ProjectileDefinition>($"{SO}/Projectiles/Projectile_{id}.asset");
            p.id = id; p.nameKey = "proj_" + id; p.icon = S(ArtPaths.Projectile(sprite));
            p.tint = tint; p.motion = motion; p.speed = speed; p.baseDamage = damage;
            p.structureMultiplier = structMul; p.guardianMultiplier = guardMul;
            p.collisionRadius = radius; p.lifetime = lifetime;
            p.gravityScale = 1f; p.homingStrength = 3f; p.bounceCount = 0; p.pierceCount = 0; p.splashRadius = 0f; p.splashFalloff = 0.4f;
            p.statusEffect = new StatusEffectSpec();
            p.impactSfx = AudioEventId.BarkHit; p.launchSfx = AudioEventId.ProjectileLaunch;
            p.shake = new ShakeProfile { amplitude = 0.05f, duration = 0.1f };
            p.impactVfxPrefab = null;
            extra?.Invoke(p);
            EditorUtility.SetDirty(p);
            projectiles[id] = p;
            return p;
        }

        static List<ProjectileDefinition> BuildProjectiles()
        {
            var list = new List<ProjectileDefinition>
            {
                Proj("thorn", "thorn", new Color(0.7f, 0.9f, 0.55f), ProjectileMotion.Straight, 22f, 32f, 1f, 1f),
                Proj("thorn_volley", "thorn", new Color(0.85f, 1f, 0.6f), ProjectileMotion.Straight, 24f, 26f, 1f, 1.1f),
                Proj("cone_bomb", "cone", new Color(0.8f, 0.6f, 0.35f), ProjectileMotion.Ballistic, 15f, 45f, 1.3f, 0.9f, 0.28f, 6f, p => { p.splashRadius = 1.6f; p.splashFalloff = 0.4f; p.shake = new ShakeProfile { amplitude = 0.1f, duration = 0.15f }; }),
                Proj("cannon_cone", "cone", new Color(1f, 0.7f, 0.3f), ProjectileMotion.Ballistic, 14f, 95f, 1.5f, 1f, 0.36f, 6f, p => { p.splashRadius = 2.4f; p.visualScale = 1.4f; p.shake = new ShakeProfile { amplitude = 0.16f, duration = 0.2f }; }),
                Proj("bark_slam", "rock", new Color(0.75f, 0.65f, 0.55f), ProjectileMotion.Straight, 14f, 30f, 2.2f, 0.6f, 0.3f, 2.5f, p => p.shake = new ShakeProfile { amplitude = 0.08f, duration = 0.12f }),
                Proj("dew_drop", "lightseed", new Color(0.6f, 0.9f, 1f), ProjectileMotion.Homing, 12f, 18f, 0.5f, 1f, 0.2f, 5f, p => p.homingStrength = 4f),
                Proj("vine_spear", "vinespear", new Color(0.5f, 0.85f, 0.4f), ProjectileMotion.Piercing, 20f, 34f, 1.6f, 1f, 0.22f, 4f, p => { p.pierceCount = 1; p.statusEffect = new StatusEffectSpec { type = StatusEffectType.Root, duration = 1.5f, magnitude = 1f }; }),
                Proj("spore_bomb", "sporebomb", new Color(0.95f, 0.6f, 0.8f), ProjectileMotion.Ballistic, 13f, 20f, 0.8f, 1f, 0.28f, 6f, p => { p.gravityScale = 0.9f; p.splashRadius = 2.2f; p.statusEffect = new StatusEffectSpec { type = StatusEffectType.Poison, duration = 4f, magnitude = 8f }; }),
                Proj("spore_cloud", "sporebomb", new Color(0.85f, 0.4f, 0.75f), ProjectileMotion.Ballistic, 12f, 30f, 0.8f, 1f, 0.34f, 6f, p => { p.gravityScale = 0.9f; p.splashRadius = 3f; p.visualScale = 1.4f; p.statusEffect = new StatusEffectSpec { type = StatusEffectType.Poison, duration = 6f, magnitude = 12f }; }),
                Proj("owl_feather", "thorn", new Color(0.9f, 0.95f, 1f), ProjectileMotion.Straight, 26f, 38f, 0.9f, 1.2f, 0.2f, 4f),
                Proj("acorn", "seed", new Color(0.75f, 0.55f, 0.3f), ProjectileMotion.Ballistic, 14f, 36f, 1.4f, 0.9f, 0.26f, 6f),
                Proj("light_seed", "lightseed", new Color(1f, 0.95f, 0.5f), ProjectileMotion.Homing, 16f, 26f, 0.7f, 1.1f, 0.2f, 5f, p => p.homingStrength = 5f),
                Proj("wood_chip", "rock", new Color(0.8f, 0.65f, 0.45f), ProjectileMotion.Straight, 18f, 24f, 1.5f, 0.8f, 0.22f, 4f),
                Proj("quill", "thorn", new Color(0.55f, 0.45f, 0.4f), ProjectileMotion.Straight, 28f, 16f, 0.8f, 1.2f, 0.16f, 3.5f),
                Proj("sprout_orb", "lightseed", new Color(0.6f, 1f, 0.7f), ProjectileMotion.Homing, 14f, 30f, 0.9f, 1f, 0.24f, 5f, p => p.homingStrength = 3.5f),
                Proj("root_boulder", "rock", new Color(0.6f, 0.5f, 0.4f), ProjectileMotion.Ballistic, 13f, 120f, 2f, 0.5f, 0.42f, 7f, p => { p.gravityScale = 1.2f; p.splashRadius = 1.8f; p.visualScale = 1.7f; p.shake = new ShakeProfile { amplitude = 0.2f, duration = 0.2f }; }),
                Proj("bee", "bee", new Color(1f, 0.85f, 0.3f), ProjectileMotion.Homing, 10f, 12f, 0.3f, 1.2f, 0.18f, 4f, p => p.homingStrength = 6f),
                Proj("bounce_cone", "cone", new Color(0.7f, 0.5f, 0.3f), ProjectileMotion.Bouncing, 16f, 28f, 1.2f, 0.9f, 0.26f, 6f, p => p.bounceCount = 2),
            };
            return list;
        }

        // ------------------------------------------------------------------ guardians
        static GuardianDefinition Guardian(string id, Rarity rarity, float hp, float atk, float str, float armor, float crit,
            string normalProj, float cd, float range, TargetPreference pref,
            GuardianSpecialKind special, string specialKey, string specialProj, float specialMag, float specialDur,
            GuardianPassiveKind passive, float passiveMag, bool flying, int arenaUnlock, bool playable)
        {
            var g = GetOrCreate<GuardianDefinition>($"{SO}/Guardians/Guardian_{id}.asset");
            g.id = id; g.nameKey = "g_" + id + "_name"; g.descriptionKey = "g_" + id + "_desc"; g.rarity = rarity;
            g.portrait = S(ArtPaths.Portrait(id)); g.cardArt = g.portrait; g.worldSprite = S(ArtPaths.WorldSprite(id)); g.worldPrefab = null;
            g.baseHealth = hp * 1.5f; g.baseAttack = atk; g.baseStructureDamage = str; g.baseArmor = armor; g.critChance = crit; g.critMultiplier = 1.5f;
            g.normalProjectile = projectiles.TryGetValue(normalProj, out var np) ? np : null;
            g.attackCooldown = cd; g.range = range; g.preferredTarget = pref; g.isFlying = flying; g.autoAttack = true; g.playerAimable = true;
            g.specialKind = special; g.specialNameKey = specialKey;
            g.specialProjectile = specialProj != null && projectiles.TryGetValue(specialProj, out var sp) ? sp : null;
            g.specialEnergyCost = 100f; g.specialMagnitude = specialMag; g.specialDuration = specialDur;
            g.passiveKind = passive; g.passiveMagnitude = passiveMag;
            g.arenaUnlockIndex = arenaUnlock; g.playableInPrototype = playable;
            EditorUtility.SetDirty(g);
            return g;
        }

        static List<GuardianDefinition> BuildGuardians()
        {
            return new List<GuardianDefinition>
            {
                Guardian("thorn_archer", Rarity.Common, 320, 34, 50, 4, 0.05f, "thorn", 2.2f, 18f, TargetPreference.Nearest, GuardianSpecialKind.Salvo, "special_salvo", "thorn_volley", 3f, 0.4f, GuardianPassiveKind.None, 0f, false, 0, true),
                Guardian("cone_bomber", Rarity.Common, 340, 42, 70, 5, 0.05f, "cone_bomb", 3.0f, 16f, TargetPreference.Structure, GuardianSpecialKind.PowerShot, "special_power_shot", "cannon_cone", 1f, 0f, GuardianPassiveKind.None, 0f, false, 0, true),
                Guardian("bark_knight", Rarity.Rare, 520, 30, 95, 12, 0.03f, "bark_slam", 3.2f, 12f, TargetPreference.Structure, GuardianSpecialKind.ShieldSelf, "special_shield", null, 200f, 6f, GuardianPassiveKind.ShieldOnHit, 0.1f, false, 0, true),
                Guardian("dew_fairy", Rarity.Rare, 260, 22, 25, 3, 0.05f, "dew_drop", 2.0f, 17f, TargetPreference.LowestHealth, GuardianSpecialKind.HealAllies, "special_heal", null, 140f, 0f, GuardianPassiveKind.HealOverTime, 3f, true, 0, true),
                Guardian("vine_master", Rarity.Rare, 380, 36, 80, 6, 0.05f, "vine_spear", 2.8f, 16f, TargetPreference.ExposedCore, GuardianSpecialKind.RootTarget, "special_root", "vine_spear", 1f, 3f, GuardianPassiveKind.ExtraStructureDamage, 0.15f, false, 1, true),
                Guardian("spore_alchemist", Rarity.Epic, 300, 28, 40, 4, 0.05f, "spore_bomb", 3.2f, 15f, TargetPreference.Random, GuardianSpecialKind.PoisonArea, "special_poison", "spore_cloud", 1f, 6f, GuardianPassiveKind.None, 0f, false, 1, true),
                Guardian("owl_scout", Rarity.Epic, 280, 46, 40, 3, 0.25f, "owl_feather", 2.4f, 22f, TargetPreference.LowestHealth, GuardianSpecialKind.PowerShot, "special_power_shot", "owl_feather", 2.5f, 0f, GuardianPassiveKind.CritBoost, 0.1f, true, 1, true),
                Guardian("oak_warden", Rarity.Epic, 700, 30, 60, 14, 0.02f, "acorn", 3.4f, 14f, TargetPreference.Nearest, GuardianSpecialKind.ArmorAllies, "special_armor", null, 8f, 8f, GuardianPassiveKind.ArmorAura, 3f, false, 1, true),
                Guardian("firefly_mage", Rarity.Epic, 260, 40, 30, 3, 0.08f, "light_seed", 2.6f, 18f, TargetPreference.LowestHealth, GuardianSpecialKind.ChainEnergy, "special_chain", "light_seed", 3f, 0f, GuardianPassiveKind.None, 0f, true, 2, false),
                Guardian("beaver_engineer", Rarity.Rare, 420, 32, 85, 7, 0.04f, "wood_chip", 2.6f, 14f, TargetPreference.Structure, GuardianSpecialKind.ReduceToolCooldowns, "special_tool_reset", null, 8f, 0f, GuardianPassiveKind.ToolCooldownReduction, 0.15f, false, 2, false),
                Guardian("hedgehog_sniper", Rarity.Legendary, 360, 30, 45, 6, 0.15f, "quill", 1.6f, 20f, TargetPreference.Nearest, GuardianSpecialKind.Salvo, "special_salvo", "quill", 6f, 0.25f, GuardianPassiveKind.CritBoost, 0.05f, false, 3, false),
                Guardian("ancient_sprout", Rarity.Legendary, 500, 38, 55, 8, 0.06f, "sprout_orb", 2.8f, 17f, TargetPreference.Nearest, GuardianSpecialKind.TeamBuff, "special_team_buff", null, 0.25f, 8f, GuardianPassiveKind.Rebirth, 1f, false, 3, false),
            };
        }

        // ------------------------------------------------------------------ tools
        static ToolDefinition Tool(string id, string sprite, Color accent, ToolEffectType effect, string proj, float magnitude, float duration, float radius, int count, float cd, bool aim, int arenaUnlock, bool playable)
        {
            var t = GetOrCreate<ToolDefinition>($"{SO}/Tools/Tool_{id}.asset");
            t.id = id; t.nameKey = "t_" + id + "_name"; t.descriptionKey = "t_" + id + "_desc"; t.icon = S(ArtPaths.Tool(sprite)); t.accentColor = accent;
            t.effect = effect; t.projectile = proj != null && projectiles.TryGetValue(proj, out var p) ? p : null;
            t.effectMagnitude = magnitude; t.effectDuration = duration; t.effectRadius = radius; t.projectileCount = count; t.cooldownSeconds = cd; t.requiresAim = aim;
            t.maxLevel = 10; t.baseUpgradeSap = 20; t.upgradeGrowth = 1.4f; t.magnitudePerLevel = 0.08f; t.cooldownReductionPerLevel = 0.02f;
            t.arenaUnlockIndex = arenaUnlock; t.playableInPrototype = playable;
            EditorUtility.SetDirty(t);
            return t;
        }

        static List<ToolDefinition> BuildTools()
        {
            return new List<ToolDefinition>
            {
                Tool("root_catapult", "catapult", new Color(0.65f, 0.5f, 0.35f), ToolEffectType.Projectile, "root_boulder", 120f, 0f, 1.8f, 1, 22f, true, 0, true),
                Tool("dew_sprayer", "dew", new Color(0.5f, 0.85f, 1f), ToolEffectType.HealArea, null, 180f, 0f, 2.5f, 1, 25f, true, 0, true),
                Tool("bee_hive", "hive", new Color(1f, 0.8f, 0.3f), ToolEffectType.HomingSwarm, "bee", 12f, 0f, 0f, 6, 28f, false, 0, true),
                Tool("wind_bell", "bell", new Color(0.75f, 0.9f, 1f), ToolEffectType.DeflectField, null, 0.35f, 5f, 0f, 1, 30f, false, 1, false),
                Tool("vine_net", "net", new Color(0.5f, 0.8f, 0.4f), ToolEffectType.SlowField, null, 0.5f, 5f, 2.5f, 1, 26f, true, 2, false),
                Tool("seed_shield", "seedshield", new Color(0.6f, 0.9f, 0.6f), ToolEffectType.Barrier, null, 250f, 8f, 0f, 1, 32f, true, 2, false),
            };
        }

        // ------------------------------------------------------------------ chests
        static ChestDefinition Chest(string id, ChestTier tier, Color glow, int cMin, int cMax, int sMin, int sMax, int gMin, int gMax, int draws, float[] weights, bool guaranteeEpic, float seconds)
        {
            var c = GetOrCreate<ChestDefinition>($"{SO}/Chests/Chest_{id}.asset");
            c.id = id; c.nameKey = "chest_" + id; c.tier = tier; c.glowColor = glow;
            // Art is authored on MainMenu > BottomBar > ChestSlots (ChestSlotsView); only fill placeholders when nothing is assigned yet.
            if (c.iconClosed == null) c.iconClosed = S(ArtPaths.Chest(id, false));
            if (c.iconOpen == null) c.iconOpen = S(ArtPaths.Chest(id, true));
            c.coinsMin = cMin; c.coinsMax = cMax; c.sapMin = sMin; c.sapMax = sMax; c.gemsMin = gMin; c.gemsMax = gMax;
            c.cardDraws = draws; c.rarityWeights = weights; c.guaranteeEpic = guaranteeEpic; c.epicPityThreshold = guaranteeEpic ? 0 : 12;
            c.unlockSeconds = seconds; c.arenaTierBonusPerIndex = 0.15f; c.gemSkipCostPerHour = 6;
            EditorUtility.SetDirty(c);
            return c;
        }

        static List<ChestDefinition> BuildChests()
        {
            return new List<ChestDefinition>
            {
                Chest("twig", ChestTier.Twig, new Color(1f, 0.85f, 0.4f), 40, 80, 2, 6, 0, 0, 3, new[] { 70f, 25f, 4.5f, 0.5f }, false, 900f),
                Chest("grove", ChestTier.Grove, new Color(0.6f, 1f, 0.5f), 120, 220, 8, 16, 0, 1, 5, new[] { 55f, 35f, 9f, 1f }, false, 10800f),
                Chest("ancient", ChestTier.Ancient, new Color(0.85f, 0.6f, 1f), 350, 600, 25, 45, 2, 6, 8, new[] { 35f, 40f, 22f, 3f }, true, 28800f),
                Chest("moon", ChestTier.Moon, new Color(0.6f, 0.8f, 1f), 600, 1000, 40, 80, 10, 20, 10, new[] { 20f, 40f, 32f, 8f }, true, 43200f),
                Chest("sun", ChestTier.Sun, new Color(1f, 0.62f, 0.2f), 1000, 1600, 70, 120, 20, 40, 12, new[] { 10f, 35f, 40f, 15f }, true, 86400f),
            };
        }

        // ------------------------------------------------------------------ arenas
        static ArenaDefinition Arena(string id, int index, int unlock, int power, Color skyTop, Color skyBottom, Color ground, Color ambient, Color fog,
            bool leaves, bool fireflies, bool mist, MusicTrackId music, int slots, BotDifficulty diff, string[] botGuardians, int botLevel, string[] botTools,
            TreeVisualTier botTier, int botTreeLevel, float rewardMult, ChestDrop[] drops, bool playable)
        {
            var a = GetOrCreate<ArenaDefinition>($"{SO}/Arenas/Arena_{index}_{id}.asset");
            a.id = id; a.nameKey = "arena_" + id; a.arenaIndex = index; a.unlockTrophies = unlock; a.recommendedPower = power; a.badge = S(ArtPaths.Icon("leaf")); a.fullyPlayable = playable;
            a.skyTop = skyTop; a.skyBottom = skyBottom; a.groundColor = ground; a.ambientTint = ambient; a.fogColor = fog;
            a.cloudsSprite = S(ArtPaths.Arena("clouds")); a.mountainsSprite = S(ArtPaths.Arena("mountains")); a.forestBackSprite = S(ArtPaths.Arena("forest_back"));
            a.forestMidSprite = S(ArtPaths.Arena("forest_mid")); a.groundSprite = S(ArtPaths.Arena("ground")); a.foregroundSprite = S(ArtPaths.Arena("foreground"));
            a.cloudSpeed = 0.15f; a.mountainParallax = 0.03f; a.forestBackParallax = 0.06f; a.forestMidParallax = 0.1f;
            a.leafParticles = leaves; a.fireflyParticles = fireflies; a.mist = mist; a.music = music;
            a.activeGuardianSlots = slots; a.botDifficulty = diff; a.botGuardianIds = botGuardians; a.botGuardianLevel = botLevel; a.botToolIds = botTools;
            a.botTreeTier = botTier; a.botTreeUpgradeLevel = botTreeLevel; a.rewardMultiplier = rewardMult; a.chestDrops = drops;
            EditorUtility.SetDirty(a);
            return a;
        }

        static ChestDrop D(string id, float w) => new ChestDrop { chestId = id, weight = w };

        static List<ArenaDefinition> BuildArenas()
        {
            return new List<ArenaDefinition>
            {
                Arena("sunny_grove", 0, 0, 400, new Color(0.42f, 0.72f, 0.98f), new Color(0.85f, 0.95f, 1f), new Color(0.42f, 0.66f, 0.32f), Color.white, new Color(1, 1, 1, 0f),
                    false, false, false, MusicTrackId.BattleSunny, 4, BotDifficulty.Easy, new[] { "thorn_archer", "cone_bomber", "bark_knight", "dew_fairy" }, 1, new[] { "root_catapult", "dew_sprayer" },
                    TreeVisualTier.Sprouting, 0, 1f, new[] { D("twig", 70f), D("grove", 30f) }, true),
                Arena("misty_swamp", 1, 60, 700, new Color(0.36f, 0.44f, 0.5f), new Color(0.6f, 0.74f, 0.66f), new Color(0.3f, 0.46f, 0.34f), new Color(0.85f, 0.95f, 0.9f), new Color(0.75f, 0.9f, 0.8f, 0.4f),
                    false, false, true, MusicTrackId.BattleSwamp, 5, BotDifficulty.Normal, new[] { "thorn_archer", "vine_master", "spore_alchemist", "bark_knight", "cone_bomber" }, 4, new[] { "root_catapult", "bee_hive" },
                    TreeVisualTier.Sprouting, 3, 1.2f, new[] { D("twig", 40f), D("grove", 50f), D("ancient", 10f) }, true),
                Arena("crimson_autumn", 2, 150, 1100, new Color(0.96f, 0.62f, 0.36f), new Color(1f, 0.86f, 0.62f), new Color(0.62f, 0.42f, 0.22f), new Color(1f, 0.92f, 0.8f), new Color(1, 1, 1, 0f),
                    true, false, false, MusicTrackId.BattleAutumn, 6, BotDifficulty.Hard, new[] { "owl_scout", "oak_warden", "vine_master", "spore_alchemist", "thorn_archer", "cone_bomber" }, 8, new[] { "root_catapult", "dew_sprayer", "bee_hive" },
                    TreeVisualTier.Strong, 8, 1.5f, new[] { D("grove", 60f), D("ancient", 40f) }, false),
                Arena("moonlit_forest", 3, 300, 1600, new Color(0.08f, 0.1f, 0.26f), new Color(0.2f, 0.26f, 0.46f), new Color(0.16f, 0.26f, 0.32f), new Color(0.7f, 0.8f, 1f), new Color(0.5f, 0.6f, 0.9f, 0.3f),
                    false, true, true, MusicTrackId.BattleMoon, 7, BotDifficulty.Hard, new[] { "owl_scout", "oak_warden", "firefly_mage", "hedgehog_sniper", "vine_master", "spore_alchemist", "bark_knight" }, 12, new[] { "root_catapult", "dew_sprayer", "bee_hive" },
                    TreeVisualTier.Ancient, 14, 2f, new[] { D("ancient", 62f), D("moon", 30f), D("sun", 8f) }, false),
            };
        }

        // ------------------------------------------------------------------ quests
        static QuestDefinition Quest(string id, QuestType type, int target, bool daily, RewardBundle reward, string icon)
        {
            var q = GetOrCreate<QuestDefinition>($"{SO}/Quests/Quest_{id}.asset");
            q.id = id; q.titleKey = "q_" + id + "_title"; q.descriptionKey = "q_" + id + "_desc"; q.type = type; q.targetCount = target; q.isDaily = daily; q.reward = reward; q.icon = S(ArtPaths.Icon(icon));
            EditorUtility.SetDirty(q);
            return q;
        }

        static List<QuestDefinition> BuildQuests()
        {
            return new List<QuestDefinition>
            {
                Quest("play_1", QuestType.PlayBattles, 1, true, R(100), "battle"),
                Quest("win_1", QuestType.WinBattles, 1, false, R(150, 5), "trophy"),
                Quest("bark_3", QuestType.BreakBarkSections, 3, true, R(80), "shield"),
                Quest("upgrade_1", QuestType.UpgradeGuardian, 1, false, R(0, 10), "arrow_up"),
                Quest("chest_1", QuestType.OpenChest, 1, false, R(60), "cards"),
                Quest("special_500", QuestType.SpecialDamage, 500, false, R(120, 0, 2), "bolt"),
            };
        }

        static AchievementDefinition Achievement(string id, QuestType metric, int target, RewardBundle reward, string icon)
        {
            var a = GetOrCreate<AchievementDefinition>($"{SO}/Quests/Achievement_{id}.asset");
            a.id = id; a.titleKey = "a_" + id + "_title"; a.descriptionKey = "a_" + id + "_desc"; a.metric = metric; a.targetCount = target; a.reward = reward; a.icon = S(ArtPaths.Icon(icon));
            EditorUtility.SetDirty(a);
            return a;
        }

        static List<AchievementDefinition> BuildAchievements()
        {
            return new List<AchievementDefinition>
            {
                Achievement("first_victory", QuestType.WinBattles, 1, R(0, 0, 5), "trophy"),
                Achievement("marksman", QuestType.SpecialDamage, 5000, R(300, 20), "target"),
                Achievement("branch_breaker", QuestType.BreakBranches, 10, R(250, 10), "branch"),
                Achievement("collector", QuestType.UpgradeGuardian, 20, R(0, 0, 10), "cards"),
                Achievement("ancient_tree", QuestType.PlayBattles, 50, R(500, 50, 10), "tree"),
            };
        }

        static DailyRewardDefinition BuildDailyRewards()
        {
            var d = GetOrCreate<DailyRewardDefinition>($"{SO}/Quests/DailyRewards.asset");
            d.days = new List<RewardBundle> { R(100), R(0, 5), R(150), R(chest: "twig"), R(0, 10), R(0, 0, 3), R(chest: "grove") };
            EditorUtility.SetDirty(d);
            return d;
        }

        // ------------------------------------------------------------------ trees
        static TreeSectionSpec Sec(string id, TreeSectionType type, float hp, float armor, string parent, Vector2 pos, Vector2 size, int slot = -1, string[] protects = null, bool targetable = true)
        {
            return new TreeSectionSpec
            {
                sectionId = id, type = type, baseHealth = hp, armor = armor, parentSectionId = parent ?? "", localPosition = pos, size = size,
                guardianSlotIndex = slot, protectsSectionIds = protects ?? Array.Empty<string>(), initiallyTargetable = targetable,
                breakBehavior = BranchBreakBehavior.FallToEmptySlot
            };
        }

        static TreeDefinition BuildTree(string id, string path, string nameKey)
        {
            var t = GetOrCreate<TreeDefinition>(path);
            t.id = id; t.nameKey = nameKey;
            t.heartwoodBaseHealth = 1200f; t.heartwoodArmor = 10f; t.protectedDamageMultiplier = 0.15f;
            t.sections = new List<TreeSectionSpec>
            {
                Sec("core", TreeSectionType.HeartwoodCore, 1200, 10, "", new Vector2(0f, 3.4f), new Vector2(1.1f, 1.1f), -1, null, false),
                Sec("trunk_low", TreeSectionType.Trunk, 520, 8, "", new Vector2(0f, 1.3f), new Vector2(1.9f, 2.6f)),
                Sec("trunk_mid", TreeSectionType.Trunk, 460, 8, "trunk_low", new Vector2(0f, 3.5f), new Vector2(1.7f, 2.4f), -1, new[] { "core" }),
                Sec("trunk_top", TreeSectionType.Trunk, 400, 6, "trunk_mid", new Vector2(0f, 5.6f), new Vector2(1.5f, 2.2f)),
                Sec("bark_low", TreeSectionType.BarkArmor, 220, 4, "trunk_low", new Vector2(1.15f, 1.4f), new Vector2(0.9f, 1.6f), -1, new[] { "trunk_low" }),
                Sec("bark_mid", TreeSectionType.BarkArmor, 200, 4, "trunk_mid", new Vector2(1.05f, 3.4f), new Vector2(0.9f, 1.6f), -1, new[] { "core", "trunk_mid" }),
                Sec("bark_top", TreeSectionType.BarkArmor, 180, 3, "trunk_top", new Vector2(0.95f, 5.4f), new Vector2(0.8f, 1.4f), -1, new[] { "trunk_top" }),
                Sec("branch_low", TreeSectionType.Branch, 260, 3, "trunk_low", new Vector2(2.1f, 2.3f), new Vector2(2.4f, 0.55f), 0),
                Sec("branch_mid", TreeSectionType.Branch, 240, 3, "trunk_mid", new Vector2(2.2f, 4.2f), new Vector2(2.4f, 0.55f), 1),
                Sec("branch_high", TreeSectionType.Branch, 220, 3, "trunk_top", new Vector2(2.0f, 6.1f), new Vector2(2.2f, 0.5f), 2),
                Sec("branch_top", TreeSectionType.Branch, 200, 2, "trunk_top", new Vector2(1.3f, 7.6f), new Vector2(1.8f, 0.5f), 3),
                Sec("branch_back_low", TreeSectionType.Branch, 260, 3, "trunk_low", new Vector2(-1.9f, 2.7f), new Vector2(2.2f, 0.55f), 4),
                Sec("branch_back_mid", TreeSectionType.Branch, 240, 3, "trunk_mid", new Vector2(-2.0f, 4.6f), new Vector2(2.2f, 0.55f), 5),
                Sec("branch_back_high", TreeSectionType.Branch, 220, 3, "trunk_top", new Vector2(-1.7f, 6.4f), new Vector2(2.0f, 0.5f), 6),
                Sec("crown", TreeSectionType.Branch, 200, 2, "trunk_top", new Vector2(-0.2f, 8.3f), new Vector2(1.6f, 0.5f), 7),
                Sec("canopy", TreeSectionType.CanopyShield, 300, 2, "trunk_top", new Vector2(0f, 7.4f), new Vector2(3.4f, 1.8f)),
                Sec("root", TreeSectionType.RootStabilizer, 400, 10, "", new Vector2(0f, 0.15f), new Vector2(3.2f, 1.0f)),
            };
            t.guardianSlotPositions = new[]
            {
                new Vector2(3.0f, 2.75f), new Vector2(3.1f, 4.65f), new Vector2(2.8f, 6.5f), new Vector2(1.9f, 8.0f),
                new Vector2(-2.7f, 3.15f), new Vector2(-2.8f, 5.05f), new Vector2(-2.4f, 6.8f), new Vector2(-0.4f, 8.75f)
            };
            t.toolMountPositions = new[] { new Vector2(1.6f, 0.7f), new Vector2(-1.6f, 0.7f), new Vector2(0.9f, 9.2f) };
            t.visuals = new[]
            {
                new TreeVisualSet { tier = TreeVisualTier.Sprouting, leafColor = new Color(0.58f, 0.82f, 0.46f), barkColor = new Color(0.66f, 0.5f, 0.34f) },
                new TreeVisualSet { tier = TreeVisualTier.Strong, leafColor = new Color(0.38f, 0.7f, 0.34f), barkColor = new Color(0.55f, 0.4f, 0.26f) },
                new TreeVisualSet { tier = TreeVisualTier.Ancient, leafColor = new Color(0.26f, 0.56f, 0.36f), barkColor = new Color(0.42f, 0.3f, 0.2f) },
            };
            foreach (var v in t.visuals)
            {
                v.trunk = S(ArtPaths.Tree("trunk", 0)); v.branch = S(ArtPaths.Tree("branch", 0)); v.bark = S(ArtPaths.Tree("bark", 0));
                v.canopy = S(ArtPaths.TreePart("canopy")); v.root = S(ArtPaths.TreePart("root")); v.heartwood = S(ArtPaths.TreePart("heartwood"));
            }
            EditorUtility.SetDirty(t);
            return t;
        }

        // ------------------------------------------------------------------ misc
        static RarityPalette BuildRarityPalette()
        {
            var p = GetOrCreate<RarityPalette>(RarityPalettePath);
            p.frameColors = new[] { new Color(0.62f, 0.66f, 0.62f), new Color(0.27f, 0.6f, 0.95f), new Color(0.63f, 0.36f, 0.9f), new Color(0.98f, 0.72f, 0.2f) };
            p.cornerIcons = new[] { S(ArtPaths.RarityIcon("common")), S(ArtPaths.RarityIcon("rare")), S(ArtPaths.RarityIcon("epic")), S(ArtPaths.RarityIcon("legendary")) };
            p.nameKeys = new[] { "rarity_common", "rarity_rare", "rarity_epic", "rarity_legendary" };
            EditorUtility.SetDirty(p);
            return p;
        }

        static AudioLibrary BuildAudioLibrary()
        {
            var lib = GetOrCreate<AudioLibrary>(AudioLibraryPath);
            if (lib.sfx == null) lib.sfx = new List<SfxEntry>();
            foreach (AudioEventId id in Enum.GetValues(typeof(AudioEventId)))
            {
                if (id == AudioEventId.None) continue;
                if (lib.sfx.Exists(e => e != null && e.id == id)) continue;
                bool ui = id == AudioEventId.UiClick || id == AudioEventId.UiPanelOpen || id == AudioEventId.UiPanelClose || id == AudioEventId.UiError || id == AudioEventId.RewardPop || id == AudioEventId.CoinCount;
                lib.sfx.Add(new SfxEntry { id = id, clips = Array.Empty<AudioClip>(), volume = 1f, pitchVariance = 0.05f, isUi = ui });
            }
            if (lib.music == null) lib.music = new List<MusicEntry>();
            foreach (MusicTrackId id in Enum.GetValues(typeof(MusicTrackId)))
            {
                if (id == MusicTrackId.None) continue;
                if (lib.music.Exists(e => e != null && e.id == id)) continue;
                lib.music.Add(new MusicEntry { id = id, clip = null, volume = 0.8f });
            }
            EditorUtility.SetDirty(lib);
            return lib;
        }

        static LocalizationTable BuildLocalization()
        {
            var t = GetOrCreate<LocalizationTable>(LocalizationPath);
            var existing = new Dictionary<string, LocalizationEntry>();
            foreach (var e in t.entries) if (e != null && !string.IsNullOrEmpty(e.key)) existing[e.key] = e;
            var list = new List<LocalizationEntry>();
            foreach (var row in LocalizationSeed.Entries)
            {
                if (!existing.TryGetValue(row[0], out var e)) e = new LocalizationEntry { key = row[0] };
                e.en = row[1];
                e.tr = row[2];
                list.Add(e);
                existing.Remove(row[0]);
            }
            foreach (var kv in existing) list.Add(kv.Value);
            t.entries = list;
            t.loadingTipKeys = new List<string>(LocalizationSeed.LoadingTips);
            EditorUtility.SetDirty(t);
            return t;
        }
    }
}
