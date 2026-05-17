using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class MainMenuSceneBuilder
{
    private const string MainMenuScenePath = "Assets/HowIFall/Scenes/MainMenu.unity";
    private const string VNPrototypeScenePath = "Assets/HowIFall/Scenes/VNPrototype.unity";

    [MenuItem("How I Fall/Build Main Menu Scene")]
    public static void BuildMainMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateMainCamera();
        var canvas = CreateCanvas();
        CreateEventSystem();

        var managers = new GameObject("Managers");
        var mainMenuController = managers.AddComponent<MainMenuController>();
        managers.AddComponent<GameState>();
        managers.AddComponent<SaveManager>();
        managers.AddComponent<SettingsManager>();

        CreateMainMenuRoot(canvas.transform, mainMenuController);
        var settingsPanelController = CreateSettingsPanel(canvas.transform);
        mainMenuController.settingsPanel = settingsPanelController;

        EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        EnsureBuildSettingsScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("MainMenu scene rebuilt successfully.");
    }

    public static void BuildMainMenuSceneBatch()
    {
        BuildMainMenuScene();
        EditorApplication.Exit(0);
    }

    private static Camera CreateMainCamera()
    {
        var cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);

        var camera = cameraGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 1000f;
        return camera;
    }

    private static Canvas CreateCanvas()
    {
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static void CreateEventSystem()
    {
        var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        var eventSystem = eventSystemGo.GetComponent<EventSystem>();
        eventSystem.sendNavigationEvents = true;
    }

    private static void CreateMainMenuRoot(Transform canvas, MainMenuController controller)
    {
        var root = CreateUiObject("MainMenuRoot", canvas);
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(520f, 520f);
        rootRect.anchoredPosition = Vector2.zero;

        var layout = root.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 20f;
        layout.childControlHeight = false;
        layout.childControlWidth = false;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;

        var startButton = CreateButton(root.transform, "Start", new Vector2(380f, 72f));
        var continueButton = CreateButton(root.transform, "Continue", new Vector2(380f, 72f));
        var settingsButton = CreateButton(root.transform, "Settings", new Vector2(380f, 72f));
        var exitButton = CreateButton(root.transform, "Exit", new Vector2(380f, 72f));

        UnityEventTools.AddPersistentListener(startButton.onClick, controller.StartGame);
        UnityEventTools.AddPersistentListener(continueButton.onClick, controller.ContinueGame);
        UnityEventTools.AddPersistentListener(settingsButton.onClick, controller.OpenSettings);
        UnityEventTools.AddPersistentListener(exitButton.onClick, controller.ExitGame);
    }

    private static SettingsPanelController CreateSettingsPanel(Transform canvas)
    {
        var panelRoot = CreateUiObject("Settings Panel", canvas);
        panelRoot.SetActive(false);

        var panelRootRect = panelRoot.GetComponent<RectTransform>();
        panelRootRect.anchorMin = Vector2.zero;
        panelRootRect.anchorMax = Vector2.one;
        panelRootRect.offsetMin = Vector2.zero;
        panelRootRect.offsetMax = Vector2.zero;

        var dimImage = panelRoot.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.7f);
        dimImage.raycastTarget = false;

        var panel = CreateUiObject("Panel", panelRoot.transform);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(820f, 700f);
        panelRect.anchoredPosition = Vector2.zero;
        var panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        panelImage.raycastTarget = false;

        var content = CreateUiObject("Content", panel.transform);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.one;
        contentRect.offsetMin = new Vector2(40f, 40f);
        contentRect.offsetMax = new Vector2(-40f, -40f);

        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 14f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.padding = new RectOffset(0, 0, 0, 0);

        var title = CreateLabel(content.transform, "Settings", 40, TextAnchor.MiddleCenter, new Vector2(0f, 64f));
        title.raycastTarget = false;

        Slider master = CreateLabeledSlider(content.transform, "Master Volume", 0f, 1f, 1f);
        Slider music = CreateLabeledSlider(content.transform, "Music Volume", 0f, 1f, 1f);
        Slider sfx = CreateLabeledSlider(content.transform, "SFX Volume", 0f, 1f, 1f);
        Slider textSpeed = CreateLabeledSlider(content.transform, "Text Speed", 0.25f, 3f, 1f);

        var fullscreenRow = CreateRow(content.transform, 56f);
        var fullscreenLabel = CreateLabel(fullscreenRow.transform, "Fullscreen", 24, TextAnchor.MiddleLeft, new Vector2(0f, 48f));
        fullscreenLabel.raycastTarget = false;
        var fullscreenToggle = CreateToggle(fullscreenRow.transform);

        var buttonsRow = CreateRow(content.transform, 76f);
        var resetButton = CreateButton(buttonsRow.transform, "Reset", new Vector2(220f, 64f));
        var backButton = CreateButton(buttonsRow.transform, "Back", new Vector2(220f, 64f));

        var settingsController = panelRoot.AddComponent<SettingsPanelController>();
        settingsController.root = panelRoot;
        settingsController.masterVolumeSlider = master;
        settingsController.musicVolumeSlider = music;
        settingsController.sfxVolumeSlider = sfx;
        settingsController.textSpeedSlider = textSpeed;
        settingsController.fullscreenToggle = fullscreenToggle;

        UnityEventTools.AddPersistentListener(master.onValueChanged, settingsController.OnMasterVolumeChanged);
        UnityEventTools.AddPersistentListener(music.onValueChanged, settingsController.OnMusicVolumeChanged);
        UnityEventTools.AddPersistentListener(sfx.onValueChanged, settingsController.OnSfxVolumeChanged);
        UnityEventTools.AddPersistentListener(textSpeed.onValueChanged, settingsController.OnTextSpeedChanged);
        UnityEventTools.AddPersistentListener(fullscreenToggle.onValueChanged, settingsController.OnFullscreenChanged);
        UnityEventTools.AddPersistentListener(resetButton.onClick, settingsController.OnResetClicked);
        UnityEventTools.AddPersistentListener(backButton.onClick, settingsController.Hide);

        return settingsController;
    }

    private static GameObject CreateRow(Transform parent, float height)
    {
        var row = CreateUiObject("Row", parent);
        var rowRect = row.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, height);

        var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 16f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlHeight = false;
        rowLayout.childControlWidth = true;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childForceExpandWidth = true;
        return row;
    }

    private static Slider CreateLabeledSlider(Transform parent, string labelText, float min, float max, float value)
    {
        var row = CreateRow(parent, 64f);

        var labelGo = CreateUiObject("Label", row.transform);
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.sizeDelta = new Vector2(240f, 56f);
        var label = labelGo.AddComponent<Text>();
        label.text = labelText;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 24;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = Color.white;
        label.raycastTarget = false;

        var slider = CreateSlider(row.transform, min, max, value);
        var sliderRect = slider.GetComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(460f, 36f);
        return slider;
    }

    private static Slider CreateSlider(Transform parent, float min, float max, float value)
    {
        var sliderGo = CreateUiObject("Slider", parent);
        var sliderImage = sliderGo.AddComponent<Image>();
        sliderImage.color = new Color(0.16f, 0.16f, 0.16f, 1f);
        sliderImage.raycastTarget = false;

        var slider = sliderGo.AddComponent<Slider>();
        slider.interactable = true;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;

        var background = CreateUiObject("Background", sliderGo.transform);
        var bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.25f);
        bgRect.anchorMax = new Vector2(1f, 0.75f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImage = background.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        bgImage.raycastTarget = false;

        var fillArea = CreateUiObject("Fill Area", sliderGo.transform);
        var fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(10f, 0f);
        fillAreaRect.offsetMax = new Vector2(-10f, 0f);

        var fill = CreateUiObject("Fill", fillArea.transform);
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.35f, 0.7f, 1f, 1f);
        fillImage.raycastTarget = false;

        var handleArea = CreateUiObject("Handle Slide Area", sliderGo.transform);
        var handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(10f, 0f);
        handleAreaRect.offsetMax = new Vector2(-10f, 0f);

        var handle = CreateUiObject("Handle", handleArea.transform);
        var handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 0f);
        handleRect.anchorMax = new Vector2(0.5f, 1f);
        handleRect.sizeDelta = new Vector2(24f, 0f);
        handleRect.anchoredPosition = Vector2.zero;
        var handleImage = handle.AddComponent<Image>();
        handleImage.color = Color.white;
        handleImage.raycastTarget = true;

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;

        return slider;
    }

    private static Toggle CreateToggle(Transform parent)
    {
        var toggleGo = CreateUiObject("Fullscreen Toggle", parent);
        var toggleRect = toggleGo.GetComponent<RectTransform>();
        toggleRect.sizeDelta = new Vector2(32f, 32f);

        var background = toggleGo.AddComponent<Image>();
        background.color = new Color(0.18f, 0.18f, 0.18f, 1f);

        var toggle = toggleGo.AddComponent<Toggle>();
        toggle.interactable = true;
        toggle.targetGraphic = background;

        var checkmarkGo = CreateUiObject("Checkmark", toggleGo.transform);
        var checkRect = checkmarkGo.GetComponent<RectTransform>();
        checkRect.anchorMin = Vector2.zero;
        checkRect.anchorMax = Vector2.one;
        checkRect.offsetMin = new Vector2(6f, 6f);
        checkRect.offsetMax = new Vector2(-6f, -6f);
        var checkmark = checkmarkGo.AddComponent<Image>();
        checkmark.color = new Color(0.35f, 0.7f, 1f, 1f);
        toggle.graphic = checkmark;
        toggle.isOn = true;

        return toggle;
    }

    private static Button CreateButton(Transform parent, string label, Vector2 size)
    {
        var buttonGo = CreateUiObject(label + " Button", parent);
        var rect = buttonGo.GetComponent<RectTransform>();
        rect.sizeDelta = size;

        var image = buttonGo.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        var button = buttonGo.AddComponent<Button>();
        button.interactable = true;
        button.targetGraphic = image;

        var text = CreateLabel(buttonGo.transform, label, 30, TextAnchor.MiddleCenter, size);
        text.raycastTarget = false;
        return button;
    }

    private static Text CreateLabel(Transform parent, string textValue, int fontSize, TextAnchor anchor, Vector2 size)
    {
        var textGo = CreateUiObject("Text", parent);
        var rect = textGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.sizeDelta = size;

        var text = textGo.AddComponent<Text>();
        text.text = textValue;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = anchor;
        return text;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
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
