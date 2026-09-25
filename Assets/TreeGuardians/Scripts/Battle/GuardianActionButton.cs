using System;
using TreeGuardians.Guardians;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.Battle
{
    /// One guardian card in the battle action bar.
    public sealed class GuardianActionButton : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Image portrait;
        [SerializeField] Image cooldownFill;
        [SerializeField] Image healthFill;
        [SerializeField] Image energyFill;
        [SerializeField] Image selectedFrame;
        [SerializeField] Image specialGlow;
        [SerializeField] GameObject deadOverlay;
        [SerializeField] GameObject stunIcon;

        public int Index { get; private set; }
        Action<int> onClick;

        public void Bind(int index, GuardianController g, Action<int> click)
        {
            Index = index;
            onClick = click;
            if (button != null) { button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => onClick?.Invoke(Index)); }
            bool active = g != null && g.IsActive;
            gameObject.SetActive(active);
            if (!active) return;
            if (portrait != null) { portrait.sprite = g.Definition.portrait; portrait.preserveAspect = true; }
            Refresh(g, false, false);
        }

        public void Refresh(GuardianController g, bool selected, bool specialArmed)
        {
            if (g == null || !g.IsActive) return;
            if (cooldownFill != null) cooldownFill.fillAmount = 1f - g.CooldownPercent;
            if (healthFill != null) healthFill.fillAmount = g.HealthPercent;
            if (energyFill != null) energyFill.fillAmount = g.EnergyPercent;
            if (selectedFrame != null) selectedFrame.enabled = selected;
            if (specialGlow != null) { specialGlow.enabled = g.SpecialReady; specialGlow.color = specialArmed && selected ? new Color(0.8f, 0.5f, 1f) : new Color(1f, 0.85f, 0.3f); }
            if (deadOverlay != null) deadOverlay.SetActive(!g.IsAlive);
            if (stunIcon != null) stunIcon.SetActive(g.IsAlive && (g.IsStunned || g.IsRooted));
            if (button != null) button.interactable = g.IsAlive;
        }
    }
}
