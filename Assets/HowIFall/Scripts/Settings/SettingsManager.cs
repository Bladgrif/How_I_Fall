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

    public void SetTextSpeed(float value)
    {
        settings.textSpeed = Mathf.Clamp(value, 0.25f, 3f);
        SaveSettings();
    }

    public void SetFullscreen(bool value)
    {
        settings.fullscreen = value;
        ApplySettings();
        SaveSettings();
    }

    private void ApplySettings()
    {
        AudioListener.volume = Mathf.Clamp01(settings.masterVolume);
        Screen.fullScreen = settings.fullscreen;
    }
}
