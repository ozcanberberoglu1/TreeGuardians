using System;
using TMPro;
using TreeGuardians.Localization;
using TreeGuardians.Tools;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.Battle
{
    /// One tool button in the battle action bar.
    public sealed class ToolActionButton : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Image icon;
        [SerializeField] Image cooldownFill;
        [SerializeField] Image selectedFrame;
        [SerializeField] TMP_Text label;

        Action<int> onClick;
        int index;

        public void Bind(int i, ToolController.ToolSlot slot, Action<int> click)
        {
            index = i;
            onClick = click;
            if (button != null) { button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => onClick?.Invoke(index)); }
            bool has = slot.def != null;
            gameObject.SetActive(has);
            if (!has) return;
            if (icon != null) { icon.sprite = slot.def.icon; icon.color = slot.def.accentColor; }
            if (label != null) label.text = LocalizationService.Tr(slot.def.nameKey);
        }

        public void Refresh(ToolController.ToolSlot slot, bool selected)
        {
            if (slot.def == null) return;
            if (cooldownFill != null) cooldownFill.fillAmount = 1f - slot.Percent;
            if (selectedFrame != null) selectedFrame.enabled = selected;
            if (button != null) button.interactable = slot.IsReady;
        }
    }
}
