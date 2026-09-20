using System;
using TMPro;
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
            if (portrait != null) portrait.sprite = def.portrait;
            if (frame != null && palette != null) frame.color = palette.GetColor(def.rarity);
            if (nameText != null) nameText.text = LocalizationService.Tr(def.nameKey);
            if (rarityText != null) rarityText.text = palette != null ? LocalizationService.Tr(palette.GetNameKey(def.rarity)) : def.rarity.ToString();
            if (descriptionText != null) descriptionText.text = LocalizationService.Tr(def.descriptionKey);
            Open();
            if (punchTarget != null) TGTween.PunchScale(punchTarget, 0.2f, 0.4f);
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
        }
    }
}
