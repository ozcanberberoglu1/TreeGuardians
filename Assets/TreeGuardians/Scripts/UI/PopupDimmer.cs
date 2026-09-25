using TreeGuardians.Core;
using UnityEngine;
using UnityEngine.UI;

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
        Coroutine fadeRoutine;
        int count;

        void Awake()
        {
            Instance = this;
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            // A full-screen dimmer must stretch: Preserve Aspect on a square sprite only darkens a centred square.
            var image = GetComponent<Image>();
            if (image != null) image.preserveAspect = false;
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
                TGTween.Stop(fadeRoutine);
                fadeRoutine = TGTween.FadeCanvasGroup(group, alpha, fade);
            }
        }

        public void Release()
        {
            if (count <= 0) return;
            count--;
            if (count == 0)
            {
                TGTween.Stop(fadeRoutine);
                fadeRoutine = TGTween.FadeCanvasGroup(group, 0f, fade, true, () => { if (count == 0) group.blocksRaycasts = false; });
            }
        }
    }
}
