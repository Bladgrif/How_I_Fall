using System;
using System.Collections.Generic;
using UnityEngine;

public interface IPreferencesView
{
    void Bind(PreferencesController controller);
    void SetVisible(bool visible);
    void Refresh(PreferencesState settings);
}

/// <summary>
/// Shared presentation behavior used by Main Menu and gameplay adapters.
/// Context-specific navigation stays in the supplied close callback/view.
/// </summary>
public sealed class PreferencesController
{
    private readonly IPreferencesService service;
    private readonly IPreferencesView view;
    private readonly Action<string> showToast;
    private readonly Action onClosed;
    private readonly UnityEngine.Object logContext;

    public PreferencesController(
        IPreferencesService service,
        IPreferencesView view,
        Action<string> showToast = null,
        Action onClosed = null,
        UnityEngine.Object logContext = null)
    {
        this.service = service;
        this.view = view;
        this.showToast = showToast;
        this.onClosed = onClosed;
        this.logContext = logContext;
    }

    public IPreferencesService Service => service;

    public void Initialize()
    {
        view?.Bind(this);
        Hide();
    }

    public void Open()
    {
        if (view == null)
        {
            Debug.LogWarning("Preferences view is not assigned.", logContext);
            return;
        }

        Refresh();
        view.SetVisible(true);
    }

    public void Close()
    {
        Hide();
        onClosed?.Invoke();
    }

    public void Hide()
    {
        view?.SetVisible(false);
    }

    public void Refresh()
    {
        if (service != null && service.IsAvailable)
        {
            view?.Refresh(service.Current);
        }
    }

    public void Reset()
    {
        if (service == null || !service.IsAvailable)
        {
            return;
        }

        service.Reset();
        Refresh();
        showToast?.Invoke("Настройки сброшены");
    }

    public void SetMasterVolume(float value) => service?.SetMasterVolume(value);
    public void SetMusicVolume(float value) => service?.SetMusicVolume(value);
    public void SetSfxVolume(float value) => service?.SetSfxVolume(value);
    public void SetRunInBackground(bool value) => service?.SetRunInBackground(value);
    public void SetTextSpeed(float value) => service?.SetTextSpeed(value);
    public void SetAutoForwardDelay(float value) => service?.SetAutoForwardDelay(value);
    public void SetSkipAfterChoices(bool value) => service?.SetSkipAfterChoices(value);
    public void SetAutoForward(bool value) => service?.SetAutoForward(value);
    public void SetAutoSave(bool value) => service?.SetAutoSave(value);

    public void SetScreenMode(string value)
    {
        service?.SetScreenMode(value);
        Refresh();
    }

    public void CycleScreenMode()
    {
        SetScreenMode(PreferencesOptions.GetNext(PreferencesOptions.ScreenModes, service != null ? service.Current.screenMode : null));
    }

    public void SetResolution(string value)
    {
        service?.SetResolution(value);
        Refresh();
    }

    public void CycleResolution()
    {
        SetResolution(PreferencesOptions.GetNext(PreferencesOptions.Resolutions, service != null ? service.Current.resolution : null));
    }

    public void SetSkipMode(string value)
    {
        service?.SetSkipMode(value);
        Refresh();
    }

    public void CycleSkipMode()
    {
        SetSkipMode(PreferencesOptions.GetNext(PreferencesOptions.SkipModes, service != null ? service.Current.skipMode : null));
    }

    public void SetSkipBehavior(string value)
    {
        service?.SetSkipBehavior(value);
        Refresh();
    }

    public void CycleSkipBehavior()
    {
        SetSkipBehavior(PreferencesOptions.GetNext(PreferencesOptions.SkipBehaviors, service != null ? service.Current.skipBehavior : null));
    }

    /// <summary>Compatibility for the compact gameplay toggle; screenMode remains canonical.</summary>
    public void SetFullscreen(bool value)
    {
        SetScreenMode(value ? SettingsOptionValues.Fullscreen : SettingsOptionValues.Windowed);
    }
}

public static class PreferencesOptions
{
    private static readonly string[] ScreenModeValues =
    {
        SettingsOptionValues.Fullscreen,
        SettingsOptionValues.Windowed,
        SettingsOptionValues.Borderless
    };

    private static readonly string[] ResolutionValues = { "1920x1080", "1600x900", "1280x720" };
    private static readonly string[] SkipModeValues = { "Виденное", "Всё", "Ничего" };
    private static readonly string[] SkipBehaviorValues = { SettingsOptionValues.ClassicSkip, SettingsOptionValues.FastSkip };

    public static IReadOnlyList<string> ScreenModes => ScreenModeValues;
    public static IReadOnlyList<string> Resolutions => ResolutionValues;
    public static IReadOnlyList<string> SkipModes => SkipModeValues;
    public static IReadOnlyList<string> SkipBehaviors => SkipBehaviorValues;

    public static string GetNext(IReadOnlyList<string> options, string current)
    {
        if (options == null || options.Count == 0)
        {
            return current;
        }

        for (int i = 0; i < options.Count; i++)
        {
            if (options[i] == current)
            {
                return options[(i + 1) % options.Count];
            }
        }

        return options[0];
    }
}

public static class PreferencesFormatting
{
    public static string TextSpeed(float value) => $"{Mathf.RoundToInt(value)} симв./сек.";
    public static string AutoForwardDelay(float value) => $"{Mathf.RoundToInt(value)} %";
}
