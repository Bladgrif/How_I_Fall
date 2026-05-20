using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class VNPrototypeSceneBuilder
{
    private const string MainMenuScenePath = "Assets/HowIFall/Scenes/MainMenu.unity";
    private const string VNPrototypeScenePath = "Assets/HowIFall/Scenes/VNPrototype.unity";
    private const string PrimarySceneDataPath = "Assets/HowIFall/Data/Dialogues/intro_school_morning.asset";
    private const string FallbackSceneDataPath = "Assets/HowIFall/Data/Dialogues/intro_school_meet.asset";
    private const string SceneRegistryPath = "Assets/HowIFall/Data/Dialogues/DialogueSceneRegistry.asset";
    private const string UiClickSfxWavPath = "Assets/HowIFall/Audio/SFX/ui_click.wav";
    private const string UiClickSfxMp3Path = "Assets/HowIFall/Audio/SFX/ui_click.mp3";
    private const string UiClickSfxOggPath = "Assets/HowIFall/Audio/SFX/ui_click.ogg";

    [MenuItem("How I Fall/Build VN Prototype Scene")]
    public static void BuildVNPrototypeScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateMainCamera();
        Canvas canvas = CreateCanvas();
        CreateEventSystem();

        GameObject managers = new GameObject("Managers");
        managers.AddComponent<GameState>();
        managers.AddComponent<SaveManager>();
        managers.AddComponent<SettingsManager>();
        managers.AddComponent<AudioManager>();

        GameObject vnRoot = CreateUiObject("VN Root", canvas.transform);
        StretchFull(vnRoot.GetComponent<RectTransform>());

        Image backgroundImage = CreateBackgroundImage(vnRoot.transform);
        Image characterImage = CreateCharacterImage(vnRoot.transform);

        GameObject dialogueBox = CreateDialogueBox(vnRoot.transform);
        GameObject nameBox = CreateNameBox(dialogueBox.transform, out TextMeshProUGUI speakerText);
        TextMeshProUGUI dialogueText = CreateDialogueText(dialogueBox.transform);
        Button nextButton = CreateNextButton(dialogueBox.transform);

        GameObject choicePanel = CreateChoicePanel(
            vnRoot.transform,
            out Button choiceMashaButton,
            out Button choiceArtemButton,
            out Button choiceLeraButton);
        choicePanel.SetActive(false);

        GameObject controllerGo = new GameObject("VN Controller");
        VNDialogueController controller = controllerGo.AddComponent<VNDialogueController>();

        CreateQuickMenu(canvas.transform, controller);

        GameObject notificationPanel = CreateNotificationPanel(
            canvas.transform,
            out TextMeshProUGUI notificationText);
        notificationPanel.SetActive(false);

        GameObject settingsPanel = CreateSettingsPanel(
            canvas.transform,
            out Slider masterVolumeSlider,
            out Slider musicVolumeSlider,
            out Slider sfxVolumeSlider,
            out Slider textSpeedSlider,
            out Toggle fullscreenToggle,
            out Button settingsCloseButton,
            out Button settingsResetButton);
        settingsPanel.SetActive(false);

        GameObject confirmExitPanel = CreateConfirmExitPanel(
            canvas.transform,
            out Button confirmExitYesButton,
            out Button confirmExitNoButton);
        confirmExitPanel.SetActive(false);

        GameObject backlogPanel = CreateBacklogPanel(
            canvas.transform,
            out TextMeshProUGUI backlogText,
            out Button backlogCloseButton);
        backlogPanel.SetActive(false);

        GameObject debugStatsPanel = CreateDebugStatsPanel(canvas.transform, out TextMeshProUGUI debugStatsText);
        debugStatsPanel.SetActive(false);
        CreateDebugStatsController(debugStatsPanel, debugStatsText);

        controller.sceneData = TryLoadSceneData();
        controller.sceneRegistry = AssetDatabase.LoadAssetAtPath<DialogueSceneRegistry>(SceneRegistryPath);
        controller.speakerText = speakerText;
        controller.dialogueText = dialogueText;
        controller.backgroundImage = backgroundImage;
        controller.characterImage = characterImage;
        controller.nameBox = nameBox;
        controller.nextButton = nextButton;
        controller.choicePanel = choicePanel;
        controller.choiceMashaButton = choiceMashaButton;
        controller.choiceArtemButton = choiceArtemButton;
        controller.choiceLeraButton = choiceLeraButton;
        controller.backlogPanel = backlogPanel;
        controller.backlogText = backlogText;
        controller.backlogCloseButton = backlogCloseButton;
        controller.notificationPanel = notificationPanel;
        controller.notificationText = notificationText;
        controller.confirmExitPanel = confirmExitPanel;
        controller.confirmExitYesButton = confirmExitYesButton;
        controller.confirmExitNoButton = confirmExitNoButton;
        controller.vnSettingsPanel = settingsPanel;
        controller.vnMasterVolumeSlider = masterVolumeSlider;
        controller.vnMusicVolumeSlider = musicVolumeSlider;
        controller.vnSfxVolumeSlider = sfxVolumeSlider;
        controller.vnTextSpeedSlider = textSpeedSlider;
        controller.vnFullscreenToggle = fullscreenToggle;
        controller.vnSettingsCloseButton = settingsCloseButton;
        controller.vnSettingsResetButton = settingsResetButton;
        controller.uiClickSfx = TryLoadAudioClip(UiClickSfxWavPath, UiClickSfxMp3Path, UiClickSfxOggPath);

        EditorSceneManager.SaveScene(scene, VNPrototypeScenePath);
        EnsureBuildSettingsScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("VNPrototype scene rebuilt successfully.");
    }

    public static void BuildVNPrototypeSceneBatch()
    {
        BuildVNPrototypeScene();
        EditorApplication.Exit(0);
    }

    private static void CreateMainCamera()
    {
        GameObject cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);

        Camera camera = cameraGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 1000f;

        cameraGo.AddComponent<AudioListener>();
    }

    private static Canvas CreateCanvas()
    {
        GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static void CreateEventSystem()
    {
        GameObject eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystemGo.GetComponent<EventSystem>().sendNavigationEvents = true;
    }

    private static Image CreateBackgroundImage(Transform parent)
    {
        GameObject background = CreateUiObject("Background Image", parent);
        StretchFull(background.GetComponent<RectTransform>());

        Image image = background.AddComponent<Image>();
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.raycastTarget = false;
        return image;
    }

    private static Image CreateCharacterImage(Transform parent)
    {
        GameObject character = CreateUiObject("Character Image", parent);
        RectTransform rect = character.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(-140f, -220f);
        rect.sizeDelta = new Vector2(850f, 1200f);

        Image image = character.AddComponent<Image>();
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    private static GameObject CreateDialogueBox(Transform parent)
    {
        GameObject box = CreateUiObject("Dialogue Box", parent);
        RectTransform rect = box.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 38f);
        rect.sizeDelta = new Vector2(1560f, 250f);

        Image image = box.AddComponent<Image>();
        image.color = new Color(0.025f, 0.018f, 0.045f, 0.68f);
        image.raycastTarget = true;

        Shadow shadow = box.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        shadow.effectDistance = new Vector2(4f, -4f);

        return box;
    }

    private static GameObject CreateNameBox(Transform parent, out TextMeshProUGUI speakerText)
    {
        GameObject nameBox = CreateUiObject("Name Box", parent);
        RectTransform rect = nameBox.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(54f, 26f);
        rect.sizeDelta = new Vector2(310f, 62f);

        Image image = nameBox.AddComponent<Image>();
        image.color = new Color(0.08f, 0.045f, 0.13f, 0.86f);
        image.raycastTarget = false;

        speakerText = CreateTMPText("Speaker Text", nameBox.transform, string.Empty, 28, new Color(0.94f, 0.9f, 0.98f, 1f));
        speakerText.alignment = TextAlignmentOptions.Center;
        StretchFull(speakerText.rectTransform, 18f, 18f, 0f, 0f);

        return nameBox;
    }

    private static TextMeshProUGUI CreateDialogueText(Transform parent)
    {
        TextMeshProUGUI text = CreateTMPText("Dialogue Text", parent, string.Empty, 31, new Color(0.92f, 0.88f, 0.96f, 1f));
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(58f, 42f);
        rect.offsetMax = new Vector2(-170f, -42f);
        return text;
    }

    private static Button CreateNextButton(Transform parent)
    {
        Button button = CreateStyledButton(parent, "Next", new Vector2(128f, 58f), 24);
        button.gameObject.name = "Next Button";

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-46f, 38f);
        return button;
    }

    private static GameObject CreateChoicePanel(
        Transform parent,
        out Button choiceMashaButton,
        out Button choiceArtemButton,
        out Button choiceLeraButton)
    {
        GameObject panel = CreateUiObject("Choice Panel", parent);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -90f);
        rect.sizeDelta = new Vector2(680f, 230f);

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        choiceMashaButton = CreateChoiceButton(panel.transform, "Choice Masha Button");
        choiceArtemButton = CreateChoiceButton(panel.transform, "Choice Artem Button");
        choiceLeraButton = CreateChoiceButton(panel.transform, "Choice Lera Button");
        return panel;
    }

    private static Button CreateChoiceButton(Transform parent, string name)
    {
        Button button = CreateStyledButton(parent, "Choice", new Vector2(680f, 62f), 24);
        button.gameObject.name = name;
        return button;
    }

    private static void CreateQuickMenu(Transform parent, VNDialogueController controller)
    {
        GameObject menu = CreateUiObject("VN Quick Menu", parent);
        RectTransform rect = menu.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-36f, -28f);
        rect.sizeDelta = new Vector2(590f, 48f);

        Image background = menu.AddComponent<Image>();
        background.color = new Color(0.02f, 0.014f, 0.04f, 0.22f);
        background.raycastTarget = false;

        HorizontalLayoutGroup layout = menu.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 9f;
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        Button saveButton = CreateQuickMenuButton(menu.transform, "Быстр. сохр.", 110f);
        UnityEventTools.AddPersistentListener(saveButton.onClick, controller.SaveGame);

        Button loadButton = CreateQuickMenuButton(menu.transform, "Быстр. загр.", 110f);
        UnityEventTools.AddPersistentListener(loadButton.onClick, controller.LoadGame);

        Button backlogButton = CreateQuickMenuButton(menu.transform, "История", 100f);
        UnityEventTools.AddPersistentListener(backlogButton.onClick, controller.ShowBacklog);

        Button settingsButton = CreateQuickMenuButton(menu.transform, "Настройки", 120f);
        UnityEventTools.AddPersistentListener(settingsButton.onClick, controller.OpenSettings);

        Button menuButton = CreateQuickMenuButton(menu.transform, "Меню", 90f);
        UnityEventTools.AddPersistentListener(menuButton.onClick, controller.ShowConfirmExit);
    }

    private static Button CreateQuickMenuButton(Transform parent, string labelText, float width)
    {
        Button button = CreateStyledButton(parent, labelText, new Vector2(width, 42f), 19);
        button.gameObject.name = labelText + " Button";
        return button;
    }

    private static GameObject CreateBacklogPanel(
        Transform parent,
        out TextMeshProUGUI backlogText,
        out Button closeButton)
    {
        GameObject panel = CreateOverlayPanel("Backlog Panel", parent, new Color(0f, 0f, 0f, 0.75f));
        GameObject window = CreateWindow(panel.transform, "Backlog Window", new Vector2(1200f, 760f), Vector2.zero);
        CreateAccentLine(window.transform, "Backlog Accent Line");

        TextMeshProUGUI title = CreateTMPText("Title", window.transform, "История", 42, new Color(0.94f, 0.9f, 0.98f, 1f));
        title.alignment = TextAlignmentOptions.Center;
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -28f);
        titleRect.sizeDelta = new Vector2(-120f, 68f);

        CreateBacklogScrollView(window.transform, out backlogText);
        closeButton = CreateCloseButton(window.transform, "Backlog Close Button", "Закрыть");
        return panel;
    }

    private static void CreateBacklogScrollView(Transform parent, out TextMeshProUGUI backlogText)
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
        viewport.AddComponent<RectMask2D>();

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

        ContentSizeFitter contentSizeFitter = content.AddComponent<ContentSizeFitter>();
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        backlogText = CreateTMPText("Backlog Text", content.transform, string.Empty, 28, new Color(0.9f, 0.86f, 0.96f, 1f));
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
        StretchFull(slidingArea.GetComponent<RectTransform>());

        GameObject handle = CreateUiObject("Handle", slidingArea.transform);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        StretchFull(handleRect);

        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = new Color(0.55f, 0.32f, 0.72f, 0.85f);
        handleImage.raycastTarget = true;

        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;
        return scrollbarGo;
    }

    private static GameObject CreateNotificationPanel(Transform parent, out TextMeshProUGUI notificationText)
    {
        GameObject panel = CreateUiObject("Notification Panel", parent);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -92f);
        rect.sizeDelta = new Vector2(420f, 54f);

        Image image = panel.AddComponent<Image>();
        image.color = new Color(0.025f, 0.018f, 0.045f, 0.82f);
        image.raycastTarget = false;

        Shadow shadow = panel.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        shadow.effectDistance = new Vector2(3f, -3f);

        notificationText = CreateTMPText("Notification Text", panel.transform, string.Empty, 22, new Color(0.94f, 0.9f, 0.98f, 1f));
        notificationText.alignment = TextAlignmentOptions.Center;
        StretchFull(notificationText.rectTransform);
        return panel;
    }

    private static GameObject CreateConfirmExitPanel(
        Transform parent,
        out Button yesButton,
        out Button noButton)
    {
        GameObject panel = CreateOverlayPanel("Confirm Exit Panel", parent, new Color(0f, 0f, 0f, 0.7f));
        GameObject window = CreateWindow(panel.transform, "Confirm Exit Window", new Vector2(640f, 300f), Vector2.zero);

        TextMeshProUGUI title = CreateTMPText("Title", window.transform, "Вернуться в главное меню?", 30, new Color(0.94f, 0.9f, 0.98f, 1f));
        title.alignment = TextAlignmentOptions.Center;
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -48f);
        titleRect.sizeDelta = new Vector2(-80f, 54f);

        TextMeshProUGUI message = CreateTMPText("Message", window.transform, "Несохранённый прогресс может быть потерян.", 22, new Color(0.82f, 0.78f, 0.9f, 1f));
        message.alignment = TextAlignmentOptions.Center;
        RectTransform messageRect = message.rectTransform;
        messageRect.anchorMin = new Vector2(0f, 1f);
        messageRect.anchorMax = new Vector2(1f, 1f);
        messageRect.pivot = new Vector2(0.5f, 1f);
        messageRect.anchoredPosition = new Vector2(0f, -112f);
        messageRect.sizeDelta = new Vector2(-80f, 46f);

        GameObject buttonRow = CreateButtonRow(window.transform, new Vector2(300f, 54f), new Vector2(0f, 42f), TextAnchor.MiddleCenter);
        yesButton = CreateStyledButton(buttonRow.transform, "Да", new Vector2(130f, 54f), 24);
        noButton = CreateStyledButton(buttonRow.transform, "Нет", new Vector2(130f, 54f), 24);
        return panel;
    }

    private static GameObject CreateSettingsPanel(
        Transform parent,
        out Slider masterVolumeSlider,
        out Slider musicVolumeSlider,
        out Slider sfxVolumeSlider,
        out Slider textSpeedSlider,
        out Toggle fullscreenToggle,
        out Button closeButton,
        out Button resetButton)
    {
        GameObject panel = CreateOverlayPanel("VN Settings Panel", parent, new Color(0f, 0f, 0f, 0.7f));
        GameObject window = CreateWindow(panel.transform, "VN Settings Window", new Vector2(760f, 560f), Vector2.zero);

        TextMeshProUGUI title = CreateTMPText("Title", window.transform, "Настройки", 38, new Color(0.94f, 0.9f, 0.98f, 1f));
        title.alignment = TextAlignmentOptions.Center;
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -30f);
        titleRect.sizeDelta = new Vector2(-80f, 58f);

        GameObject content = CreateUiObject("Settings Content", window.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = new Vector2(0f, -112f);
        contentRect.sizeDelta = new Vector2(-120f, 320f);

        VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 16f;
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childControlWidth = false;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = false;
        contentLayout.childForceExpandHeight = false;

        masterVolumeSlider = CreateSettingsSlider(content.transform, "Master Volume", 0f, 1f, 1f);
        musicVolumeSlider = CreateSettingsSlider(content.transform, "Music Volume", 0f, 1f, 1f);
        sfxVolumeSlider = CreateSettingsSlider(content.transform, "SFX Volume", 0f, 1f, 1f);
        textSpeedSlider = CreateSettingsSlider(content.transform, "Text Speed", 0.25f, 3f, 1f);
        fullscreenToggle = CreateSettingsToggle(content.transform, "Fullscreen");

        GameObject buttonRow = CreateButtonRow(window.transform, new Vector2(300f, 54f), new Vector2(-48f, 38f), TextAnchor.MiddleRight);
        resetButton = CreateStyledButton(buttonRow.transform, "Сбросить", new Vector2(140f, 50f), 22);
        closeButton = CreateStyledButton(buttonRow.transform, "Закрыть", new Vector2(140f, 50f), 22);

        return panel;
    }

    private static Slider CreateSettingsSlider(Transform parent, string labelText, float minValue, float maxValue, float value)
    {
        GameObject row = CreateSettingsRow(parent, labelText);

        GameObject sliderGo = CreateUiObject($"{labelText} Slider", row.transform);
        RectTransform sliderRect = sliderGo.GetComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(360f, 32f);

        Image hitArea = sliderGo.AddComponent<Image>();
        hitArea.color = new Color(0f, 0f, 0f, 0f);
        hitArea.raycastTarget = true;

        Slider slider = sliderGo.AddComponent<Slider>();
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.value = value;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;
        slider.interactable = true;

        GameObject background = CreateUiObject("Background", sliderGo.transform);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(1f, 0.5f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.anchoredPosition = Vector2.zero;
        backgroundRect.sizeDelta = new Vector2(0f, 8f);
        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = new Color(0.12f, 0.1f, 0.18f, 1f);
        backgroundImage.raycastTarget = false;

        GameObject fillArea = CreateUiObject("Fill Area", sliderGo.transform);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(9f, 0f);
        fillAreaRect.offsetMax = new Vector2(-9f, 0f);

        GameObject fill = CreateUiObject("Fill", fillArea.transform);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0.5f);
        fillRect.anchorMax = new Vector2(1f, 0.5f);
        fillRect.pivot = new Vector2(0.5f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = new Vector2(0f, 8f);
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.58f, 0.32f, 0.78f, 1f);
        fillImage.raycastTarget = false;

        GameObject handleArea = CreateUiObject("Handle Slide Area", sliderGo.transform);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(9f, 0f);
        handleAreaRect.offsetMax = new Vector2(-9f, 0f);

        GameObject handle = CreateUiObject("Handle", handleArea.transform);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(18f, 24f);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = new Color(0.92f, 0.88f, 0.98f, 1f);
        handleImage.raycastTarget = true;

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        return slider;
    }

    private static Toggle CreateSettingsToggle(Transform parent, string labelText)
    {
        GameObject row = CreateSettingsRow(parent, labelText);

        GameObject toggleGo = CreateUiObject($"{labelText} Toggle", row.transform);
        RectTransform toggleRect = toggleGo.GetComponent<RectTransform>();
        toggleRect.sizeDelta = new Vector2(360f, 32f);

        Toggle toggle = toggleGo.AddComponent<Toggle>();
        toggle.interactable = true;

        GameObject background = CreateUiObject("Background", toggleGo.transform);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0f, 0.5f);
        backgroundRect.pivot = new Vector2(0f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(28f, 28f);

        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = new Color(0.12f, 0.1f, 0.18f, 1f);
        backgroundImage.raycastTarget = true;

        GameObject checkmark = CreateUiObject("Checkmark", background.transform);
        RectTransform checkmarkRect = checkmark.GetComponent<RectTransform>();
        checkmarkRect.anchorMin = new Vector2(0.5f, 0.5f);
        checkmarkRect.anchorMax = new Vector2(0.5f, 0.5f);
        checkmarkRect.pivot = new Vector2(0.5f, 0.5f);
        checkmarkRect.sizeDelta = new Vector2(18f, 18f);
        Image checkmarkImage = checkmark.AddComponent<Image>();
        checkmarkImage.color = new Color(0.58f, 0.32f, 0.78f, 1f);
        checkmarkImage.raycastTarget = false;

        toggle.targetGraphic = backgroundImage;
        toggle.graphic = checkmarkImage;
        return toggle;
    }

    private static GameObject CreateSettingsRow(Transform parent, string labelText)
    {
        GameObject row = CreateUiObject($"{labelText} Row", parent);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(620f, 42f);

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 24f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI label = CreateTMPText("Label", row.transform, labelText, 22, new Color(0.82f, 0.78f, 0.9f, 1f));
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.rectTransform.sizeDelta = new Vector2(220f, 36f);
        return row;
    }

    private static GameObject CreateDebugStatsPanel(Transform parent, out TextMeshProUGUI statsText)
    {
        GameObject panel = CreateUiObject("Debug Stats Panel", parent);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-36f, -92f);
        rect.sizeDelta = new Vector2(280f, 310f);

        Image image = panel.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.58f);
        image.raycastTarget = true;

        statsText = CreateTMPText("Stats Text", panel.transform, "DEBUG STATS", 18, new Color(0.88f, 0.84f, 0.94f, 1f));
        statsText.alignment = TextAlignmentOptions.TopLeft;
        StretchFull(statsText.rectTransform, 16f, 16f, 14f, 14f);
        return panel;
    }

    private static void CreateDebugStatsController(GameObject debugStatsPanel, TextMeshProUGUI debugStatsText)
    {
        GameObject controllerGo = new GameObject("Debug Stats Panel Controller");

        DebugStatsPanelController panelController = controllerGo.AddComponent<DebugStatsPanelController>();
        panelController.root = debugStatsPanel;
        panelController.visibleByDefault = false;

        DebugStatsView statsView = controllerGo.AddComponent<DebugStatsView>();
        statsView.root = debugStatsPanel;
        statsView.statsText = debugStatsText;
    }

    private static GameObject CreateOverlayPanel(string name, Transform parent, Color dimColor)
    {
        GameObject panel = CreateUiObject(name, parent);
        StretchFull(panel.GetComponent<RectTransform>());
        Image dimImage = panel.AddComponent<Image>();
        dimImage.color = dimColor;
        dimImage.raycastTarget = true;
        return panel;
    }

    private static GameObject CreateWindow(Transform parent, string name, Vector2 size, Vector2 position)
    {
        GameObject window = CreateUiObject(name, parent);
        RectTransform rect = window.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = window.AddComponent<Image>();
        image.color = new Color(0.025f, 0.018f, 0.045f, 0.94f);
        image.raycastTarget = true;

        Shadow shadow = window.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(5f, -5f);
        return window;
    }

    private static void CreateAccentLine(Transform parent, string name)
    {
        GameObject line = CreateUiObject(name, parent);
        RectTransform rect = line.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(3f, -48f);

        Image image = line.AddComponent<Image>();
        image.color = new Color(0.55f, 0.22f, 0.8f, 0.65f);
        image.raycastTarget = false;
    }

    private static GameObject CreateButtonRow(Transform parent, Vector2 size, Vector2 position, TextAnchor alignment)
    {
        GameObject row = CreateUiObject("Button Row", parent);
        RectTransform rect = row.GetComponent<RectTransform>();
        rect.anchorMin = alignment == TextAnchor.MiddleRight ? new Vector2(1f, 0f) : new Vector2(0.5f, 0f);
        rect.anchorMax = alignment == TextAnchor.MiddleRight ? new Vector2(1f, 0f) : new Vector2(0.5f, 0f);
        rect.pivot = alignment == TextAnchor.MiddleRight ? new Vector2(1f, 0f) : new Vector2(0.5f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 18f;
        layout.childAlignment = alignment;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return row;
    }

    private static Button CreateCloseButton(Transform parent, string name, string labelText)
    {
        Button button = CreateStyledButton(parent, labelText, new Vector2(190f, 54f), 24);
        button.gameObject.name = name;
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-82f, 34f);
        return button;
    }

    private static Button CreateStyledButton(Transform parent, string labelText, Vector2 size, int fontSize)
    {
        GameObject buttonGo = CreateUiObject(labelText + " Button", parent);
        RectTransform rect = buttonGo.GetComponent<RectTransform>();
        rect.sizeDelta = size;

        Image image = buttonGo.AddComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = true;

        Button button = buttonGo.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.04f, 0.025f, 0.07f, 0.58f);
        colors.highlightedColor = new Color(0.38f, 0.18f, 0.5f, 0.78f);
        colors.pressedColor = new Color(0.5f, 0.22f, 0.65f, 0.9f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.02f, 0.02f, 0.03f, 0.35f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TextMeshProUGUI label = CreateTMPText("Text", buttonGo.transform, labelText, fontSize, new Color(0.9f, 0.86f, 0.96f, 0.95f));
        label.alignment = TextAlignmentOptions.Center;
        StretchFull(label.rectTransform);
        return button;
    }

    private static TextMeshProUGUI CreateTMPText(string name, Transform parent, string text, int fontSize, Color color)
    {
        GameObject textGo = CreateUiObject(name, parent);
        TextMeshProUGUI textComponent = textGo.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.color = color;
        textComponent.raycastTarget = false;
        return textComponent;
    }

    private static DialogueSceneData TryLoadSceneData()
    {
        DialogueSceneData sceneData = AssetDatabase.LoadAssetAtPath<DialogueSceneData>(PrimarySceneDataPath);
        if (sceneData != null)
        {
            return sceneData;
        }

        Debug.LogWarning($"Dialogue scene data was not found at {PrimarySceneDataPath}. Trying fallback: {FallbackSceneDataPath}");
        return AssetDatabase.LoadAssetAtPath<DialogueSceneData>(FallbackSceneDataPath);
    }

    private static AudioClip TryLoadAudioClip(params string[] paths)
    {
        foreach (string path in paths)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null)
            {
                Debug.Log($"Loaded audio clip: {path}");
                return clip;
            }
        }

        Debug.LogWarning($"Audio clip was not found. Checked paths: {string.Join(", ", paths)}");
        return null;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StretchFull(RectTransform rect, float left = 0f, float right = 0f, float bottom = 0f, float top = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void EnsureBuildSettingsScenes()
    {
        var scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(MainMenuScenePath, true),
            new EditorBuildSettingsScene(VNPrototypeScenePath, true)
        };

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
