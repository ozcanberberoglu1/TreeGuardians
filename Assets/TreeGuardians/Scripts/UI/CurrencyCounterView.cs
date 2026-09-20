using TMPro;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI
{
    /// Top-bar currency counter; updates only when the value changes, with a count tween and punch.
    public sealed class CurrencyCounterView : MonoBehaviour
    {
        [SerializeField] CurrencyType currency = CurrencyType.Coins;
        [SerializeField] TMP_Text valueText;
        [SerializeField] Image icon;
        [SerializeField] RectTransform punchTarget;
        [SerializeField] float countDuration = 0.5f;

        int shown = int.MinValue;
        Coroutine counting;

        public CurrencyType Currency => currency;

        void OnEnable()
        {
            GameEventBus.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            BootAwaiter.WhenReady(RefreshImmediate);
        }

        void OnDisable()
        {
            GameEventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
        }

        void OnCurrencyChanged(CurrencyChangedEvent e)
        {
            if (e.type != currency || !isActiveAndEnabled) return;
            AnimateTo(e.newValue);
        }

        public void RefreshImmediate()
        {
            var progress = Services.Get<PlayerProgressService>();
            if (progress == null || progress.Wallet == null || valueText == null) return;
            int v = progress.Wallet.Get(currency);
            shown = v;
            valueText.SetText("{0}", v);
        }

        void AnimateTo(int value)
        {
            if (valueText == null) return;
            int from = shown == int.MinValue ? value : shown;
            shown = value;
            TGTween.Stop(counting);
            counting = TGTween.CountTo(valueText, from, value, QualityApplier.ReduceMotion ? 0f : countDuration, "{0:N0}");
            if (punchTarget != null && value > from) TGTween.PunchScale(punchTarget, 0.15f, 0.25f);
        }
    }
}
