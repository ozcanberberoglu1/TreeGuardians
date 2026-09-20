using UnityEngine;

namespace TreeGuardians.Core
{
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        [Tooltip("Ekranın üst çentiğini dikkate al.")] public bool applyTop = true;
        [Tooltip("Ekranın alt home çubuğunu dikkate al.")] public bool applyBottom = true;
        public bool applyLeft = true;
        public bool applyRight = true;
        [Tooltip("Editor'de de Screen.safeArea uygulansın (normalde yalnız cihazda).")] public bool applyInEditor;

        RectTransform rt;
        Rect lastSafeArea = new Rect(-1, -1, -1, -1);
        int lastW, lastH;

        void OnEnable()
        {
            rt = GetComponent<RectTransform>();
            Apply();
        }

        void Update()
        {
            if (Screen.width != lastW || Screen.height != lastH || Screen.safeArea != lastSafeArea)
                Apply();
        }

        public void Apply()
        {
            if (rt == null) rt = GetComponent<RectTransform>();
            var safe = Screen.safeArea;
            if (Application.isEditor && !applyInEditor) safe = new Rect(0f, 0f, Screen.width, Screen.height);
            lastSafeArea = Screen.safeArea;
            lastW = Screen.width;
            lastH = Screen.height;
            if (Screen.width <= 0 || Screen.height <= 0) return;

            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            if (!applyLeft) min.x = 0f;
            if (!applyBottom) min.y = 0f;
            if (!applyRight) max.x = 1f;
            if (!applyTop) max.y = 1f;

            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
