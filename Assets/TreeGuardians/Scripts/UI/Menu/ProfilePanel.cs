using TMPro;
using TreeGuardians.Core;
using TreeGuardians.Localization;
using TreeGuardians.Meta;
using TreeGuardians.Save;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Menu
{
    public sealed class ProfilePanel : UIPanel
    {
        /// PlayerSaveData's untouched default name; shown as the localized default ("Guardian 1234" / "Muhafız 1234").
        const string DefaultSaveName = "Guardian";

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

        void OnEnable()
        {
            GameEventBus.Subscribe<LanguageChangedEvent>(OnLanguage);
        }

        void OnDisable()
        {
            GameEventBus.Unsubscribe<LanguageChangedEvent>(OnLanguage);
        }

        void OnLanguage(LanguageChangedEvent e) { if (IsOpen) Refresh(); }

        protected override void OnOpen() => Refresh();

        void Refresh()
        {
            var progress = Services.Get<PlayerProgressService>();
            if (progress == null) return;
            var d = progress.Data;
            if (nameText != null) nameText.text = GetDisplayName(d);
            if (levelText != null) levelText.text = LocalizationService.Tr("menu_level", d.playerLevel);
            if (trophiesText != null) trophiesText.text = LocalizationService.Tr("currency_trophies") + ": " + LocalizationService.Number(d.trophies);
            if (bestTrophiesText != null) bestTrophiesText.text = LocalizationService.Tr("profile_best_trophies") + ": " + LocalizationService.Number(d.bestTrophies);
            if (battlesText != null) battlesText.text = LocalizationService.Tr("profile_battles") + ": " + LocalizationService.Number(d.stats.battlesPlayed);
            if (winsText != null) winsText.text = LocalizationService.Tr("profile_wins") + ": " + LocalizationService.Number(d.stats.wins);
            if (winRateText != null)
            {
                string rate = d.stats.battlesPlayed > 0
                    ? LocalizationService.Tr("fmt_percent", Mathf.RoundToInt(100f * d.stats.wins / d.stats.battlesPlayed))
                    : "-";
                winRateText.text = LocalizationService.Tr("profile_win_rate", rate);
            }
            int unlocked = 0;
            for (int i = 0; i < progress.Database.guardians.Count; i++) if (progress.Database.guardians[i] != null && progress.IsGuardianUnlocked(progress.Database.guardians[i].id)) unlocked++;
            if (collectionText != null) collectionText.text = LocalizationService.Tr("guardian_collection") + ": " + unlocked + "/" + progress.Database.guardians.Count;
        }

        /// The player's name; the untouched save default becomes a localized "Guardian 1234" with a stable per-install number.
        public static string GetDisplayName(PlayerSaveData d)
        {
            if (d == null) return "";
            if (!string.IsNullOrEmpty(d.displayName) && d.displayName != DefaultSaveName) return d.displayName;
            return LocalizationService.Tr("profile_default_name", StableNumber(d.playerId).ToString("0000"));
        }

        /// FNV-1a over the player id (string.GetHashCode is not stable across runtimes).
        static int StableNumber(string id)
        {
            if (string.IsNullOrEmpty(id)) return 1000;
            unchecked
            {
                uint h = 2166136261;
                for (int i = 0; i < id.Length; i++) { h ^= id[i]; h *= 16777619; }
                return (int)(h % 10000u);
            }
        }
    }
}
