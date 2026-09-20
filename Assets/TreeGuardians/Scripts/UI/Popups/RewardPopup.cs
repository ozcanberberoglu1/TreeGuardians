using System;
using System.Collections.Generic;
using TMPro;
using TreeGuardians.Audio;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Localization;
using TreeGuardians.Rewards;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Popups
{
    /// Reveals rewards one by one on pre-authored tiles, then "Collect All".
    public sealed class RewardPopup : UIPopup
    {
        [SerializeField] TMP_Text titleText;
        [SerializeField] RewardItemView[] tiles = new RewardItemView[8];
        [SerializeField] TMP_Text overflowText;
        [SerializeField] Button collectButton;
        [SerializeField] RewardSprites sprites = new RewardSprites();
        [SerializeField] float revealDelay = 0.15f;

        readonly List<RewardItem> items = new List<RewardItem>(16);
        Action onDone;

        protected override void Awake()
        {
            base.Awake();
            if (collectButton != null) collectButton.onClick.AddListener(() => { var cb = onDone; Close(); cb?.Invoke(); });
        }

        public void Show(RewardBundle bundle, GameDatabase db, Action done = null, string titleKey = "popup_reward_title")
        {
            onDone = done;
            if (titleText != null) titleText.text = LocalizationService.Tr(titleKey);
            RewardPresenter.Build(bundle, db, sprites, items);
            int shown = Mathf.Min(items.Count, tiles.Length);
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] == null) continue;
                if (i < shown)
                {
                    tiles[i].Bind(items[i]);
                    tiles[i].Pop(0.1f + i * revealDelay);
                }
                else tiles[i].Hide();
            }
            if (overflowText != null)
            {
                int extra = items.Count - shown;
                overflowText.gameObject.SetActive(extra > 0);
                if (extra > 0) overflowText.text = "+" + extra;
            }
            if (collectButton != null) collectButton.interactable = false;
            Open();
            Services.Get<AudioService>()?.PlayUi(AudioEventId.RewardPop);
            TGTween.Delay(0.1f + shown * revealDelay + 0.1f, () => { if (collectButton != null) collectButton.interactable = true; });
        }
    }
}
