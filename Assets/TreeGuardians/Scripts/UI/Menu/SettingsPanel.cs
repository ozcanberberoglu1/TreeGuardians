using TMPro;
using TreeGuardians.Audio;
using TreeGuardians.Core;
using TreeGuardians.Data;
using TreeGuardians.Localization;
using TreeGuardians.Meta;
using TreeGuardians.Save;
using UnityEngine;
using UnityEngine.UI;

namespace TreeGuardians.UI.Menu
{
    /// Language, music/SFX, vibration, reduce motion/haptics, quality, aim mode, tutorial replay and save reset.
    public sealed class SettingsPanel : UIPanel
    {
        [SerializeField] Button englishButton;
        [SerializeField] Button turkishButton;
        [SerializeField] Slider musicSlider;
        [SerializeField] Slider sfxSlider;
        [SerializeField] Toggle vibrationToggle;
        [SerializeField] Toggle reduceHapticsToggle;
        [SerializeField] Toggle reduceMotionToggle;
        [SerializeField] Button qualityLowButton;
        [SerializeField] Button qualityMediumButton;
        [SerializeField] Button qualityHighButton;
        [SerializeField] Button aimPullButton;
        [SerializeField] Button aimDirectButton;
        [SerializeField] Button replayTutorialButton;
        [SerializeField] Button resetSaveButton;
        [SerializeField] Button closeButton;
        [SerializeField] TMP_Text versionText;

        bool syncing;

        protected override void Awake()
        {
            base.Awake();
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (englishButton != null) englishButton.onClick.AddListener(() => SetLanguage(Language.English));
            if (turkishButton != null) turkishButton.onClick.AddListener(() => SetLanguage(Language.Turkish));
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(v => { if (!syncing) { Settings.musicVolume = v; ApplyAudio(false); } });
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(v => { if (!syncing) { Settings.sfxVolume = v; ApplyAudio(false); } });
            if (vibrationToggle != null) vibrationToggle.onValueChanged.AddListener(v => { if (!syncing) { Settings.vibration = v; Persist(); } });
            if (reduceHapticsToggle != null) reduceHapticsToggle.onValueChanged.AddListener(v => { if (!syncing) { Settings.reduceHaptics = v; Persist(); } });
            if (reduceMotionToggle != null) reduceMotionToggle.onValueChanged.AddListener(v => { if (!syncing) { Settings.reduceMotion = v; ApplyQuality(); } });
            if (qualityLowButton != null) qualityLowButton.onClick.AddListener(() => { Settings.qualityTier = 0; ApplyQuality(); });
            if (qualityMediumButton != null) qualityMediumButton.onClick.AddListener(() => { Settings.qualityTier = 1; ApplyQuality(); });
            if (qualityHighButton != null) qualityHighButton.onClick.AddListener(() => { Settings.qualityTier = 2; ApplyQuality(); });
            if (aimPullButton != null) aimPullButton.onClick.AddListener(() => { Settings.aimMode = (int)AimMode.PullBack; Persist(); Refresh(); });
            if (aimDirectButton != null) aimDirectButton.onClick.AddListener(() => { Settings.aimMode = (int)AimMode.DirectDrag; Persist(); Refresh(); });
            if (replayTutorialButton != null) replayTutorialButton.onClick.AddListener(OnReplayTutorial);
            if (resetSaveButton != null) resetSaveButton.onClick.AddListener(OnResetSave);
        }

        static SettingsSaveData Settings => Services.Get<PlayerProgressService>()?.Data.settings;

        protected override void OnOpen()
        {
            Refresh();
            var cfg = Services.Get<GameConfigProvider>();
            if (versionText != null && cfg != null && cfg.Config != null) versionText.text = LocalizationService.Tr("settings_version") + " " + cfg.Config.version;
        }

        void Refresh()
        {
            var s = Settings;
            if (s == null) return;
            syncing = true;
            var loc = Services.Get<LocalizationService>();
            Highlight(englishButton, loc != null && loc.Current == Language.English);
            Highlight(turkishButton, loc != null && loc.Current == Language.Turkish);
            if (musicSlider != null) musicSlider.value = s.musicVolume;
            if (sfxSlider != null) sfxSlider.value = s.sfxVolume;
            if (vibrationToggle != null) vibrationToggle.isOn = s.vibration;
            if (reduceHapticsToggle != null) reduceHapticsToggle.isOn = s.reduceHaptics;
            if (reduceMotionToggle != null) reduceMotionToggle.isOn = s.reduceMotion;
            Highlight(qualityLowButton, s.qualityTier == 0);
            Highlight(qualityMediumButton, s.qualityTier == 1);
            Highlight(qualityHighButton, s.qualityTier == 2);
            Highlight(aimPullButton, s.aimMode == (int)AimMode.PullBack);
            Highlight(aimDirectButton, s.aimMode == (int)AimMode.DirectDrag);
            syncing = false;
        }

        static void Highlight(Button b, bool on)
        {
            if (b == null) return;
            var fb = b.GetComponent<UIButtonFeedback>();
            if (fb != null) fb.SetSelected(on);
        }

        void SetLanguage(Language lang)
        {
            Services.Get<LocalizationService>()?.SetLanguage(lang);
            Refresh();
        }

        void ApplyAudio(bool persist)
        {
            Services.Get<AudioService>()?.ApplySettings();
            if (persist) Persist();
        }

        void ApplyQuality()
        {
            var cfg = Services.Get<GameConfigProvider>();
            var s = Settings;
            if (s != null) QualityApplier.Apply(s.qualityTier, s.reduceMotion, cfg != null ? cfg.Config : null);
            Persist();
            Refresh();
        }

        void Persist()
        {
            Services.Get<SaveService>()?.SaveNow();
            GameEventBus.Publish(new SettingsChangedEvent());
        }

        protected override void OnClose()
        {
            Persist();
        }

        void OnReplayTutorial()
        {
            var progress = Services.Get<PlayerProgressService>();
            if (progress == null) return;
            progress.Data.tutorial.battleTutorialDone = false;
            progress.Data.tutorial.menuTutorialDone = false;
            progress.Data.tutorial.skipped = false;
            progress.Data.tutorial.lastStep = 0;
            progress.Save();
            MenuUIController.Instance?.Toast("settings_replay_tutorial");
        }

        void OnResetSave()
        {
            MenuUIController.Instance?.ShowConfirm("reset_save_title", "reset_save_body", () =>
            {
                var save = Services.Get<SaveService>();
                if (save == null) return;
                save.ResetToNewSave();
                Services.Get<PlayerProgressService>()?.Bind(save.Data, true);
                Services.Get<Chests.ChestService>()?.Initialize(Services.Get<PlayerProgressService>(), save, true);
                Services.Get<Quests.QuestService>()?.Initialize(Services.Get<PlayerProgressService>(), save);
                save.SaveNow();
                Services.Get<SceneFlow.SceneFlowService>()?.GoToMainMenu();
            });
        }
    }
}
