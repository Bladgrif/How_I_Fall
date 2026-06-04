using System.Collections;
using TMPro;
using UnityEngine;

public class MainMenuController : MonoBehaviour
{
    private const float NotificationDurationSeconds = 2f;

    public SettingsPanelController settingsPanel;

    [SerializeField] private GameObject aboutPanel;
    [SerializeField] private GameObject helpPanel;
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
