using System.Collections.Generic;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Guardians;
using TreeGuardians.Trees;
using UnityEngine;

namespace TreeGuardians.Battle
{
    /// Single place where armor, protection links, root reduction and splash falloff are applied.
    public sealed class DamageResolver
    {
        readonly BattleContext ctx;
        readonly List<Collider2D> overlap = new List<Collider2D>(24);
        ContactFilter2D filter;

        public DamageResolver(BattleContext context)
        {
            ctx = context;
            filter = new ContactFilter2D { useTriggers = true, useLayerMask = false };
        }

        public static float ArmorReduce(float raw, float armor) => raw * 100f / (100f + Mathf.Max(0f, armor));

        static readonly Color CastleDebris = new Color(0.55f, 0.4f, 0.28f);

        static void Shake(float trauma) => GameEventBus.Publish(new ScreenShakeEvent { amplitude = trauma, duration = 0.3f });
        static void HitStop(float seconds) => GameEventBus.Publish(new HitStopEvent { seconds = seconds });

        /// Opens a hole (with debris) in every destructible castle that has a wall part at the point.
        public void CarveAt(Vector2 point, float radius, BattleSide source, bool hitOwnSide, Vector2 direction = default)
        {
            if (radius <= 0f) return;
            CarveOn(ctx.playerTree, point, radius, source, hitOwnSide, direction);
            CarveOn(ctx.enemyTree, point, radius, source, hitOwnSide, direction);
        }

        void CarveOn(TreeSection s, Vector2 point, float radius, Vector2 direction = default, bool crit = false)
        {
            var tree = s.Tree;
            if (tree == null || !tree.IsDestructible || s.Type == TreeSectionType.RootStabilizer) return;
            float armorFactor = Mathf.Clamp(100f / (100f + s.Armor), 0.6f, 1f);
            tree.Carve(point, radius * armorFactor);
            ctx.vfx?.WallImpact(point, direction, radius * armorFactor, crit);
            Services.Get<Audio.AudioService>()?.PlaySfxAt(AudioEventId.Debris, point, 0.6f);
            if (ctx.NextFloat() < ctx.balance.holeSmokeChance) ctx.vfx?.AddSmokeSource(point, ctx.Range(ctx.balance.holeSmokeSecondsMin, ctx.balance.holeSmokeSecondsMax));
        }

        /// Wall damage + a smaller hole where a projectile hit a guardian through an existing hole.
        public void ChipWall(TreeController tree, Vector2 point, float radius, float rawStructure, DamageInfo info)
        {
            if (tree == null || !tree.IsDestructible || radius <= 0f) return;
            for (int i = 0; i < tree.Sections.Count; i++)
            {
                var s = tree.Sections[i];
                if (s == null || s.IsDestroyed || s.Type == TreeSectionType.RootStabilizer) continue;
                if (!s.WorldBounds.Contains(new Vector3(point.x, point.y, s.WorldBounds.center.z))) continue;
                var hi = info;
                hi.hitPoint = point;
                hi.holeRadius = radius;
                if (rawStructure > 0f) HitSection(s, rawStructure, hi);
                else CarveOn(s, point, radius, info.direction);
                return;
            }
        }

        void CarveOn(TreeController tree, Vector2 point, float radius, BattleSide source, bool hitOwnSide, Vector2 direction)
        {
            if (tree == null || !tree.IsDestructible) return;
            if (tree.Side == source && !hitOwnSide) return;
            for (int i = 0; i < tree.Sections.Count; i++)
            {
                var s = tree.Sections[i];
                if (s == null || s.IsDestroyed || s.Type == TreeSectionType.RootStabilizer) continue;
                if (!s.WorldBounds.Contains(new Vector3(point.x, point.y, s.WorldBounds.center.z))) continue;
                CarveOn(s, point, radius, direction);
                return;
            }
        }

        public float HitSection(TreeSection s, float raw, in DamageInfo info)
        {
            if (s == null || s.IsDestroyed || raw <= 0f) return 0f;
            float dmg = ArmorReduce(raw, s.Armor);
            if (s.IsProtected && s.Tree != null && s.Tree.Definition != null) dmg *= s.Tree.Definition.protectedDamageMultiplier;
            if (info.isSplash && s.Tree != null) dmg *= 1f - s.Tree.RootDamageReduction;
            if (info.status.type == StatusEffectType.Poison) dmg *= 1f - s.PoisonResistance;
            bool wasAlive = !s.IsDestroyed;
            float applied = s.ApplyDamage(dmg, info);
            if (applied <= 0f && dmg > 0f) Services.Get<Audio.AudioService>()?.PlaySfxAt(AudioEventId.ShieldBlock, info.hitPoint == Vector2.zero ? (Vector2)s.transform.position : info.hitPoint);
            if (applied > 0f)
            {
                var stats = ctx.StatsOf(info.source);
                stats.damageDealt += applied;
                if (s.Type != TreeSectionType.HeartwoodCore) stats.structureDamage += applied;
                if (info.isSpecial) { stats.specialDamage += applied; GameEventBus.Publish(new SpecialDamageEvent { amount = applied, side = info.source }); }
                GameEventBus.Publish(new SectionDamagedEvent { side = s.Side, amount = applied, isCore = s.Type == TreeSectionType.HeartwoodCore });
                ctx.vfx?.Number(info.hitPoint == Vector2.zero ? (Vector2)s.transform.position : info.hitPoint, applied, info.isCrit ? BattleVFX.NumberStyle.Crit : BattleVFX.NumberStyle.Structure);
                if (info.holeRadius > 0f) CarveOn(s, info.hitPoint == Vector2.zero ? (Vector2)s.transform.position : info.hitPoint, info.holeRadius, info.direction, info.isCrit);
                if (info.isCrit) { HitStop(0.05f); Shake(0.45f); Services.Get<Audio.AudioService>()?.PlaySfxAt(AudioEventId.CritHit, info.hitPoint); }
                else if (applied >= s.MaxHealth * 0.25f) HitStop(0.035f);
                if (wasAlive && s.IsDestroyed)
                {
                    if (s.Type == TreeSectionType.BarkArmor) stats.barkBroken++;
                    else if (s.Type == TreeSectionType.Branch) stats.branchesBroken++;
                    stats.sectionsBroken++;
                    if (s.Tree == null || !s.Tree.IsDestructible)
                        ctx.vfx?.Shards(s.transform.position, s.Type == TreeSectionType.HeartwoodCore ? 14 : 7, s.Type == TreeSectionType.CanopyShield ? new Color(0.5f, 0.8f, 0.4f) : CastleDebris);
                    Shake(s.Type == TreeSectionType.HeartwoodCore ? 0.9f : 0.65f);
                    HitStop(0.07f);
                    Services.Get<Audio.AudioService>()?.PlaySfxAt(AudioEventId.BranchBreak, s.transform.position); // wall part break
                    if (s.Side == BattleSide.Player) Services.Get<Audio.HapticService>()?.Medium();
                }
            }
            return applied;
        }

        public float HitGuardian(GuardianController g, float raw, in DamageInfo info, bool silent = false)
        {
            if (g == null || !g.IsAlive || raw <= 0f) return 0f;
            float dmg = ArmorReduce(raw, g.Armor + g.BuffArmorBonus + g.AuraArmorBonus);
            bool wasAlive = g.IsAlive;
            float applied = g.ApplyDamage(dmg, info);
            if (applied <= 0f && dmg > 0f && !silent) Services.Get<Audio.AudioService>()?.PlaySfxAt(AudioEventId.ShieldBlock, g.transform.position);
            if (info.status.type != StatusEffectType.None) g.ApplyStatus(info.status);
            if (applied > 0f)
            {
                var stats = ctx.StatsOf(info.source);
                stats.damageDealt += applied;
                if (info.isSpecial) { stats.specialDamage += applied; GameEventBus.Publish(new SpecialDamageEvent { amount = applied, side = info.source }); }
                if (!silent) ctx.vfx?.Number(info.hitPoint == Vector2.zero ? (Vector2)g.transform.position : info.hitPoint, applied, info.isCrit ? BattleVFX.NumberStyle.Crit : BattleVFX.NumberStyle.Guardian);
                bool killed = wasAlive && !g.IsAlive;
                GameEventBus.Publish(new GuardianDamagedEvent { side = g.Side, amount = applied, killed = killed });
                if (!silent)
                {
                    if (killed) { HitStop(0.06f); Shake(0.35f); }
                    else if (info.isCrit) { HitStop(0.05f); Shake(0.4f); Services.Get<Audio.AudioService>()?.PlaySfxAt(AudioEventId.CritHit, info.hitPoint); }
                    else Shake(0.2f);
                    if (!killed) Services.Get<Audio.AudioService>()?.PlaySfxAt(g.Definition.hurtSfx, info.hitPoint == Vector2.zero ? (Vector2)g.transform.position : info.hitPoint);
                }
            }
            return applied;
        }

        /// Area damage with linear falloff. Own side is skipped unless hitOwnSide.
        public void Splash(Vector2 center, float radius, float falloff, float rawGuardian, float rawStructure, DamageInfo info, bool hitOwnSide)
        {
            if (radius <= 0f || ctx.targeting == null) return;
            overlap.Clear();
            Physics2D.OverlapCircle(center, radius, filter, overlap);
            info.isSplash = true;
            for (int i = 0; i < overlap.Count; i++)
            {
                var c = overlap[i];
                if (c == null) continue;
                float d = Vector2.Distance(center, c.bounds.ClosestPoint(center));
                float mult = Mathf.Lerp(1f, falloff, Mathf.Clamp01(d / radius));
                if (ctx.targeting.TryGetSection(c, out var section))
                {
                    if (section.Side == info.source && !hitOwnSide) continue;
                    var hi = info; hi.hitPoint = c.bounds.center; hi.holeRadius = 0f; // the splash carves once at its center (CarveAt)
                    HitSection(section, rawStructure * mult, hi);
                }
                else if (ctx.targeting.TryGetGuardian(c, out var guardian))
                {
                    if (guardian.Side == info.source && !hitOwnSide) continue;
                    var wall = ctx.TreeOf(guardian.Side);
                    if (wall != null && wall.IsCoveredAt(c.bounds.center)) continue; // sheltered behind an intact wall
                    var hi = info; hi.hitPoint = c.bounds.center;
                    HitGuardian(guardian, rawGuardian * mult, hi);
                }
            }
            ctx.vfx?.Explosion(center, radius, new Color(1f, 0.8f, 0.5f), info.status.type == StatusEffectType.Poison);
            Shake(Mathf.Clamp(0.3f + radius * 0.1f, 0.35f, 0.6f));
            if (info.status.type != StatusEffectType.Poison) Services.Get<Audio.AudioService>()?.PlaySfxAt(AudioEventId.Explosion, center);
        }

        /// Heals own sections and guardians in an area.
        public void HealArea(Vector2 center, float radius, float amount, BattleSide side)
        {
            overlap.Clear();
            Physics2D.OverlapCircle(center, radius, filter, overlap);
            for (int i = 0; i < overlap.Count; i++)
            {
                var c = overlap[i];
                if (c == null || ctx.targeting == null) continue;
                if (ctx.targeting.TryGetSection(c, out var s) && s.Side == side) { s.Heal(amount); ctx.vfx?.Heal(c.bounds.center); }
                else if (ctx.targeting.TryGetGuardian(c, out var g) && g.Side == side) { g.Heal(amount); ctx.vfx?.Heal(c.bounds.center); }
            }
        }
    }
}
