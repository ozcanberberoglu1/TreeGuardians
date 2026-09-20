using System;
using System.Collections.Generic;
using TreeGuardians.Battle;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Trees;
using UnityEngine;

namespace TreeGuardians.Guardians
{
    /// One side's eight pre-authored guardian views; handles activation, ticking, buffs and branch-break relocation.
    public sealed class GuardianRoster : MonoBehaviour
    {
        [SerializeField] BattleSide side = BattleSide.Player;
        [SerializeField] GuardianController[] slots = new GuardianController[8];

        public BattleSide Side => side;
        public IReadOnlyList<GuardianController> Slots => slots;
        public float SapFlowBonus { get; private set; }
        public int ActiveCount { get; private set; }

        public event Action<GuardianController> OnGuardianDefeated;
        public event Action OnRosterChanged;

        BattleContext ctx;
        TreeController tree;
        int playerControlledSlot = -1;

        public void Initialize(string[] ids, int[] levels, int fallbackLevel, int activeSlots, BattleContext context, TreeController ownTree)
        {
            ctx = context;
            tree = ownTree;
            SapFlowBonus = tree != null ? tree.SapFlowBonus : 0f;
            ActiveCount = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                var view = slots[i];
                if (view == null) continue;
                var def = ids != null && i < ids.Length ? ctx.database.GetGuardian(ids[i]) : null;
                bool active = def != null && i < activeSlots;
                if (!active) { view.Deactivate(); continue; }
                int level = levels != null && i < levels.Length && levels[i] > 0 ? levels[i] : fallbackLevel;
                var slotT = tree != null ? tree.GetSlot(i) : null;
                if (slotT != null) { view.transform.SetParent(slotT, false); view.transform.localPosition = Vector3.zero; }
                view.Setup(def, level, side, i, ctx, this);
                ActiveCount++;
            }
            if (tree != null)
            {
                tree.OnBranchBroken -= HandleBranchBroken;
                tree.OnBranchBroken += HandleBranchBroken;
            }
            RecomputeAuras();
        }

        public void Tick(float dt)
        {
            var oppTools = ctx != null ? ctx.ToolsOf(BattleContext.Opponent(side)) : null;
            for (int i = 0; i < slots.Length; i++)
            {
                var g = slots[i];
                if (g == null || !g.IsActive || !g.IsAlive) continue;
                if (oppTools != null && oppTools.IsSlowedAt(g.transform.position, out float slow)) g.SetExternalSlow(slow);
                else g.SetExternalSlow(0f);
                g.Tick(dt);
            }
        }

        public GuardianController Get(int slot) => slot >= 0 && slot < slots.Length ? slots[slot] : null;

        public GuardianController GetBySlotPosition(int slot)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null && slots[i].IsActive && slots[i].IsAlive && slots[i].SlotIndex == slot) return slots[i];
            return null;
        }

        public int AliveCount
        {
            get
            {
                int c = 0;
                for (int i = 0; i < slots.Length; i++) if (slots[i] != null && slots[i].IsActive && slots[i].IsAlive) c++;
                return c;
            }
        }

        public void GetAlive(List<GuardianController> into)
        {
            into.Clear();
            for (int i = 0; i < slots.Length; i++) if (slots[i] != null && slots[i].IsActive && slots[i].IsAlive) into.Add(slots[i]);
        }

        public void SetPlayerControlled(int slot)
        {
            playerControlledSlot = slot;
            for (int i = 0; i < slots.Length; i++) slots[i]?.SetSelected(i == slot);
        }

        public bool IsPlayerControlling(GuardianController g) => g != null && playerControlledSlot >= 0 && g == slots[playerControlledSlot];

        public void HealAll(float amount, GuardianController except)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var g = slots[i];
                if (g == null || !g.IsActive || !g.IsAlive) continue;
                g.Heal(amount * (g == except ? 0.5f : 1f));
                ctx.vfx?.Heal(g.transform.position);
            }
        }

        public void ApplyArmorBuff(float bonus, float duration)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null && slots[i].IsActive && slots[i].IsAlive) slots[i].ApplyArmorBuff(bonus, duration);
        }

        public void ApplyAttackBuff(float multiplier, float duration)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null && slots[i].IsActive && slots[i].IsAlive) slots[i].ApplyAttackBuff(multiplier, duration);
        }

        void RecomputeAuras()
        {
            float armorAura = 0f;
            float toolCd = 0f;
            for (int i = 0; i < slots.Length; i++)
            {
                var g = slots[i];
                if (g == null || !g.IsActive || !g.IsAlive) continue;
                if (g.Definition.passiveKind == GuardianPassiveKind.ArmorAura) armorAura += g.Definition.passiveMagnitude;
                if (g.Definition.passiveKind == GuardianPassiveKind.ToolCooldownReduction) toolCd += g.Definition.passiveMagnitude;
            }
            if (armorAura > 0f) ApplyArmorBuff(armorAura, 99999f);
            ctx?.ToolsOf(side)?.SetCooldownMultiplier(Mathf.Clamp(1f - toolCd, 0.4f, 1f));
        }

        void HandleBranchBroken(TreeSection branch)
        {
            var g = GetBySlotPosition(branch.GuardianSlotIndex);
            if (g == null) return;
            g.OnBranchBroken(branch.Spec != null ? branch.Spec.breakBehavior : BranchBreakBehavior.FallToEmptySlot);
            OnRosterChanged?.Invoke();
        }

        /// Moves a guardian to the closest lower, empty, supported slot. Returns false when none exists.
        public bool TryRelocate(GuardianController g)
        {
            if (tree == null || g == null) return false;
            var current = tree.GetSlot(g.SlotIndex);
            float currentY = current != null ? current.position.y : g.transform.position.y;
            int best = -1;
            float bestY = float.MinValue;
            for (int i = 0; i < tree.GuardianSlots.Length; i++)
            {
                var t = tree.GuardianSlots[i];
                if (t == null || i == g.SlotIndex) continue;
                if (t.position.y >= currentY - 0.05f) continue;
                if (!tree.IsSlotSupported(i)) continue;
                if (GetBySlotPosition(i) != null) continue;
                if (t.position.y > bestY) { bestY = t.position.y; best = i; }
            }
            if (best < 0) return false;
            g.MoveToSlot(best, tree.GetSlot(best));
            return true;
        }

        public void NotifyDefeated(GuardianController g)
        {
            if (ctx != null) ctx.StatsOf(BattleContext.Opponent(side)).guardiansDefeated++;
            OnGuardianDefeated?.Invoke(g);
            OnRosterChanged?.Invoke();
            RecomputeAuras();
        }
    }
}
