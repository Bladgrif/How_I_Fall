using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsPanelController : MonoBehaviour, IPreferencesView
{
    private static readonly string[] RefreshRateOptions = { "60", "120", "144" };
    private static readonly string[] GameLookOptions = { "Чистый", "VHS", "Кинематографичный" };
    private static readonly string[] InterfaceStyleOptions = { "Классический", "Современный" };
    private static readonly string[] LanguageOptions = { "Русский", "English" };
    private static readonly string[] FontSizeModeOptions = { "Мелкий", "Средний", "Крупный" };

    public GameObject root;
    public TextMeshProUGUI settingsTitleText;
    public GameObject videoContent;
    public GameObject audioContent;
    public GameObject gameContent;
    public Image videoTabImage;
    public Image audioTabImage;
    public Image gameTabImage;
    public TextMeshProUGUI videoTabText;
    public TextMeshProUGUI audioTabText;
    public TextMeshProUGUI gameTabText;
    public Sprite activeTabSprite;
    public Sprite inactiveTabSprite;
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;
    public Slider ambientVolumeSlider;
    public Toggle musicDuringPauseToggle;
    public TextMeshProUGUI screenModeValueText;
    public TextMeshProUGUI resolutionValueText;
    public TextMeshProUGUI refreshRateValueText;
    public TextMeshProUGUI gameLookValueText;
    public TextMeshProUGUI interfaceStyleValueText;
    public Toggle rewindVhsFilterToggle;
    public Toggle runInBackgroundToggle;
    public Toggle characterAnimationsToggle;
    public Toggle backgroundAnimationsToggle;
    public TextMeshProUGUI languageValueText;
    public TextMeshProUGUI fontSizeModeValueText;
    public TextMeshProUGUI skipModeValueText;
    public TextMeshProUGUI skipBehaviorValueText;
    public Slider textSpeedSlider;
    public TextMeshProUGUI textSpeedValueText;
    public Slider autoForwardDelaySlider;
    public TextMeshProUGUI autoForwardDelayValueText;
    public Toggle skipAfterChoicesToggle;
    public Toggle autoForwardToggle;
    public Toggle autoSaveToggle;
    public Toggle showHintsToggle;
    public GameObject[] objectsToHideWhenOpen;
    public GameObject[] controlsHiddenUntilImplemented;

    private PreferencesController preferencesController;
    private SharedPreferencesView sharedView;
    private bool sharedViewInitialized;

    public PreferencesController SharedController
    {
        get
        {
            if (preferencesController == null)
            {
                preferencesController = new PreferencesController(new PreferencesService(), this, logContext: this);
            }

            return preferencesController;
        }
    }

    private void Awake()
    {
        EnsureSharedViewInitialized();
    }

    public void Show()
    {
        EnsureSharedViewInitialized();
        SharedController.Open();
    }

    public void Hide()
    {
        SharedController.Close();
    }

    public void RefreshUi()
    {
        SharedController.Refresh();
    }

    public void ResetSettings()
    {
        SharedController.Reset();
    }

    void IPreferencesView.Bind(PreferencesController controller)
    {
        EnsureSharedView();
        sharedView?.Bind(controller);
    }

    void IPreferencesView.SetVisible(bool visible)
    {
        SetHiddenObjectsActive(!visible);
        if (root != null)
        {
            root.SetActive(false);
        }

        EnsureSharedView();
        sharedView?.SetVisible(visible);
    }

    void IPreferencesView.Refresh(PreferencesState settings)
    {
        EnsureSharedView();
        sharedView?.Refresh(settings);
    }

    private void EnsureSharedViewInitialized()
    {
        if (sharedViewInitialized)
        {
            return;
        }

        if (root == null)
        {
            root = gameObject;
        }

        EnsureSharedView();
        sharedViewInitialized = true;
        SharedController.Initialize();
    }

    private void EnsureSharedView()
    {
        if (sharedView == null && root != null)
        {
            sharedView = SharedPreferencesView.Create(root.transform, "MainMenu");
        }
    }

    private void RefreshFromSettings(PreferencesState settings)
    {
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(settings.masterVolume);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.SetValueWithoutNotify(settings.musicVolume);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.SetValueWithoutNotify(settings.sfxVolume);
        }

        SetValueText(screenModeValueText, settings.screenMode);
        SetValueText(resolutionValueText, settings.resolution);
        if (runInBackgroundToggle != null)
        {
            runInBackgroundToggle.SetIsOnWithoutNotify(settings.runInBackground);
        }

        SetValueText(skipModeValueText, settings.skipMode);
        SetValueText(skipBehaviorValueText, settings.skipBehavior);

        if (textSpeedSlider != null)
        {
            textSpeedSlider.SetValueWithoutNotify(settings.textSpeed);
        }

        SetTextSpeedValue(settings.textSpeed);

        if (autoForwardDelaySlider != null)
        {
            autoForwardDelaySlider.SetValueWithoutNotify(settings.autoForwardDelay);
        }

        SetAutoForwardDelayValue(settings.autoForwardDelay);

        if (skipAfterChoicesToggle != null)
        {
            skipAfterChoicesToggle.SetIsOnWithoutNotify(settings.skipAfterChoices);
        }

        if (autoForwardToggle != null)
        {
            autoForwardToggle.SetIsOnWithoutNotify(settings.autoForward);
        }

        if (autoSaveToggle != null)
        {
            autoSaveToggle.SetIsOnWithoutNotify(settings.autoSave);
        }

    }

    public void OnMasterVolumeChanged(float value)
    {
        SharedController.SetMasterVolume(value);
    }

    public void OnMusicVolumeChanged(float value)
    {
        SharedController.SetMusicVolume(value);
    }

    public void OnSfxVolumeChanged(float value)
    {
        SharedController.SetSfxVolume(value);
    }

    public void OnAmbientVolumeChanged(float value)
    {
        SettingsManager.Instance?.SetAmbientVolume(value);
    }

    public void OnMusicDuringPauseChanged(bool value)
    {
        SettingsManager.Instance?.SetMusicDuringPause(value);
    }

    public void CycleScreenMode()
    {
        SharedController.CycleScreenMode();
    }

    public void CycleResolution()
    {
        SharedController.CycleResolution();
    }

    public void CycleRefreshRate()
    {
        if (SettingsManager.Instance == null)
        {
            return;
        }

        string value = GetNextValue(RefreshRateOptions, SettingsManager.Instance.settings.refreshRate);
        SettingsManager.Instance.SetRefreshRate(value);
        SetValueText(refreshRateValueText, value);
    }

    public void CycleGameLook()
    {
        if (SettingsManager.Instance == null)
        {
            return;
        }

        string value = GetNextValue(GameLookOptions, SettingsManager.Instance.settings.gameLook);
        SettingsManager.Instance.SetGameLook(value);
        SetValueText(gameLookValueText, value);
    }

    public void CycleInterfaceStyle()
    {
        if (SettingsManager.Instance == null)
        {
            return;
        }

        string value = GetNextValue(InterfaceStyleOptions, SettingsManager.Instance.settings.interfaceStyle);
        SettingsManager.Instance.SetInterfaceStyle(value);
        SetValueText(interfaceStyleValueText, value);
    }

    public void OnRewindVhsFilterChanged(bool value)
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetRewindVhsFilter(value);
        }
    }

    public void OnRunInBackgroundChanged(bool value)
    {
        SharedController.SetRunInBackground(value);
    }

    public void OnCharacterAnimationsChanged(bool value)
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetCharacterAnimations(value);
        }
    }

    public void OnBackgroundAnimationsChanged(bool value)
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetBackgroundAnimations(value);
        }
    }

    public void CycleLanguage()
    {
        if (SettingsManager.Instance == null)
        {
            return;
        }

        string value = GetNextValue(LanguageOptions, SettingsManager.Instance.settings.language);
        SettingsManager.Instance.SetLanguage(value);
        SetValueText(languageValueText, value);
    }

    public void CycleFontSizeMode()
    {
        if (SettingsManager.Instance == null)
        {
            return;
        }

        string value = GetNextValue(FontSizeModeOptions, SettingsManager.Instance.settings.fontSizeMode);
        SettingsManager.Instance.SetFontSizeMode(value);
        SetValueText(fontSizeModeValueText, value);
    }

    public void CycleSkipMode()
    {
        SharedController.CycleSkipMode();
    }

    public void CycleSkipBehavior()
    {
        SharedController.CycleSkipBehavior();
    }

    public void OnTextSpeedChanged(float value)
    {
        SharedController.SetTextSpeed(value);

        SetTextSpeedValue(value);
    }

    public void OnAutoForwardDelayChanged(float value)
    {
        SharedController.SetAutoForwardDelay(value);

        SetAutoForwardDelayValue(value);
    }

    public void OnSkipAfterChoicesChanged(bool value)
    {
        SharedController.SetSkipAfterChoices(value);
    }

    public void OnAutoForwardChanged(bool value)
    {
        SharedController.SetAutoForward(value);
    }

    public void OnAutoSaveChanged(bool value)
    {
        SharedController.SetAutoSave(value);
    }

    public void OnShowHintsChanged(bool value)
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetShowHints(value);
        }
    }

    public void ShowVideoTab()
    {
        SetActiveTab(videoContent, videoTabImage, videoTabText);
        SetTitle("Настройки: видео");
    }

    public void ShowAudioTab()
    {
        SetActiveTab(audioContent, audioTabImage, audioTabText);
        SetTitle("Настройки: аудио");
    }

    public void ShowGameTab()
    {
        SetActiveTab(gameContent, gameTabImage, gameTabText);
        SetTitle("Настройки: игра");
    }

    private void SetHiddenControlsActive(bool isActive)
    {
        if (controlsHiddenUntilImplemented == null)
        {
            return;
        }

        foreach (GameObject control in controlsHiddenUntilImplemented)
        {
            if (control != null)
            {
                control.SetActive(isActive);
            }
        }
    }

    private void SetHiddenObjectsActive(bool isActive)
    {
        if (objectsToHideWhenOpen == null)
        {
            return;
        }

        foreach (GameObject target in objectsToHideWhenOpen)
        {
            if (target != null)
            {
                target.SetActive(isActive);
            }
        }
    }

    private void SetActiveTab(GameObject activeContent, Image activeImage, TextMeshProUGUI activeText)
    {
        SetContentActive(videoContent, videoContent == activeContent);
        SetContentActive(audioContent, audioContent == activeContent);
        SetContentActive(gameContent, gameContent == activeContent);

        SetTabVisual(videoTabImage, videoTabText, videoTabImage == activeImage && videoTabText == activeText);
        SetTabVisual(audioTabImage, audioTabText, audioTabImage == activeImage && audioTabText == activeText);
        SetTabVisual(gameTabImage, gameTabText, gameTabImage == activeImage && gameTabText == activeText);
    }

    private void SetContentActive(GameObject content, bool active)
    {
        if (content != null)
        {
            content.SetActive(active);
        }
    }

    private void SetTabVisual(Image image, TextMeshProUGUI text, bool active)
    {
        if (image != null)
        {
            image.sprite = active ? activeTabSprite : inactiveTabSprite;
            image.color = image.sprite != null
                ? new Color(1f, 1f, 1f, active ? 0.95f : 0.80f)
                : active ? new Color(0.86f, 0.16f, 0.14f, 0.92f) : new Color(0.02f, 0.06f, 0.12f, 0.58f);
        }

        if (text != null)
        {
            text.color = active ? Color.white : new Color(1f, 1f, 1f, 0.92f);
        }
    }

    private void SetTitle(string value)
    {
        if (settingsTitleText != null)
        {
            settingsTitleText.text = value;
        }
    }

    private void SetValueText(TextMeshProUGUI text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    private void SetTextSpeedValue(float value)
    {
        SetValueText(textSpeedValueText, PreferencesFormatting.TextSpeed(value));
    }

    private void SetAutoForwardDelayValue(float value)
    {
        SetValueText(autoForwardDelayValueText, PreferencesFormatting.AutoForwardDelay(value));
    }

    private string GetNextValue(string[] options, string current)
    {
        if (options == null || options.Length == 0)
        {
            return current;
        }

        int index = System.Array.IndexOf(options, current);
        if (index < 0)
        {
            return options[0];
        }

        return options[(index + 1) % options.Length];
    }
}
