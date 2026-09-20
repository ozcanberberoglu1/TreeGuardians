using System.Collections;
using TMPro;
using TreeGuardians.Core;
using TreeGuardians.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.SceneFlow
{
    /// Shows real AsyncOperation progress (smoothed), rotates localized tips, then activates the next scene.
    public sealed class LoadingController : MonoBehaviour
    {
        [Header("UI (scene-local)")]
        [SerializeField] Image barFill;
        [SerializeField] TMP_Text percentText;
        [SerializeField] TMP_Text tipText;
        [SerializeField] RectTransform spinner;
        [SerializeField] CanvasGroup rootGroup;
        [SerializeField] RectTransform leavesLayer;

        [Header("Motion")]
        [SerializeField] float spinnerDegreesPerSecond = 120f;
        [SerializeField] float leavesDriftSpeed = 12f;

        float displayed;
        float target;
        int tipIndex;
        float tipTimer;
        int lastPercent = -1;

        IEnumerator Start()
        {
            var flow = Services.Get<SceneFlowService>();
            var cfgProvider = Services.Get<GameConfigProvider>();
            var loc = Services.Get<LocalizationService>();
            var cfg = cfgProvider != null ? cfgProvider.Config : null;
            float minSeconds = cfg != null ? cfg.minLoadingSeconds : 1f;
            float smoothing = cfg != null ? cfg.loadingBarSmoothing : 6f;
            float tipInterval = cfg != null ? cfg.loadingTipIntervalSeconds : 2.5f;

            if (rootGroup != null) { rootGroup.alpha = 0f; TGTween.FadeCanvasGroup(rootGroup, 1f, 0.2f); }
            ShowTip(loc);

            if (flow == null)
            {
                TGLog.Warn("LoadingController: SceneFlowService missing (scene played directly). Staying on Loading.");
                yield break;
            }

            var op = flow.BeginLoadNext();
            if (op == null)
            {
                TGLog.Error("LoadingController: next scene could not be loaded. Is it in Build Settings?");
                yield break;
            }

            float startTime = Time.unscaledTime;
            while (true)
            {
                float real = Mathf.Clamp01(op.progress / 0.9f);
                float elapsedRatio = Mathf.Clamp01((Time.unscaledTime - startTime) / Mathf.Max(0.01f, minSeconds));
                target = Mathf.Min(real, Mathf.Max(elapsedRatio, real * 0.35f));
                if (real >= 1f && elapsedRatio >= 1f) target = 1f;

                displayed = Mathf.MoveTowards(displayed, target, smoothing * Time.unscaledDeltaTime);
                UpdateBar(displayed);
                UpdateMotion();

                tipTimer += Time.unscaledDeltaTime;
                if (tipTimer >= tipInterval) { tipTimer = 0f; tipIndex++; ShowTip(loc); }

                if (displayed >= 0.999f && op.progress >= 0.9f && Time.unscaledTime - startTime >= minSeconds) break;
                yield return null;
            }

            UpdateBar(1f);
            if (rootGroup != null) yield return TGTween.FadeCanvasGroup(rootGroup, 0f, 0.15f);
            released = true;
            flow.ActivatePendingLoad();
        }

        bool released;

        void OnDestroy()
        {
            // If the Loading scene is torn down early (tests, forced loads), never leave a held-back load behind.
            if (!released) Services.Get<SceneFlowService>()?.ActivatePendingLoad();
        }

        void UpdateBar(float value)
        {
            if (barFill != null) barFill.fillAmount = value;
            int pct = Mathf.RoundToInt(value * 100f);
            if (pct != lastPercent && percentText != null)
            {
                lastPercent = pct;
                percentText.SetText("{0}%", pct);
            }
        }

        void UpdateMotion()
        {
            if (QualityApplier.ReduceMotion) return;
            if (spinner != null) spinner.Rotate(0f, 0f, -spinnerDegreesPerSecond * Time.unscaledDeltaTime);
            if (leavesLayer != null)
            {
                var p = leavesLayer.anchoredPosition;
                p.x -= leavesDriftSpeed * Time.unscaledDeltaTime;
                if (p.x < -200f) p.x += 200f;
                leavesLayer.anchoredPosition = p;
            }
        }

        void ShowTip(LocalizationService loc)
        {
            if (tipText == null) return;
            var keys = loc != null ? loc.LoadingTipKeys : null;
            if (keys == null || keys.Count == 0) { tipText.text = ""; return; }
            tipText.text = loc.Get(keys[tipIndex % keys.Count]);
        }
    }
}
