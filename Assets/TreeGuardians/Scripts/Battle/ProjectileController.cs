using System.Collections.Generic;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Guardians;
using UnityEngine;

namespace TreeGuardians.Battle
{
    /// Pooled projectile. Motion is integrated by ProjectileService at a fixed timestep; hits resolve exactly once per target.
    public sealed class ProjectileController : PooledBehaviour
    {
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] TrailRenderer trail;
        [Tooltip("Merminin etrafındaki parıltı (isabette gizlenir).")] [SerializeField] SpriteRenderer glow;
        [Tooltip("İz parçacıkları (duman / pırıltı). İsabette emisyon durur, mevcut parçacıklar söner.")] [SerializeField] ParticleSystem trailFx;
        [Tooltip("İsabetten sonra iz ve parçacıkların sönmesi için en az bekleme (saniye).")] [SerializeField] float minLingerSeconds = 0.25f;

        public ProjectileDefinition Def { get; private set; }
        public BattleSide Side { get; private set; }
        public GuardianController Source { get; private set; }
        public bool IsSpecial { get; private set; }
        public bool IsTool { get; private set; }
        public bool IsCrit { get; set; }
        public float DamageMultiplier { get; private set; } = 1f;
        public Vector2 Position => position;
        public Vector2 Velocity => velocity;
        /// Time (Time.time) at which a finished projectile goes back to the pool (its trail has faded by then).
        public float ReleaseAt { get; private set; }

        Vector2 position;
        Vector2 velocity;
        float age;
        int pierceLeft;
        int bounceLeft;
        Collider2D homingTarget;
        readonly HashSet<int> hitIds = new HashSet<int>(8);
        bool hitSomething;

        public void Launch(ProjectileDefinition def, Vector2 origin, Vector2 initialVelocity, BattleSide side, GuardianController source, bool special, bool tool, float damageMultiplier, Collider2D homing)
        {
            Def = def;
            Side = side;
            Source = source;
            IsSpecial = special;
            IsTool = tool;
            IsCrit = false;
            DamageMultiplier = damageMultiplier;
            position = origin;
            velocity = initialVelocity;
            age = 0f;
            pierceLeft = def.pierceCount;
            bounceLeft = def.bounceCount;
            homingTarget = homing;
            hitIds.Clear();
            hitSomething = false;
            transform.position = origin;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
            transform.localScale = Vector3.one * def.visualScale;
            if (sprite != null)
            {
                sprite.sprite = def.icon;
                sprite.color = def.tint;
                sprite.enabled = def.icon != null;
            }
            if (glow != null) glow.enabled = true;
            if (trail != null) { trail.Clear(); trail.startColor = def.tint; trail.endColor = new Color(def.tint.r, def.tint.g, def.tint.b, 0f); trail.emitting = true; }
            if (trailFx != null) { trailFx.Clear(true); trailFx.Play(true); }
            ReleaseAt = 0f;
        }

        /// Finished flight: hides the body and stops emitting, so the trail and trail particles fade out in place.
        /// Returns the Time.time at which it is safe to return the projectile to the pool.
        public float BeginFade()
        {
            if (sprite != null) sprite.enabled = false;
            if (glow != null) glow.enabled = false;
            float linger = Mathf.Max(0f, minLingerSeconds);
            if (trail != null) { trail.emitting = false; linger = Mathf.Max(linger, trail.time); }
            if (trailFx != null)
            {
                trailFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                linger = Mathf.Max(linger, trailFx.main.startLifetime.constantMax);
            }
            homingTarget = null;
            ReleaseAt = Time.time + linger;
            return ReleaseAt;
        }

        public override void OnDespawned()
        {
            if (trail != null) { trail.emitting = false; trail.Clear(); }
            if (trailFx != null) { trailFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
            homingTarget = null;
            Source = null;
        }

        float RawVsGuardian => (Source != null ? Source.Attack : Def.baseDamage) * Def.guardianMultiplier * DamageMultiplier;
        float RawVsStructure => (Source != null ? Source.StructureDamage * (Source.Definition.passiveKind == GuardianPassiveKind.ExtraStructureDamage ? 1f + Source.Definition.passiveMagnitude : 1f) : Def.baseDamage) * Def.structureMultiplier * DamageMultiplier;

        /// Advances one fixed step. Returns false when the projectile is finished.
        public bool Step(float dt, ProjectileService svc, List<RaycastHit2D> hits, ContactFilter2D filter)
        {
            age += dt;
            if (age > Def.lifetime) { if (!hitSomething) svc.OnMiss(this); return false; }

            switch (Def.motion)
            {
                case ProjectileMotion.Ballistic:
                case ProjectileMotion.AreaBomb:
                case ProjectileMotion.Bouncing:
                    velocity.y -= svc.Gravity * Def.gravityScale * dt;
                    break;
                case ProjectileMotion.Homing:
                    if (homingTarget != null && homingTarget.enabled)
                    {
                        Vector2 desired = ((Vector2)homingTarget.bounds.center - position).normalized * Def.speed;
                        velocity = Vector2.Lerp(velocity, desired, Mathf.Clamp01(Def.homingStrength * dt));
                        if (velocity.sqrMagnitude > 0.01f) velocity = velocity.normalized * Def.speed;
                    }
                    // No target (aimed shot): keep the exact aimed direction, no droop.
                    break;
            }
            float deflect = svc.DeflectFor(Side);
            if (deflect > 0f) velocity += new Vector2(-Mathf.Sign(velocity.x) * deflect * 2.5f, deflect * 3f) * dt;

            Vector2 delta = velocity * dt;
            float dist = delta.magnitude;
            if (dist > 0.0001f)
            {
                hits.Clear();
                int n = Physics2D.CircleCast(position, Def.collisionRadius, delta / dist, filter, hits, dist + Def.collisionRadius * 0.5f);
                if (n > 1) hits.Sort((a, b) => a.distance.CompareTo(b.distance));
                for (int i = 0; i < n; i++)
                {
                    var hit = hits[i];
                    if (hit.collider == null) continue;
                    int id = hit.collider.GetInstanceID();
                    if (hitIds.Contains(id)) continue;
                    if (!svc.ResolveHit(this, hit, RawVsGuardian, RawVsStructure, out bool consumed)) continue;
                    hitIds.Add(id);
                    hitSomething = true;
                    if (!consumed) continue;
                    if (Def.splashRadius > 0f) { svc.SplashAt(this, hit.point, RawVsGuardian, RawVsStructure); return false; }
                    if (pierceLeft > 0) { pierceLeft--; continue; }
                    if (bounceLeft > 0)
                    {
                        bounceLeft--;
                        velocity = Vector2.Reflect(velocity, hit.normal) * 0.65f;
                        position = hit.point + hit.normal * (Def.collisionRadius + 0.02f);
                        transform.position = position;
                        return true;
                    }
                    return false;
                }
            }
            position += delta;
            transform.position = position;
            if (velocity.sqrMagnitude > 0.001f) transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);

            if (position.y < svc.GroundY)
            {
                if (Def.splashRadius > 0f) svc.SplashAt(this, new Vector2(position.x, svc.GroundY + 0.1f), RawVsGuardian, RawVsStructure);
                else svc.OnGroundHit(this);
                if (!hitSomething && Def.splashRadius <= 0f) svc.OnMiss(this);
                return false;
            }
            if (Mathf.Abs(position.x) > svc.ArenaHalfWidth || position.y > svc.CeilingY) { if (!hitSomething) svc.OnMiss(this); return false; }
            return true;
        }
    }
}
