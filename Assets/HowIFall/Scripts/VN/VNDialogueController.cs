using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class VNDialogueController : MonoBehaviour
{
    private const string MissingSceneDataText = "Dialogue scene data is missing.";
    private const int MaxBacklogEntries = 100;
    private const string EndPrototypeText = "Конец Unity-прототипа.";

    public DialogueSceneData sceneData;
    public DialogueSceneRegistry sceneRegistry;

    public TextMeshProUGUI speakerText;
    public TextMeshProUGUI dialogueText;
    public Image backgroundImage;
    public Image characterImage;
    public GameObject nameBox;
    public Button nextButton;
    public GameObject choicePanel;
    public Button choiceMashaButton;
    public Button choiceArtemButton;
    public Button choiceLeraButton;
    public GameObject backlogPanel;
    public TextMeshProUGUI backlogText;
    public Button backlogCloseButton;
    public AudioClip uiClickSfx;
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
    private string currentFullText = string.Empty;
    private bool isTyping;
    private readonly List<DialogueBacklogEntry> backlog = new List<DialogueBacklogEntry>();

    private void Start()
    {
        if (!ValidateRequiredUiReferences())
        {
            enabled = false;
            return;
        }

        GameState gameState = GameState.EnsureInstance();

        choiceButtons = new[] { choiceMashaButton, choiceArtemButton, choiceLeraButton };

        if (backlogPanel != null)
        {
            backlogPanel.SetActive(false);
        }

        if (backlogCloseButton != null)
        {
            backlogCloseButton.onClick.AddListener(HideBacklog);
        }

        nextButton.onClick.AddListener(() =>
        {
            PlayUiClick();
            ShowNextLine();
        });

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
                PlayUiClick();
                Choose(choiceIndex);
            });
        }

        if (gameState.hasLoadedSave && !string.IsNullOrEmpty(gameState.currentSceneId) && sceneRegistry != null)
        {
            RestoreFromGameState();
            return;
        }

        LoadDialogueScene(sceneData);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Save();
            }
        }

        if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
        {
            if (SaveManager.Instance != null)
            {
                if (SaveManager.Instance.Load())
                {
                    RestoreFromGameState();
                }
                else
                {
                    Debug.LogWarning("No save file found.");
                }
            }
        }

        if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
        {
            ShowBacklog();
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && backlogPanel != null && backlogPanel.activeSelf)
        {
            HideBacklog();
        }
    }

    private void PlayUiClick()
    {
        if (uiClickSfx != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(uiClickSfx);
        }
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
                LoadDialogueScene(nextSceneData);
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

        currentLineIndex++;
        GameState.EnsureInstance().currentLineIndex = currentLineIndex;

        if (currentLineIndex >= activeLines.Count)
        {
            if (activeChoices.Count > 0)
            {
                ShowChoices();
                return;
            }

            if (sceneData != null && sceneData.defaultNextScene != null)
            {
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
                LoadDialogueScene(sceneData.defaultNextScene);
                return;
            }

            showingEndLine = true;
            ShowNarration(EndPrototypeText);
            return;
        }

        showingChoice = true;
        nextButton.interactable = false;
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
        GameState.EnsureInstance().ApplyChoice(choice);
        pendingNextScene = choice.nextScene != null ? choice.nextScene : sceneData.defaultNextScene;
        ShowFinalLine(choice.resultText);
    }

    private void ShowFinalLine(string text)
    {
        finalLineText = text;
        showingChoice = false;
        showingFinalLine = true;
        choicePanel.SetActive(false);
        nextButton.interactable = true;
        ShowNarration(finalLineText);
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
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        backlog.Add(new DialogueBacklogEntry
        {
            speaker = speaker,
            text = text
        });

        while (backlog.Count > MaxBacklogEntries)
        {
            backlog.RemoveAt(0);
        }
    }

    public void ShowBacklog()
    {
        if (backlogPanel == null || backlogText == null)
        {
            Debug.LogWarning("VNDialogueController: backlogPanel or backlogText is not assigned.", this);
            return;
        }

        List<string> lines = new List<string>();

        foreach (DialogueBacklogEntry entry in backlog)
        {
            if (string.IsNullOrWhiteSpace(entry.speaker))
            {
                lines.Add(entry.text);
            }
            else
            {
                lines.Add($"{entry.speaker}: {entry.text}");
            }
        }

        backlogText.text = string.Join("\n\n", lines);
        backlogPanel.SetActive(true);
    }

    public void HideBacklog()
    {
        if (backlogPanel != null)
        {
            backlogPanel.SetActive(false);
        }
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

    public void RestoreFromGameState()
    {
        GameState gameState = GameState.Instance;

        if (gameState == null)
        {
            Debug.LogWarning("VNDialogueController: GameState.Instance is missing.");
            return;
        }

        if (sceneRegistry == null)
        {
            Debug.LogWarning("VNDialogueController: sceneRegistry is not assigned.", this);
            return;
        }

        DialogueSceneData restoredScene = sceneRegistry.FindById(gameState.currentSceneId);

        if (restoredScene == null)
        {
            Debug.LogWarning($"VNDialogueController: scene '{gameState.currentSceneId}' was not found in registry.", this);
            return;
        }

        LoadDialogueScene(restoredScene, gameState.currentLineIndex);
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
        nextButton.interactable = true;
        GameState gameState = GameState.EnsureInstance();
        gameState.currentSceneId = sceneData.sceneId;
        gameState.currentLineIndex = currentLineIndex;
        ShowLine(activeLines[currentLineIndex]);
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
}
