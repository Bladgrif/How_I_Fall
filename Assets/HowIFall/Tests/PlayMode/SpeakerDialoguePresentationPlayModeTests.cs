using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Speaker/Dialogue target v1 regression: typing caret and advance-cue states,
/// narration transitions without stale speaker presentation, and long speaker
/// names that must not collide with the dialogue body.
/// </summary>
public class SpeakerDialoguePresentationPlayModeTests
{
    private const string TypingCaretName = "Typing Caret";
    private const string LongSpeakerFixtureName = "Александр Сергеевич";

    private const string TypingFixtureText =
        "TECH DEMO ONLY / NOT CANON: нейтральная реплика для проверки состояния набора текста.";
    private const string LongNameFixtureText =
        "TECH DEMO ONLY / NOT CANON: длинное имя говорящего не давит на текст и не ломает ритм строки.";
    private const string NarrationFixtureText =
        "TECH DEMO ONLY / NOT CANON: авторская ремарка без говорящего.";

    private readonly List<Object> createdObjects = new List<Object>();

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        for (int index = createdObjects.Count - 1; index >= 0; index--)
        {
            Object item = createdObjects[index];
            if (item != null)
            {
                Object.Destroy(item);
            }
        }
        createdObjects.Clear();
        yield return null;

        // The controller's Start creates GameState/SaveManager singletons through
        // EnsureInstance; they must not leak into later scene-loading test fixtures.
        DestroyExistingSingletons();
        yield return null;
    }

    private static void DestroyExistingSingletons()
    {
        DestroyAll<VNDialogueController>();
        DestroyAll<GameState>();
        DestroyAll<SettingsManager>();
        DestroyAll<AudioManager>();
        DestroyAll<SaveManager>();
        DestroyAll<SceneFlowManager>();
    }

    private static void DestroyAll<T>() where T : Object
    {
        foreach (T item in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (item != null)
            {
                Object.Destroy(item);
            }
        }
    }

    [UnityTest]
    public IEnumerator TypingLine_ShowsCaretWithoutAdvanceCue_UntilLineCompletesNaturally()
    {
        SpeakerSurface surface = CreateSpeakerSurface();
        yield return null;

        LoadFixture(surface.Controller, new List<DialogueLine>
        {
            new DialogueLine { lineId = "typing_1", speaker = "ТЕХ-ДЕМО: Артём", text = TypingFixtureText }
        });
        yield return null;

        Transform caret = surface.Controller.dialogueText.transform.Find(TypingCaretName);
        Assert.That(caret, Is.Not.Null, "Typing line must build the runtime caret.");
        Assert.That(caret.gameObject.activeInHierarchy, Is.True,
            "In-progress line must show the cyan typing caret.");
        Assert.That(GetAdvanceIndicator(surface).gameObject.activeInHierarchy, Is.False,
            "In-progress line must hide the advance cue.");
        Assert.That(surface.Controller.dialogueText.text.Length, Is.LessThan(TypingFixtureText.Length),
            "The fixture line must still be typing for this proof.");

        yield return WaitForTypingComplete(surface.Controller);

        Assert.That(caret.gameObject.activeInHierarchy, Is.False,
            "Completed line must hide the typing caret.");
        Assert.That(GetAdvanceIndicator(surface).gameObject.activeInHierarchy, Is.True,
            "Completed line must show the cyan advance cue.");
        Assert.That(surface.Controller.dialogueText.text, Is.EqualTo(TypingFixtureText));
    }

    [UnityTest]
    public IEnumerator CompleteTyping_MidLine_RestoresAdvanceCueAndHidesCaret()
    {
        SpeakerSurface surface = CreateSpeakerSurface();
        yield return null;

        LoadFixture(surface.Controller, new List<DialogueLine>
        {
            new DialogueLine { lineId = "typing_complete_1", speaker = "ТЕХ-ДЕМО: Артём", text = TypingFixtureText }
        });
        yield return null;
        yield return new WaitForSeconds(0.25f);

        InvokePrivate(surface.Controller, "CompleteTyping");
        yield return null;

        Transform caret = surface.Controller.dialogueText.transform.Find(TypingCaretName);
        Assert.That(caret, Is.Not.Null);
        Assert.That(caret.gameObject.activeInHierarchy, Is.False,
            "Player-completed line must hide the typing caret.");
        Assert.That(GetAdvanceIndicator(surface).gameObject.activeInHierarchy, Is.True,
            "Player-completed line must show the advance cue.");
        Assert.That(surface.Controller.dialogueText.text, Is.EqualTo(TypingFixtureText),
            "Completed line must render the full text without caret residue.");
    }

    [UnityTest]
    public IEnumerator NarrationAfterNamedLine_ClearsSpeakerPresentationWithoutStaleState()
    {
        SpeakerSurface surface = CreateSpeakerSurface();
        yield return null;

        LoadFixture(surface.Controller, new List<DialogueLine>
        {
            new DialogueLine { lineId = "narration_named_1", speaker = "ТЕХ-ДЕМО: Артём", text = TypingFixtureText },
            new DialogueLine { lineId = "narration_plain_1", speaker = string.Empty, text = NarrationFixtureText }
        });
        yield return WaitForTypingComplete(surface.Controller);

        surface.Controller.AdvanceDialogue();
        yield return WaitForTypingComplete(surface.Controller);

        Assert.That(surface.Controller.nameBox.activeInHierarchy, Is.False,
            "Narration must hide the speaker name box.");
        Assert.That(surface.Controller.speakerText.text, Is.Empty,
            "Narration must not keep a stale speaker label.");
        Transform caret = surface.Controller.dialogueText.transform.Find(TypingCaretName);
        Assert.That(caret, Is.Not.Null);
        Assert.That(caret.gameObject.activeInHierarchy, Is.False,
            "Completed narration must not keep a typing caret.");
        Assert.That(GetAdvanceIndicator(surface).gameObject.activeInHierarchy, Is.True,
            "Completed narration must show the advance cue.");
        Assert.That(surface.Controller.dialogueText.text, Is.EqualTo(NarrationFixtureText));
    }

    [UnityTest]
    public IEnumerator LongSpeakerName_KeepsOneLineLabelInsideShellWithoutBodyCollision()
    {
        SpeakerSurface surface = CreateSpeakerSurface();
        yield return null;

        LoadFixture(surface.Controller, new List<DialogueLine>
        {
            new DialogueLine { lineId = "long_name_1", speaker = LongSpeakerFixtureName, text = LongNameFixtureText }
        });
        yield return WaitForTypingComplete(surface.Controller);
        yield return null;

        Assert.That(surface.Controller.nameBox.activeInHierarchy, Is.True,
            "Named line must keep the speaker name box visible.");
        RectTransform nameRect = (RectTransform)surface.Controller.nameBox.transform;
        Assert.That(nameRect.rect.width, Is.LessThanOrEqualTo(500.5f),
            "Speaker name box must stay inside the supported width clamp.");

        TextMeshProUGUI speakerText = surface.Controller.speakerText;
        Assert.That(speakerText.textInfo.lineCount, Is.EqualTo(1),
            "Long speaker name must render as one line inside the name box.");

        RectTransform speakerRect = speakerText.rectTransform;
        RectTransform bodyRect = surface.Controller.dialogueText.rectTransform;
        Assert.That(Overlaps(GetWorldRect(speakerRect), GetWorldRect(bodyRect)), Is.False,
            "Long speaker name must not collide with the dialogue body.");

        Transform caret = surface.Controller.dialogueText.transform.Find(TypingCaretName);
        Assert.That(caret.gameObject.activeInHierarchy, Is.False,
            "Completed long-name line must not show the typing caret.");
        Assert.That(nameRect.transform.Find("Speaker Accent"), Is.Not.Null,
            "Named line must keep the cyan speaker accent.");
    }

    private static TextMeshProUGUI GetAdvanceIndicator(SpeakerSurface surface)
    {
        TextMeshProUGUI indicator = surface.Controller.nextButton.GetComponentInChildren<TextMeshProUGUI>(true);
        Assert.That(indicator, Is.Not.Null, "Next button must expose the advance indicator label.");
        return indicator;
    }

    private static IEnumerator WaitForTypingComplete(VNDialogueController controller)
    {
        const float timeoutSeconds = 15f;
        float startedAt = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - startedAt < timeoutSeconds)
        {
            if (!(bool)GetPrivateField(controller, "isTyping"))
            {
                yield break;
            }

            yield return null;
        }

        Assert.Fail("Typing did not complete within the test timeout.");
    }

    private void LoadFixture(VNDialogueController controller, List<DialogueLine> lines)
    {
        DialogueSceneData fixture = ScriptableObject.CreateInstance<DialogueSceneData>();
        fixture.hideFlags = HideFlags.DontSave;
        fixture.sceneId = "TECH_DEMO_SPEAKER_PRESENTATION_" + createdObjects.Count;
        fixture.lines = lines;
        fixture.choices = new List<DialogueChoice>();
        createdObjects.Add(fixture);
        if (controller.sceneRegistry != null && !controller.sceneRegistry.scenes.Contains(fixture))
        {
            controller.sceneRegistry.scenes.Add(fixture);
        }

        InvokePrivate(controller, "LoadDialogueScene", fixture, 0, false);
    }

    private static Rect GetWorldRect(RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        Vector2 min = corners[0];
        Vector2 max = corners[0];
        for (int i = 1; i < corners.Length; i++)
        {
            min = Vector2.Min(min, corners[i]);
            max = Vector2.Max(max, corners[i]);
        }

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static bool Overlaps(Rect a, Rect b)
    {
        return a.Overlaps(b);
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Expected private field is missing: " + fieldName);
        return field.GetValue(target);
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = null;
        foreach (MethodInfo candidate in target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            if (candidate.Name == methodName && candidate.GetParameters().Length == arguments.Length)
            {
                method = candidate;
                break;
            }
        }

        Assert.That(method, Is.Not.Null, "Expected private method is missing: " + methodName);
        method.Invoke(target, arguments);
    }

    private SpeakerSurface CreateSpeakerSurface()
    {
        // The controller's Awake reports the missing demo scene data in this isolated surface test.
        UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "Dialogue scene data is missing.");

        GameObject canvas = CreateTrackedObject(new GameObject("Speaker Canvas", typeof(Canvas)));
        GameObject shell = CreateTrackedObject(new GameObject("Reading Shell", typeof(RectTransform)));
        shell.transform.SetParent(canvas.transform, false);
        RectTransform shellRect = (RectTransform)shell.transform;
        shellRect.anchorMin = new Vector2(0.5f, 0f);
        shellRect.anchorMax = new Vector2(0.5f, 0f);
        shellRect.pivot = new Vector2(0.5f, 0f);
        shellRect.anchoredPosition = new Vector2(0f, 92f);
        shellRect.sizeDelta = new Vector2(1320f, 330f);

        GameObject dialogueUiRoot = CreateTrackedObject(new GameObject("Dialogue Ui Root", typeof(RectTransform), typeof(Image)));
        dialogueUiRoot.transform.SetParent(shell.transform, false);

        GameObject nameBox = CreateTrackedObject(new GameObject("Name Box", typeof(RectTransform), typeof(Image)));
        nameBox.transform.SetParent(dialogueUiRoot.transform, false);
        RectTransform nameBoxRect = (RectTransform)nameBox.transform;
        nameBoxRect.anchorMin = nameBoxRect.anchorMax = new Vector2(0f, 1f);
        nameBoxRect.pivot = new Vector2(0f, 1f);
        nameBoxRect.anchoredPosition = new Vector2(34f, 44f);
        nameBoxRect.sizeDelta = new Vector2(340f, 92f);

        GameObject speakerObject = CreateTrackedObject(new GameObject("Speaker Text", typeof(RectTransform), typeof(TextMeshProUGUI)));
        speakerObject.transform.SetParent(nameBox.transform, false);
        RectTransform speakerRect = (RectTransform)speakerObject.transform;
        speakerRect.anchorMin = Vector2.zero;
        speakerRect.anchorMax = Vector2.one;
        speakerRect.pivot = new Vector2(0.5f, 0.5f);
        speakerRect.anchoredPosition = new Vector2(0f, 2f);
        speakerRect.sizeDelta = new Vector2(-36f, -4f);

        GameObject dialogueObject = CreateTrackedObject(new GameObject("Dialogue Text", typeof(RectTransform), typeof(TextMeshProUGUI)));
        dialogueObject.transform.SetParent(dialogueUiRoot.transform, false);
        RectTransform dialogueRect = (RectTransform)dialogueObject.transform;
        dialogueRect.anchorMin = Vector2.zero;
        dialogueRect.anchorMax = Vector2.one;
        dialogueRect.pivot = new Vector2(0.5f, 0.5f);
        dialogueRect.sizeDelta = new Vector2(-160f, -90f);

        GameObject nextButtonObject = CreateTrackedObject(new GameObject("Next Button", typeof(RectTransform), typeof(Button)));
        nextButtonObject.transform.SetParent(dialogueUiRoot.transform, false);
        RectTransform nextButtonRect = (RectTransform)nextButtonObject.transform;
        nextButtonRect.anchorMin = Vector2.zero;
        nextButtonRect.anchorMax = Vector2.one;
        nextButtonRect.sizeDelta = Vector2.zero;
        GameObject indicatorObject = CreateTrackedObject(new GameObject("Next Indicator", typeof(RectTransform), typeof(TextMeshProUGUI)));
        indicatorObject.transform.SetParent(nextButtonObject.transform, false);

        GameObject choicePanel = CreateTrackedObject(new GameObject("Choice Panel", typeof(RectTransform)));
        choicePanel.transform.SetParent(canvas.transform, false);

        CreateTrackedObject(new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)));

        GameObject controllerObject = CreateTrackedObject(new GameObject("Controller", typeof(VNDialogueController)));
        VNDialogueController controller = controllerObject.GetComponent<VNDialogueController>();
        controller.speakerText = speakerObject.GetComponent<TextMeshProUGUI>();
        controller.dialogueText = dialogueObject.GetComponent<TextMeshProUGUI>();
        controller.nameBox = nameBox;
        controller.nextButton = nextButtonObject.GetComponent<Button>();
        controller.dialogueUiRoot = dialogueUiRoot;
        controller.choicePanel = choicePanel;
        controller.choiceMashaButton = CreateTrackedObject(new GameObject("ChoiceMasha", typeof(Button))).GetComponent<Button>();
        controller.choiceArtemButton = CreateTrackedObject(new GameObject("ChoiceArtem", typeof(Button))).GetComponent<Button>();
        controller.choiceLeraButton = CreateTrackedObject(new GameObject("ChoiceLera", typeof(Button))).GetComponent<Button>();
        controller.vnSettingsDimOverlay = CreateTrackedObject(new GameObject("Settings Overlay"));
        controller.vnSettingsPanel = CreateTrackedObject(new GameObject("Settings Panel"));

        return new SpeakerSurface(controller);
    }

    private T CreateTrackedObject<T>(T created) where T : Object
    {
        createdObjects.Add(created);
        return created;
    }

    private readonly struct SpeakerSurface
    {
        public SpeakerSurface(VNDialogueController controller)
        {
            Controller = controller;
        }

        public VNDialogueController Controller { get; }
    }
}
