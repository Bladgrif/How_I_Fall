using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Thin gameplay view/context adapter for the shared PreferencesController.
/// The filename is retained so Unity keeps the existing asset metadata stable.
/// </summary>
public sealed class VNPreferencesAdapter : IPreferencesView
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
    private PreferencesController controller;
    private bool isBound;

    public VNPreferencesAdapter(
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
        Button resetButton)
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
    }

    public void Bind(PreferencesController sharedController)
    {
        controller = sharedController;
        if (isBound)
        {
            return;
        }

        isBound = true;
        closeButton?.onClick.AddListener(controller.Close);
        resetButton?.onClick.AddListener(controller.Reset);
        masterVolumeSlider?.onValueChanged.AddListener(controller.SetMasterVolume);
        musicVolumeSlider?.onValueChanged.AddListener(controller.SetMusicVolume);
        sfxVolumeSlider?.onValueChanged.AddListener(controller.SetSfxVolume);
        textSpeedSlider?.onValueChanged.AddListener(controller.SetTextSpeed);
        autoForwardToggle?.onValueChanged.AddListener(controller.SetAutoForward);
        autoForwardDelaySlider?.onValueChanged.AddListener(controller.SetAutoForwardDelay);
        fullscreenToggle?.onValueChanged.AddListener(controller.SetFullscreen);

    }

    public void SetVisible(bool visible)
    {
        panel?.SetActive(visible);
        dimOverlay?.SetActive(visible);
    }

    public void Refresh(PreferencesState settings)
    {
        masterVolumeSlider?.SetValueWithoutNotify(settings.masterVolume);
        musicVolumeSlider?.SetValueWithoutNotify(settings.musicVolume);
        sfxVolumeSlider?.SetValueWithoutNotify(settings.sfxVolume);
        textSpeedSlider?.SetValueWithoutNotify(settings.textSpeed);
        autoForwardToggle?.SetIsOnWithoutNotify(settings.autoForward);
        autoForwardDelaySlider?.SetValueWithoutNotify(settings.autoForwardDelay);
        fullscreenToggle?.SetIsOnWithoutNotify(SettingsManager.IsFullscreenScreenMode(settings.screenMode));
    }

}
