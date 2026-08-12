using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class SharedPreferencesFoundationSmokeTests
{
    private const string MasterVolumeKey = "hif_master_volume";
    private const string MusicVolumeKey = "hif_music_volume";
    private const string SfxVolumeKey = "hif_sfx_volume";
    private const string MuteAllKey = "hif_mute_all";
    private const string AmbientVolumeKey = "hif_ambient_volume";
    private const string MusicDuringPauseKey = "hif_music_during_pause";
    private const string ScreenModeKey = "hif_screen_mode";
    private const string ResolutionKey = "hif_resolution";
    private const string RefreshRateKey = "hif_refresh_rate";
    private const string GameLookKey = "hif_game_look";
    private const string InterfaceStyleKey = "hif_interface_style";
    private const string RewindVhsFilterKey = "hif_rewind_vhs_filter";
    private const string RunInBackgroundKey = "hif_run_in_background";
    private const string CharacterAnimationsKey = "hif_character_animations";
    private const string BackgroundAnimationsKey = "hif_background_animations";
    private const string LanguageKey = "hif_language";
    private const string FontSizeModeKey = "hif_font_size_mode";
    private const string SkipModeKey = "hif_skip_mode";
    private const string SkipBehaviorKey = "hif_skip_behavior";
    private const string TextSpeedKey = "hif_text_speed";
    private const string DialogueTextScaleKey = "hif_dialogue_text_scale";
    private const string TextboxOpacityKey = "hif_textbox_opacity";
    private const string AutoForwardDelayKey = "hif_auto_forward_delay";
    private const string SkipAfterChoicesKey = "hif_skip_after_choices";
    private const string AutoForwardKey = "hif_auto_forward";
    private const string AutoSaveKey = "hif_auto_save";
    private const string ShowHintsKey = "hif_show_hints";
    private const string FullscreenKey = "hif_fullscreen";

    private static readonly string[] TestKeys =
    {
        MasterVolumeKey,
        MusicVolumeKey,
        SfxVolumeKey,
        MuteAllKey,
        AmbientVolumeKey,
        MusicDuringPauseKey,
        ScreenModeKey,
        ResolutionKey,
        RefreshRateKey,
        GameLookKey,
        InterfaceStyleKey,
        RewindVhsFilterKey,
        RunInBackgroundKey,
        CharacterAnimationsKey,
        BackgroundAnimationsKey,
        LanguageKey,
        FontSizeModeKey,
        SkipModeKey,
        SkipBehaviorKey,
        TextSpeedKey,
        DialogueTextScaleKey,
        TextboxOpacityKey,
        AutoForwardDelayKey,
        SkipAfterChoicesKey,
        AutoForwardKey,
        AutoSaveKey,
        ShowHintsKey,
        FullscreenKey
    };

    [MenuItem("How I Fall/Tests/Run Shared Preferences Foundation Smoke Tests")]
    public static void RunFromMenu()
    {
        RunBatchMode();
        Debug.Log("How I Fall shared Preferences foundation smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        VerifyContractIsTypedAndTruthful();
        VerifyBothContextsShareLiveStateAndReset();
        VerifyPersistenceRoundtripAndCanonicalDisplayMode();
        VerifySaveIsolation();
    }

    private static void VerifyContractIsTypedAndTruthful()
    {
        Type contract = typeof(IPreferencesService);
        Require(contract.IsInterface, "Shared Preferences service must be a typed interface.");
        Require(typeof(PreferencesService).GetInterfaces().Contains(contract), "PreferencesService must implement the shared contract.");
        Require(typeof(SettingsPanelController).GetInterfaces().Contains(typeof(IPreferencesView)), "Main Menu must use the shared Preferences view contract.");
        Require(typeof(VNPreferencesAdapter).GetInterfaces().Contains(typeof(IPreferencesView)), "Gameplay must use the shared Preferences view contract.");

        string[] prohibitedNames =
        {
            "RefreshRate", "GameLook", "InterfaceStyle", "RewindVhsFilter",
            "CharacterAnimations", "BackgroundAnimations", "Language", "FontSizeMode", "ShowHints"
        };
        string[] memberNames = contract.GetMembers().Select(member => member.Name).ToArray();
        string[] stateNames = typeof(PreferencesState).GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Select(field => field.Name)
            .ToArray();
        foreach (string name in prohibitedNames)
        {
            Require(!memberNames.Any(memberName => memberName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0),
                $"Fake/unused field '{name}' must not be exposed through IPreferencesService.");
            Require(!stateNames.Any(memberName => memberName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0),
                $"Fake/unused field '{name}' must not be exposed through PreferencesState.");
        }

        Require(!memberNames.Any(memberName => memberName.IndexOf("ShowQuickMenu", StringComparison.OrdinalIgnoreCase) >= 0)
            && !stateNames.Any(memberName => memberName.IndexOf("showQuickMenu", StringComparison.OrdinalIgnoreCase) >= 0),
            "Paused B03 Quick Menu visibility must remain outside the Phase 1 shared contract.");
        Require(!memberNames.Any(memberName => memberName.IndexOf("AmbientVolume", StringComparison.OrdinalIgnoreCase) >= 0
                || memberName.IndexOf("MusicDuringPause", StringComparison.OrdinalIgnoreCase) >= 0)
            && !stateNames.Any(memberName => memberName.IndexOf("ambientVolume", StringComparison.OrdinalIgnoreCase) >= 0
                || memberName.IndexOf("musicDuringPause", StringComparison.OrdinalIgnoreCase) >= 0),
            "Partial audio fields must remain persisted compatibility data, not approved player-facing Preferences.");

        Require(contract.GetMethod("SetMasterVolume") != null, "Approved Master Volume must be exposed.");
        Require(contract.GetMethod("SetMuteAll") != null, "Approved Mute All must be exposed.");
        Require(contract.GetMethod("SetScreenMode") != null, "Approved Screen Mode must be exposed.");
        Require(contract.GetMethod("SetResolution") != null, "Approved Resolution must be exposed.");
        Require(contract.GetMethod("SetRunInBackground") != null, "Approved Run in Background must be exposed.");
        Require(contract.GetMethod("SetSkipMode") != null, "Approved Skip Mode must be exposed.");
        Require(contract.GetMethod("SetSkipBehavior") != null, "Approved Skip Speed must be exposed.");
        Require(contract.GetMethod("SetTextSpeed") != null, "Approved Text Speed must be exposed.");
        Require(contract.GetMethod("SetDialogueTextScale") != null, "Approved dialogue Text Size must be exposed.");
        Require(contract.GetMethod("SetTextboxOpacity") != null, "Approved Textbox Opacity must be exposed.");
        Require(contract.GetMethod("SetSkipAfterChoices") != null, "Approved Skip After Choices must be exposed.");
        Require(contract.GetMethod("SetAutoForward") != null, "Approved Auto Forward must be exposed.");
        Require(contract.GetMethod("SetAutoSave") != null, "Approved Autosave must be exposed.");
        Require(contract.GetMethod("SetFullscreen") == null, "Legacy fullscreen must not be a second shared-service truth.");
    }

    private static void VerifyBothContextsShareLiveStateAndReset()
    {
        SettingsManager manager = GetSettingsManager();
        GameSettings previousSettings = manager.settings;
        var mainView = new RecordingView();
        var gameplayView = new RecordingView();
        var mainController = new PreferencesController(new PreferencesService(manager), mainView);
        var gameplayController = new PreferencesController(new PreferencesService(manager), gameplayView);

        try
        {
            manager.settings = new GameSettings();
            mainController.Initialize();
            gameplayController.Initialize();
            mainController.Open();
            gameplayController.Open();

            Require(!typeof(PreferencesService).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(field => field.FieldType == typeof(GameSettings) || field.FieldType == typeof(PreferencesState)),
                "PreferencesService must not store a second settings copy or cached presentation state.");
            Require(!typeof(PreferencesController).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(field => field.FieldType == typeof(GameSettings) || field.FieldType == typeof(PreferencesState)),
                "PreferencesController must not store a second settings copy or cached presentation state.");

            mainController.SetMusicVolume(0.31f);
            gameplayController.Refresh();
            Require(Mathf.Approximately(gameplayView.LastSettings.musicVolume, 0.31f), "Gameplay must see a Main Menu change on refresh.");

            gameplayController.SetTextSpeed(73f);
            gameplayController.SetDialogueTextScale(1.15f);
            gameplayController.SetTextboxOpacity(0.44f);
            mainController.Refresh();
            Require(Mathf.Approximately(mainView.LastSettings.textSpeed, 73f), "Main Menu must see a gameplay change on refresh.");
            Require(Mathf.Approximately(mainView.LastSettings.dialogueTextScale, 1.15f), "Main Menu must see gameplay Text Size changes.");
            Require(Mathf.Approximately(mainView.LastSettings.textboxOpacity, 0.44f), "Main Menu must see gameplay Textbox Opacity changes.");

            gameplayController.Reset();
            mainController.Refresh();
            Require(Mathf.Approximately(mainView.LastSettings.musicVolume, new GameSettings().musicVolume), "Reset in gameplay must refresh shared defaults in Main Menu.");
            Require(Mathf.Approximately(gameplayView.LastSettings.textSpeed, new GameSettings().textSpeed), "Reset must refresh the currently open gameplay view.");
        }
        finally
        {
            manager.settings = previousSettings;
        }
    }

    private static void VerifyPersistenceRoundtripAndCanonicalDisplayMode()
    {
        SettingsManager manager = GetSettingsManager();
        GameSettings previousSettings = manager.settings;
        var snapshots = CapturePreferences();

        try
        {
            manager.settings = new GameSettings();
            var service = new PreferencesService(manager);
            service.SetMasterVolume(0.41f);
            service.SetMusicVolume(0.42f);
            service.SetSfxVolume(0.43f);
            service.SetMuteAll(true);
            service.SetScreenMode(SettingsOptionValues.Borderless);
            service.SetResolution("1600x900");
            service.SetRunInBackground(true);
            service.SetSkipMode("Всё");
            service.SetSkipBehavior(SettingsOptionValues.FastSkip);
            service.SetTextSpeed(77f);
            service.SetDialogueTextScale(1.2f);
            service.SetTextboxOpacity(0.37f);
            service.SetAutoForwardDelay(333f);
            service.SetSkipAfterChoices(true);
            service.SetAutoForward(true);
            service.SetAutoSave(false);

            manager.settings = null;
            manager.LoadSettings();
            GameSettings loaded = manager.CurrentSettings;
            Require(Mathf.Approximately(loaded.masterVolume, 0.41f), "Master Volume persistence roundtrip failed.");
            Require(Mathf.Approximately(loaded.musicVolume, 0.42f), "Music Volume persistence roundtrip failed.");
            Require(Mathf.Approximately(loaded.sfxVolume, 0.43f), "SFX Volume persistence roundtrip failed.");
            Require(loaded.muteAll, "Mute All persistence roundtrip failed.");
            Require(loaded.screenMode == SettingsOptionValues.Borderless, "Screen Mode persistence roundtrip failed.");
            Require(loaded.resolution == "1600x900", "Resolution persistence roundtrip failed.");
            Require(loaded.runInBackground, "Run in Background persistence roundtrip failed.");
            Require(loaded.skipMode == "Всё", "Skip Mode persistence roundtrip failed.");
            Require(loaded.skipBehavior == SettingsOptionValues.FastSkip, "Skip Speed persistence roundtrip failed.");
            Require(Mathf.Approximately(loaded.textSpeed, 77f), "Text Speed persistence roundtrip failed.");
            Require(Mathf.Approximately(loaded.dialogueTextScale, 1.2f), "Text Size persistence roundtrip failed.");
            Require(Mathf.Approximately(loaded.textboxOpacity, 0.37f), "Textbox Opacity persistence roundtrip failed.");
            Require(Mathf.Approximately(loaded.autoForwardDelay, 333f), "Auto Forward Delay persistence roundtrip failed.");
            Require(loaded.skipAfterChoices, "Skip After Choices persistence roundtrip failed.");
            Require(loaded.autoForward, "Auto Forward persistence roundtrip failed.");
            Require(!loaded.autoSave, "Autosave persistence roundtrip failed.");

            PlayerPrefs.SetString(ScreenModeKey, SettingsOptionValues.Windowed);
            PlayerPrefs.SetInt(FullscreenKey, 1);
            manager.LoadSettings();
            Require(manager.CurrentSettings.screenMode == SettingsOptionValues.Windowed, "Legacy fullscreen must not override canonical screenMode during load.");
            Require(!manager.CurrentSettings.fullscreen, "Compatibility bool must be derived from canonical Windowed mode.");
            manager.SaveSettings();
            Require(PlayerPrefs.GetInt(FullscreenKey) == 0, "Compatibility key must be rewritten from canonical screenMode.");

            manager.settings = null;
            manager.ResetSettings();
            Require(manager.CurrentSettings != null, "Reset must recover safely from a null settings DTO.");
            GameSettings defaults = new GameSettings();
            Require(Mathf.Approximately(manager.CurrentSettings.masterVolume, defaults.masterVolume), "Reset must restore Master Volume default.");
            Require(Mathf.Approximately(manager.CurrentSettings.musicVolume, defaults.musicVolume), "Reset must restore Music Volume default.");
            Require(Mathf.Approximately(manager.CurrentSettings.sfxVolume, defaults.sfxVolume), "Reset must restore SFX Volume default.");
            Require(manager.CurrentSettings.muteAll == defaults.muteAll, "Reset must restore Mute All default.");
            Require(manager.CurrentSettings.screenMode == defaults.screenMode, "Reset must restore canonical Screen Mode default.");
            Require(manager.CurrentSettings.resolution == defaults.resolution, "Reset must restore Resolution default.");
            Require(manager.CurrentSettings.runInBackground == defaults.runInBackground, "Reset must restore Run in Background default.");
            Require(manager.CurrentSettings.skipMode == defaults.skipMode, "Reset must restore Skip Mode default.");
            Require(manager.CurrentSettings.skipBehavior == defaults.skipBehavior, "Reset must restore Skip Speed default.");
            Require(Mathf.Approximately(manager.CurrentSettings.textSpeed, defaults.textSpeed), "Reset must restore Text Speed default.");
            Require(Mathf.Approximately(manager.CurrentSettings.dialogueTextScale, defaults.dialogueTextScale), "Reset must restore Text Size default.");
            Require(Mathf.Approximately(manager.CurrentSettings.textboxOpacity, defaults.textboxOpacity), "Reset must restore Textbox Opacity default.");
            Require(Mathf.Approximately(manager.CurrentSettings.autoForwardDelay, defaults.autoForwardDelay), "Reset must restore Auto Forward Delay default.");
            Require(manager.CurrentSettings.skipAfterChoices == defaults.skipAfterChoices, "Reset must restore Skip After Choices default.");
            Require(manager.CurrentSettings.autoForward == defaults.autoForward, "Reset must restore Auto Forward default.");
            Require(manager.CurrentSettings.autoSave == defaults.autoSave, "Reset must restore Autosave default.");
        }
        finally
        {
            manager.settings = previousSettings;
            RestorePreferences(snapshots);
        }
    }

    private static void VerifySaveIsolation()
    {
        Require(SaveData.CurrentVersion == 3, "Shared Preferences foundation must preserve SaveData v3.");
        string json = JsonUtility.ToJson(new SaveData());
        Require(json.IndexOf("settings", StringComparison.OrdinalIgnoreCase) < 0, "SaveData JSON must not gain a settings object.");
        Require(json.IndexOf("preferences", StringComparison.OrdinalIgnoreCase) < 0, "SaveData JSON must not gain Preferences state.");
        Require(!typeof(SaveData).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(field => field.FieldType == typeof(GameSettings)), "SaveData must not reference GameSettings.");
    }

    private static Dictionary<string, PreferenceSnapshot> CapturePreferences()
    {
        var snapshots = new Dictionary<string, PreferenceSnapshot>();
        foreach (string key in TestKeys)
        {
            snapshots[key] = new PreferenceSnapshot(key);
        }

        return snapshots;
    }

    private static void RestorePreferences(Dictionary<string, PreferenceSnapshot> snapshots)
    {
        foreach (PreferenceSnapshot snapshot in snapshots.Values)
        {
            snapshot.Restore();
        }

        PlayerPrefs.Save();
    }

    private static SettingsManager GetSettingsManager()
    {
        if (SettingsManager.Instance != null)
        {
            return SettingsManager.Instance;
        }

        SettingsManager manager = new GameObject("Shared Preferences Settings Manager").AddComponent<SettingsManager>();
        typeof(SettingsManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, manager);
        return manager;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class RecordingView : IPreferencesView
    {
        public PreferencesState LastSettings { get; private set; }

        public void Bind(PreferencesController controller)
        {
        }

        public void SetVisible(bool visible)
        {
        }

        public void Refresh(PreferencesState settings)
        {
            LastSettings = settings;
        }
    }

    private readonly struct PreferenceSnapshot
    {
        private readonly string key;
        private readonly bool existed;
        private readonly PreferenceValueType type;
        private readonly float floatValue;
        private readonly int intValue;
        private readonly string stringValue;

        public PreferenceSnapshot(string key)
        {
            this.key = key;
            existed = PlayerPrefs.HasKey(key);
            type = GetValueType(key);
            floatValue = type == PreferenceValueType.Float && existed ? PlayerPrefs.GetFloat(key) : 0f;
            intValue = type == PreferenceValueType.Int && existed ? PlayerPrefs.GetInt(key) : 0;
            stringValue = type == PreferenceValueType.String && existed ? PlayerPrefs.GetString(key) : null;
        }

        public void Restore()
        {
            if (!existed)
            {
                PlayerPrefs.DeleteKey(key);
                return;
            }

            switch (type)
            {
                case PreferenceValueType.Float:
                    PlayerPrefs.SetFloat(key, floatValue);
                    break;
                case PreferenceValueType.Int:
                    PlayerPrefs.SetInt(key, intValue);
                    break;
                default:
                    PlayerPrefs.SetString(key, stringValue);
                    break;
            }
        }

        private static PreferenceValueType GetValueType(string key)
        {
            if (key == MasterVolumeKey || key == MusicVolumeKey || key == SfxVolumeKey || key == AmbientVolumeKey
                || key == TextSpeedKey || key == AutoForwardDelayKey || key == DialogueTextScaleKey
                || key == TextboxOpacityKey)
            {
                return PreferenceValueType.Float;
            }

            if (key == ScreenModeKey || key == ResolutionKey || key == RefreshRateKey
                || key == GameLookKey || key == InterfaceStyleKey || key == LanguageKey
                || key == FontSizeModeKey || key == SkipModeKey || key == SkipBehaviorKey)
            {
                return PreferenceValueType.String;
            }

            return PreferenceValueType.Int;
        }
    }

    private enum PreferenceValueType
    {
        Float,
        Int,
        String
    }
}
