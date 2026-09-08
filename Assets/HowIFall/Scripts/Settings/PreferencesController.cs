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
    public bool IsOpen { get; private set; }
    private GameSettings draft;
    private PreferencesState baseline;
    public bool IsDirty => IsOpen && draft != null && !new PreferencesState(draft).Equals(baseline);

    private static GameSettings Copy(PreferencesState state) => new GameSettings
    {
        masterVolume = state.masterVolume,
        musicVolume = state.musicVolume,
        sfxVolume = state.sfxVolume,
        muteAll = state.muteAll,
        screenMode = state.screenMode,
        resolution = state.resolution,
        runInBackground = state.runInBackground,
        skipMode = state.skipMode,
        skipBehavior = state.skipBehavior,
        textSpeed = state.textSpeed,
        dialogueTextScale = state.dialogueTextScale,
        textboxOpacity = state.textboxOpacity,
        autoForwardDelay = state.autoForwardDelay,
        skipAfterChoices = state.skipAfterChoices,
        autoForward = state.autoForward,
        autoSave = state.autoSave,
        showQuickMenu = state.showQuickMenu,
    };

    private void Edit(Action<GameSettings> edit)
    {
        if (!IsOpen || draft == null) return;
        edit(draft);
        Refresh();
    }

    public void Apply()
    {
        if (!IsOpen || draft == null || service == null || !service.IsAvailable) return;
        if (draft.masterVolume != baseline.masterVolume) service.SetMasterVolume(draft.masterVolume);
        if (draft.musicVolume != baseline.musicVolume) service.SetMusicVolume(draft.musicVolume);
        if (draft.sfxVolume != baseline.sfxVolume) service.SetSfxVolume(draft.sfxVolume);
        if (draft.muteAll != baseline.muteAll) service.SetMuteAll(draft.muteAll);
        if (draft.screenMode != baseline.screenMode) service.SetScreenMode(draft.screenMode);
        if (draft.resolution != baseline.resolution) service.SetResolution(draft.resolution);
        if (draft.runInBackground != baseline.runInBackground) service.SetRunInBackground(draft.runInBackground);
        if (draft.skipMode != baseline.skipMode) service.SetSkipMode(draft.skipMode);
        if (draft.skipBehavior != baseline.skipBehavior) service.SetSkipBehavior(draft.skipBehavior);
        if (draft.textSpeed != baseline.textSpeed) service.SetTextSpeed(draft.textSpeed);
        if (draft.dialogueTextScale != baseline.dialogueTextScale) service.SetDialogueTextScale(draft.dialogueTextScale);
        if (draft.textboxOpacity != baseline.textboxOpacity) service.SetTextboxOpacity(draft.textboxOpacity);
        if (draft.autoForwardDelay != baseline.autoForwardDelay) service.SetAutoForwardDelay(draft.autoForwardDelay);
        if (draft.skipAfterChoices != baseline.skipAfterChoices) service.SetSkipAfterChoices(draft.skipAfterChoices);
        if (draft.autoForward != baseline.autoForward) service.SetAutoForward(draft.autoForward);
        if (draft.autoSave != baseline.autoSave) service.SetAutoSave(draft.autoSave);
        if (draft.showQuickMenu != baseline.showQuickMenu) service.SetShowQuickMenu(draft.showQuickMenu);
        baseline = service.Current;
        draft = Copy(baseline);
        Refresh();
    }

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

        if (service == null || !service.IsAvailable) return;
        baseline = service.Current;
        draft = Copy(baseline);
        IsOpen = true;
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
        IsOpen = false;
        draft = null;
        baseline = default;
    }

    public void Refresh()
    {
        if (service != null && service.IsAvailable)
        {
            view?.Refresh(draft != null ? new PreferencesState(draft) : service.Current);
        }
    }

    public void Reset()
    {
        if (service == null || !service.IsAvailable)
        {
            return;
        }

        if (!IsOpen || draft == null) return;
        GameSettings defaults = new GameSettings();
        // Reset only the player-facing settings; compatibility-only values stay untouched.
        draft.masterVolume = defaults.masterVolume;
        draft.musicVolume = defaults.musicVolume;
        draft.sfxVolume = defaults.sfxVolume;
        draft.screenMode = defaults.screenMode;
        draft.resolution = defaults.resolution;
        draft.skipMode = defaults.skipMode;
        draft.textSpeed = defaults.textSpeed;
        draft.dialogueTextScale = defaults.dialogueTextScale;
        draft.textboxOpacity = defaults.textboxOpacity;
        draft.autoForwardDelay = defaults.autoForwardDelay;
        draft.skipAfterChoices = defaults.skipAfterChoices;
        draft.autoSave = defaults.autoSave;
        draft.showQuickMenu = defaults.showQuickMenu;
        Refresh();
    }

    public void SetMasterVolume(float value) => Edit(state => state.masterVolume = Mathf.Clamp01(value));
    public void SetMusicVolume(float value) => Edit(state => state.musicVolume = Mathf.Clamp01(value));
    public void SetSfxVolume(float value) => Edit(state => state.sfxVolume = Mathf.Clamp01(value));
    public void SetMuteAll(bool value) => Edit(state => state.muteAll = value);
    public void SetRunInBackground(bool value) => Edit(state => state.runInBackground = value);
    public void SetTextSpeed(float value) => Edit(state => state.textSpeed = Mathf.Clamp(value, 20f, 100f));
    public void SetDialogueTextScale(float value) => Edit(state => state.dialogueTextScale = Mathf.Clamp(value, 0.85f, 1.25f));
    public void SetTextboxOpacity(float value) => Edit(state => state.textboxOpacity = Mathf.Clamp01(value));
    public void SetAutoForwardDelay(float value) => Edit(state => state.autoForwardDelay = Mathf.Clamp(value, 50f, 500f));
    public void SetSkipAfterChoices(bool value) => Edit(state => state.skipAfterChoices = value);
    public void SetAutoForward(bool value) => Edit(state => state.autoForward = value);
    public void SetAutoSave(bool value) => Edit(state => state.autoSave = value);
    public void SetShowQuickMenu(bool value) => Edit(state => state.showQuickMenu = value);

    public void SetSkipUnseen(bool value)
    {
        SetSkipMode(value ? "Всё" : "Виденное");
    }

    public void SetScreenMode(string value)
    {
        Edit(state => state.screenMode = string.IsNullOrEmpty(value) ? SettingsOptionValues.Fullscreen : value);
    }

    public void CycleScreenMode()
    {
        SetScreenMode(PreferencesOptions.GetNext(PreferencesOptions.ScreenModes, draft != null ? draft.screenMode : null));
    }

    public void SetResolution(string value)
    {
        Edit(state => state.resolution = string.IsNullOrEmpty(value) ? "1920x1080" : value);
    }

    public void CycleResolution()
    {
        SetResolution(PreferencesOptions.GetNext(PreferencesOptions.Resolutions, draft != null ? draft.resolution : null));
    }

    public void SetSkipMode(string value)
    {
        Edit(state => state.skipMode = string.IsNullOrEmpty(value) ? "Виденное" : value);
    }

    public void CycleSkipMode()
    {
        SetSkipMode(PreferencesOptions.GetNext(PreferencesOptions.SkipModes, draft != null ? draft.skipMode : null));
    }

    public void SetSkipBehavior(string value)
    {
        Edit(state => state.skipBehavior = string.IsNullOrEmpty(value) ? SettingsOptionValues.ClassicSkip : value);
    }

    public void CycleSkipBehavior()
    {
        SetSkipBehavior(PreferencesOptions.GetNext(PreferencesOptions.SkipBehaviors, draft != null ? draft.skipBehavior : null));
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

    private static readonly string[] ResolutionValues = { "1280x720", "1600x900", "1920x1080", "2560x1440", "3840x2160" };
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
    public static string TextSpeed(float value) => value < 40f ? "Медленно" : value < 65f ? "Обычно" : value < 85f ? "Быстро" : "Очень быстро";
    public static readonly string[] TextScaleLabels = { "Меньше", "Обычный", "Крупный", "Очень крупный" };
    public static string TextScaleLabel(float value) => value < 0.925f ? TextScaleLabels[0] : value < 1.075f ? TextScaleLabels[1] : value < 1.2f ? TextScaleLabels[2] : TextScaleLabels[3];
    public static float TextScaleValue(string label) => label == TextScaleLabels[0] ? 0.85f : label == TextScaleLabels[2] ? 1.15f : label == TextScaleLabels[3] ? 1.25f : 1f;
    public static float AutoForwardDelaySeconds(float storedValue) => Mathf.Clamp(storedValue / 100f, 0.5f, 5f);
    public static float AutoForwardDelayStored(float seconds) => Mathf.Clamp(seconds, 0.5f, 5f) * 100f;
    public static string AutoForwardDelay(float storedValue) => $"{AutoForwardDelaySeconds(storedValue):0.0} сек.";
    public static string Percent(float value) => $"{Mathf.RoundToInt(Mathf.Clamp01(value) * 100f)} %";
    public static string TextScale(float value) => $"{Mathf.RoundToInt(Mathf.Clamp(value, 0.85f, 1.25f) * 100f)} %";
}
