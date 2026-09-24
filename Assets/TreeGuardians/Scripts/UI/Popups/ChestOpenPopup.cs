using System.Collections.Generic;
using TMPro;
using TreeGuardians.Audio;
using TreeGuardians.Chests;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Localization;
using TreeGuardians.Meta;
using TreeGuardians.Rewards;
using TreeGuardians.UI.Menu;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Popups
{
    /// Dimmer → chest bounce → tap (or auto) → burst → reward tiles → Collect All.
    public sealed class ChestOpenPopup : UIPopup
    {
        [SerializeField] Image chestImage;
        [SerializeField] Image glow;
        [SerializeField] Button chestButton;
        [SerializeField] TMP_Text hintText;
        [SerializeField] RewardItemView[] tiles = new RewardItemView[8];
        [SerializeField] Button collectButton;
        [SerializeField] RewardSprites sprites = new RewardSprites();
        [SerializeField] float autoOpenSeconds = 2.5f;
        [SerializeField] float revealDelay = 0.15f;

        readonly List<RewardItem> items = new List<RewardItem>(16);
        int slotIndex;
        bool opened;
        ChestDefinition def;
        RewardBundle bundle;
        Coroutine autoOpen;

        protected override void Awake()
        {
            base.Awake();
            if (chestButton != null) chestButton.onClick.AddListener(OpenChest);
            if (collectButton != null) collectButton.onClick.AddListener(Close);
        }

        public void Show(int slot)
        {
            var chests = Services.Get<ChestService>();
            def = chests != null ? chests.GetDefinition(slot) : null;
            if (def == null) return;
            slotIndex = slot;
            opened = false;
            bundle = null;
            if (chestImage != null) { chestImage.sprite = ChestArt.Closed(def); chestImage.color = Color.white; }
            if (glow != null) { glow.color = new Color(def.glowColor.r, def.glowColor.g, def.glowColor.b, 0f); }
            if (hintText != null) hintText.text = LocalizationService.Tr("chest_tap_to_open");
            foreach (var t in tiles) t?.Hide();
            if (collectButton != null) collectButton.gameObject.SetActive(false);
            if (chestButton != null) chestButton.interactable = true;
            Open();
            if (chestImage != null) TGTween.PunchScale(chestImage.transform, 0.18f, 0.5f);
            TGTween.Stop(autoOpen);
            autoOpen = TGTween.Delay(autoOpenSeconds, OpenChest);
        }

        void OpenChest()
        {
            if (opened) return;
            opened = true;
            TGTween.Stop(autoOpen);
            var chests = Services.Get<ChestService>();
            var progress = Services.Get<PlayerProgressService>();
            if (chests == null || progress == null) { Close(); return; }
            bundle = chests.Open(slotIndex);
            if (bundle == null) { Close(); return; }

            if (chestButton != null) chestButton.interactable = false;
            if (hintText != null) hintText.text = "";
            if (chestImage != null) { chestImage.sprite = ChestArt.Open(def); TGTween.PunchScale(chestImage.transform, 0.25f, 0.4f); }
            if (glow != null) { glow.color = new Color(def.glowColor.r, def.glowColor.g, def.glowColor.b, 0.9f); TGTween.ScaleTo(glow.transform, Vector3.one * 1.6f, 0.5f, Ease.OutCubic); }
            Services.Get<AudioService>()?.PlayUi(AudioEventId.ChestOpen);
            Services.Get<HapticService>()?.Medium();

            RewardPresenter.Build(bundle, progress.Database, sprites, items);
            int shown = Mathf.Min(items.Count, tiles.Length);
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == null) continue;
                if (i < shown) { tiles[i].Bind(items[i]); tiles[i].Pop(0.3f + i * revealDelay); }
                else tiles[i].Hide();
            }
            TGTween.Delay(0.3f + shown * revealDelay + 0.1f, () => { if (collectButton != null) collectButton.gameObject.SetActive(true); });
        }

        protected override void OnClose()
        {
            base.OnClose();
            TGTween.Stop(autoOpen);
            if (glow != null) glow.transform.localScale = Vector3.one;
        }
    }
}
