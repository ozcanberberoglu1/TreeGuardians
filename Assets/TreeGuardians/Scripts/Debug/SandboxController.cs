using TMPro;
using TreeGuardians.Battle;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.Debugging
{
    /// Developer sandbox: swap guardians into player slots, fire any projectile, damage sections, use tools, force outcomes.
    public sealed class SandboxController : MonoBehaviour
    {
        [SerializeField] BattleManager manager;
        [SerializeField] UIPanel panel;
        [SerializeField] Button toggleButton;
        [SerializeField] Button[] guardianButtons = new Button[0];
        [SerializeField] string[] guardianIds = new string[0];
        [SerializeField] Button[] projectileButtons = new Button[0];
        [SerializeField] string[] projectileIds = new string[0];
        [SerializeField] Button[] toolButtons = new Button[0];
        [SerializeField] string[] toolIds = new string[0];
        [SerializeField] Button damageSectionButton;
        [SerializeField] Button breakBranchButton;
        [SerializeField] Button healAllButton;
        [SerializeField] Button fillEnergyButton;
        [SerializeField] Button winButton;
        [SerializeField] Button loseButton;
        [SerializeField] Button addTimeButton;
        [SerializeField] Button resetButton;
        [SerializeField] TMP_Text infoText;

        int nextSlot;
        float infoTimer;

        void Awake()
        {
            if (toggleButton != null) toggleButton.onClick.AddListener(() => panel?.Toggle());
            for (int i = 0; i < guardianButtons.Length; i++) { int idx = i; guardianButtons[i]?.onClick.AddListener(() => AssignGuardian(idx)); }
            for (int i = 0; i < projectileButtons.Length; i++) { int idx = i; projectileButtons[i]?.onClick.AddListener(() => FireProjectile(idx)); }
            for (int i = 0; i < toolButtons.Length; i++) { int idx = i; toolButtons[i]?.onClick.AddListener(() => UseTool(idx)); }
            if (damageSectionButton != null) damageSectionButton.onClick.AddListener(DamageRandomSection);
            if (breakBranchButton != null) breakBranchButton.onClick.AddListener(BreakBranch);
            if (healAllButton != null) healAllButton.onClick.AddListener(HealAll);
            if (fillEnergyButton != null) fillEnergyButton.onClick.AddListener(FillEnergy);
            if (winButton != null) winButton.onClick.AddListener(() => ForceEnd(true));
            if (loseButton != null) loseButton.onClick.AddListener(() => ForceEnd(false));
            if (addTimeButton != null) addTimeButton.onClick.AddListener(() => { if (Ctx != null) Ctx.timeRemaining += 60f; });
            if (resetButton != null) resetButton.onClick.AddListener(() => Services.Get<SceneFlow.SceneFlowService>()?.GoToSandbox());
            panel?.HideImmediate();
        }

        BattleContext Ctx => manager != null ? manager.Context : null;

        void Update()
        {
            if (infoText == null || Ctx == null) return;
            infoTimer -= Time.unscaledDeltaTime;
            if (infoTimer > 0f) return;
            infoTimer = 0.5f;
            infoText.text = $"State: {manager.StateMachine.State}  Time: {Ctx.timeRemaining:0}  Projectiles: {(Ctx.projectiles != null ? Ctx.projectiles.ActiveCount : 0)}\nPlayer core {Ctx.playerTree?.CorePercent:P0} alive {Ctx.playerRoster?.AliveCount}   Enemy core {Ctx.enemyTree?.CorePercent:P0} alive {Ctx.enemyRoster?.AliveCount}";
        }

        void AssignGuardian(int index)
        {
            var ctx = Ctx;
            if (ctx == null || index >= guardianIds.Length) return;
            var def = ctx.database.GetGuardian(guardianIds[index]);
            var roster = ctx.playerRoster;
            if (def == null || roster == null) return;
            var slot = roster.Get(nextSlot);
            if (slot != null)
            {
                var slotT = ctx.playerTree != null ? ctx.playerTree.GetSlot(nextSlot) : null;
                if (slotT != null) { slot.transform.SetParent(slotT, false); slot.transform.localPosition = Vector3.zero; }
                slot.Setup(def, 5, BattleSide.Player, nextSlot, ctx, roster);
                ctx.targeting?.BuildRegistry();
            }
            nextSlot = (nextSlot + 1) % 8;
        }

        void FireProjectile(int index)
        {
            var ctx = Ctx;
            if (ctx == null || index >= projectileIds.Length || ctx.projectiles == null) return;
            var def = ctx.database.GetProjectile(projectileIds[index]);
            if (def == null || ctx.playerTree == null || ctx.enemyTree == null) return;
            Vector2 from = (Vector2)ctx.playerTree.transform.position + new Vector2(2.5f, 5f);
            Vector2 to = ctx.enemyTree.Core != null ? (Vector2)ctx.enemyTree.Core.transform.position : (Vector2)ctx.enemyTree.transform.position + Vector2.up * 3f;
            var v = ctx.projectiles.LaunchVelocity(def, from, to, 1f);
            ctx.projectiles.Fire(def, from, v, BattleSide.Player, null, false, true, 1f, null);
        }

        void UseTool(int index)
        {
            var ctx = Ctx;
            if (ctx == null || index >= toolIds.Length || ctx.playerTools == null) return;
            var target = ctx.enemyTree != null && ctx.enemyTree.Core != null ? (Vector2)ctx.enemyTree.Core.transform.position : Vector2.zero;
            for (int i = 0; i < ctx.playerTools.Count; i++)
                if (ctx.playerTools.Get(i).def != null && ctx.playerTools.Get(i).def.id == toolIds[index]) { ctx.playerTools.ReduceCooldowns(999f); ctx.playerTools.Use(i, target); return; }
        }

        void DamageRandomSection()
        {
            var ctx = Ctx;
            if (ctx == null || ctx.enemyTree == null) return;
            var list = new System.Collections.Generic.List<Trees.TreeSection>();
            ctx.enemyTree.GetAliveSections(list, false);
            if (list.Count == 0) return;
            var s = list[ctx.rng.Next(list.Count)];
            ctx.damage.HitSection(s, s.MaxHealth * 0.5f + 20f, new DamageInfo { amount = 100f, source = BattleSide.Player, hitPoint = s.transform.position });
        }

        void BreakBranch()
        {
            var ctx = Ctx;
            if (ctx == null || ctx.enemyTree == null) return;
            var list = new System.Collections.Generic.List<Trees.TreeSection>();
            ctx.enemyTree.GetAliveSections(list, false);
            foreach (var s in list)
                if (s.Type == TreeSectionType.Branch) { ctx.damage.HitSection(s, s.MaxHealth * 3f, new DamageInfo { amount = s.MaxHealth * 3f, source = BattleSide.Player, hitPoint = s.transform.position }); return; }
        }

        void HealAll()
        {
            var ctx = Ctx;
            if (ctx == null) return;
            ctx.playerRoster?.HealAll(9999f, null);
            var list = new System.Collections.Generic.List<Trees.TreeSection>();
            ctx.playerTree?.GetAliveSections(list, false);
            foreach (var s in list) s.Heal(9999f);
        }

        void FillEnergy()
        {
            var ctx = Ctx;
            if (ctx == null || ctx.playerRoster == null) return;
            for (int i = 0; i < ctx.playerRoster.Slots.Count; i++) ctx.playerRoster.Slots[i]?.AddEnergy(999f);
        }

        void ForceEnd(bool win)
        {
            var ctx = Ctx;
            if (ctx == null) return;
            var tree = win ? ctx.enemyTree : ctx.playerTree;
            if (tree == null || tree.Core == null) return;
            ctx.damage.HitSection(tree.Core, tree.Core.MaxHealth * 100f, new DamageInfo { amount = 99999f, source = win ? BattleSide.Player : BattleSide.Enemy, hitPoint = tree.Core.transform.position });
        }
    }
}
