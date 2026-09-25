using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Menu
{
    /// Sets a progress bar fill without the pointed ends that Image.Type.Filled gives rounded 9-sliced art.
    /// - A UIFill on the bar (or a parent) that drives this image: UIFill.SetValue.
    /// - Image.Type.Filled (legacy scene setup): fillAmount, unchanged behaviour.
    /// - Sliced / Simple / Tiled image stretched in its bar: the width follows the value through anchorMax.x, with a minimum
    ///   visible width so the rounded caps never invert; hidden at 0.
    public static class FillBar
    {
        public const float DefaultMinVisible = 0.06f;

        public static void Set(Image fill, float value, float minVisible = DefaultMinVisible)
        {
            if (fill == null) return;
            value = Mathf.Clamp01(value);
            var driver = fill.GetComponentInParent<UIFill>();
            if (driver != null && driver.FillImage == fill) { driver.SetValue(value, true); return; }
            if (fill.type == Image.Type.Filled) { fill.fillAmount = value; return; }

            bool visible = value > 0.001f;
            if (fill.enabled != visible) fill.enabled = visible;
            if (!visible) return;
            var rt = fill.rectTransform;
            // Keep the fill at least as wide as it is tall (plus its inset) so the rounded caps never overlap or invert.
            float minFrac = Mathf.Clamp01(minVisible);
            var bar = rt.parent as RectTransform;
            if (bar != null && bar.rect.width > 1f)
                minFrac = Mathf.Max(minFrac, Mathf.Clamp01((rt.rect.height - rt.sizeDelta.x) / bar.rect.width));
            var min = rt.anchorMin;
            var max = rt.anchorMax;
            min.x = 0f;
            max.x = Mathf.Lerp(minFrac, 1f, value);
            rt.anchorMin = min;
            rt.anchorMax = max;
        }
    }
}
