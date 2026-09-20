using UnityEngine;

namespace TreeGuardians.UI
{
    /// Keeps a centered panel at its preferred width but never wider than its parent minus side margins (tablets, narrow aspects).
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public sealed class ResponsiveWidth : MonoBehaviour
    {
        [SerializeField] float preferredWidth = 1900f;
        [SerializeField] float sideMargin = 40f;

        RectTransform rt;
        float lastParentWidth = -1f;

        void OnEnable()
        {
            rt = GetComponent<RectTransform>();
            Apply();
        }

        void Update()
        {
            var parent = rt != null ? rt.parent as RectTransform : null;
            if (parent == null) return;
            if (!Mathf.Approximately(parent.rect.width, lastParentWidth)) Apply();
        }

        public void Apply()
        {
            if (rt == null) rt = GetComponent<RectTransform>();
            var parent = rt.parent as RectTransform;
            if (parent == null) return;
            lastParentWidth = parent.rect.width;
            float available = Mathf.Max(200f, parent.rect.width - sideMargin * 2f);
            var size = rt.sizeDelta;
            size.x = Mathf.Min(preferredWidth, available);
            rt.sizeDelta = size;
        }
    }
}
