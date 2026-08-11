using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VNDialogueController : MonoBehaviour
{
    public static VNDialogueController Instance { get; private set; }

    private const string MissingSceneDataText = "Dialogue scene data is missing.";
    private const float SkipCadenceSeconds = 0.12f;
    private const string EndPrototypeText = "\u041a\u043e\u043d\u0435\u0446 Unity-\u043f\u0440\u043e\u0442\u043e\u0442\u0438\u043f\u0430.";
    private const string ChoiceConfigurationErrorText = "\u0418\u0441\u0442\u043e\u0440\u0438\u044f \u043d\u0435 \u043c\u043e\u0436\u0435\u0442 \u0431\u044b\u0442\u044c \u043f\u0440\u043e\u0434\u043e\u043b\u0436\u0435\u043d\u0430.";
    public const int SupportedChoiceButtonCapacity = 3;

    public DialogueSceneData sceneData;
    public DialogueSceneRegistry sceneRegistry;

    public TextMeshProUGUI speakerText;
    public TextMeshProUGUI dialogueText;
    public Image backgroundImage;
    public Image characterImage;
    public GameObject nameBox;
    public Button nextButton;
    public GameObject dialogueUiRoot;
    public GameObject choiceDimOverlay;
    public GameObject choicePanel;
    public Button choiceMashaButton;
    public Button choiceArtemButton;
    public Button choiceLeraButton;
    public GameObject backlogDimOverlay;
    public GameObject backlogPanel;
    public TextMeshProUGUI backlogText;
    public Button backlogCloseButton;
    public GameObject notificationPanel;
    public TextMeshProUGUI notificationText;
    public float notificationDuration = 1.5f;
    public GameObject confirmExitPanel;
    public Button confirmExitYesButton;
    public Button confirmExitNoButton;
    public GameObject vnSettingsDimOverlay;
    public GameObject vnSettingsPanel;
    public Slider vnMasterVolumeSlider;
    public Slider vnMusicVolumeSlider;
    public Slider vnSfxVolumeSlider;
    public Slider vnTextSpeedSlider;
    public Toggle vnAutoForwardToggle;
    public Slider vnAutoForwardDelaySlider;
    public Toggle vnFullscreenToggle;
    public Button vnSettingsCloseButton;
    public Button vnSettingsResetButton;
    public ManualSaveLoadPanel manualSaveLoadPanel;
    public CharacterHubController characterHubController;
    public float baseCharactersPerSecond = 45f;

    public Vector2 characterLeftPosition = new Vector2(-420f, -220f);
    public Vector2 characterCenterPosition = new Vector2(0f, -220f);
    public Vector2 characterRightPosition = new Vector2(420f, -220f);
    public Vector2 characterSoloPosition = new Vector2(-140f, -220f);
    public Vector2 characterDefaultSize = new Vector2(850f, 1200f);

    private int currentLineIndex;
    private bool showingChoice;
    private bool showingFinalLine;
    private bool showingEndLine;
    private string finalLineText;
    private List<DialogueLine> activeLines;
    private List<DialogueChoice> activeChoices;
    private readonly List<VisibleChoice> visibleChoices = new List<VisibleChoice>();
    private Button[] choiceButtons;
    private DialogueSceneData pendingNextScene;
    private Coroutine typingCoroutine;
    private Coroutine autoForwardCoroutine;
    private Coroutine skipCoroutine;
    private Coroutine notificationCoroutine;
    private string currentFullText = string.Empty;
    private bool isTyping;
    private bool quickSaveInProgress;
    private bool autoSaveInProgress;
    private bool pendingAutoSave;
    private bool preLoadAutoSavePending;
    private System.Action<bool> preLoadAutoSaveCompletion;
    private readonly DialogueBacklog backlog = new DialogueBacklog(DialogueBacklog.DefaultCapacity);
    private int backlogCaptureSuppressionDepth;
    private VNSettingsPresenter settingsPresenter;
    private bool observedAutoForward;
    private bool skipEnabled;
    private DialogueReadHistory readHistory;
    private DialogueSceneData displayedLineScene;
    private DialogueLine displayedLine;
    private SpecialModeCoordinator specialModeCoordinator;
    private bool specialModeWasActive;
    private bool isInterfaceHidden;

    public bool IsInterfaceHidden => isInterfaceHidden;
    public bool HasActiveSpecialMode => specialModeCoordinator != null && specialModeCoordinator.HasActiveOwner;
    public bool IsCharacterHubOpen => characterHubController != null && characterHubController.IsOpen;
    public bool CanAdvanceDialogue => !IsCharacterHubOpen && !isInterfaceHidden && (specialModeCoordinator == null || !specialModeCoordinator.IsDialogueAdvanceBlocked);
    public bool CanSave => !IsCharacterHubOpen && !SceneFlowManager.IsReplayModeActive && !isInterfaceHidden && (specialModeCoordinator == null || specialModeCoordinator.CanSave);
    public bool CanLoad => !IsCharacterHubOpen && !SceneFlowManager.IsReplayModeActive && !isInterfaceHidden && (specialModeCoordinator == null || specialModeCoordinator.CanLoad);
    public bool CanOpenQuickMenu => !IsCharacterHubOpen && !isInterfaceHidden && (specialModeCoordinator == null || specialModeCoordinator.CanOpenQuickMenu);
    public bool CanOpenBacklog => !IsCharacterHubOpen && !isInterfaceHidden && (specialModeCoordinator == null || specialModeCoordinator.CanOpenBacklog);
    public bool CanOpenSettings => !IsCharacterHubOpen && !isInterfaceHidden && (specialModeCoordinator == null || specialModeCoordinator.CanOpenSettings);
    public bool CanReturnToMainMenu => !IsCharacterHubOpen && !isInterfaceHidden && (specialModeCoordinator == null || specialModeCoordinator.CanReturnToMainMenu);
    public bool CanOpenCharacterHub => characterHubController != null
        && !SceneFlowManager.IsReplayModeActive
        && !isInterfaceHidden
        && !showingChoice
        && !IsCharacterHubOpen
        && (specialModeCoordinator == null || !specialModeCoordinator.HasActiveOwner)
        && !IsAnyOrdinaryModalOpen();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"[VN] Duplicate VNDialogueController detected on '{gameObject.name}'.", this);
            enabled = false;
            return;
        }

        Instance = this;
        specialModeCoordinator = new SpecialModeCoordinator(GetSpecialModeEntryBlockerReason, message => Debug.LogWarning(message, this));
        EnsureReadHistory();
    }

    private void Start()
    {
        if (!ValidateRequiredUiReferences())
        {
            enabled = false;
            if (SceneFlowManager.IsReplayModeActive)
            {
                SceneFlowManager.Instance.FailReplay("VN replay host is missing required UI references.");
            }
            return;
        }

        GameState gameState = GameState.EnsureInstance();
        characterHubController = CharacterHubController.TryCreateRuntime(this);
        SaveManager saveManager = SaveManager.EnsureInstance(sceneRegistry);
        Debug.Log($"[VN] Start. sceneId='{gameState.currentSceneId}', lineId='{gameState.currentLineId}', lineIndex={gameState.currentLineIndex}, sceneData='{(sceneData != null ? sceneData.sceneId : "<null>")}'.", this);

        choiceButtons = new[] { choiceMashaButton, choiceArtemButton, choiceLeraButton };

        if (backlogPanel != null)
        {
            backlogPanel.SetActive(false);
        }

        SetBacklogOverlayActive(false);

        if (notificationPanel != null)
        {
            notificationPanel.SetActive(false);
        }

        if (confirmExitPanel != null)
        {
            confirmExitPanel.SetActive(false);
        }

        settingsPresenter = new VNSettingsPresenter(
            vnSettingsDimOverlay,
            vnSettingsPanel,
            vnMasterVolumeSlider,
            vnMusicVolumeSlider,
            vnSfxVolumeSlider,
            vnTextSpeedSlider,
            vnAutoForwardToggle,
            vnAutoForwardDelaySlider,
            vnFullscreenToggle,
            vnSettingsCloseButton,
            vnSettingsResetButton,
            new VNSettingsService(),
            ShowToast,
            this);
        settingsPresenter.Initialize();
        observedAutoForward = IsAutoForwardEnabled();

        if (backlogCloseButton != null)
        {
            backlogCloseButton.onClick.AddListener(HideBacklog);
        }

        if (confirmExitYesButton != null)
        {
            confirmExitYesButton.onClick.AddListener(ConfirmReturnToMainMenu);
        }

        if (confirmExitNoButton != null)
        {
            confirmExitNoButton.onClick.AddListener(HideConfirmExit);
        }

        nextButton.onClick.AddListener(AdvanceDialogue);

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] == null)
            {
                Debug.LogWarning($"Choice button at index {i} is not assigned. This choice slot will be skipped.", this);
                continue;
            }

            int choiceIndex = i;
            choiceButtons[i].onClick.AddListener(() =>
            {
                Choose(choiceIndex);
            });
        }

        if (SceneFlowManager.IsReplayModeActive)
        {
            SceneFlowManager flow = SceneFlowManager.Instance;
            if (flow == null || !flow.TryGetReplayStartScene(out DialogueSceneData replayStartScene)
                || !IsRegisteredDialogueScene(replayStartScene))
            {
                flow?.FailReplay("VN replay start scene is missing, invalid, or unregistered.");
                return;
            }

            ClearBacklog();
            try
            {
                LoadDialogueScene(replayStartScene);
                flow.AttachReplayHost(this);
            }
            catch (System.Exception exception)
            {
                flow.FailReplay($"VN replay graph failed to start. {exception.Message}");
            }
            return;
        }

        if (saveManager.HasPendingSceneRestore)
        {
            int pendingSlotIndex = saveManager.PendingSlotIndex;
            saveManager.GetPendingBacklogRestore(
                out List<DialogueBacklogEntry> pendingBacklogSnapshot,
                out bool hasBacklogSnapshot);
            ReplaceBacklogFromSnapshot(pendingBacklogSnapshot);

            bool restored = false;
            try
            {
                restored = RestoreFromGameState(hasBacklogSnapshot);
            }
            catch (System.Exception exception)
            {
                Debug.LogError(
                    $"[LOAD] Pending restore for slot {pendingSlotIndex} threw {exception.GetType().Name}: {exception.Message}",
                    this);
            }

            if (restored)
            {
                saveManager.CompletePendingSceneRestore();
                return;
            }

            ClearBacklog();
            saveManager.FailPendingSceneRestoreAndReset();
            Debug.LogError(
                $"[LOAD] Pending restore for slot {pendingSlotIndex} failed in VNDialogueController.Start(). Loaded GameState was discarded, ResetState() was applied, and configured start scene '{(sceneData != null ? sceneData.sceneId : "<null>")}' will be started.",
                this);
            LoadDialogueScene(sceneData);
            return;
        }

        LoadDialogueScene(sceneData);
    }

    private void Update()
    {
        RefreshSpecialModeOwnerLifecycle();
        RefreshAutoForwardState();

        if (isInterfaceHidden)
        {
            if (VNInputMap.WasPressedThisFrame(VNInputAction.ToggleInterfaceVisibility)
                || VNInputMap.WasPressedThisFrame(VNInputAction.CloseOrCancel))
            {
                RestoreInterface();
            }

            return;
        }

        if (VNInputMap.WasPressedThisFrame(VNInputAction.ToggleInterfaceVisibility))
        {
            TryHideInterface();
            return;
        }

        if (VNInputMap.WasPressedThisFrame(VNInputAction.ToggleSkip))
        {
            ToggleSkip();
        }

        if (VNInputMap.WasPressedThisFrame(VNInputAction.OpenSave) && CanSave)
        {
            manualSaveLoadPanel?.OpenSave();
        }

        if (VNInputMap.WasPressedThisFrame(VNInputAction.QuickSave))
        {
            RequestQuickSave();
        }

        if (VNInputMap.WasPressedThisFrame(VNInputAction.QuickLoad))
        {
            RequestQuickLoad();
        }

        if (VNInputMap.WasPressedThisFrame(VNInputAction.OpenLoad) && CanLoad)
        {
            manualSaveLoadPanel?.OpenLoad();
        }

        if (VNInputMap.WasPressedThisFrame(VNInputAction.ShowBacklog))
        {
            ShowBacklog();
        }

        if (VNInputMap.WasPressedThisFrame(VNInputAction.CloseOrCancel))
        {
            if (IsCharacterHubOpen)
            {
                CloseCharacterHub();
                return;
            }

            if (HasActiveSpecialMode)
            {
                specialModeCoordinator.TryRequestEscapeCancel();
                return;
            }

            if (backlogPanel != null && backlogPanel.activeSelf)
            {
                HideBacklog();
                return;
            }

            if (confirmExitPanel != null && confirmExitPanel.activeSelf)
            {
                HideConfirmExit();
                return;
            }

            if (vnSettingsPanel != null && vnSettingsPanel.activeSelf)
            {
                HideSettings();
                return;
            }

            if (manualSaveLoadPanel != null && manualSaveLoadPanel.IsOpen)
            {
                // ManualSaveLoadPanel handles Escape itself so that an open
                // confirmation is cancelled before the whole panel closes.
                return;
            }
        }
    }

    public bool TryHideInterface()
    {
        if (isInterfaceHidden || IsHideInterfaceBlocked())
        {
            return false;
        }

        isInterfaceHidden = true;
        StopAutoForwardTimer();
        StopSkipTimer();
        if (dialogueUiRoot != null)
        {
            dialogueUiRoot.SetActive(false);
        }

        FindFirstObjectByType<VNQuickMenu>(FindObjectsInactive.Include)?.SetPlayerInterfaceHidden(true);
        return true;
    }

    public void RestoreInterface()
    {
        if (!isInterfaceHidden)
        {
            return;
        }

        isInterfaceHidden = false;
        if (dialogueUiRoot != null)
        {
            dialogueUiRoot.SetActive(true);
        }

        FindFirstObjectByType<VNQuickMenu>(FindObjectsInactive.Include)?.SetPlayerInterfaceHidden(false);
        StartAutoForwardDelayIfReady();
        StartSkipDelayIfReady();
    }

    private bool IsHideInterfaceBlocked()
    {
        return HasActiveSpecialMode
            || IsCharacterHubOpen
            || showingChoice
            || isTyping
            || quickSaveInProgress
            || autoSaveInProgress
            || preLoadAutoSavePending
            || (choicePanel != null && choicePanel.activeSelf)
            || (backlogPanel != null && backlogPanel.activeSelf)
            || (vnSettingsPanel != null && vnSettingsPanel.activeSelf)
            || (manualSaveLoadPanel != null && manualSaveLoadPanel.IsOpen)
            || (confirmExitPanel != null && confirmExitPanel.activeSelf)
            || (notificationPanel != null && notificationPanel.activeSelf);
    }

    public bool TryEnterSpecialMode(UnityEngine.Object owner, SpecialModePolicy policy, out SpecialModeLease lease)
    {
        if (specialModeCoordinator == null)
        {
            specialModeCoordinator = new SpecialModeCoordinator(GetSpecialModeEntryBlockerReason, message => Debug.LogWarning(message, this));
        }

        if (!specialModeCoordinator.TryEnter(owner, policy, out lease))
        {
            return false;
        }

        if (policy.BlocksAuto)
        {
            StopAutoForwardTimer();
        }

        if (policy.BlocksSkip)
        {
            StopSkipTimer();
        }

        specialModeWasActive = true;
        RefreshQuickMenuSpecialModeVisibility();
        return true;
    }

    public bool ExitSpecialMode(SpecialModeLease lease)
    {
        if (specialModeCoordinator == null || !specialModeCoordinator.Exit(lease))
        {
            return false;
        }

        specialModeWasActive = false;
        StartAutoForwardDelayIfReady();
        StartSkipDelayIfReady();
        RefreshQuickMenuSpecialModeVisibility();
        return true;
    }

    private void RefreshSpecialModeOwnerLifecycle()
    {
        if (!specialModeWasActive || HasActiveSpecialMode)
        {
            return;
        }

        specialModeWasActive = false;
        StartAutoForwardDelayIfReady();
        StartSkipDelayIfReady();
        RefreshQuickMenuSpecialModeVisibility();
    }

    private void RefreshQuickMenuSpecialModeVisibility()
    {
        FindFirstObjectByType<VNQuickMenu>(FindObjectsInactive.Include)?.RefreshSpecialModeVisibility();
    }

    private string GetSpecialModeEntryBlockerReason()
    {
        if (IsCharacterHubOpen)
        {
            return "character hub";
        }

        if (isInterfaceHidden)
        {
            return "hidden interface";
        }

        if (showingChoice || (choicePanel != null && choicePanel.activeSelf))
        {
            return "ordinary choice";
        }

        if (backlogPanel != null && backlogPanel.activeSelf)
        {
            return "backlog";
        }

        if (vnSettingsPanel != null && vnSettingsPanel.activeSelf)
        {
            return "VN settings";
        }

        if (manualSaveLoadPanel != null && manualSaveLoadPanel.IsOpen)
        {
            return "manual save/load";
        }

        return confirmExitPanel != null && confirmExitPanel.activeSelf ? "main menu confirmation" : null;
    }

    public bool OpenCharacterHub()
    {
        if (!CanOpenCharacterHub || !characterHubController.Open())
        {
            return false;
        }

        StopAutoForwardTimer();
        StopSkipTimer();
        return true;
    }

    public void CloseCharacterHub()
    {
        if (characterHubController == null || !characterHubController.Hide())
        {
            return;
        }

        StartAutoForwardDelayIfReady();
        StartSkipDelayIfReady();
    }

    private bool IsAnyOrdinaryModalOpen()
    {
        return (choicePanel != null && choicePanel.activeSelf)
            || (backlogPanel != null && backlogPanel.activeSelf)
            || (vnSettingsPanel != null && vnSettingsPanel.activeSelf)
            || (manualSaveLoadPanel != null && manualSaveLoadPanel.IsOpen)
            || (confirmExitPanel != null && confirmExitPanel.activeSelf);
    }

    public void RequestQuickSave()
    {
        if (RejectReplaySaveLoad("QUICK SAVE"))
        {
            return;
        }

        if (quickSaveInProgress)
        {
            return;
        }

        if (!IsActiveControllerInCurrentScene())
        {
            Debug.LogWarning("[QUICK SAVE] Request ignored because VNDialogueController is not active in the current scene.", this);
            return;
        }

        if (IsSystemSaveBlockedByModal())
        {
            return;
        }

        quickSaveInProgress = true;
        try
        {
            StartCoroutine(CaptureScreenshotAndSave(
                (manager, screenshot) => manager.SaveQuick(screenshot),
                "QUICK SAVE",
                CompleteQuickSave));
        }
        catch (System.Exception exception)
        {
            quickSaveInProgress = false;
            Debug.LogError($"[QUICK SAVE] Could not start quick-save coroutine. {exception.Message}", this);
            ShowToast("РќРµ СѓРґР°Р»РѕСЃСЊ СЃРѕР·РґР°С‚СЊ Р±С‹СЃС‚СЂРѕРµ СЃРѕС…СЂР°РЅРµРЅРёРµ");
        }
    }

    public void RequestQuickLoad()
    {
        if (RejectReplaySaveLoad("QUICK LOAD"))
        {
            return;
        }

        if (!IsActiveControllerInCurrentScene())
        {
            Debug.LogWarning("[QUICK LOAD] Request ignored because VNDialogueController is not active in the current scene.", this);
            return;
        }

        if (IsSystemLoadBlockedByModal())
        {
            return;
        }

        if (manualSaveLoadPanel == null || !manualSaveLoadPanel.RequestQuickLoad())
        {
            ShowToast("\u041d\u0435\u0442 \u0434\u043e\u0441\u0442\u0443\u043f\u043d\u044b\u0445 \u0431\u044b\u0441\u0442\u0440\u044b\u0445 \u0441\u043e\u0445\u0440\u0430\u043d\u0435\u043d\u0438\u0439");
        }
    }

    public void RequestAutoSave()
    {
        if (RejectReplaySaveLoad("AUTO SAVE"))
        {
            return;
        }

        if (!IsActiveControllerInCurrentScene())
        {
            Debug.LogWarning("[AUTO SAVE] Request ignored because VNDialogueController is not active in the current scene.", this);
            return;
        }

        if (IsSystemSaveBlockedByModal()
            || (SettingsManager.Instance != null
                && SettingsManager.Instance.settings != null
                && !SettingsManager.Instance.settings.autoSave))
        {
            return;
        }

        if (autoSaveInProgress)
        {
            pendingAutoSave = true;
            return;
        }

        autoSaveInProgress = true;
        try
        {
            StartCoroutine(CaptureScreenshotAndSave(
                (manager, screenshot) => manager.SaveAuto(screenshot),
                "AUTO SAVE",
                CompleteAutoSave));
        }
        catch (System.Exception exception)
        {
            autoSaveInProgress = false;
            Debug.LogError($"[AUTO SAVE] Could not start autosave coroutine. {exception.Message}", this);
            RunPendingAutoSaveIfNeeded();
        }
    }

    public void RequestPreLoadAutoSave(System.Action<bool> onCompleted)
    {
        if (RejectReplaySaveLoad("PRE-LOAD AUTO SAVE"))
        {
            onCompleted?.Invoke(false);
            return;
        }

        if (!IsActiveControllerInCurrentScene())
        {
            Debug.LogWarning("[PRE-LOAD AUTO SAVE] Request ignored because VNDialogueController is not active in the current scene.", this);
            onCompleted?.Invoke(false);
            return;
        }

        if (!CanLoad)
        {
            onCompleted?.Invoke(false);
            return;
        }

        if (preLoadAutoSavePending)
        {
            Debug.LogWarning("[PRE-LOAD AUTO SAVE] Request ignored because another pre-load checkpoint is already pending.", this);
            onCompleted?.Invoke(false);
            return;
        }

        // A confirmed load takes priority over a deferred ordinary checkpoint: it must
        // be preceded by exactly its own capture, not an unbounded autosave queue.
        pendingAutoSave = false;
        preLoadAutoSavePending = true;
        preLoadAutoSaveCompletion = onCompleted;

        if (!autoSaveInProgress)
        {
            StartPreLoadAutoSave();
        }
    }

    private void StartPreLoadAutoSave()
    {
        if (!preLoadAutoSavePending || autoSaveInProgress)
        {
            return;
        }

        autoSaveInProgress = true;
        try
        {
            StartCoroutine(CaptureScreenshotAndSave(
                (manager, screenshot) => manager.SaveAuto(screenshot),
                "PRE-LOAD AUTO SAVE",
                CompletePreLoadAutoSave));
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[PRE-LOAD AUTO SAVE] Could not start autosave coroutine. {exception.Message}", this);
            CompletePreLoadAutoSave(false);
        }
    }

    private IEnumerator CaptureScreenshotAndSave(
        System.Func<SaveManager, Texture2D, bool> saveOperation,
        string logPrefix,
        System.Action<bool> onCompleted)
    {
        Texture2D screenshot = null;
        bool saved = false;

        try
        {
            yield return new WaitForEndOfFrame();

            try
            {
                screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                if (screenshot == null)
                {
                    Debug.LogError($"[{logPrefix}] ScreenCapture returned no screenshot.", this);
                }
                else if (SaveManager.Instance == null)
                {
                    Debug.LogError($"[{logPrefix}] SaveManager.Instance is missing.", this);
                }
                else
                {
                    saved = saveOperation != null && saveOperation(SaveManager.Instance, screenshot);
                    if (!saved)
                    {
                        Debug.LogError($"[{logPrefix}] Save operation returned false.", this);
                    }
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[{logPrefix}] Save failed. {exception.Message}", this);
            }
        }
        finally
        {
            if (screenshot != null)
            {
                Destroy(screenshot);
            }

            onCompleted?.Invoke(saved);
        }
    }

    private void CompleteQuickSave(bool saved)
    {
        quickSaveInProgress = false;
        ShowToast(saved
            ? "\u0411\u044b\u0441\u0442\u0440\u043e\u0435 \u0441\u043e\u0445\u0440\u0430\u043d\u0435\u043d\u0438\u0435 \u0441\u043e\u0437\u0434\u0430\u043d\u043e"
            : "\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u0441\u043e\u0437\u0434\u0430\u0442\u044c \u0431\u044b\u0441\u0442\u0440\u043e\u0435 \u0441\u043e\u0445\u0440\u0430\u043d\u0435\u043d\u0438\u0435");
    }

    private void CompleteAutoSave(bool saved)
    {
        autoSaveInProgress = false;
        if (preLoadAutoSavePending)
        {
            StartPreLoadAutoSave();
            return;
        }

        RunPendingAutoSaveIfNeeded();
    }

    private void CompletePreLoadAutoSave(bool saved)
    {
        autoSaveInProgress = false;
        preLoadAutoSavePending = false;
        System.Action<bool> onCompleted = preLoadAutoSaveCompletion;
        preLoadAutoSaveCompletion = null;
        onCompleted?.Invoke(saved);
    }

    private void RunPendingAutoSaveIfNeeded()
    {
        if (preLoadAutoSavePending)
        {
            StartPreLoadAutoSave();
            return;
        }

        if (!pendingAutoSave)
        {
            return;
        }

        pendingAutoSave = false;
        RequestAutoSave();
    }

    private bool IsActiveControllerInCurrentScene()
    {
        return Instance == this
            && isActiveAndEnabled
            && gameObject.activeInHierarchy
            && gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene();
    }

    private bool IsSystemSaveBlockedByModal()
    {
        return !CanSave
            || (manualSaveLoadPanel != null && manualSaveLoadPanel.IsOpen)
            || (backlogPanel != null && backlogPanel.activeSelf)
            || (vnSettingsPanel != null && vnSettingsPanel.activeSelf)
            || (confirmExitPanel != null && confirmExitPanel.activeSelf);
    }

    private bool IsSystemLoadBlockedByModal()
    {
        return !CanLoad
            || (manualSaveLoadPanel != null && manualSaveLoadPanel.IsOpen)
            || (backlogPanel != null && backlogPanel.activeSelf)
            || (vnSettingsPanel != null && vnSettingsPanel.activeSelf)
            || (confirmExitPanel != null && confirmExitPanel.activeSelf);
    }

    private bool RejectReplaySaveLoad(string operation)
    {
        if (!SceneFlowManager.IsReplayModeActive)
        {
            return false;
        }

        Debug.LogWarning($"[REPLAY] {operation} request denied before UI or screenshot work.", this);
        return true;
    }

    public void AdvanceDialogue()
    {
        if (!CanAdvanceDialogue || IsAdvanceBlockedByOpenPanel())
        {
            return;
        }

        StopAutoForwardTimer();
        StopSkipTimer();
        ShowNextLine();
    }

    private bool IsAdvanceBlockedByOpenPanel()
    {
        return IsCharacterHubOpen
            || isInterfaceHidden
            || (choicePanel != null && choicePanel.activeSelf)
            || (backlogPanel != null && backlogPanel.activeSelf)
            || (confirmExitPanel != null && confirmExitPanel.activeSelf)
            || (vnSettingsPanel != null && vnSettingsPanel.activeSelf)
            || (manualSaveLoadPanel != null && manualSaveLoadPanel.IsOpen);
    }

    /// <summary>
    /// Changes the player preference used by the single auto-forward timer.
    /// The preference is owned and persisted by SettingsManager, never SaveData.
    /// </summary>
    public void SetAutoForward(bool enabled)
    {
        if (isInterfaceHidden || IsCharacterHubOpen)
        {
            return;
        }

        SettingsManager.Instance?.SetAutoForward(enabled);
        observedAutoForward = enabled;

        if (enabled)
        {
            StartAutoForwardDelayIfReady();
        }
        else
        {
            StopAutoForwardTimer();
        }
    }

    public void ToggleAutoForward()
    {
        SetAutoForward(!IsAutoForwardEnabled());
    }

    /// <summary>
    /// Enables or disables runtime dialogue skip. This state is intentionally not saved in SaveData.
    /// Ctrl and the future Quick Menu use this entry point.
    /// </summary>
    public void SetSkip(bool enabled)
    {
        if (isInterfaceHidden || IsCharacterHubOpen || (specialModeCoordinator != null && specialModeCoordinator.IsSkipBlocked))
        {
            return;
        }

        skipEnabled = enabled;
        StopSkipTimer();

        if (!skipEnabled)
        {
            StartAutoForwardDelayIfReady();
            return;
        }

        StopAutoForwardTimer();
        if (isTyping)
        {
            CompleteTyping();
        }

        StartSkipDelayIfReady();
    }

    public void ToggleSkip()
    {
        SetSkip(!skipEnabled);
    }

    private bool IsSeenOnlySkipMode()
    {
        string skipMode = SettingsManager.Instance != null && SettingsManager.Instance.settings != null
            ? SettingsManager.Instance.settings.skipMode
            : "\u0412\u0438\u0434\u0435\u043d\u043d\u043e\u0435";
        return !IsAllTextSkipMode(skipMode);
    }

    public static bool IsAllTextSkipMode(string skipMode)
    {
        return string.Equals(skipMode, "\u0412\u0441\u0435", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(skipMode, "All", System.StringComparison.OrdinalIgnoreCase);
    }

    public static float GetSkipCadenceSeconds()
    {
        string skipBehavior = SettingsManager.Instance != null && SettingsManager.Instance.settings != null
            ? SettingsManager.Instance.settings.skipBehavior
            : SettingsOptionValues.ClassicSkip;
        return GetSkipCadenceSeconds(skipBehavior);
    }

    public static float GetSkipCadenceSeconds(string skipBehavior)
    {
        return string.Equals(skipBehavior, SettingsOptionValues.FastSkip, System.StringComparison.Ordinal)
            ? SkipCadenceSeconds * 0.5f
            : SkipCadenceSeconds;
    }

    private DialogueReadHistory EnsureReadHistory()
    {
        if (readHistory == null)
        {
            readHistory = new DialogueReadHistory();
        }

        return readHistory;
    }

    private bool IsLineAllowedForSkip(DialogueSceneData data, DialogueLine line)
    {
        if (!IsSeenOnlySkipMode())
        {
            return true;
        }

        if (data == null || line == null)
        {
            return false;
        }

        return SceneFlowManager.IsReplayModeActive
            ? SceneFlowManager.Instance.IsReplayLineSeen(data.sceneId, line.lineId)
            : EnsureReadHistory().IsSeen(data.sceneId, line.lineId);
    }

    private void MarkDisplayedLineSeen()
    {
        if (displayedLineScene == null || displayedLine == null
            || string.IsNullOrWhiteSpace(displayedLineScene.sceneId)
            || string.IsNullOrWhiteSpace(displayedLine.lineId))
        {
            return;
        }

        if (SceneFlowManager.IsReplayModeActive)
        {
            SceneFlowManager.Instance.MarkReplayLineSeen(displayedLineScene.sceneId, displayedLine.lineId);
            return;
        }

        EnsureReadHistory().MarkSeen(displayedLineScene.sceneId, displayedLine.lineId);
    }

    private void StartSkipDelayIfReady()
    {
        StopSkipTimer();
        if (skipEnabled
            && !isInterfaceHidden
            && (specialModeCoordinator == null || !specialModeCoordinator.IsSkipBlocked)
            && !IsAdvanceBlockedByOpenPanel()
            && !showingChoice
            && !showingEndLine)
        {
            skipCoroutine = StartCoroutine(SkipAfterDelay());
        }
    }

    private void StopSkipTimer()
    {
        if (skipCoroutine != null)
        {
            StopCoroutine(skipCoroutine);
            skipCoroutine = null;
        }
    }

    private IEnumerator SkipAfterDelay()
    {
        yield return new WaitForSecondsRealtime(GetSkipCadenceSeconds());
        skipCoroutine = null;

        if (!skipEnabled
            || isInterfaceHidden
            || (specialModeCoordinator != null && specialModeCoordinator.IsSkipBlocked)
            || IsAdvanceBlockedByOpenPanel()
            || showingChoice
            || showingEndLine)
        {
            yield break;
        }

        AdvanceSkipOnce();
    }

    private void AdvanceSkipOnce()
    {
        if (isTyping)
        {
            CompleteTyping();
            StartSkipDelayIfReady();
            return;
        }

        if (showingFinalLine)
        {
            AdvanceDialogue();
            StartSkipDelayIfReady();
            return;
        }

        if (activeLines == null)
        {
            SetSkip(false);
            return;
        }

        int nextLineIndex = currentLineIndex + 1;
        if (nextLineIndex < activeLines.Count)
        {
            bool allowed = IsLineAllowedForSkip(sceneData, activeLines[nextLineIndex]);
            AdvanceDialogue();
            if (!allowed)
            {
                SetSkip(false);
                return;
            }

            if (isTyping)
            {
                CompleteTyping();
            }

            StartSkipDelayIfReady();
            return;
        }

        if (activeChoices != null && activeChoices.Count > 0)
        {
            AdvanceDialogue();
            if (!ShouldResumeSkipAfterChoice())
            {
                SetSkip(false);
            }

            return;
        }

        if (sceneData != null && sceneData.defaultNextScene != null
            && sceneData.defaultNextScene.lines != null && sceneData.defaultNextScene.lines.Count > 0)
        {
            bool allowed = IsLineAllowedForSkip(sceneData.defaultNextScene, sceneData.defaultNextScene.lines[0]);
            AdvanceDialogue();
            if (!allowed)
            {
                SetSkip(false);
                return;
            }

            if (isTyping)
            {
                CompleteTyping();
            }

            StartSkipDelayIfReady();
            return;
        }

        AdvanceDialogue();
        SetSkip(false);
    }

    private bool ShouldResumeSkipAfterChoice()
    {
        return SettingsManager.Instance != null
            && SettingsManager.Instance.settings != null
            && SettingsManager.Instance.settings.skipAfterChoices;
    }

    public bool IsSkipEnabled => skipEnabled;

    public bool IsAutoForwardEnabledState => IsAutoForwardEnabled();

    private bool IsAutoForwardEnabled()
    {
        return SettingsManager.Instance != null
            && SettingsManager.Instance.settings != null
            && SettingsManager.Instance.settings.autoForward;
    }

    private void RefreshAutoForwardState()
    {
        bool enabled = IsAutoForwardEnabled();
        if (enabled == observedAutoForward)
        {
            return;
        }

        observedAutoForward = enabled;
        if (enabled)
        {
            StartAutoForwardDelayIfReady();
        }
        else
        {
            StopAutoForwardTimer();
        }
    }

    private bool CanAutoAdvance()
    {
        return IsAutoForwardEnabled()
            && !skipEnabled
            && !isTyping
            && !showingChoice
            && !showingEndLine
            && activeLines != null
            && !isInterfaceHidden
            && (specialModeCoordinator == null || !specialModeCoordinator.IsAutoBlocked)
            && !IsAdvanceBlockedByOpenPanel();
    }

    private void StartAutoForwardDelayIfReady()
    {
        StopAutoForwardTimer();

        if (CanAutoAdvance())
        {
            autoForwardCoroutine = StartCoroutine(AutoForwardAfterDelay());
        }
    }

    private void StopAutoForwardTimer()
    {
        if (autoForwardCoroutine != null)
        {
            StopCoroutine(autoForwardCoroutine);
            autoForwardCoroutine = null;
        }
    }

    private IEnumerator AutoForwardAfterDelay()
    {
        // The stored UI range (50..500) was historically displayed as percent.
        // Treat it as tenths of a second: 50 = 0.5 s and 500 = 5.0 s.
        // realtimeSinceStartup keeps Auto independent from Time.timeScale.
        float delaySeconds = GetAutoForwardDelaySeconds(
            SettingsManager.Instance != null ? SettingsManager.Instance.settings.autoForwardDelay : 250f);
        float startedAt = Time.realtimeSinceStartup;

        while (true)
        {
            if (!CanAutoAdvance())
            {
                // A modal or choice never consumes the previous wait. When it is
                // dismissed, begin one full delay from the displayed line.
                while (!CanAutoAdvance())
                {
                    if (!IsAutoForwardEnabled())
                    {
                        autoForwardCoroutine = null;
                        yield break;
                    }

                    yield return null;
                }

                delaySeconds = GetAutoForwardDelaySeconds(SettingsManager.Instance.settings.autoForwardDelay);
                startedAt = Time.realtimeSinceStartup;
            }

            if (Time.realtimeSinceStartup - startedAt >= delaySeconds)
            {
                autoForwardCoroutine = null;
                AdvanceDialogue();
                yield break;
            }

            yield return null;
        }
    }

    public static float GetAutoForwardDelaySeconds(float storedDelay)
    {
        return Mathf.Clamp(storedDelay, 50f, 500f) / 100f;
    }

    private void ShowNextLine()
    {
        if (showingChoice)
        {
            return;
        }

        if (isTyping)
        {
            CompleteTyping();
            return;
        }

        if (showingFinalLine)
        {
            showingFinalLine = false;

            if (pendingNextScene != null)
            {
                DialogueSceneData nextSceneData = pendingNextScene;
                pendingNextScene = null;
                ClearChoiceState();
                LoadDialogueScene(nextSceneData);
                return;
            }

            ClearChoiceState();
            if (TryEndTerminalReplay())
            {
                return;
            }

            showingEndLine = true;
            ShowNarration(EndPrototypeText);
            return;
        }

        if (showingEndLine || activeLines == null)
        {
            return;
        }

        int nextLineIndex = currentLineIndex + 1;
        if (nextLineIndex >= activeLines.Count)
        {
            if (activeChoices.Count > 0)
            {
                ShowChoices();
                return;
            }

            if (sceneData != null && sceneData.defaultNextScene != null)
            {
                ClearChoiceState();
                LoadDialogueScene(sceneData.defaultNextScene);
                return;
            }

            if (TryEndTerminalReplay())
            {
                return;
            }

            showingEndLine = true;
            ShowNarration(EndPrototypeText);
            return;
        }

        currentLineIndex = nextLineIndex;
        UpdateSavedDialoguePosition();
        ShowLine(activeLines[currentLineIndex]);
    }

    private void ShowChoices(bool requestAutoSave = true)
    {
        if (activeChoices == null || activeChoices.Count == 0)
        {
            if (sceneData != null && sceneData.defaultNextScene != null)
            {
                ClearChoiceState();
                LoadDialogueScene(sceneData.defaultNextScene);
                return;
            }

            if (TryEndTerminalReplay())
            {
                return;
            }

            showingEndLine = true;
            ShowNarration(EndPrototypeText);
            return;
        }

        visibleChoices.Clear();
        visibleChoices.AddRange(ConditionalChoiceEvaluator.BuildVisibleChoices(activeChoices, GameState.EnsureInstance()));

        int usableCapacity = GetUsableChoiceButtonCapacity();
        if (IsChoiceCapacityExceeded(visibleChoices.Count, usableCapacity))
        {
            ShowControlledChoiceError(
                $"[CHOICE CONDITIONS] Scene '{sceneData.sceneId}' has {visibleChoices.Count} visible choices but only {usableCapacity} usable choice buttons.");
            return;
        }

        if (visibleChoices.Count == 0)
        {
            if (sceneData != null && sceneData.defaultNextScene != null)
            {
                ClearChoiceState();
                LoadDialogueScene(sceneData.defaultNextScene);
                return;
            }

            ShowControlledChoiceError(
                $"[CHOICE CONDITIONS] Scene '{sceneData.sceneId}' has source choices but none are available and no defaultNextScene.");
            return;
        }

        showingChoice = true;
        ClearChoiceState();
        RememberChoicePosition();
        nextButton.interactable = false;
        SetChoiceOverlayActive(true);
        choicePanel.SetActive(true);

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] == null)
            {
                Debug.LogWarning($"Choice button at index {i} is null and will be skipped.", this);
                continue;
            }

            bool hasChoice = i < visibleChoices.Count;
            choiceButtons[i].gameObject.SetActive(hasChoice);

            if (hasChoice)
            {
                SetButtonText(choiceButtons[i], visibleChoices[i].choice.text);
            }
        }

        if (requestAutoSave)
        {
            RequestAutoSave();
        }
    }

    private void Choose(int displaySlot)
    {
        if (!ConditionalChoiceEvaluator.TryGetVisibleChoice(visibleChoices, displaySlot, out VisibleChoice visibleChoice))
        {
            return;
        }

        DialogueChoice choice = visibleChoice.choice;

        GameState gameState = GameState.EnsureInstance();
        gameState.ApplyChoice(choice);
        gameState.selectedChoiceIndex = visibleChoice.sourceChoiceIndex;
        gameState.choiceResultActive = true;
        pendingNextScene = choice.nextScene != null ? choice.nextScene : sceneData.defaultNextScene;
        gameState.pendingNextSceneId = pendingNextScene != null ? pendingNextScene.sceneId : string.Empty;
        ShowFinalLine(choice.resultText);
        string relationshipFeedback = RelationshipFeedback.Build(choice);
        if (!string.IsNullOrEmpty(relationshipFeedback))
        {
            ShowToast(relationshipFeedback);
        }

        if (skipEnabled && ShouldResumeSkipAfterChoice())
        {
            if (isTyping)
            {
                CompleteTyping();
            }

            StartSkipDelayIfReady();
        }
    }

    public int GetUsableChoiceButtonCapacity()
    {
        if (choiceButtons == null)
        {
            return 0;
        }

        int capacity = 0;
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] != null)
            {
                capacity++;
            }
        }

        return capacity;
    }

    public static bool IsChoiceCapacityExceeded(int visibleChoiceCount, int usableCapacity)
    {
        return visibleChoiceCount > usableCapacity;
    }

    private void ShowControlledChoiceError(string diagnostic)
    {
        Debug.LogError(diagnostic, this);
        if (SceneFlowManager.IsReplayModeActive)
        {
            SceneFlowManager.Instance.FailReplay(diagnostic);
            return;
        }

        visibleChoices.Clear();
        ClearChoiceState();
        pendingNextScene = null;
        showingChoice = false;
        showingFinalLine = false;
        showingEndLine = true;
        choicePanel.SetActive(false);
        SetChoiceOverlayActive(false);
        nextButton.interactable = true;
        ShowNarration(ChoiceConfigurationErrorText);
    }

    private void ShowFinalLine(string text)
    {
        finalLineText = text;
        showingChoice = false;
        showingFinalLine = true;
        choicePanel.SetActive(false);
        SetChoiceOverlayActive(false);
        nextButton.interactable = true;
        ShowNarration(finalLineText);
    }

    private void SetChoiceOverlayActive(bool isActive)
    {
        if (choiceDimOverlay != null)
        {
            choiceDimOverlay.SetActive(isActive);
        }
    }

    private void ShowLine(DialogueLine line)
    {
        displayedLineScene = sceneData;
        displayedLine = line;
        bool hasSpeaker = !string.IsNullOrWhiteSpace(line.speaker);
        nameBox.SetActive(hasSpeaker);
        speakerText.text = hasSpeaker ? line.speaker : string.Empty;
        AddToBacklog(line.speaker, line.text);
        ShowText(line.text);
        ApplyVisuals(line);
    }

    private void ShowNarration(string text)
    {
        displayedLineScene = null;
        displayedLine = null;
        nameBox.SetActive(false);
        speakerText.text = string.Empty;
        AddToBacklog(string.Empty, text);
        ShowText(text);
    }

    private void AddToBacklog(string speaker, string text)
    {
        if (backlogCaptureSuppressionDepth > 0)
        {
            return;
        }

        backlog.Add(speaker, text);
    }

    public List<DialogueBacklogEntry> CaptureBacklogSnapshot()
    {
        return backlog.CaptureSnapshot();
    }

    public void ReplaceBacklogFromSnapshot(IEnumerable<DialogueBacklogEntry> snapshot)
    {
        backlog.ReplaceFromSnapshot(snapshot, warning => Debug.LogWarning($"[BACKLOG] {warning}", this));
    }

    public void ClearBacklog()
    {
        backlog.Clear();
    }

    public void ShowBacklog()
    {
        if (!CanOpenBacklog)
        {
            return;
        }

        if (backlogPanel == null || backlogText == null)
        {
            Debug.LogWarning("VNDialogueController: backlogPanel or backlogText is not assigned.", this);
            return;
        }

        backlogText.text = backlog.BuildRichText();
        StopAutoForwardTimer();
        SetBacklogOverlayActive(true);
        backlogPanel.SetActive(true);
    }

    public void HideBacklog()
    {
        if (backlogPanel != null)
        {
            backlogPanel.SetActive(false);
        }

        SetBacklogOverlayActive(false);
        StartAutoForwardDelayIfReady();
        StartSkipDelayIfReady();
    }

    private void SetBacklogOverlayActive(bool isActive)
    {
        if (backlogDimOverlay != null)
        {
            backlogDimOverlay.SetActive(isActive);
        }
    }

    public bool TryGetSavePosition(
        out string sceneId,
        out string lineId,
        out int lineIndex,
        out string error)
    {
        sceneId = string.Empty;
        lineId = string.Empty;
        lineIndex = -1;
        error = string.Empty;

        if (sceneData == null || activeLines == null || activeLines.Count == 0)
        {
            error = "No active dialogue scene or lines.";
            return false;
        }

        int resolvedIndex = currentLineIndex;
        GameState gameState = GameState.Instance;
        if ((resolvedIndex < 0 || resolvedIndex >= activeLines.Count)
            && gameState != null
            && gameState.currentSceneId == sceneData.sceneId)
        {
            resolvedIndex = gameState.currentLineIndex;
        }

        if (resolvedIndex < 0 || resolvedIndex >= activeLines.Count || activeLines[resolvedIndex] == null)
        {
            error = $"Current line index {resolvedIndex} is invalid for scene '{sceneData.sceneId}'.";
            return false;
        }

        sceneId = sceneData.sceneId ?? string.Empty;
        lineId = activeLines[resolvedIndex].lineId ?? string.Empty;
        lineIndex = resolvedIndex;

        if (string.IsNullOrWhiteSpace(sceneId) || string.IsNullOrWhiteSpace(lineId))
        {
            error = "Current sceneId or lineId is empty.";
            return false;
        }

        return true;
    }

    public void OpenSettings()
    {
        if (!CanOpenSettings)
        {
            return;
        }

        StopAutoForwardTimer();
        settingsPresenter?.Open();
    }

    public void HideSettings()
    {
        settingsPresenter?.Hide();
        StartAutoForwardDelayIfReady();
        StartSkipDelayIfReady();
    }

    public void ResetSettings()
    {
        settingsPresenter?.Reset();
    }

    public void OnMasterVolumeChanged(float value)
    {
        settingsPresenter?.SetMasterVolume(value);
    }

    public void OnMusicVolumeChanged(float value)
    {
        settingsPresenter?.SetMusicVolume(value);
    }

    public void OnSfxVolumeChanged(float value)
    {
        settingsPresenter?.SetSfxVolume(value);
    }

    public void OnTextSpeedChanged(float value)
    {
        settingsPresenter?.SetTextSpeed(value);
    }

    public void OnFullscreenChanged(bool value)
    {
        settingsPresenter?.SetFullscreen(value);
    }

    public void ReturnToMainMenu()
    {
        if (!CanReturnToMainMenu)
        {
            return;
        }

        if (SceneFlowManager.IsReplayModeActive)
        {
            StopSkipTimer();
            StopAutoForwardTimer();
            SceneFlowManager.Instance.EndReplay();
            return;
        }

        specialModeCoordinator?.ForceClearForHostLifecycle("Return to Main Menu");

        if (backlogPanel != null)
        {
            backlogPanel.SetActive(false);
        }

        SetBacklogOverlayActive(false);

        if (confirmExitPanel != null)
        {
            confirmExitPanel.SetActive(false);
        }

        settingsPresenter?.Hide();

        if (choicePanel != null)
        {
            choicePanel.SetActive(false);
        }

        SetChoiceOverlayActive(false);

        if (nextButton != null)
        {
            nextButton.interactable = true;
        }

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        isTyping = false;
        StopAutoForwardTimer();

        SceneFlowManager.EnsureInstance().ReturnToMainMenu();
    }

    public void ShowConfirmExit()
    {
        if (!CanReturnToMainMenu)
        {
            return;
        }

        if (SceneFlowManager.IsReplayModeActive)
        {
            ReturnToMainMenu();
            return;
        }

        if (confirmExitPanel == null)
        {
            ReturnToMainMenu();
            return;
        }

        StopAutoForwardTimer();
        confirmExitPanel.SetActive(true);
    }

    public void HideConfirmExit()
    {
        if (confirmExitPanel != null)
        {
            confirmExitPanel.SetActive(false);
        }

        StartAutoForwardDelayIfReady();
    }

    private void ConfirmReturnToMainMenu()
    {
        HideConfirmExit();
        ReturnToMainMenu();
    }

    private void ShowToast(string message)
    {
        if (notificationPanel == null || notificationText == null)
        {
            Debug.Log(message);
            return;
        }

        if (notificationCoroutine != null)
        {
            StopCoroutine(notificationCoroutine);
        }

        notificationText.text = message;
        notificationPanel.SetActive(true);
        notificationCoroutine = StartCoroutine(HideToastAfterDelay());
    }

    private IEnumerator HideToastAfterDelay()
    {
        yield return new WaitForSeconds(notificationDuration);

        if (notificationPanel != null)
        {
            notificationPanel.SetActive(false);
        }

        notificationCoroutine = null;
    }

    private void ShowText(string text)
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        if (text == null)
        {
            text = string.Empty;
        }

        currentFullText = text;
        typingCoroutine = StartCoroutine(TypeText(text));
    }

    private IEnumerator TypeText(string text)
    {
        isTyping = true;
        dialogueText.text = string.Empty;

        float textSpeed = 1f;

        if (SettingsManager.Instance != null)
        {
            textSpeed = SettingsManager.Instance.settings.textSpeed;
        }

        float charsPerSecond = baseCharactersPerSecond * Mathf.Max(0.1f, textSpeed);
        float characterDelay = 1f / charsPerSecond;

        foreach (char character in text)
        {
            dialogueText.text += character;
            yield return new WaitForSecondsRealtime(characterDelay);
        }

        dialogueText.text = text;
        isTyping = false;
        typingCoroutine = null;
        MarkDisplayedLineSeen();
        StartAutoForwardDelayIfReady();
    }

    private void CompleteTyping()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        dialogueText.text = currentFullText;
        isTyping = false;
        typingCoroutine = null;
        MarkDisplayedLineSeen();
        StartAutoForwardDelayIfReady();
    }

    private void SetButtonText(Button button, string text)
    {
        TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();

        if (buttonText != null)
        {
            buttonText.text = text;
            return;
        }

        Debug.LogWarning($"TextMeshProUGUI is missing on button '{button.name}'.", button);
    }

    private void ApplyVisuals(DialogueLine line)
    {
        if (backgroundImage != null && line.background != null)
        {
            backgroundImage.sprite = line.background;
            backgroundImage.color = Color.white;
            backgroundImage.enabled = true;
        }

        if (characterImage == null)
        {
            return;
        }

        if (line.hideCharacter)
        {
            characterImage.enabled = false;
            return;
        }

        if (line.characterSprite != null)
        {
            characterImage.sprite = line.characterSprite;
            characterImage.enabled = true;
            characterImage.preserveAspect = true;
            characterImage.rectTransform.sizeDelta = characterDefaultSize;
            characterImage.rectTransform.anchoredPosition = GetCharacterPosition(line.characterPosition);
        }
    }

    private Sprite FindLastBackgroundBeforeOrAt(int lineIndex)
    {
        if (activeLines == null)
        {
            return null;
        }

        int safeIndex = Mathf.Clamp(lineIndex, 0, activeLines.Count - 1);

        for (int i = safeIndex; i >= 0; i--)
        {
            if (activeLines[i] != null && activeLines[i].background != null)
            {
                return activeLines[i].background;
            }
        }

        return null;
    }

    private Vector2 GetCharacterPosition(CharacterPosition position)
    {
        switch (position)
        {
            case CharacterPosition.Left:
                return characterLeftPosition;
            case CharacterPosition.Center:
                return characterCenterPosition;
            case CharacterPosition.Right:
                return characterRightPosition;
            case CharacterPosition.Solo:
                return characterSoloPosition;
            default:
                return characterCenterPosition;
        }
    }

    private bool ValidateRequiredUiReferences()
    {
        bool isValid = true;

        isValid &= ValidateReference(speakerText, nameof(speakerText));
        isValid &= ValidateReference(dialogueText, nameof(dialogueText));
        isValid &= ValidateReference(nameBox, nameof(nameBox));
        isValid &= ValidateReference(nextButton, nameof(nextButton));
        isValid &= ValidateReference(dialogueUiRoot, nameof(dialogueUiRoot));
        isValid &= ValidateReference(choicePanel, nameof(choicePanel));
        isValid &= ValidateReference(choiceMashaButton, nameof(choiceMashaButton));
        isValid &= ValidateReference(choiceArtemButton, nameof(choiceArtemButton));
        isValid &= ValidateReference(choiceLeraButton, nameof(choiceLeraButton));

        return isValid;
    }

    private bool ValidateReference(Object reference, string fieldName)
    {
        if (reference != null)
        {
            return true;
        }

        Debug.LogError($"VNDialogueController: required reference '{fieldName}' is not assigned.", this);
        return false;
    }

    public bool RestoreFromGameState()
    {
        return RestoreFromGameState(false);
    }

    public bool RestoreFromGameState(bool snapshotContainsVisibleEntry)
    {
        GameState gameState = GameState.Instance;

        if (gameState == null)
        {
            Debug.LogWarning("VNDialogueController: GameState.Instance is missing.");
            return false;
        }

        if (sceneRegistry == null)
        {
            Debug.LogWarning("VNDialogueController: sceneRegistry is not assigned.", this);
            return false;
        }

        bool restoreChoiceResult = gameState.choiceResultActive;
        int restoredChoiceIndex = gameState.selectedChoiceIndex;
        string restoredPendingNextSceneId = gameState.pendingNextSceneId ?? string.Empty;
        Debug.Log($"[VN LOAD] Preflighting GameState. sceneId='{gameState.currentSceneId}', lineId='{gameState.currentLineId}', fallbackLineIndex={gameState.currentLineIndex}, choiceIndex={restoredChoiceIndex}, choiceResultActive={restoreChoiceResult}, pendingNextSceneId='{restoredPendingNextSceneId}'.", this);
        DialogueSceneData restoredScene = sceneRegistry.FindById(gameState.currentSceneId);

        if (restoredScene == null)
        {
            Debug.LogWarning($"[VN LOAD] Scene '{gameState.currentSceneId}' was not found in DialogueSceneRegistry; no new scene was started.", this);
            return false;
        }

        Debug.Log($"[VN LOAD] Found DialogueSceneData sceneId='{restoredScene.sceneId}' asset='{restoredScene.name}'.", this);

        int restoredLineIndex = restoredScene.FindLineIndexById(gameState.currentLineId);
        if (restoredLineIndex < 0 && string.IsNullOrEmpty(gameState.currentLineId))
        {
            restoredLineIndex = gameState.currentLineIndex;
        }

        if (restoredScene.lines == null
            || restoredLineIndex < 0
            || restoredLineIndex >= restoredScene.lines.Count
            || restoredScene.lines[restoredLineIndex] == null)
        {
            Debug.LogWarning($"[VN LOAD] Line '{gameState.currentLineId}' with fallback index {gameState.currentLineIndex} is invalid for scene '{restoredScene.sceneId}'. No dialogue state was changed.", this);
            return false;
        }

        DialogueChoice restoredChoice = null;
        DialogueSceneData restoredPendingNextScene = null;
        if (!restoreChoiceResult)
        {
            if (restoredChoiceIndex != -1 || !string.IsNullOrEmpty(restoredPendingNextSceneId))
            {
                Debug.LogWarning("[VN LOAD] Inactive choice state contains a selected choice or pending scene. No dialogue state was changed.", this);
                return false;
            }
        }
        else
        {
            if (restoredScene.choices == null
                || restoredChoiceIndex < 0
                || restoredChoiceIndex >= restoredScene.choices.Count)
            {
                Debug.LogWarning($"[VN LOAD] Choice index {restoredChoiceIndex} is invalid for scene '{restoredScene.sceneId}'. No dialogue state was changed.", this);
                return false;
            }

            restoredChoice = restoredScene.choices[restoredChoiceIndex];
            if (restoredChoice == null)
            {
                Debug.LogWarning($"[VN LOAD] Choice {restoredChoiceIndex} in scene '{restoredScene.sceneId}' is null. No dialogue state was changed.", this);
                return false;
            }

            DialogueSceneData configuredNextScene = restoredChoice.nextScene != null
                ? restoredChoice.nextScene
                : restoredScene.defaultNextScene;

            if (configuredNextScene == null)
            {
                if (!string.IsNullOrEmpty(restoredPendingNextSceneId))
                {
                    Debug.LogWarning($"[VN LOAD] Choice {restoredChoiceIndex} has no transition target, but pending scene is '{restoredPendingNextSceneId}'. No dialogue state was changed.", this);
                    return false;
                }
            }
            else
            {
                restoredPendingNextScene = sceneRegistry.FindById(configuredNextScene.sceneId);
                if (restoredPendingNextScene != configuredNextScene)
                {
                    Debug.LogWarning($"[VN LOAD] Configured choice target '{configuredNextScene.sceneId}' is absent from the registry. No dialogue state was changed.", this);
                    return false;
                }

                if (!string.IsNullOrEmpty(restoredPendingNextSceneId)
                    && restoredPendingNextSceneId != configuredNextScene.sceneId)
                {
                    Debug.LogWarning($"[VN LOAD] Pending scene '{restoredPendingNextSceneId}' does not exactly match choice target '{configuredNextScene.sceneId}'. No dialogue state was changed.", this);
                    return false;
                }
            }
        }

        Debug.Log($"[VN LOAD] Preflight passed. requestedLineId='{gameState.currentLineId}', resolvedIndex={restoredLineIndex}, resolvedLineId='{restoredScene.lines[restoredLineIndex].lineId}'.", this);

        if (snapshotContainsVisibleEntry)
        {
            RunWithoutBacklogCapture(() =>
            {
                RestoreDialogueDisplay(
                    restoredScene,
                    restoredLineIndex,
                    restoreChoiceResult,
                    restoredChoice,
                    restoredChoiceIndex,
                    restoredPendingNextScene);
                return true;
            });
        }
        else if (restoreChoiceResult)
        {
            // Legacy saves have no snapshot. Hide the underlying saved line from
            // History, then capture the visible result beat exactly once.
            RunWithoutBacklogCapture(() =>
            {
                LoadDialogueScene(restoredScene, restoredLineIndex, false);
                return true;
            });
            RestoreChoiceResult(restoredChoice, restoredChoiceIndex, restoredPendingNextScene);
        }
        else
        {
            RestoreDialogueDisplay(
                restoredScene,
                restoredLineIndex,
                false,
                restoredChoice,
                restoredChoiceIndex,
                restoredPendingNextScene);
        }

        Debug.Log($"[VN LOAD] Restoration finished. activeSceneId='{sceneData.sceneId}', activeLineIndex={currentLineIndex}, activeLineId='{(activeLines != null && currentLineIndex >= 0 && currentLineIndex < activeLines.Count && activeLines[currentLineIndex] != null ? activeLines[currentLineIndex].lineId : "<invalid>")}', choiceResultActive={GameState.Instance.choiceResultActive}.", this);
        return true;
    }

    private void RestoreDialogueDisplay(
        DialogueSceneData restoredScene,
        int restoredLineIndex,
        bool restoreChoiceResult,
        DialogueChoice restoredChoice,
        int restoredChoiceIndex,
        DialogueSceneData restoredPendingNextScene)
    {
        LoadDialogueScene(restoredScene, restoredLineIndex, false);

        if (restoreChoiceResult)
        {
            RestoreChoiceResult(restoredChoice, restoredChoiceIndex, restoredPendingNextScene);
        }
        else if (restoredScene.choices != null
            && restoredScene.choices.Count > 0
            && restoredLineIndex == restoredScene.lines.Count - 1)
        {
            if (isTyping)
            {
                CompleteTyping();
            }

            ShowChoices(false);
        }
    }

    private bool RunWithoutBacklogCapture(System.Func<bool> action)
    {
        backlogCaptureSuppressionDepth++;
        try
        {
            return action != null && action();
        }
        finally
        {
            backlogCaptureSuppressionDepth--;
        }
    }

    private void RestoreChoiceResult(
        DialogueChoice restoredChoice,
        int restoredChoiceIndex,
        DialogueSceneData restoredPendingNextScene)
    {
        pendingNextScene = restoredPendingNextScene;

        GameState gameState = GameState.EnsureInstance();
        gameState.selectedChoiceIndex = restoredChoiceIndex;
        gameState.choiceResultActive = true;
        gameState.pendingNextSceneId = pendingNextScene != null ? pendingNextScene.sceneId : string.Empty;
        ShowFinalLine(restoredChoice.resultText);
    }

    private void RememberChoicePosition()
    {
        if (activeLines == null || activeLines.Count == 0)
        {
            return;
        }

        GameState gameState = GameState.EnsureInstance();
        gameState.currentLineIndex = activeLines.Count - 1;
        gameState.currentLineId = activeLines[activeLines.Count - 1] != null
            ? activeLines[activeLines.Count - 1].lineId ?? string.Empty
            : string.Empty;
    }

    private void ClearChoiceState()
    {
        GameState gameState = GameState.Instance;
        if (gameState == null)
        {
            return;
        }

        gameState.selectedChoiceIndex = -1;
        gameState.choiceResultActive = false;
        gameState.pendingNextSceneId = string.Empty;
    }

    /// <summary>Minimal typed bridge for authored systems that use the normal VN scene transition path.</summary>
    public bool TryRouteToScene(DialogueSceneData targetScene)
    {
        if (HasActiveSpecialMode || !IsRegisteredDialogueScene(targetScene))
        {
            return false;
        }

        LoadDialogueScene(targetScene, 0, false);
        return true;
    }

    /// <summary>Checks registry membership without exposing a second scene-flow system.</summary>
    public bool IsRegisteredDialogueScene(DialogueSceneData targetScene)
    {
        return targetScene != null
            && sceneRegistry != null
            && sceneRegistry.scenes != null
            && sceneRegistry.scenes.Contains(targetScene)
            && targetScene.lines != null
            && targetScene.lines.Count > 0;
    }

    private void LoadDialogueScene(DialogueSceneData data)
    {
        LoadDialogueScene(data, 0, true);
    }

    private void LoadDialogueScene(DialogueSceneData data, int startLineIndex, bool requestAutoSave)
    {
        StopAutoForwardTimer();

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        currentFullText = string.Empty;
        isTyping = false;
        visibleChoices.Clear();

        if (data == null)
        {
            Debug.LogError("Dialogue scene data is missing.", this);
            if (SceneFlowManager.IsReplayModeActive)
            {
                SceneFlowManager.Instance.FailReplay("Replay attempted to load a missing dialogue scene.");
                return;
            }

            activeLines = null;
            activeChoices = new List<DialogueChoice>();
            showingChoice = false;
            showingFinalLine = false;
            showingEndLine = true;
            pendingNextScene = null;
            choicePanel.SetActive(false);
            SetChoiceOverlayActive(false);
            nextButton.interactable = true;
            ShowNarration(MissingSceneDataText);
            return;
        }

        if (data.lines == null || data.lines.Count == 0)
        {
            Debug.LogError($"Dialogue scene '{data.name}' has no lines.", data);
            if (SceneFlowManager.IsReplayModeActive)
            {
                SceneFlowManager.Instance.FailReplay($"Replay scene '{data.name}' has no lines.");
                return;
            }

            activeLines = null;
            activeChoices = new List<DialogueChoice>();
            showingChoice = false;
            showingFinalLine = false;
            showingEndLine = true;
            pendingNextScene = null;
            choicePanel.SetActive(false);
            SetChoiceOverlayActive(false);
            nextButton.interactable = true;
            ShowNarration(MissingSceneDataText);
            return;
        }

        sceneData = data;
        ApplySceneAudio();
        activeLines = sceneData.lines;
        activeChoices = sceneData.choices ?? new List<DialogueChoice>();
        currentLineIndex = Mathf.Clamp(startLineIndex, 0, activeLines.Count - 1);
        showingChoice = false;
        showingFinalLine = false;
        showingEndLine = false;
        pendingNextScene = null;
        choicePanel.SetActive(false);
        SetChoiceOverlayActive(false);
        nextButton.interactable = true;
        GameState gameState = GameState.EnsureInstance();
        gameState.currentSceneId = sceneData.sceneId;
        gameState.currentLineIndex = currentLineIndex;
        gameState.currentLineId = activeLines[currentLineIndex].lineId ?? string.Empty;

        Sprite restoredBackground = FindLastBackgroundBeforeOrAt(currentLineIndex);
        if (backgroundImage != null && restoredBackground != null)
        {
            backgroundImage.sprite = restoredBackground;
            backgroundImage.color = Color.white;
            backgroundImage.enabled = true;
        }

        bool firstLineAllowedForSkip = IsLineAllowedForSkip(sceneData, activeLines[currentLineIndex]);
        if (skipEnabled && !firstLineAllowedForSkip)
        {
            SetSkip(false);
        }

        ShowLine(activeLines[currentLineIndex]);
        if (skipEnabled && isTyping)
        {
            CompleteTyping();
            StartSkipDelayIfReady();
        }

        if (requestAutoSave)
        {
            RequestAutoSave();
        }
    }

    private void UpdateSavedDialoguePosition()
    {
        GameState gameState = GameState.EnsureInstance();
        gameState.currentLineIndex = currentLineIndex;
        gameState.currentLineId = activeLines != null
            && currentLineIndex >= 0
            && currentLineIndex < activeLines.Count
            && activeLines[currentLineIndex] != null
                ? activeLines[currentLineIndex].lineId ?? string.Empty
                : string.Empty;
    }

    private void ApplySceneAudio()
    {
        if (sceneData == null || AudioManager.Instance == null)
        {
            return;
        }

        if (sceneData.stopMusicOnStart)
        {
            AudioManager.Instance.StopMusic();
            return;
        }

        if (sceneData.backgroundMusic != null)
        {
            AudioManager.Instance.PlayMusic(sceneData.backgroundMusic);
        }
    }

    private void OnDestroy()
    {
        if (isInterfaceHidden)
        {
            RestoreInterface();
        }

        specialModeCoordinator?.ForceClearForHostLifecycle("VNDialogueController destroyed");
        StopSkipTimer();
        StopAutoForwardTimer();

        if (Instance == this)
        {
            Instance = null;
        }

        SceneFlowManager flow = SceneFlowManager.Instance;
        if (flow != null && flow.IsReplayHost(this))
        {
            flow.FailReplay("Replay VN host was destroyed unexpectedly.");
        }
    }

    private bool TryEndTerminalReplay()
    {
        SceneFlowManager flow = SceneFlowManager.Instance;
        if (flow == null || !flow.IsReplayMode)
        {
            return false;
        }

        StopSkipTimer();
        StopAutoForwardTimer();
        flow.EndReplay();
        return true;
    }

    public void StopReplayExecutionForCleanup()
    {
        StopSkipTimer();
        StopAutoForwardTimer();
    }
}
