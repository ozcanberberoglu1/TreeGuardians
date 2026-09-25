using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI
{
    /// Fill bar for 9-sliced art: resizes the fill rect by anchors so rounded caps never squash
    /// (Image.Type.Filled stretches sliced sprites). Optional trailing "ghost" fill shows recent loss.
    [DisallowMultipleComponent]
    public sealed class UIFill : MonoBehaviour
    {
        [SerializeField] RectTransform fill;
        [SerializeField] Image fillImage;
        [SerializeField] RectTransform ghost;
        [SerializeField] bool vertical;
        [Tooltip("Sağdan sola (ya da yukarıdan aşağı) dolar; rakip çubuğu için.")] [SerializeField] bool reverse;
        [SerializeField, Range(0f, 1f)] float value = 1f;
        [Tooltip("Smallest visible fill so the rounded caps never invert (fraction of full size).")]
        [SerializeField, Range(0f, 0.3f)] float minVisible = 0.06f;
        [SerializeField] float ghostDelay = 0.35f;
        [SerializeField] float ghostSpeed = 0.9f;

        float ghostValue = 1f;
        float ghostHoldUntil;

        public float Value => value;
        public Image FillImage => fillImage;

        public Color Color
        {
            get => fillImage != null ? fillImage.color : Color.white;
            set { if (fillImage != null && fillImage.color != value) fillImage.color = value; }
        }

        public void SetValue(float v, bool instant = false)
        {
            v = Mathf.Clamp01(v);
            if (Mathf.Approximately(v, value) && !instant) return;
            if (v < value) ghostHoldUntil = Time.unscaledTime + ghostDelay;
            value = v;
            if (instant || v > ghostValue) ghostValue = v;
            Apply(fill, value);
            Apply(ghost, ghostValue);
        }

        void Update()
        {
            if (ghost == null || ghostValue <= value || Time.unscaledTime < ghostHoldUntil) return;
            ghostValue = Mathf.MoveTowards(ghostValue, value, ghostSpeed * Time.unscaledDeltaTime);
            Apply(ghost, ghostValue);
        }

        void Apply(RectTransform rt, float v)
        {
            if (rt == null) return;
            bool visible = v > 0.001f;
            if (rt.gameObject.activeSelf != visible) rt.gameObject.SetActive(visible);
            if (!visible) return;
            float shown = Mathf.Lerp(minVisible, 1f, v);
            var min = rt.anchorMin;
            var max = rt.anchorMax;
            if (vertical) { if (reverse) { min.y = 1f - shown; max.y = 1f; } else { min.y = 0f; max.y = shown; } }
            else { if (reverse) { min.x = 1f - shown; max.x = 1f; } else { min.x = 0f; max.x = shown; } }
            rt.anchorMin = min;
            rt.anchorMax = max;
        }

        void OnValidate()
        {
            if (fill != null && fillImage == null) fillImage = fill.GetComponent<Image>();
            ghostValue = value;
            Apply(fill, value);
            Apply(ghost, value);
        }

#if UNITY_EDITOR
        public void EditorSetup(RectTransform fillRect, RectTransform ghostRect, bool isVertical, bool isReverse = false)
        {
            reverse = isReverse;
            fill = fillRect;
            fillImage = fillRect != null ? fillRect.GetComponent<Image>() : null;
            ghost = ghostRect;
            vertical = isVertical;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
