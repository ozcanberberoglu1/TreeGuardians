using System;
using TreeGuardians.Core;
using TreeGuardians.Data;
using UnityEngine;

namespace TreeGuardians.Battle
{
    public enum BattleCommandType { SelectGuardian = 0, FireNormal = 1, FireSpecial = 2, UseTool = 3 }

    /// One unit of intent from a commander (player input or bot). Executed by BattleManager under the same rules for both sides.
    public struct BattleCommand
    {
        public BattleCommandType type;
        public BattleSide side;
        public int slotIndex;
        public Vector2 direction;
        public float power;
        public Vector2 targetPoint;

        public static BattleCommand Select(BattleSide side, int slot) => new BattleCommand { type = BattleCommandType.SelectGuardian, side = side, slotIndex = slot };
        public static BattleCommand Fire(BattleSide side, int slot, Vector2 dir, float power, bool special) => new BattleCommand { type = special ? BattleCommandType.FireSpecial : BattleCommandType.FireNormal, side = side, slotIndex = slot, direction = dir, power = power };
        public static BattleCommand Tool(BattleSide side, int toolIndex, Vector2 target) => new BattleCommand { type = BattleCommandType.UseTool, side = side, slotIndex = toolIndex, targetPoint = target };
    }

    /// Player input and bot AI both implement this; BattleManager drains commands each tick.
    public interface IBattleCommander
    {
        BattleSide Side { get; }
        void Initialize(BattleContext context);
        void Tick(float dt);
        bool TryDequeue(out BattleCommand command);
        void OnBattleEnded();
    }

    public struct DamageInfo
    {
        public float amount;
        public BattleSide source;
        public bool isSpecial;
        public bool isTool;
        public bool isSplash;
        public bool isCrit;
        public StatusEffectSpec status;
        public Vector2 hitPoint;
        public int sourceSlot;
        [Tooltip("Kale duvarında açılacak delik yarıçapı (0 = delik yok).")] public float holeRadius;
        /// Travel direction of the projectile (debris flies back against it).
        public Vector2 direction;
    }

    [Serializable]
    public sealed class BattleStats
    {
        public float damageDealt;
        public float structureDamage;
        public float specialDamage;
        public int shotsFired;
        public int shotsHit;
        public int barkBroken;
        public int branchesBroken;
        public int sectionsBroken;
        public int toolsUsed;
        public int guardiansDefeated;
    }

    /// Shared runtime state for one battle. Systems receive it once in Initialize.
    public sealed class BattleContext
    {
        public BattleSetup setup;
        public GameBalanceConfig balance;
        public GameDatabase database;
        public System.Random rng;
        public float timeRemaining;
        public bool isPlaying;
        public bool isPaused;
        public float elapsed;
        /// Castle duel: sides alternate single shots (see BattleTurnController).
        public bool turnBased;
        public BattleTurnController turns;

        public Trees.TreeController playerTree;
        public Trees.TreeController enemyTree;
        public Guardians.GuardianRoster playerRoster;
        public Guardians.GuardianRoster enemyRoster;
        public Tools.ToolController playerTools;
        public Tools.ToolController enemyTools;
        public ProjectileService projectiles;
        public BattleVFX vfx;
        public TargetingController targeting;
        public DamageResolver damage;

        public readonly BattleStats playerStats = new BattleStats();
        public readonly BattleStats enemyStats = new BattleStats();

        public float enemyStructureTotalAtStart;

        public Trees.TreeController TreeOf(BattleSide side) => side == BattleSide.Player ? playerTree : enemyTree;
        public Guardians.GuardianRoster RosterOf(BattleSide side) => side == BattleSide.Player ? playerRoster : enemyRoster;
        public Tools.ToolController ToolsOf(BattleSide side) => side == BattleSide.Player ? playerTools : enemyTools;
        public BattleStats StatsOf(BattleSide side) => side == BattleSide.Player ? playerStats : enemyStats;
        public static BattleSide Opponent(BattleSide side) => side == BattleSide.Player ? BattleSide.Enemy : BattleSide.Player;

        public float NextFloat() => (float)rng.NextDouble();
        public float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
