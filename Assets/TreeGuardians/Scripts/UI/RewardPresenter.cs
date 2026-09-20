using System.Collections.Generic;
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
        public Color coinTint = new Color(0.98f, 0.74f, 0.22f);
        public Color sapTint = new Color(0.45f, 0.8f, 0.4f);
        public Color gemTint = new Color(0.6f, 0.45f, 0.9f);
        public Color trophyTint = new Color(0.98f, 0.8f, 0.3f);
    }

    /// Converts a RewardBundle into displayable items using the database sprites.
    public static class RewardPresenter
    {
        public static void Build(RewardBundle bundle, GameDatabase db, RewardSprites sprites, List<RewardItem> into)
        {
            into.Clear();
            if (bundle == null) return;
            if (bundle.coins > 0) into.Add(new RewardItem { icon = sprites.coin, label = "+" + bundle.coins.ToString("N0"), sub = LocalizationService.Tr("currency_coins"), tint = sprites.coinTint });
            if (bundle.sap > 0) into.Add(new RewardItem { icon = sprites.sap, label = "+" + bundle.sap.ToString("N0"), sub = LocalizationService.Tr("currency_sap"), tint = sprites.sapTint });
            if (bundle.gems > 0) into.Add(new RewardItem { icon = sprites.gem, label = "+" + bundle.gems.ToString("N0"), sub = LocalizationService.Tr("currency_gems"), tint = sprites.gemTint });
            if (bundle.trophies != 0) into.Add(new RewardItem { icon = sprites.trophy, label = (bundle.trophies > 0 ? "+" : "") + bundle.trophies, sub = LocalizationService.Tr("currency_trophies"), tint = sprites.trophyTint });
            for (int i = 0; i < bundle.cards.Count; i++)
            {
                var c = bundle.cards[i];
                var def = db != null ? db.GetGuardian(c.guardianId) : null;
                into.Add(new RewardItem { icon = def != null ? def.portrait : sprites.card, label = "x" + c.count, sub = def != null ? LocalizationService.Tr(def.nameKey) : c.guardianId, tint = Color.white });
            }
            for (int i = 0; i < bundle.chestIds.Count; i++)
            {
                var def = db != null ? db.GetChest(bundle.chestIds[i]) : null;
                into.Add(new RewardItem { icon = def != null ? def.iconClosed : sprites.chest, label = "", sub = def != null ? LocalizationService.Tr(def.nameKey) : bundle.chestIds[i], tint = Color.white });
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
    }
}
