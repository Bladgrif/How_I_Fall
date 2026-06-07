using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    public GameSettings settings = new GameSettings();

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
    private const string TextSpeedKey = "hif_text_speed";
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
        settings.masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 0.8f);
        settings.musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 0.8f);
        settings.sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 0.8f);
        settings.ambientVolume = PlayerPrefs.GetFloat(AmbientVolumeKey, 0.8f);
        settings.musicDuringPause = PlayerPrefs.GetInt(MusicDuringPauseKey, 0) == 1;
        settings.screenMode = PlayerPrefs.GetString(ScreenModeKey, "Полный экран");
        settings.resolution = PlayerPrefs.GetString(ResolutionKey, "1920x1080");
        settings.refreshRate = PlayerPrefs.GetString(RefreshRateKey, "60");
        settings.gameLook = PlayerPrefs.GetString(GameLookKey, "Чистый");
        settings.interfaceStyle = PlayerPrefs.GetString(InterfaceStyleKey, "Классический");
        settings.rewindVhsFilter = PlayerPrefs.GetInt(RewindVhsFilterKey, 1) == 1;
        settings.runInBackground = PlayerPrefs.GetInt(RunInBackgroundKey, 0) == 1;
        settings.characterAnimations = PlayerPrefs.GetInt(CharacterAnimationsKey, 1) == 1;
        settings.backgroundAnimations = PlayerPrefs.GetInt(BackgroundAnimationsKey, 1) == 1;
        settings.textSpeed = PlayerPrefs.GetFloat(TextSpeedKey, 1f);
        settings.fullscreen = PlayerPrefs.GetInt(FullscreenKey, 1) == 1;
        ApplySettings();
        AudioManager.Instance?.ApplySettingsVolume();
    }

    public void SaveSettings()
    {
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
        PlayerPrefs.SetFloat(TextSpeedKey, settings.textSpeed);
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
        settings.screenMode = string.IsNullOrEmpty(value) ? "Полный экран" : value;
        settings.fullscreen = settings.screenMode == "Полный экран";
        ApplySettings();
        SaveSettings();
    }

    public void SetResolution(string value)
    {
        settings.resolution = string.IsNullOrEmpty(value) ? "1920x1080" : value;
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

    public void SetTextSpeed(float value)
    {
        settings.textSpeed = Mathf.Clamp(value, 0.25f, 3f);
        SaveSettings();
    }

    public void SetFullscreen(bool value)
    {
        settings.fullscreen = value;
        settings.screenMode = value ? "Полный экран" : "Окно";
        ApplySettings();
        SaveSettings();
    }

    private void ApplySettings()
    {
        AudioListener.volume = Mathf.Clamp01(settings.masterVolume);
        Screen.fullScreen = settings.screenMode == "Полный экран";
        Application.runInBackground = settings.runInBackground;
    }
}
