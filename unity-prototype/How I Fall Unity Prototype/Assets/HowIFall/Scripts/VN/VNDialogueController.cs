using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VNDialogueController : MonoBehaviour
{
    public List<DialogueLine> lines = new List<DialogueLine>();

    public TextMeshProUGUI speakerText;
    public TextMeshProUGUI dialogueText;
    public GameObject nameBox;
    public Button nextButton;
    public GameObject choicePanel;
    public Button choiceMashaButton;
    public Button choiceArtemButton;
    public Button choiceLeraButton;
    public VNStats stats;

    private int currentLineIndex;
    private bool showingChoice;
    private bool showingFinalLine;
    private bool showingEndLine;
    private string finalLineText;

    private void Awake()
    {
        if (lines.Count == 0)
        {
            lines.Add(new DialogueLine
            {
                speaker = string.Empty,
                text = "Утро у школьных ворот пахнет мокрым асфальтом и чем-то сладким из киоска через дорогу."
            });
            lines.Add(new DialogueLine
            {
                speaker = "Маша",
                text = "Ты всё-таки пришёл."
            });
            lines.Add(new DialogueLine
            {
                speaker = string.Empty,
                text = "Маша улыбается осторожно, как будто проверяет, не исчезну ли я прямо перед ней."
            });
            lines.Add(new DialogueLine
            {
                speaker = "Маша",
                text = "Я думала, после вчерашней вечеринки ты проспишь первый урок."
            });
            lines.Add(new DialogueLine
            {
                speaker = string.Empty,
                text = "Я хотел ответить шуткой, но в памяти вспыхивает только круг свечей и чей-то тихий смех."
            });
        }
    }

    private void Start()
    {
        if (stats == null)
        {
            stats = GetComponent<VNStats>();
        }

        nextButton.onClick.AddListener(ShowNextLine);
        choiceMashaButton.onClick.AddListener(ChooseMasha);
        choiceArtemButton.onClick.AddListener(ChooseArtem);
        choiceLeraButton.onClick.AddListener(ChooseLera);

        currentLineIndex = 0;
        showingChoice = false;
        showingFinalLine = false;
        showingEndLine = false;

        choicePanel.SetActive(false);
        ShowLine(lines[currentLineIndex]);
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
            ShowNarration("Конец Unity-прототипа.");
            return;
        }

        if (showingEndLine)
        {
            return;
        }

        currentLineIndex++;

        if (currentLineIndex >= lines.Count)
        {
            ShowChoices();
            return;
        }

        ShowLine(lines[currentLineIndex]);
    }

    private void ShowChoices()
    {
        showingChoice = true;
        nextButton.interactable = false;
        choicePanel.SetActive(true);
    }

    private void ChooseMasha()
    {
        stats.romance += 1;
        stats.trustMasha += 1;
        stats.selfControl += 1;
        ShowFinalLine("Я поворачиваюсь к Маше и заставляю себя говорить ровно.");
    }

    private void ChooseArtem()
    {
        stats.lust += 1;
        stats.trustArtem += 1;
        stats.selfControl -= 1;
        ShowFinalLine("Если у Артёма есть план побега, сейчас самое время показать карту.");
    }

    private void ChooseLera()
    {
        stats.suspicion += 1;
        stats.purity += 1;
        ShowFinalLine("Я всё ещё думаю о вчерашнем ритуале. И это пугает сильнее, чем должно.");
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
    }

    private void ShowNarration(string text)
    {
        nameBox.SetActive(false);
        speakerText.text = string.Empty;
        dialogueText.text = text;
    }
}
