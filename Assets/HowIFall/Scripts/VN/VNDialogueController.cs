using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class VNDialogueController : MonoBehaviour
{
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
    public GameObject choicePanel;
    public Button choiceMashaButton;
    public Button choiceArtemButton;
    public Button choiceLeraButton;
    public AudioClip uiClickSfx;

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

    private void Start()
    {
        if (!ValidateRequiredUiReferences())
        {
            enabled = false;
            return;
        }

        GameState gameState = GameState.EnsureInstance();

        choiceButtons = new[] { choiceMashaButton, choiceArtemButton, choiceLeraButton };

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
        dialogueText.text = line.text;
        ApplyVisuals(line);
    }

    private void ShowNarration(string text)
    {
        nameBox.SetActive(false);
        speakerText.text = string.Empty;
        dialogueText.text = text;
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
