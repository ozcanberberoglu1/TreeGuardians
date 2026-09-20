using System;
using TMPro;
using TreeGuardians.Chests;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Localization;
using TreeGuardians.Meta;
using TreeGuardians.Monetization;
using TreeGuardians.Rewards;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Menu
{
    /// Mock shop: every purchase is simulated (labelled TEST) through the IIAPService / IRewardedAdService abstractions.
    public sealed class ShopPanel : UIPanel
    {
        [SerializeField] Button freeChestButton;
        [SerializeField] TMP_Text freeChestLabel;
        [SerializeField] Button coinPackButton;
        [SerializeField] Button sapPackButton;
        [SerializeField] Button starterPackButton;
        [SerializeField] Button barkSkinButton;
        [SerializeField] Button watchAdButton;
        [SerializeField] Button closeButton;
        [SerializeField] TMP_Text noteText;
        [SerializeField] int coinPackAmount = 1000;
        [SerializeField] int sapPackAmount = 100;
        [SerializeField] int adCoins = 50;
        [SerializeField] float freeChestCooldownHours = 4f;

        long lastFreeChestTicks;
        bool busy;

        protected override void Awake()
        {
            base.Awake();
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (freeChestButton != null) freeChestButton.onClick.AddListener(OnFreeChest);
            if (coinPackButton != null) coinPackButton.onClick.AddListener(() => Purchase("coins_1000", R(coinPackAmount, 0, 0)));
            if (sapPackButton != null) sapPackButton.onClick.AddListener(() => Purchase("sap_100", R(0, sapPackAmount, 0)));
            if (starterPackButton != null) starterPackButton.onClick.AddListener(() => Purchase("starter", R(2000, 150, 30)));
            if (barkSkinButton != null) barkSkinButton.onClick.AddListener(() => Purchase("skin_bark_moss", null, () => { var p = Services.Get<PlayerProgressService>(); if (p != null) { p.Data.tree.skinId = "moss"; p.Save(); } }));
            if (watchAdButton != null) watchAdButton.onClick.AddListener(OnWatchAd);
        }

        protected override void OnOpen()
        {
            if (noteText != null) noteText.text = LocalizationService.Tr("shop_mock_note");
            RefreshFree();
        }

        static RewardBundle R(int c, int s, int g) => new RewardBundle { coins = c, sap = s, gems = g };

        void RefreshFree()
        {
            if (freeChestLabel == null) return;
            var save = Services.Get<Save.SaveService>();
            var now = save != null ? save.GetUtcNow() : DateTime.UtcNow;
            var next = new DateTime(lastFreeChestTicks, DateTimeKind.Utc).AddHours(freeChestCooldownHours);
            bool ready = lastFreeChestTicks == 0 || now >= next;
            freeChestLabel.text = ready ? LocalizationService.Tr("ui_free") : ChestSlotView.FormatTime((next - now).TotalSeconds);
            if (freeChestButton != null) freeChestButton.interactable = ready;
        }

        void OnFreeChest()
        {
            var chests = Services.Get<ChestService>();
            var save = Services.Get<Save.SaveService>();
            if (chests == null) return;
            if (!chests.HasFreeSlot) { MenuUIController.Instance?.Toast("results_chest_full"); return; }
            if (chests.TryAddChest("twig", 0) >= 0)
            {
                lastFreeChestTicks = (save != null ? save.GetUtcNow() : DateTime.UtcNow).Ticks;
                MenuUIController.Instance?.Toast("chest_twig");
                RefreshFree();
            }
        }

        void Purchase(string productId, RewardBundle reward, Action extra = null)
        {
            if (busy) return;
            busy = true;
            MonetizationServices.IAP.Purchase(productId, ok =>
            {
                busy = false;
                if (!ok || this == null) return;
                extra?.Invoke();
                if (reward != null)
                {
                    var result = Services.Get<RewardService>()?.Apply(reward, "shop_mock");
                    MenuUIController.Instance?.ShowRewardResult(reward, result);
                }
                else MenuUIController.Instance?.Toast("ui_test");
            });
        }

        void OnWatchAd()
        {
            if (busy) return;
            busy = true;
            MonetizationServices.Ads.Show(ok =>
            {
                busy = false;
                if (!ok || this == null) return;
                var reward = R(adCoins, 0, 0);
                var result = Services.Get<RewardService>()?.Apply(reward, "ad_mock");
                MenuUIController.Instance?.ShowRewardResult(reward, result);
            });
        }
    }
}
