using TreeGuardians.Core;
using UnityEngine;

namespace TreeGuardians.UI
{
    /// Shared dimmer behind popups; reference counted so stacked popups keep it visible.
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class PopupDimmer : MonoBehaviour
    {
        public static PopupDimmer Instance { get; private set; }

        [SerializeField] float alpha = 0.6f;
        [SerializeField] float fade = 0.15f;

        CanvasGroup group;
        int count;

        void Awake()
        {
            Instance = this;
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Retain()
        {
            count++;
            if (count == 1)
            {
                group.blocksRaycasts = true;
                TGTween.FadeCanvasGroup(group, alpha, fade);
            }
        }

        public void Release()
        {
            count = Mathf.Max(0, count - 1);
            if (count == 0)
            {
                TGTween.FadeCanvasGroup(group, 0f, fade, true, () => { if (count == 0) group.blocksRaycasts = false; });
            }
        }
    }
}
