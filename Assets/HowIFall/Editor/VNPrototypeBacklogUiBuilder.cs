using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class VNPrototypeBacklogUiBuilder
{
    private const string VNPrototypeScenePath = "Assets/HowIFall/Scenes/VNPrototype.unity";

    [MenuItem("How I Fall/Build VN Backlog UI")]
    public static void BuildBacklogUi()
    {
        var scene = EditorSceneManager.OpenScene(VNPrototypeScenePath);
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        VNDialogueController controller = Object.FindFirstObjectByType<VNDialogueController>();

        if (canvas == null)
        {
            Debug.LogError("VNPrototypeBacklogUiBuilder: Canvas was not found in VNPrototype.");
            return;
        }

        if (controller == null)
        {
            Debug.LogError("VNPrototypeBacklogUiBuilder: VNDialogueController was not found in VNPrototype.");
            return;
        }

        Transform existingPanel = canvas.transform.Find("Backlog Panel");

        if (existingPanel != null)
        {
            Object.DestroyImmediate(existingPanel.gameObject);
        }

        GameObject backlogPanel = CreateBacklogPanel(canvas.transform, out TextMeshProUGUI backlogText, out Button closeButton);
        backlogPanel.transform.SetAsLastSibling();
        backlogPanel.SetActive(false);

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("backlogPanel").objectReferenceValue = backlogPanel;
        serializedController.FindProperty("backlogText").objectReferenceValue = backlogText;
        serializedController.FindProperty("backlogCloseButton").objectReferenceValue = closeButton;
        serializedController.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("VNPrototype backlog UI was created and assigned.");
    }

    public static void BuildBacklogUiBatch()
    {
        BuildBacklogUi();
    }

    private static GameObject CreateBacklogPanel(Transform parent, out TextMeshProUGUI backlogText, out Button closeButton)
    {
        GameObject panel = CreateUiObject("Backlog Panel", parent);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        StretchToParent(panelRect);

        Image dimImage = panel.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.75f);
        dimImage.raycastTarget = true;

        GameObject window = CreateUiObject("Backlog Window", panel.transform);
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.anchoredPosition = Vector2.zero;
        windowRect.sizeDelta = new Vector2(1200f, 760f);

        Image windowImage = window.AddComponent<Image>();
        windowImage.color = new Color(0.025f, 0.018f, 0.045f, 0.9f);
        windowImage.raycastTarget = true;

        Shadow shadow = window.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(5f, -5f);

        CreateAccentLine(window.transform);
        CreateTitle(window.transform);
        CreateScrollView(window.transform, out backlogText);
        closeButton = CreateCloseButton(window.transform);

        return panel;
    }

    private static void CreateAccentLine(Transform parent)
    {
        GameObject line = CreateUiObject("Backlog Accent Line", parent);
        RectTransform rect = line.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(3f, -48f);

        Image image = line.AddComponent<Image>();
        image.color = new Color(0.55f, 0.22f, 0.8f, 0.65f);
        image.raycastTarget = false;
    }

    private static void CreateTitle(Transform parent)
    {
        TextMeshProUGUI title = CreateText("Title", parent, "История", 42, new Color(0.94f, 0.9f, 0.98f, 1f));
        title.alignment = TextAlignmentOptions.Center;

        RectTransform rect = title.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -28f);
        rect.sizeDelta = new Vector2(-120f, 68f);
    }

    private static void CreateScrollView(Transform parent, out TextMeshProUGUI backlogText)
    {
        GameObject scrollView = CreateUiObject("Backlog Scroll View", parent);
        RectTransform scrollRectTransform = scrollView.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0f, 0f);
        scrollRectTransform.anchorMax = new Vector2(1f, 1f);
        scrollRectTransform.offsetMin = new Vector2(82f, 112f);
        scrollRectTransform.offsetMax = new Vector2(-82f, -118f);

        Image scrollBackground = scrollView.AddComponent<Image>();
        scrollBackground.color = new Color(0.04f, 0.03f, 0.07f, 0.55f);
        scrollBackground.raycastTarget = true;

        ScrollRect scrollRect = scrollView.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 32f;

        GameObject viewport = CreateUiObject("Viewport", scrollView.transform);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(24f, 20f);
        viewportRect.offsetMax = new Vector2(-42f, -20f);

        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewportImage.raycastTarget = true;

        RectMask2D mask = viewport.AddComponent<RectMask2D>();
        mask.padding = Vector4.zero;

        GameObject content = CreateUiObject("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layoutGroup = content.AddComponent<VerticalLayoutGroup>();
        layoutGroup.childAlignment = TextAnchor.UpperLeft;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.padding = new RectOffset(0, 0, 0, 0);

        ContentSizeFitter contentSizeFitter = content.AddComponent<ContentSizeFitter>();
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        backlogText = CreateText("Backlog Text", content.transform, string.Empty, 28, new Color(0.9f, 0.86f, 0.96f, 1f));
        backlogText.alignment = TextAlignmentOptions.TopLeft;
        backlogText.enableWordWrapping = true;
        backlogText.overflowMode = TextOverflowModes.Overflow;
        backlogText.raycastTarget = false;

        LayoutElement textLayout = backlogText.gameObject.AddComponent<LayoutElement>();
        textLayout.flexibleWidth = 1f;
        textLayout.minHeight = 80f;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        GameObject scrollbar = CreateVerticalScrollbar(scrollView.transform);
        scrollRect.verticalScrollbar = scrollbar.GetComponent<Scrollbar>();
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = -8f;
    }

    private static GameObject CreateVerticalScrollbar(Transform parent)
    {
        GameObject scrollbarGo = CreateUiObject("Vertical Scrollbar", parent);
        RectTransform scrollbarRect = scrollbarGo.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.offsetMin = new Vector2(-20f, 20f);
        scrollbarRect.offsetMax = new Vector2(-8f, -20f);

        Image scrollbarImage = scrollbarGo.AddComponent<Image>();
        scrollbarImage.color = new Color(0.1f, 0.08f, 0.14f, 0.8f);
        scrollbarImage.raycastTarget = true;

        Scrollbar scrollbar = scrollbarGo.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        GameObject slidingArea = CreateUiObject("Sliding Area", scrollbarGo.transform);
        RectTransform slidingRect = slidingArea.GetComponent<RectTransform>();
        StretchToParent(slidingRect);

        GameObject handle = CreateUiObject("Handle", slidingArea.transform);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        StretchToParent(handleRect);

        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = new Color(0.55f, 0.32f, 0.72f, 0.85f);
        handleImage.raycastTarget = true;

        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;

        return scrollbarGo;
    }

    private static Button CreateCloseButton(Transform parent)
    {
        GameObject buttonGo = CreateUiObject("Backlog Close Button", parent);
        RectTransform rect = buttonGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-82f, 34f);
        rect.sizeDelta = new Vector2(190f, 54f);

        Image image = buttonGo.AddComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = true;

        Button button = buttonGo.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.08f, 0.06f, 0.12f, 0.95f);
        colors.highlightedColor = new Color(0.34f, 0.16f, 0.46f, 0.95f);
        colors.pressedColor = new Color(0.45f, 0.2f, 0.58f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.04f, 0.03f, 0.06f, 0.65f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TextMeshProUGUI label = CreateText("Text", buttonGo.transform, "Закрыть", 24, new Color(0.94f, 0.9f, 0.98f, 1f));
        label.alignment = TextAlignmentOptions.Center;
        StretchToParent(label.rectTransform);

        return button;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, int fontSize, Color color)
    {
        GameObject textGo = CreateUiObject(name, parent);
        TextMeshProUGUI textComponent = textGo.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.color = color;
        textComponent.raycastTarget = false;
        return textComponent;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
