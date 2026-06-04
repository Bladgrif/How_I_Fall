using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    private const float NotificationDurationSeconds = 2f;

    public SettingsPanelController settingsPanel;

    [SerializeField] private GameObject aboutPanel;
    [SerializeField] private GameObject helpPanel;
    [SerializeField] private GameObject exitConfirmPanel;
    [SerializeField] private GameObject loadPanel;
    [SerializeField] private TextMeshProUGUI loadSaveTitleText;
    [SerializeField] private TextMeshProUGUI loadSaveMetaText;
    [SerializeField] private TextMeshProUGUI loadSavePreviewText;
    [SerializeField] private Button loadSaveButton;
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField] private GameObject notificationPanel;

    private Coroutine notificationCoroutine;

    public void StartGame()
    {
        SceneFlowManager.EnsureInstance().StartNewGame();
    }

    public void ContinueGame()
    {
        if (SaveManager.Instance != null && SaveManager.Instance.HasSave())
        {
            SceneFlowManager.EnsureInstance().ContinueGame();
            return;
        }

        ShowNotification("Нет сохранения");
        Debug.LogWarning("No save file found.");
    }

    public void OpenLoadPanel()
    {
        if (loadPanel == null)
        {
            ContinueGame();
            return;
        }

        RefreshLoadPanel();
        loadPanel.SetActive(true);
    }

    public void CloseLoadPanel()
    {
        if (loadPanel != null)
        {
            loadPanel.SetActive(false);
        }
    }

    private void RefreshLoadPanel()
    {
        bool hasSave = SaveManager.Instance != null && SaveManager.Instance.HasSave();
        SaveData saveInfo = hasSave ? SaveManager.Instance.GetSaveInfo() : null;

        if (loadSaveButton != null)
        {
            loadSaveButton.interactable = hasSave && saveInfo != null;
        }

        if (!hasSave || saveInfo == null)
        {
            SetLoadSaveText(
                "Сохранение не найдено",
                string.Empty,
                "Начните новую игру, чтобы создать quick save.");
            return;
        }

        SetLoadSaveText(
            string.IsNullOrEmpty(saveInfo.sceneTitle) ? "Quick Save" : saveInfo.sceneTitle,
            string.IsNullOrEmpty(saveInfo.savedAt) ? string.Empty : saveInfo.savedAt,
            string.IsNullOrEmpty(saveInfo.linePreview) ? "Без превью" : saveInfo.linePreview);
    }

    private void SetLoadSaveText(string title, string meta, string preview)
    {
        if (loadSaveTitleText != null)
        {
            loadSaveTitleText.text = title;
        }

        if (loadSaveMetaText != null)
        {
            loadSaveMetaText.text = meta;
        }

        if (loadSavePreviewText != null)
        {
            loadSavePreviewText.text = preview;
        }
    }

    public void LoadSelectedSave()
    {
        if (SaveManager.Instance != null && SaveManager.Instance.HasSave())
        {
            SceneFlowManager.EnsureInstance().ContinueGame();
            return;
        }

        ShowNotification("Нет сохранения");
    }

    public void DeleteSave()
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogWarning("SaveManager.Instance is missing.");
            return;
        }

        SaveManager.Instance.DeleteSave();
    }

    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.Show();
            return;
        }

        Debug.LogWarning("SettingsPanelController is not assigned.");
    }

    public void OpenAbout()
    {
        if (aboutPanel != null)
        {
            aboutPanel.SetActive(true);
        }
    }

    public void OpenHelp()
    {
        if (helpPanel != null)
        {
            helpPanel.SetActive(true);
        }
    }

    public void CloseAbout()
    {
        if (aboutPanel != null)
        {
            aboutPanel.SetActive(false);
        }
    }

    public void CloseHelp()
    {
        if (helpPanel != null)
        {
            helpPanel.SetActive(false);
        }
    }

    public void OpenExitConfirm()
    {
        if (exitConfirmPanel != null)
        {
            exitConfirmPanel.SetActive(true);
            return;
        }

        ExitGame();
    }

    public void CloseExitConfirm()
    {
        if (exitConfirmPanel != null)
        {
            exitConfirmPanel.SetActive(false);
        }
    }

    public void ConfirmExit()
    {
        CloseExitConfirm();
        ExitGame();
    }

    public void ShowNotification(string message)
    {
        if (notificationText != null)
        {
            notificationText.text = message;
        }

        if (notificationPanel == null)
        {
            return;
        }

        notificationPanel.SetActive(true);

        if (notificationCoroutine != null)
        {
            StopCoroutine(notificationCoroutine);
        }

        notificationCoroutine = StartCoroutine(HideNotificationAfterDelay());
    }

    private IEnumerator HideNotificationAfterDelay()
    {
        yield return new WaitForSeconds(NotificationDurationSeconds);

        if (notificationPanel != null)
        {
            notificationPanel.SetActive(false);
        }

        notificationCoroutine = null;
    }

    public void OpenGallery()
    {
        Debug.Log("Gallery is not implemented yet");
    }

    public void ExitGame()
    {
        SceneFlowManager.EnsureInstance().QuitGame();
    }
}
