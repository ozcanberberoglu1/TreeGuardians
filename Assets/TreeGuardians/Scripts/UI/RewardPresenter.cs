using System.Collections.Generic;
using TreeGuardians.Chests;
using TreeGuardians.Data;
using TreeGuardians.Localization;
using TreeGuardians.Rewards;
using UnityEngine;

namespace TreeGuardians.UI
{
    public struct RewardItem
    {
        public Sprite icon;
        public string label;
        public string sub;
        public Color tint;
        public bool isNew;
        /// Numeric amount for the reveal count-up (used only when amountFormat is set).
        public int amount;
        /// Culture-aware format for the count-up, e.g. "+{0:N0}"; null = no count-up (label is shown as is).
        public string amountFormat;
    }

    [System.Serializable]
    public sealed class RewardSprites
    {
        public Sprite coin;
        public Sprite sap;
        public Sprite gem;
        public Sprite trophy;
        public Sprite card;
        public Sprite chest;
        [Tooltip("Closed chest art by ChestTier (Twig, Grove, Ancient, Moon, Sun). Used when the chest definition has no icon.")]
        public Sprite[] chestByTier = new Sprite[0];
        public Color coinTint = new Color(0.98f, 0.74f, 0.22f);
        public Color sapTint = new Color(0.45f, 0.8f, 0.4f);
        public Color gemTint = new Color(0.6f, 0.45f, 0.9f);
        public Color trophyTint = new Color(0.98f, 0.8f, 0.3f);

        public Sprite ChestFor(ChestTier tier)
        {
            int i = (int)tier;
            return chestByTier != null && i >= 0 && i < chestByTier.Length ? chestByTier[i] : null;
        }
    }

    /// Converts a RewardBundle into displayable items using the database sprites.
    public static class RewardPresenter
    {
        public const string PlusFormat = "+{0:N0}";
        public const string SignedFormat = "{0:N0}";

        public static void Build(RewardBundle bundle, GameDatabase db, RewardSprites sprites, List<RewardItem> into)
        {
            into.Clear();
            if (bundle == null) return;
            if (bundle.coins > 0) into.Add(Amount(sprites.coin, bundle.coins, "currency_coins", sprites.coinTint));
            if (bundle.sap > 0) into.Add(Amount(sprites.sap, bundle.sap, "currency_sap", sprites.sapTint));
            if (bundle.gems > 0) into.Add(Amount(sprites.gem, bundle.gems, "currency_gems", sprites.gemTint));
            if (bundle.trophies != 0) into.Add(Amount(sprites.trophy, bundle.trophies, "currency_trophies", sprites.trophyTint));
            for (int i = 0; i < bundle.cards.Count; i++)
            {
                var c = bundle.cards[i];
                var def = db != null ? db.GetGuardian(c.guardianId) : null;
                into.Add(new RewardItem { icon = def != null ? def.portrait : sprites.card, label = "x" + LocalizationService.Number(c.count), sub = def != null ? LocalizationService.Tr(def.nameKey) : c.guardianId, tint = Color.white });
            }
            for (int i = 0; i < bundle.chestIds.Count; i++)
            {
                var def = db != null ? db.GetChest(bundle.chestIds[i]) : null;
                Sprite chestIcon = def != null ? ChestArt.Closed(def) : null;
                if (chestIcon == null && def != null) chestIcon = sprites.ChestFor(def.tier);
                into.Add(new RewardItem { icon = chestIcon != null ? chestIcon : sprites.chest, label = "", sub = def != null ? LocalizationService.Tr(def.nameKey) : bundle.chestIds[i], tint = Color.white });
            }
            for (int i = 0; i < bundle.unlockGuardianIds.Count; i++)
            {
                var def = db != null ? db.GetGuardian(bundle.unlockGuardianIds[i]) : null;
                into.Add(new RewardItem { icon = def != null ? def.portrait : sprites.card, label = LocalizationService.Tr("ui_new"), sub = def != null ? LocalizationService.Tr(def.nameKey) : "", tint = Color.white, isNew = true });
            }
            for (int i = 0; i < bundle.unlockToolIds.Count; i++)
            {
                var def = db != null ? db.GetTool(bundle.unlockToolIds[i]) : null;
                into.Add(new RewardItem { icon = def != null ? def.icon : sprites.card, label = LocalizationService.Tr("ui_new"), sub = def != null ? LocalizationService.Tr(def.nameKey) : "", tint = def != null ? def.accentColor : Color.white, isNew = true });
            }
        }

        /// Currency-style tile: "+1.234" (TR) / "+1,234" (EN), negative values keep their minus sign.
        static RewardItem Amount(Sprite icon, int value, string subKey, Color tint)
        {
            return new RewardItem
            {
                icon = icon,
                label = (value > 0 ? "+" : "") + LocalizationService.Number(value),
                sub = LocalizationService.Tr(subKey),
                tint = tint,
                amount = value,
                amountFormat = value > 0 ? PlusFormat : SignedFormat
            };
        }
    }
}
