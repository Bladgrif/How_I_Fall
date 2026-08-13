public readonly struct PreferencesState
{
    public readonly float masterVolume;
    public readonly float musicVolume;
    public readonly float sfxVolume;
    public readonly bool muteAll;
    public readonly string screenMode;
    public readonly string resolution;
    public readonly bool runInBackground;
    public readonly string skipMode;
    public readonly string skipBehavior;
    public readonly float textSpeed;
    public readonly float dialogueTextScale;
    public readonly float textboxOpacity;
    public readonly float autoForwardDelay;
    public readonly bool skipAfterChoices;
    public readonly bool autoForward;
    public readonly bool autoSave;
    public readonly bool showQuickMenu;

    public PreferencesState(GameSettings settings)
    {
        GameSettings source = settings ?? new GameSettings();
        masterVolume = source.masterVolume;
        musicVolume = source.musicVolume;
        sfxVolume = source.sfxVolume;
        muteAll = source.muteAll;
        screenMode = source.screenMode;
        resolution = source.resolution;
        runInBackground = source.runInBackground;
        skipMode = source.skipMode;
        skipBehavior = source.skipBehavior;
        textSpeed = source.textSpeed;
        dialogueTextScale = source.dialogueTextScale;
        textboxOpacity = source.textboxOpacity;
        autoForwardDelay = source.autoForwardDelay;
        skipAfterChoices = source.skipAfterChoices;
        autoForward = source.autoForward;
        autoSave = source.autoSave;
        showQuickMenu = source.showQuickMenu;
    }
}

public interface IPreferencesService
{
    PreferencesState Current { get; }
    bool IsAvailable { get; }

    void Reset();
    void SetMasterVolume(float value);
    void SetMusicVolume(float value);
    void SetSfxVolume(float value);
    void SetMuteAll(bool value);
    void SetScreenMode(string value);
    void SetResolution(string value);
    void SetRunInBackground(bool value);
    void SetSkipMode(string value);
    void SetSkipBehavior(string value);
    void SetTextSpeed(float value);
    void SetDialogueTextScale(float value);
    void SetTextboxOpacity(float value);
    void SetAutoForwardDelay(float value);
    void SetSkipAfterChoices(bool value);
    void SetAutoForward(bool value);
    void SetAutoSave(bool value);
    void SetShowQuickMenu(bool value);
}

/// <summary>
/// Stateless typed bridge to the single SettingsManager owner.
/// It never caches or mirrors GameSettings.
/// </summary>
public sealed class PreferencesService : IPreferencesService
{
    private readonly SettingsManager fixedManager;
    private readonly bool useGlobalManager;

    public PreferencesService()
    {
        useGlobalManager = true;
    }

    public PreferencesService(SettingsManager manager)
    {
        fixedManager = manager;
        useGlobalManager = false;
    }

    private SettingsManager Manager => useGlobalManager ? SettingsManager.Instance : fixedManager;

    public PreferencesState Current => new PreferencesState(Manager != null ? Manager.CurrentSettings : null);
    public bool IsAvailable => Manager != null;

    public void Reset() => Manager?.ResetSettings();
    public void SetMasterVolume(float value) => Manager?.SetMasterVolume(value);
    public void SetMusicVolume(float value) => Manager?.SetMusicVolume(value);
    public void SetSfxVolume(float value) => Manager?.SetSfxVolume(value);
    public void SetMuteAll(bool value) => Manager?.SetMuteAll(value);
    public void SetScreenMode(string value) => Manager?.SetScreenMode(value);
    public void SetResolution(string value) => Manager?.SetResolution(value);
    public void SetRunInBackground(bool value) => Manager?.SetRunInBackground(value);
    public void SetSkipMode(string value) => Manager?.SetSkipMode(value);
    public void SetSkipBehavior(string value) => Manager?.SetSkipBehavior(value);
    public void SetTextSpeed(float value) => Manager?.SetTextSpeed(value);
    public void SetDialogueTextScale(float value) => Manager?.SetDialogueTextScale(value);
    public void SetTextboxOpacity(float value) => Manager?.SetTextboxOpacity(value);
    public void SetAutoForwardDelay(float value) => Manager?.SetAutoForwardDelay(value);
    public void SetSkipAfterChoices(bool value) => Manager?.SetSkipAfterChoices(value);
    public void SetAutoForward(bool value) => Manager?.SetAutoForward(value);
    public void SetAutoSave(bool value) => Manager?.SetAutoSave(value);
    public void SetShowQuickMenu(bool value) => Manager?.SetShowQuickMenu(value);
}
