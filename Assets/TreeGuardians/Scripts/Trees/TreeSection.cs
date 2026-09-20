using System;
using System.Collections.Generic;
using TreeGuardians.Battle;
using TreeGuardians.Core;
using TreeGuardians.Data;
using UnityEngine;

namespace TreeGuardians.Trees
{
    /// One damageable part of a tree. Pre-authored in the scene; numbers come from TreeDefinition at battle start.
    public sealed class TreeSection : MonoBehaviour
    {
        [Header("Identity (matches TreeDefinition.sections)")]
        [SerializeField] string sectionId = "section";
        [SerializeField] TreeSectionType type = TreeSectionType.BarkArmor;
        [SerializeField] int guardianSlotIndex = -1;

        [Header("Visuals")]
        [SerializeField] SpriteRenderer[] renderers = new SpriteRenderer[0];
        [Tooltip("Sağlam, çatlak, ağır hasarlı sprite'lar (boşsa renk koyulaşır).")]
        [SerializeField] Sprite[] damageStateSprites = new Sprite[3];
        [SerializeField] Collider2D sectionCollider;
        [SerializeField] Transform damageVfxAnchor;
        [SerializeField] Color destroyedTint = new Color(0.3f, 0.3f, 0.3f, 0.35f);

        public string SectionId => sectionId;
        public TreeSectionType Type => type;
        public int GuardianSlotIndex => guardianSlotIndex;
        public Collider2D Collider => sectionCollider;
        public Transform VfxAnchor => damageVfxAnchor != null ? damageVfxAnchor : transform;

        public BattleSide Side { get; private set; }
        public TreeController Tree { get; private set; }
        public TreeSectionSpec Spec { get; private set; }
        public float MaxHealth { get; private set; }
        public float Health { get; private set; }
        public float Armor { get; private set; }
        public float PoisonResistance { get; private set; }
        public bool IsDestroyed { get; private set; }
        public bool InitiallyTargetable { get; private set; }
        public float HealthPercent => MaxHealth > 0f ? Health / MaxHealth : 0f;

        public TreeSection Parent { get; internal set; }
        public readonly List<TreeSection> Children = new List<TreeSection>(4);
        public readonly List<TreeSection> Protectors = new List<TreeSection>(4);
        public readonly List<TreeSection> Protects = new List<TreeSection>(4);

        public float ShieldHealth { get; private set; }
        public float ShieldExpiresAt { get; private set; }

        public event Action<TreeSection, DamageInfo> OnDamaged;
        public event Action<TreeSection> OnDestroyedEvent;

        Color[] baseColors;
        float flashUntil;
        int damageStage;
        bool wasProtected;

        public bool IsProtected
        {
            get
            {
                for (int i = 0; i < Protectors.Count; i++)
                    if (!Protectors[i].IsDestroyed) return true;
                return false;
            }
        }

        public bool IsTargetable => !IsDestroyed && (InitiallyTargetable || !IsProtected);

        public bool HasActiveShield => ShieldHealth > 0f && Time.time < ShieldExpiresAt;

        public void Initialize(TreeSectionSpec spec, BattleSide side, TreeController tree, float healthMultiplier, float armorBonus)
        {
            Spec = spec;
            Side = side;
            Tree = tree;
            sectionId = spec.sectionId;
            type = spec.type;
            guardianSlotIndex = spec.guardianSlotIndex;
            MaxHealth = Mathf.Max(1f, spec.baseHealth * healthMultiplier);
            Health = MaxHealth;
            Armor = Mathf.Max(0f, spec.armor + armorBonus);
            PoisonResistance = Mathf.Clamp01(spec.poisonResistance);
            InitiallyTargetable = spec.initiallyTargetable;
            IsDestroyed = false;
            ShieldHealth = 0f;
            damageStage = 0;
            Children.Clear();
            Protectors.Clear();
            Protects.Clear();
            Parent = null;
            if (baseColors == null || baseColors.Length != renderers.Length)
            {
                baseColors = new Color[renderers.Length];
                for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i] != null ? renderers[i].color : Color.white;
            }
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].enabled = true;
                renderers[i].color = baseColors[i];
                if (damageStateSprites != null && damageStateSprites.Length > 0 && damageStateSprites[0] != null) renderers[i].sprite = damageStateSprites[0];
            }
            if (sectionCollider != null) sectionCollider.enabled = true;
            gameObject.SetActive(true);
        }

        public void SetBaseColor(Color color)
        {
            if (baseColors == null || baseColors.Length != renderers.Length) baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                baseColors[i] = color;
                if (renderers[i] != null && !IsDestroyed) renderers[i].color = color;
            }
        }

        public void SetSprites(Sprite intact, Sprite cracked, Sprite heavy)
        {
            if (damageStateSprites == null || damageStateSprites.Length != 3) damageStateSprites = new Sprite[3];
            damageStateSprites[0] = intact;
            damageStateSprites[1] = cracked != null ? cracked : intact;
            damageStateSprites[2] = heavy != null ? heavy : damageStateSprites[1];
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null && intact != null) renderers[i].sprite = intact;
        }

        public void AddShield(float amount, float duration)
        {
            ShieldHealth = Mathf.Max(ShieldHealth, amount);
            ShieldExpiresAt = Time.time + duration;
        }

        /// Applies already-resolved damage. Returns the amount actually removed from health.
        public float ApplyDamage(float amount, in DamageInfo info)
        {
            if (IsDestroyed || amount <= 0f) return 0f;
            if (HasActiveShield)
            {
                float absorbed = Mathf.Min(ShieldHealth, amount);
                ShieldHealth -= absorbed;
                amount -= absorbed;
                if (amount <= 0f) { Flash(); return 0f; }
            }
            float before = Health;
            Health = Mathf.Max(0f, Health - amount);
            float applied = before - Health;
            Flash();
            UpdateDamageStage();
            OnDamaged?.Invoke(this, info);
            if (Health <= 0f) Destroy(info);
            return applied;
        }

        public void Heal(float amount)
        {
            if (IsDestroyed || amount <= 0f) return;
            Health = Mathf.Min(MaxHealth, Health + amount);
            UpdateDamageStage();
        }

        void Destroy(in DamageInfo info)
        {
            if (IsDestroyed) return;
            IsDestroyed = true;
            if (sectionCollider != null) sectionCollider.enabled = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].color = destroyedTint;
                if (damageStateSprites != null && damageStateSprites.Length > 2 && damageStateSprites[2] != null) renderers[i].sprite = damageStateSprites[2];
            }
            OnDestroyedEvent?.Invoke(this);
            GameEventBus.Publish(new SectionDestroyedEvent { type = type, side = Side, sectionId = sectionId });
        }

        void UpdateDamageStage()
        {
            int stage = HealthPercent > 0.66f ? 0 : HealthPercent > 0.33f ? 1 : 2;
            if (stage == damageStage) return;
            damageStage = stage;
            if (damageStateSprites == null || damageStateSprites.Length < 3) return;
            var s = damageStateSprites[stage] != null ? damageStateSprites[stage] : damageStateSprites[0];
            if (s == null) return;
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].sprite = s;
        }

        void Flash()
        {
            flashUntil = Time.time + 0.08f;
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].color = Color.white;
        }

        /// Called by TreeController each frame; restores colours after a hit flash and reacts to protection changes.
        public void Tick()
        {
            if (flashUntil > 0f && Time.time >= flashUntil)
            {
                flashUntil = 0f;
                if (!IsDestroyed)
                    for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].color = baseColors[i];
            }
            if (!IsDestroyed && type == TreeSectionType.HeartwoodCore)
            {
                bool protectedNow = IsProtected;
                if (wasProtected && !protectedNow) GameEventBus.Publish(new CoreExposedEvent { side = Side });
                wasProtected = protectedNow;
            }
        }

        public void ResetProtectionState() => wasProtected = IsProtected;

        void OnValidate()
        {
            if (sectionCollider == null) sectionCollider = GetComponent<Collider2D>();
            if (renderers == null || renderers.Length == 0)
            {
                var sr = GetComponent<SpriteRenderer>();
                if (sr != null) renderers = new[] { sr };
            }
        }
    }
}
