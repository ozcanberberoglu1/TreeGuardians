using System.Collections.Generic;
using TreeGuardians.Battle;
using TreeGuardians.Data;
using TreeGuardians.Guardians;
using TreeGuardians.Trees;
using UnityEngine;

namespace TreeGuardians.AI
{
    /// Offline opponent. Plays by the same command rules; difficulty only changes thinking speed, aim error and target choice.
    public sealed class BotBattleCommander : MonoBehaviour, IBattleCommander
    {
        [SerializeField] float easyThink = 2.6f;
        [SerializeField] float normalThink = 1.5f;
        [SerializeField] float hardThink = 0.85f;
        [SerializeField] float easyAngleError = 12f;
        [SerializeField] float normalAngleError = 5f;
        [SerializeField] float hardAngleError = 1.5f;

        public BattleSide Side => BattleSide.Enemy;

        readonly Queue<BattleCommand> queue = new Queue<BattleCommand>(8);
        readonly List<GuardianController> ownAlive = new List<GuardianController>(8);
        readonly List<GuardianController> enemyAlive = new List<GuardianController>(8);
        readonly List<TreeSection> enemySections = new List<TreeSection>(32);
        BattleContext ctx;
        BotDifficulty difficulty;
        float thinkTimer;
        float toolTimer;

        public void Initialize(BattleContext context)
        {
            ctx = context;
            difficulty = ctx.setup != null ? ctx.setup.difficulty : BotDifficulty.Normal;
            thinkTimer = ThinkInterval * 1.5f;
            toolTimer = 6f;
            queue.Clear();
        }

        float ThinkInterval => difficulty == BotDifficulty.Easy ? easyThink : difficulty == BotDifficulty.Hard ? hardThink : normalThink;
        float AngleError => difficulty == BotDifficulty.Easy ? easyAngleError : difficulty == BotDifficulty.Hard ? hardAngleError : normalAngleError;

        public void Tick(float dt)
        {
            if (ctx == null || !ctx.isPlaying) return;
            thinkTimer -= dt;
            toolTimer -= dt;
            if (thinkTimer <= 0f)
            {
                thinkTimer = ThinkInterval * ctx.Range(0.8f, 1.25f);
                Think();
            }
            if (toolTimer <= 0f)
            {
                toolTimer = ctx.Range(2.5f, 5f);
                ConsiderTools();
            }
        }

        void Think()
        {
            var roster = ctx.enemyRoster;
            if (roster == null) return;
            roster.GetAlive(ownAlive);
            if (ownAlive.Count == 0) return;
            GuardianController shooter = null;
            int viewIndex = -1;
            int start = ctx.rng.Next(ownAlive.Count);
            for (int k = 0; k < ownAlive.Count; k++)
            {
                var g = ownAlive[(start + k) % ownAlive.Count];
                bool special = g.SpecialReady && ctx.NextFloat() < (difficulty == BotDifficulty.Hard ? 0.85f : 0.55f);
                if (!g.CanFire(special) && !g.CanFire(false)) continue;
                shooter = g;
                viewIndex = IndexOf(roster, g);
                if (viewIndex < 0) continue;
                bool useSpecial = special && g.CanFire(true);
                if (!ChooseAimPoint(g, useSpecial, out var aim)) return;
                var def = useSpecial && g.Definition.specialProjectile != null ? g.Definition.specialProjectile : g.Definition.normalProjectile;
                Vector2 dir;
                if (def != null)
                {
                    var v = ctx.projectiles.LaunchVelocity(def, g.MuzzlePosition, aim, 1f);
                    dir = v.sqrMagnitude > 0.001f ? v.normalized : Vector2.left;
                }
                else dir = (aim - g.MuzzlePosition).normalized;
                float err = ctx.Range(-AngleError, AngleError) * Mathf.Deg2Rad;
                float c = Mathf.Cos(err), s = Mathf.Sin(err);
                dir = new Vector2(dir.x * c - dir.y * s, dir.x * s + dir.y * c);
                queue.Enqueue(BattleCommand.Fire(Side, viewIndex, dir, 1f, useSpecial));
                return;
            }
        }

        static int IndexOf(GuardianRoster roster, GuardianController g)
        {
            for (int i = 0; i < roster.Slots.Count; i++) if (roster.Slots[i] == g) return i;
            return -1;
        }

        bool ChooseAimPoint(GuardianController shooter, bool special, out Vector2 aim)
        {
            aim = Vector2.zero;
            var tree = ctx.playerTree;
            var roster = ctx.playerRoster;
            if (tree == null) return false;
            tree.GetAliveSections(enemySections, true);
            if (roster != null) roster.GetAlive(enemyAlive); else enemyAlive.Clear();
            Vector2 from = shooter.MuzzlePosition;

            if (difficulty == BotDifficulty.Easy)
            {
                int total = enemySections.Count + enemyAlive.Count;
                if (total == 0) return false;
                int pick = ctx.rng.Next(total);
                aim = pick < enemySections.Count ? (Vector2)enemySections[pick].Collider.bounds.center : (Vector2)enemyAlive[pick - enemySections.Count].BodyCollider.bounds.center;
                return true;
            }

            if (difficulty == BotDifficulty.Hard)
            {
                TreeSection weakBranch = null;
                float weakest = 0.45f;
                for (int i = 0; i < enemySections.Count; i++)
                {
                    var s = enemySections[i];
                    if (s.Type != TreeSectionType.Branch || s.GuardianSlotIndex < 0) continue;
                    if (roster != null && roster.GetBySlotPosition(s.GuardianSlotIndex) == null) continue;
                    if (s.HealthPercent < weakest) { weakest = s.HealthPercent; weakBranch = s; }
                }
                if (weakBranch != null) { aim = weakBranch.Collider.bounds.center; return true; }
                if (tree.Core != null && !tree.Core.IsDestroyed && !tree.Core.IsProtected) { aim = tree.Core.Collider.bounds.center; return true; }
                var def = special && shooter.Definition.specialProjectile != null ? shooter.Definition.specialProjectile : shooter.Definition.normalProjectile;
                if (def != null && def.splashRadius > 0f && enemyAlive.Count >= 2)
                {
                    Vector2 c = Vector2.zero;
                    for (int i = 0; i < enemyAlive.Count; i++) c += (Vector2)enemyAlive[i].transform.position;
                    aim = c / enemyAlive.Count;
                    return true;
                }
                if (def != null && def.motion == ProjectileMotion.Homing && enemyAlive.Count > 0)
                {
                    GuardianController low = enemyAlive[0];
                    for (int i = 1; i < enemyAlive.Count; i++) if (enemyAlive[i].HealthPercent < low.HealthPercent) low = enemyAlive[i];
                    aim = low.BodyCollider.bounds.center;
                    return true;
                }
            }

            // Normal (and Hard fallback): alternate between exposed low-health guardians and the nearest bark plate.
            if (enemyAlive.Count > 0 && ctx.NextFloat() < 0.5f)
            {
                GuardianController low = enemyAlive[0];
                for (int i = 1; i < enemyAlive.Count; i++) if (enemyAlive[i].HealthPercent < low.HealthPercent) low = enemyAlive[i];
                aim = low.BodyCollider.bounds.center;
                return true;
            }
            TreeSection best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < enemySections.Count; i++)
            {
                var s = enemySections[i];
                float score = Vector2.Distance(from, s.transform.position);
                if (s.Type == TreeSectionType.BarkArmor) score -= 4f;
                if (s.Type == TreeSectionType.HeartwoodCore) score += s.IsProtected ? 25f : -8f;
                if (s.Type == TreeSectionType.RootStabilizer) score += 6f;
                if (score < bestScore) { bestScore = score; best = s; }
            }
            if (best == null) return false;
            aim = best.Collider.bounds.center;
            return true;
        }

        void ConsiderTools()
        {
            var tools = ctx.enemyTools;
            if (tools == null) return;
            float chance = difficulty == BotDifficulty.Easy ? 0.2f : difficulty == BotDifficulty.Hard ? 0.9f : 0.5f;
            for (int i = 0; i < tools.Count; i++)
            {
                if (!tools.CanUse(i)) continue;
                if (ctx.NextFloat() > chance) continue;
                var def = tools.Get(i).def;
                Vector2 target = ChooseToolTarget(def);
                queue.Enqueue(BattleCommand.Tool(Side, i, target));
                return;
            }
        }

        Vector2 ChooseToolTarget(ToolDefinition def)
        {
            var playerTree = ctx.playerTree;
            var ownTree = ctx.enemyTree;
            switch (def.effect)
            {
                case ToolEffectType.HealArea:
                {
                    if (ownTree == null) return Vector2.zero;
                    ownTree.GetAliveSections(enemySections, false);
                    TreeSection worst = null;
                    for (int i = 0; i < enemySections.Count; i++) if (worst == null || enemySections[i].HealthPercent < worst.HealthPercent) worst = enemySections[i];
                    return worst != null ? (Vector2)worst.transform.position : (Vector2)ownTree.transform.position;
                }
                case ToolEffectType.Barrier:
                    return ownTree != null && ownTree.Core != null ? (Vector2)ownTree.Core.transform.position : Vector2.zero;
                case ToolEffectType.SlowField:
                {
                    if (ctx.playerRoster != null) ctx.playerRoster.GetAlive(enemyAlive); else enemyAlive.Clear();
                    if (enemyAlive.Count == 0) return playerTree != null ? (Vector2)playerTree.transform.position : Vector2.zero;
                    Vector2 c = Vector2.zero;
                    for (int i = 0; i < enemyAlive.Count; i++) c += (Vector2)enemyAlive[i].transform.position;
                    return c / enemyAlive.Count;
                }
                default:
                {
                    if (playerTree == null) return Vector2.zero;
                    if (playerTree.Core != null && !playerTree.Core.IsProtected && !playerTree.Core.IsDestroyed) return playerTree.Core.Collider.bounds.center;
                    playerTree.GetAliveSections(enemySections, true);
                    TreeSection best = null;
                    for (int i = 0; i < enemySections.Count; i++)
                        if (enemySections[i].Type == TreeSectionType.BarkArmor && (best == null || enemySections[i].HealthPercent < best.HealthPercent)) best = enemySections[i];
                    if (best == null && enemySections.Count > 0) best = enemySections[ctx.rng.Next(enemySections.Count)];
                    return best != null ? (Vector2)best.Collider.bounds.center : (Vector2)playerTree.transform.position;
                }
            }
        }

        public bool TryDequeue(out BattleCommand command)
        {
            if (queue.Count > 0) { command = queue.Dequeue(); return true; }
            command = default;
            return false;
        }

        public void OnBattleEnded() => queue.Clear();
    }
}
