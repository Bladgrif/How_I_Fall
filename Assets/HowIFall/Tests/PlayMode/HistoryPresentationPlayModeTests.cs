using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class HistoryPresentationPlayModeTests
{
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
        // EnsureInstance; they are not part of the tracked objects and must not
        // leak into later scene-loading test fixtures.
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
    public IEnumerator ShowBacklog_BuildsQuietEntryRowsWithLatestEmphasis()
    {
        // The controller's Awake reports the missing demo scene data in this isolated surface test.
        UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "Dialogue scene data is missing.");
        HistorySurface surface = CreateHistorySurface();
        yield return null;

        surface.Controller.ReplaceBacklogFromSnapshot(new List<DialogueBacklogEntry>
        {
            new DialogueBacklogEntry { speaker = "<Лера>", text = "Реплика & <тег>" },
            new DialogueBacklogEntry { speaker = string.Empty, text = "Авторская ремарка без говорящего." },
            new DialogueBacklogEntry { speaker = "Рассказчик", text = "Самая свежая запись истории." }
        });

        surface.Controller.ShowBacklog();
        yield return null;

        Assert.That(surface.EmptyText.gameObject.activeSelf, Is.False, "Empty surface must be hidden while entries exist.");
        List<Transform> rows = CollectActiveRows(surface.Content, surface.EmptyText.transform);
        Assert.That(rows.Count, Is.EqualTo(3), "History must render one quiet block per backlog entry.");

        TextMeshProUGUI firstSpeaker = rows[0].Find("History Entry Speaker")?.GetComponent<TextMeshProUGUI>();
        Assert.That(firstSpeaker, Is.Not.Null, "Speaker entry must expose a speaker label.");
        Assert.That(firstSpeaker.text, Does.Contain("&lt;Лера&gt;"), "Speaker label must keep rich text escaped.");
        Assert.That(rows[0].Find("History Entry Text") != null, Is.True, "Speaker entry must expose a body label.");
        Assert.That(rows[1].Find("History Entry Speaker") == null, Is.True, "Narration entry must not show a speaker label.");

        // Target v1: every entry carries a quiet accent bar (cyan for speaker lines,
        // muted for narration), and only the latest entry gains the soft focus frame.
        Image speakerBar = rows[0].Find("History Entry Accent Bar")?.GetComponent<Image>();
        Image narrationBar = rows[1].Find("History Entry Accent Bar")?.GetComponent<Image>();
        Image latestBar = rows[2].Find("History Entry Accent Bar")?.GetComponent<Image>();
        Assert.That(speakerBar, Is.Not.Null, "Speaker entry must carry an accent bar.");
        Assert.That(narrationBar, Is.Not.Null, "Narration entry must carry a muted accent bar.");
        Assert.That(latestBar, Is.Not.Null, "Latest entry must carry an accent bar.");
        Assert.That(narrationBar.color, Is.Not.EqualTo(speakerBar.color), "Narration bar must stay muted against the speaker bar.");
        Assert.That(latestBar.color, Is.Not.EqualTo(speakerBar.color), "Latest bar must read brighter than older speaker bars.");

        Assert.That(rows[2].Find("History Entry Focus Frame") != null, Is.True, "Latest entry must carry the soft current-state frame.");
        Assert.That(rows[0].Find("History Entry Focus Frame") == null, Is.True, "Older entries must not carry the current-state frame.");
        Image latestPlate = rows[2].GetComponent<Image>();
        Image olderPlate = rows[0].GetComponent<Image>();
        Assert.That(latestPlate.color.a, Is.GreaterThan(0.01f), "Latest entry must keep a subtle fill.");
        Assert.That(olderPlate.color.a, Is.LessThan(0.01f), "Older entries must stay transparent against the panel.");
        Assert.That(olderPlate.raycastTarget, Is.True, "Entry plates must stay part of scroll drag hit-testing.");

        TextMeshProUGUI latestBody = rows[2].Find("History Entry Text")?.GetComponent<TextMeshProUGUI>();
        Assert.That(latestBody, Is.Not.Null);
        Assert.That(latestBody.font != null && latestBody.font.name == "Runtime Backlog Cyrillic Fallback",
            Is.True, "History entry labels must use the transient Cyrillic fallback font.");

        Button closeButton = surface.Controller.backlogCloseButton;
        Assert.That(closeButton, Is.Not.Null);
        Assert.That(closeButton.targetGraphic is Image closeFill && closeFill.sprite != null,
            Is.True, "History close action must be styled as the target pill.");
        Assert.That(closeButton.colors.normalColor, Is.EqualTo(Color.white),
            "Close button ColorBlock must stay neutral while presentation owns visible states.");
    }

    [UnityTest]
    public IEnumerator ShowBacklog_WithoutEntries_ShowsComposedEmptyState()
    {
        UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "Dialogue scene data is missing.");
        HistorySurface surface = CreateHistorySurface();
        yield return null;

        // The isolated controller seeds its missing-scene error into the backlog during Awake;
        // this test targets the genuinely empty history state.
        surface.Controller.ClearBacklog();
        surface.Controller.ShowBacklog();
        yield return null;

        Transform emptyState = surface.Controller.backlogPanel.transform.Find("History Empty State");
        Assert.That(emptyState, Is.Not.Null, "Empty history must render the composed empty state.");
        Assert.That(emptyState.gameObject.activeSelf, Is.True, "Empty state must be visible without entries.");
        Assert.That(surface.EmptyText.gameObject.activeSelf, Is.False,
            "The bare contract text surface stays hidden while the composed empty state renders.");
        TextMeshProUGUI primary = emptyState.Find("History Empty Primary")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI secondary = emptyState.Find("History Empty Secondary")?.GetComponent<TextMeshProUGUI>();
        Assert.That(primary, Is.Not.Null, "Empty state must expose the primary caption.");
        Assert.That(primary.text, Is.EqualTo("Здесь пока нет записей."));
        Assert.That(secondary, Is.Not.Null, "Empty state must expose the secondary caption.");
        Assert.That(secondary.text, Is.EqualTo("История будет заполняться по мере чтения."));
        List<Transform> rows = CollectActiveRows(surface.Content, surface.EmptyText.transform);
        Assert.That(rows.Count, Is.EqualTo(0), "Empty history must not render entry blocks.");
    }

    private static List<Transform> CollectActiveRows(RectTransform content, Transform emptyText)
    {
        List<Transform> rows = new List<Transform>();
        foreach (Transform child in content)
        {
            if (child != emptyText && child.gameObject.activeSelf)
            {
                rows.Add(child);
            }
        }

        return rows;
    }

    private HistorySurface CreateHistorySurface()
    {
        GameObject canvas = CreateTrackedObject(new GameObject("History Canvas", typeof(Canvas)));
        GameObject panel = CreateTrackedObject(new GameObject("History Panel", typeof(RectTransform), typeof(Image)));
        panel.transform.SetParent(canvas.transform, false);
        GameObject scrollView = CreateTrackedObject(new GameObject("History Scroll View", typeof(RectTransform), typeof(ScrollRect)));
        scrollView.transform.SetParent(panel.transform, false);
        GameObject viewport = CreateTrackedObject(new GameObject("Viewport", typeof(RectTransform)));
        viewport.transform.SetParent(scrollView.transform, false);
        GameObject content = CreateTrackedObject(new GameObject(
            "Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)));
        content.transform.SetParent(viewport.transform, false);

        RectTransform contentRect = (RectTransform)content.transform;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scrollRect = scrollView.GetComponent<ScrollRect>();
        scrollRect.viewport = (RectTransform)viewport.transform;
        scrollRect.content = contentRect;

        GameObject emptyTextObject = CreateTrackedObject(new GameObject("History Text", typeof(RectTransform), typeof(TextMeshProUGUI)));
        emptyTextObject.transform.SetParent(content.transform, false);

        GameObject closeButton = CreateTrackedObject(new GameObject("History Close Button", typeof(RectTransform), typeof(Image), typeof(Button)));
        closeButton.transform.SetParent(panel.transform, false);

        CreateTrackedObject(new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)));

        GameObject controllerObject = CreateTrackedObject(new GameObject("Controller", typeof(VNDialogueController)));
        VNDialogueController controller = controllerObject.GetComponent<VNDialogueController>();
        controller.backlogPanel = panel;
        controller.backlogText = emptyTextObject.GetComponent<TextMeshProUGUI>();
        controller.backlogCloseButton = closeButton.GetComponent<Button>();

        // The controller validates its ordinary reading-surface references on Awake;
        // this test exercises only the History surface, so quiet placeholders satisfy that gate.
        controller.speakerText = CreateTrackedObject(new GameObject("Speaker", typeof(TextMeshProUGUI))).GetComponent<TextMeshProUGUI>();
        controller.dialogueText = CreateTrackedObject(new GameObject("Dialogue", typeof(TextMeshProUGUI))).GetComponent<TextMeshProUGUI>();
        controller.nameBox = CreateTrackedObject(new GameObject("NameBox"));
        controller.nextButton = CreateTrackedObject(new GameObject("Next", typeof(Button))).GetComponent<Button>();
        controller.dialogueUiRoot = CreateTrackedObject(new GameObject("DialogueUiRoot"));
        controller.choicePanel = CreateTrackedObject(new GameObject("ChoicePanel"));
        controller.choiceMashaButton = CreateTrackedObject(new GameObject("ChoiceMasha", typeof(Button))).GetComponent<Button>();
        controller.choiceArtemButton = CreateTrackedObject(new GameObject("ChoiceArtem", typeof(Button))).GetComponent<Button>();
        controller.choiceLeraButton = CreateTrackedObject(new GameObject("ChoiceLera", typeof(Button))).GetComponent<Button>();
        controller.vnSettingsDimOverlay = CreateTrackedObject(new GameObject("Settings Overlay"));
        controller.vnSettingsPanel = CreateTrackedObject(new GameObject("Settings Panel"));

        return new HistorySurface(controller, scrollRect, emptyTextObject.GetComponent<TextMeshProUGUI>());
    }

    private T CreateTrackedObject<T>(T created) where T : Object
    {
        createdObjects.Add(created);
        return created;
    }

    private readonly struct HistorySurface
    {
        public HistorySurface(VNDialogueController controller, ScrollRect scrollRect, TextMeshProUGUI emptyText)
        {
            Controller = controller;
            ScrollRect = scrollRect;
            EmptyText = emptyText;
        }

        public VNDialogueController Controller { get; }
        public ScrollRect ScrollRect { get; }
        public RectTransform Content => ScrollRect.content;
        public TextMeshProUGUI EmptyText { get; }
    }
}
