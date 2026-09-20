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
    }
}
