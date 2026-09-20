using System;
using System.Collections.Generic;
using TMPro;
using TreeGuardians.Localization;
using TreeGuardians.Rewards;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Menu
{
    /// One quest/achievement row: icon, title, description, progress, claim button.
    public sealed class QuestRowView : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text descText;
        [SerializeField] Image progressFill;
        [SerializeField] TMP_Text progressText;
        [SerializeField] TMP_Text rewardText;
        [SerializeField] Button claimButton;
        [SerializeField] TMP_Text claimLabel;
        [SerializeField] GameObject dailyTag;

        string id;
        bool isAchievement;
        Action<string, bool> onClaim;

        void Awake()
        {
            if (claimButton != null) claimButton.onClick.AddListener(() => onClaim?.Invoke(id, isAchievement));
        }

        public void Bind(string questId, bool achievement, Sprite sprite, string titleKey, string descKey, int progress, int target, bool claimed, bool claimable, RewardBundle reward, bool daily, Action<string, bool> claim)
        {
            id = questId;
            isAchievement = achievement;
            onClaim = claim;
            if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
            if (titleText != null) titleText.text = LocalizationService.Tr(titleKey);
            if (descText != null) descText.text = LocalizationService.Tr(descKey);
            if (progressFill != null) progressFill.fillAmount = target > 0 ? Mathf.Clamp01(progress / (float)target) : 0f;
            if (progressText != null) progressText.text = $"{Mathf.Min(progress, target)}/{target}";
            if (rewardText != null) rewardText.text = RewardSummary(reward);
            if (claimButton != null) claimButton.interactable = claimable;
            if (claimLabel != null) claimLabel.text = LocalizationService.Tr(claimed ? "ui_claimed" : "ui_claim");
            if (dailyTag != null) dailyTag.SetActive(daily);
            gameObject.SetActive(true);
        }

        static string RewardSummary(RewardBundle r)
        {
            if (r == null) return "";
            var parts = new List<string>(4);
            if (r.coins > 0) parts.Add(r.coins + " " + LocalizationService.Tr("currency_coins"));
            if (r.sap > 0) parts.Add(r.sap + " " + LocalizationService.Tr("currency_sap"));
            if (r.gems > 0) parts.Add(r.gems + " " + LocalizationService.Tr("currency_gems"));
            if (r.chestIds.Count > 0) parts.Add(LocalizationService.Tr("chest_" + r.chestIds[0]));
            return string.Join(" · ", parts);
        }
    }
}
