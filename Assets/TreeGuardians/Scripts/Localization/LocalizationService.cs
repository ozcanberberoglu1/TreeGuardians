using System;
using System.Collections.Generic;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Save;
using UnityEngine;

namespace TreeGuardians.Localization
{
    public sealed class LocalizationService : MonoBehaviour
    {
        public Language Current { get; private set; } = Language.English;

        readonly Dictionary<string, string> en = new Dictionary<string, string>(512);
        readonly Dictionary<string, string> tr = new Dictionary<string, string>(512);
        Dictionary<string, string> active;
        SettingsSaveData settings;
        SaveService save;
        LocalizationTable table;

        public event Action OnLanguageChanged;

        public IReadOnlyList<string> LoadingTipKeys => table != null ? table.loadingTipKeys : null;

        void Awake()
        {
            Services.Register(this);
            active = en;
        }

        void OnDestroy()
        {
            Services.Unregister(this);
        }

        public void Initialize(LocalizationTable table, SettingsSaveData settings, SaveService save)
        {
            this.table = table;
            this.settings = settings;
            this.save = save;
            if (table != null) table.BuildInto(en, tr);
            else TGLog.Warn("LocalizationService: no table assigned; keys will be shown.");
            var lang = settings != null && settings.language >= 0 ? (Language)settings.language : DetectSystemLanguage();
            SetLanguage(lang, false);
        }

        public void ReloadTable(LocalizationTable newTable)
        {
            table = newTable;
            if (table != null) table.BuildInto(en, tr);
            OnLanguageChanged?.Invoke();
        }

        static Language DetectSystemLanguage() =>
            Application.systemLanguage == SystemLanguage.Turkish ? Language.Turkish : Language.English;

        public void SetLanguage(Language lang, bool persist = true)
        {
            Current = lang;
            active = lang == Language.Turkish ? tr : en;
            TGTween.NumberCulture = Culture;
            if (persist && settings != null)
            {
                settings.language = (int)lang;
                save?.SaveNow();
            }
            OnLanguageChanged?.Invoke();
            GameEventBus.Publish(new LanguageChangedEvent { language = lang });
        }

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (active != null && active.TryGetValue(key, out var v)) return v;
            return "[" + key + "]";
        }

        public bool Has(string key) => !string.IsNullOrEmpty(key) && active != null && active.ContainsKey(key);

        public string Format(string key, object arg0) => string.Format(Get(key), arg0);
        public string Format(string key, object arg0, object arg1) => string.Format(Get(key), arg0, arg1);
        public string Format(string key, object arg0, object arg1, object arg2) => string.Format(Get(key), arg0, arg1, arg2);

        public static string Tr(string key)
        {
            var s = Services.Get<LocalizationService>();
            return s != null ? s.Get(key) : "[" + key + "]";
        }

        /// Culture-aware number format for the active language ("1,234" EN / "1.234" TR).
        public System.Globalization.CultureInfo Culture => Current == Language.Turkish ? TurkishCulture : System.Globalization.CultureInfo.InvariantCulture;
        static readonly System.Globalization.CultureInfo TurkishCulture = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");

        public static string Tr(string key, object arg0)
        {
            var s = Services.Get<LocalizationService>();
            return s != null ? string.Format(s.Culture, s.Get(key), arg0) : "[" + key + "]";
        }

        public static string Tr(string key, object arg0, object arg1)
        {
            var s = Services.Get<LocalizationService>();
            return s != null ? string.Format(s.Culture, s.Get(key), arg0, arg1) : "[" + key + "]";
        }

        /// Formats a count with the active language's thousands separator.
        public static string Number(long value)
        {
            var s = Services.Get<LocalizationService>();
            return value.ToString("N0", s != null ? s.Culture : System.Globalization.CultureInfo.InvariantCulture);
        }

        /// Localized short duration: "1h 05m" / "1sa 05dk", "4m 09s" / "4dk 09sn", "12s" / "12sn".
        public static string Duration(double seconds)
        {
            if (seconds < 0) seconds = 0;
            var ts = System.TimeSpan.FromSeconds(System.Math.Ceiling(seconds));
            if (ts.TotalHours >= 1) return Tr("time_hm", (int)ts.TotalHours, ts.Minutes.ToString("00"));
            if (ts.TotalMinutes >= 1) return Tr("time_ms", ts.Minutes, ts.Seconds.ToString("00"));
            return Tr("time_s", ts.Seconds);
        }
    }
}
