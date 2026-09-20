using TMPro;
using TreeGuardians.Core;
using TreeGuardians.Localization;
using TreeGuardians.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Menu
{
    public sealed class ProfilePanel : UIPanel
    {
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text levelText;
        [SerializeField] TMP_Text trophiesText;
        [SerializeField] TMP_Text bestTrophiesText;
        [SerializeField] TMP_Text battlesText;
        [SerializeField] TMP_Text winsText;
        [SerializeField] TMP_Text winRateText;
        [SerializeField] TMP_Text collectionText;
        [SerializeField] Button closeButton;

        protected override void Awake()
        {
            base.Awake();
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        protected override void OnOpen()
        {
            var progress = Services.Get<PlayerProgressService>();
            if (progress == null) return;
            var d = progress.Data;
            if (nameText != null) nameText.text = d.displayName;
            if (levelText != null) levelText.text = string.Format(LocalizationService.Tr("menu_level"), d.playerLevel);
            if (trophiesText != null) trophiesText.text = LocalizationService.Tr("currency_trophies") + ": " + d.trophies;
            if (bestTrophiesText != null) bestTrophiesText.text = LocalizationService.Tr("profile_best_trophies") + ": " + d.bestTrophies;
            if (battlesText != null) battlesText.text = LocalizationService.Tr("profile_battles") + ": " + d.stats.battlesPlayed;
            if (winsText != null) winsText.text = LocalizationService.Tr("profile_wins") + ": " + d.stats.wins;
            if (winRateText != null) winRateText.text = d.stats.battlesPlayed > 0 ? $"{Mathf.RoundToInt(100f * d.stats.wins / d.stats.battlesPlayed)}%" : "-";
            int unlocked = 0;
            for (int i = 0; i < progress.Database.guardians.Count; i++) if (progress.Database.guardians[i] != null && progress.IsGuardianUnlocked(progress.Database.guardians[i].id)) unlocked++;
            if (collectionText != null) collectionText.text = LocalizationService.Tr("guardian_collection") + ": " + unlocked + "/" + progress.Database.guardians.Count;
        }
    }
}
