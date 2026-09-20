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

        public float HitSection(TreeSection s, float raw, in DamageInfo info)
        {
            if (s == null || s.IsDestroyed || raw <= 0f) return 0f;
            float dmg = ArmorReduce(raw, s.Armor);
            if (s.IsProtected && s.Tree != null && s.Tree.Definition != null) dmg *= s.Tree.Definition.protectedDamageMultiplier;
            if (info.isSplash && s.Tree != null) dmg *= 1f - s.Tree.RootDamageReduction;
            if (info.status.type == StatusEffectType.Poison) dmg *= 1f - s.PoisonResistance;
            bool wasAlive = !s.IsDestroyed;
            float applied = s.ApplyDamage(dmg, info);
            if (applied > 0f)
            {
                var stats = ctx.StatsOf(info.source);
                stats.damageDealt += applied;
                if (s.Type != TreeSectionType.HeartwoodCore) stats.structureDamage += applied;
                if (info.isSpecial) { stats.specialDamage += applied; GameEventBus.Publish(new SpecialDamageEvent { amount = applied, side = info.source }); }
                GameEventBus.Publish(new SectionDamagedEvent { side = s.Side, amount = applied, isCore = s.Type == TreeSectionType.HeartwoodCore });
                ctx.vfx?.DamageNumber(info.hitPoint == Vector2.zero ? (Vector2)s.transform.position : info.hitPoint, applied, info.isCrit ? new Color(1f, 0.85f, 0.3f) : Color.white, info.isCrit);
                if (wasAlive && s.IsDestroyed)
                {
                    if (s.Type == TreeSectionType.BarkArmor) stats.barkBroken++;
                    else if (s.Type == TreeSectionType.Branch) stats.branchesBroken++;
                    stats.sectionsBroken++;
                    ctx.vfx?.Shards(s.transform.position, s.Type == TreeSectionType.HeartwoodCore ? 14 : 7, s.Type == TreeSectionType.CanopyShield ? new Color(0.5f, 0.8f, 0.4f) : new Color(0.6f, 0.45f, 0.3f));
                    GameEventBus.Publish(new ScreenShakeEvent { amplitude = s.Type == TreeSectionType.HeartwoodCore ? 0.35f : 0.14f, duration = 0.2f });
                }
            }
            return applied;
        }

        public float HitGuardian(GuardianController g, float raw, in DamageInfo info, bool silent = false)
        {
            if (g == null || !g.IsAlive || raw <= 0f) return 0f;
            float dmg = ArmorReduce(raw, g.Armor + g.BuffArmorBonus);
            float applied = g.ApplyDamage(dmg, info);
            if (info.status.type != StatusEffectType.None) g.ApplyStatus(info.status);
            if (applied > 0f)
            {
                var stats = ctx.StatsOf(info.source);
                stats.damageDealt += applied;
                if (info.isSpecial) { stats.specialDamage += applied; GameEventBus.Publish(new SpecialDamageEvent { amount = applied, side = info.source }); }
                if (!silent) ctx.vfx?.DamageNumber(info.hitPoint == Vector2.zero ? (Vector2)g.transform.position : info.hitPoint, applied, info.isCrit ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.6f, 0.6f), info.isCrit);
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
                    var hi = info; hi.hitPoint = c.bounds.center;
                    HitSection(section, rawStructure * mult, hi);
                }
                else if (ctx.targeting.TryGetGuardian(c, out var guardian))
                {
                    if (guardian.Side == info.source && !hitOwnSide) continue;
                    var hi = info; hi.hitPoint = c.bounds.center;
                    HitGuardian(guardian, rawGuardian * mult, hi);
                }
            }
            ctx.vfx?.Burst(center, new Color(1f, 0.8f, 0.5f), Mathf.Clamp(radius * 0.8f, 0.6f, 2.5f));
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
