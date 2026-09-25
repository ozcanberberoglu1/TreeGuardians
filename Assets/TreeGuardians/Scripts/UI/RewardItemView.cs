using TMPro;
using TreeGuardians.Audio;
using TreeGuardians.Core;
using TreeGuardians.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI
{
    /// One pre-authored reward tile (icon + label + sub-label). Pop() reveals it with a scale-in, a pop sound that rises
    /// in pitch along the row, and a culture-aware count-up of the amount.
    public sealed class RewardItemView : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] TMP_Text label;
        [SerializeField] TMP_Text sub;
        [SerializeField] GameObject newBadge;
        [SerializeField] CanvasGroup group;

        [Header("Reveal")]
        [SerializeField] bool popSound = true;
        [SerializeField, Range(0f, 1f)] float popVolume = 0.6f;
        [Tooltip("Pitch added per sibling index so a row of tiles plays a rising run.")]
        [SerializeField] float popPitchStep = 0.06f;
        [SerializeField] bool countUp = true;
        [SerializeField] float countDuration = 0.45f;

        int amount;
        string amountFormat;
        Coroutine counting;

        public void Bind(in RewardItem item)
        {
            if (icon != null) { icon.sprite = item.icon; icon.color = item.tint; icon.enabled = item.icon != null; icon.preserveAspect = true; }
            if (label != null) label.text = item.label;
            if (sub != null) sub.text = item.sub;
            if (newBadge != null) newBadge.SetActive(item.isNew);
            amount = item.amount;
            amountFormat = item.amountFormat;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            TGTween.Stop(counting);
            counting = null;
            gameObject.SetActive(false);
        }

        public void Pop(float delay)
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            bool reduce = QualityApplier.ReduceMotion;
            bool count = countUp && !reduce && label != null && !string.IsNullOrEmpty(amountFormat) && amount != 0;
            TGTween.Stop(counting);
            counting = null;
            transform.localScale = Vector3.zero;
            if (group != null) group.alpha = 0f;
            if (count) label.SetText(string.Format(TGTween.NumberCulture, amountFormat, 0));
            int target = amount;
            string format = amountFormat;
            TGTween.Delay(delay, () =>
            {
                if (this == null) return;
                if (group != null) TGTween.FadeCanvasGroup(group, 1f, reduce ? 0f : 0.12f);
                TGTween.ScaleTo(transform, Vector3.one, reduce ? 0f : 0.28f, Ease.OutBack);
                if (popSound)
                    Services.Get<AudioService>()?.PlaySfx(AudioEventId.RewardPop, popVolume, 1f + popPitchStep * transform.GetSiblingIndex(), 0f);
                if (count && label != null) counting = TGTween.CountTo(label, 0, target, countDuration, format);
            });
        }
    }
}
