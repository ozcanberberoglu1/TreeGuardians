using System.Collections.Generic;
using TreeGuardians.Data;
using TreeGuardians.Guardians;
using TreeGuardians.Trees;
using UnityEngine;

namespace TreeGuardians.Battle
{
    /// Collider → target registry and target selection rules shared by auto-attacks, tools and the bot.
    public sealed class TargetingController
    {
        readonly BattleContext ctx;
        readonly Dictionary<Collider2D, TreeSection> sections = new Dictionary<Collider2D, TreeSection>(64);
        readonly Dictionary<Collider2D, GuardianController> guardians = new Dictionary<Collider2D, GuardianController>(16);
        readonly List<TreeSection> tmpSections = new List<TreeSection>(32);
        readonly List<GuardianController> tmpGuardians = new List<GuardianController>(8);

        public float MidlineX = 0f;

        public TargetingController(BattleContext context)
        {
            ctx = context;
        }

        public void BuildRegistry()
        {
            sections.Clear();
            guardians.Clear();
            RegisterTree(ctx.playerTree);
            RegisterTree(ctx.enemyTree);
            RegisterRoster(ctx.playerRoster);
            RegisterRoster(ctx.enemyRoster);
        }

        void RegisterTree(TreeController tree)
        {
            if (tree == null) return;
            for (int i = 0; i < tree.Sections.Count; i++)
            {
                var s = tree.Sections[i];
                if (s != null && s.Collider != null) sections[s.Collider] = s;
            }
        }

        void RegisterRoster(GuardianRoster roster)
        {
            if (roster == null) return;
            for (int i = 0; i < roster.Slots.Count; i++)
            {
                var g = roster.Slots[i];
                if (g != null && g.BodyCollider != null) guardians[g.BodyCollider] = g;
            }
        }

        public bool TryGetSection(Collider2D c, out TreeSection s) => sections.TryGetValue(c, out s);
        public bool TryGetGuardian(Collider2D c, out GuardianController g) => guardians.TryGetValue(c, out g);

        public BattleSide SideAt(Vector2 position) => position.x < MidlineX ? BattleSide.Player : BattleSide.Enemy;

        /// Picks an enemy target according to preference. Returns false when nothing is targetable.
        public bool FindTarget(BattleSide shooter, TargetPreference pref, Vector2 from, float range, out Vector2 aimPoint, out Collider2D target)
        {
            aimPoint = Vector2.zero;
            target = null;
            var enemySide = BattleContext.Opponent(shooter);
            var tree = ctx.TreeOf(enemySide);
            var roster = ctx.RosterOf(enemySide);
            if (tree == null) return false;
            tree.GetAliveSections(tmpSections, true);
            if (roster != null) roster.GetAlive(tmpGuardians); else tmpGuardians.Clear();

            Collider2D best = null;
            float bestScore = float.MaxValue;

            switch (pref)
            {
                case TargetPreference.ExposedCore:
                    if (tree.Core != null && !tree.Core.IsDestroyed && !tree.Core.IsProtected && tree.Core.Collider != null) { best = tree.Core.Collider; break; }
                    goto case TargetPreference.Structure;
                case TargetPreference.Structure:
                    for (int i = 0; i < tmpSections.Count; i++)
                    {
                        var s = tmpSections[i];
                        if (s.Collider == null) continue;
                        float score = Vector2.Distance(from, s.transform.position);
                        if (s.Type == TreeSectionType.HeartwoodCore) score += s.IsProtected ? 30f : -6f;
                        if (s.Type == TreeSectionType.BarkArmor) score -= 3f;
                        if (s.Type == TreeSectionType.RootStabilizer) score += 8f;
                        if (score < bestScore) { bestScore = score; best = s.Collider; }
                    }
                    break;
                case TargetPreference.LowestHealth:
                    for (int i = 0; i < tmpGuardians.Count; i++)
                    {
                        var g = tmpGuardians[i];
                        if (g.BodyCollider == null) continue;
                        float score = g.HealthPercent * 10f + Vector2.Distance(from, g.transform.position) * 0.1f;
                        if (score < bestScore) { bestScore = score; best = g.BodyCollider; }
                    }
                    if (best == null) goto case TargetPreference.Nearest;
                    break;
                case TargetPreference.Random:
                {
                    int total = tmpSections.Count + tmpGuardians.Count;
                    if (total == 0) return false;
                    int pick = ctx.rng.Next(total);
                    best = pick < tmpSections.Count ? tmpSections[pick].Collider : tmpGuardians[pick - tmpSections.Count].BodyCollider;
                    break;
                }
                case TargetPreference.Support:
                case TargetPreference.Nearest:
                default:
                    for (int i = 0; i < tmpGuardians.Count; i++)
                    {
                        var g = tmpGuardians[i];
                        if (g.BodyCollider == null) continue;
                        float score = Vector2.Distance(from, g.transform.position) - 1.5f;
                        if (score < bestScore) { bestScore = score; best = g.BodyCollider; }
                    }
                    for (int i = 0; i < tmpSections.Count; i++)
                    {
                        var s = tmpSections[i];
                        if (s.Collider == null) continue;
                        float score = Vector2.Distance(from, s.transform.position);
                        if (s.Type == TreeSectionType.HeartwoodCore && s.IsProtected) score += 30f;
                        if (score < bestScore) { bestScore = score; best = s.Collider; }
                    }
                    break;
            }
            if (best == null) return false;
            target = best;
            aimPoint = best.bounds.center;
            return true;
        }

        /// Nearest enemy collider roughly in the given direction (for manual homing shots).
        public bool FindInDirection(BattleSide shooter, Vector2 from, Vector2 direction, out Collider2D target)
        {
            target = null;
            var enemySide = BattleContext.Opponent(shooter);
            var tree = ctx.TreeOf(enemySide);
            var roster = ctx.RosterOf(enemySide);
            if (tree == null) return false;
            tree.GetAliveSections(tmpSections, true);
            if (roster != null) roster.GetAlive(tmpGuardians); else tmpGuardians.Clear();
            Vector2 dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
            float best = float.MaxValue;
            for (int i = 0; i < tmpGuardians.Count; i++)
            {
                var c = tmpGuardians[i].BodyCollider;
                if (c == null) continue;
                float score = Score(from, dir, c.bounds.center) - 1f;
                if (score < best) { best = score; target = c; }
            }
            for (int i = 0; i < tmpSections.Count; i++)
            {
                var c = tmpSections[i].Collider;
                if (c == null) continue;
                float score = Score(from, dir, c.bounds.center);
                if (score < best) { best = score; target = c; }
            }
            return target != null;
        }

        static float Score(Vector2 from, Vector2 dir, Vector2 point)
        {
            Vector2 to = point - from;
            float dist = to.magnitude;
            float angle = Vector2.Angle(dir, to);
            return dist * 0.5f + angle * 0.15f;
        }

        /// Instant chain strike used by ChainEnergy: hits up to count alive enemy guardians, nearest first.
        public bool ChainStrike(GuardianController source, int count, float damage)
        {
            var enemySide = BattleContext.Opponent(source.Side);
            var roster = ctx.RosterOf(enemySide);
            if (roster == null) return false;
            roster.GetAlive(tmpGuardians);
            if (tmpGuardians.Count == 0)
            {
                var tree = ctx.TreeOf(enemySide);
                if (tree == null) return false;
                tree.GetAliveSections(tmpSections, true);
                if (tmpSections.Count == 0) return false;
                var s = tmpSections[ctx.rng.Next(tmpSections.Count)];
                ctx.damage.HitSection(s, damage * 1.5f, new DamageInfo { amount = damage, source = source.Side, isSpecial = true, hitPoint = s.transform.position, sourceSlot = source.SlotIndex });
                ctx.vfx?.Burst(s.transform.position, new Color(1f, 0.95f, 0.4f), 1f);
                Core.Services.Get<Audio.AudioService>()?.PlaySfxAt(AudioEventId.MagicImpact, s.transform.position, 0.7f);
                return true;
            }
            Vector2 from = source.MuzzlePosition;
            int hits = 0;
            while (hits < count && tmpGuardians.Count > 0)
            {
                int bi = 0;
                float bd = float.MaxValue;
                for (int i = 0; i < tmpGuardians.Count; i++)
                {
                    float d = Vector2.Distance(from, tmpGuardians[i].transform.position);
                    if (d < bd) { bd = d; bi = i; }
                }
                var g = tmpGuardians[bi];
                tmpGuardians.RemoveAt(bi);
                float dmg = damage * Mathf.Pow(0.8f, hits);
                ctx.damage.HitGuardian(g, dmg, new DamageInfo { amount = dmg, source = source.Side, isSpecial = true, hitPoint = g.transform.position, sourceSlot = source.SlotIndex });
                ctx.vfx?.Burst(g.transform.position, new Color(1f, 0.95f, 0.4f), 0.8f);
                Core.Services.Get<Audio.AudioService>()?.PlaySfxAt(AudioEventId.MagicImpact, g.transform.position, 0.7f, 1f + hits * 0.08f);
                from = g.transform.position;
                hits++;
            }
            return hits > 0;
        }
    }
}
