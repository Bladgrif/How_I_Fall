using UnityEngine;
using UnityEngine.UI;

public interface IVNSettingsService
{
    GameSettings Settings { get; }
    bool IsAvailable { get; }
    void Reset();
    void SetMasterVolume(float value);
    void SetMusicVolume(float value);
    void SetSfxVolume(float value);
    void SetTextSpeed(float value);
    void SetAutoForward(bool value);
    void SetAutoForwardDelay(float value);
    void SetFullscreen(bool value);
}

public sealed class VNSettingsService : IVNSettingsService
{
    public GameSettings Settings => SettingsManager.Instance?.settings;
    public bool IsAvailable => SettingsManager.Instance != null;

    public void Reset() => SettingsManager.Instance?.ResetSettings();
    public void SetMasterVolume(float value) => SettingsManager.Instance?.SetMasterVolume(value);
    public void SetMusicVolume(float value) => SettingsManager.Instance?.SetMusicVolume(value);
    public void SetSfxVolume(float value) => SettingsManager.Instance?.SetSfxVolume(value);
    public void SetTextSpeed(float value) => SettingsManager.Instance?.SetTextSpeed(value);
    public void SetAutoForward(bool value) => SettingsManager.Instance?.SetAutoForward(value);
    public void SetAutoForwardDelay(float value) => SettingsManager.Instance?.SetAutoForwardDelay(value);
    public void SetFullscreen(bool value) => SettingsManager.Instance?.SetFullscreen(value);
}

public sealed class VNSettingsPresenter
{
    private readonly GameObject dimOverlay;
    private readonly GameObject panel;
    private readonly Slider masterVolumeSlider;
    private readonly Slider musicVolumeSlider;
    private readonly Slider sfxVolumeSlider;
    private readonly Slider textSpeedSlider;
    private readonly Toggle autoForwardToggle;
    private readonly Slider autoForwardDelaySlider;
    private readonly Toggle fullscreenToggle;
    private readonly Button closeButton;
    private readonly Button resetButton;
    private readonly IVNSettingsService settingsService;
    private readonly System.Action<string> showToast;
    private readonly Object logContext;

    public VNSettingsPresenter(
        GameObject dimOverlay,
        GameObject panel,
        Slider masterVolumeSlider,
        Slider musicVolumeSlider,
        Slider sfxVolumeSlider,
        Slider textSpeedSlider,
        Toggle autoForwardToggle,
        Slider autoForwardDelaySlider,
        Toggle fullscreenToggle,
        Button closeButton,
        Button resetButton,
        IVNSettingsService settingsService,
        System.Action<string> showToast,
        Object logContext)
    {
        this.dimOverlay = dimOverlay;
        this.panel = panel;
        this.masterVolumeSlider = masterVolumeSlider;
        this.musicVolumeSlider = musicVolumeSlider;
        this.sfxVolumeSlider = sfxVolumeSlider;
        this.textSpeedSlider = textSpeedSlider;
        this.autoForwardToggle = autoForwardToggle;
        this.autoForwardDelaySlider = autoForwardDelaySlider;
        this.fullscreenToggle = fullscreenToggle;
        this.closeButton = closeButton;
        this.resetButton = resetButton;
        this.settingsService = settingsService;
        this.showToast = showToast;
        this.logContext = logContext;
    }

    public void Initialize()
    {
        SetVisible(false);

        closeButton?.onClick.AddListener(Hide);
        resetButton?.onClick.AddListener(Reset);
        masterVolumeSlider?.onValueChanged.AddListener(SetMasterVolume);
        musicVolumeSlider?.onValueChanged.AddListener(SetMusicVolume);
        sfxVolumeSlider?.onValueChanged.AddListener(SetSfxVolume);
        textSpeedSlider?.onValueChanged.AddListener(SetTextSpeed);
        autoForwardToggle?.onValueChanged.AddListener(SetAutoForward);
        autoForwardDelaySlider?.onValueChanged.AddListener(SetAutoForwardDelay);
        fullscreenToggle?.onValueChanged.AddListener(SetFullscreen);
    }

    public void Open()
    {
        if (panel == null)
        {
            Debug.LogWarning("VN settings panel is not assigned.", logContext);
            return;
        }

        Refresh();
        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
    }

    public void Reset()
    {
        if (settingsService == null || !settingsService.IsAvailable)
        {
            return;
        }

        settingsService.Reset();
        Refresh();
        showToast?.Invoke("Настройки сброшены");
    }

    public void SetMasterVolume(float value)
    {
        settingsService?.SetMasterVolume(value);
    }

    public void SetMusicVolume(float value)
    {
        settingsService?.SetMusicVolume(value);
    }

    public void SetSfxVolume(float value)
    {
        settingsService?.SetSfxVolume(value);
    }

    public void SetTextSpeed(float value)
    {
        settingsService?.SetTextSpeed(value);
    }

    public void SetAutoForward(bool value)
    {
        settingsService?.SetAutoForward(value);
    }

    public void SetAutoForwardDelay(float value)
    {
        settingsService?.SetAutoForwardDelay(value);
    }

    public void SetFullscreen(bool value)
    {
        settingsService?.SetFullscreen(value);
    }

    private void Refresh()
    {
        GameSettings settings = settingsService?.Settings;
        if (settings == null)
        {
            return;
        }
        masterVolumeSlider?.SetValueWithoutNotify(settings.masterVolume);
        musicVolumeSlider?.SetValueWithoutNotify(settings.musicVolume);
        sfxVolumeSlider?.SetValueWithoutNotify(settings.sfxVolume);
        textSpeedSlider?.SetValueWithoutNotify(settings.textSpeed);
        autoForwardToggle?.SetIsOnWithoutNotify(settings.autoForward);
        autoForwardDelaySlider?.SetValueWithoutNotify(settings.autoForwardDelay);
        fullscreenToggle?.SetIsOnWithoutNotify(settings.fullscreen);
    }

    private void SetVisible(bool visible)
    {
        panel?.SetActive(visible);
        dimOverlay?.SetActive(visible);
    }
}
