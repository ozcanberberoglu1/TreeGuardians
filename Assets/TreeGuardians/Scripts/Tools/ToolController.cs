using System.Collections.Generic;
using TreeGuardians.Audio;
using TreeGuardians.Battle;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Guardians;
using TreeGuardians.Trees;
using UnityEngine;

namespace TreeGuardians.Tools
{
    /// One side's three tool mounts with cooldowns and effects. Same rules for player and bot.
    public sealed class ToolController : MonoBehaviour
    {
        public struct ToolSlot
        {
            public ToolDefinition def;
            public int level;
            public float cooldown;
            public float cooldownDuration;
            public bool IsReady => def != null && cooldown <= 0f;
            public float Percent => cooldownDuration > 0f ? Mathf.Clamp01(1f - cooldown / cooldownDuration) : 1f;
        }

        [SerializeField] BattleSide side = BattleSide.Player;
        [SerializeField] Transform[] mounts = new Transform[3];

        readonly ToolSlot[] slots = new ToolSlot[3];
        readonly List<GuardianController> tmpGuardians = new List<GuardianController>(8);
        readonly List<TreeSection> tmpSections = new List<TreeSection>(32);
        BattleContext ctx;
        TreeController tree;
        float deflectUntil;
        int deflectTurn = int.MinValue, shieldTurn = int.MinValue;
        TreeSection shieldSection;
        float deflectStrength;
        float slowUntil;
        Vector2 slowCenter;
        float slowRadius;
        float slowStrength;
        float cooldownMultiplier = 1f;

        public BattleSide Side => side;
        public int Count => slots.Length;
        bool TurnMode => ctx != null && ctx.turnBased && ctx.turns != null;

        /// Turn mode: a tool cast on turn T protects through the opponent's next turn (T+1); real time keeps the seconds timer.
        public float DeflectStrength => (Time.time < deflectUntil || (TurnMode && ctx.turns.TurnIndex <= deflectTurn + 1)) ? deflectStrength : 0f;

        public void Initialize(string[] ids, int[] levels, BattleContext context, TreeController ownTree)
        {
            ctx = context;
            tree = ownTree;
            deflectUntil = 0f;
            deflectTurn = shieldTurn = int.MinValue;
            shieldSection = null;
            slowUntil = 0f;
            for (int i = 0; i < slots.Length; i++)
            {
                var def = ids != null && i < ids.Length ? ctx.database.GetTool(ids[i]) : null;
                int level = levels != null && i < levels.Length && levels[i] > 0 ? levels[i] : 1;
                slots[i] = new ToolSlot { def = def, level = level, cooldownDuration = def != null ? def.GetCooldown(level) : 0f, cooldown = def != null ? def.GetCooldown(level) * 0.35f : 0f };
                if (def != null && def.projectile != null) ctx.projectiles?.Prewarm(def.projectile);
            }
        }

        public ToolSlot Get(int i) => i >= 0 && i < slots.Length ? slots[i] : default;

        public void Tick(float dt)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].def != null && slots[i].cooldown > 0f) slots[i].cooldown = Mathf.Max(0f, slots[i].cooldown - dt);
            if (shieldSection != null && TurnMode && ctx.turns.TurnIndex > shieldTurn + 1) { shieldSection.ClearShield(); shieldSection = null; }
        }

        public bool CanUse(int i) => i >= 0 && i < slots.Length && slots[i].IsReady;

        public bool RequiresAim(int i) => CanUse(i) && slots[i].def.requiresAim;

        public Vector2 MountPosition(int i) => i >= 0 && i < mounts.Length && mounts[i] != null ? (Vector2)mounts[i].position : (Vector2)transform.position;

        public bool Use(int i, Vector2 target)
        {
            if (!CanUse(i)) return false;
            var def = slots[i].def;
            float magnitude = def.GetMagnitude(slots[i].level);
            Vector2 mount = MountPosition(i);
            bool used;
            switch (def.effect)
            {
                case ToolEffectType.Projectile:
                {
                    if (def.projectile == null) return false;
                    var velocity = ctx.projectiles.LaunchVelocity(def.projectile, mount, target, 1f);
                    float mult = def.projectile.baseDamage > 0f ? magnitude / def.projectile.baseDamage : 1f;
                    used = ctx.projectiles.Fire(def.projectile, mount, velocity, side, null, false, true, mult, null) != null;
                    break;
                }
                case ToolEffectType.HealArea:
                {
                    Vector2 center = ctx.targeting != null && ctx.targeting.SideAt(target) == side ? target : (tree != null && tree.Core != null ? (Vector2)tree.Core.transform.position : mount);
                    ctx.damage.HealArea(center, def.effectRadius, magnitude, side);
                    Services.Get<AudioService>()?.PlaySfxAt(AudioEventId.Heal, center);
                    used = true;
                    break;
                }
                case ToolEffectType.HomingSwarm:
                {
                    if (def.projectile == null) return false;
                    var roster = ctx.RosterOf(BattleContext.Opponent(side));
                    if (roster != null) roster.GetAlive(tmpGuardians); else tmpGuardians.Clear();
                    var enemyTree = ctx.TreeOf(BattleContext.Opponent(side));
                    if (enemyTree != null) enemyTree.GetAliveSections(tmpSections, true); else tmpSections.Clear();
                    int count = Mathf.Max(1, def.projectileCount);
                    float mult = def.projectile.baseDamage > 0f ? magnitude / def.projectile.baseDamage : 1f;
                    for (int k = 0; k < count; k++)
                    {
                        Collider2D homing = null;
                        if (tmpGuardians.Count > 0) homing = tmpGuardians[ctx.rng.Next(tmpGuardians.Count)].BodyCollider;
                        else if (tmpSections.Count > 0) homing = tmpSections[ctx.rng.Next(tmpSections.Count)].Collider;
                        float ang = (side == BattleSide.Player ? 60f : 120f) + ctx.Range(-25f, 25f);
                        var dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
                        ctx.projectiles.Fire(def.projectile, mount + dir * 0.2f * k, dir * def.projectile.speed, side, null, false, true, mult, homing);
                    }
                    used = true;
                    break;
                }
                case ToolEffectType.DeflectField:
                    deflectUntil = Time.time + def.effectDuration;
                    if (TurnMode) deflectTurn = ctx.turns.TurnIndex;
                    deflectStrength = magnitude;
                    ctx.vfx?.Burst(tree != null && tree.Core != null ? (Vector2)tree.Core.transform.position : mount, new Color(0.7f, 0.9f, 1f), 2.2f);
                    used = true;
                    break;
                case ToolEffectType.SlowField:
                    slowUntil = Time.time + def.effectDuration;
                    slowCenter = target;
                    slowRadius = def.effectRadius;
                    slowStrength = Mathf.Clamp01(magnitude);
                    if (TurnMode)
                    {
                        // Turn mode has no cooldown race, so the net roots enemy guardians inside it for part of their next turn.
                        ctx.RosterOf(BattleContext.Opponent(side))?.GetAlive(tmpGuardians);
                        for (int k = 0; k < tmpGuardians.Count; k++)
                            if (Vector2.Distance(tmpGuardians[k].transform.position, target) <= def.effectRadius)
                                tmpGuardians[k].ApplyStatus(new StatusEffectSpec { type = StatusEffectType.Root, duration = Mathf.Max(2.5f, def.effectDuration * 0.5f), magnitude = 1f });
                    }
                    ctx.vfx?.Poison(target, def.effectRadius);
                    used = true;
                    break;
                case ToolEffectType.Barrier:
                {
                    if (tree == null) return false;
                    tree.GetAliveSections(tmpSections, false);
                    TreeSection best = null;
                    float bd = float.MaxValue;
                    for (int k = 0; k < tmpSections.Count; k++)
                    {
                        float d = Vector2.Distance(target, tmpSections[k].transform.position);
                        if (d < bd) { bd = d; best = tmpSections[k]; }
                    }
                    if (best == null) return false;
                    if (TurnMode)
                    {
                        shieldSection?.ClearShield();
                        best.AddShield(magnitude, 1e6f);
                        shieldSection = best;
                        shieldTurn = ctx.turns.TurnIndex;
                    }
                    else best.AddShield(magnitude, def.effectDuration);
                    ctx.vfx?.Burst(best.transform.position, new Color(0.6f, 1f, 0.6f), 1.4f);
                    used = true;
                    break;
                }
                default:
                    used = false;
                    break;
            }
            if (!used) return false;
            slots[i].cooldownDuration = def.GetCooldown(slots[i].level) * cooldownMultiplier;
            slots[i].cooldown = slots[i].cooldownDuration;
            ctx.StatsOf(side).toolsUsed++;
            Services.Get<AudioService>()?.PlaySfxAt(def.useSfx, mount);
            GameEventBus.Publish(new ToolUsedEvent { toolId = def.id, side = side });
            return true;
        }

        public bool IsSlowedAt(Vector2 position, out float strength)
        {
            strength = 0f;
            if (Time.time >= slowUntil) return false;
            if (Vector2.Distance(position, slowCenter) > slowRadius) return false;
            strength = slowStrength;
            return true;
        }

        public void ReduceCooldowns(float seconds)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].def != null) slots[i].cooldown = Mathf.Max(0f, slots[i].cooldown - seconds);
        }

        public void SetCooldownMultiplier(float m) => cooldownMultiplier = Mathf.Clamp(m, 0.3f, 1f);
    }
}
