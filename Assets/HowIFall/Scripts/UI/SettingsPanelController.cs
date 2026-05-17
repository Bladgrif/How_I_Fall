using UnityEngine;
using UnityEngine.UI;

public class SettingsPanelController : MonoBehaviour
{
    public GameObject root;
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;
    public Slider textSpeedSlider;
    public Toggle fullscreenToggle;
    public GameObject[] objectsToHideWhenOpen;

    private void Awake()
    {
        if (root == null)
        {
            root = gameObject;
        }
    }

    public void Show()
    {
        if (root == null)
        {
            return;
        }

        SetHiddenObjectsActive(false);
        root.SetActive(true);
        RefreshUi();
    }

    public void Hide()
    {
        if (root == null)
        {
            return;
        }

        root.SetActive(false);
        SetHiddenObjectsActive(true);
    }

    public void RefreshUi()
    {
        if (SettingsManager.Instance == null)
        {
            return;
        }

        GameSettings settings = SettingsManager.Instance.settings;

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

        if (textSpeedSlider != null)
        {
            textSpeedSlider.SetValueWithoutNotify(settings.textSpeed);
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(settings.fullscreen);
        }
    }

    public void OnMasterVolumeChanged(float value)
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetMasterVolume(value);
        }
    }

    public void OnMusicVolumeChanged(float value)
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetMusicVolume(value);
        }
    }

    public void OnSfxVolumeChanged(float value)
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetSfxVolume(value);
        }
    }

    public void OnTextSpeedChanged(float value)
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetTextSpeed(value);
        }
    }

    public void OnFullscreenChanged(bool value)
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetFullscreen(value);
        }
    }

    public void OnResetClicked()
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.ResetSettings();
            RefreshUi();
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
}
