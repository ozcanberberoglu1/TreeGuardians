using TMPro;
using TreeGuardians.Core;
using TreeGuardians.Localization;
using TreeGuardians.Meta;
using TreeGuardians.Quests;
using TreeGuardians.Rewards;
using TreeGuardians.UI.Menu;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Popups
{
    /// Seven pre-authored day tiles; highlights today's tile and claims it.
    public sealed class DailyRewardPopup : DailyRewardPopupRef
    {
        [SerializeField] TMP_Text titleText;
        [SerializeField] RewardItemView[] dayTiles = new RewardItemView[7];
        [SerializeField] Image[] dayHighlights = new Image[7];
        [SerializeField] TMP_Text[] dayLabels = new TMP_Text[7];
        [SerializeField] Button claimButton;
        [SerializeField] TMP_Text claimLabel;
        [SerializeField] Button closeButton;
        [SerializeField] RewardSprites sprites = new RewardSprites();

        readonly System.Collections.Generic.List<RewardItem> items = new System.Collections.Generic.List<RewardItem>(4);

        protected override void Awake()
        {
            base.Awake();
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (claimButton != null) claimButton.onClick.AddListener(OnClaim);
        }

        public override void Show()
        {
            var quests = Services.Get<QuestService>();
            var progress = Services.Get<PlayerProgressService>();
            if (quests == null || progress == null || progress.Database.dailyRewards == null) return;
            int today = quests.DailyRewardDayIndex;
            if (titleText != null) titleText.text = LocalizationService.Tr("daily_title");
            var days = progress.Database.dailyRewards.days;
            for (int i = 0; i < dayTiles.Length; i++)
            {
                if (dayTiles[i] == null) continue;
                if (i < days.Count)
                {
                    RewardPresenter.Build(days[i], progress.Database, sprites, items);
                    if (items.Count > 0) dayTiles[i].Bind(items[0]);
                    else dayTiles[i].Hide();
                }
                else dayTiles[i].Hide();
                if (i < dayHighlights.Length && dayHighlights[i] != null) dayHighlights[i].enabled = i == today;
                if (i < dayLabels.Length && dayLabels[i] != null) dayLabels[i].text = string.Format(LocalizationService.Tr("daily_day"), i + 1);
            }
            bool can = quests.CanClaimDailyReward();
            if (claimButton != null) claimButton.interactable = can;
            if (claimLabel != null) claimLabel.text = can ? LocalizationService.Tr("ui_claim") : string.Format(LocalizationService.Tr("daily_next_in"), ChestSlotView.FormatTime(quests.TimeUntilDailyReward().TotalSeconds));
            Open();
        }

        void OnClaim()
        {
            var quests = Services.Get<QuestService>();
            if (quests == null) return;
            var bundle = quests.ClaimDailyReward();
            Close();
            if (bundle != null) MenuUIController.Instance?.ShowRewards(bundle);
        }
    }
}
