using System;
using System.Text;
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
        [Header("Claimed state (optional)")]
        [Tooltip("İsteğe bağlı 'Alındı' göstergesi (tik + yazı). Atanırsa ödül alınınca Claim butonu gizlenir ve bu gösterilir.")]
        [SerializeField] GameObject claimedState;
        [Tooltip("Satırın CanvasGroup'u (boşsa aynı objede aranır). Alınmış görevler soluk gösterilir.")]
        [SerializeField] CanvasGroup rowGroup;
        [SerializeField, Range(0f, 1f)] float claimedAlpha = 0.6f;

        static readonly StringBuilder Summary = new StringBuilder(64);

        string id;
        bool isAchievement;
        Action<string, bool> onClaim;

        public bool IsClaimed { get; private set; }

        void Awake()
        {
            if (claimButton != null) claimButton.onClick.AddListener(() => onClaim?.Invoke(id, isAchievement));
            if (rowGroup == null) rowGroup = GetComponent<CanvasGroup>();
        }

        public void Bind(string questId, bool achievement, Sprite sprite, string titleKey, string descKey, int progress, int target, bool claimed, bool claimable, RewardBundle reward, bool daily, Action<string, bool> claim)
        {
            id = questId;
            isAchievement = achievement;
            onClaim = claim;
            IsClaimed = claimed;
            if (icon != null) { icon.sprite = sprite; icon.enabled = sprite != null; }
            if (titleText != null) titleText.text = LocalizationService.Tr(titleKey);
            if (descText != null) descText.text = LocalizationService.Tr(descKey);
            FillBar.Set(progressFill, target > 0 ? Mathf.Clamp01(progress / (float)target) : 0f);
            if (progressText != null) progressText.text = LocalizationService.Number(Mathf.Min(progress, target)) + "/" + LocalizationService.Number(target);
            if (rewardText != null) rewardText.text = RewardSummary(reward);
            if (claimButton != null)
            {
                claimButton.interactable = claimable;
                claimButton.gameObject.SetActive(!(claimed && claimedState != null));
            }
            if (claimLabel != null) claimLabel.text = LocalizationService.Tr(claimed ? "ui_claimed" : "ui_claim");
            if (claimedState != null) claimedState.SetActive(claimed);
            if (rowGroup != null) rowGroup.alpha = claimed ? claimedAlpha : 1f;
            if (dailyTag != null) dailyTag.SetActive(daily);
            gameObject.SetActive(true);
        }

        static string RewardSummary(RewardBundle r)
        {
            if (r == null) return "";
            Summary.Clear();
            if (r.coins > 0) Append(LocalizationService.Number(r.coins) + " " + LocalizationService.Tr("currency_coins"));
            if (r.sap > 0) Append(LocalizationService.Number(r.sap) + " " + LocalizationService.Tr("currency_sap"));
            if (r.gems > 0) Append(LocalizationService.Number(r.gems) + " " + LocalizationService.Tr("currency_gems"));
            if (r.chestIds.Count > 0) Append(LocalizationService.Tr("chest_" + r.chestIds[0]));
            return Summary.ToString();
        }

        static void Append(string part)
        {
            if (Summary.Length > 0) Summary.Append(" · ");
            Summary.Append(part);
        }
    }
}
