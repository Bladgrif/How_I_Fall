using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public sealed class MainMenuController : MonoBehaviour
{
    private const float NotificationDurationSeconds = 2f;

    public SettingsPanelController settingsPanel;
    public ManualSaveLoadPanel manualSaveLoadPanel;
    public DialogueSceneRegistry dialogueRegistry;
    public Button continueButton;

    [SerializeField] private GameObject aboutPanel;
    [SerializeField] private GameObject helpPanel;
    [SerializeField] private TextMeshProUGUI helpText;
    [SerializeField] private GameObject exitConfirmPanel;
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField] private GameObject notificationPanel;
    [SerializeField] private GameObject galleryPanel;
    [SerializeField] private Button galleryReplayButton;
    [SerializeField] private TextMeshProUGUI galleryReplayTitle;
    [SerializeField] private TextMeshProUGUI galleryReplayState;
    [SerializeField] private GameObject galleryLockedOverlay;
    [SerializeField] private List<ReplayEntryDefinition> replayEntries = new List<ReplayEntryDefinition>();

    private Coroutine notificationCoroutine;

    public TextMeshProUGUI HelpText => helpText;
    public GameObject GalleryPanel => galleryPanel;
    public Button GalleryReplayButton => galleryReplayButton;
    public TextMeshProUGUI GalleryReplayTitle => galleryReplayTitle;
    public TextMeshProUGUI GalleryReplayState => galleryReplayState;
    public GameObject GalleryLockedOverlay => galleryLockedOverlay;
    public IReadOnlyList<ReplayEntryDefinition> ReplayEntries => replayEntries;

    private void Start()
    {
        RefreshHelpText();
        SaveManager.EnsureInstance(dialogueRegistry);
        RefreshContinueAvailability();
        RefreshGallery();
    }

    public void StartGame()
    {
        if (RejectActiveReplay("New Game"))
        {
            return;
        }

        SceneFlowManager.EnsureInstance().StartNewGame();
    }

    public void ContinueFromLatestSave()
    {
        if (RejectActiveReplay("Continue"))
        {
            return;
        }

        SaveManager saveManager = SaveManager.EnsureInstance(dialogueRegistry);
        if (!saveManager.LoadLatest())
        {
            ShowNotification("Нет совместимых сохранений");
            RefreshContinueAvailability();
        }
    }

    public void OpenManualLoad()
    {
        if (RejectActiveReplay("Load"))
        {
            return;
        }

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
        RefreshHelpText();

        if (helpPanel != null)
        {
            helpPanel.SetActive(true);
        }
    }

    private void RefreshHelpText()
    {
        if (helpText != null)
        {
            helpText.text = VNInputMap.BuildHelpText();
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
        RefreshGallery();
        if (galleryPanel != null)
        {
            galleryPanel.SetActive(true);
        }
    }

    public void CloseGallery()
    {
        if (galleryPanel != null)
        {
            galleryPanel.SetActive(false);
        }
    }

    public void StartTestReplay()
    {
        if (replayEntries == null || replayEntries.Count != 1 || replayEntries[0] == null)
        {
            ShowNotification("TEST REPLAY is unavailable");
            return;
        }

        ReplayEntryDefinition definition = replayEntries[0];
        if (!ReplayUnlockRegistry.Default.IsUnlocked(definition.replayId))
        {
            ShowNotification("TEST REPLAY is locked");
            RefreshGallery();
            return;
        }

        SceneFlowManager flow = SceneFlowManager.EnsureInstance();
        if (!flow.TryStartReplay(definition, replayEntries, dialogueRegistry, out string error))
        {
            ShowNotification(string.IsNullOrEmpty(error) ? "TEST REPLAY could not start" : error);
        }
    }

    public void RefreshGallery()
    {
        ReplayEntryDefinition definition = replayEntries != null && replayEntries.Count == 1
            ? replayEntries[0]
            : null;
        bool valid = definition != null
            && SceneFlowManager.TryValidateReplayDefinition(definition, replayEntries, dialogueRegistry, out _);
        bool unlocked = valid && ReplayUnlockRegistry.Default.IsUnlocked(definition.replayId);

        if (galleryReplayTitle != null)
        {
            galleryReplayTitle.text = definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
                ? definition.displayName
                : "TEST REPLAY";
        }

        if (galleryReplayState != null)
        {
            galleryReplayState.text = unlocked
                ? "TECH DEMO ONLY - NOT CANON"
                : "LOCKED";
        }

        if (galleryLockedOverlay != null)
        {
            galleryLockedOverlay.SetActive(!unlocked);
        }

        if (galleryReplayButton != null)
        {
            galleryReplayButton.interactable = unlocked;
        }
    }

    private bool RejectActiveReplay(string operation)
    {
        if (!SceneFlowManager.IsReplayModeActive)
        {
            return false;
        }

        Debug.LogWarning($"[REPLAY] Main Menu {operation} was denied while replay cleanup is pending.", this);
        ShowNotification("End Replay before continuing");
        return true;
    }

    public void ExitGame()
    {
        SceneFlowManager.EnsureInstance().QuitGame();
    }
}
