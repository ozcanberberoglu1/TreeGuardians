using System;
using System.Collections.Generic;
using TreeGuardians.Battle;
using TreeGuardians.Core;
using TreeGuardians.Data;
using UnityEngine;

namespace TreeGuardians.Trees
{
    /// A whole tree: pre-authored sections, guardian slots, tool mounts and the Heartwood core.
    public sealed class TreeController : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] TreeDefinition definition;
        [SerializeField] BattleSide side = BattleSide.Player;
        [SerializeField] TreeSection[] sections = new TreeSection[0];
        [SerializeField] TreeSection core;
        [SerializeField] Transform[] guardianSlots = new Transform[8];
        [SerializeField] Transform[] toolMounts = new Transform[3];

        [Header("Tinted visuals")]
        [SerializeField] SpriteRenderer[] barkRenderers = new SpriteRenderer[0];
        [SerializeField] SpriteRenderer[] leafRenderers = new SpriteRenderer[0];
        [SerializeField] SpriteRenderer heartwoodGlow;

        [Header("Castle (destructible)")]
        [Tooltip("Parçalar delinebilir kale duvarlarıdır: paylaşılan maske, arkada siyah katman, önde maskeli duvar.")] [SerializeField] bool destructible;
        [Tooltip("Ağaç kademesine göre renk uygula (kullanıcı sanatı için kapalı).")] [SerializeField] bool tintByTier = true;
        [SerializeField] float maskPixelsPerUnit = 48f;
        [SerializeField] float maskMargin = 0.4f;
        [SerializeField] Color silhouetteColor = new Color(0.05f, 0.04f, 0.07f, 1f);
        [SerializeField] float silhouetteFadeSeconds = 0.2f;

        [Header("World health bar")]
        [SerializeField] Transform healthBarFill;
        [SerializeField] SpriteRenderer healthBarFillRenderer;
        [SerializeField] Gradient healthGradient;

        public TreeDefinition Definition => definition;
        public BattleSide Side => side;
        public TreeSection Core => core;
        public IReadOnlyList<TreeSection> Sections => sections;
        public Transform[] GuardianSlots => guardianSlots;
        public Transform[] ToolMounts => toolMounts;
        public bool IsCoreDestroyed => core != null && core.IsDestroyed;
        /// Core health, or castle integrity (1 - structure damage) when the tree has no core.
        public float CorePercent => core != null ? core.HealthPercent : 1f - StructureDamagePercent();
        public bool IsDestructible => destructible && mask != null;
        public DestructibleMask Mask => mask;
        public bool Silhouetted => silhouetteTarget > 0.5f;
        public float RootDamageReduction { get; private set; }
        public float SapFlowBonus { get; private set; }
        public float StructureHealthTotal { get; private set; }

        public event Action<TreeSection> OnBranchBroken;
        public event Action<TreeSection> OnSectionDestroyed;
        public event Action OnCoreDestroyed;

        readonly Dictionary<string, TreeSection> byId = new Dictionary<string, TreeSection>(32);
        readonly List<TreeSection> targetable = new List<TreeSection>(32);
        float fillBaseScaleX = 1f;
        bool initialized;
        DestructibleMask mask;
        float silhouette;
        float silhouetteTarget;
        Coroutine silhouetteTween;

        void OnDestroy()
        {
            mask?.Dispose();
            mask = null;
        }

        public void Initialize(int[] upgradeLevels, TreeVisualTier tier, BattleContext context)
        {
            if (definition == null) { TGLog.Error($"TreeController ({side}) has no TreeDefinition."); return; }
            int Lvl(TreeUpgradePath p) => upgradeLevels != null && (int)p < upgradeLevels.Length ? Mathf.Max(0, upgradeLevels[(int)p]) : 0;
            float coreMult = 1f + definition.heartwoodHealthPerLevel * Lvl(TreeUpgradePath.HeartwoodLevel);
            float armorBonus = definition.barkArmorPerLevel * Lvl(TreeUpgradePath.BarkThickness);
            float branchMult = 1f + definition.branchHealthPerLevel * Lvl(TreeUpgradePath.BranchDurability);
            RootDamageReduction = Mathf.Clamp01(definition.rootDamageReductionPerLevel * Lvl(TreeUpgradePath.RootStrength));
            SapFlowBonus = definition.sapFlowEnergyPerLevel * Lvl(TreeUpgradePath.SapFlow);

            byId.Clear();
            StructureHealthTotal = 0f;
            var visuals = definition.GetVisuals(tier);
            for (int i = 0; i < sections.Length; i++)
            {
                var s = sections[i];
                if (s == null) continue;
                var spec = definition.FindSection(s.SectionId);
                if (spec == null) { TGLog.Warn($"Tree {side}: section '{s.SectionId}' not in definition; hiding."); s.gameObject.SetActive(false); continue; }
                float mult = spec.type == TreeSectionType.HeartwoodCore ? coreMult : spec.type == TreeSectionType.Branch ? branchMult : 1f;
                s.Initialize(spec, side, this, mult, spec.type == TreeSectionType.HeartwoodCore ? 0f : armorBonus);
                s.OnDestroyedEvent -= HandleSectionDestroyed;
                s.OnDestroyedEvent += HandleSectionDestroyed;
                byId[s.SectionId] = s;
                if (CountsForStructure(spec.type)) StructureHealthTotal += s.MaxHealth;
                if (visuals != null && tintByTier) ApplyVisual(s, visuals);
            }
            if (destructible) SetupMask();
            foreach (var kv in byId)
            {
                var s = kv.Value;
                if (!string.IsNullOrEmpty(s.Spec.parentSectionId) && byId.TryGetValue(s.Spec.parentSectionId, out var parent))
                {
                    s.Parent = parent;
                    parent.Children.Add(s);
                }
                for (int i = 0; i < s.Spec.protectsSectionIds.Length; i++)
                {
                    if (!byId.TryGetValue(s.Spec.protectsSectionIds[i], out var protectedSection)) continue;
                    s.Protects.Add(protectedSection);
                    protectedSection.Protectors.Add(s);
                }
            }
            foreach (var kv in byId) kv.Value.ResetProtectionState();
            if (core == null) foreach (var kv in byId) if (kv.Value.Type == TreeSectionType.HeartwoodCore) core = kv.Value;
            if (visuals != null && tintByTier)
            {
                foreach (var r in barkRenderers) if (r != null) r.color = visuals.barkColor;
                foreach (var r in leafRenderers) if (r != null) r.color = visuals.leafColor;
            }
            if (healthBarFill != null) fillBaseScaleX = healthBarFill.localScale.x == 0f ? 1f : Mathf.Abs(healthBarFill.localScale.x);
            initialized = true;
            UpdateHealthBar();
        }

        static bool IsCarvable(TreeSection s) => s != null && s.Type != TreeSectionType.RootStabilizer && s.Type != TreeSectionType.HeartwoodCore;

        /// Castles: only the wall parts count for integrity (the base is solid scenery); legacy trees: everything but the core.
        bool CountsForStructure(TreeSectionType type) => type != TreeSectionType.HeartwoodCore && !(destructible && type == TreeSectionType.RootStabilizer);

        /// Castle duel: every wall part has lost all its health.
        public bool IsCastleDestroyed
        {
            get
            {
                if (!destructible) return false;
                bool any = false;
                for (int i = 0; i < sections.Length; i++)
                {
                    var s = sections[i];
                    if (s == null || !IsCarvable(s) || !s.gameObject.activeSelf) continue;
                    any = true;
                    if (!s.IsDestroyed) return false;
                }
                return any;
            }
        }

        void SetupMask()
        {
            var rect = new Rect(0f, 0f, 0f, 0f);
            bool any = false;
            for (int i = 0; i < sections.Length; i++)
            {
                var s = sections[i];
                if (s == null || !s.gameObject.activeSelf) continue;
                var b = s.WorldBounds;
                var r = Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
                if (!any) { rect = r; any = true; }
                else rect = Rect.MinMaxRect(Mathf.Min(rect.xMin, r.xMin), Mathf.Min(rect.yMin, r.yMin), Mathf.Max(rect.xMax, r.xMax), Mathf.Max(rect.yMax, r.yMax));
            }
            if (!any) return;
            rect = Rect.MinMaxRect(rect.xMin - maskMargin, rect.yMin - maskMargin, rect.xMax + maskMargin, rect.yMax + maskMargin);
            mask?.Dispose();
            mask = new DestructibleMask(rect, maskPixelsPerUnit);
            for (int i = 0; i < sections.Length; i++)
            {
                var s = sections[i];
                if (s == null) continue;
                s.SetMask(mask.Texture, mask.ShaderRect, IsCarvable(s));
                s.SetSilhouette(0f);
            }
            silhouette = 0f;
            silhouetteTarget = 0f;
        }

        /// Opens a hole in the wall at a world point (projectile impact).
        public void Carve(Vector2 world, float radius)
        {
            if (mask == null || radius <= 0f) return;
            mask.Carve(world, radius);
            mask.Apply();
        }

        /// True when the wall is already open at this point (projectiles fly through).
        public bool IsHoleAt(Vector2 world) => mask != null && mask.IsDestroyedAt(world);

        /// True when an intact wall part still covers this point (guardians behind it cannot be hit).
        public bool IsCoveredAt(Vector2 world)
        {
            if (!IsDestructible) return false;
            for (int i = 0; i < sections.Length; i++)
            {
                var s = sections[i];
                if (s == null || s.IsDestroyed || !IsCarvable(s)) continue;
                if (!s.WorldBounds.Contains(new Vector3(world.x, world.y, s.WorldBounds.center.z))) continue;
                return !mask.IsDestroyedAt(world);
            }
            return false;
        }

        /// Fades the carvable walls to a flat silhouette so the guardians inside can be seen (player aiming).
        public void SetSilhouette(bool on, bool instant = false)
        {
            silhouetteTarget = on ? 1f : 0f;
            TGTween.Stop(silhouetteTween);
            silhouetteTween = TGTween.FloatTo(silhouette, silhouetteTarget, instant || QualityApplier.ReduceMotion ? 0f : silhouetteFadeSeconds, ApplySilhouette);
        }

        void ApplySilhouette(float value)
        {
            silhouette = value;
            for (int i = 0; i < sections.Length; i++)
            {
                var s = sections[i];
                if (s == null || !IsCarvable(s)) continue;
                s.SetSilhouette(value);
            }
        }

        void ApplyVisual(TreeSection s, TreeVisualSet v)
        {
            switch (s.Type)
            {
                case TreeSectionType.Trunk:
                case TreeSectionType.RootStabilizer:
                case TreeSectionType.Branch:
                case TreeSectionType.BarkArmor:
                    s.SetBaseColor(v.barkColor);
                    break;
                case TreeSectionType.CanopyShield:
                    s.SetBaseColor(v.leafColor);
                    break;
                case TreeSectionType.HeartwoodCore:
                    s.SetBaseColor(Color.white);
                    break;
            }
        }

        void HandleSectionDestroyed(TreeSection s)
        {
            if (mask != null && IsCarvable(s))
            {
                var b = s.WorldBounds;
                mask.ClearRect(Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y));
                mask.Apply();
            }
            OnSectionDestroyed?.Invoke(s);
            if (s.Type == TreeSectionType.Branch) OnBranchBroken?.Invoke(s);
            if (s.Type == TreeSectionType.HeartwoodCore) OnCoreDestroyed?.Invoke();
            for (int i = 0; i < s.Children.Count; i++)
            {
                var child = s.Children[i];
                if (child.IsDestroyed || child.Type == TreeSectionType.HeartwoodCore) continue;
                var cascade = new DamageInfo { amount = child.Health, source = BattleContext.Opponent(side), isSplash = true };
                child.ApplyDamage(child.Health + 1f, cascade);
            }
            UpdateHealthBar();
        }

        public TreeSection GetSection(string id) => byId.TryGetValue(id, out var s) ? s : null;

        public TreeSection GetBranchForSlot(int slot)
        {
            for (int i = 0; i < sections.Length; i++)
                if (sections[i] != null && sections[i].Type == TreeSectionType.Branch && sections[i].GuardianSlotIndex == slot) return sections[i];
            return null;
        }

        public bool IsSlotSupported(int slot)
        {
            var b = GetBranchForSlot(slot);
            return b == null || !b.IsDestroyed;
        }

        public Transform GetSlot(int slot) => slot >= 0 && slot < guardianSlots.Length ? guardianSlots[slot] : null;

        /// Fills the list with sections that can currently be hit (destroyed ones excluded).
        public void GetAliveSections(List<TreeSection> into, bool onlyTargetable)
        {
            into.Clear();
            for (int i = 0; i < sections.Length; i++)
            {
                var s = sections[i];
                if (s == null || s.IsDestroyed || !s.gameObject.activeInHierarchy) continue;
                if (onlyTargetable && !s.IsTargetable) continue;
                into.Add(s);
            }
        }

        public float StructureHealthRemaining()
        {
            float t = 0f;
            for (int i = 0; i < sections.Length; i++)
            {
                var s = sections[i];
                if (s == null || !CountsForStructure(s.Type)) continue;
                t += s.Health;
            }
            return t;
        }

        public float StructureDamagePercent() => StructureHealthTotal > 0f ? 1f - StructureHealthRemaining() / StructureHealthTotal : 0f;

        public void Tick()
        {
            if (!initialized) return;
            for (int i = 0; i < sections.Length; i++) sections[i]?.Tick();
            if (heartwoodGlow != null && core != null)
            {
                bool exposed = !core.IsDestroyed && !core.IsProtected;
                float pulse = exposed ? 0.75f + Mathf.Sin(Time.time * 6f) * 0.25f : 0.35f;
                var c = heartwoodGlow.color; c.a = pulse; heartwoodGlow.color = c;
            }
        }

        public void UpdateHealthBar()
        {
            if (healthBarFill == null) return;
            float p = Mathf.Clamp01(CorePercent);
            var s = healthBarFill.localScale;
            s.x = fillBaseScaleX * p;
            healthBarFill.localScale = s;
            if (healthBarFillRenderer != null && healthGradient != null) healthBarFillRenderer.color = healthGradient.Evaluate(p);
        }

        public void RefreshVisualsForEditor()
        {
            if (definition == null) return;
            var v = definition.GetVisuals(TreeVisualTier.Sprouting);
            if (v == null) return;
            foreach (var r in barkRenderers) if (r != null) r.color = v.barkColor;
            foreach (var r in leafRenderers) if (r != null) r.color = v.leafColor;
        }
    }
}
