using System;
using TreeGuardians.Core;
using UnityEngine;

namespace TreeGuardians.UI
{
    /// Full-screen fade used when entering/leaving a scene. Lives in each scene's canvas.
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class TransitionOverlay : MonoBehaviour
    {
        [SerializeField] float fadeInDuration = 0.25f;
        [SerializeField] float fadeOutDuration = 0.2f;
        [SerializeField] bool fadeInOnStart = true;

        CanvasGroup group;

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.alpha = fadeInOnStart ? 1f : 0f;
            group.blocksRaycasts = fadeInOnStart;
        }

        void Start()
        {
            if (fadeInOnStart) Reveal();
        }

        /// Fade the overlay away to reveal the scene.
        public void Reveal(Action onDone = null)
        {
            group.blocksRaycasts = true;
            TGTween.FadeCanvasGroup(group, 0f, QualityApplier.ReduceMotion ? 0f : fadeInDuration, true, () =>
            {
                group.blocksRaycasts = false;
                onDone?.Invoke();
            });
        }

        /// Fade to opaque, then run the callback (typically a scene load).
        public void Cover(Action onCovered)
        {
            group.blocksRaycasts = true;
            TGTween.FadeCanvasGroup(group, 1f, QualityApplier.ReduceMotion ? 0f : fadeOutDuration, true, onCovered);
        }
    }
}
