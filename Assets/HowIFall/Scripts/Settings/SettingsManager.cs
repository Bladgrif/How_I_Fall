using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    public GameSettings settings = new GameSettings();
    public GameSettings CurrentSettings
    {
        get
        {
            if (settings == null)
            {
                settings = new GameSettings();
            }

            return settings;
        }
    }

    private const string MasterVolumeKey = "hif_master_volume";
    private const string MusicVolumeKey = "hif_music_volume";
    private const string SfxVolumeKey = "hif_sfx_volume";
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
    private const string AutoForwardDelayKey = "hif_auto_forward_delay";
    private const string SkipAfterChoicesKey = "hif_skip_after_choices";
    private const string AutoForwardKey = "hif_auto_forward";
    private const string AutoSaveKey = "hif_auto_save";
    private const string ShowHintsKey = "hif_show_hints";
    private const string FullscreenKey = "hif_fullscreen";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadSettings();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void LoadSettings()
    {
        _ = CurrentSettings;
        settings.masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 0.8f);
        settings.musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 0.8f);
        settings.sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 0.8f);
        settings.ambientVolume = PlayerPrefs.GetFloat(AmbientVolumeKey, 0.8f);
        settings.musicDuringPause = PlayerPrefs.GetInt(MusicDuringPauseKey, 0) == 1;
        settings.screenMode = PlayerPrefs.GetString(ScreenModeKey, SettingsOptionValues.Fullscreen);
        settings.resolution = PlayerPrefs.GetString(ResolutionKey, "1920x1080");
        settings.refreshRate = PlayerPrefs.GetString(RefreshRateKey, "60");
        settings.gameLook = PlayerPrefs.GetString(GameLookKey, "Чистый");
        settings.interfaceStyle = PlayerPrefs.GetString(InterfaceStyleKey, "Классический");
        settings.rewindVhsFilter = PlayerPrefs.GetInt(RewindVhsFilterKey, 1) == 1;
        settings.runInBackground = PlayerPrefs.GetInt(RunInBackgroundKey, 0) == 1;
        settings.characterAnimations = PlayerPrefs.GetInt(CharacterAnimationsKey, 1) == 1;
        settings.backgroundAnimations = PlayerPrefs.GetInt(BackgroundAnimationsKey, 1) == 1;
        settings.language = PlayerPrefs.GetString(LanguageKey, "Русский");
        settings.fontSizeMode = PlayerPrefs.GetString(FontSizeModeKey, "Мелкий");
        settings.skipMode = PlayerPrefs.GetString(SkipModeKey, "Виденное");
        settings.skipBehavior = PlayerPrefs.GetString(SkipBehaviorKey, SettingsOptionValues.ClassicSkip);
        settings.textSpeed = Mathf.Clamp(PlayerPrefs.GetFloat(TextSpeedKey, 50f), 20f, 100f);
        settings.autoForwardDelay = Mathf.Clamp(PlayerPrefs.GetFloat(AutoForwardDelayKey, 250f), 50f, 500f);
        settings.skipAfterChoices = PlayerPrefs.GetInt(SkipAfterChoicesKey, 0) == 1;
        settings.autoForward = PlayerPrefs.GetInt(AutoForwardKey, 0) == 1;
        settings.autoSave = PlayerPrefs.GetInt(AutoSaveKey, 1) == 1;
        settings.showHints = PlayerPrefs.GetInt(ShowHintsKey, 1) == 1;
        settings.fullscreen = IsFullscreenScreenMode(settings.screenMode);
        ApplySettings();
        AudioManager.Instance?.ApplySettingsVolume();
    }

    public void SaveSettings()
    {
        _ = CurrentSettings;
        // screenMode is canonical. The bool/key remain write-only compatibility data.
        settings.fullscreen = IsFullscreenScreenMode(settings.screenMode);
        PlayerPrefs.SetFloat(MasterVolumeKey, settings.masterVolume);
        PlayerPrefs.SetFloat(MusicVolumeKey, settings.musicVolume);
        PlayerPrefs.SetFloat(SfxVolumeKey, settings.sfxVolume);
        PlayerPrefs.SetFloat(AmbientVolumeKey, settings.ambientVolume);
        PlayerPrefs.SetInt(MusicDuringPauseKey, settings.musicDuringPause ? 1 : 0);
        PlayerPrefs.SetString(ScreenModeKey, settings.screenMode);
        PlayerPrefs.SetString(ResolutionKey, settings.resolution);
        PlayerPrefs.SetString(RefreshRateKey, settings.refreshRate);
        PlayerPrefs.SetString(GameLookKey, settings.gameLook);
        PlayerPrefs.SetString(InterfaceStyleKey, settings.interfaceStyle);
        PlayerPrefs.SetInt(RewindVhsFilterKey, settings.rewindVhsFilter ? 1 : 0);
        PlayerPrefs.SetInt(RunInBackgroundKey, settings.runInBackground ? 1 : 0);
        PlayerPrefs.SetInt(CharacterAnimationsKey, settings.characterAnimations ? 1 : 0);
        PlayerPrefs.SetInt(BackgroundAnimationsKey, settings.backgroundAnimations ? 1 : 0);
        PlayerPrefs.SetString(LanguageKey, settings.language);
        PlayerPrefs.SetString(FontSizeModeKey, settings.fontSizeMode);
        PlayerPrefs.SetString(SkipModeKey, settings.skipMode);
        PlayerPrefs.SetString(SkipBehaviorKey, settings.skipBehavior);
        PlayerPrefs.SetFloat(TextSpeedKey, settings.textSpeed);
        PlayerPrefs.SetFloat(AutoForwardDelayKey, settings.autoForwardDelay);
        PlayerPrefs.SetInt(SkipAfterChoicesKey, settings.skipAfterChoices ? 1 : 0);
        PlayerPrefs.SetInt(AutoForwardKey, settings.autoForward ? 1 : 0);
        PlayerPrefs.SetInt(AutoSaveKey, settings.autoSave ? 1 : 0);
        PlayerPrefs.SetInt(ShowHintsKey, settings.showHints ? 1 : 0);
        PlayerPrefs.SetInt(FullscreenKey, settings.fullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void ResetSettings()
    {
        settings = new GameSettings();
        ApplySettings();
        SaveSettings();
        AudioManager.Instance?.ApplySettingsVolume();
    }

    public void SetMasterVolume(float value)
    {
        settings.masterVolume = Mathf.Clamp01(value);
        ApplySettings();
        SaveSettings();
    }

    public void SetMusicVolume(float value)
    {
        settings.musicVolume = Mathf.Clamp01(value);
        SaveSettings();
        AudioManager.Instance?.ApplySettingsVolume();
    }

    public void SetSfxVolume(float value)
    {
        settings.sfxVolume = Mathf.Clamp01(value);
        SaveSettings();
        AudioManager.Instance?.ApplySettingsVolume();
    }

    public void SetAmbientVolume(float value)
    {
        settings.ambientVolume = Mathf.Clamp01(value);
        SaveSettings();
        AudioManager.Instance?.ApplySettingsVolume();
    }

    public void SetMusicDuringPause(bool value)
    {
        settings.musicDuringPause = value;
        SaveSettings();
        AudioManager.Instance?.ApplySettingsVolume();
    }

    public void SetScreenMode(string value)
    {
        settings.screenMode = string.IsNullOrEmpty(value) ? SettingsOptionValues.Fullscreen : value;
        settings.fullscreen = IsFullscreenScreenMode(settings.screenMode);
        ApplySettings();
        SaveSettings();
    }

    public void SetResolution(string value)
    {
        settings.resolution = string.IsNullOrEmpty(value) ? "1920x1080" : value;
        ApplySettings();
        SaveSettings();
    }

    public void SetRefreshRate(string value)
    {
        settings.refreshRate = string.IsNullOrEmpty(value) ? "60" : value;
        SaveSettings();
    }

    public void SetGameLook(string value)
    {
        settings.gameLook = string.IsNullOrEmpty(value) ? "Чистый" : value;
        SaveSettings();
    }

    public void SetInterfaceStyle(string value)
    {
        settings.interfaceStyle = string.IsNullOrEmpty(value) ? "Классический" : value;
        SaveSettings();
    }

    public void SetRewindVhsFilter(bool value)
    {
        settings.rewindVhsFilter = value;
        SaveSettings();
    }

    public void SetRunInBackground(bool value)
    {
        settings.runInBackground = value;
        ApplySettings();
        SaveSettings();
    }

    public void SetCharacterAnimations(bool value)
    {
        settings.characterAnimations = value;
        SaveSettings();
    }

    public void SetBackgroundAnimations(bool value)
    {
        settings.backgroundAnimations = value;
        SaveSettings();
    }

    public void SetLanguage(string value)
    {
        settings.language = string.IsNullOrEmpty(value) ? "Русский" : value;
        SaveSettings();
    }

    public void SetFontSizeMode(string value)
    {
        settings.fontSizeMode = string.IsNullOrEmpty(value) ? "Мелкий" : value;
        SaveSettings();
    }

    public void SetSkipMode(string value)
    {
        settings.skipMode = string.IsNullOrEmpty(value) ? "Виденное" : value;
        SaveSettings();
    }

    public void SetSkipBehavior(string value)
    {
        settings.skipBehavior = string.IsNullOrEmpty(value) ? SettingsOptionValues.ClassicSkip : value;
        SaveSettings();
    }

    public void SetTextSpeed(float value)
    {
        settings.textSpeed = Mathf.Clamp(value, 20f, 100f);
        SaveSettings();
    }

    public void SetAutoForwardDelay(float value)
    {
        settings.autoForwardDelay = Mathf.Clamp(value, 50f, 500f);
        SaveSettings();
    }

    public void SetSkipAfterChoices(bool value)
    {
        settings.skipAfterChoices = value;
        SaveSettings();
    }

    public void SetAutoForward(bool value)
    {
        settings.autoForward = value;
        SaveSettings();
    }

    public void SetAutoSave(bool value)
    {
        settings.autoSave = value;
        SaveSettings();
    }

    public void SetShowHints(bool value)
    {
        settings.showHints = value;
        SaveSettings();
    }


    public void SetFullscreen(bool value)
    {
        SetScreenMode(value ? SettingsOptionValues.Fullscreen : SettingsOptionValues.Windowed);
    }

    public static bool IsFullscreenScreenMode(string screenMode)
    {
        return GetFullScreenMode(screenMode) != FullScreenMode.Windowed;
    }

    public static FullScreenMode GetFullScreenMode(string screenMode)
    {
        if (screenMode == SettingsOptionValues.Borderless)
        {
            return FullScreenMode.FullScreenWindow;
        }

        return screenMode == SettingsOptionValues.Windowed
            ? FullScreenMode.Windowed
            : FullScreenMode.ExclusiveFullScreen;
    }

    public static bool TryParseResolution(string value, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] dimensions = value.Split('x');
        return dimensions.Length == 2
            && int.TryParse(dimensions[0], out width)
            && int.TryParse(dimensions[1], out height)
            && width > 0
            && height > 0;
    }

    private void ApplyResolution()
    {
        if (string.IsNullOrWhiteSpace(settings.resolution))
        {
            return;
        }

        if (!TryParseResolution(settings.resolution, out int width, out int height))
        {
            return;
        }

        Screen.SetResolution(width, height, GetFullScreenMode(settings.screenMode));
    }

    private void ApplySettings()
    {
        AudioListener.volume = Mathf.Clamp01(settings.masterVolume);
        Screen.fullScreenMode = GetFullScreenMode(settings.screenMode);
        ApplyResolution();
        Application.runInBackground = settings.runInBackground;
    }
}
