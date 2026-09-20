using TreeGuardians.Chests;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Meta;
using TreeGuardians.Save;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Menu
{
    /// Developer-only resource cheats. Hidden outside Editor / Development builds.
    public sealed class DebugPanel : UIPanel
    {
        [SerializeField] Button addCoinsButton;
        [SerializeField] Button addSapButton;
        [SerializeField] Button addGemsButton;
        [SerializeField] Button unlockAllButton;
        [SerializeField] Button finishChestsButton;
        [SerializeField] Button addChestButton;
        [SerializeField] Button resetButton;
        [SerializeField] Button sandboxButton;
        [SerializeField] Button closeButton;

        protected override void Awake()
        {
            base.Awake();
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (addCoinsButton != null) addCoinsButton.onClick.AddListener(() => Add(CurrencyType.Coins, 1000));
            if (addSapButton != null) addSapButton.onClick.AddListener(() => Add(CurrencyType.Sap, 100));
            if (addGemsButton != null) addGemsButton.onClick.AddListener(() => Add(CurrencyType.Gems, 50));
            if (unlockAllButton != null) unlockAllButton.onClick.AddListener(UnlockAll);
            if (finishChestsButton != null) finishChestsButton.onClick.AddListener(FinishChests);
            if (addChestButton != null) addChestButton.onClick.AddListener(() => Services.Get<ChestService>()?.TryAddChest("grove", 0));
            if (resetButton != null) resetButton.onClick.AddListener(() =>
            {
                var save = Services.Get<SaveService>();
                if (save == null) return;
                save.ResetToNewSave();
                Services.Get<SceneFlow.SceneFlowService>()?.GoToMainMenu();
            });
            if (sandboxButton != null) sandboxButton.onClick.AddListener(() => Services.Get<SceneFlow.SceneFlowService>()?.GoToSandbox());
        }

        static void Add(CurrencyType t, int amount)
        {
            var p = Services.Get<PlayerProgressService>();
            if (p == null) return;
            p.Wallet.Add(t, amount);
            p.Save();
        }

        static void UnlockAll()
        {
            var p = Services.Get<PlayerProgressService>();
            if (p == null) return;
            foreach (var g in p.Database.guardians) if (g != null) p.UnlockGuardian(g.id);
            foreach (var t in p.Database.tools) if (t != null) p.UnlockTool(t.id);
            p.Save();
        }

        static void FinishChests()
        {
            var p = Services.Get<PlayerProgressService>();
            var chests = Services.Get<ChestService>();
            if (p == null || chests == null) return;
            var now = Services.Get<SaveService>()?.GetUtcNow().Ticks ?? System.DateTime.UtcNow.Ticks;
            foreach (var s in p.Data.chestSlots)
            {
                if (s.IsEmpty) continue;
                s.unlocking = true;
                s.unlockStartUtcTicks = now - 1;
                s.unlockEndUtcTicks = now;
            }
            p.Save();
            GameEventBus.Publish(new ChestSlotsChangedEvent());
        }
    }
}
