using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI
{
    /// Uniformly scales a fixed-size row (cards, tiles) down when its parent is narrower than the row's preferred width.
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public sealed class FitRowScale : MonoBehaviour
    {
        [SerializeField] float padding = 24f;
        [SerializeField] float minScale = 0.5f;

        RectTransform rt;
        float lastParentWidth = -1f;
        float lastPreferred = -1f;

        void OnEnable()
        {
            rt = GetComponent<RectTransform>();
            lastParentWidth = -1f;
        }

        void LateUpdate()
        {
            if (rt == null) rt = GetComponent<RectTransform>();
            var parent = rt.parent as RectTransform;
            if (parent == null) return;
            float preferred = LayoutUtility.GetPreferredWidth(rt);
            if (preferred <= 1f) preferred = rt.rect.width;
            if (Mathf.Approximately(parent.rect.width, lastParentWidth) && Mathf.Approximately(preferred, lastPreferred)) return;
            lastParentWidth = parent.rect.width;
            lastPreferred = preferred;
            float available = Mathf.Max(1f, parent.rect.width - padding * 2f);
            float scale = Mathf.Clamp(available / Mathf.Max(1f, preferred), minScale, 1f);
            rt.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
