using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsPanelController : MonoBehaviour
{
    public GameObject root;
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
        ShowAudioTab();
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

        if (ambientVolumeSlider != null)
        {
            ambientVolumeSlider.SetValueWithoutNotify(settings.ambientVolume);
        }

        if (musicDuringPauseToggle != null)
        {
            musicDuringPauseToggle.SetIsOnWithoutNotify(settings.musicDuringPause);
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

    public void OnAmbientVolumeChanged(float value)
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetAmbientVolume(value);
        }
    }

    public void OnMusicDuringPauseChanged(bool value)
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.SetMusicDuringPause(value);
        }
    }

    public void ShowVideoTab()
    {
        SetActiveTab(videoContent, videoTabImage, videoTabText);
        Debug.Log("TODO: Settings video tab");
    }

    public void ShowAudioTab()
    {
        SetActiveTab(audioContent, audioTabImage, audioTabText);
    }

    public void ShowGameTab()
    {
        SetActiveTab(gameContent, gameTabImage, gameTabText);
        Debug.Log("TODO: Settings game tab");
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
}
