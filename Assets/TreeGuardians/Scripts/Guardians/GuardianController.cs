using System;
using TreeGuardians.Audio;
using TreeGuardians.Battle;
using TreeGuardians.Core;
using TreeGuardians.Data;
using UnityEngine;

namespace TreeGuardians.Guardians
{
    /// Pre-authored guardian slot view + runtime combat state. Definition data is never mutated.
    public sealed class GuardianController : MonoBehaviour
    {
        struct ActiveStatus { public float remaining; public float magnitude; public float tick; }

        [Header("Visuals (pre-authored)")]
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] Transform visualRoot;
        [SerializeField] Transform muzzle;
        [SerializeField] Transform healthBarFill;
        [SerializeField] SpriteRenderer healthBarFillRenderer;
        [SerializeField] Transform energyBarFill;
        [Tooltip("Can/enerji çubuğu renderer'ları: sıra modunda yalnızca nişan alırken ya da hasar sonrası kısa süre görünür.")]
        [SerializeField] SpriteRenderer[] barRenderers = new SpriteRenderer[0];
        [SerializeField] float barsShowAfterHit = 1.6f;
        [SerializeField] SpriteRenderer selectionRing;
        [SerializeField] SpriteRenderer shieldVisual;
        [SerializeField] SpriteRenderer statusIcon;
        [SerializeField] Collider2D bodyCollider;
        [SerializeField] SpriteRenderer platform;
        [SerializeField] Gradient healthGradient;
        [Tooltip("Vuruşta beyaz flaş için rig renderer'larına atanan materyal (Tree Guardians/2D/Sprite Silhouette).")] [SerializeField] Material flashMaterial;
        [SerializeField] bool showPlatform = true;

        public GuardianDefinition Definition { get; private set; }
        public int Level { get; private set; }
        public BattleSide Side { get; private set; }
        public int SlotIndex { get; private set; }
        public int HomeSlot { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsAlive { get; private set; }
        public float MaxHealth { get; private set; }
        public float Health { get; private set; }
        public float Attack { get; private set; }
        public float StructureDamage { get; private set; }
        public float Armor { get; private set; }
        public float CritChance { get; private set; }
        public float CritMultiplier { get; private set; }
        public float Cooldown { get; private set; }
        public float CooldownDuration { get; private set; }
        public float SpecialEnergy { get; private set; }
        public float SpecialEnergyMax { get; private set; }
        public float ShieldHealth { get; private set; }
        public float BuffArmorBonus { get; private set; }
        public float BuffAttackMultiplier { get; private set; } = 1f;
        /// Armor from allied auras; set exactly by GuardianRoster.RecomputeAuras (0 when the aura guardian is gone).
        public float AuraArmorBonus { get; set; }
        public bool IsStunned => statuses[(int)StatusEffectType.Stun].remaining > 0f;
        public bool IsRooted => statuses[(int)StatusEffectType.Root].remaining > 0f;
        public bool IsSlowed => statuses[(int)StatusEffectType.Slow].remaining > 0f || externalSlow > 0f;
        public bool SpecialReady => IsAlive && IsActive && SpecialEnergy >= Definition.specialEnergyCost;
        public float HealthPercent => MaxHealth > 0f ? Health / MaxHealth : 0f;
        public float CooldownPercent => CooldownDuration > 0f ? Mathf.Clamp01(1f - Cooldown / CooldownDuration) : 1f;
        public float EnergyPercent => SpecialEnergyMax > 0f ? Mathf.Clamp01(SpecialEnergy / SpecialEnergyMax) : 0f;
        public Vector2 MuzzlePosition => muzzle != null ? (Vector2)muzzle.position : (Vector2)transform.position;
        public Collider2D BodyCollider => bodyCollider;
        public bool RebirthUsed { get; private set; }

        public event Action<GuardianController> OnStateChanged;

        readonly ActiveStatus[] statuses = new ActiveStatus[8];
        BattleContext ctx;
        GuardianRoster roster;
        Color baseColor = Color.white;
        float flashUntil;
        float buffArmorUntil;
        float buffAttackUntil;
        int buffArmorTurn = int.MinValue, buffAttackTurn = int.MinValue;
        bool TurnMode => ctx != null && ctx.turnBased && ctx.turns != null;
        /// Last turn index that still counts for an effect cast now: through the owner's next own turn.
        int NextOwnTurn => ctx.turns.TurnIndex + (ctx.turns.CurrentSide == Side ? 2 : 1);
        float externalSlow;
        float bobPhase;
        float fillBaseScaleX = 1f;
        float energyBaseScaleX = 1f;
        GameObject customVisual;
        Animator customAnimator;
        SpriteRenderer[] customRenderers;
        MaterialPropertyBlock flashBlock;
        float flashAmount;
        float squashT = -1f;
        float dyingT = -1f;
        static readonly int SilhouetteId = Shader.PropertyToID("_Silhouette");
        static readonly int SilhouetteColorId = Shader.PropertyToID("_SilhouetteColor");

        public void Setup(GuardianDefinition def, int level, BattleSide side, int slot, BattleContext context, GuardianRoster owner)
        {
            Definition = def;
            Level = Mathf.Max(1, level);
            Side = side;
            SlotIndex = slot;
            HomeSlot = slot;
            ctx = context;
            roster = owner;
            IsActive = true;
            IsAlive = true;
            RebirthUsed = false;
            var balance = ctx.balance;
            MaxHealth = def.GetHealth(Level, balance);
            Health = MaxHealth;
            Attack = def.GetAttack(Level, balance);
            StructureDamage = def.GetStructureDamage(Level, balance);
            Armor = def.GetArmor(Level);
            CritChance = Mathf.Max(balance.critChanceMin, def.critChance + (def.passiveKind == GuardianPassiveKind.CritBoost ? def.passiveMagnitude : 0f));
            CritMultiplier = def.critMultiplier;
            CooldownDuration = def.attackCooldown;
            Cooldown = def.attackCooldown * ctx.Range(0.3f, 0.8f);
            SpecialEnergyMax = balance.specialEnergyMax;
            SpecialEnergy = 0f;
            ShieldHealth = 0f;
            BuffArmorBonus = 0f;
            BuffAttackMultiplier = 1f;
            buffArmorTurn = buffAttackTurn = int.MinValue;
            externalSlow = 0f;
            for (int i = 0; i < statuses.Length; i++) statuses[i] = default;
            bobPhase = ctx.NextFloat() * 6.28f;

            if (customVisual != null) { Destroy(customVisual); customVisual = null; customAnimator = null; customRenderers = null; }
            if (sprite != null)
            {
                sprite.enabled = def.worldPrefab == null;
                sprite.sprite = def.worldSprite;
                baseColor = def.tintColor;
                sprite.color = baseColor;
            }
            if (def.worldPrefab != null && visualRoot != null)
            {
                customVisual = Instantiate(def.worldPrefab, visualRoot);
                customVisual.name = "AnimalVisual";
                AnimalVisual.FitToAnchor(customVisual, def.worldPrefab.transform.localScale, Mathf.Max(0.01f, def.battleVisualScale), false);
                customAnimator = customVisual.GetComponentInChildren<Animator>(true);
                customRenderers = customVisual.GetComponentsInChildren<SpriteRenderer>(true);
                if (flashMaterial != null) foreach (var r in customRenderers) r.sharedMaterial = flashMaterial;
                foreach (var r in customVisual.GetComponentsInChildren<Renderer>(true)) r.sortingOrder += sprite != null ? sprite.sortingOrder : 8;
            }
            CacheSortingOrders();
            ApplySortingOffset(0);
            if (healthBarFill != null) fillBaseScaleX = healthBarFill.localScale.x == 0f ? 1f : Mathf.Abs(healthBarFill.localScale.x);
            if (energyBarFill != null) energyBaseScaleX = energyBarFill.localScale.x == 0f ? 1f : Mathf.Abs(energyBarFill.localScale.x);
            if (bodyCollider != null) bodyCollider.enabled = true;
            if (selectionRing != null) selectionRing.enabled = false;
            if (shieldVisual != null) shieldVisual.enabled = false;
            if (statusIcon != null) statusIcon.enabled = false;
            if (platform != null) platform.enabled = showPlatform;
            flashAmount = 0f; squashT = -1f; dyingT = -1f; AuraArmorBonus = 0f;
            if (visualRoot != null) visualRoot.localScale = Vector3.one;
            ApplyFlash(0f);
            gameObject.SetActive(true);
            UpdateBars();
        }

        public void Deactivate()
        {
            IsActive = false;
            IsAlive = false;
            Definition = null;
            gameObject.SetActive(false);
        }

        public void SetSelected(bool selected)
        {
            if (selectionRing != null) selectionRing.enabled = selected && IsAlive;
        }

        Renderer[] sortedRenderers;
        int[] baseSortingOrders;
        int sortingOffset;

        void CacheSortingOrders()
        {
            sortedRenderers = GetComponentsInChildren<Renderer>(true);
            baseSortingOrders = new int[sortedRenderers.Length];
            for (int i = 0; i < sortedRenderers.Length; i++) baseSortingOrders[i] = sortedRenderers[i].sortingOrder;
        }

        /// Lifts (or restores) every renderer of this guardian, e.g. above the silhouetted castle wall while the player aims.
        public void ApplySortingOffset(int offset)
        {
            if (sortedRenderers == null) CacheSortingOrders();
            sortingOffset = offset;
            for (int i = 0; i < sortedRenderers.Length; i++)
                if (sortedRenderers[i] != null) sortedRenderers[i].sortingOrder = baseSortingOrders[i] + offset;
        }

        public void SetExternalSlow(float strength) => externalSlow = Mathf.Clamp01(strength);

        /// Hit flash, squash and the death pop run even when the guardian is no longer alive.
        void TickVisuals(float dt)
        {
            if (flashAmount > 0f) { flashAmount = Mathf.Max(0f, flashAmount - dt / 0.14f); ApplyFlash(flashAmount * flashAmount); }
            if (visualRoot == null) return;
            if (dyingT >= 0f)
            {
                dyingT += dt;
                float s = dyingT < 0.08f ? Mathf.Lerp(1f, 1.2f, dyingT / 0.08f) : Mathf.Lerp(1.2f, 0f, (dyingT - 0.08f) / 0.14f);
                visualRoot.localScale = new Vector3(s, s, 1f);
                if (dyingT >= 0.22f) { dyingT = -1f; if (customVisual != null) customVisual.SetActive(false); if (sprite != null) sprite.enabled = false; visualRoot.localScale = Vector3.one; }
                return;
            }
            if (squashT >= 0f)
            {
                squashT += dt;
                float k = Mathf.Clamp01(squashT / 0.16f);
                float a = Mathf.Sin(k * Mathf.PI) * (1f - k);
                visualRoot.localScale = new Vector3(1f + 0.14f * a, 1f - 0.12f * a, 1f);
                if (k >= 1f) { squashT = -1f; visualRoot.localScale = Vector3.one; }
            }
        }

        void ApplyFlash(float amount)
        {
            if (customRenderers == null || flashMaterial == null) return;
            if (flashBlock == null) flashBlock = new MaterialPropertyBlock();
            for (int i = 0; i < customRenderers.Length; i++)
            {
                var r = customRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(flashBlock);
                flashBlock.SetFloat(SilhouetteId, amount);
                flashBlock.SetColor(SilhouetteColorId, Color.white);
                r.SetPropertyBlock(flashBlock);
            }
        }

        public void Tick(float dt) => Tick(dt, dt);

        /// statusDt: time that counts down statuses (in the turn-based duel only on the owner's own turn).
        public void Tick(float dt, float statusDt)
        {
            TickVisuals(dt);
            if (!IsActive || !IsAlive) return;
            float speed = IsSlowed ? 1f - Mathf.Max(externalSlow, statuses[(int)StatusEffectType.Slow].magnitude) * 0.6f : 1f;
            if (Cooldown > 0f) Cooldown = Mathf.Max(0f, Cooldown - dt * speed);

            float regen = ctx.balance.specialEnergyPerSecond * (1f + (roster != null ? roster.SapFlowBonus : 0f));
            SpecialEnergy = Mathf.Min(SpecialEnergyMax, SpecialEnergy + regen * dt);
            CheckSpecialReadyCue();

            for (int i = 0; i < statuses.Length; i++)
            {
                if (statuses[i].remaining <= 0f) continue;
                statuses[i].remaining -= statusDt;
                if ((StatusEffectType)i == StatusEffectType.Poison)
                {
                    statuses[i].tick += dt;
                    if (statuses[i].tick >= 0.5f)
                    {
                        statuses[i].tick -= 0.5f;
                        var info = new DamageInfo { amount = statuses[i].magnitude * 0.5f, source = BattleContext.Opponent(Side), hitPoint = transform.position };
                        ctx.damage.HitGuardian(this, statuses[i].magnitude * 0.5f, info, true);
                        if (!IsAlive) return;
                    }
                }
            }
            if (Definition.passiveKind == GuardianPassiveKind.HealOverTime && Health < MaxHealth)
                Health = Mathf.Min(MaxHealth, Health + Definition.passiveMagnitude * dt);
            if (Time.time > buffArmorUntil && !(TurnMode && ctx.turns.TurnIndex <= buffArmorTurn)) BuffArmorBonus = 0f;
            if (Time.time > buffAttackUntil && !(TurnMode && ctx.turns.TurnIndex <= buffAttackTurn)) BuffAttackMultiplier = 1f;

            if (flashUntil > 0f && Time.time >= flashUntil) { flashUntil = 0f; if (sprite != null) sprite.color = baseColor; }
            if (visualRoot != null && !QualityApplier.ReduceMotion && squashT < 0f)
            {
                float bob = IsStunned ? 0f : Mathf.Sin(Time.time * 2.2f + bobPhase) * 0.035f;
                var p = visualRoot.localPosition; p.y = bob; visualRoot.localPosition = p;
                if (IsStunned) visualRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 18f) * 6f);
                else if (visualRoot.localRotation != Quaternion.identity) visualRoot.localRotation = Quaternion.identity;
            }
            if (shieldVisual != null) shieldVisual.enabled = ShieldHealth > 0f;
            if (statusIcon != null) statusIcon.enabled = IsStunned || IsRooted || statuses[(int)StatusEffectType.Poison].remaining > 0f;
            UpdateBars();

            if (Definition.autoAttack && !ctx.turnBased && Cooldown <= 0f && !IsStunned && !IsRooted && (roster == null || !roster.IsPlayerControlling(this)))
                AutoFire();
        }

        void AutoFire()
        {
            if (ctx.targeting == null || ctx.projectiles == null || Definition.normalProjectile == null) return;
            if (!ctx.targeting.FindTarget(Side, Definition.preferredTarget, MuzzlePosition, Definition.range, out var aimPoint, out var targetCollider)) return;
            var velocity = ctx.projectiles.LaunchVelocity(Definition.normalProjectile, MuzzlePosition, aimPoint, 1f);
            float error = ctx.Range(-6f, 6f) * Mathf.Deg2Rad;
            velocity = Rotate(velocity, error);
            FireWithVelocity(velocity, false, targetCollider, true);
        }

        static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        public bool CanFire(bool special)
        {
            if (!IsAlive || !IsActive || IsStunned || IsRooted) return false;
            return special ? SpecialReady : (ctx.turnBased || Cooldown <= 0f);
        }

        /// Fires along a direction with a 0..1 power (manual aim). Returns false when not allowed.
        public bool Fire(Vector2 direction, float power, bool special)
        {
            if (!CanFire(special)) return false;
            var proj = special && Definition.specialProjectile != null ? Definition.specialProjectile : Definition.normalProjectile;
            if (special) return FireSpecial(direction, power);
            if (proj == null) return false;
            var velocity = direction.normalized * proj.speed * Mathf.Clamp(power, 0.35f, 1f);
            // Aimed shots always fly where they were aimed: no automatic homing target (only auto-attacks home).
            return FireWithVelocity(velocity, false, null);
        }

        bool FireWithVelocity(Vector2 velocity, bool special, Collider2D homing, bool auto = false)
        {
            var proj = Definition.normalProjectile;
            if (proj == null || ctx.projectiles == null) return false;
            bool crit = ctx.NextFloat() < CritChance;
            float mult = (crit ? CritMultiplier : 1f) * BuffAttackMultiplier * (auto ? ctx.balance.autoAttackDamageMultiplier : 1f);
            var p = ctx.projectiles.Fire(proj, MuzzlePosition, velocity, Side, this, special, false, mult, homing);
            if (p != null) p.IsCrit = crit;
            AnimalVisual.PlayAttack(customAnimator);
            ctx.vfx?.Muzzle(MuzzlePosition, velocity, proj.tint, special);
            Cooldown = CooldownDuration * (auto ? ctx.balance.autoAttackCooldownMultiplier : 1f);
            Services.Get<AudioService>()?.PlaySfx(Definition.attackSfx);
            return true;
        }

        bool FireSpecial(Vector2 direction, float power)
        {
            var def = Definition;
            var proj = def.specialProjectile != null ? def.specialProjectile : def.normalProjectile;
            var dir = direction.sqrMagnitude > 0.001f ? direction.normalized : (Side == BattleSide.Player ? Vector2.right : Vector2.left);
            bool crit = ctx.NextFloat() < CritChance;
            float critMult = crit ? CritMultiplier : 1f;
            bool fired = true;
            switch (def.specialKind)
            {
                case GuardianSpecialKind.PowerShot:
                case GuardianSpecialKind.RootTarget:
                case GuardianSpecialKind.PoisonArea:
                {
                    if (proj == null) return false;
                    var velocity = dir * proj.speed * Mathf.Clamp(power, 0.35f, 1f);
                    var p = ctx.projectiles.Fire(proj, MuzzlePosition, velocity, Side, this, true, false, Mathf.Max(1f, def.specialMagnitude) * critMult * BuffAttackMultiplier, null);
                    if (p != null) p.IsCrit = crit;
                    break;
                }
                case GuardianSpecialKind.Salvo:
                {
                    if (proj == null) return false;
                    int count = Mathf.Max(2, Mathf.RoundToInt(def.specialMagnitude));
                    float spread = 14f * Mathf.Deg2Rad;
                    for (int i = 0; i < count; i++)
                    {
                        float a = Mathf.Lerp(-spread, spread, count == 1 ? 0.5f : i / (float)(count - 1));
                        var v = Rotate(dir, a) * proj.speed * Mathf.Clamp(power, 0.35f, 1f) * ctx.Range(0.92f, 1.08f);
                        ctx.projectiles.Fire(proj, MuzzlePosition, v, Side, this, true, false, BuffAttackMultiplier, null);
                    }
                    break;
                }
                case GuardianSpecialKind.HealAllies:
                    roster?.HealAll(def.specialMagnitude, this);
                    ctx.TreeOf(Side)?.Core?.Heal(def.specialMagnitude * 0.5f);
                    ctx.vfx?.Heal(transform.position);
                    Services.Get<AudioService>()?.PlaySfx(AudioEventId.Heal);
                    break;
                case GuardianSpecialKind.ChainEnergy:
                    fired = ctx.targeting != null && ctx.targeting.ChainStrike(this, Mathf.Max(1, Mathf.RoundToInt(def.specialMagnitude)), Attack * 1.2f * BuffAttackMultiplier);
                    if (fired) Services.Get<AudioService>()?.PlaySfxAt(AudioEventId.LaunchMagic, MuzzlePosition);
                    break;
                case GuardianSpecialKind.ShieldSelf:
                    ShieldHealth += def.specialMagnitude;
                    ctx.vfx?.Burst(transform.position, new Color(0.6f, 0.9f, 1f), 1.2f);
                    Services.Get<AudioService>()?.PlaySfxAt(AudioEventId.ShieldUp, transform.position);
                    break;
                case GuardianSpecialKind.ArmorAllies:
                    roster?.ApplyArmorBuff(def.specialMagnitude, def.specialDuration);
                    ctx.vfx?.Burst(transform.position, new Color(0.7f, 0.85f, 1f), 1.4f);
                    Services.Get<AudioService>()?.PlaySfxAt(AudioEventId.SpecialBuff, transform.position);
                    break;
                case GuardianSpecialKind.ReduceToolCooldowns:
                    ctx.ToolsOf(Side)?.ReduceCooldowns(def.specialMagnitude);
                    ctx.vfx?.Burst(transform.position, new Color(1f, 0.9f, 0.5f), 1.2f);
                    Services.Get<AudioService>()?.PlaySfxAt(AudioEventId.SpecialBuff, transform.position);
                    break;
                case GuardianSpecialKind.TeamBuff:
                    roster?.ApplyAttackBuff(1f + def.specialMagnitude, def.specialDuration);
                    ctx.vfx?.Burst(transform.position, new Color(1f, 0.95f, 0.6f), 1.6f);
                    Services.Get<AudioService>()?.PlaySfxAt(AudioEventId.SpecialBuff, transform.position);
                    break;
                default:
                    fired = false;
                    break;
            }
            if (!fired) return false;
            AnimalVisual.PlayAttack(customAnimator);
            ctx.vfx?.Muzzle(MuzzlePosition, dir, proj != null ? proj.tint : new Color(1f, 0.9f, 0.5f), true);
            SpecialEnergy = 0f;
            Services.Get<AudioService>()?.PlaySfx(Definition.attackSfx);
            OnStateChanged?.Invoke(this);
            return true;
        }

        public void AddEnergy(float amount)
        {
            if (!IsAlive) return;
            SpecialEnergy = Mathf.Min(SpecialEnergyMax, SpecialEnergy + amount);
            CheckSpecialReadyCue();
        }

        bool specialReadyCued;

        /// A short chime the moment a player guardian's special fills (once per fill).
        void CheckSpecialReadyCue()
        {
            bool ready = SpecialReady;
            if (ready && !specialReadyCued && Side == BattleSide.Player) Services.Get<AudioService>()?.PlayUi(AudioEventId.SpecialReady);
            specialReadyCued = ready;
        }

        public void OnOwnProjectileHit()
        {
            AddEnergy(ctx.balance.specialEnergyPerHit);
            if (Definition.passiveKind == GuardianPassiveKind.ShieldOnHit) ShieldHealth += Attack * Definition.passiveMagnitude;
        }

        public float ApplyDamage(float amount, in DamageInfo info)
        {
            if (!IsAlive || amount <= 0f) return 0f;
            if (ShieldHealth > 0f)
            {
                float absorbed = Mathf.Min(ShieldHealth, amount);
                ShieldHealth -= absorbed;
                amount -= absorbed;
                if (amount <= 0f) { Flash(); return 0f; }
            }
            float before = Health;
            Health = Mathf.Max(0f, Health - amount);
            Flash();
            AddEnergy(ctx.balance.specialEnergyPerHit * 0.5f);
            if (Health <= 0f) Die();
            OnStateChanged?.Invoke(this);
            return before - Health;
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            Health = Mathf.Min(MaxHealth, Health + amount);
            OnStateChanged?.Invoke(this);
        }

        public void ApplyStatus(StatusEffectSpec spec)
        {
            if (!IsAlive || spec.type == StatusEffectType.None || spec.duration <= 0f) return;
            int i = (int)spec.type;
            if (i < 0 || i >= statuses.Length) return;
            switch (spec.type)
            {
                case StatusEffectType.Shield: ShieldHealth += spec.magnitude; return;
                case StatusEffectType.Heal: Heal(spec.magnitude); return;
                case StatusEffectType.ArmorBuff: ApplyArmorBuff(spec.magnitude, spec.duration); return;
            }
            statuses[i].remaining = Mathf.Max(statuses[i].remaining, spec.duration);
            statuses[i].magnitude = Mathf.Max(statuses[i].magnitude, spec.magnitude);
        }

        public void ApplyArmorBuff(float bonus, float duration)
        {
            BuffArmorBonus = Mathf.Max(BuffArmorBonus, bonus);
            buffArmorUntil = Mathf.Max(buffArmorUntil, Time.time + duration);
            if (TurnMode) buffArmorTurn = Mathf.Max(buffArmorTurn, NextOwnTurn);
        }

        public void ApplyAttackBuff(float multiplier, float duration)
        {
            BuffAttackMultiplier = Mathf.Max(BuffAttackMultiplier, multiplier);
            buffAttackUntil = Mathf.Max(buffAttackUntil, Time.time + duration);
            if (TurnMode) buffAttackTurn = Mathf.Max(buffAttackTurn, NextOwnTurn);
        }

        public void Stun(float seconds)
        {
            statuses[(int)StatusEffectType.Stun].remaining = Mathf.Max(statuses[(int)StatusEffectType.Stun].remaining, seconds);
        }

        void Flash()
        {
            flashUntil = Time.time + 0.08f;
            if (sprite != null) sprite.color = Color.white;
            flashAmount = 1f;
            ApplyFlash(1f);
            if (!QualityApplier.ReduceMotion) squashT = 0f;
        }

        void Die()
        {
            if (Definition.passiveKind == GuardianPassiveKind.Rebirth && !RebirthUsed)
            {
                RebirthUsed = true;
                Health = MaxHealth * 0.5f;
                ShieldHealth = 0f;
                for (int i = 0; i < statuses.Length; i++) statuses[i] = default;
                ctx.vfx?.Burst(transform.position, new Color(0.7f, 1f, 0.7f), 1.8f);
                Services.Get<AudioService>()?.PlaySfxAt(AudioEventId.Heal, transform.position, 1f, 1.2f);
                return;
            }
            IsAlive = false;
            if (bodyCollider != null) bodyCollider.enabled = false;
            if (sprite != null) sprite.color = new Color(0.4f, 0.4f, 0.4f, 0.35f);
            if (selectionRing != null) selectionRing.enabled = false;
            if (shieldVisual != null) shieldVisual.enabled = false;
            if (statusIcon != null) statusIcon.enabled = false;
            if (platform != null) platform.enabled = false;
            // Pop then vanish (driven in TickVisuals), with a poof at the body.
            if (customVisual != null || sprite != null) dyingT = QualityApplier.ReduceMotion ? 0.3f : 0f;
            var center = bodyCollider != null ? (Vector2)bodyCollider.bounds.center : (Vector2)transform.position;
            ctx.vfx?.GuardianPoof(center, Definition.tintColor);
            Services.Get<AudioService>()?.PlaySfxAt(AudioEventId.GuardianDeath, transform.position);
            if (Side == BattleSide.Player) Services.Get<HapticService>()?.Medium();
            roster?.NotifyDefeated(this);
            GameEventBus.Publish(new GuardianDefeatedEvent { side = Side, guardianId = Definition.id });
            OnStateChanged?.Invoke(this);
        }

        /// Called when the branch under this guardian breaks.
        public void OnBranchBroken(BranchBreakBehavior behavior)
        {
            if (!IsAlive || !IsActive) return;
            if (Definition.isFlying && behavior != BranchBreakBehavior.Eliminate)
            {
                Stun(0.6f);
                return;
            }
            switch (behavior)
            {
                case BranchBreakBehavior.Eliminate:
                    Health = 0f;
                    Die();
                    return;
                case BranchBreakBehavior.FallWithDamage:
                    FallDamage();
                    if (!IsAlive) return;
                    if (roster == null || !roster.TryRelocate(this)) { Health = 0f; Die(); }
                    return;
                case BranchBreakBehavior.HoverIfFlying:
                case BranchBreakBehavior.FallToEmptySlot:
                default:
                    FallDamage();
                    if (!IsAlive) return;
                    if (roster == null || !roster.TryRelocate(this)) { Health = 0f; Die(); }
                    return;
            }
        }

        void FallDamage()
        {
            var info = new DamageInfo { amount = MaxHealth * ctx.balance.branchBreakDamagePercent, source = BattleContext.Opponent(Side), isSplash = true, hitPoint = transform.position };
            ApplyDamage(MaxHealth * ctx.balance.branchBreakDamagePercent, info);
            Stun(ctx.balance.branchBreakStunSeconds);
        }

        public void MoveToSlot(int slot, Transform slotTransform)
        {
            SlotIndex = slot;
            if (slotTransform != null)
            {
                transform.SetParent(slotTransform, false);
                transform.localPosition = Vector3.zero;
            }
            OnStateChanged?.Invoke(this);
        }

        float barsAlpha = -1f, barsUntil, lastBarHealth = -1f;

        void UpdateBars()
        {
            float hp = HealthPercent;
            if (lastBarHealth >= 0f && hp < lastBarHealth - 0.0001f) barsUntil = Time.time + barsShowAfterHit;
            lastBarHealth = hp;
            if (barRenderers != null && barRenderers.Length > 0)
            {
                bool show = IsAlive && (ctx == null || !ctx.turnBased || sortingOffset != 0 || Time.time < barsUntil);
                float target = show ? 1f : 0f;
                barsAlpha = barsAlpha < 0f ? target : Mathf.MoveTowards(barsAlpha, target, Time.deltaTime * 6f);
            }
            if (healthBarFill != null)
            {
                var s = healthBarFill.localScale; s.x = fillBaseScaleX * HealthPercent; healthBarFill.localScale = s;
                if (healthBarFillRenderer != null && healthGradient != null) healthBarFillRenderer.color = healthGradient.Evaluate(HealthPercent);
            }
            if (energyBarFill != null)
            {
                var s = energyBarFill.localScale; s.x = energyBaseScaleX * EnergyPercent; energyBarFill.localScale = s;
            }
            if (barsAlpha >= 0f && barRenderers != null)
                for (int i = 0; i < barRenderers.Length; i++)
                {
                    var r = barRenderers[i];
                    if (r == null) continue;
                    var c = r.color;
                    if (!Mathf.Approximately(c.a, barsAlpha)) { c.a = barsAlpha; r.color = c; }
                }
        }
    }
}
