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
    private const string KeyVisualPath = "Assets/HowIFall/Art/UI/MainMenu/main_menu_key_visual.png";
    private const string MainMenuMusicMp3Path = "Assets/HowIFall/Audio/Music/main_menu_bgm.mp3";
    private const string MainMenuMusicOggPath = "Assets/HowIFall/Audio/Music/main_menu_bgm.ogg";
    private const string UiHoverSfxPath = "Assets/HowIFall/Audio/SFX/ui_hover.wav";
    private const string UiClickSfxPath = "Assets/HowIFall/Audio/SFX/ui_click.wav";

    [MenuItem("How I Fall/Build Main Menu Scene")]
    public static void BuildMainMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateMainCamera();
        var canvas = CreateCanvas();
        CreateEventSystem();
        var backgroundTransform = CreateBackgroundLayer(canvas.transform);
        var overlayGraphic = CreateOverlay(canvas.transform);

        var managers = new GameObject("Managers");
        var mainMenuController = managers.AddComponent<MainMenuController>();
        managers.AddComponent<GameState>();
        managers.AddComponent<SaveManager>();
        managers.AddComponent<SettingsManager>();
        managers.AddComponent<AudioManager>();
        var musicPlayer = managers.AddComponent<MainMenuMusicPlayer>();
        musicPlayer.musicClip = TryLoadMainMenuMusicClip();

        var menuCanvasGroup = CreateMainMenuRoot(canvas.transform, mainMenuController);
        var titleCanvasGroup = CreateGameLogo(canvas.transform, out var titleObject);
        var footerObject = CreateFooter(canvas.transform);

        var settingsPanelController = CreateSettingsPanel(canvas.transform);
        settingsPanelController.objectsToHideWhenOpen = new[] { titleObject, footerObject };
        mainMenuController.settingsPanel = settingsPanelController;

        CreateMainMenuAnimator(canvas.transform, backgroundTransform, menuCanvasGroup, titleCanvasGroup, overlayGraphic);

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

    private static RectTransform CreateBackgroundLayer(Transform canvas)
    {
        var bg = CreateUiObject("Background", canvas);
        StretchFull(bg.GetComponent<RectTransform>());
        var bgRect = bg.GetComponent<RectTransform>();
        bgRect.offsetMin = new Vector2(-40f, -40f);
        bgRect.offsetMax = new Vector2(40f, 40f);

        var image = bg.AddComponent<Image>();
        var keySprite = TryLoadKeyVisualSprite();
        if (keySprite != null)
        {
            image.sprite = keySprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
        }
        else
        {
            Debug.LogWarning("Main menu key visual not found at path: " + KeyVisualPath);
            image.color = new Color(0.035f, 0.03f, 0.055f, 1f);
        }

        image.raycastTarget = false;
        bg.transform.SetAsFirstSibling();
        return bgRect;
    }

    private static Sprite TryLoadKeyVisualSprite()
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(KeyVisualPath);
        ValidateKeyVisualAspect(texture);

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(KeyVisualPath);
        if (sprite != null)
        {
            return sprite;
        }

        if (texture == null)
        {
            return null;
        }

        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }

    private static AudioClip TryLoadMainMenuMusicClip()
    {
        var clip = TryLoadAudioClip(MainMenuMusicMp3Path);
        if (clip != null)
        {
            return clip;
        }

        return TryLoadAudioClip(MainMenuMusicOggPath);
    }

    private static AudioClip TryLoadAudioClip(string path)
    {
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    private static void ValidateKeyVisualAspect(Texture2D texture)
    {
        if (texture == null || texture.height == 0)
        {
            return;
        }

        float aspect = (float)texture.width / texture.height;
        float target = 16f / 9f;
        if (Mathf.Abs(aspect - target) > 0.01f)
        {
            Debug.LogWarning("Main menu key visual should be 16:9, recommended 1920x1080.");
        }
    }

    private static Graphic CreateOverlay(Transform canvas)
    {
        var overlay = CreateUiObject("Background Overlay", canvas);
        StretchFull(overlay.GetComponent<RectTransform>());
        var image = overlay.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.32f);
        image.raycastTarget = false;
        return image;
    }

    private static CanvasGroup CreateMainMenuRoot(Transform canvas, MainMenuController controller)
    {
        var root = CreateUiObject("MainMenuRoot", canvas);
        var canvasGroup = root.AddComponent<CanvasGroup>();
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 0.5f);
        rootRect.anchorMax = new Vector2(0f, 0.5f);
        rootRect.pivot = new Vector2(0f, 0.5f);
        rootRect.anchoredPosition = new Vector2(58f, 10f);
        rootRect.sizeDelta = new Vector2(380f, 450f);

        var panel = root.AddComponent<Image>();
        panel.color = new Color(0.035f, 0.025f, 0.06f, 0.66f);
        panel.raycastTarget = false;

        var panelShadow = root.AddComponent<Shadow>();
        panelShadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
        panelShadow.effectDistance = new Vector2(3f, -3f);

        var accent = CreateUiObject("Accent Line", root.transform);
        var accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = new Vector2(12f, 0f);
        accentRect.sizeDelta = new Vector2(2f, -24f);
        var accentImage = accent.AddComponent<Image>();
        accentImage.color = new Color(0.72f, 0.42f, 0.84f, 0.35f);
        accentImage.raycastTarget = false;

        var content = CreateUiObject("Menu Content", root.transform);
        StretchFull(content.GetComponent<RectTransform>(), 24f, 24f, 20f, 20f);

        string[] labels = { "Начать", "Загрузить", "Настройки", "Об игре", "Помощь", "Выход" };
        var methods = new System.Action<Button>[]
        {
            b => UnityEventTools.AddPersistentListener(b.onClick, controller.StartGame),
            b => UnityEventTools.AddPersistentListener(b.onClick, controller.ContinueGame),
            b => UnityEventTools.AddPersistentListener(b.onClick, controller.OpenSettings),
            b => UnityEventTools.AddPersistentListener(b.onClick, controller.OpenAbout),
            b => UnityEventTools.AddPersistentListener(b.onClick, controller.OpenHelp),
            b => UnityEventTools.AddPersistentListener(b.onClick, controller.ExitGame)
        };

        const float rowHeight = 56f;
        const float rowSpacing = 7f;
        for (int i = 0; i < labels.Length; i++)
        {
            float y = 140f - i * (rowHeight + rowSpacing);
            var row = CreateUiObject(labels[i] + " Row", content.transform);
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 0.5f);
            rowRect.anchorMax = new Vector2(1f, 0.5f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.anchoredPosition = new Vector2(0f, y);
            rowRect.sizeDelta = new Vector2(0f, rowHeight);

            var button = CreateMenuButton(row.transform, labels[i]);
            methods[i](button);

            if (i < labels.Length - 1)
            {
                var sep = CreateUiObject("Separator", content.transform);
                var sepRect = sep.GetComponent<RectTransform>();
                sepRect.anchorMin = new Vector2(0f, 0.5f);
                sepRect.anchorMax = new Vector2(1f, 0.5f);
                sepRect.pivot = new Vector2(0.5f, 0.5f);
                sepRect.anchoredPosition = new Vector2(0f, y - (rowHeight * 0.5f + 4f));
                sepRect.offsetMin = new Vector2(30f, sepRect.offsetMin.y);
                sepRect.offsetMax = new Vector2(-40f, sepRect.offsetMax.y);
                sepRect.sizeDelta = new Vector2(0f, 1f);

                var sepImage = sep.AddComponent<Image>();
                sepImage.color = new Color(0.85f, 0.8f, 0.95f, 0.14f);
                sepImage.raycastTarget = false;
            }
        }

        return canvasGroup;
    }

    private static Button CreateMenuButton(Transform parent, string label)
    {
        var buttonGo = CreateUiObject(label + " Button", parent);
        StretchFull(buttonGo.GetComponent<RectTransform>());

        var image = buttonGo.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;

        var button = buttonGo.AddComponent<Button>();
        button.interactable = true;
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;

        var colors = button.colors;
        colors.normalColor = new Color(0f, 0f, 0f, 0f);
        colors.highlightedColor = new Color(0.45f, 0.18f, 0.58f, 0.35f);
        colors.pressedColor = new Color(0.58f, 0.22f, 0.72f, 0.48f);
        colors.selectedColor = new Color(0.45f, 0.18f, 0.58f, 0.35f);
        colors.disabledColor = new Color(0f, 0f, 0f, 0.08f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        var text = CreateLabel(buttonGo.transform, label, 28, TextAnchor.MiddleLeft);
        text.color = new Color(0.9f, 0.87f, 0.96f, 0.98f);
        text.raycastTarget = false;
        var textRect = text.GetComponent<RectTransform>();
        textRect.offsetMin = new Vector2(30f, 0f);
        textRect.offsetMax = new Vector2(-12f, 0f);

        var hoverEffect = buttonGo.AddComponent<MainMenuButtonHoverEffect>();
        hoverEffect.highlightImage = image;
        hoverEffect.labelText = text;
        hoverEffect.normalHighlightColor = new Color(0f, 0f, 0f, 0f);
        hoverEffect.hoverHighlightColor = new Color(0.45f, 0.18f, 0.58f, 0.35f);
        hoverEffect.pressedHighlightColor = new Color(0.58f, 0.22f, 0.72f, 0.48f);
        hoverEffect.normalTextColor = new Color(0.9f, 0.87f, 0.96f, 0.98f);
        hoverEffect.hoverTextColor = new Color(1f, 0.95f, 1f, 1f);
        hoverEffect.hoverSfx = TryLoadAudioClip(UiHoverSfxPath);
        hoverEffect.clickSfx = TryLoadAudioClip(UiClickSfxPath);

        return button;
    }

    private static CanvasGroup CreateGameLogo(Transform canvas, out GameObject titleObject)
    {
        var logoGo = CreateUiObject("Game Title", canvas);
        titleObject = logoGo;
        var rect = logoGo.GetComponent<RectTransform>();
        var canvasGroup = logoGo.AddComponent<CanvasGroup>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(180f, 108f);
        rect.sizeDelta = new Vector2(1000f, 160f);

        var text = logoGo.AddComponent<Text>();
        text.text = "How I Fall";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 122;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.95f, 0.92f, 0.98f, 0.98f);
        text.raycastTarget = false;

        var shadow = logoGo.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.12f, 0.03f, 0.18f, 0.75f);
        shadow.effectDistance = new Vector2(3f, -3f);
        return canvasGroup;
    }

    private static void CreateMainMenuAnimator(
        Transform canvas,
        RectTransform backgroundTransform,
        CanvasGroup menuCanvasGroup,
        CanvasGroup titleCanvasGroup,
        Graphic overlayGraphic)
    {
        var animatorGo = new GameObject("MainMenuAnimator");
        animatorGo.transform.SetParent(canvas, false);

        var animator = animatorGo.AddComponent<MainMenuAnimator>();
        animator.backgroundTransform = backgroundTransform;
        animator.menuCanvasGroup = menuCanvasGroup;
        animator.titleCanvasGroup = titleCanvasGroup;
        animator.backgroundOverlay = overlayGraphic;
    }

    private static GameObject CreateFooter(Transform canvas)
    {
        var footer = CreateUiObject("Prototype Build", canvas);
        var rect = footer.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-24f, 16f);
        rect.sizeDelta = new Vector2(320f, 28f);

        var text = footer.AddComponent<Text>();
        text.text = "Prototype Build";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 17;
        text.alignment = TextAnchor.MiddleRight;
        text.color = new Color(0.66f, 0.63f, 0.74f, 0.50f);
        text.raycastTarget = false;
        return footer;
    }

    private static SettingsPanelController CreateSettingsPanel(Transform canvas)
    {
        var panelRoot = CreateUiObject("Settings Panel", canvas);
        panelRoot.SetActive(false);
        StretchFull(panelRoot.GetComponent<RectTransform>());

        var dimImage = panelRoot.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.82f);
        dimImage.raycastTarget = true;

        var window = CreateUiObject("Settings Window", panelRoot.transform);
        var windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(900f, 560f);
        windowRect.anchoredPosition = new Vector2(150f, 25f);
        var windowImage = window.AddComponent<Image>();
        windowImage.color = new Color(0.025f, 0.018f, 0.045f, 0.96f);
        windowImage.raycastTarget = false;

        var windowShadow = window.AddComponent<Shadow>();
        windowShadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        windowShadow.effectDistance = new Vector2(5f, -5f);

        var accent = CreateUiObject("Left Accent Line", window.transform);
        var accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = new Vector2(18f, 0f);
        accentRect.sizeDelta = new Vector2(3f, -48f);
        var accentImage = accent.AddComponent<Image>();
        accentImage.color = new Color(0.55f, 0.22f, 0.8f, 0.65f);
        accentImage.raycastTarget = false;

        var title = CreateLabel(window.transform, "Настройки", 39, TextAnchor.MiddleCenter);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -30f);
        titleRect.sizeDelta = new Vector2(0f, 58f);
        title.color = new Color(0.94f, 0.9f, 0.98f, 1f);
        title.raycastTarget = false;

        var sections = CreateUiObject("Sections", window.transform);
        var sectionsRect = sections.GetComponent<RectTransform>();
        sectionsRect.anchorMin = new Vector2(0f, 0f);
        sectionsRect.anchorMax = new Vector2(0f, 1f);
        sectionsRect.pivot = new Vector2(0f, 0.5f);
        sectionsRect.anchoredPosition = new Vector2(52f, -34f);
        sectionsRect.sizeDelta = new Vector2(176f, -158f);

        var sectionsLayout = sections.AddComponent<VerticalLayoutGroup>();
        sectionsLayout.spacing = 14f;
        sectionsLayout.childAlignment = TextAnchor.UpperLeft;
        sectionsLayout.childControlHeight = false;
        sectionsLayout.childControlWidth = true;
        sectionsLayout.childForceExpandHeight = false;
        sectionsLayout.childForceExpandWidth = true;

        CreateSectionLabel(sections.transform, "Видео", false);
        CreateSectionLabel(sections.transform, "Аудио", true);
        CreateSectionLabel(sections.transform, "Игра", false);

        var settingsArea = CreateUiObject("Settings Content", window.transform);
        var settingsRect = settingsArea.GetComponent<RectTransform>();
        settingsRect.anchorMin = new Vector2(0f, 0f);
        settingsRect.anchorMax = new Vector2(1f, 1f);
        settingsRect.offsetMin = new Vector2(250f, 96f);
        settingsRect.offsetMax = new Vector2(-54f, -96f);

        var settingsLayout = settingsArea.AddComponent<VerticalLayoutGroup>();
        settingsLayout.spacing = 10f;
        settingsLayout.childAlignment = TextAnchor.UpperLeft;
        settingsLayout.childControlHeight = false;
        settingsLayout.childControlWidth = true;
        settingsLayout.childForceExpandHeight = false;
        settingsLayout.childForceExpandWidth = true;

        Slider master = CreateLabeledSlider(settingsArea.transform, "Master Volume", 0f, 1f, 1f);
        Slider music = CreateLabeledSlider(settingsArea.transform, "Music Volume", 0f, 1f, 1f);
        Slider sfx = CreateLabeledSlider(settingsArea.transform, "SFX Volume", 0f, 1f, 1f);
        Slider textSpeed = CreateLabeledSlider(settingsArea.transform, "Text Speed", 0.25f, 3f, 1f);

        var fullscreenRow = CreateRow(settingsArea.transform, 54f);
        var fullscreenLabel = CreateLabel(fullscreenRow.transform, "Fullscreen", 22, TextAnchor.MiddleLeft);
        fullscreenLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(210f, 52f);
        fullscreenLabel.color = new Color(0.82f, 0.78f, 0.9f, 1f);
        fullscreenLabel.raycastTarget = false;
        var fullscreenToggle = CreateToggle(fullscreenRow.transform);

        var buttonsRow = CreateUiObject("Buttons", window.transform);
        var buttonsRect = buttonsRow.GetComponent<RectTransform>();
        buttonsRect.anchorMin = new Vector2(0f, 0f);
        buttonsRect.anchorMax = new Vector2(1f, 0f);
        buttonsRect.pivot = new Vector2(0.5f, 0f);
        buttonsRect.offsetMin = new Vector2(250f, 34f);
        buttonsRect.offsetMax = new Vector2(-54f, 84f);

        var buttonsLayout = buttonsRow.AddComponent<HorizontalLayoutGroup>();
        buttonsLayout.spacing = 18f;
        buttonsLayout.childAlignment = TextAnchor.MiddleRight;
        buttonsLayout.childControlHeight = false;
        buttonsLayout.childControlWidth = false;
        buttonsLayout.childForceExpandHeight = false;
        buttonsLayout.childForceExpandWidth = false;

        var resetButton = CreateStyledButton(buttonsRow.transform, "Reset", new Vector2(180f, 50f));
        var backButton = CreateStyledButton(buttonsRow.transform, "Back", new Vector2(180f, 50f));

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
        row.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, height);

        var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 16f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlHeight = false;
        rowLayout.childControlWidth = false;
        rowLayout.childForceExpandHeight = false;
        rowLayout.childForceExpandWidth = false;
        return row;
    }

    private static Text CreateSectionLabel(Transform parent, string label, bool active)
    {
        var text = CreateLabel(parent, label, 22, TextAnchor.MiddleLeft);
        text.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 34f);
        text.color = active
            ? new Color(0.86f, 0.58f, 1f, 1f)
            : new Color(0.54f, 0.5f, 0.64f, 0.95f);
        text.raycastTarget = false;
        return text;
    }

    private static Slider CreateLabeledSlider(Transform parent, string labelText, float min, float max, float value)
    {
        var row = CreateRow(parent, 54f);

        var labelGo = CreateUiObject("Label", row.transform);
        labelGo.GetComponent<RectTransform>().sizeDelta = new Vector2(210f, 52f);
        var label = labelGo.AddComponent<Text>();
        label.text = labelText;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 22;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = new Color(0.82f, 0.78f, 0.9f, 1f);
        label.raycastTarget = false;

        var slider = CreateSlider(row.transform, min, max, value);
        slider.GetComponent<RectTransform>().sizeDelta = new Vector2(320f, 30f);
        return slider;
    }

    private static Slider CreateSlider(Transform parent, float min, float max, float value)
    {
        var sliderGo = CreateUiObject("Slider", parent);
        var hitArea = sliderGo.AddComponent<Image>();
        hitArea.color = new Color(0f, 0f, 0f, 0f);
        hitArea.raycastTarget = true;

        var slider = sliderGo.AddComponent<Slider>();
        slider.interactable = true;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;

        var background = CreateUiObject("Background", sliderGo.transform);
        var bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0.5f);
        bgRect.anchorMax = new Vector2(1f, 0.5f);
        bgRect.pivot = new Vector2(0.5f, 0.5f);
        bgRect.anchoredPosition = Vector2.zero;
        bgRect.sizeDelta = new Vector2(0f, 6f);
        var bgImage = background.AddComponent<Image>();
        bgImage.color = new Color(0.12f, 0.1f, 0.18f, 1f);
        bgImage.raycastTarget = false;

        var fillArea = CreateUiObject("Fill Area", sliderGo.transform);
        var fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
        fillAreaRect.pivot = new Vector2(0.5f, 0.5f);
        fillAreaRect.offsetMin = new Vector2(9f, -3f);
        fillAreaRect.offsetMax = new Vector2(-9f, 3f);

        var fill = CreateUiObject("Fill", fillArea.transform);
        StretchFull(fill.GetComponent<RectTransform>());
        var fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.58f, 0.32f, 0.78f, 1f);
        fillImage.raycastTarget = false;

        var handleArea = CreateUiObject("Handle Slide Area", sliderGo.transform);
        StretchFull(handleArea.GetComponent<RectTransform>(), 9f, 9f, 0f, 0f);

        var handle = CreateUiObject("Handle", handleArea.transform);
        var handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 0f);
        handleRect.anchorMax = new Vector2(0.5f, 1f);
        handleRect.sizeDelta = new Vector2(18f, -6f);
        handleRect.anchoredPosition = Vector2.zero;
        var handleImage = handle.AddComponent<Image>();
        handleImage.color = new Color(0.92f, 0.88f, 0.98f, 1f);

        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        return slider;
    }

    private static Toggle CreateToggle(Transform parent)
    {
        var toggleGo = CreateUiObject("Fullscreen Toggle", parent);
        toggleGo.GetComponent<RectTransform>().sizeDelta = new Vector2(28f, 28f);

        var background = toggleGo.AddComponent<Image>();
        background.color = new Color(0.12f, 0.1f, 0.16f, 1f);

        var toggle = toggleGo.AddComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.interactable = true;

        var checkmarkGo = CreateUiObject("Checkmark", toggleGo.transform);
        StretchFull(checkmarkGo.GetComponent<RectTransform>(), 5f, 5f, 5f, 5f);
        var checkmark = checkmarkGo.AddComponent<Image>();
        checkmark.color = new Color(0.78f, 0.52f, 0.95f, 1f);
        toggle.graphic = checkmark;
        toggle.isOn = true;
        return toggle;
    }

    private static Button CreateStyledButton(Transform parent, string label, Vector2 size)
    {
        var buttonGo = CreateUiObject(label + " Button", parent);
        buttonGo.GetComponent<RectTransform>().sizeDelta = size;

        var image = buttonGo.AddComponent<Image>();
        image.color = Color.white;

        var button = buttonGo.AddComponent<Button>();
        button.targetGraphic = image;

        var colors = button.colors;
        colors.normalColor = new Color(0.08f, 0.06f, 0.12f, 0.95f);
        colors.highlightedColor = new Color(0.34f, 0.16f, 0.46f, 0.95f);
        colors.pressedColor = new Color(0.45f, 0.2f, 0.58f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.06f, 0.05f, 0.08f, 0.5f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        var text = CreateLabel(buttonGo.transform, label, 24, TextAnchor.MiddleCenter);
        text.color = new Color(0.9f, 0.87f, 0.96f, 0.98f);
        text.raycastTarget = false;
        return button;
    }

    private static Text CreateLabel(Transform parent, string textValue, int fontSize, TextAnchor anchor)
    {
        var textGo = CreateUiObject("Text", parent);
        StretchFull(textGo.GetComponent<RectTransform>());
        var text = textGo.AddComponent<Text>();
        text.text = textValue;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.color = Color.white;
        return text;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
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
