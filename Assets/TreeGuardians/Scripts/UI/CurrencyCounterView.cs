using TMPro;
using TreeGuardians.Audio;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Localization;
using TreeGuardians.Meta;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI
{
    /// Top-bar currency counter; updates only when the value changes, with a count tween, punch and coin sound.
    /// Numbers use the active language's thousands separator ("2,183" EN / "2.183" TR).
    public sealed class CurrencyCounterView : MonoBehaviour
    {
        [SerializeField] CurrencyType currency = CurrencyType.Coins;
        [SerializeField] TMP_Text valueText;
        [SerializeField] Image icon;
        [SerializeField] RectTransform punchTarget;
        [SerializeField] float countDuration = 0.5f;
        [Tooltip("Coin shower sound when the value goes up (never for trophies).")]
        [SerializeField] bool countSound = true;
        [SerializeField, Range(0f, 1f)] float countSoundVolume = 1f;

        int shown = int.MinValue;
        Coroutine counting;

        public CurrencyType Currency => currency;

        void OnEnable()
        {
            GameEventBus.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            GameEventBus.Subscribe<LanguageChangedEvent>(OnLanguageChanged);
            BootAwaiter.WhenReady(RefreshImmediate);
        }

        void OnDisable()
        {
            GameEventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            GameEventBus.Unsubscribe<LanguageChangedEvent>(OnLanguageChanged);
        }

        void OnCurrencyChanged(CurrencyChangedEvent e)
        {
            if (e.type != currency || !isActiveAndEnabled) return;
            AnimateTo(e.newValue);
        }

        void OnLanguageChanged(LanguageChangedEvent e)
        {
            if (!isActiveAndEnabled) return;
            TGTween.Stop(counting);
            counting = null;
            RefreshImmediate();
        }

        public void RefreshImmediate()
        {
            if (this == null) return;
            var progress = Services.Get<PlayerProgressService>();
            if (progress == null || progress.Wallet == null || valueText == null) return;
            int v = progress.Wallet.Get(currency);
            shown = v;
            valueText.SetText(LocalizationService.Number(v));
        }

        void AnimateTo(int value)
        {
            if (valueText == null) return;
            int from = shown == int.MinValue ? value : shown;
            shown = value;
            TGTween.Stop(counting);
            counting = TGTween.CountTo(valueText, from, value, QualityApplier.ReduceMotion ? 0f : countDuration, "{0:N0}");
            if (value > from)
            {
                if (punchTarget != null && !QualityApplier.ReduceMotion) TGTween.PunchScale(punchTarget, 0.15f, 0.25f);
                if (countSound && currency != CurrencyType.Trophies)
                    Services.Get<AudioService>()?.PlaySfx(AudioEventId.CoinCount, countSoundVolume);
            }
        }
    }
}
