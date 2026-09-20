using System;
using TreeGuardians.Audio;
using TreeGuardians.Core;
using TreeGuardians.Data;
using UnityEngine;

namespace TreeGuardians.UI
{
    /// Pre-authored panel that a controller opens/closes. Fade + small scale, 0.15–0.3s, unscaled time.
    [RequireComponent(typeof(CanvasGroup))]
    public class UIPanel : MonoBehaviour
    {
        [SerializeField] float openDuration = 0.22f;
        [SerializeField] float closeDuration = 0.15f;
        [SerializeField] float closedScale = 0.94f;
        [SerializeField] bool playSounds = true;
        [SerializeField] RectTransform scaleTarget;

        CanvasGroup group;
        Coroutine fade;
        Coroutine scale;

        public bool IsOpen { get; private set; }
        public event Action OnOpened;
        public event Action OnClosed;

        protected virtual void Awake()
        {
            group = GetComponent<CanvasGroup>();
            if (scaleTarget == null) scaleTarget = transform as RectTransform;
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            gameObject.SetActive(true);
            group.blocksRaycasts = true;
            group.interactable = true;
            TGTween.Stop(fade);
            TGTween.Stop(scale);
            bool reduce = QualityApplier.ReduceMotion;
            float dur = reduce ? 0f : openDuration;
            group.alpha = reduce ? 1f : 0f;
            if (scaleTarget != null) scaleTarget.localScale = reduce ? Vector3.one : Vector3.one * closedScale;
            fade = TGTween.FadeCanvasGroup(group, 1f, dur);
            if (scaleTarget != null) scale = TGTween.ScaleTo(scaleTarget, Vector3.one, dur, Ease.OutBack);
            if (playSounds) Services.Get<AudioService>()?.PlayUi(AudioEventId.UiPanelOpen);
            OnOpen();
            OnOpened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            group.blocksRaycasts = false;
            group.interactable = false;
            TGTween.Stop(fade);
            TGTween.Stop(scale);
            float dur = QualityApplier.ReduceMotion ? 0f : closeDuration;
            fade = TGTween.FadeCanvasGroup(group, 0f, dur, true, () => { if (!IsOpen) gameObject.SetActive(false); });
            if (scaleTarget != null) scale = TGTween.ScaleTo(scaleTarget, Vector3.one * closedScale, dur, Ease.InQuad);
            if (playSounds) Services.Get<AudioService>()?.PlayUi(AudioEventId.UiPanelClose);
            OnClose();
            OnClosed?.Invoke();
        }

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        /// Instantly hide without animation (used at scene start).
        public void HideImmediate()
        {
            IsOpen = false;
            if (group == null) group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            gameObject.SetActive(false);
        }

        protected virtual void OnOpen() { }
        protected virtual void OnClose() { }
    }
}
