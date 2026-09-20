using TMPro;
using TreeGuardians.Core;
using TreeGuardians.Localization;
using TreeGuardians.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace TreeGuardians.Editor
{
    /// Editor-time factory for pre-authored UI and 2D scene objects. Everything it creates is saved into the scene.
    public static class UIFactory
    {
        public const string FontPath = "Assets/TreeGuardians/Settings/TG_Main SDF.asset";

        public static readonly Color Outline = new Color(0.11f, 0.13f, 0.18f);
        public static readonly Color PanelDark = new Color(0.13f, 0.17f, 0.24f, 0.96f);
        public static readonly Color PanelMid = new Color(0.2f, 0.26f, 0.35f, 1f);
        public static readonly Color PanelLight = new Color(0.93f, 0.9f, 0.82f, 1f);
        public static readonly Color Green = new Color(0.36f, 0.72f, 0.36f);
        public static readonly Color GreenDark = new Color(0.22f, 0.5f, 0.26f);
        public static readonly Color Honey = new Color(0.98f, 0.74f, 0.22f);
        public static readonly Color HoneyDark = new Color(0.85f, 0.55f, 0.12f);
        public static readonly Color Blue = new Color(0.3f, 0.6f, 0.92f);
        public static readonly Color Purple = new Color(0.6f, 0.4f, 0.85f);
        public static readonly Color Red = new Color(0.88f, 0.3f, 0.28f);
        public static readonly Color TextLight = new Color(0.97f, 0.96f, 0.92f);
        public static readonly Color TextDark = new Color(0.14f, 0.16f, 0.2f);
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.6f);

        static TMP_FontAsset font;

        public static TMP_FontAsset Font
        {
            get
            {
                if (font == null) font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                if (font == null) font = TMP_Settings.defaultFontAsset;
                return font;
            }
        }

        public static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
        public static Sprite Ui(string name) => Sprite(ArtPaths.Ui(name));
        public static Sprite Icon(string name) => Sprite(ArtPaths.Icon(name));

        // ------------------------------------------------------------------ serialized field helper
        public static void Set(Component c, string field, Object value)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[UIFactory] {c.GetType().Name} has no field '{field}'."); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Set(Component c, string field, float value)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[UIFactory] {c.GetType().Name} has no field '{field}'."); return; }
            if (p.propertyType == SerializedPropertyType.Integer) p.intValue = Mathf.RoundToInt(value);
            else if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value != 0f;
            else if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = Mathf.RoundToInt(value);
            else p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Set(Component c, string field, bool value) => Set(c, field, value ? 1f : 0f);

        public static void Set(Component c, string field, string value)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[UIFactory] {c.GetType().Name} has no field '{field}'."); return; }
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetArray(Component c, string field, Object[] values)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[UIFactory] {c.GetType().Name} has no field '{field}'."); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetColor(Component c, string field, Color value)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[UIFactory] {c.GetType().Name} has no field '{field}'."); return; }
            p.colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ scene basics
        public static GameObject Root(string name)
        {
            var go = new GameObject(name);
            return go;
        }

        public static GameObject Child(string name, Transform parent, Vector3? localPos = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (localPos.HasValue) go.transform.localPosition = localPos.Value;
            return go;
        }

        public static Camera Camera2D(string name, float orthoSize, Color background, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderType = CameraRenderType.Base;
            go.AddComponent<AudioListener>();
            return cam;
        }

        public static EventSystem EventSystem()
        {
            var go = new GameObject("EventSystem");
            var es = go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            return es;
        }

        public static Canvas Canvas(string name, int sortingOrder = 0, Transform parent = null, bool pixelPerfect = false)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = pixelPerfect;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2340f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform SafeArea(Transform canvas)
        {
            var rt = Rect("SafeArea", canvas);
            Stretch(rt);
            rt.gameObject.AddComponent<SafeAreaFitter>();
            return rt;
        }

        // ------------------------------------------------------------------ rect helpers
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(100f, 100f);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform AnchorStretchX(RectTransform rt, float y, float height, float left = 0f, float right = 0f, float pivotY = 0.5f, float anchorY = 0.5f)
        {
            rt.anchorMin = new Vector2(0f, anchorY);
            rt.anchorMax = new Vector2(1f, anchorY);
            rt.pivot = new Vector2(0.5f, pivotY);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(-(left + right), height);
            return rt;
        }

        public static RectTransform AnchorStretchY(RectTransform rt, float x, float width, float bottom = 0f, float top = 0f, float pivotX = 0.5f, float anchorX = 0.5f)
        {
            rt.anchorMin = new Vector2(anchorX, 0f);
            rt.anchorMax = new Vector2(anchorX, 1f);
            rt.pivot = new Vector2(pivotX, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(width, -(bottom + top));
            return rt;
        }

        // ------------------------------------------------------------------ widgets
        public static Image Image(string name, Transform parent, Sprite sprite, Color color, bool sliced = true, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = sliced && sprite != null && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.raycastTarget = raycast;
            img.preserveAspect = !sliced;
            return img;
        }

        public static Image Panel(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var img = Image(name, parent, sprite != null ? sprite : Ui("panel"), color, true, true);
            return img;
        }

        public static Image FillBar(string name, Transform parent, Color back, Color fill, out Image fillImage)
        {
            var bg = Image(name, parent, Ui("bar_back"), back, true, false);
            fillImage = Image("Fill", bg.transform, Ui("bar_fill"), fill, true, false);
            Stretch((RectTransform)fillImage.transform, 6f, 6f, 6f, 6f);
            fillImage.type = UnityEngine.UI.Image.Type.Filled;
            fillImage.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillAmount = 0.5f;
            return bg;
        }

        public static TMP_Text Text(string name, Transform parent, string localizationKey, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false, string literal = null)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.raycastTarget = false;
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.text = literal ?? localizationKey;
            if (!string.IsNullOrEmpty(localizationKey))
            {
                var loc = rt.gameObject.AddComponent<LocalizedTMPText>();
                Set(loc, "key", localizationKey);
            }
            return t;
        }

        public static TMP_Text OutlinedText(string name, Transform parent, string key, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, string literal = null)
        {
            var t = Text(name, parent, key, size, color, align, true, literal);
            t.outlineWidth = 0.2f;
            t.outlineColor = Outline;
            return t;
        }

        public static Button Button(string name, Transform parent, Color color, string labelKey, float fontSize = 40f, Sprite sprite = null, Color? labelColor = null, string literal = null)
        {
            var img = Image(name, parent, sprite != null ? sprite : Ui("button"), color, true, true);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.05f, 1.05f, 1.05f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.8f);
            colors.selectedColor = Color.white;
            btn.colors = colors;
            img.gameObject.AddComponent<UIButtonFeedback>();
            if (!string.IsNullOrEmpty(labelKey) || literal != null)
            {
                var label = OutlinedText("Label", img.transform, labelKey, fontSize, labelColor ?? TextLight, TextAlignmentOptions.Center, literal);
                Stretch((RectTransform)label.transform, 12f, 6f, 12f, 6f);
            }
            return btn;
        }

        public static Button IconButton(string name, Transform parent, Color color, Sprite icon, Color iconColor, string labelKey = null, float labelSize = 26f)
        {
            var btn = Button(name, parent, color, null);
            var ic = Image("Icon", btn.transform, icon, iconColor, false, false);
            if (labelKey != null)
            {
                Anchor((RectTransform)ic.transform, new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(70f, 70f));
                var label = OutlinedText("Label", btn.transform, labelKey, labelSize, TextLight);
                AnchorStretchX((RectTransform)label.transform, 8f, 34f, 4f, 4f, 0f, 0f);
            }
            else
            {
                Stretch((RectTransform)ic.transform, 18f, 18f, 18f, 18f);
            }
            return btn;
        }

        public static CanvasGroup Group(GameObject go, float alpha = 1f)
        {
            var g = go.GetComponent<CanvasGroup>();
            if (g == null) g = go.AddComponent<CanvasGroup>();
            g.alpha = alpha;
            return g;
        }

        public static RectTransform HorizontalGroup(string name, Transform parent, float spacing, TextAnchor align = TextAnchor.MiddleCenter, RectOffset padding = null, bool expandChildren = false)
        {
            var rt = Rect(name, parent);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = align;
            h.childControlWidth = false;
            h.childControlHeight = false;
            h.childForceExpandWidth = expandChildren;
            h.childForceExpandHeight = false;
            if (padding != null) h.padding = padding;
            return rt;
        }

        public static RectTransform VerticalGroup(string name, Transform parent, float spacing, TextAnchor align = TextAnchor.UpperCenter, RectOffset padding = null)
        {
            var rt = Rect(name, parent);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = align;
            v.childControlWidth = false;
            v.childControlHeight = false;
            v.childForceExpandWidth = false;
            v.childForceExpandHeight = false;
            if (padding != null) v.padding = padding;
            return rt;
        }

        public static RectTransform GridGroup(string name, Transform parent, Vector2 cell, Vector2 spacing, int columns, RectOffset padding = null)
        {
            var rt = Rect(name, parent);
            var g = rt.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = cell;
            g.spacing = spacing;
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = columns;
            g.childAlignment = TextAnchor.UpperLeft;
            if (padding != null) g.padding = padding;
            var fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rt;
        }

        public static ScrollRect ScrollView(string name, Transform parent, out RectTransform content, bool horizontal = false)
        {
            var viewport = Image(name, parent, Ui("soft"), new Color(0f, 0f, 0f, 0.15f), true, true);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            content = Rect("Content", viewport.transform);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = horizontal ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(horizontal ? 1000f : 0f, 600f);
            scroll.content = content;
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.horizontal = horizontal;
            scroll.vertical = !horizontal;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            return scroll;
        }

        public static Slider Slider(string name, Transform parent, Color fill)
        {
            var rt = Rect(name, parent);
            rt.sizeDelta = new Vector2(500f, 44f);
            var slider = rt.gameObject.AddComponent<Slider>();
            var bg = Image("Background", rt, Ui("bar_back"), PanelMid, true, false);
            Stretch((RectTransform)bg.transform);
            var fillArea = Rect("Fill Area", rt);
            Stretch(fillArea, 8f, 8f, 8f, 8f);
            var fillImg = Image("Fill", fillArea, Ui("bar_fill"), fill, true, false);
            Stretch((RectTransform)fillImg.transform);
            var handleArea = Rect("Handle Slide Area", rt);
            Stretch(handleArea, 16f, 0f, 16f, 0f);
            var handle = Image("Handle", handleArea, Ui("circle"), TextLight, false, true);
            ((RectTransform)handle.transform).sizeDelta = new Vector2(52f, 52f);
            slider.fillRect = (RectTransform)fillImg.transform;
            slider.handleRect = (RectTransform)handle.transform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.8f;
            return slider;
        }

        public static Toggle Toggle(string name, Transform parent, string labelKey)
        {
            var rt = Rect(name, parent);
            rt.sizeDelta = new Vector2(560f, 64f);
            var toggle = rt.gameObject.AddComponent<Toggle>();
            var box = Image("Background", rt, Ui("panel"), PanelMid, true, true);
            Anchor((RectTransform)box.transform, new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(120f, 56f), new Vector2(1f, 0.5f));
            var check = Image("Checkmark", box.transform, Ui("circle"), Honey, false, false);
            Anchor((RectTransform)check.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
            var label = Text("Label", rt, labelKey, 32f, TextLight, TextAlignmentOptions.MidlineLeft);
            AnchorStretchY((RectTransform)label.transform, 0f, 400f, 0f, 0f, 0f, 0f);
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = true;
            rt.gameObject.AddComponent<UIButtonFeedback>();
            return toggle;
        }

        // ------------------------------------------------------------------ world sprites
        public static SpriteRenderer WorldSprite(string name, Transform parent, Sprite sprite, Color color, int order, Vector3 localPos, Vector2? size = null, string sortingLayer = "Default")
        {
            var go = Child(name, parent, localPos);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            sr.sortingLayerName = sortingLayer;
            if (size.HasValue && sprite != null)
            {
                var b = sprite.bounds.size;
                go.transform.localScale = new Vector3(size.Value.x / Mathf.Max(0.001f, b.x), size.Value.y / Mathf.Max(0.001f, b.y), 1f);
            }
            return sr;
        }

        public static SpriteRenderer WorldSpriteScaled(string name, Transform parent, Sprite sprite, Color color, int order, Vector3 localPos, float scale, string sortingLayer = "Default")
        {
            var sr = WorldSprite(name, parent, sprite, color, order, localPos, null, sortingLayer);
            sr.transform.localScale = Vector3.one * scale;
            return sr;
        }
    }
}
