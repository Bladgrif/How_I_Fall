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
    private readonly SharedPreferencesView sharedView;
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
        Transform contextTransform = panel != null ? panel.transform : dimOverlay != null ? dimOverlay.transform : null;
        sharedView = SharedPreferencesView.Create(contextTransform, "Gameplay");
    }

    public void Bind(PreferencesController sharedController)
    {
        controller = sharedController;
        if (isBound)
        {
            return;
        }

        isBound = true;
        sharedView?.Bind(controller);
    }

    public void SetVisible(bool visible)
    {
        panel?.SetActive(false);
        dimOverlay?.SetActive(false);
        sharedView?.SetVisible(visible);
    }

    public void Refresh(PreferencesState settings)
    {
        sharedView?.Refresh(settings);
    }

    public SharedPreferencesView SharedView => sharedView;

}
