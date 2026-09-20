using System;
using TMPro;
using TreeGuardians.Data;
using TreeGuardians.Localization;
using TreeGuardians.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Menu
{
    public sealed class ToolCardView : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] Image frame;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text levelText;
        [SerializeField] GameObject lockedOverlay;
        [SerializeField] GameObject emptyState;
        [SerializeField] Button button;
        [SerializeField] UIButtonFeedback feedback;

        public string ToolId { get; private set; }
        public int SlotIndex { get; set; } = -1;
        Action<ToolCardView> onClick;

        void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            if (feedback == null) feedback = GetComponent<UIButtonFeedback>();
            if (button != null) button.onClick.AddListener(() => onClick?.Invoke(this));
        }

        public void SetClickHandler(Action<ToolCardView> h) => onClick = h;

        public void BindEmpty()
        {
            ToolId = "";
            if (emptyState != null) emptyState.SetActive(true);
            if (icon != null) icon.enabled = false;
            if (nameText != null) nameText.text = "";
            if (levelText != null) levelText.text = "";
            if (lockedOverlay != null) lockedOverlay.SetActive(false);
            if (frame != null) frame.color = new Color(0.4f, 0.45f, 0.5f);
            gameObject.SetActive(true);
        }

        public void Bind(ToolDefinition def, PlayerProgressService progress)
        {
            ToolId = def.id;
            bool unlocked = progress.IsToolUnlocked(def.id);
            if (emptyState != null) emptyState.SetActive(false);
            if (icon != null) { icon.enabled = true; icon.sprite = def.icon; icon.color = unlocked ? Color.white : new Color(0.5f, 0.5f, 0.55f); }
            if (frame != null) frame.color = def.accentColor;
            if (nameText != null) nameText.text = LocalizationService.Tr(def.nameKey);
            if (levelText != null) levelText.text = unlocked ? string.Format(LocalizationService.Tr("ui_level_short"), progress.GetToolLevel(def.id)) : LocalizationService.Tr("ui_locked");
            if (lockedOverlay != null) lockedOverlay.SetActive(!unlocked);
            gameObject.SetActive(true);
        }

        public void SetSelected(bool v) => feedback?.SetSelected(v);
    }
}
