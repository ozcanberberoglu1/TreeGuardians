using System;
using TMPro;
using TreeGuardians.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Popups
{
    public sealed class GenericConfirmPopup : UIPopup
    {
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text bodyText;
        [SerializeField] Button yesButton;
        [SerializeField] Button noButton;
        [SerializeField] TMP_Text yesLabel;
        [SerializeField] TMP_Text noLabel;

        Action onYes;
        Action onNo;

        protected override void Awake()
        {
            base.Awake();
            if (yesButton != null) yesButton.onClick.AddListener(() => { var cb = onYes; Close(); cb?.Invoke(); });
            if (noButton != null) noButton.onClick.AddListener(() => { var cb = onNo; Close(); cb?.Invoke(); });
        }

        public void Show(string titleKey, string bodyKey, Action yes, Action no = null, string bodyLiteral = null, string yesKey = "ui_yes", string noKey = "ui_no", bool showNo = true)
        {
            onYes = yes;
            onNo = no;
            if (titleText != null) titleText.text = LocalizationService.Tr(titleKey);
            if (bodyText != null) bodyText.text = bodyLiteral ?? LocalizationService.Tr(bodyKey);
            if (yesLabel != null) yesLabel.text = LocalizationService.Tr(yesKey);
            if (noLabel != null) noLabel.text = LocalizationService.Tr(noKey);
            if (noButton != null) noButton.gameObject.SetActive(showNo);
            Open();
        }
    }
}
