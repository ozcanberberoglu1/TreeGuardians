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
        [Tooltip("İsteğe bağlı: TopBar > ProfileButton > ProfileLabel. Atanırsa oyuncu adını gösterir (üzerindeki LocalizedTMPText kapatılır).")]
        [SerializeField] TMP_Text profileNameText;
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
        [Tooltip("Henüz olmayan özellikler (Sezon, Etkinlikler) için buton tonu; 'Yakında' izlenimi verir.")]
        [SerializeField] Color comingSoonTint = new Color(0.75f, 0.75f, 0.75f, 1f);

        [Header("Center")]
        [SerializeField] TMP_Text treePowerText;
        [SerializeField] TMP_Text arenaNameText;
        [SerializeField] Image arenaBadge;
        [SerializeField] TMP_Text arenaProgressText;
        [Tooltip("İsteğe bağlı: arena çubuğunun içinde '90 / 150' kupa sayacı.")] [SerializeField] TMP_Text arenaTrophyCountText;
        [SerializeField] Image arenaProgressFill;
        [SerializeField] Button editLoadoutButton;
        [SerializeField] CenterTreeDisplay centerTree;
        [Tooltip("Muhafız paneli açıkken sönen ana menü HUD kökleri (TopBar, raylar, orta HUD, BottomBar...). Paneller ve popup katmanı dahil edilmez.")] [SerializeField] CanvasGroup[] hudGroups = new CanvasGroup[0];
        [Tooltip("Ortalanmış paneller (Mağaza, Görevler, Ayarlar...) açıkken sönen HUD kökleri. TopBar'ı eklemeyin: para birimleri görünür kalsın.")]
        [SerializeField] CanvasGroup[] panelHiddenGroups = new CanvasGroup[0];

        [Header("Bottom")]
        [Tooltip("Açıksa Rank butonu Arena Yolu'nu (RankingPanel) açar ve kupa sayısını gösterir; başarımlar Görevler panelindeki sekmeden açılır. Kapalıysa başarımlar sekmesini açar.")]
        [SerializeField] bool rankButtonOpensArenaPath = true;
        [Tooltip("Başarımlar sekmesini (veya Arena Yolu'nu) açar; etiket = tamamlanan/toplam başarım (veya kupa).")] [SerializeField] Button rankButton;
        [SerializeField] TMP_Text rankLabel;
        [Tooltip("Alınmamış başarım ödülü sayısı; 0 ise gizlenir.")] [SerializeField] GameObject rankBadge;
        [SerializeField] TMP_Text rankBadgeText;
        [Tooltip("Görevler sekmesini açar; etiket = tamamlanan/toplam görev.")] [SerializeField] Button questsButton;
        [SerializeField] TMP_Text questsLabel;
        [Tooltip("Alınmamış görev ödülü + hazır günlük ödül sayısı; 0 ise gizlenir.")] [SerializeField] GameObject questsBadge;
        [SerializeField] TMP_Text questsBadgeText;
        [Tooltip("İsteğe bağlı: arena yolu panelini (RankingPanel) açan buton.")] [SerializeField] Button arenaPathButton;
        [SerializeField] Button battleButton;
        [Tooltip("BottomBar > ChestSlots üzerindeki ChestSlotsView: slotlar ve sandık görselleri.")] [SerializeField] ChestSlotsView chestSlotsView;

        [Header("Misc")]
        [SerializeField] UIToast toast;
        [SerializeField] TransitionOverlay transition;
        [SerializeField] Button debugButton;

        UIPanel current;
        PlayerProgressService progress;
        QuestService questService;
        bool initialized;
        bool hudForcedHidden;
        /// lastClaimUtcTicks of the daily reward the start-up prompt was shown for (once per claimable reward, not once per process).
        static long dailyPromptShownForClaimTicks = long.MinValue;

        readonly System.Collections.Generic.Dictionary<CanvasGroup, Coroutine> hudFades = new System.Collections.Generic.Dictionary<CanvasGroup, Coroutine>(8);
        readonly System.Collections.Generic.Dictionary<CanvasGroup, bool> hudVisible = new System.Collections.Generic.Dictionary<CanvasGroup, bool>(8);

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
                GameEventBus.Unsubscribe<GuardianUpgradedEvent>(OnTutorialUpgrade);
                if (questService != null) questService.OnChanged -= RefreshQuestSummaries;
                if (guardiansPanel != null)
                {
                    guardiansPanel.OnOpened -= OnGuardiansPanelOpened;
                    guardiansPanel.OnClosed -= OnGuardiansPanelClosed;
                }
            }
        }

        void OnGuardiansPanelOpened() => RefreshHudVisibility();
        void OnGuardiansPanelClosed() => RefreshHudVisibility();

        /// Forces the whole main-menu HUD hidden (false) or back to its automatic state (true).
        public void SetHudVisible(bool visible)
        {
            hudForcedHidden = !visible;
            RefreshHudVisibility();
        }

        /// Side-docked Guardians panel: every hudGroup fades out. A centered panel: panelHiddenGroups fade out (rails, center HUD,
        /// bottom bar) while the TopBar keeps the currencies visible. One pass over both lists so the two rules never fight.
        void RefreshHudVisibility()
        {
            bool guardiansOpen = hudForcedHidden || (guardiansPanel != null && guardiansPanel.IsOpen);
            bool centeredOpen = current != null && current.IsOpen && current != guardiansPanel;
            ApplyHudGroups(hudGroups, guardiansOpen, centeredOpen);
            ApplyHudGroups(panelHiddenGroups, guardiansOpen, centeredOpen);
        }

        void ApplyHudGroups(CanvasGroup[] groups, bool guardiansOpen, bool centeredOpen)
        {
            if (groups == null) return;
            for (int i = 0; i < groups.Length; i++)
            {
                var g = groups[i];
                if (g == null) continue;
                bool hide = (guardiansOpen && Contains(hudGroups, g)) || (centeredOpen && Contains(panelHiddenGroups, g));
                SetGroupVisible(g, !hide);
            }
        }

        static bool Contains(CanvasGroup[] groups, CanvasGroup g)
        {
            if (groups == null) return false;
            for (int i = 0; i < groups.Length; i++) if (groups[i] == g) return true;
            return false;
        }

        void SetGroupVisible(CanvasGroup g, bool visible)
        {
            if (hudVisible.TryGetValue(g, out bool was) && was == visible) return;
            hudVisible[g] = visible;
            if (hudFades.TryGetValue(g, out var running)) TGTween.Stop(running);
            g.blocksRaycasts = visible;
            g.interactable = visible;
            hudFades[g] = TGTween.FadeCanvasGroup(g, visible ? 1f : 0f, QualityApplier.ReduceMotion ? 0f : 0.2f);
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
            TintComingSoon(seasonButton);
            TintComingSoon(eventsButton);
            Wire(inboxButton, () => connectionPopup?.Show());
            Wire(treeButton, () => OpenPanel(treeUpgradePanel));
            Wire(guardiansButton, () => OpenPanel(guardiansPanel));
            Wire(toolsButton, () => OpenPanel(toolsPanel));
            Wire(profileButton, () => OpenPanel(profilePanel));
            Wire(editLoadoutButton, () => OpenPanel(guardiansPanel));
            if (rankButtonOpensArenaPath && rankingPanel != null) Wire(rankButton, () => OpenPanel(rankingPanel));
            else Wire(rankButton, () => OpenQuests(true));
            Wire(questsButton, () => OpenQuests(false));
            Wire(arenaPathButton, () => OpenPanel(rankingPanel));
            Wire(battleButton, () => OpenPanel(battlePrepPanel));
            Wire(debugButton, () => OpenPanel(debugPanel));
            if (chestSlotsView != null)
            {
                for (int i = 0; i < chestSlotsView.SlotCount; i++)
                {
                    var slot = chestSlotsView.GetSlot(i);
                    if (slot == null) continue;
                    int idx = i;
                    slot.SetClickHandler(() => OpenChestSlot(idx));
                }
                if (chestSlotsView.SlotCount != progress.Balance.chestSlotCount)
                    Debug.LogWarning($"[MenuUIController] ChestSlotsView has {chestSlotsView.SlotCount} slots but GameBalanceConfig.chestSlotCount is {progress.Balance.chestSlotCount}.", chestSlotsView);
            }
            questService = Services.Get<QuestService>();
            // A session left open (or resumed) across the day boundary: reset dailies before the badges are computed.
            questService?.CheckDailyReset();
            if (questService != null) questService.OnChanged += RefreshQuestSummaries;
            if (profileNameText != null)
            {
                var localized = profileNameText.GetComponent<LocalizedTMPText>();
                if (localized != null) localized.enabled = false;
            }
            if (guardiansPanel != null)
            {
                guardiansPanel.OnOpened += OnGuardiansPanelOpened;
                guardiansPanel.OnClosed += OnGuardiansPanelClosed;
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
                TryPromptDailyReward();
            }

            var tut = progress.Data.tutorial;
            if (tut.battleTutorialDone && !tut.menuTutorialDone && !tut.skipped)
            {
                TGTween.Delay(1.2f, () => { if (this != null) Toast("tut_step_8"); });
                GameEventBus.Subscribe<GuardianUpgradedEvent>(OnTutorialUpgrade);
            }
        }

        /// Shows the daily reward popup once per claimable reward (a session left open past the cooldown prompts again).
        void TryPromptDailyReward()
        {
            var quests = questService != null ? questService : Services.Get<QuestService>();
            if (quests == null || dailyRewardPopup == null || progress == null || !quests.CanClaimDailyReward()) return;
            long claimTicks = progress.Data.dailyReward.lastClaimUtcTicks;
            if (dailyPromptShownForClaimTicks == claimTicks) return;
            dailyPromptShownForClaimTicks = claimTicks;
            TGTween.Delay(0.6f, () => { if (this != null && current == null && !dailyRewardPopup.IsOpen) dailyRewardPopup.Show(); });
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus || !initialized || this == null) return;
            questService?.CheckDailyReset();
            TryPromptDailyReward();
        }

        void TintComingSoon(Button b)
        {
            if (b == null || b.targetGraphic == null) return;
            b.targetGraphic.color = comingSoonTint;
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
            // The guardian detail is opened on top of the Guardians panel outside 'current'; never leave it behind another panel.
            if (panel != guardianDetailPanel) CloseGuardianDetail();
            if (current != null) current.Close();
            current = panel;
            panel.OnClosed += HandleCurrentClosed;
            panel.Open();
            RefreshHudVisibility();
        }

        void HandleCurrentClosed()
        {
            if (current != null) current.OnClosed -= HandleCurrentClosed;
            current = null;
            RefreshHudVisibility();
        }

        /// Closes the guardian detail panel if it is open (Guardians panel closing, another panel opening).
        public void CloseGuardianDetail()
        {
            if (guardianDetailPanel != null && guardianDetailPanel.IsOpen) guardianDetailPanel.Close();
        }

        public void CloseCurrent()
        {
            if (current != null) current.Close();
        }

        public bool IsAnyPanelOpen => current != null;
        public bool IsGuardianDetailOpen => guardianDetailPanel != null && guardianDetailPanel.IsOpen;
        public CenterTreeDisplay CenterTree => centerTree;

        /// Quests panel on the requested tab (Rank button = achievements, Quests button = quests).
        public void OpenQuests(bool achievements)
        {
            questsPanel?.ShowTab(achievements);
            OpenPanel(questsPanel);
        }

        public void OpenGuardianDetail(string guardianId, int slot)
        {
            guardianDetailPanel?.Show(guardianId, slot);
        }

        public void OpenChestSlot(int index)
        {
            if (chestsPanel == null) return;
            if (current != chestsPanel)
            {
                CloseGuardianDetail();
                if (current != null) current.Close();
                current = chestsPanel;
                chestsPanel.OnClosed += HandleCurrentClosed;
            }
            chestsPanel.ShowSlot(index);
            if (!chestsPanel.IsOpen && current == chestsPanel) HandleCurrentClosed();
            else RefreshHudVisibility();
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
            if (profileNameText != null) profileNameText.text = ProfilePanel.GetDisplayName(progress.Data);
            if (treePowerText != null) treePowerText.text = LocalizationService.Number(progress.GetTreePower());
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
                    FillBar.Set(arenaProgressFill, t);
                    if (arenaProgressText != null) arenaProgressText.text = LocalizationService.Tr("menu_next_arena", LocalizationService.Number(next.unlockTrophies));
                    if (arenaTrophyCountText != null) arenaTrophyCountText.text = LocalizationService.Number(progress.Data.trophies) + " / " + LocalizationService.Number(next.unlockTrophies);
                }
                else
                {
                    FillBar.Set(arenaProgressFill, 1f);
                    if (arenaProgressText != null) arenaProgressText.text = LocalizationService.Tr("menu_max_arena");
                    if (arenaTrophyCountText != null) arenaTrophyCountText.text = LocalizationService.Number(progress.Data.trophies);
                }
            }
            centerTree?.Refresh();
            RefreshQuestSummaries();
            if (chestSlotsView != null)
                for (int i = 0; i < chestSlotsView.SlotCount; i++) chestSlotsView.GetSlot(i)?.Refresh();
        }

        /// Rank = achievements (done/total + unclaimed rewards), Quests = quests (done/total + unclaimed rewards incl. daily).
        void RefreshQuestSummaries()
        {
            if (this == null) return;
            var quests = questService != null ? questService : Services.Get<QuestService>();
            var q = quests != null ? quests.GetQuestSummary() : default;
            var a = quests != null ? quests.GetAchievementSummary() : default;
            int questClaimable = quests != null ? quests.QuestClaimableCount : 0;
            if (rankButtonOpensArenaPath && rankingPanel != null)
            {
                // Rank = arena path (trophies); achievements live in the Quests panel tab, so their rewards join the Quests badge.
                SetSummary(questsLabel, questsBadge, questsBadgeText, q.completed, q.total, questClaimable + a.claimable);
                if (rankLabel != null) rankLabel.text = progress != null ? LocalizationService.Number(progress.Data.trophies) : "";
                if (rankBadge != null) rankBadge.SetActive(false);
            }
            else
            {
                SetSummary(questsLabel, questsBadge, questsBadgeText, q.completed, q.total, questClaimable);
                SetSummary(rankLabel, rankBadge, rankBadgeText, a.completed, a.total, a.claimable);
            }
        }

        static void SetSummary(TMP_Text label, GameObject badge, TMP_Text badgeText, int completed, int total, int claimable)
        {
            if (label != null) label.text = completed + "/" + total;
            if (badge != null) badge.SetActive(claimable > 0);
            if (badgeText != null) badgeText.text = claimable.ToString();
        }

        void OnLoadoutChanged(LoadoutChangedEvent e) => Refresh();
        void OnTreeUpgraded(TreeUpgradedEvent e) => Refresh();
        void OnArenaChanged(ArenaChangedEvent e) => Refresh();
        void OnCurrencyChanged(CurrencyChangedEvent e) { if (e.type == CurrencyType.Trophies) Refresh(); }
        void OnLanguageChanged(LanguageChangedEvent e) => Refresh();
        void OnQuestProgress(QuestProgressEvent e) => RefreshQuestSummaries();
        void OnQuestClaimed(QuestClaimedEvent e) => RefreshQuestSummaries();
        void OnRewardApplied(RewardAppliedEvent e) => Refresh();
    }
}
