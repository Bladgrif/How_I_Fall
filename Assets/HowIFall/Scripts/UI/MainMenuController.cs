using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    private const float NotificationDurationSeconds = 2f;

    public SettingsPanelController settingsPanel;
    public ManualSaveLoadPanel manualSaveLoadPanel;
    public DialogueSceneRegistry dialogueRegistry;
    public Button continueButton;

    [SerializeField] private GameObject aboutPanel;
    [SerializeField] private GameObject helpPanel;
    [SerializeField] private GameObject exitConfirmPanel;
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField] private GameObject notificationPanel;

    private Coroutine notificationCoroutine;

    private void Start()
    {
        SaveManager.EnsureInstance(dialogueRegistry);
        RefreshContinueAvailability();
    }

    public void StartGame()
    {
        SceneFlowManager.EnsureInstance().StartNewGame();
    }

    public void ContinueFromLatestSave()
    {
        SaveManager saveManager = SaveManager.EnsureInstance(dialogueRegistry);
        if (!saveManager.LoadLatest())
        {
            ShowNotification("Нет совместимых сохранений");
            RefreshContinueAvailability();
        }
    }

    public void OpenManualLoad()
    {
        if (manualSaveLoadPanel == null)
        {
            ShowNotification("Экран загрузки недоступен");
            Debug.LogError("[LOAD UI] ManualSaveLoadPanel is not assigned.", this);
            return;
        }

        SaveManager.EnsureInstance(dialogueRegistry);
        manualSaveLoadPanel.OpenLoad();
    }

    public void RefreshContinueAvailability()
    {
        if (continueButton == null)
        {
            return;
        }

        SaveManager saveManager = SaveManager.EnsureInstance(dialogueRegistry);
        continueButton.interactable = saveManager.HasAnyValidSave();
    }

    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.Show();
            return;
        }

        Debug.LogWarning("SettingsPanelController is not assigned.", this);
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
            Debug.Log(message, this);
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
        Debug.Log("Gallery is not implemented yet", this);
    }

    public void ExitGame()
    {
        SceneFlowManager.EnsureInstance().QuitGame();
    }
}
