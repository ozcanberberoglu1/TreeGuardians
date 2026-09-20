using System.Globalization;
using TMPro;
using TreeGuardians.Core;
using TreeGuardians.Data;
using UnityEngine;

namespace TreeGuardians.Localization
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedTMPText : MonoBehaviour
    {
        static readonly CultureInfo TurkishCulture = new CultureInfo("tr-TR");

        [SerializeField] string key;
        [SerializeField] bool toUpper;
        [SerializeField] string prefix = "";
        [SerializeField] string suffix = "";

        TMP_Text text;
        LocalizationService loc;

        public string Key
        {
            get => key;
            set { key = value; Refresh(); }
        }

        void Awake()
        {
            text = GetComponent<TMP_Text>();
        }

        void OnEnable()
        {
            loc = Services.Get<LocalizationService>();
            if (loc != null) loc.OnLanguageChanged += Refresh;
            else BootAwaiter.WhenReady(OnBootReady);
            Refresh();
        }

        void OnDisable()
        {
            if (loc != null) loc.OnLanguageChanged -= Refresh;
        }

        void OnBootReady()
        {
            if (!isActiveAndEnabled) return;
            loc = Services.Get<LocalizationService>();
            if (loc != null) loc.OnLanguageChanged += Refresh;
            Refresh();
        }

        public void Refresh()
        {
            if (text == null) text = GetComponent<TMP_Text>();
            if (text == null) return;
            if (loc == null) loc = Services.Get<LocalizationService>();
            string v = loc != null ? loc.Get(key) : key;
            if (toUpper)
                v = loc != null && loc.Current == Language.Turkish ? v.ToUpper(TurkishCulture) : v.ToUpperInvariant();
            if (prefix.Length > 0 || suffix.Length > 0) v = prefix + v + suffix;
            text.text = v;
        }

        public void SetKey(string newKey, bool refresh = true)
        {
            key = newKey;
            if (refresh) Refresh();
        }
    }
}
