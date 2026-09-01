using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Linq;

public class VNDialogueController : MonoBehaviour
{
    public static VNDialogueController Instance { get; private set; }

    private const string MissingSceneDataText = "Dialogue scene data is missing.";
    private const float SkipCadenceSeconds = 0.12f;
    private const string EndPrototypeText = "\u041a\u043e\u043d\u0435\u0446 Unity-\u043f\u0440\u043e\u0442\u043e\u0442\u0438\u043f\u0430.";
    private const string ChoiceConfigurationErrorText = "\u0418\u0441\u0442\u043e\u0440\u0438\u044f \u043d\u0435 \u043c\u043e\u0436\u0435\u0442 \u0431\u044b\u0442\u044c \u043f\u0440\u043e\u0434\u043e\u043b\u0436\u0435\u043d\u0430.";
    public const int SupportedChoiceButtonCapacity = 4;
    private static readonly string[] TemporaryReadingChromeNames =
    {
        "How I Fall Logo",
        "Chapter Info",
        "Top Left Soft Shade"
    };

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
    private Coroutine relationshipCueCoroutine;
    private GameObject relationshipCueRoot;
    private CanvasGroup relationshipCueCanvasGroup;
    private string currentFullText = string.Empty;
    private bool isTyping;
    private bool quickSaveInProgress;
    private bool autoSaveInProgress;
    private bool pendingAutoSave;
    private bool preLoadAutoSavePending;
    private System.Action<bool> preLoadAutoSaveCompletion;
    private readonly DialogueBacklog backlog = new DialogueBacklog(DialogueBacklog.DefaultCapacity);
    private int backlogCaptureSuppressionDepth;
    private PreferencesController preferencesController;
    private float dialogueBaseFontSize;
    private Image dialogueBoxBackground;
    private Vector2 dialogueBaseBoxSize;
    private Vector2 dialogueBaseTextSize;
    private bool readingPresentationInitialized;
    private bool observedAutoForward;
    private bool skipEnabled;
    private DialogueReadHistory readHistory;
    private DialogueSceneData displayedLineScene;
    private DialogueLine displayedLine;
    private SpecialModeCoordinator specialModeCoordinator;
    private ChatController chatController;
    private InteractiveSceneController interactiveSceneController;
    private MapSceneController mapSceneController;
    private VNGameMenuController gameMenuController;
    private bool specialModeWasActive;
    private bool isInterfaceHidden;
    private bool isRuntimeReady;
    private bool mainMenuConfirmationOpenedFromGameMenu;
    private UnityEngine.Object dialogueShellSuppressionOwner;
    private bool dialogueShellWasVisibleBeforeSuppression;

    public bool IsInterfaceHidden => isInterfaceHidden;
    public bool IsRelationshipCueVisible => relationshipCueRoot != null && relationshipCueRoot.activeInHierarchy;
    public bool IsPreferencesOpen => (preferencesController != null && preferencesController.IsOpen)
        || (vnSettingsPanel != null && vnSettingsPanel.activeSelf);
    /// <summary>True while a transient special presentation owns the ordinary dialogue shell.</summary>
    public bool IsDialogueShellSuppressed => dialogueShellSuppressionOwner != null;
    /// <summary>True only after this controller completed its scene-local Start initialization.</summary>
    public bool IsRuntimeReady => isRuntimeReady && isActiveAndEnabled;
    public ChatController ActiveChatController => chatController;
    public InteractiveSceneController ActiveInteractiveSceneController => interactiveSceneController;
    public MapSceneController ActiveMapSceneController => mapSceneController;
    public bool HasActiveSpecialMode => specialModeCoordinator != null && specialModeCoordinator.HasActiveOwner;
    /// <summary>Generic backend guard for any active exclusive special interaction.</summary>
    public bool IsSpecialModeSaveLoadBlocked => specialModeCoordinator != null
        && specialModeCoordinator.HasActiveOwner
        && (!specialModeCoordinator.CanSave || !specialModeCoordinator.CanLoad);
    public bool IsCharacterHubOpen => characterHubController != null && characterHubController.IsOpen;
    public bool IsGameMenuOpen => gameMenuController != null && gameMenuController.IsOpen;
    public VNGameMenuController GameMenuController => gameMenuController;
    public bool CanAdvanceDialogue => !IsCharacterHubOpen && !IsGameMenuOpen && !isInterfaceHidden && (specialModeCoordinator == null || !specialModeCoordinator.IsDialogueAdvanceBlocked);
    public bool CanSave => !IsCharacterHubOpen && !SceneFlowManager.IsReplayModeActive && !isInterfaceHidden && (specialModeCoordinator == null || specialModeCoordinator.CanSave);
    public bool CanLoad => !IsCharacterHubOpen && !SceneFlowManager.IsReplayModeActive && !isInterfaceHidden && (specialModeCoordinator == null || specialModeCoordinator.CanLoad);
    public bool CanOpenQuickMenu => !IsCharacterHubOpen && !IsGameMenuOpen && !isInterfaceHidden && (specialModeCoordinator == null || specialModeCoordinator.CanOpenQuickMenu);
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
    public bool CanOpenGameMenu => IsRuntimeReady
        && !IsGameMenuOpen
        && !isInterfaceHidden
        && (!IsDialogueShellSuppressed || (specialModeCoordinator != null && specialModeCoordinator.CanOpenGameMenu))
        && (!HasActiveSpecialMode || (specialModeCoordinator != null && specialModeCoordinator.CanOpenGameMenu))
        && !IsCharacterHubOpen
        && !showingChoice
        && !quickSaveInProgress
        && !autoSaveInProgress
        && !preLoadAutoSavePending
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
        HideLegacyTopMenuButton();
        HideTemporaryReadingChrome();
        specialModeCoordinator = new SpecialModeCoordinator(GetSpecialModeEntryBlockerReason, message => Debug.LogWarning(message, this));
        EnsureReadHistory();
        SettingsManager.DialoguePresentationChanged += RefreshDialoguePresentation;
    }

    /// <summary>Quick Menu and Esc are the single ordinary Game Menu routes; hide the legacy scene button without serializing the scene.</summary>
    private void HideLegacyTopMenuButton()
    {
        GameObject legacyButton = GameObject.Find("Top Menu Button");
        if (legacyButton == null)
        {
            legacyButton = transform.root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == "Top Menu Button")?.gameObject;
        }
        if (legacyButton != null) legacyButton.SetActive(false);
    }

    /// <summary>Hides the current non-canon scene chrome while story chapter structure is deferred.</summary>
    private void HideTemporaryReadingChrome()
    {
        foreach (string objectName in TemporaryReadingChromeNames)
        {
            GameObject chrome = GameObject.Find(objectName);
            if (chrome != null)
            {
                chrome.SetActive(false);
            }
        }
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

        RefreshDialoguePresentation();

        GameState gameState = GameState.EnsureInstance();
        characterHubController = CharacterHubController.TryCreateRuntime(this);
        chatController = ChatController.TryCreateRuntime(this);
        SaveManager saveManager = SaveManager.EnsureInstance(sceneRegistry);
        Debug.Log($"[VN] Start. sceneId='{gameState.currentSceneId}', lineId='{gameState.currentLineId}', lineIndex={gameState.currentLineIndex}, sceneData='{(sceneData != null ? sceneData.sceneId : "<null>")}'.", this);

        EnsureChoiceButtonCapacity();

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

        var preferencesView = new VNPreferencesAdapter(
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
            vnSettingsResetButton);
        preferencesController = new PreferencesController(
            new PreferencesService(),
            preferencesView,
            ShowToast,
            ResumeAfterPreferencesClosed,
            this);
        preferencesController.Initialize();
        gameMenuController = VNGameMenuController.TryCreateRuntime(this);
        HideLegacyTopMenuButton();
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
                isRuntimeReady = true;
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
                isRuntimeReady = true;
                return;
            }

            ClearBacklog();
            saveManager.FailPendingSceneRestoreAndReset();
            Debug.LogError(
                $"[LOAD] Pending restore for slot {pendingSlotIndex} failed in VNDialogueController.Start(). Loaded GameState was discarded, ResetState() was applied, and configured start scene '{(sceneData != null ? sceneData.sceneId : "<null>")}' will be started.",
                this);
            LoadDialogueScene(sceneData);
            isRuntimeReady = true;
            return;
        }

        LoadDialogueScene(sceneData);
        isRuntimeReady = true;
    }

    private void Update()
    {
        RefreshSpecialModeOwnerLifecycle();
        RefreshAutoForwardState();
        RefreshChoiceFocusPresentation();

        if (VNInputMap.WasPressedThisFrame(VNInputAction.CloseOrCancel) && !IsHandlingPreferencesDropdownCancel())
        {
            HandleEscapePressed();
            return;
        }

        if (IsPreferencesOpen)
        {
            return;
        }

        if (isInterfaceHidden)
        {
            if (VNInputMap.WasPressedThisFrame(VNInputAction.ToggleInterfaceVisibility))
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

        if (IsGameMenuOpen)
        {
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

    }

    private bool IsHandlingPreferencesDropdownCancel()
    {
        if (!IsPreferencesOpen)
        {
            return false;
        }

        SharedPreferencesView view = FindFirstObjectByType<SharedPreferencesView>(FindObjectsInactive.Include);
        return view != null && view.IsVisible && view.IsHandlingDropdownCancel;
    }

    /// <summary>Canonical single-owner Escape routing. Returns after exactly one owner handles the press.</summary>
    public bool HandleEscapePressed()
    {
        if (isInterfaceHidden)
        {
            RestoreInterface();
            return true;
        }

        if (IsCharacterHubOpen)
        {
            CloseCharacterHub();
            return true;
        }

        if (chatController != null && chatController.TryCloseMediaViewerOnEscape())
        {
            return true;
        }

        if (HasActiveSpecialMode)
        {
            if (IsGameMenuOpen)
            {
                return gameMenuController.TryHandleEscape();
            }

            if (specialModeCoordinator != null && specialModeCoordinator.CanOpenGameMenu)
            {
                return OpenGameMenu();
            }

            specialModeCoordinator.TryRequestEscapeCancel();
            return true;
        }

        if (confirmExitPanel != null && confirmExitPanel.activeSelf)
        {
            HideConfirmExit();
            return true;
        }

        if (manualSaveLoadPanel != null && manualSaveLoadPanel.IsOpen)
        {
            // ManualSaveLoadPanel owns its confirmation-first Escape handling in its own Update.
            return true;
        }

        if (IsPreferencesOpen)
        {
            HideSettings();
            return true;
        }

        if (backlogPanel != null && backlogPanel.activeSelf)
        {
            HideBacklog();
            return true;
        }

        if (gameMenuController != null && gameMenuController.TryHandleEscape())
        {
            return true;
        }

        return OpenGameMenu();
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
            || IsGameMenuOpen
            || IsPreferencesOpen
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

        if (IsPreferencesOpen)
        {
            return "VN settings";
        }

        if (IsGameMenuOpen)
        {
            return "game menu";
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

        TrySuppressDialogueShell(characterHubController);
        StopAutoForwardTimer();
        StopSkipTimer();
        return true;
    }

    /// <summary>
    /// Gives one transient presentation ownership of the ordinary dialogue shell without changing
    /// player Hide UI state or forcing a later visibility restore from an unrelated owner.
    /// </summary>
    public bool TrySuppressDialogueShell(UnityEngine.Object owner)
    {
        if (owner == null)
        {
            return false;
        }

        if (dialogueShellSuppressionOwner != null && dialogueShellSuppressionOwner != owner)
        {
            return false;
        }

        if (dialogueShellSuppressionOwner == owner)
        {
            if (dialogueUiRoot != null && dialogueUiRoot.activeSelf)
            {
                dialogueUiRoot.SetActive(false);
            }

            return true;
        }

        dialogueShellSuppressionOwner = owner;
        dialogueShellWasVisibleBeforeSuppression = dialogueUiRoot != null && dialogueUiRoot.activeSelf;
        if (dialogueUiRoot != null && dialogueUiRoot.activeSelf)
        {
            dialogueUiRoot.SetActive(false);
        }

        return true;
    }

    /// <summary>Releases the matching transient dialogue-shell owner without overriding Hide UI.</summary>
    public void ReleaseDialogueShellSuppression(UnityEngine.Object owner)
    {
        if (owner == null || dialogueShellSuppressionOwner != owner)
        {
            return;
        }

        dialogueShellSuppressionOwner = null;
        if (!isInterfaceHidden && dialogueShellWasVisibleBeforeSuppression && dialogueUiRoot != null)
        {
            dialogueUiRoot.SetActive(true);
        }

        dialogueShellWasVisibleBeforeSuppression = false;
    }

    public void CloseCharacterHub()
    {
        if (characterHubController == null || !characterHubController.Hide())
        {
            return;
        }

        ReleaseDialogueShellSuppression(characterHubController);
        StartAutoForwardDelayIfReady();
        StartSkipDelayIfReady();
        gameMenuController?.NotifyCharactersClosed();
    }

    /// <summary>Narrow story-facing entry point for the typed chat technical foundation.</summary>
    public bool StartChat(ChatSceneData chat)
    {
        return TryStartChat(chat, out _);
    }

    /// <summary>Narrow diagnostic contract for authored chat startup and the technical launcher.</summary>
    public bool TryStartChat(ChatSceneData chat, out string failureReason)
    {
        failureReason = string.Empty;
        if (!IsRuntimeReady)
        {
            failureReason = "controller not ready";
            return false;
        }

        if (chat == null)
        {
            failureReason = "null chat data";
            return false;
        }

        if (SceneFlowManager.IsReplayModeActive)
        {
            failureReason = "Replay active";
            return false;
        }

        if (chatController != null && chatController.IsRunning)
        {
            failureReason = "Chat already active";
            return false;
        }

        if (HasActiveSpecialMode)
        {
            failureReason = "another special mode active";
            return false;
        }

        if (chatController == null && !ChatController.TryCreateRuntime(this, out chatController, out failureReason))
        {
            return false;
        }

        if (chatController == null)
        {
            failureReason = "Canvas/UI unavailable";
            return false;
        }

        return chatController.TryStartChat(chat, out failureReason);
    }

    /// <summary>Narrow story-facing entry point for authored interactive image scenes.</summary>
    public bool TryStartInteractiveScene(InteractiveSceneData scene, out string failureReason)
    {
        failureReason = string.Empty;
        if (!IsRuntimeReady) { failureReason = "controller not ready"; return false; }
        if (scene == null) { failureReason = "null interactive scene data"; return false; }
        if (SceneFlowManager.IsReplayModeActive) { failureReason = "Replay active"; return false; }
        if (HasActiveSpecialMode) { failureReason = "another special mode active"; return false; }
        if (interactiveSceneController == null && !InteractiveSceneController.TryCreateRuntime(this, out interactiveSceneController, out failureReason)) return false;
        return interactiveSceneController != null && interactiveSceneController.TryStart(scene, out failureReason);
    }

    /// <summary>Narrow story-facing entry point for authored map/location scenes.</summary>
    public bool TryStartMapScene(MapSceneData map, out string failureReason)
    {
        failureReason = string.Empty;
        if (!IsRuntimeReady) { failureReason = "controller not ready"; return false; }
        if (map == null) { failureReason = "null map scene data"; return false; }
        if (SceneFlowManager.IsReplayModeActive) { failureReason = "Replay active"; return false; }
        if (HasActiveSpecialMode) { failureReason = "another special mode active"; return false; }
        if (mapSceneController == null && !MapSceneController.TryCreateRuntime(this, out mapSceneController, out failureReason)) return false;
        return mapSceneController != null && mapSceneController.TryStart(map, out failureReason);
    }

    private bool IsAnyOrdinaryModalOpen()
    {
        return (choicePanel != null && choicePanel.activeSelf)
            || (backlogPanel != null && backlogPanel.activeSelf)
            || IsPreferencesOpen
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
            // WaitForEndOfFrame is not resumed by Unity's batch-mode PlayMode runner.
            // A normal player build keeps the end-of-frame capture; CI advances one frame instead.
            if (Application.isBatchMode)
            {
                yield return null;
            }
            else
            {
                yield return new WaitForEndOfFrame();
            }

            try
            {
                screenshot = SaveManager.CaptureScreenshotForSave();
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
            || IsPreferencesOpen
            || (confirmExitPanel != null && confirmExitPanel.activeSelf);
    }

    private bool IsSystemLoadBlockedByModal()
    {
        return !CanLoad
            || (manualSaveLoadPanel != null && manualSaveLoadPanel.IsOpen)
            || (backlogPanel != null && backlogPanel.activeSelf)
            || IsPreferencesOpen
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
            || IsGameMenuOpen
            || isInterfaceHidden
            || (choicePanel != null && choicePanel.activeSelf)
            || (backlogPanel != null && backlogPanel.activeSelf)
            || (confirmExitPanel != null && confirmExitPanel.activeSelf)
            || IsPreferencesOpen
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

        ApplyChoicePresentation();
        FocusFirstVisibleChoice();

        if (requestAutoSave)
        {
            RequestAutoSave();
        }
    }

    private void Choose(int displaySlot)
    {
        if (!showingChoice
            || choicePanel == null
            || !choicePanel.activeInHierarchy
            || !ConditionalChoiceEvaluator.TryGetVisibleChoice(visibleChoices, displaySlot, out VisibleChoice visibleChoice))
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
        ShowRelationshipCue(RelationshipFeedback.GetCueKind(choice));

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
        ApplyBacklogPlayerFacingPalette();
        ApplyBacklogPresentation();
        SetBacklogOverlayActive(true);
        backlogPanel.SetActive(true);
        Canvas.ForceUpdateCanvases();
        ScrollRect scrollRect = backlogPanel.GetComponentInChildren<ScrollRect>(true);
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }

        Focus(backlogCloseButton);
    }

    private void ApplyBacklogPlayerFacingPalette()
    {
        if (backlogPanel == null)
        {
            return;
        }

        foreach (Image image in backlogPanel.GetComponentsInChildren<Image>(true))
        {
            Color color = image.color;
            if (image.GetComponentInParent<Button>(true) == null
                && color.r > color.g * 1.4f
                && color.r > color.b * 1.2f)
            {
                image.color = new Color(0.30f, 0.58f, 0.80f, color.a);
            }
        }
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
        gameMenuController?.NotifyHistoryClosed();
    }

    private void SetBacklogOverlayActive(bool isActive)
    {
        if (backlogDimOverlay != null)
        {
            backlogDimOverlay.SetActive(isActive);
        }
    }

    private void ApplyBacklogPresentation()
    {
        if (backlogText == null)
        {
            return;
        }

        backlogText.alignment = TextAlignmentOptions.TopLeft;
        backlogText.margin = new Vector4(10f, 12f, 40f, 12f);
        backlogText.lineSpacing = 8f;
        backlogText.enableWordWrapping = true;
        backlogText.overflowMode = TextOverflowModes.Overflow;

        if (backlogCloseButton != null)
        {
            ColorBlock colors = backlogCloseButton.colors;
            colors.normalColor = new Color(0.04f, 0.14f, 0.21f, 0.94f);
            colors.highlightedColor = new Color(0.10f, 0.27f, 0.36f, 0.98f);
            colors.pressedColor = new Color(0.12f, 0.32f, 0.42f, 1f);
            colors.selectedColor = new Color(0.10f, 0.27f, 0.36f, 0.98f);
            colors.colorMultiplier = 1f;
            backlogCloseButton.colors = colors;
            if (backlogCloseButton.targetGraphic is Image image)
            {
                image.color = colors.normalColor;
            }
        }
    }

    private void ApplyChoicePresentation()
    {
        if (choiceButtons == null)
        {
            return;
        }

        const float spacing = 10f;
        var visibleButtons = new List<Button>();
        var heights = new List<float>();
        foreach (Button button in choiceButtons)
        {
            if (button == null || !button.gameObject.activeSelf)
            {
                continue;
            }

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.margin = new Vector4(28f, 8f, 28f, 8f);
                label.enableWordWrapping = true;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.fontSize = Mathf.Max(label.fontSize, 20f);
            }

            RectTransform rect = button.transform as RectTransform;
            float height = 54f;
            if (rect != null && label != null)
            {
                float textWidth = Mathf.Max(1f, rect.rect.width - label.margin.x - label.margin.z);
                float preferredTextHeight = label.GetPreferredValues(label.text, textWidth, 0f).y;
                height = Mathf.Clamp(preferredTextHeight + label.margin.y + label.margin.w, 54f, 64f);
            }

            visibleButtons.Add(button);
            heights.Add(height);
        }

        float totalHeight = Mathf.Max(0f, (visibleButtons.Count - 1) * spacing);
        foreach (float height in heights)
        {
            totalHeight += height;
        }

        RectTransform panelRect = choicePanel != null ? choicePanel.transform as RectTransform : null;
        float currentTop = panelRect != null ? panelRect.rect.height * 0.5f - 22f : totalHeight * 0.5f;
        for (int i = 0; i < visibleButtons.Count; i++)
        {
            Button button = visibleButtons[i];
            RectTransform rect = button.transform as RectTransform;
            if (rect != null)
            {
                float height = heights[i];
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, currentTop - height * 0.5f);
                currentTop -= height + spacing;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.025f, 0.06f, 0.12f, 0.88f);
            colors.highlightedColor = new Color(0.08f, 0.18f, 0.25f, 0.96f);
            colors.pressedColor = new Color(0.12f, 0.30f, 0.40f, 1f);
            colors.selectedColor = new Color(0.08f, 0.20f, 0.29f, 0.98f);
            colors.disabledColor = new Color(0.02f, 0.02f, 0.03f, 0.35f);
            colors.colorMultiplier = 1f;
            button.colors = colors;

            if (button.GetComponent<ChoicePointerFocus>() == null)
            {
                button.gameObject.AddComponent<ChoicePointerFocus>();
            }

            Outline outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = new Color(0.44f, 0.78f, 0.98f, 0.52f);
                outline.effectDistance = new Vector2(1f, -1f);
            }

            foreach (Image image in button.GetComponentsInChildren<Image>(true))
            {
                if (image != button.targetGraphic && image.color.r > image.color.g * 1.3f)
                {
                    image.color = new Color(0.34f, 0.72f, 0.94f, image.color.a);
                }
            }
        }

        if (choicePanel != null)
        {
            foreach (Image image in choicePanel.GetComponentsInChildren<Image>(true))
            {
                if (image.GetComponentInParent<Button>(true) == null
                    && image.color.r > image.color.g * 1.3f)
                {
                    image.color = new Color(0.30f, 0.66f, 0.90f, image.color.a);
                }
            }
        }
    }

    private void EnsureChoiceButtonCapacity()
    {
        var buttons = new List<Button> { choiceMashaButton, choiceArtemButton, choiceLeraButton };
        buttons.RemoveAll(button => button == null);
        while (buttons.Count < SupportedChoiceButtonCapacity && buttons.Count > 0)
        {
            Button template = buttons[buttons.Count - 1];
            Button duplicate = Instantiate(template, template.transform.parent);
            duplicate.name = "Choice Runtime Slot " + (buttons.Count + 1);
            duplicate.onClick.RemoveAllListeners();
            duplicate.gameObject.SetActive(false);
            buttons.Add(duplicate);
        }

        choiceButtons = buttons.ToArray();
    }

    private void ShowRelationshipCue(RelationshipCueKind cueKind)
    {
        if (cueKind == RelationshipCueKind.None)
        {
            return;
        }

        EnsureRelationshipCue();
        if (relationshipCueRoot == null)
        {
            return;
        }

        Color accent = cueKind == RelationshipCueKind.Positive
            ? new Color(0.35f, 0.84f, 0.68f, 1f)
            : cueKind == RelationshipCueKind.Negative
                ? new Color(0.90f, 0.42f, 0.48f, 1f)
                : new Color(0.94f, 0.76f, 0.34f, 1f);
        Image[] strokes = relationshipCueRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < strokes.Length; i++)
        {
            strokes[i].color = accent;
            RectTransform strokeRect = strokes[i].transform as RectTransform;
            if (strokeRect != null)
            {
                float rotation = cueKind == RelationshipCueKind.Mixed
                    ? 0f
                    : cueKind == RelationshipCueKind.Negative
                        ? (i == 0 ? 45f : -45f)
                        : (i == 0 ? -45f : 45f);
                strokeRect.localEulerAngles = new Vector3(0f, 0f, rotation);
                strokeRect.anchoredPosition = cueKind == RelationshipCueKind.Mixed
                    ? new Vector2(0f, i == 0 ? -5f : 5f)
                    : new Vector2(i == 0 ? -7f : 7f, 0f);
            }
        }

        relationshipCueCanvasGroup.alpha = 1f;
        relationshipCueRoot.transform.localScale = Vector3.one;
        relationshipCueRoot.SetActive(true);
        if (relationshipCueCoroutine != null)
        {
            StopCoroutine(relationshipCueCoroutine);
        }
        relationshipCueCoroutine = StartCoroutine(HideRelationshipCueAfterDelay());
    }

    private void EnsureRelationshipCue()
    {
        if (relationshipCueRoot != null || nameBox == null || dialogueUiRoot == null)
        {
            return;
        }

        relationshipCueRoot = new GameObject("Relationship Consequence Cue", typeof(RectTransform), typeof(CanvasGroup));
        relationshipCueRoot.transform.SetParent(dialogueUiRoot.transform.parent, false);
        RectTransform rootRect = relationshipCueRoot.transform as RectTransform;
        rootRect.anchorMin = new Vector2(0f, 0f);
        rootRect.anchorMax = new Vector2(0f, 0f);
        rootRect.pivot = new Vector2(0f, 0.5f);
        rootRect.anchoredPosition = new Vector2(390f, 390f);
        rootRect.sizeDelta = new Vector2(36f, 28f);
        relationshipCueCanvasGroup = relationshipCueRoot.GetComponent<CanvasGroup>();
        relationshipCueCanvasGroup.blocksRaycasts = false;
        relationshipCueCanvasGroup.interactable = false;

        Image choiceImage = choiceMashaButton != null ? choiceMashaButton.targetGraphic as Image : null;
        Sprite cueSprite = choiceImage != null ? choiceImage.sprite : null;
        for (int i = 0; i < 2; i++)
        {
            GameObject stroke = new GameObject("Cue Stroke", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            stroke.transform.SetParent(relationshipCueRoot.transform, false);
            Image image = stroke.GetComponent<Image>();
            image.sprite = cueSprite;
            image.raycastTarget = false;
            RectTransform rect = stroke.transform as RectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(i == 0 ? -7f : 7f, 0f);
            rect.sizeDelta = new Vector2(20f, 4f);
        }

        relationshipCueRoot.transform.SetAsLastSibling();
        relationshipCueRoot.SetActive(false);
    }

    private IEnumerator HideRelationshipCueAfterDelay()
    {
        yield return new WaitForSecondsRealtime(1.25f);

        if (relationshipCueRoot != null)
        {
            relationshipCueRoot.SetActive(false);
        }
        relationshipCueCoroutine = null;
    }

    private void FocusFirstVisibleChoice()
    {
        if (choiceButtons == null)
        {
            return;
        }

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            Button button = choiceButtons[i];
            if (button != null && button.gameObject.activeInHierarchy && button.interactable)
            {
                Focus(button);
                return;
            }
        }
    }

    private void RefreshChoiceFocusPresentation()
    {
        if (!showingChoice || choiceButtons == null)
        {
            return;
        }

        EventSystem eventSystem = EventSystem.current ?? UnityEngine.Object.FindFirstObjectByType<EventSystem>();
        GameObject selectedObject = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            Button button = choiceButtons[i];
            if (button == null || !button.gameObject.activeInHierarchy)
            {
                continue;
            }

            bool selected = button.gameObject == selectedObject;
            if (button.targetGraphic is Image targetImage)
            {
                targetImage.color = selected
                    ? new Color(0.08f, 0.23f, 0.32f, 0.98f)
                    : new Color(0.025f, 0.06f, 0.12f, 0.88f);
            }

            Outline outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = selected
                    ? new Color(0.58f, 0.90f, 1f, 0.96f)
                    : new Color(0.44f, 0.78f, 0.98f, 0.32f);
                outline.effectDistance = selected ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
            }

            foreach (Image image in button.GetComponentsInChildren<Image>(true))
            {
                if (image != button.targetGraphic)
                {
                    image.color = selected
                        ? new Color(0.48f, 0.86f, 1f, 1f)
                        : new Color(0.34f, 0.72f, 0.94f, 0.72f);
                }
            }
        }
    }

    private static void Focus(Selectable control)
    {
        if (control == null)
        {
            return;
        }

        EventSystem eventSystem = EventSystem.current ?? UnityEngine.Object.FindFirstObjectByType<EventSystem>();
        eventSystem?.SetSelectedGameObject(control.gameObject);
    }

    private sealed class ChoicePointerFocus : MonoBehaviour, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            Button button = GetComponent<Button>();
            if (button != null && button.isActiveAndEnabled && button.interactable)
            {
                (EventSystem.current ?? UnityEngine.Object.FindFirstObjectByType<EventSystem>())?.SetSelectedGameObject(button.gameObject);
            }
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
        StopSkipTimer();
        preferencesController?.Open();
        if (preferencesController != null && preferencesController.IsOpen)
        {
            SetQuickMenuPreferencesModalHidden(true);
        }
    }

    public void HideSettings()
    {
        preferencesController?.Close();
        vnSettingsPanel?.SetActive(false);
        vnSettingsDimOverlay?.SetActive(false);
    }

    private void ResumeAfterPreferencesClosed()
    {
        SetQuickMenuPreferencesModalHidden(false);
        StartAutoForwardDelayIfReady();
        StartSkipDelayIfReady();
        gameMenuController?.NotifyPreferencesClosed();
    }

    private void SetQuickMenuPreferencesModalHidden(bool hidden)
    {
        FindFirstObjectByType<VNQuickMenu>(FindObjectsInactive.Include)?.SetPreferencesModalHidden(hidden);
    }

    public bool OpenGameMenu()
    {
        return gameMenuController != null && gameMenuController.Open();
    }

    public void OnGameMenuOpened()
    {
        StopAutoForwardTimer();
        StopSkipTimer();
        FindFirstObjectByType<VNQuickMenu>(FindObjectsInactive.Include)?.SetGameMenuModalHidden(true);
    }

    public void OnGameMenuClosed()
    {
        FindFirstObjectByType<VNQuickMenu>(FindObjectsInactive.Include)?.SetGameMenuModalHidden(false);
        StartAutoForwardDelayIfReady();
        StartSkipDelayIfReady();
    }

    public void ResetSettings()
    {
        preferencesController?.Reset();
    }

    public void OnMasterVolumeChanged(float value)
    {
        preferencesController?.SetMasterVolume(value);
    }

    public void OnMusicVolumeChanged(float value)
    {
        preferencesController?.SetMusicVolume(value);
    }

    public void OnSfxVolumeChanged(float value)
    {
        preferencesController?.SetSfxVolume(value);
    }

    public void OnTextSpeedChanged(float value)
    {
        preferencesController?.SetTextSpeed(value);
    }

    public void OnFullscreenChanged(bool value)
    {
        preferencesController?.SetFullscreen(value);
    }

    /// <summary>Applies only the approved dialogue text and textbox background consumers.</summary>
    public void RefreshDialoguePresentation()
    {
        GameSettings presentationSettings = SettingsManager.Instance != null
            ? SettingsManager.Instance.CurrentSettings
            : new GameSettings();

        if (dialogueText != null)
        {
            if (dialogueBaseFontSize <= 0f)
            {
                dialogueBaseFontSize = dialogueText.fontSize;
            }

        }

        if (dialogueUiRoot != null && dialogueBoxBackground == null)
        {
            dialogueBoxBackground = dialogueUiRoot.GetComponent<Image>();
        }

        HideTemporaryReadingChrome();
        ApplyReadingShellPresentation();
        ApplyDialoguePresentation(dialogueText, dialogueBoxBackground, dialogueBaseFontSize, presentationSettings);
    }

    /// <summary>Keeps the ordinary reading shell readable at the supported text-scale range without serializing the scene.</summary>
    private void ApplyReadingShellPresentation()
    {
        if (dialogueUiRoot == null || dialogueText == null)
        {
            return;
        }

        RectTransform boxRect = dialogueUiRoot.transform as RectTransform;
        RectTransform textRect = dialogueText.rectTransform;
        if (boxRect == null || textRect == null)
        {
            return;
        }

        if (!readingPresentationInitialized)
        {
            dialogueBaseBoxSize = boxRect.sizeDelta;
            dialogueBaseTextSize = textRect.sizeDelta;
            readingPresentationInitialized = true;
        }

        // The authored 90 px text area clips several ordinary lines at 125%.
        // Reserve height rather than weakening the existing accessibility setting.
        boxRect.sizeDelta = new Vector2(dialogueBaseBoxSize.x, Mathf.Max(dialogueBaseBoxSize.y, 360f));
        textRect.sizeDelta = new Vector2(dialogueBaseTextSize.x, -80f);
        dialogueText.margin = new Vector4(4f, 12f, 4f, 8f);
        dialogueText.alignment = TextAlignmentOptions.TopLeft;
        dialogueText.lineSpacing = 5f;
        dialogueText.enableWordWrapping = true;
        dialogueText.overflowMode = TextOverflowModes.Masking;
        dialogueText.color = new Color(0.96f, 0.97f, 0.98f, 1f);

        if (dialogueBoxBackground != null)
        {
            Color textboxColor = dialogueBoxBackground.color;
            dialogueBoxBackground.color = new Color(0.025f, 0.035f, 0.05f, textboxColor.a);
        }

        if (nameBox != null)
        {
            RectTransform nameRect = nameBox.transform as RectTransform;
            if (nameRect != null)
            {
                nameRect.sizeDelta = new Vector2(280f, 52f);
                nameRect.anchoredPosition = new Vector2(38f, 24f);
            }

            Image nameBackground = nameBox.GetComponent<Image>();
            if (nameBackground != null)
            {
                nameBackground.color = new Color(0.025f, 0.035f, 0.05f, 0.94f);
            }
        }

        if (speakerText != null)
        {
            speakerText.fontStyle = FontStyles.Bold;
            speakerText.color = new Color(0.74f, 0.90f, 1f, 1f);
            speakerText.alignment = TextAlignmentOptions.Left;
        }

        TextMeshProUGUI advanceIndicator = nextButton != null
            ? nextButton.GetComponentInChildren<TextMeshProUGUI>(true)
            : null;
        if (advanceIndicator != null)
        {
            advanceIndicator.color = new Color(0.60f, 0.82f, 0.96f, 0.85f);
        }

        ApplyChoicePresentation();
        ApplyBacklogPresentation();
    }

    public static void ApplyDialoguePresentation(
        TextMeshProUGUI targetDialogueText,
        Image targetTextboxBackground,
        float baseFontSize,
        GameSettings settings)
    {
        GameSettings source = settings ?? new GameSettings();
        if (targetDialogueText != null && baseFontSize > 0f)
        {
            targetDialogueText.fontSize = baseFontSize * Mathf.Clamp(source.dialogueTextScale, 0.85f, 1.25f);
        }

        if (targetTextboxBackground != null)
        {
            Color color = targetTextboxBackground.color;
            color.a = Mathf.Clamp01(source.textboxOpacity);
            targetTextboxBackground.color = color;
        }
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

        preferencesController?.Hide();

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

    public void ShowConfirmExitFromGameMenu()
    {
        mainMenuConfirmationOpenedFromGameMenu = true;
        ShowConfirmExit();
        if (confirmExitPanel == null || !confirmExitPanel.activeSelf)
        {
            mainMenuConfirmationOpenedFromGameMenu = false;
        }
    }

    public void HideConfirmExit()
    {
        if (confirmExitPanel != null)
        {
            confirmExitPanel.SetActive(false);
        }

        StartAutoForwardDelayIfReady();
        if (mainMenuConfirmationOpenedFromGameMenu)
        {
            mainMenuConfirmationOpenedFromGameMenu = false;
            gameMenuController?.NotifyMainMenuConfirmationClosed();
        }
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

        float charsPerSecond = GetCharactersPerSecond(textSpeed);
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

    public static float GetCharactersPerSecond(float storedTextSpeed)
    {
        return Mathf.Clamp(storedTextSpeed, 20f, 100f);
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
        SettingsManager.DialoguePresentationChanged -= RefreshDialoguePresentation;
        isRuntimeReady = false;
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
