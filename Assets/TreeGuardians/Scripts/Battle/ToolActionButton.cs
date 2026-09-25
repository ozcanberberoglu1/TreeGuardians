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
        [SerializeField] CanvasGroup group;
        [SerializeField] Image icon;
        [SerializeField] Image cooldownFill;
        [SerializeField] Image selectedFrame;
        [SerializeField] TMP_Text label;
        [SerializeField] TMP_Text cooldownText;
        [SerializeField] float waitingAlpha = 0.55f;

        Action<int> onClick;
        int index;
        int lastCd = -1;

        public void Bind(int i, ToolController.ToolSlot slot, Action<int> click)
        {
            index = i;
            onClick = click;
            if (button != null) { button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => onClick?.Invoke(index)); }
            bool has = slot.def != null;
            gameObject.SetActive(has);
            if (!has) return;
            if (icon != null) { icon.sprite = slot.def.icon; icon.color = Color.white; icon.preserveAspect = true; }
            if (label != null) label.text = LocalizationService.Tr(slot.def.nameKey);
            lastCd = -1;
        }

        public void Refresh(ToolController.ToolSlot slot, bool selected) => Refresh(slot, selected, true);

        public void Refresh(ToolController.ToolSlot slot, bool selected, bool canAct)
        {
            if (slot.def == null) return;
            bool ready = slot.IsReady;
            if (cooldownFill != null) cooldownFill.fillAmount = ready ? 0f : 1f - slot.Percent;
            if (cooldownText != null)
            {
                int cd = ready ? 0 : Mathf.CeilToInt(slot.cooldown);
                if (cd != lastCd)
                {
                    lastCd = cd;
                    cooldownText.gameObject.SetActive(cd > 0);
                    if (cd > 0) cooldownText.SetText("{0}", cd);
                }
            }
            if (selectedFrame != null && selectedFrame.enabled != selected) selectedFrame.enabled = selected;
            if (button != null) button.interactable = ready && canAct;
            if (icon != null) icon.color = ready ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
            if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, canAct ? 1f : waitingAlpha, Time.unscaledDeltaTime * 4f);
        }
    }
}
