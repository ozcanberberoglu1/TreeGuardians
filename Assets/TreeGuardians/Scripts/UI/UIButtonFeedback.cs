using TreeGuardians.Audio;
using TreeGuardians.Core;
using TreeGuardians.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TreeGuardians.UI
{
    /// Pressed scale (0.96), click sound and light haptic for any Button. Also exposes a selected state tint.
    [RequireComponent(typeof(RectTransform))]
    public sealed class UIButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] float pressedScale = 0.96f;
        [SerializeField] float duration = 0.08f;
        [SerializeField] AudioEventId clickSound = AudioEventId.UiClick;
        [SerializeField] bool haptic = true;
        [SerializeField] Graphic selectedGraphic;
        [SerializeField] Color selectedColor = new Color(1f, 0.85f, 0.35f);
        [Tooltip("Seçiliyken rengi değişen etiket (boşsa selectedGraphic altındaki ilk metin). Sarı zeminde koyu yazı okunur.")]
        [SerializeField] TMPro.TMP_Text selectedLabel;
        [SerializeField] Color selectedLabelColor = new Color(0.29f, 0.17f, 0.06f, 1f);
        [SerializeField] Sprite selectedSprite;
        Color selectedLabelBaseColor;
        Image selectedImage;
        Sprite baseSprite;

        Button button;
        Vector3 baseScale;
        bool pressed;
        Color selectedGraphicBaseColor;
        bool selected;

        void Awake()
        {
            button = GetComponent<Button>();
            baseScale = transform.localScale;
            InitSelection();
        }

        bool selectionInited;

        /// SetSelected can run before Awake (inactive panels), so the base colours are captured lazily.
        void InitSelection()
        {
            if (selectionInited) return;
            selectionInited = true;
            if (selectedGraphic != null) selectedGraphicBaseColor = selectedGraphic.color;
            if (selectedLabel == null && selectedGraphic != null) selectedLabel = GetComponentInChildren<TMPro.TMP_Text>(true);
            if (selectedLabel != null) selectedLabelBaseColor = selectedLabel.color;
            selectedImage = selectedGraphic as Image;
            if (selectedImage != null) baseSprite = selectedImage.sprite;
        }

        void OnDisable()
        {
            pressed = false;
            transform.localScale = baseScale;
        }

        bool Interactable => button == null || button.interactable;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Interactable) return;
            pressed = true;
            TGTween.ScaleTo(transform, baseScale * pressedScale, duration, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!pressed) return;
            pressed = false;
            TGTween.ScaleTo(transform, baseScale, duration, Ease.OutBack);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!pressed) return;
            pressed = false;
            TGTween.ScaleTo(transform, baseScale, duration, Ease.OutQuad);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Interactable) return;
            if (clickSound != AudioEventId.None) Services.Get<AudioService>()?.PlayUi(clickSound);
            if (haptic) Services.Get<HapticService>()?.Light();
        }

        public void SetSelected(bool value)
        {
            InitSelection();
            selected = value;
            if (selectedGraphic != null) selectedGraphic.color = value ? selectedColor : selectedGraphicBaseColor;
            if (selectedLabel != null) selectedLabel.color = value ? selectedLabelColor : selectedLabelBaseColor;
            if (selectedImage != null && selectedSprite != null) selectedImage.sprite = value ? selectedSprite : baseSprite;
        }

        public bool IsSelected => selected;

        /// Horizontal shake for invalid actions.
        public void ShakeInvalid()
        {
            TGTween.ShakeLocal(transform, 10f, 0.25f);
            Services.Get<AudioService>()?.PlayUi(AudioEventId.UiError);
        }
    }
}
