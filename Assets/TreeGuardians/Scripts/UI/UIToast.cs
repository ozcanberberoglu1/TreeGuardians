using System.Collections;
using TMPro;
using TreeGuardians.Core;
using TreeGuardians.Localization;
using UnityEngine;

namespace TreeGuardians.UI
{
    /// Single pre-authored toast; shows a localized message briefly.
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UIToast : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] float showSeconds = 1.6f;
        [SerializeField] float fadeSeconds = 0.15f;

        CanvasGroup group;
        Coroutine routine;

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }

        public void ShowKey(string key) => ShowText(LocalizationService.Tr(key));

        public void ShowText(string text)
        {
            if (label != null) label.text = text;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            float t = 0f;
            while (t < fadeSeconds) { t += Time.unscaledDeltaTime; group.alpha = t / fadeSeconds; yield return null; }
            group.alpha = 1f;
            t = 0f;
            while (t < showSeconds) { t += Time.unscaledDeltaTime; yield return null; }
            t = 0f;
            while (t < fadeSeconds) { t += Time.unscaledDeltaTime; group.alpha = 1f - t / fadeSeconds; yield return null; }
            group.alpha = 0f;
            routine = null;
        }
    }
}
