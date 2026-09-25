using System;
using TreeGuardians.Core;
using TreeGuardians.Guardians;
using TreeGuardians.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.Battle
{
    /// One guardian card in the battle action bar.
    public sealed class GuardianActionButton : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform lift;
        [SerializeField] Image portrait;
        [SerializeField] Image cooldownFill;
        [SerializeField] Image healthFill;
        [SerializeField] Image energyFill;
        [SerializeField] UIFill healthBar;
        [SerializeField] UIFill energyBar;
        [SerializeField] Image selectedFrame;
        [SerializeField] Image specialGlow;
        [SerializeField] GameObject deadOverlay;
        [SerializeField] GameObject stunIcon;
        [SerializeField] float selectedLift = 18f;
        [SerializeField] float selectedScale = 1.06f;
        [SerializeField] float idleAlpha = 1f;
        [SerializeField] float waitingAlpha = 0.55f;
        [SerializeField] Color healthColor = new Color(0.47f, 0.82f, 0.29f);
        [SerializeField] Color healthLowColor = new Color(0.94f, 0.33f, 0.27f);
        [SerializeField] Color energyColor = new Color(0.36f, 0.72f, 1f);
        [SerializeField] Color energyFullColor = new Color(1f, 0.82f, 0.25f);
        [SerializeField] Color armedGlow = new Color(0.8f, 0.5f, 1f);
        [SerializeField] Color readyGlow = new Color(1f, 0.85f, 0.3f);

        public int Index { get; private set; }
        Action<int> onClick;
        float liftT;
        bool wasSelected;
        bool wasAlive = true;

        public void Bind(int index, GuardianController g, Action<int> click)
        {
            Index = index;
            onClick = click;
            if (button != null) { button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => onClick?.Invoke(Index)); }
            bool active = g != null && g.IsActive;
            gameObject.SetActive(active);
            if (!active) return;
            if (portrait != null) { portrait.sprite = g.Definition.portrait; portrait.preserveAspect = true; portrait.color = Color.white; }
            liftT = 0f;
            wasSelected = false;
            wasAlive = true;
            healthBar?.SetValue(1f, true);
            energyBar?.SetValue(g.EnergyPercent, true);
            ApplyLift();
            Refresh(g, false, false, true, false);
        }

        public void Refresh(GuardianController g, bool selected, bool specialArmed) => Refresh(g, selected, specialArmed, true, false);

        public void Refresh(GuardianController g, bool selected, bool specialArmed, bool canAct, bool turnBased)
        {
            if (g == null || !g.IsActive) return;
            bool alive = g.IsAlive;
            if (cooldownFill != null)
            {
                // Turn-based shots ignore cooldowns, so the sweep would only mislead.
                float cd = turnBased || !alive ? 0f : 1f - g.CooldownPercent;
                cooldownFill.fillAmount = cd;
                if (cooldownFill.enabled != cd > 0.001f) cooldownFill.enabled = cd > 0.001f;
            }
            float hp = g.HealthPercent;
            if (healthBar != null) { healthBar.SetValue(hp); healthBar.Color = hp > 0.35f ? healthColor : healthLowColor; }
            else if (healthFill != null) healthFill.fillAmount = hp;
            float en = g.EnergyPercent;
            if (energyBar != null) { energyBar.SetValue(en); energyBar.Color = g.SpecialReady ? energyFullColor : energyColor; }
            else if (energyFill != null) energyFill.fillAmount = en;
            if (selectedFrame != null && selectedFrame.enabled != (selected && alive)) selectedFrame.enabled = selected && alive;
            if (specialGlow != null)
            {
                bool show = alive && g.SpecialReady;
                if (specialGlow.enabled != show) specialGlow.enabled = show;
                if (show)
                {
                    var c = specialArmed && selected ? armedGlow : readyGlow;
                    c.a = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f);
                    specialGlow.color = c;
                }
            }
            if (deadOverlay != null && deadOverlay.activeSelf != !alive) deadOverlay.SetActive(!alive);
            if (stunIcon != null)
            {
                bool stun = alive && (g.IsStunned || g.IsRooted);
                if (stunIcon.activeSelf != stun) stunIcon.SetActive(stun);
            }
            if (portrait != null) portrait.color = alive ? Color.white : new Color(0.35f, 0.35f, 0.35f, 1f);
            if (button != null) button.interactable = alive && canAct;
            if (group != null)
            {
                float a = !alive ? 0.7f : canAct ? idleAlpha : waitingAlpha;
                group.alpha = Mathf.MoveTowards(group.alpha, a, Time.unscaledDeltaTime * 4f);
            }
            if (alive != wasAlive) { wasAlive = alive; if (!alive) TGTween.ShakeLocal(transform, 10f, 0.3f); }
            bool sel = selected && alive;
            if (sel && !wasSelected) TGTween.PunchScale(lift != null ? lift : transform, 0.08f, 0.18f);
            wasSelected = sel;
            float target = sel ? 1f : 0f;
            if (!Mathf.Approximately(liftT, target))
            {
                liftT = Mathf.MoveTowards(liftT, target, Time.unscaledDeltaTime * 8f);
                ApplyLift();
            }
        }

        void ApplyLift()
        {
            if (lift == null) return;
            float e = TGTween.Evaluate(Ease.OutQuad, liftT);
            lift.anchoredPosition = new Vector2(0f, selectedLift * e);
            lift.localScale = Vector3.one * Mathf.Lerp(1f, selectedScale, e);
        }
    }
}
