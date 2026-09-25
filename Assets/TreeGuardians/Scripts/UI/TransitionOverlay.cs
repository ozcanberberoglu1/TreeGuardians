using System;
using TreeGuardians.Audio;
using TreeGuardians.Core;
using TreeGuardians.Data;
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
        [Tooltip("Ekran kararırken (sahne çıkışı) çalan UI sesi. None = sessiz.")]
        [SerializeField] AudioEventId coverSound = AudioEventId.Transition;
        [SerializeField, Range(0f, 1f)] float coverSoundVolume = 1f;

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
            // UI pool: plays during a paused battle too; in the tap's frame it replaces the button's generic click.
            if (coverSound != AudioEventId.None) Services.Get<AudioService>()?.PlayUi(coverSound, coverSoundVolume);
            TGTween.FadeCanvasGroup(group, 1f, QualityApplier.ReduceMotion ? 0f : fadeOutDuration, true, onCovered);
        }
    }
}
