using System;
using TMPro;
using TreeGuardians.Data;
using TreeGuardians.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Popups
{
    public sealed class InsufficientCurrencyPopup : UIPopup
    {
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text bodyText;
        [SerializeField] Image icon;
        [SerializeField] Button shopButton;
        [SerializeField] Button okButton;
        [SerializeField] RewardSprites sprites = new RewardSprites();

        Action onShop;

        protected override void Awake()
        {
            base.Awake();
            if (okButton != null) okButton.onClick.AddListener(Close);
            if (shopButton != null) shopButton.onClick.AddListener(() => { var cb = onShop; Close(); cb?.Invoke(); });
        }

        public void Show(CurrencyType type, Action goToShop)
        {
            onShop = goToShop;
            string nameKey = type switch
            {
                CurrencyType.Coins => "currency_coins",
                CurrencyType.Sap => "currency_sap",
                CurrencyType.Gems => "currency_gems",
                _ => "currency_trophies"
            };
            if (titleText != null) titleText.text = LocalizationService.Tr("insufficient_title");
            if (bodyText != null) bodyText.text = string.Format(LocalizationService.Tr("insufficient_body"), LocalizationService.Tr(nameKey));
            if (icon != null)
            {
                icon.sprite = type switch { CurrencyType.Coins => sprites.coin, CurrencyType.Sap => sprites.sap, CurrencyType.Gems => sprites.gem, _ => sprites.trophy };
                icon.color = type switch { CurrencyType.Coins => sprites.coinTint, CurrencyType.Sap => sprites.sapTint, CurrencyType.Gems => sprites.gemTint, _ => sprites.trophyTint };
            }
            if (shopButton != null) shopButton.gameObject.SetActive(goToShop != null);
            Open();
        }
    }
}
