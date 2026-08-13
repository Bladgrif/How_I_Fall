using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public sealed class MainMenuController : MonoBehaviour
{
    private const float NotificationDurationSeconds = 2f;
    private static readonly string[] TargetActionRoutes =
    {
        nameof(ContinueFromLatestSave),
        nameof(StartGame),
        nameof(OpenManualLoad),
        nameof(OpenSettings),
        nameof(OpenHelp),
        nameof(OpenAbout),
        nameof(OpenExitConfirm)
    };

    private static readonly string[] TargetActionLabels =
    {
        "Продолжить",
        "Новая игра",
        "Загрузить",
        "Настройки",
        "Помощь",
        "Об игре",
        "Выйти"
    };

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
    private readonly List<Button> playerFacingActionButtons = new List<Button>();

    public TextMeshProUGUI HelpText => helpText;
    public GameObject GalleryPanel => galleryPanel;
    public Button GalleryReplayButton => galleryReplayButton;
    public TextMeshProUGUI GalleryReplayTitle => galleryReplayTitle;
    public TextMeshProUGUI GalleryReplayState => galleryReplayState;
    public GameObject GalleryLockedOverlay => galleryLockedOverlay;
    public IReadOnlyList<ReplayEntryDefinition> ReplayEntries => replayEntries;
    public static IReadOnlyList<string> TargetPlayerFacingActionRoutes => TargetActionRoutes;
    public IReadOnlyList<Button> PlayerFacingActionButtons => playerFacingActionButtons;

    private void Awake()
    {
        ApplyPlayerFacingPresentation();
    }

    private void Start()
    {
        RefreshHelpText();
        SaveManager.EnsureInstance(dialogueRegistry);
        RefreshContinueAvailability();
        RefreshGallery();
    }

    /// <summary>
    /// Reuses the authored MainMenu scene wiring while presenting only the Phase 4 action set.
    /// No serialized scene mutation or replacement menu hierarchy is required.
    /// </summary>
    public bool ApplyPlayerFacingPresentation()
    {
        Button[] orderedButtons =
        {
            FindPersistentButton(this, nameof(ContinueFromLatestSave)) ?? continueButton,
            FindPersistentButton(this, nameof(StartGame)),
            FindPersistentButton(this, nameof(OpenManualLoad)) ?? FindPersistentButton(manualSaveLoadPanel, nameof(ManualSaveLoadPanel.OpenLoad)),
            FindPersistentButton(this, nameof(OpenSettings)),
            FindPersistentButton(this, nameof(OpenHelp)),
            FindPersistentButton(this, nameof(OpenAbout)),
            FindPersistentButton(this, nameof(OpenExitConfirm))
        };

        if (orderedButtons.Any(button => button == null))
        {
            Debug.LogError("[MAIN MENU] SCENE WIRING BLOCKER: one or more Phase 4 action buttons could not be resolved from existing persistent routes.", this);
            return false;
        }

        Transform[] orderedRows = orderedButtons.Select(button => button.transform.parent).ToArray();
        Transform menuContent = orderedRows[0] != null ? orderedRows[0].parent : null;
        if (menuContent == null || orderedRows.Any(row => row == null || row.parent != menuContent))
        {
            Debug.LogError("[MAIN MENU] SCENE WIRING BLOCKER: Phase 4 action rows do not share the authored Menu Content parent.", this);
            return false;
        }

        List<Transform> currentRows = orderedRows.OrderBy(row => row.GetSiblingIndex()).ToList();
        List<RectLayoutSnapshot> visualSlots = currentRows
            .Select(row => new RectLayoutSnapshot(row as RectTransform))
            .ToList();
        for (int index = 0; index < orderedRows.Length; index++)
        {
            visualSlots[index].Apply(orderedRows[index] as RectTransform);
            SetButtonLabel(orderedButtons[index], TargetActionLabels[index]);
        }

        List<Transform> separators = new List<Transform>();
        for (int index = 0; index < menuContent.childCount; index++)
        {
            Transform child = menuContent.GetChild(index);
            if (!orderedRows.Contains(child))
            {
                separators.Add(child);
            }
        }

        var finalHierarchy = new List<Transform>();
        for (int index = 0; index < orderedRows.Length; index++)
        {
            finalHierarchy.Add(orderedRows[index]);
            if (index < orderedRows.Length - 1 && index < separators.Count)
            {
                finalHierarchy.Add(separators[index]);
            }
        }

        foreach (Transform child in finalHierarchy)
        {
            child.SetSiblingIndex(finalHierarchy.IndexOf(child));
        }

        Button galleryEntry = FindPersistentButton(this, nameof(OpenGallery));
        if (galleryEntry != null && galleryEntry.transform.parent != null && galleryEntry.transform.parent.parent == menuContent)
        {
            galleryEntry.transform.parent.gameObject.SetActive(false);
        }

        playerFacingActionButtons.Clear();
        playerFacingActionButtons.AddRange(orderedButtons);
        return true;
    }

    private Button FindPersistentButton(Object target, string methodName)
    {
        if (target == null)
        {
            return null;
        }

        Canvas canvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        Button[] candidates = canvas != null
            ? canvas.GetComponentsInChildren<Button>(true)
            : FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button button in candidates)
        {
            int count = button.onClick.GetPersistentEventCount();
            for (int index = 0; index < count; index++)
            {
                if (button.onClick.GetPersistentTarget(index) == target
                    && button.onClick.GetPersistentMethodName(index) == methodName)
                {
                    return button;
                }
            }
        }

        return null;
    }

    private static void SetButtonLabel(Button button, string value)
    {
        TextMeshProUGUI label = button != null
            ? button.GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(text => text.gameObject.name == "Text")
                ?? button.GetComponentInChildren<TextMeshProUGUI>(true)
            : null;
        if (label != null)
        {
            label.text = value;
            return;
        }

        Text legacyLabel = button != null
            ? button.GetComponentsInChildren<Text>(true)
                .FirstOrDefault(text => text.gameObject.name == "Text")
                ?? button.GetComponentInChildren<Text>(true)
            : null;
        if (legacyLabel != null)
        {
            legacyLabel.text = value;
        }
    }

    private readonly struct RectLayoutSnapshot
    {
        private readonly Vector2 anchorMin;
        private readonly Vector2 anchorMax;
        private readonly Vector2 anchoredPosition;
        private readonly Vector2 sizeDelta;
        private readonly Vector2 pivot;

        public RectLayoutSnapshot(RectTransform source)
        {
            anchorMin = source != null ? source.anchorMin : Vector2.zero;
            anchorMax = source != null ? source.anchorMax : Vector2.zero;
            anchoredPosition = source != null ? source.anchoredPosition : Vector2.zero;
            sizeDelta = source != null ? source.sizeDelta : Vector2.zero;
            pivot = source != null ? source.pivot : Vector2.zero;
        }

        public void Apply(RectTransform target)
        {
            if (target == null)
            {
                return;
            }

            target.anchorMin = anchorMin;
            target.anchorMax = anchorMax;
            target.anchoredPosition = anchoredPosition;
            target.sizeDelta = sizeDelta;
            target.pivot = pivot;
        }
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
