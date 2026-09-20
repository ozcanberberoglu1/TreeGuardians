using TMPro;
using TreeGuardians.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI
{
    /// One pre-authored reward tile (icon + label + sub-label).
    public sealed class RewardItemView : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] TMP_Text label;
        [SerializeField] TMP_Text sub;
        [SerializeField] GameObject newBadge;
        [SerializeField] CanvasGroup group;

        public void Bind(in RewardItem item)
        {
            if (icon != null) { icon.sprite = item.icon; icon.color = item.tint; icon.enabled = item.icon != null; }
            if (label != null) label.text = item.label;
            if (sub != null) sub.text = item.sub;
            if (newBadge != null) newBadge.SetActive(item.isNew);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void Pop(float delay)
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            transform.localScale = Vector3.zero;
            if (group != null) group.alpha = 0f;
            TGTween.Delay(delay, () =>
            {
                if (this == null) return;
                if (group != null) TGTween.FadeCanvasGroup(group, 1f, 0.12f);
                TGTween.ScaleTo(transform, Vector3.one, QualityApplier.ReduceMotion ? 0f : 0.28f, Ease.OutBack);
            });
        }
    }
}
