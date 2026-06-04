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
    private const string PrimarySceneDataPath = VNUITestSceneContentBuilder.UITestScenePath;
    private const string FallbackSceneDataPath = VNUITestSceneContentBuilder.UITestScenePath;
    private const string SceneRegistryPath = "Assets/HowIFall/Data/Dialogues/DialogueSceneRegistry.asset";
    private const string LogoPath = "Assets/HowIFall/Art/UI/MainMenu/logo_how_i_fall.png";
    private const string PlaceholderCharacterPath = "Assets/HowIFall/Art/Characters/Placeholders/placeholder_female_student_default.png";
    private const string UiClickSfxWavPath = "Assets/HowIFall/Audio/SFX/ui_click.wav";
    private const string UiClickSfxMp3Path = "Assets/HowIFall/Audio/SFX/ui_click.mp3";
    private const string UiClickSfxOggPath = "Assets/HowIFall/Audio/SFX/ui_click.ogg";

    [MenuItem("How I Fall/Build VN Prototype Scene")]
    public static void BuildVNPrototypeScene()
    {
        VNUITestSceneContentBuilder.BuildUITestSceneAsset();
        ConfigureSpriteImportSettings(PlaceholderCharacterPath);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateMainCamera();
        Canvas canvas = CreateCanvas();
        CreateEventSystem();

        GameObject managers = new GameObject("Managers");
        managers.AddComponent<GameState>();
        managers.AddComponent<SaveManager>();
        managers.AddComponent<SettingsManager>();
        managers.AddComponent<AudioManager>();
        managers.AddComponent<SceneFlowManager>();

        GameObject vnRoot = CreateUiObject("VN Root", canvas.transform);
        StretchFull(vnRoot.GetComponent<RectTransform>());

        Image backgroundImage = CreateBackgroundImage(vnRoot.transform);
        Image characterImage = CreateCharacterImage(vnRoot.transform);
        CreateTopLeftBrandBlock(vnRoot.transform);

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

        CreateTopMenuButton(canvas.transform, controller);
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
        controller.characterLeftPosition = new Vector2(210f, -1120f);
        controller.characterCenterPosition = new Vector2(260f, -1120f);
        controller.characterRightPosition = new Vector2(430f, -1120f);
        controller.characterSoloPosition = new Vector2(210f, -1120f);
        controller.characterDefaultSize = new Vector2(1500f, 2100f);
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
        GameObject character = CreateUiObject("Placeholder Character", parent);
        RectTransform rect = character.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(210f, -1120f);
        rect.sizeDelta = new Vector2(1500f, 2100f);

        Image image = character.AddComponent<Image>();
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    private static void CreateTopLeftBrandBlock(Transform parent)
    {
        GameObject shade = CreateUiObject("Top Left Soft Shade", parent);
        RectTransform shadeRect = shade.GetComponent<RectTransform>();
        shadeRect.anchorMin = new Vector2(0f, 1f);
        shadeRect.anchorMax = new Vector2(0f, 1f);
        shadeRect.pivot = new Vector2(0f, 1f);
        shadeRect.anchoredPosition = Vector2.zero;
        shadeRect.sizeDelta = new Vector2(360f, 240f);
        Image shadeImage = shade.AddComponent<Image>();
        shadeImage.color = new Color(0f, 0f, 0f, 0.22f);
        shadeImage.raycastTarget = false;
        shade.transform.SetSiblingIndex(1);

        Sprite logoSprite = TryLoadSprite(LogoPath);
        if (logoSprite != null)
        {
            GameObject logo = CreateUiObject("How I Fall Logo", parent);
            RectTransform logoRect = logo.GetComponent<RectTransform>();
            logoRect.anchorMin = new Vector2(0f, 1f);
            logoRect.anchorMax = new Vector2(0f, 1f);
            logoRect.pivot = new Vector2(0f, 1f);
            logoRect.anchoredPosition = new Vector2(34f, -24f);
            logoRect.sizeDelta = new Vector2(230f, 130f);

            Image logoImage = logo.AddComponent<Image>();
            logoImage.sprite = logoSprite;
            logoImage.type = Image.Type.Simple;
            logoImage.preserveAspect = true;
            logoImage.color = Color.white;
            logoImage.raycastTarget = false;
        }

        GameObject chapter = CreateUiObject("Chapter Info", parent);
        RectTransform chapterRect = chapter.GetComponent<RectTransform>();
        chapterRect.anchorMin = new Vector2(0f, 1f);
        chapterRect.anchorMax = new Vector2(0f, 1f);
        chapterRect.pivot = new Vector2(0f, 1f);
        chapterRect.anchoredPosition = new Vector2(46f, -140f);
        chapterRect.sizeDelta = new Vector2(330f, 74f);

        GameObject accent = CreateUiObject("Chapter Red Accent", chapter.transform);
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(4f, -4f);
        Image accentImage = accent.AddComponent<Image>();
        accentImage.color = new Color(0.9f, 0.08f, 0.06f, 1f);
        accentImage.raycastTarget = false;

        TextMeshProUGUI chapterLabel = CreateTMPText("Chapter Label", chapter.transform, "CHAPTER 1", 18, new Color(1f, 1f, 1f, 0.85f));
        chapterLabel.fontStyle = FontStyles.Bold;
        chapterLabel.characterSpacing = 7f;
        chapterLabel.alignment = TextAlignmentOptions.Left;
        AddSoftTextShadow(chapterLabel.gameObject);
        RectTransform chapterLabelRect = chapterLabel.rectTransform;
        chapterLabelRect.anchorMin = new Vector2(0f, 1f);
        chapterLabelRect.anchorMax = new Vector2(1f, 1f);
        chapterLabelRect.pivot = new Vector2(0f, 1f);
        chapterLabelRect.offsetMin = new Vector2(18f, -30f);
        chapterLabelRect.offsetMax = new Vector2(0f, 0f);

        TextMeshProUGUI chapterTitle = CreateTMPText("Chapter Title", chapter.transform, "Порог", 24, new Color(1f, 1f, 1f, 0.95f));
        chapterTitle.alignment = TextAlignmentOptions.Left;
        AddSoftTextShadow(chapterTitle.gameObject);
        RectTransform chapterTitleRect = chapterTitle.rectTransform;
        chapterTitleRect.anchorMin = new Vector2(0f, 0f);
        chapterTitleRect.anchorMax = new Vector2(1f, 0f);
        chapterTitleRect.pivot = new Vector2(0f, 0f);
        chapterTitleRect.offsetMin = new Vector2(18f, 0f);
        chapterTitleRect.offsetMax = new Vector2(0f, 38f);
    }

    private static void CreateTopMenuButton(Transform parent, VNDialogueController controller)
    {
        Button menuButton = CreateStyledButton(parent, "МЕНЮ", new Vector2(132f, 38f), 17);
        menuButton.gameObject.name = "Top Menu Button";

        RectTransform rect = menuButton.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-56f, -38f);

        Image image = menuButton.GetComponent<Image>();
        image.color = Color.white;

        ColorBlock colors = menuButton.colors;
        colors.normalColor = new Color(0.015f, 0.035f, 0.075f, 0.72f);
        colors.highlightedColor = new Color(0.55f, 0.08f, 0.07f, 0.86f);
        colors.pressedColor = new Color(0.9f, 0.08f, 0.06f, 0.95f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.02f, 0.02f, 0.03f, 0.35f);
        menuButton.colors = colors;

        UnityEventTools.AddPersistentListener(menuButton.onClick, controller.ShowConfirmExit);
    }

    private static GameObject CreateDialogueBox(Transform parent)
    {
        GameObject box = CreateUiObject("Dialogue Box", parent);
        RectTransform rect = box.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(220f, 92f);
        rect.offsetMax = new Vector2(-220f, 272f);

        Image image = box.AddComponent<Image>();
        image.color = new Color(0.015f, 0.035f, 0.075f, 0.79f);
        image.raycastTarget = true;

        Outline outline = box.AddComponent<Outline>();
        outline.effectColor = new Color(0.62f, 0.78f, 1f, 0.25f);
        outline.effectDistance = new Vector2(1f, -1f);

        Shadow shadow = box.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
        shadow.effectDistance = new Vector2(5f, -5f);

        return box;
    }

    private static GameObject CreateNameBox(Transform parent, out TextMeshProUGUI speakerText)
    {
        GameObject nameBox = CreateUiObject("Name Box", parent);
        RectTransform rect = nameBox.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(50f, 24f);
        rect.sizeDelta = new Vector2(240f, 58f);

        Image image = nameBox.AddComponent<Image>();
        image.color = new Color(0.055f, 0.18f, 0.36f, 0.96f);
        image.raycastTarget = false;

        GameObject accent = CreateUiObject("Name Box Red Underline", nameBox.transform);
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(1f, 0f);
        accentRect.pivot = new Vector2(0.5f, 0f);
        accentRect.offsetMin = new Vector2(18f, 0f);
        accentRect.offsetMax = new Vector2(-18f, 4f);
        Image accentImage = accent.AddComponent<Image>();
        accentImage.color = new Color(0.9f, 0.08f, 0.06f, 1f);
        accentImage.raycastTarget = false;

        speakerText = CreateTMPText("Speaker Text", nameBox.transform, string.Empty, 30, new Color(1f, 1f, 1f, 0.98f));
        speakerText.fontStyle = FontStyles.Bold | FontStyles.Italic;
        speakerText.alignment = TextAlignmentOptions.Center;
        StretchFull(speakerText.rectTransform, 18f, 18f, 4f, 0f);

        return nameBox;
    }

    private static TextMeshProUGUI CreateDialogueText(Transform parent)
    {
        TextMeshProUGUI text = CreateTMPText("Dialogue Text", parent, string.Empty, 32, new Color(1f, 1f, 1f, 0.95f));
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.lineSpacing = 6f;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(80f, 38f);
        rect.offsetMax = new Vector2(-80f, -52f);
        return text;
    }

    private static Button CreateNextButton(Transform parent)
    {
        GameObject buttonGo = CreateUiObject("Next Button", parent);
        RectTransform rect = buttonGo.GetComponent<RectTransform>();
        StretchFull(rect);

        Image hitArea = buttonGo.AddComponent<Image>();
        hitArea.color = new Color(0f, 0f, 0f, 0f);
        hitArea.raycastTarget = true;

        Button button = buttonGo.AddComponent<Button>();
        button.targetGraphic = hitArea;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0f, 0f, 0f, 0f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.03f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.06f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = colors.normalColor;
        button.colors = colors;

        TextMeshProUGUI indicator = CreateTMPText("Next Indicator", buttonGo.transform, "▼", 24, new Color(1f, 1f, 1f, 0.8f));
        indicator.alignment = TextAlignmentOptions.Center;
        RectTransform indicatorRect = indicator.rectTransform;
        indicatorRect.anchorMin = new Vector2(1f, 0f);
        indicatorRect.anchorMax = new Vector2(1f, 0f);
        indicatorRect.pivot = new Vector2(1f, 0f);
        indicatorRect.anchoredPosition = new Vector2(-42f, 26f);
        indicatorRect.sizeDelta = new Vector2(38f, 34f);
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
        rect.anchoredPosition = new Vector2(0f, 40f);
        rect.sizeDelta = new Vector2(720f, 360f);

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.015f, 0.035f, 0.075f, 0.88f);
        panelImage.raycastTarget = true;

        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.62f, 0.78f, 1f, 0.3f);
        outline.effectDistance = new Vector2(1f, -1f);

        Shadow shadow = panel.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        shadow.effectDistance = new Vector2(6f, -6f);

        TextMeshProUGUI title = CreateTMPText("Choice Title", panel.transform, "Что сделать?", 32, new Color(1f, 1f, 1f, 0.95f));
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -34f);
        titleRect.sizeDelta = new Vector2(-96f, 52f);

        GameObject buttonStack = CreateUiObject("Choice Button Stack", panel.transform);
        RectTransform stackRect = buttonStack.GetComponent<RectTransform>();
        stackRect.anchorMin = new Vector2(0.5f, 0.5f);
        stackRect.anchorMax = new Vector2(0.5f, 0.5f);
        stackRect.pivot = new Vector2(0.5f, 0.5f);
        stackRect.anchoredPosition = new Vector2(0f, -46f);
        stackRect.sizeDelta = new Vector2(560f, 198f);

        VerticalLayoutGroup layout = buttonStack.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        choiceMashaButton = CreateChoiceButton(buttonStack.transform, "Choice Option 1 Button");
        choiceArtemButton = CreateChoiceButton(buttonStack.transform, "Choice Option 2 Button");
        choiceLeraButton = CreateChoiceButton(buttonStack.transform, "Choice Option 3 Button");
        return panel;
    }

    private static Button CreateChoiceButton(Transform parent, string name)
    {
        Button button = CreateStyledButton(parent, "Choice", new Vector2(560f, 54f), 24);
        button.gameObject.name = name;

        Image image = button.GetComponent<Image>();
        image.color = Color.white;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.015f, 0.035f, 0.075f, 0.75f);
        colors.highlightedColor = new Color(0.85f, 0.08f, 0.07f, 0.9f);
        colors.pressedColor = new Color(0.9f, 0.08f, 0.06f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.02f, 0.02f, 0.03f, 0.35f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        Outline outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.2f);
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject marker = CreateUiObject("Red Accent", button.transform);
        RectTransform markerRect = marker.GetComponent<RectTransform>();
        markerRect.anchorMin = new Vector2(0f, 0.5f);
        markerRect.anchorMax = new Vector2(0f, 0.5f);
        markerRect.pivot = new Vector2(0f, 0.5f);
        markerRect.anchoredPosition = new Vector2(14f, 0f);
        markerRect.sizeDelta = new Vector2(4f, 28f);
        Image markerImage = marker.AddComponent<Image>();
        markerImage.color = new Color(0.9f, 0.08f, 0.06f, 0.75f);
        markerImage.raycastTarget = false;
        return button;
    }

    private static void CreateQuickMenu(Transform parent, VNDialogueController controller)
    {
        GameObject menu = CreateUiObject("VN Quick Menu", parent);
        RectTransform rect = menu.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(210f, 34f);
        rect.offsetMax = new Vector2(-210f, 76f);

        GameObject leftGroup = CreateQuickMenuGroup(menu.transform, "Quick Menu Left", TextAnchor.MiddleLeft);
        RectTransform leftRect = leftGroup.GetComponent<RectTransform>();
        leftRect.anchorMin = new Vector2(0f, 0f);
        leftRect.anchorMax = new Vector2(0.5f, 1f);
        leftRect.offsetMin = Vector2.zero;
        leftRect.offsetMax = Vector2.zero;

        GameObject rightGroup = CreateQuickMenuGroup(menu.transform, "Quick Menu Right", TextAnchor.MiddleRight);
        RectTransform rightRect = rightGroup.GetComponent<RectTransform>();
        rightRect.anchorMin = new Vector2(0.5f, 0f);
        rightRect.anchorMax = new Vector2(1f, 1f);
        rightRect.offsetMin = Vector2.zero;
        rightRect.offsetMax = Vector2.zero;

        Button backlogButton = CreateQuickMenuButton(leftGroup.transform, "История", 94f, true);
        UnityEventTools.AddPersistentListener(backlogButton.onClick, controller.ShowBacklog);

        CreateQuickMenuButton(leftGroup.transform, "Авто", 72f, false);
        CreateQuickMenuButton(leftGroup.transform, "Пропуск", 96f, false);

        Button saveButton = CreateQuickMenuButton(rightGroup.transform, "Сохранить", 110f, true);
        UnityEventTools.AddPersistentListener(saveButton.onClick, controller.SaveGame);

        Button loadButton = CreateQuickMenuButton(rightGroup.transform, "Загрузить", 104f, true);
        UnityEventTools.AddPersistentListener(loadButton.onClick, controller.LoadGame);

        Button settingsButton = CreateQuickMenuButton(rightGroup.transform, "Настройки", 110f, true);
        UnityEventTools.AddPersistentListener(settingsButton.onClick, controller.OpenSettings);
    }

    private static GameObject CreateQuickMenuGroup(Transform parent, string name, TextAnchor alignment)
    {
        GameObject group = CreateUiObject(name, parent);
        HorizontalLayoutGroup layout = group.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 18f;
        layout.childAlignment = alignment;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return group;
    }

    private static Button CreateQuickMenuButton(Transform parent, string labelText, float width, bool interactable)
    {
        GameObject buttonGo = CreateUiObject(labelText + " Button", parent);
        RectTransform rect = buttonGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, 42f);

        Image hitArea = buttonGo.AddComponent<Image>();
        hitArea.color = new Color(0f, 0f, 0f, 0f);
        hitArea.raycastTarget = true;

        Button button = buttonGo.AddComponent<Button>();
        button.targetGraphic = hitArea;
        button.interactable = interactable;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0f, 0f, 0f, 0f);
        colors.highlightedColor = new Color(0.9f, 0.08f, 0.06f, 0.16f);
        colors.pressedColor = new Color(0.9f, 0.08f, 0.06f, 0.28f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0f, 0f, 0f, 0f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        Color textColor = interactable
            ? new Color(1f, 1f, 1f, 0.78f)
            : new Color(1f, 1f, 1f, 0.35f);
        TextMeshProUGUI label = CreateTMPText("Text", buttonGo.transform, labelText, 20, textColor);
        label.alignment = TextAlignmentOptions.Center;
        StretchFull(label.rectTransform);

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
        scrollbarImage.color = new Color(0.015f, 0.035f, 0.075f, 0.8f);
        scrollbarImage.raycastTarget = true;

        Scrollbar scrollbar = scrollbarGo.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        GameObject slidingArea = CreateUiObject("Sliding Area", scrollbarGo.transform);
        StretchFull(slidingArea.GetComponent<RectTransform>());

        GameObject handle = CreateUiObject("Handle", slidingArea.transform);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        StretchFull(handleRect);

        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = new Color(0.9f, 0.08f, 0.06f, 0.85f);
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
        image.color = new Color(0.015f, 0.035f, 0.075f, 0.84f);
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
        backgroundImage.color = new Color(0.015f, 0.035f, 0.075f, 1f);
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
        fillImage.color = new Color(0.9f, 0.08f, 0.06f, 1f);
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
        backgroundImage.color = new Color(0.015f, 0.035f, 0.075f, 1f);
        backgroundImage.raycastTarget = true;

        GameObject checkmark = CreateUiObject("Checkmark", background.transform);
        RectTransform checkmarkRect = checkmark.GetComponent<RectTransform>();
        checkmarkRect.anchorMin = new Vector2(0.5f, 0.5f);
        checkmarkRect.anchorMax = new Vector2(0.5f, 0.5f);
        checkmarkRect.pivot = new Vector2(0.5f, 0.5f);
        checkmarkRect.sizeDelta = new Vector2(18f, 18f);
        Image checkmarkImage = checkmark.AddComponent<Image>();
        checkmarkImage.color = new Color(0.9f, 0.08f, 0.06f, 1f);
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
        image.color = new Color(0.025f, 0.07f, 0.13f, 0.94f);
        image.raycastTarget = true;

        Outline outline = window.AddComponent<Outline>();
        outline.effectColor = new Color(0.62f, 0.78f, 1f, 0.25f);
        outline.effectDistance = new Vector2(1f, -1f);

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
        image.color = new Color(0.9f, 0.08f, 0.06f, 0.75f);
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
        colors.normalColor = new Color(0.015f, 0.035f, 0.075f, 0.78f);
        colors.highlightedColor = new Color(0.55f, 0.08f, 0.07f, 0.86f);
        colors.pressedColor = new Color(0.9f, 0.08f, 0.06f, 0.95f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.02f, 0.02f, 0.03f, 0.35f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TextMeshProUGUI label = CreateTMPText("Text", buttonGo.transform, labelText, fontSize, new Color(1f, 1f, 1f, 0.95f));
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

    private static void AddSoftTextShadow(GameObject target)
    {
        Shadow shadow = target.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        shadow.effectDistance = new Vector2(2f, -2f);
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

    private static Sprite TryLoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite != null)
        {
            return sprite;
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
        {
            Debug.LogWarning($"Sprite was not found at {path}.");
            return null;
        }

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

    private static void ConfigureSpriteImportSettings(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"Texture importer was not found for {path}.");
            return;
        }

        bool changed = false;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
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
