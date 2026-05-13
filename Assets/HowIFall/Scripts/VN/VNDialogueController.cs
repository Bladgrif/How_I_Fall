using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VNDialogueController : MonoBehaviour
{
    private const string MissingSceneDataText = "Dialogue scene data is missing.";
    private const string EndPrototypeText = "Конец Unity-прототипа.";

    public DialogueSceneData sceneData;

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

    public Vector2 characterLeftPosition = new Vector2(-420f, -220f);
    public Vector2 characterCenterPosition = new Vector2(0f, -220f);
    public Vector2 characterRightPosition = new Vector2(420f, -220f);
    public Vector2 characterSoloPosition = new Vector2(-140f, -220f);
    public Vector2 characterDefaultSize = new Vector2(850f, 1200f);

    public VNStats stats;

    private int currentLineIndex;
    private bool showingChoice;
    private bool showingFinalLine;
    private bool showingEndLine;
    private string finalLineText;
    private List<DialogueLine> activeLines;
    private List<DialogueChoice> activeChoices;
    private Button[] choiceButtons;

    private void Start()
    {
        if (!ValidateRequiredUiReferences())
        {
            enabled = false;
            return;
        }

        if (stats == null)
        {
            stats = GetComponent<VNStats>();
        }

        choiceButtons = new[] { choiceMashaButton, choiceArtemButton, choiceLeraButton };

        nextButton.onClick.AddListener(ShowNextLine);

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] == null)
            {
                Debug.LogWarning($"Choice button at index {i} is not assigned. This choice slot will be skipped.", this);
                continue;
            }

            int choiceIndex = i;
            choiceButtons[i].onClick.AddListener(() => Choose(choiceIndex));
        }

        currentLineIndex = 0;
        showingChoice = false;
        showingFinalLine = false;
        showingEndLine = false;

        choicePanel.SetActive(false);

        if (sceneData == null || sceneData.lines == null || sceneData.lines.Count == 0)
        {
            Debug.LogError("Dialogue scene data is missing or empty.", this);
            ShowNarration(MissingSceneDataText);
            return;
        }

        activeLines = sceneData.lines;
        activeChoices = sceneData.choices ?? new List<DialogueChoice>();

        ShowLine(activeLines[currentLineIndex]);
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
            showingEndLine = true;
            ShowNarration(EndPrototypeText);
            return;
        }

        if (showingEndLine || activeLines == null)
        {
            return;
        }

        currentLineIndex++;

        if (currentLineIndex >= activeLines.Count)
        {
            ShowChoices();
            return;
        }

        ShowLine(activeLines[currentLineIndex]);
    }

    private void ShowChoices()
    {
        if (activeChoices.Count == 0)
        {
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
        ApplyChoice(choice);
        ShowFinalLine(choice.resultText);
    }

    private void ApplyChoice(DialogueChoice choice)
    {
        if (stats == null)
        {
            return;
        }

        stats.lust += choice.lustDelta;
        stats.romance += choice.romanceDelta;
        stats.purity += choice.purityDelta;
        stats.corruptionLevel += choice.corruptionDelta;
        stats.selfControl += choice.selfControlDelta;
        stats.suspicion += choice.suspicionDelta;
        stats.trustMasha += choice.trustMashaDelta;
        stats.trustArtem += choice.trustArtemDelta;
        stats.leraInterest += choice.leraInterestDelta;
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
}
