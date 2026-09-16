using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Focused non-serialized checks for the ordinary VN reading loop.</summary>
public static class ReadingExperienceSmokeTests
{
    [MenuItem("How I Fall/Tests/Run Reading Experience Smoke Tests")]
    public static void RunFromMenu()
    {
        RunBatchMode();
        Debug.Log("How I Fall reading experience smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        VerifyPresentationSettingsRange();
        VerifyTypewriterAdvanceContract();
        VerifyAdaptiveReadingShellAndNamePlate();
        VerifyChoiceFocusAndSingleActivation();
    }

    private static void VerifyPresentationSettingsRange()
    {
        GameObject owner = new GameObject("ReadingPresentationSmoke", typeof(RectTransform));
        GameObject textOwner = new GameObject("Dialogue Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        GameObject backgroundOwner = new GameObject("Dialogue Background", typeof(RectTransform), typeof(Image));
        try
        {
            TextMeshProUGUI text = textOwner.GetComponent<TextMeshProUGUI>();
            Image background = backgroundOwner.GetComponent<Image>();
            background.color = new Color(0.02f, 0.04f, 0.08f, 0.62f);

            VNDialogueController.ApplyDialoguePresentation(text, background, 32f,
                new GameSettings { dialogueTextScale = 0.85f, textboxOpacity = 0f });
            Require(Mathf.Approximately(text.fontSize, 27.2f), "Dialogue text must keep the supported 0.85x scale.");
            Require(Mathf.Approximately(background.color.a, 0f), "Textbox opacity 0 must remain visible as a supported setting result.");

            VNDialogueController.ApplyDialoguePresentation(text, background, 32f,
                new GameSettings { dialogueTextScale = 1.25f, textboxOpacity = 1f });
            Require(Mathf.Approximately(text.fontSize, 40f), "Dialogue text must keep the supported 1.25x scale.");
            Require(Mathf.Approximately(background.color.a, 1f), "Textbox opacity 1 must remain supported.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(backgroundOwner);
            UnityEngine.Object.DestroyImmediate(textOwner);
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    private static void VerifyTypewriterAdvanceContract()
    {
        GameObject gameStateOwner = new GameObject("ReadingTypewriterSmokeGameState", typeof(GameState));
        GameObject controllerOwner = new GameObject("ReadingTypewriterSmokeController");
        GameObject nameBox = new GameObject("ReadingTypewriterSmokeNameBox");
        GameObject speakerOwner = new GameObject("ReadingTypewriterSmokeSpeaker", typeof(RectTransform), typeof(TextMeshProUGUI));
        GameObject textOwner = new GameObject("ReadingTypewriterSmokeText", typeof(RectTransform), typeof(TextMeshProUGUI));
        DialogueSceneData scene = ScriptableObject.CreateInstance<DialogueSceneData>();
        try
        {
            scene.sceneId = "TECH_DEMO_TYPEWRITER";
            scene.lines = new List<DialogueLine>
            {
                new DialogueLine { lineId = "line_1", text = "Первая TECH DEMO ONLY строка" },
                new DialogueLine { lineId = "line_2", text = "Вторая TECH DEMO ONLY строка" }
            };

            VNDialogueController controller = controllerOwner.AddComponent<VNDialogueController>();
            controller.nameBox = nameBox;
            controller.speakerText = speakerOwner.GetComponent<TextMeshProUGUI>();
            controller.dialogueText = textOwner.GetComponent<TextMeshProUGUI>();
            SetPrivate(controller, "sceneData", scene);
            SetPrivate(controller, "activeLines", scene.lines);
            SetPrivate(controller, "activeChoices", new List<DialogueChoice>());
            SetPrivate(controller, "currentLineIndex", 0);
            SetPrivate(controller, "currentFullText", scene.lines[0].text);
            SetPrivate(controller, "isTyping", true);

            controller.AdvanceDialogue();
            Require(GetPrivate<int>(controller, "currentLineIndex") == 0,
                "The first Advance while typing must complete only the current line.");
            Require(controller.dialogueText.text == scene.lines[0].text,
                "The first Advance while typing must reveal the complete current line.");

            controller.AdvanceDialogue();
            Require(GetPrivate<int>(controller, "currentLineIndex") == 1,
                "The next Advance after type completion must progress exactly once.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(scene);
            UnityEngine.Object.DestroyImmediate(textOwner);
            UnityEngine.Object.DestroyImmediate(speakerOwner);
            UnityEngine.Object.DestroyImmediate(nameBox);
            UnityEngine.Object.DestroyImmediate(controllerOwner);
            UnityEngine.Object.DestroyImmediate(gameStateOwner);
        }
    }

    private static void VerifyChoiceFocusAndSingleActivation()
    {
        GameObject eventSystemOwner = new GameObject("ReadingChoiceSmokeEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        EventSystem eventSystem = eventSystemOwner.GetComponent<EventSystem>();
        MethodInfo onEnable = typeof(EventSystem).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(onEnable != null, "EventSystem.OnEnable is missing.");
        onEnable.Invoke(eventSystem, null);
        EventSystem.current = eventSystem;
        GameObject gameStateOwner = new GameObject("ReadingChoiceSmokeGameState", typeof(GameState));
        GameObject controllerOwner = new GameObject("ReadingChoiceSmokeController");
        GameObject choicePanel = new GameObject("ReadingChoiceSmokePanel", typeof(RectTransform));
        GameObject nameBox = new GameObject("ReadingChoiceSmokeNameBox");
        GameObject dialogueTextOwner = new GameObject("ReadingChoiceSmokeText", typeof(RectTransform), typeof(TextMeshProUGUI));
        var temporaryAssets = new List<UnityEngine.Object>();
        VNDialogueController controller = null;
        Button fourthButton = null;

        try
        {
            controller = controllerOwner.AddComponent<VNDialogueController>();
            controller.choicePanel = choicePanel;
            controller.nameBox = nameBox;
            controller.dialogueText = dialogueTextOwner.GetComponent<TextMeshProUGUI>();
            controller.speakerText = controller.dialogueText;
            controller.nextButton = CreateButton("ReadingChoiceSmokeNext");
            controller.choiceMashaButton = CreateButton("ReadingChoiceSmokeOne");
            controller.choiceArtemButton = CreateButton("ReadingChoiceSmokeTwo");
            controller.choiceLeraButton = CreateButton("ReadingChoiceSmokeThree");
            controller.choiceMashaButton.transform.SetParent(choicePanel.transform, false);
            controller.choiceArtemButton.transform.SetParent(choicePanel.transform, false);
            controller.choiceLeraButton.transform.SetParent(choicePanel.transform, false);
            fourthButton = UnityEngine.Object.Instantiate(controller.choiceLeraButton, choicePanel.transform);
            fourthButton.name = "Choice Runtime Slot 4";
            choicePanel.SetActive(false);

            DialogueSceneData scene = ScriptableObject.CreateInstance<DialogueSceneData>();
            scene.sceneId = "TECH_DEMO_READING_CHOICES";
            scene.lines = new List<DialogueLine> { new DialogueLine { lineId = "line_1", text = "TECH DEMO ONLY" } };
            temporaryAssets.Add(scene);

            SetPrivate(controller, "sceneData", scene);
            SetPrivate(controller, "activeLines", scene.lines);
            SetPrivate(controller, "choiceButtons", new[]
            {
                controller.choiceMashaButton,
                controller.choiceArtemButton,
                controller.choiceLeraButton,
                fourthButton
            });

            List<DialogueChoice> threeChoices = new List<DialogueChoice>
            {
                new DialogueChoice { text = "Первый TECH DEMO ONLY вариант с достаточно длинной строкой для переноса", romanceDelta = 5 },
                new DialogueChoice { text = "Второй вариант" },
                new DialogueChoice { text = "Третий вариант" },
                new DialogueChoice { text = "Четвёртый TECH DEMO ONLY вариант с полным длинным текстом, который обязан переноситься без многоточия и сохранять смысл решения игрока.", trustMashaDelta = 7 }
            };
            SetPrivate(controller, "activeChoices", threeChoices);
            InvokeShowChoices(controller);

            Require(EventSystem.current.currentSelectedGameObject == controller.choiceMashaButton.gameObject,
                "The first visible choice must receive deterministic EventSystem focus.");
            Require(controller.choiceMashaButton.GetComponentInChildren<TextMeshProUGUI>(true).enableWordWrapping,
                "Long choice labels must permit wrapping instead of horizontal overflow.");
            TextMeshProUGUI fourthLabel = fourthButton.GetComponentInChildren<TextMeshProUGUI>(true);
            Require(fourthLabel.enableWordWrapping && fourthLabel.overflowMode != TextOverflowModes.Ellipsis,
                "Long fourth choice must retain complete text without semantic ellipsis.");
            Require(fourthLabel.text == threeChoices[3].text,
                "Fourth choice label must retain the complete source text.");
            RectTransform panelRect = choicePanel.transform as RectTransform;
            RectTransform fourthRect = fourthButton.transform as RectTransform;
            Require(panelRect.rect.yMin <= fourthRect.anchoredPosition.y - fourthRect.rect.height * 0.5f
                && panelRect.rect.yMax >= fourthRect.anchoredPosition.y + fourthRect.rect.height * 0.5f,
                "The adaptive choice panel must contain the full long fourth option.");
            InvokeChoose(controller, 3);
            Require(GameState.Instance.selectedChoiceIndex == 3 && GameState.Instance.trustMasha == 7,
                "Fourth runtime slot must select source index 3 and keep its own delta.");

            SetPrivate(controller, "activeChoices", threeChoices);
            InvokeShowChoices(controller);

            EventSystem.current.SetSelectedGameObject(controller.choiceLeraButton.gameObject);
            SetPrivate(controller, "activeChoices", new List<DialogueChoice> { threeChoices[0] });
            InvokeShowChoices(controller);
            Require(!controller.choiceArtemButton.gameObject.activeSelf && !controller.choiceLeraButton.gameObject.activeSelf,
                "Hidden choices must not remain visible after conditions change.");
            Require(EventSystem.current.currentSelectedGameObject == controller.choiceMashaButton.gameObject,
                "A hidden choice must not retain stale EventSystem focus.");

            InvokeChoose(controller, 0);
            Require(GameState.Instance.romance == 5, "The visible source choice must apply exactly once.");
            InvokeChoose(controller, 0);
            Require(GameState.Instance.romance == 5, "A repeated callback after the panel closes must not apply a choice twice.");
        }
        finally
        {
            MethodInfo onDisable = typeof(EventSystem).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic);
            onDisable?.Invoke(eventSystem, null);

            foreach (UnityEngine.Object asset in temporaryAssets)
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }

            if (controller != null)
            {
                UnityEngine.Object.DestroyImmediate(controller.nextButton != null ? controller.nextButton.gameObject : null);
                UnityEngine.Object.DestroyImmediate(controller.choiceMashaButton != null ? controller.choiceMashaButton.gameObject : null);
                UnityEngine.Object.DestroyImmediate(controller.choiceArtemButton != null ? controller.choiceArtemButton.gameObject : null);
                UnityEngine.Object.DestroyImmediate(controller.choiceLeraButton != null ? controller.choiceLeraButton.gameObject : null);
                UnityEngine.Object.DestroyImmediate(fourthButton != null ? fourthButton.gameObject : null);
            }
            UnityEngine.Object.DestroyImmediate(dialogueTextOwner);
            UnityEngine.Object.DestroyImmediate(nameBox);
            UnityEngine.Object.DestroyImmediate(choicePanel);
            UnityEngine.Object.DestroyImmediate(controllerOwner);
            UnityEngine.Object.DestroyImmediate(gameStateOwner);
            UnityEngine.Object.DestroyImmediate(eventSystemOwner);
        }
    }

    private static void VerifyAdaptiveReadingShellAndNamePlate()
    {
        GameObject shellOwner = new GameObject("ReadingShellSmokeBox", typeof(RectTransform), typeof(Image));
        GameObject textOwner = new GameObject("ReadingShellSmokeText", typeof(RectTransform), typeof(TextMeshProUGUI));
        GameObject nameOwner = new GameObject("ReadingShellSmokeNameBox", typeof(RectTransform), typeof(Image));
        GameObject speakerOwner = new GameObject("ReadingShellSmokeSpeaker", typeof(RectTransform), typeof(TextMeshProUGUI));
        try
        {
            textOwner.transform.SetParent(shellOwner.transform, false);
            RectTransform boxRect = shellOwner.transform as RectTransform;
            boxRect.sizeDelta = new Vector2(-440f, 180f);

            TextMeshProUGUI text = textOwner.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            TextMeshProUGUI speaker = speakerOwner.GetComponent<TextMeshProUGUI>();
            speaker.font = TMP_Settings.defaultFontAsset;

            VNDialogueController controller = shellOwner.AddComponent<VNDialogueController>();
            controller.dialogueUiRoot = shellOwner;
            controller.dialogueText = text;
            controller.nameBox = nameOwner;
            controller.speakerText = speaker;

            InvokePrivate(controller, "ApplyReadingShellPresentation");

            Require(Mathf.Approximately(boxRect.sizeDelta.x, 1240f), "Reading shell must keep its reading-column width.");
            Require(boxRect.sizeDelta.y <= 340f + 0.01f, "Idle reading shell must respect the existing height bound.");

            InvokePrivate(controller, "ApplyReadingShellContentHeight", "Короткая TECH DEMO ONLY реплика.");
            float shortHeight = boxRect.sizeDelta.y;
            Require(shortHeight >= 150f - 0.01f, "Short replies must not collapse below the readable shell minimum.");
            Require(shortHeight < 340f, "Short replies must stop reserving the full HUD-bar height.");

            text.fontSize = 40f;
            InvokePrivate(controller, "ApplyReadingShellContentHeight",
                "Длинная TECH DEMO ONLY реплика для проверки того, что адаптивная высота чтения растёт вместе с содержанием, сохраняет читаемый запас под строками и не обрезает многострочный текст при поддерживаемом масштабе диалога.");
            float grownHeight = boxRect.sizeDelta.y;
            Require(grownHeight > shortHeight, "Adaptive shell must grow with multi-line content.");
            Require(grownHeight <= 340f + 0.01f, "Adaptive shell must keep the existing long-text capacity bound.");

            InvokePrivate(controller, "ApplyReadingShellContentHeight", new string('Д', 4000));
            Require(Mathf.Approximately(boxRect.sizeDelta.y, 340f), "Overflowing content must stay capped at the existing bound.");

            Image nameBackground = nameOwner.GetComponent<Image>();
            Require(nameBackground.sprite == null, "Speaker plate must drop the baked decorative sprite.");
            Require(Mathf.Approximately(nameBackground.color.a, 0.60f), "Speaker plate must stay lighter than the dialogue shell.");

            InvokePrivate(controller, "ApplyNameBoxWidth", "TECH DEMO — Голос проверки");
            float plateWidth = ((RectTransform)nameOwner.transform).sizeDelta.x;
            Require(plateWidth >= 180f && plateWidth < 500f, "Speaker plate must hug the speaker name inside its bounded width.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(speakerOwner);
            UnityEngine.Object.DestroyImmediate(nameOwner);
            UnityEngine.Object.DestroyImmediate(textOwner);
            UnityEngine.Object.DestroyImmediate(shellOwner);
        }
    }

    private static void InvokePrivate(VNDialogueController controller, string methodName, params object[] arguments)
    {
        MethodInfo method = typeof(VNDialogueController).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Require(method != null, "Missing reading-loop method: " + methodName);
        method.Invoke(controller, arguments);
    }

    private static Button CreateButton(string name)
    {
        GameObject owner = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        RectTransform rect = owner.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(600f, 54f);
        Button button = owner.GetComponent<Button>();
        button.targetGraphic = owner.GetComponent<Image>();

        GameObject labelOwner = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelOwner.transform.SetParent(owner.transform, false);
        TextMeshProUGUI label = labelOwner.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        return button;
    }

    private static void InvokeShowChoices(VNDialogueController controller)
    {
        MethodInfo method = typeof(VNDialogueController).GetMethod("ShowChoices", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(method != null, "Reading choice presentation method is missing.");
        method.Invoke(controller, new object[] { false });
    }

    private static void InvokeChoose(VNDialogueController controller, int slot)
    {
        MethodInfo method = typeof(VNDialogueController).GetMethod("Choose", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(method != null, "Choice activation method is missing.");
        method.Invoke(controller, new object[] { slot });
    }

    private static void SetPrivate(VNDialogueController controller, string fieldName, object value)
    {
        FieldInfo field = typeof(VNDialogueController).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Require(field != null, "Missing reading-loop field: " + fieldName);
        field.SetValue(controller, value);
    }

    private static T GetPrivate<T>(VNDialogueController controller, string fieldName)
    {
        FieldInfo field = typeof(VNDialogueController).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Require(field != null, "Missing reading-loop field: " + fieldName);
        return (T)field.GetValue(controller);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
