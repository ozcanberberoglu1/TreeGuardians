using System;
using TMPro;
using TreeGuardians.Audio;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Localization;
using TreeGuardians.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Menu
{
    [Serializable]
    public sealed class TreeUpgradeRow
    {
        public TreeUpgradePath path;
        public Image icon;
        public TMP_Text nameText;
        public TMP_Text descText;
        public TMP_Text levelText;
        public TMP_Text costText;
        public Button upgradeButton;
    }

    /// Six upgrade paths with sap costs; refreshes the center tree tier after upgrades.
    public sealed class TreeUpgradePanel : UIPanel
    {
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text tierText;
        [SerializeField] TMP_Text totalLevelText;
        [SerializeField] TreeUpgradeRow[] rows = new TreeUpgradeRow[6];
        [SerializeField] Button closeButton;
        [SerializeField] Color okColor = new Color(0.9f, 0.95f, 0.9f);
        [SerializeField] Color missingColor = new Color(1f, 0.4f, 0.35f);

        static readonly string[] NameKeys = { "tree_heartwood", "tree_bark", "tree_branch", "tree_root", "tree_sap", "tree_canopy" };

        protected override void Awake()
        {
            base.Awake();
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            foreach (var r in rows)
            {
                if (r == null || r.upgradeButton == null) continue;
                var path = r.path;
                r.upgradeButton.onClick.AddListener(() => OnUpgrade(path));
            }
        }

        void OnEnable()
        {
            GameEventBus.Subscribe<CurrencyChangedEvent>(OnCurrency);
            GameEventBus.Subscribe<LanguageChangedEvent>(OnLanguage);
        }

        void OnDisable()
        {
            GameEventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrency);
            GameEventBus.Unsubscribe<LanguageChangedEvent>(OnLanguage);
        }

        void OnCurrency(CurrencyChangedEvent e) { if (IsOpen && e.type == CurrencyType.Sap) Refresh(); }
        void OnLanguage(LanguageChangedEvent e) { if (IsOpen) Refresh(); }

        protected override void OnOpen() => Refresh();

        void Refresh()
        {
            var progress = Services.Get<PlayerProgressService>();
            if (progress == null) return;
            if (titleText != null) titleText.text = LocalizationService.Tr("tree_panel_title");
            if (tierText != null)
            {
                var tier = progress.GetTreeVisualTier();
                tierText.text = LocalizationService.Tr(tier == TreeVisualTier.Ancient ? "tree_tier_ancient" : tier == TreeVisualTier.Strong ? "tree_tier_strong" : "tree_tier_sprouting");
            }
            if (totalLevelText != null) totalLevelText.text = string.Format(LocalizationService.Tr("menu_level"), progress.TotalTreeLevel);
            foreach (var r in rows)
            {
                if (r == null) continue;
                int p = (int)r.path;
                int level = progress.GetTreeUpgradeLevel(r.path);
                bool can = progress.CanUpgradeTree(r.path, out int cost, out string reason);
                bool max = level >= progress.Balance.treeUpgradeMaxLevel;
                if (r.nameText != null) r.nameText.text = LocalizationService.Tr(NameKeys[p]);
                if (r.descText != null) r.descText.text = LocalizationService.Tr(NameKeys[p] + "_desc");
                if (r.levelText != null) r.levelText.text = $"{level}/{progress.Balance.treeUpgradeMaxLevel}";
                if (r.costText != null)
                {
                    r.costText.text = max ? LocalizationService.Tr("ui_max") : cost.ToString("N0");
                    r.costText.color = max || progress.Wallet.CanAfford(CurrencyType.Sap, cost) ? okColor : missingColor;
                }
                if (r.upgradeButton != null) r.upgradeButton.interactable = can;
            }
        }

        void OnUpgrade(TreeUpgradePath path)
        {
            var progress = Services.Get<PlayerProgressService>();
            if (progress == null) return;
            if (!progress.CanUpgradeTree(path, out _, out string reason))
            {
                if (reason == "insufficient_sap") MenuUIController.Instance?.ShowInsufficient(CurrencyType.Sap);
                else MenuUIController.Instance?.Toast(reason);
                return;
            }
            if (progress.TryUpgradeTree(path))
            {
                Services.Get<AudioService>()?.PlayUi(AudioEventId.Upgrade);
                Services.Get<HapticService>()?.Medium();
                Refresh();
            }
        }
    }
}
