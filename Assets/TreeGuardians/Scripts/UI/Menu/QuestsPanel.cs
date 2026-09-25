using System.Collections.Generic;
using TMPro;
using TreeGuardians.Core;
using TreeGuardians.Localization;
using TreeGuardians.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Menu
{
    /// Quests & achievements tabs plus the daily reward entry.
    public sealed class QuestsPanel : UIPanel
    {
        [SerializeField] Button tabQuestsButton;
        [SerializeField] Button tabAchievementsButton;
        [SerializeField] RectTransform listContent;
        [SerializeField] QuestRowView rowTemplate;
        [SerializeField] Button dailyRewardButton;
        [SerializeField] TMP_Text dailyRewardLabel;
        [SerializeField] Button closeButton;
        [SerializeField] DailyRewardPopupRef dailyPopupRef;

        readonly List<QuestRowView> rows = new List<QuestRowView>(16);
        bool showAchievements;
        float nextTick;

        protected override void Awake()
        {
            base.Awake();
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (tabQuestsButton != null) tabQuestsButton.onClick.AddListener(() => { showAchievements = false; Refresh(); });
            if (tabAchievementsButton != null) tabAchievementsButton.onClick.AddListener(() => { showAchievements = true; Refresh(); });
            if (dailyRewardButton != null) dailyRewardButton.onClick.AddListener(OnDailyReward);
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            GameEventBus.Subscribe<QuestProgressEvent>(OnQuestEvent);
            GameEventBus.Subscribe<QuestClaimedEvent>(OnQuestClaimed);
            GameEventBus.Subscribe<LanguageChangedEvent>(OnLanguage);
        }

        void OnDisable()
        {
            GameEventBus.Unsubscribe<QuestProgressEvent>(OnQuestEvent);
            GameEventBus.Unsubscribe<QuestClaimedEvent>(OnQuestClaimed);
            GameEventBus.Unsubscribe<LanguageChangedEvent>(OnLanguage);
        }

        void OnQuestEvent(QuestProgressEvent e) { if (IsOpen) Refresh(); }
        void OnQuestClaimed(QuestClaimedEvent e) { if (IsOpen) Refresh(); }
        void OnLanguage(LanguageChangedEvent e) { if (IsOpen) Refresh(); }

        protected override void OnOpen() => Refresh();

        /// Selects the tab; safe to call before Open(). The Rank button opens achievements, the Quests button opens quests.
        public void ShowTab(bool achievements)
        {
            showAchievements = achievements;
            if (IsOpen) Refresh();
        }

        void Update()
        {
            if (!IsOpen || !Services.IsBootstrapped || Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + 1f;
            var quests = Services.Get<QuestService>();
            // Panel left open across the day boundary: daily quests reset live.
            if (quests != null && quests.CheckDailyReset()) Refresh();
            else RefreshDaily();
        }

        void Refresh()
        {
            var quests = Services.Get<QuestService>();
            if (quests == null) return;
            Highlight(tabQuestsButton, !showAchievements);
            Highlight(tabAchievementsButton, showAchievements);
            int n = 0;
            // Three passes keep the list order stable inside each group: claimable first, then in progress, claimed last.
            for (int pass = 0; pass < 3; pass++)
            {
                if (!showAchievements)
                {
                    var list = quests.Quests;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var q = list[i];
                        var def = quests.GetQuestDefinition(q.id);
                        if (def == null) continue;
                        bool claimable = quests.CanClaim(q.id);
                        if (Group(claimable, q.claimed) != pass) continue;
                        Row(n++).Bind(q.id, false, def.icon, def.titleKey, def.descriptionKey, q.progress, def.targetCount, q.claimed, claimable, def.reward, def.isDaily, OnClaim);
                    }
                }
                else
                {
                    var list = quests.Achievements;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var a = list[i];
                        var def = quests.GetAchievementDefinition(a.id);
                        if (def == null) continue;
                        bool claimable = quests.CanClaimAchievement(a.id);
                        if (Group(claimable, a.claimed) != pass) continue;
                        Row(n++).Bind(a.id, true, def.icon, def.titleKey, def.descriptionKey, a.progress, def.targetCount, a.claimed, claimable, def.reward, false, OnClaim);
                    }
                }
            }
            for (int i = n; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
            RefreshDaily();
        }

        void RefreshDaily()
        {
            var quests = Services.Get<QuestService>();
            if (quests == null || dailyRewardLabel == null) return;
            if (quests.CanClaimDailyReward()) dailyRewardLabel.text = LocalizationService.Tr("menu_daily_reward") + " — " + LocalizationService.Tr("ui_claim");
            else dailyRewardLabel.text = string.Format(LocalizationService.Tr("daily_next_in"), ChestSlotView.FormatTime(quests.TimeUntilDailyReward().TotalSeconds));
        }

        static int Group(bool claimable, bool claimed) => claimable ? 0 : claimed ? 2 : 1;

        QuestRowView Row(int i)
        {
            while (rows.Count <= i)
            {
                var r = Instantiate(rowTemplate, listContent);
                r.name = "QuestRow_" + rows.Count;
                rows.Add(r);
            }
            return rows[i];
        }

        static void Highlight(Button b, bool on)
        {
            if (b == null) return;
            var fb = b.GetComponent<UIButtonFeedback>();
            if (fb != null) fb.SetSelected(on);
        }

        void OnClaim(string id, bool achievement)
        {
            var quests = Services.Get<QuestService>();
            if (quests == null) return;
            var bundle = achievement ? quests.TryClaimAchievement(id) : quests.TryClaim(id);
            if (bundle != null) MenuUIController.Instance?.ShowRewards(bundle, Refresh);
            Refresh();
        }

        void OnDailyReward()
        {
            if (dailyPopupRef != null) dailyPopupRef.Show();
        }
    }
}
