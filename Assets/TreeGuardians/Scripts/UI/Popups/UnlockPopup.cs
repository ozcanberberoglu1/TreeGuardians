using System;
using TMPro;
using TreeGuardians.Audio;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Popups
{
    public sealed class UnlockPopup : UIPopup
    {
        [SerializeField] TMP_Text titleText;
        [SerializeField] Image portrait;
        [SerializeField] Image frame;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text rarityText;
        [SerializeField] TMP_Text descriptionText;
        [SerializeField] Button okButton;
        [SerializeField] RectTransform punchTarget;

        [Header("Audio")]
        [Tooltip("Kilit açma fanfarı sırasında müzik ve ambiyansın inildiği seviye (0-1).")]
        [SerializeField, Range(0f, 1f)] float unlockDuckTo = 0.35f;
        [Tooltip("Kısmanın süresi, saniye (fanfar yaklaşık 2.2 sn).")]
        [SerializeField] float unlockDuckHold = 2f;

        Action onDone;

        protected override void Awake()
        {
            base.Awake();
            if (okButton != null) okButton.onClick.AddListener(() => { var cb = onDone; Close(); cb?.Invoke(); });
        }

        public void ShowGuardian(GuardianDefinition def, RarityPalette palette, Action done = null)
        {
            onDone = done;
            if (titleText != null) titleText.text = LocalizationService.Tr("popup_unlock_title");
            if (portrait != null) { portrait.sprite = def.portrait; portrait.preserveAspect = true; }
            if (frame != null && palette != null) frame.color = palette.GetColor(def.rarity);
            if (nameText != null) nameText.text = LocalizationService.Tr(def.nameKey);
            if (rarityText != null)
            {
                rarityText.text = palette != null ? LocalizationService.Tr(palette.GetNameKey(def.rarity)) : def.rarity.ToString();
                if (palette != null) rarityText.color = palette.GetColor(def.rarity);
            }
            if (descriptionText != null) descriptionText.text = LocalizationService.Tr(def.descriptionKey);
            Open();
            if (punchTarget != null) TGTween.PunchScale(punchTarget, 0.2f, 0.4f);
            PlayUnlockSound();
        }

        public void ShowTool(ToolDefinition def, Action done = null)
        {
            onDone = done;
            if (titleText != null) titleText.text = LocalizationService.Tr("popup_unlock_tool");
            if (portrait != null) portrait.sprite = def.icon;
            if (frame != null) frame.color = def.accentColor;
            if (nameText != null) nameText.text = LocalizationService.Tr(def.nameKey);
            if (rarityText != null) rarityText.text = "";
            if (descriptionText != null) descriptionText.text = LocalizationService.Tr(def.descriptionKey);
            Open();
            if (punchTarget != null) TGTween.PunchScale(punchTarget, 0.2f, 0.4f);
            PlayUnlockSound();
        }

        /// Fanfare over a ducked music/ambience bed. The library entry also ducks; the explicit duck keeps the bed down
        /// for the whole fanfare even with an older AudioLibrary asset (Duck keeps the deepest level and the latest end).
        void PlayUnlockSound()
        {
            var sfx = Services.Get<AudioService>();
            if (sfx == null) return;
            sfx.PlayUi(AudioEventId.Unlock);
            sfx.Duck(unlockDuckTo, unlockDuckHold);
        }
    }
}
