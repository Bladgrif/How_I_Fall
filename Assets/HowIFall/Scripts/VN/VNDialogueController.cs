using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class VNDialogueController : MonoBehaviour
{
    public static VNDialogueController Instance { get; private set; }

    private const string MissingSceneDataText = "Dialogue scene data is missing.";
    private const string EndPrototypeText = "Конец Unity-прототипа.";

    public DialogueSceneData sceneData;
    public DialogueSceneRegistry sceneRegistry;

    public TextMeshProUGUI speakerText;
    public TextMeshProUGUI dialogueText;
    public Image backgroundImage;
    public Image characterImage;
    public GameObject nameBox;
    public Button nextButton;
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
    public Toggle vnFullscreenToggle;
    public Button vnSettingsCloseButton;
    public Button vnSettingsResetButton;
    public ManualSaveLoadPanel manualSaveLoadPanel;
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
    private Button[] choiceButtons;
    private DialogueSceneData pendingNextScene;
    private Coroutine typingCoroutine;
    private Coroutine notificationCoroutine;
    private string currentFullText = string.Empty;
    private bool isTyping;
    private readonly DialogueBacklog backlog = new DialogueBacklog(100);
    private VNSettingsPresenter settingsPresenter;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"[VN] Duplicate VNDialogueController detected on '{gameObject.name}'.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (!ValidateRequiredUiReferences())
        {
            enabled = false;
            return;
        }

        GameState gameState = GameState.EnsureInstance();
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
            vnFullscreenToggle,
            vnSettingsCloseButton,
            vnSettingsResetButton,
            new VNSettingsService(),
            ShowToast,
            this);
        settingsPresenter.Initialize();

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

        if (saveManager.HasPendingSceneRestore)
        {
            int pendingSlotIndex = saveManager.PendingSlotIndex;
            if (RestoreFromGameState())
            {
                saveManager.CompletePendingSceneRestore();
                return;
            }

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
        if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
        {
            manualSaveLoadPanel?.OpenSave();
        }

        if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
        {
            manualSaveLoadPanel?.OpenLoad();
        }

        if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
        {
            ShowBacklog();
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
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
                manualSaveLoadPanel.Close();
                return;
            }
        }
    }

    public void AdvanceDialogue()
    {
        if (IsAdvanceBlockedByOpenPanel())
        {
            return;
        }

        ShowNextLine();
    }

    private bool IsAdvanceBlockedByOpenPanel()
    {
        return (choicePanel != null && choicePanel.activeSelf)
            || (backlogPanel != null && backlogPanel.activeSelf)
            || (confirmExitPanel != null && confirmExitPanel.activeSelf)
            || (vnSettingsPanel != null && vnSettingsPanel.activeSelf)
            || (manualSaveLoadPanel != null && manualSaveLoadPanel.IsOpen);
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
            showingEndLine = true;
            ShowNarration(EndPrototypeText);
            return;
        }

        if (showingEndLine || activeLines == null)
        {
            return;
        }

        currentLineIndex++;
        UpdateSavedDialoguePosition();

        if (currentLineIndex >= activeLines.Count)
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

            showingEndLine = true;
            ShowNarration(EndPrototypeText);
            return;
        }

        ShowLine(activeLines[currentLineIndex]);
    }

    private void ShowChoices()
    {
        if (activeChoices.Count == 0)
        {
            if (sceneData != null && sceneData.defaultNextScene != null)
            {
                ClearChoiceState();
                LoadDialogueScene(sceneData.defaultNextScene);
                return;
            }

            showingEndLine = true;
            ShowNarration(EndPrototypeText);
            return;
        }

        showingChoice = true;
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

            bool hasChoice = i < activeChoices.Count;
            choiceButtons[i].gameObject.SetActive(hasChoice);

            if (hasChoice)
            {
                SetButtonText(choiceButtons[i], activeChoices[i].text);
            }
        }
    }

    private void Choose(int choiceIndex)
    {
        if (choiceIndex < 0 || choiceIndex >= activeChoices.Count)
        {
            return;
        }

        DialogueChoice choice = activeChoices[choiceIndex];
        GameState gameState = GameState.EnsureInstance();
        gameState.ApplyChoice(choice);
        gameState.selectedChoiceIndex = choiceIndex;
        gameState.choiceResultActive = true;
        pendingNextScene = choice.nextScene != null ? choice.nextScene : sceneData.defaultNextScene;
        gameState.pendingNextSceneId = pendingNextScene != null ? pendingNextScene.sceneId : string.Empty;
        ShowFinalLine(choice.resultText);
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
        bool hasSpeaker = !string.IsNullOrWhiteSpace(line.speaker);
        nameBox.SetActive(hasSpeaker);
        speakerText.text = hasSpeaker ? line.speaker : string.Empty;
        AddToBacklog(line.speaker, line.text);
        ShowText(line.text);
        ApplyVisuals(line);
    }

    private void ShowNarration(string text)
    {
        nameBox.SetActive(false);
        speakerText.text = string.Empty;
        AddToBacklog(string.Empty, text);
        ShowText(text);
    }

    private void AddToBacklog(string speaker, string text)
    {
        backlog.Add(speaker, text);
    }

    public void ShowBacklog()
    {
        if (backlogPanel == null || backlogText == null)
        {
            Debug.LogWarning("VNDialogueController: backlogPanel or backlogText is not assigned.", this);
            return;
        }

        backlogText.text = backlog.BuildRichText();
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
        settingsPresenter?.Open();
    }

    public void HideSettings()
    {
        settingsPresenter?.Hide();
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

        SceneFlowManager.EnsureInstance().ReturnToMainMenu();
    }

    public void ShowConfirmExit()
    {
        if (confirmExitPanel == null)
        {
            ReturnToMainMenu();
            return;
        }

        confirmExitPanel.SetActive(true);
    }

    public void HideConfirmExit()
    {
        if (confirmExitPanel != null)
        {
            confirmExitPanel.SetActive(false);
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

        float charsPerSecond = baseCharactersPerSecond * Mathf.Max(0.1f, textSpeed);
        float characterDelay = 1f / charsPerSecond;

        foreach (char character in text)
        {
            dialogueText.text += character;
            yield return new WaitForSeconds(characterDelay);
        }

        dialogueText.text = text;
        isTyping = false;
        typingCoroutine = null;
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

            if (!string.IsNullOrEmpty(restoredPendingNextSceneId))
            {
                restoredPendingNextScene = sceneRegistry.FindById(restoredPendingNextSceneId);
                if (restoredPendingNextScene == null
                    || (configuredNextScene != null && restoredPendingNextScene != configuredNextScene))
                {
                    Debug.LogWarning($"[VN LOAD] Pending scene '{restoredPendingNextSceneId}' is invalid for choice {restoredChoiceIndex}. No dialogue state was changed.", this);
                    return false;
                }
            }
            else if (configuredNextScene != null)
            {
                restoredPendingNextScene = sceneRegistry.FindById(configuredNextScene.sceneId);
                if (restoredPendingNextScene != configuredNextScene)
                {
                    Debug.LogWarning($"[VN LOAD] Configured choice target '{configuredNextScene.sceneId}' is absent from the registry. No dialogue state was changed.", this);
                    return false;
                }
            }
        }

        Debug.Log($"[VN LOAD] Preflight passed. requestedLineId='{gameState.currentLineId}', resolvedIndex={restoredLineIndex}, resolvedLineId='{restoredScene.lines[restoredLineIndex].lineId}'.", this);

        LoadDialogueScene(restoredScene, restoredLineIndex);

        if (restoreChoiceResult)
        {
            RestoreChoiceResult(restoredChoice, restoredChoiceIndex, restoredPendingNextScene);
        }

        Debug.Log($"[VN LOAD] Restoration finished. activeSceneId='{sceneData.sceneId}', activeLineIndex={currentLineIndex}, activeLineId='{(activeLines != null && currentLineIndex >= 0 && currentLineIndex < activeLines.Count && activeLines[currentLineIndex] != null ? activeLines[currentLineIndex].lineId : "<invalid>")}', choiceResultActive={GameState.Instance.choiceResultActive}.", this);
        return true;
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

    private void LoadDialogueScene(DialogueSceneData data)
    {
        LoadDialogueScene(data, 0);
    }

    private void LoadDialogueScene(DialogueSceneData data, int startLineIndex)
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        currentFullText = string.Empty;
        isTyping = false;

        if (data == null)
        {
            Debug.LogError("Dialogue scene data is missing.", this);
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

        ShowLine(activeLines[currentLineIndex]);
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
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
