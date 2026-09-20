using System;
using TMPro;
using TreeGuardians.Audio;
using TreeGuardians.Battle;
using TreeGuardians.Chests;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Localization;
using TreeGuardians.Meta;
using TreeGuardians.Quests;
using TreeGuardians.Rewards;
using TreeGuardians.Save;
using TreeGuardians.SceneFlow;
using TreeGuardians.UI.Popups;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Menu
{
    /// Owns navigation between pre-authored panels and popups. Never instantiates UI.
    public sealed class MenuUIController : MonoBehaviour
    {
        public static MenuUIController Instance { get; private set; }

        [Header("Panels")]
        [SerializeField] GuardiansPanel guardiansPanel;
        [SerializeField] GuardianDetailPanel guardianDetailPanel;
        [SerializeField] TreeUpgradePanel treeUpgradePanel;
        [SerializeField] ToolsPanel toolsPanel;
        [SerializeField] BattlePrepPanel battlePrepPanel;
        [SerializeField] ChestsPanel chestsPanel;
        [SerializeField] ShopPanel shopPanel;
        [SerializeField] QuestsPanel questsPanel;
        [SerializeField] RankingPanel rankingPanel;
        [SerializeField] ProfilePanel profilePanel;
        [SerializeField] SettingsPanel settingsPanel;
        [SerializeField] DebugPanel debugPanel;

        [Header("Popups")]
        [SerializeField] GenericConfirmPopup confirmPopup;
        [SerializeField] RewardPopup rewardPopup;
        [SerializeField] InsufficientCurrencyPopup insufficientPopup;
        [SerializeField] UnlockPopup unlockPopup;
        [SerializeField] DailyRewardPopup dailyRewardPopup;
        [SerializeField] ChestOpenPopup chestOpenPopup;
        [SerializeField] ConnectionInfoPopup connectionPopup;

        [Header("Top Bar")]
        [SerializeField] TMP_Text playerLevelText;
        [SerializeField] Button settingsButton;
        [SerializeField] Button coinsButton;
        [SerializeField] Button sapButton;
        [SerializeField] Button gemsButton;

        [Header("Rails")]
        [SerializeField] Button shopButton;
        [SerializeField] Button seasonButton;
        [SerializeField] Button eventsButton;
        [SerializeField] Button inboxButton;
        [SerializeField] Button treeButton;
        [SerializeField] Button guardiansButton;
        [SerializeField] Button toolsButton;
        [SerializeField] Button profileButton;

        [Header("Center")]
        [SerializeField] TMP_Text treePowerText;
        [SerializeField] TMP_Text arenaNameText;
        [SerializeField] Image arenaBadge;
        [SerializeField] TMP_Text arenaProgressText;
        [SerializeField] Image arenaProgressFill;
        [SerializeField] Button editLoadoutButton;
        [SerializeField] CenterTreeDisplay centerTree;

        [Header("Bottom")]
        [SerializeField] Button rankButton;
        [SerializeField] Button questsButton;
        [SerializeField] GameObject questsBadge;
        [SerializeField] TMP_Text questsBadgeText;
        [SerializeField] Button battleButton;
        [SerializeField] ChestSlotView[] chestSlots = new ChestSlotView[4];

        [Header("Misc")]
        [SerializeField] UIToast toast;
        [SerializeField] TransitionOverlay transition;
        [SerializeField] Button debugButton;

        UIPanel current;
        PlayerProgressService progress;
        bool initialized;
        static bool dailyPromptShownThisSession;

        void Awake()
        {
            Instance = this;
            HideAll();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (initialized)
            {
                GameEventBus.Unsubscribe<LoadoutChangedEvent>(OnLoadoutChanged);
                GameEventBus.Unsubscribe<TreeUpgradedEvent>(OnTreeUpgraded);
                GameEventBus.Unsubscribe<ArenaChangedEvent>(OnArenaChanged);
                GameEventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
                GameEventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
                GameEventBus.Unsubscribe<QuestProgressEvent>(OnQuestProgress);
                GameEventBus.Unsubscribe<QuestClaimedEvent>(OnQuestClaimed);
                GameEventBus.Unsubscribe<RewardAppliedEvent>(OnRewardApplied);
            }
        }

        void HideAll()
        {
            UIPanel[] panels = { guardiansPanel, guardianDetailPanel, treeUpgradePanel, toolsPanel, battlePrepPanel, chestsPanel, shopPanel, questsPanel, rankingPanel, profilePanel, settingsPanel, debugPanel,
                confirmPopup, rewardPopup, insufficientPopup, unlockPopup, dailyRewardPopup, chestOpenPopup, connectionPopup };
            foreach (var p in panels) if (p != null) p.HideImmediate();
        }

        void Start()
        {
            BootAwaiter.WhenReady(Init);
        }

        void Init()
        {
            if (initialized || this == null) return;
            initialized = true;
            progress = Services.Get<PlayerProgressService>();

            Wire(settingsButton, () => OpenPanel(settingsPanel));
            Wire(coinsButton, () => Toast("currency_info_coins"));
            Wire(sapButton, () => Toast("currency_info_sap"));
            Wire(gemsButton, () => Toast("currency_info_gems"));
            Wire(shopButton, () => OpenPanel(shopPanel));
            Wire(seasonButton, () => Toast("menu_coming_soon"));
            Wire(eventsButton, () => Toast("menu_coming_soon"));
            Wire(inboxButton, () => connectionPopup?.Show());
            Wire(treeButton, () => OpenPanel(treeUpgradePanel));
            Wire(guardiansButton, () => OpenPanel(guardiansPanel));
            Wire(toolsButton, () => OpenPanel(toolsPanel));
            Wire(profileButton, () => OpenPanel(profilePanel));
            Wire(editLoadoutButton, () => OpenPanel(guardiansPanel));
            Wire(rankButton, () => OpenPanel(rankingPanel));
            Wire(questsButton, () => OpenPanel(questsPanel));
            Wire(battleButton, () => OpenPanel(battlePrepPanel));
            Wire(debugButton, () => OpenPanel(debugPanel));
            for (int i = 0; i < chestSlots.Length; i++)
            {
                if (chestSlots[i] == null) continue;
                int idx = i;
                chestSlots[i].SetClickHandler(() => OpenChestSlot(idx));
            }
            if (debugButton != null) debugButton.gameObject.SetActive(Application.isEditor || Debug.isDebugBuild);

            GameEventBus.Subscribe<LoadoutChangedEvent>(OnLoadoutChanged);
            GameEventBus.Subscribe<TreeUpgradedEvent>(OnTreeUpgraded);
            GameEventBus.Subscribe<ArenaChangedEvent>(OnArenaChanged);
            GameEventBus.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            GameEventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);
            GameEventBus.Subscribe<QuestProgressEvent>(OnQuestProgress);
            GameEventBus.Subscribe<QuestClaimedEvent>(OnQuestClaimed);
            GameEventBus.Subscribe<RewardAppliedEvent>(OnRewardApplied);

            Refresh();
            Services.Get<AudioService>()?.PlayMusic(MusicTrackId.MainMenu);

            var save = Services.Get<SaveService>();
            if (save != null && (save.LoadFailed || save.RecoveredFromBackup) && confirmPopup != null)
            {
                confirmPopup.Show("save_corrupt_title", save.LoadFailed ? "save_corrupt_body" : "save_recovered_body", null, null, null, "ui_ok", "ui_no", false);
            }
            else
            {
                var quests = Services.Get<QuestService>();
                if (!dailyPromptShownThisSession && quests != null && quests.CanClaimDailyReward() && dailyRewardPopup != null)
                {
                    dailyPromptShownThisSession = true;
                    TGTween.Delay(0.6f, () => { if (this != null && current == null) dailyRewardPopup.Show(); });
                }
            }

            var tut = progress.Data.tutorial;
            if (tut.battleTutorialDone && !tut.menuTutorialDone && !tut.skipped)
            {
                TGTween.Delay(1.2f, () => { if (this != null) Toast("tut_step_8"); });
                GameEventBus.Subscribe<GuardianUpgradedEvent>(OnTutorialUpgrade);
            }
        }

        void OnTutorialUpgrade(GuardianUpgradedEvent e)
        {
            GameEventBus.Unsubscribe<GuardianUpgradedEvent>(OnTutorialUpgrade);
            if (progress == null) return;
            progress.Data.tutorial.menuTutorialDone = true;
            progress.Save();
        }

        static void Wire(Button b, Action a)
        {
            if (b != null && a != null) b.onClick.AddListener(() => a());
        }

        // ---------------- navigation ----------------

        public void OpenPanel(UIPanel panel)
        {
            if (panel == null) { Toast("menu_coming_soon"); return; }
            if (current == panel) return;
            if (current != null) current.Close();
            current = panel;
            panel.Open();
            panel.OnClosed += HandleCurrentClosed;
        }

        void HandleCurrentClosed()
        {
            if (current != null) current.OnClosed -= HandleCurrentClosed;
            current = null;
        }

        public void CloseCurrent()
        {
            if (current != null) current.Close();
        }

        public bool IsAnyPanelOpen => current != null;

        public void OpenGuardianDetail(string guardianId, int slot)
        {
            guardianDetailPanel?.Show(guardianId, slot);
        }

        public void OpenChestSlot(int index)
        {
            chestsPanel?.ShowSlot(index);
            if (chestsPanel != null && current != chestsPanel)
            {
                if (current != null) current.Close();
                current = chestsPanel;
                chestsPanel.OnClosed += HandleCurrentClosed;
            }
        }

        public void OpenChestPopup(int slotIndex)
        {
            chestOpenPopup?.Show(slotIndex);
        }

        public void ShowConfirm(string titleKey, string bodyKey, Action onYes, Action onNo = null, string bodyLiteral = null)
        {
            confirmPopup?.Show(titleKey, bodyKey, onYes, onNo, bodyLiteral);
        }

        public void ShowRewards(RewardBundle bundle, Action done = null, string titleKey = "popup_reward_title")
        {
            if (bundle == null || bundle.IsEmpty) { done?.Invoke(); return; }
            var db = progress != null ? progress.Database : null;
            if (rewardPopup != null) rewardPopup.Show(bundle, db, done, titleKey);
            else done?.Invoke();
        }

        public void ShowRewardResult(RewardBundle bundle, RewardApplyResult result, Action done = null)
        {
            ShowRewards(bundle, () => ShowUnlocks(result, 0, done));
        }

        void ShowUnlocks(RewardApplyResult result, int index, Action done)
        {
            if (result == null || unlockPopup == null || progress == null) { done?.Invoke(); return; }
            if (index < result.unlockedGuardianIds.Count)
            {
                var def = progress.Database.GetGuardian(result.unlockedGuardianIds[index]);
                if (def != null) { unlockPopup.ShowGuardian(def, progress.Database.rarityPalette, () => ShowUnlocks(result, index + 1, done)); return; }
                ShowUnlocks(result, index + 1, done);
                return;
            }
            int toolIndex = index - result.unlockedGuardianIds.Count;
            if (toolIndex < result.unlockedToolIds.Count)
            {
                var def = progress.Database.GetTool(result.unlockedToolIds[toolIndex]);
                if (def != null) { unlockPopup.ShowTool(def, () => ShowUnlocks(result, index + 1, done)); return; }
                ShowUnlocks(result, index + 1, done);
                return;
            }
            done?.Invoke();
        }

        public void ShowInsufficient(CurrencyType type)
        {
            insufficientPopup?.Show(type, () => OpenPanel(shopPanel));
            Services.Get<AudioService>()?.PlayUi(AudioEventId.UiError);
        }

        public void Toast(string key)
        {
            toast?.ShowKey(key);
        }

        public void ToastLiteral(string text)
        {
            toast?.ShowText(text);
        }

        // ---------------- battle ----------------

        public void StartBattle(BotDifficulty difficulty)
        {
            if (progress == null) progress = Services.Get<PlayerProgressService>();
            if (progress == null) return;
            if (progress.EquippedGuardianCount == 0)
            {
                Toast("loadout_empty");
                battleButton?.GetComponent<UIButtonFeedback>()?.ShakeInvalid();
                return;
            }
            var flow = Services.Get<SceneFlowService>();
            if (flow == null) return;
            var arena = progress.CurrentArena;
            var setup = BattleSetupFactory.Create(progress, arena, difficulty, !progress.Data.tutorial.battleTutorialDone && !progress.Data.tutorial.skipped);
            progress.Save();
            if (transition != null) transition.Cover(() => flow.GoToBattle(setup));
            else flow.GoToBattle(setup);
        }

        // ---------------- refresh ----------------

        public void Refresh()
        {
            if (progress == null) return;
            if (playerLevelText != null) playerLevelText.text = progress.Data.playerLevel.ToString();
            if (treePowerText != null) treePowerText.text = progress.GetTreePower().ToString("N0");
            var arena = progress.CurrentArena;
            if (arena != null)
            {
                if (arenaNameText != null) arenaNameText.text = LocalizationService.Tr(arena.nameKey);
                if (arenaBadge != null) arenaBadge.sprite = arena.badge;
                var next = progress.Database.GetNextArena(arena.arenaIndex);
                if (next != null)
                {
                    int span = Mathf.Max(1, next.unlockTrophies - arena.unlockTrophies);
                    float t = Mathf.Clamp01((progress.Data.trophies - arena.unlockTrophies) / (float)span);
                    if (arenaProgressFill != null) arenaProgressFill.fillAmount = t;
                    if (arenaProgressText != null) arenaProgressText.text = string.Format(LocalizationService.Tr("menu_next_arena"), next.unlockTrophies);
                }
                else
                {
                    if (arenaProgressFill != null) arenaProgressFill.fillAmount = 1f;
                    if (arenaProgressText != null) arenaProgressText.text = LocalizationService.Tr("menu_max_arena");
                }
            }
            centerTree?.Refresh();
            RefreshQuestBadge();
            for (int i = 0; i < chestSlots.Length; i++) chestSlots[i]?.Refresh();
        }

        void RefreshQuestBadge()
        {
            var quests = Services.Get<QuestService>();
            int n = quests != null ? quests.ClaimableCount : 0;
            if (questsBadge != null) questsBadge.SetActive(n > 0);
            if (questsBadgeText != null) questsBadgeText.text = n.ToString();
        }

        void OnLoadoutChanged(LoadoutChangedEvent e) => Refresh();
        void OnTreeUpgraded(TreeUpgradedEvent e) => Refresh();
        void OnArenaChanged(ArenaChangedEvent e) => Refresh();
        void OnCurrencyChanged(CurrencyChangedEvent e) { if (e.type == CurrencyType.Trophies) Refresh(); }
        void OnLanguageChanged(LanguageChangedEvent e) => Refresh();
        void OnQuestProgress(QuestProgressEvent e) => RefreshQuestBadge();
        void OnQuestClaimed(QuestClaimedEvent e) => RefreshQuestBadge();
        void OnRewardApplied(RewardAppliedEvent e) => Refresh();
    }
}
