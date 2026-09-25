using System.Collections.Generic;
using TreeGuardians.Audio;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Guardians;
using UnityEngine;

namespace TreeGuardians.Battle
{
    /// Pools projectiles per definition, integrates motion at fixed timestep and routes hits to the DamageResolver.
    public sealed class ProjectileService : MonoBehaviour
    {
        [SerializeField] Transform poolRoot;
        [SerializeField] GameObject defaultPrefab;
        [SerializeField] float gravity = 22f;
        [SerializeField] float groundY = -3.3f;
        [SerializeField] float ceilingY = 14f;
        [SerializeField] float arenaHalfWidth = 15f;
        [SerializeField] int prewarmPerType = 6;

        public float Gravity => gravity;
        public float GroundY => groundY;
        public float CeilingY => ceilingY;
        public float ArenaHalfWidth => arenaHalfWidth;
        public int ActiveCount => active.Count;

        readonly Dictionary<ProjectileDefinition, ObjectPool<ProjectileController>> pools = new Dictionary<ProjectileDefinition, ObjectPool<ProjectileController>>(16);
        readonly List<ProjectileController> active = new List<ProjectileController>(64);
        readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(16);
        ContactFilter2D filter;
        BattleContext ctx;

        public void Initialize(BattleContext context)
        {
            ctx = context;
            filter = new ContactFilter2D { useTriggers = true, useLayerMask = false };
            if (poolRoot == null) poolRoot = transform;
            if (defaultPrefab == null && ctx.database != null) defaultPrefab = ctx.database.defaultProjectilePrefab;
            ReleaseAll();
        }

        public void Prewarm(ProjectileDefinition def)
        {
            if (def != null) PoolFor(def);
        }

        ObjectPool<ProjectileController> PoolFor(ProjectileDefinition def)
        {
            if (pools.TryGetValue(def, out var pool)) return pool;
            var prefabGo = def.prefab != null ? def.prefab : defaultPrefab;
            var prefab = prefabGo != null ? prefabGo.GetComponent<ProjectileController>() : null;
            if (prefab == null)
            {
                TGLog.Error($"ProjectileService: no ProjectileController prefab for '{def.id}'.");
                return null;
            }
            pool = new ObjectPool<ProjectileController>(prefab, poolRoot, prewarmPerType, 128);
            pools[def] = pool;
            return pool;
        }

        public ProjectileController Fire(ProjectileDefinition def, Vector2 origin, Vector2 velocity, BattleSide side, GuardianController source, bool special, bool tool, float damageMultiplier, Collider2D homingTarget)
        {
            if (def == null) return null;
            var pool = PoolFor(def);
            if (pool == null) return null;
            var p = pool.Get();
            p.Launch(def, origin, velocity, side, source, special, tool, damageMultiplier, homingTarget);
            active.Add(p);
            ctx.StatsOf(side).shotsFired++;
            Services.Get<AudioService>()?.PlaySfx(def.launchSfx);
            GameEventBus.Publish(new ProjectileFiredEvent { side = side, isSpecial = special });
            return p;
        }

        public void Tick(float dt)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var p = active[i];
                if (p == null) { active.RemoveAt(i); continue; }
                if (!p.Step(dt, this, hits, filter))
                {
                    active.RemoveAt(i);
                    p.ReleaseToPool();
                }
            }
        }

        public void ReleaseAll()
        {
            for (int i = 0; i < active.Count; i++) active[i]?.ReleaseToPool();
            active.Clear();
        }

        /// Returns true when the collider is a valid enemy target (and applies damage); consumed=false lets the projectile pass.
        public bool ResolveHit(ProjectileController p, RaycastHit2D hit, float rawGuardian, float rawStructure, out bool consumed)
        {
            consumed = true;
            if (ctx.targeting == null) return false;
            var info = new DamageInfo
            {
                source = p.Side, isSpecial = p.IsSpecial, isTool = p.IsTool, isCrit = p.IsCrit,
                status = p.Def.statusEffect, hitPoint = hit.point, sourceSlot = p.Source != null ? p.Source.SlotIndex : -1
            };
            // A cast that starts inside the collider reports distance 0; use the projectile position as the impact point then.
            Vector2 point = hit.distance <= 0.001f ? p.Position : hit.point;
            info.hitPoint = point;
            if (ctx.targeting.TryGetSection(hit.collider, out var section))
            {
                if (section.Side == p.Side && !p.Def.canHitOwnSide) return false;
                if (section.IsDestroyed) return false;
                if (section.Tree != null && section.Tree.IsDestructible && section.Tree.IsHoleAt(point)) return false; // flies on through the hole
                info.holeRadius = HoleRadius(p);
                if (p.Def.splashRadius > 0f) return true;
                info.amount = rawStructure;
                ctx.damage.HitSection(section, rawStructure, info);
                OnHitFeedback(p, point, true);
                return true;
            }
            if (ctx.targeting.TryGetGuardian(hit.collider, out var guardian))
            {
                if (guardian.Side == p.Side && !p.Def.canHitOwnSide) return false;
                if (!guardian.IsAlive) return false;
                var wall = ctx.TreeOf(guardian.Side);
                if (wall != null && wall.IsCoveredAt(point)) return false; // still hidden behind an intact wall
                if (p.Def.splashRadius > 0f) return true;
                info.amount = rawGuardian;
                ctx.damage.HitGuardian(guardian, rawGuardian, info);
                // The wall around a guardian hit keeps crumbling, so a barely exposed guardian does not shield the wall.
                ctx.damage.ChipWall(wall, point, HoleRadius(p) * ctx.balance.guardianHitWallChipRadius, rawStructure * ctx.balance.guardianHitWallChipDamage, info);
                OnHitFeedback(p, point, true);
                return true;
            }
            return false;
        }

        float HoleRadius(ProjectileController p)
        {
            float r = p.Def.holeRadius > 0f ? p.Def.holeRadius : ctx.balance.castleHoleRadius;
            return r * (p.IsSpecial ? 1.3f : 1f);
        }

        public void SplashAt(ProjectileController p, Vector2 point, float rawGuardian, float rawStructure)
        {
            var info = new DamageInfo { source = p.Side, isSpecial = p.IsSpecial, isTool = p.IsTool, isCrit = p.IsCrit, status = p.Def.statusEffect, hitPoint = point, sourceSlot = p.Source != null ? p.Source.SlotIndex : -1 };
            ctx.damage.Splash(point, p.Def.splashRadius, p.Def.splashFalloff, rawGuardian, rawStructure, info, p.Def.canHitOwnSide);
            ctx.damage.CarveAt(point, Mathf.Max(HoleRadius(p), p.Def.splashRadius * 0.85f), p.Side, p.Def.canHitOwnSide);
            if (p.Def.statusEffect.type == StatusEffectType.Poison) ctx.vfx?.Poison(point, p.Def.splashRadius);
            OnHitFeedback(p, point, true);
        }

        void OnHitFeedback(ProjectileController p, Vector2 point, bool counted)
        {
            if (counted) { ctx.StatsOf(p.Side).shotsHit++; GameEventBus.Publish(new ProjectileHitEvent { side = p.Side, hitTarget = true }); }
            p.Source?.OnOwnProjectileHit();
            ctx.vfx?.Hit(point, p.Def.tint);
            Services.Get<AudioService>()?.PlaySfx(p.Def.impactSfx);
            if (p.Def.shake.amplitude > 0f) GameEventBus.Publish(new ScreenShakeEvent { amplitude = p.Def.shake.amplitude, duration = p.Def.shake.duration });
        }

        public void OnGroundHit(ProjectileController p)
        {
            ctx.vfx?.Hit(new Vector2(p.Position.x, groundY + 0.1f), new Color(0.7f, 0.6f, 0.4f));
        }

        public void OnMiss(ProjectileController p)
        {
            GameEventBus.Publish(new ProjectileHitEvent { side = p.Side, hitTarget = false });
        }

        public float DeflectFor(BattleSide projectileSide)
        {
            var tools = ctx.ToolsOf(BattleContext.Opponent(projectileSide));
            return tools != null ? tools.DeflectStrength : 0f;
        }

        /// Solves a launch velocity for the projectile type toward a point. Ballistic types get a low arc when reachable.
        public Vector2 LaunchVelocity(ProjectileDefinition def, Vector2 from, Vector2 to, float power)
        {
            float speed = def.speed * Mathf.Clamp(power, 0.35f, 1f);
            bool arcs = def.motion == ProjectileMotion.Ballistic || def.motion == ProjectileMotion.AreaBomb || def.motion == ProjectileMotion.Bouncing;
            if (!arcs) return (to - from).normalized * speed;
            SolveBallistic(from, to, speed, gravity * def.gravityScale, out var v);
            return v;
        }

        public static bool SolveBallistic(Vector2 from, Vector2 to, float speed, float g, out Vector2 velocity)
        {
            float dx = to.x - from.x;
            float dy = to.y - from.y;
            if (g <= 0.001f || Mathf.Abs(dx) < 0.001f)
            {
                velocity = new Vector2(dx, dy).normalized * speed;
                return true;
            }
            float s2 = speed * speed;
            float disc = s2 * s2 - g * (g * dx * dx + 2f * dy * s2);
            if (disc < 0f)
            {
                float ang = 45f * Mathf.Deg2Rad;
                velocity = new Vector2(Mathf.Sign(dx) * Mathf.Cos(ang), Mathf.Sin(ang)) * speed;
                return false;
            }
            float root = Mathf.Sqrt(disc);
            float angle = Mathf.Atan((s2 - root) / (g * Mathf.Abs(dx)));
            velocity = new Vector2(Mathf.Sign(dx) * Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            return true;
        }

        /// Predicts the first part of the flight for the aim guide (no collisions).
        public void PredictPath(ProjectileDefinition def, Vector2 origin, Vector2 velocity, List<Vector2> into, int points, float stepSeconds)
        {
            into.Clear();
            bool arcs = def.motion == ProjectileMotion.Ballistic || def.motion == ProjectileMotion.AreaBomb || def.motion == ProjectileMotion.Bouncing;
            Vector2 p = origin, v = velocity;
            for (int i = 0; i < points; i++)
            {
                if (arcs) v.y -= gravity * def.gravityScale * stepSeconds;
                p += v * stepSeconds;
                if (p.y < groundY) break;
                into.Add(p);
            }
        }
    }
}
