using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace TreeGuardians.Core
{
    public enum Ease { Linear, OutQuad, InQuad, InOutQuad, OutCubic, OutBack, OutElastic }

    public static class TGTween
    {
        /// Number culture for CountTo (set by LocalizationService when the language changes).
        public static System.IFormatProvider NumberCulture = System.Globalization.CultureInfo.InvariantCulture;

        public static float Evaluate(Ease ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case Ease.OutQuad: return 1f - (1f - t) * (1f - t);
                case Ease.InQuad: return t * t;
                case Ease.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                case Ease.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                case Ease.OutBack:
                {
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
                }
                case Ease.OutElastic:
                {
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    const float c4 = (2f * Mathf.PI) / 3f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
                }
                default: return t;
            }
        }

        static Coroutine Run(IEnumerator routine)
        {
            var r = TweenRunner.Instance;
            if (r == null || !r.isActiveAndEnabled) return null;
            return r.StartCoroutine(routine);
        }

        public static void Stop(Coroutine c)
        {
            if (c != null && TweenRunner.Instance != null) TweenRunner.Instance.StopCoroutine(c);
        }

        public static Coroutine ScaleTo(Transform t, Vector3 target, float duration, Ease ease = Ease.OutQuad, bool unscaled = true, Action onDone = null)
        {
            if (t == null) return null;
            if (duration <= 0f || TweenRunner.Instance == null) { t.localScale = target; onDone?.Invoke(); return null; }
            return Run(ScaleRoutine(t, target, duration, ease, unscaled, onDone));
        }

        static IEnumerator ScaleRoutine(Transform t, Vector3 target, float duration, Ease ease, bool unscaled, Action onDone)
        {
            Vector3 from = t.localScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (t == null) yield break;
                elapsed += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                t.localScale = Vector3.LerpUnclamped(from, target, Evaluate(ease, elapsed / duration));
                yield return null;
            }
            if (t != null) t.localScale = target;
            onDone?.Invoke();
        }

        /// Generic float tween; 'apply' receives the eased value every frame (and the final value once).
        public static Coroutine FloatTo(float from, float to, float duration, Action<float> apply, Ease ease = Ease.OutQuad, bool unscaled = true, Action onDone = null)
        {
            if (apply == null) return null;
            if (duration <= 0f || TweenRunner.Instance == null) { apply(to); onDone?.Invoke(); return null; }
            return Run(FloatRoutine(from, to, duration, apply, ease, unscaled, onDone));
        }

        static IEnumerator FloatRoutine(float from, float to, float duration, Action<float> apply, Ease ease, bool unscaled, Action onDone)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                apply(Mathf.LerpUnclamped(from, to, Evaluate(ease, elapsed / duration)));
                yield return null;
            }
            apply(to);
            onDone?.Invoke();
        }

        public static Coroutine MoveLocal(Transform t, Vector3 target, float duration, Ease ease = Ease.OutCubic, bool unscaled = true, Action onDone = null)
        {
            if (t == null) return null;
            if (duration <= 0f || TweenRunner.Instance == null) { t.localPosition = target; onDone?.Invoke(); return null; }
            return Run(MoveLocalRoutine(t, target, duration, ease, unscaled, onDone));
        }

        static IEnumerator MoveLocalRoutine(Transform t, Vector3 target, float duration, Ease ease, bool unscaled, Action onDone)
        {
            Vector3 from = t.localPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (t == null) yield break;
                elapsed += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                t.localPosition = Vector3.LerpUnclamped(from, target, Evaluate(ease, elapsed / duration));
                yield return null;
            }
            if (t != null) t.localPosition = target;
            onDone?.Invoke();
        }

        public static Coroutine PunchScale(Transform t, float punch = 0.12f, float duration = 0.25f, bool unscaled = true)
        {
            if (t == null) return null;
            if (TweenRunner.Instance == null) return null;
            return Run(PunchRoutine(t, punch, duration, unscaled));
        }

        static IEnumerator PunchRoutine(Transform t, float punch, float duration, bool unscaled)
        {
            Vector3 baseScale = t.localScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (t == null) yield break;
                elapsed += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                float n = Mathf.Clamp01(elapsed / duration);
                float s = 1f + punch * Mathf.Sin(n * Mathf.PI) * (1f - n * 0.5f);
                t.localScale = baseScale * s;
                yield return null;
            }
            if (t != null) t.localScale = baseScale;
        }

        public static Coroutine FadeCanvasGroup(CanvasGroup cg, float to, float duration, bool unscaled = true, Action onDone = null)
        {
            if (cg == null) return null;
            if (duration <= 0f || TweenRunner.Instance == null) { cg.alpha = to; onDone?.Invoke(); return null; }
            return Run(FadeRoutine(cg, to, duration, unscaled, onDone));
        }

        static IEnumerator FadeRoutine(CanvasGroup cg, float to, float duration, bool unscaled, Action onDone)
        {
            float from = cg.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (cg == null) yield break;
                elapsed += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                cg.alpha = Mathf.Lerp(from, to, Evaluate(Ease.OutQuad, elapsed / duration));
                yield return null;
            }
            if (cg != null) cg.alpha = to;
            onDone?.Invoke();
        }

        public static Coroutine ShakeLocal(Transform t, float amplitude = 12f, float duration = 0.3f, bool unscaled = true)
        {
            if (t == null || TweenRunner.Instance == null) return null;
            return Run(ShakeRoutine(t, amplitude, duration, unscaled));
        }

        static IEnumerator ShakeRoutine(Transform t, float amplitude, float duration, bool unscaled)
        {
            Vector3 basePos = t.localPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (t == null) yield break;
                elapsed += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                float n = 1f - Mathf.Clamp01(elapsed / duration);
                float x = Mathf.Sin(elapsed * 60f) * amplitude * n;
                t.localPosition = basePos + new Vector3(x, 0f, 0f);
                yield return null;
            }
            if (t != null) t.localPosition = basePos;
        }

        public static Coroutine CountTo(TMP_Text text, int from, int to, float duration, string format = "{0:N0}", bool unscaled = true)
        {
            if (text == null) return null;
            if (duration <= 0f || TweenRunner.Instance == null) { text.SetText(string.Format(NumberCulture, format, to)); return null; }
            return Run(CountRoutine(text, from, to, duration, format, unscaled));
        }

        static IEnumerator CountRoutine(TMP_Text text, int from, int to, float duration, string format, bool unscaled)
        {
            float elapsed = 0f;
            int last = int.MinValue;
            while (elapsed < duration)
            {
                if (text == null) yield break;
                elapsed += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                int v = Mathf.RoundToInt(Mathf.Lerp(from, to, Evaluate(Ease.OutCubic, elapsed / duration)));
                if (v != last) { last = v; text.SetText(string.Format(NumberCulture, format, v)); }
                yield return null;
            }
            if (text != null) text.SetText(string.Format(NumberCulture, format, to));
        }

        public static Coroutine MoveAnchored(RectTransform rt, Vector2 to, float duration, Ease ease = Ease.OutCubic, bool unscaled = true, Action onDone = null)
        {
            if (rt == null) return null;
            if (duration <= 0f || TweenRunner.Instance == null) { rt.anchoredPosition = to; onDone?.Invoke(); return null; }
            return Run(MoveAnchoredRoutine(rt, to, duration, ease, unscaled, onDone));
        }

        static IEnumerator MoveAnchoredRoutine(RectTransform rt, Vector2 to, float duration, Ease ease, bool unscaled, Action onDone)
        {
            Vector2 from = rt.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (rt == null) yield break;
                elapsed += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                rt.anchoredPosition = Vector2.LerpUnclamped(from, to, Evaluate(ease, elapsed / duration));
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = to;
            onDone?.Invoke();
        }

        public static Coroutine Delay(float seconds, Action callback, bool unscaled = true)
        {
            if (callback == null) return null;
            if (seconds <= 0f || TweenRunner.Instance == null) { callback(); return null; }
            return Run(DelayRoutine(seconds, callback, unscaled));
        }

        static IEnumerator DelayRoutine(float seconds, Action callback, bool unscaled)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
            }
            callback();
        }

        public static Coroutine FillTo(UnityEngine.UI.Image image, float to, float duration, bool unscaled = true)
        {
            if (image == null) return null;
            if (duration <= 0f || TweenRunner.Instance == null) { image.fillAmount = to; return null; }
            return Run(FillRoutine(image, to, duration, unscaled));
        }

        static IEnumerator FillRoutine(UnityEngine.UI.Image image, float to, float duration, bool unscaled)
        {
            float from = image.fillAmount;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (image == null) yield break;
                elapsed += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                image.fillAmount = Mathf.Lerp(from, to, Evaluate(Ease.OutQuad, elapsed / duration));
                yield return null;
            }
            if (image != null) image.fillAmount = to;
        }
    }
}
