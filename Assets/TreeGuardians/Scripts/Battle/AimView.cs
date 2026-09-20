using System.Collections.Generic;
using TreeGuardians.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.Battle
{
    /// Pre-authored aim guide in the AimLayer: predicted dots (first part of the flight only), power gauge, reticle.
    public sealed class AimView : MonoBehaviour
    {
        [SerializeField] RectTransform layer;
        [SerializeField] Image[] dots = new Image[12];
        [SerializeField] RectTransform powerGauge;
        [SerializeField] Image powerFill;
        [SerializeField] Image reticle;
        [SerializeField] Color validColor = new Color(1f, 0.95f, 0.6f);
        [SerializeField] Color invalidColor = new Color(1f, 0.4f, 0.35f);
        [SerializeField] Color specialColor = new Color(0.75f, 0.5f, 1f);
        [SerializeField] float predictSeconds = 0.9f;

        readonly List<Vector2> points = new List<Vector2>(16);
        Camera worldCam;
        Canvas canvas;

        public void Initialize(Camera cam)
        {
            worldCam = cam;
            canvas = GetComponentInParent<Canvas>();
            Hide();
        }

        public void Show(ProjectileService svc, ProjectileDefinition def, Vector2 origin, Vector2 velocity, float power, bool valid, bool special)
        {
            if (svc == null || def == null || worldCam == null) { Hide(); return; }
            svc.PredictPath(def, origin, velocity, points, dots.Length, predictSeconds / Mathf.Max(1, dots.Length));
            var color = !valid ? invalidColor : special ? specialColor : validColor;
            for (int i = 0; i < dots.Length; i++)
            {
                var d = dots[i];
                if (d == null) continue;
                if (i < points.Count)
                {
                    d.enabled = true;
                    d.rectTransform.anchoredPosition = WorldToLayer(points[i]);
                    float a = 1f - i / (float)dots.Length * 0.7f;
                    d.color = new Color(color.r, color.g, color.b, a);
                    d.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, i / (float)dots.Length);
                }
                else d.enabled = false;
            }
            if (powerGauge != null)
            {
                powerGauge.gameObject.SetActive(true);
                powerGauge.anchoredPosition = WorldToLayer(origin) + new Vector2(0f, 90f);
            }
            if (powerFill != null) { powerFill.fillAmount = power; powerFill.color = color; }
            if (reticle != null)
            {
                reticle.enabled = points.Count > 0;
                if (points.Count > 0) reticle.rectTransform.anchoredPosition = WorldToLayer(points[points.Count - 1]);
                reticle.color = color;
            }
        }

        public void Hide()
        {
            for (int i = 0; i < dots.Length; i++) if (dots[i] != null) dots[i].enabled = false;
            if (powerGauge != null) powerGauge.gameObject.SetActive(false);
            if (reticle != null) reticle.enabled = false;
        }

        Vector2 WorldToLayer(Vector2 world)
        {
            if (layer == null || worldCam == null) return Vector2.zero;
            Vector2 screen = worldCam.WorldToScreenPoint(world);
            var uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, uiCam, out var local);
            return local;
        }
    }
}
