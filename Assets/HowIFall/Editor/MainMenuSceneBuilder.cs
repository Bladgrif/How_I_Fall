using System.Collections.Generic;
using System.IO;
using TMPro;
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
    private const string BackgroundPath = "Assets/HowIFall/Art/UI/MainMenu/main_menu_background.png";
    private const string KeyVisualPath = "Assets/HowIFall/Art/UI/MainMenu/main_menu_key_visual.png";
    private const string LeftGradientOverlayPath = "Assets/HowIFall/Art/UI/MainMenu/left_gradient_overlay.png";
    private const string LogoPath = "Assets/HowIFall/Art/UI/MainMenu/logo_how_i_fall.png";
    private const string MenuHoverBrushPath = "Assets/HowIFall/Art/UI/MainMenu/menu_hover_brush.png";
    private const string MenuPlayIndicatorPath = "Assets/HowIFall/Art/UI/MainMenu/menu_play_indicator.png";
    private const string SettingsBackgroundPath = "Assets/HowIFall/Art/UI/Settings/settings_background.png";
    private const string SettingsPanelBgPath = "Assets/HowIFall/Art/UI/Settings/settings_panel_bg.png";
    private const string SettingsTabActivePath = "Assets/HowIFall/Art/UI/Settings/settings_tab_active.png";
    private const string SettingsTabInactivePath = "Assets/HowIFall/Art/UI/Settings/settings_tab_inactive.png";
    private const string SettingsBackButtonPath = "Assets/HowIFall/Art/UI/Settings/settings_back_button.png";
    private const string MainMenuMusicMp3Path = "Assets/HowIFall/Audio/Music/main_menu_bgm.mp3";
    private const string MainMenuMusicOggPath = "Assets/HowIFall/Audio/Music/main_menu_bgm.ogg";
    private const float MenuRowWidth = 360f;
    private const float MenuHoverBrushWidth = 290f;
    private const float MenuHoverBrushOffsetX = -20f;

    [MenuItem("How I Fall/Build Main Menu Scene")]
    public static void BuildMainMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateMainCamera();
        var canvas = CreateCanvas();
        CreateEventSystem();
        var backgroundTransform = CreateBackgroundLayer(canvas.transform);
        CreateLeftGradientOverlay(canvas.transform);

        var sceneControllers = new GameObject("Scene Controllers");
        var mainMenuController = sceneControllers.AddComponent<MainMenuController>();
        var musicPlayer = sceneControllers.AddComponent<MainMenuMusicPlayer>();
        musicPlayer.musicClip = TryLoadMainMenuMusicClip();

        var managers = new GameObject("Managers");
        managers.AddComponent<GameState>();
        managers.AddComponent<SaveManager>();
        managers.AddComponent<SettingsManager>();
        managers.AddComponent<AudioManager>();
        managers.AddComponent<SceneFlowManager>();

        var menuCanvasGroup = CreateMainMenuRoot(canvas.transform, mainMenuController);
        var titleCanvasGroup = CreateGameLogo(canvas.transform, out var titleObject);
        var pressAnyObject = CreatePressAnyButton(canvas.transform);

        var settingsPanelController = CreateSettingsPanel(canvas.transform);
        settingsPanelController.objectsToHideWhenOpen = new[] { titleObject, pressAnyObject };
        mainMenuController.settingsPanel = settingsPanelController;

        var aboutPanel = CreateInfoOverlayPanel(
            canvas.transform,
            mainMenuController,
            "About Panel",
            "Об игре",
            "How I Fall — подростковая визуальная новелла о неловких чувствах, школьных тайнах и выборе, после которого уже нельзя притворяться прежним.\n\nЖанр: школьная драма, романтика, лёгкая мистика, детектив.\nВерсия: In Development",
            new Vector2(820f, 520f),
            true);
        var helpPanel = CreateInfoOverlayPanel(
            canvas.transform,
            mainMenuController,
            "Help Panel",
            "Помощь",
            "Управление:\n\nЛКМ / Space — следующая реплика\nEsc — закрыть окно или вернуться назад\nH — история реплик\nCtrl — пропуск текста\nF — полноэкранный режим\n\nВ главном меню:\n\nНачать — новая игра\nЗагрузить — экран сохранений\nНастройки — параметры игры",
            new Vector2(820f, 560f),
            false);
        var loadPanel = CreateLoadPanel(
            canvas.transform,
            mainMenuController,
            out var loadSaveTitleText,
            out var loadSaveMetaText,
            out var loadSavePreviewText,
            out var loadSaveButton);
        var exitConfirmPanel = CreateExitConfirmPanel(canvas.transform, mainMenuController);
        var notificationPanel = CreateNotificationPanel(canvas.transform, out var notificationText);
        AssignMainMenuControllerReferences(
            mainMenuController,
            aboutPanel,
            helpPanel,
            exitConfirmPanel,
            loadPanel,
            loadSaveTitleText,
            loadSaveMetaText,
            loadSavePreviewText,
            loadSaveButton,
            notificationPanel,
            notificationText);

        CreateMainMenuAnimator(canvas.transform, backgroundTransform, menuCanvasGroup, titleCanvasGroup, null);

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

        cameraGo.AddComponent<AudioListener>();
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
        var keySprite = TryLoadMainMenuBackgroundSprite();
        if (keySprite != null)
        {
            image.sprite = keySprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
        }
        else
        {
            Debug.LogWarning("Main menu background not found. Checked paths: " + BackgroundPath + ", " + KeyVisualPath);
            image.color = new Color(0.78f, 0.88f, 0.96f, 1f);
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

    private static Sprite TryLoadMainMenuBackgroundSprite()
    {
        var background = TryLoadSprite(BackgroundPath);
        if (background != null)
        {
            return background;
        }

        return TryLoadSprite(KeyVisualPath);
    }

    private static Sprite TryLoadSprite(string path, bool validateAspect = false)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (validateAspect)
        {
            ValidateKeyVisualAspect(texture);
        }

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
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
        return TryLoadAudioClip(MainMenuMusicMp3Path, MainMenuMusicOggPath);
    }

    private static AudioClip TryLoadAudioClip(params string[] paths)
    {
        foreach (string path in paths)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null)
            {
                Debug.Log($"Loaded audio clip: {path}");
                return clip;
            }
        }

        Debug.LogWarning($"Audio clip was not found. Checked paths: {string.Join(", ", paths)}");
        return null;
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

    private static void CreateLeftGradientOverlay(Transform canvas)
    {
        var overlay = CreateUiObject("Left Gradient Overlay", canvas);
        var rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(760f, 0f);

        var image = overlay.AddComponent<Image>();
        image.sprite = EnsureLeftGradientSprite();
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = Color.white;
        image.raycastTarget = false;
    }

    private static Sprite EnsureLeftGradientSprite()
    {
        const int width = 1024;
        const int height = 16;
        const float leftAlpha = 0.56f;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var color = new Color(0.015f, 0.025f, 0.055f, 1f);

        for (int x = 0; x < width; x++)
        {
            float t = x / (float)(width - 1);
            float alpha = Mathf.Lerp(leftAlpha, 0f, Mathf.SmoothStep(0f, 1f, t));
            var pixel = new Color(color.r, color.g, color.b, alpha);

            for (int y = 0; y < height; y++)
            {
                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply();

        string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), LeftGradientOverlayPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
        File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(LeftGradientOverlayPath);

        var importer = AssetImporter.GetAtPath(LeftGradientOverlayPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(LeftGradientOverlayPath);
    }

    private static CanvasGroup CreateMainMenuRoot(Transform canvas, MainMenuController controller)
    {
        var root = CreateUiObject("MainMenuRoot", canvas);
        var canvasGroup = root.AddComponent<CanvasGroup>();
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 0.5f);
        rootRect.anchorMax = new Vector2(0f, 0.5f);
        rootRect.pivot = new Vector2(0f, 0.5f);
        rootRect.anchoredPosition = new Vector2(140f, -96f);
        rootRect.sizeDelta = new Vector2(380f, 410f);

        var panel = root.AddComponent<Image>();
        panel.color = new Color(1f, 1f, 1f, 0f);
        panel.raycastTarget = false;

        var panelShadow = root.AddComponent<Shadow>();
        panelShadow.effectColor = new Color(0f, 0f, 0f, 0f);
        panelShadow.effectDistance = new Vector2(3f, -3f);

        var accent = CreateUiObject("Accent Line", root.transform);
        var accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = new Vector2(12f, 0f);
        accentRect.sizeDelta = new Vector2(2f, -24f);
        var accentImage = accent.AddComponent<Image>();
        accentImage.color = new Color(1f, 1f, 1f, 0f);
        accentImage.raycastTarget = false;

        var content = CreateUiObject("Menu Content", root.transform);
        StretchFull(content.GetComponent<RectTransform>());

        string[] labels = { "Начать", "Загрузить", "Настройки", "Об игре", "Помощь", "Выход" };
        var methods = new System.Action<Button>[]
        {
            b => UnityEventTools.AddPersistentListener(b.onClick, controller.StartGame),
            b => UnityEventTools.AddPersistentListener(b.onClick, controller.OpenLoadPanel),
            b => UnityEventTools.AddPersistentListener(b.onClick, controller.OpenSettings),
            b => UnityEventTools.AddPersistentListener(b.onClick, controller.OpenAbout),
            b => UnityEventTools.AddPersistentListener(b.onClick, controller.OpenHelp),
            b => UnityEventTools.AddPersistentListener(b.onClick, controller.OpenExitConfirm)
        };

        const float rowHeight = 62f;
        const float rowSpacing = 0f;
        for (int i = 0; i < labels.Length; i++)
        {
            float y = 136f - i * (rowHeight + rowSpacing);
            var row = CreateUiObject(labels[i] + " Row", content.transform);
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 0.5f);
            rowRect.anchorMax = new Vector2(0f, 0.5f);
            rowRect.pivot = new Vector2(0f, 0.5f);
            rowRect.anchoredPosition = new Vector2(0f, y);
            rowRect.sizeDelta = new Vector2(MenuRowWidth, rowHeight);

            var button = CreateMenuButton(row.transform, labels[i]);
            methods[i](button);

            if (i < labels.Length - 1)
            {
                var sep = CreateUiObject("Separator", content.transform);
                var sepRect = sep.GetComponent<RectTransform>();
                sepRect.anchorMin = new Vector2(0f, 0.5f);
                sepRect.anchorMax = new Vector2(0f, 0.5f);
                sepRect.pivot = new Vector2(0f, 0.5f);
                sepRect.anchoredPosition = new Vector2(MenuHoverBrushOffsetX, y - (rowHeight * 0.5f + 4f));
                sepRect.sizeDelta = new Vector2(MenuHoverBrushWidth, 1f);

                var sepImage = sep.AddComponent<Image>();
                sepImage.color = new Color(0.85f, 0.8f, 0.95f, 0.20f);
                sepImage.raycastTarget = false;
            }
        }

        return canvasGroup;
    }

    private static Button CreateMenuButton(Transform parent, string label)
    {
        var buttonGo = CreateUiObject(label + " Button", parent);
        var buttonRect = buttonGo.GetComponent<RectTransform>();
        StretchFull(buttonRect);
        buttonRect.offsetMin = new Vector2(MenuHoverBrushOffsetX, -4f);
        buttonRect.offsetMax = new Vector2(MenuHoverBrushOffsetX + MenuHoverBrushWidth - MenuRowWidth, 4f);

        var image = buttonGo.AddComponent<Image>();
        image.sprite = TryLoadSprite(MenuHoverBrushPath);
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;

        var button = buttonGo.AddComponent<Button>();
        button.interactable = true;
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;

        var colors = button.colors;
        colors.normalColor = new Color(0f, 0f, 0f, 0f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
        colors.pressedColor = new Color(0.96f, 0.9f, 0.88f, 0.95f);
        colors.selectedColor = new Color(1f, 1f, 1f, 0.92f);
        colors.disabledColor = new Color(0f, 0f, 0f, 0.08f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        var text = CreateLabel(buttonGo.transform, label, 27, TextAnchor.MiddleLeft);
        text.color = new Color(0.92f, 0.94f, 0.98f, 0.95f);
        text.raycastTarget = false;
        var textShadow = text.gameObject.AddComponent<Shadow>();
        textShadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
        textShadow.effectDistance = new Vector2(2f, -2f);
        var textRect = text.GetComponent<RectTransform>();
        textRect.offsetMin = new Vector2(26f, 0f);
        textRect.offsetMax = new Vector2(-24f, 0f);

        var playIndicator = CreateMenuPlayIndicator(buttonGo.transform);

        var hoverEffect = buttonGo.AddComponent<MainMenuButtonHoverEffect>();
        hoverEffect.highlightImage = image;
        hoverEffect.labelText = text;
        hoverEffect.playIndicator = playIndicator;
        hoverEffect.normalHighlightColor = new Color(0f, 0f, 0f, 0f);
        hoverEffect.hoverHighlightColor = new Color(1f, 1f, 1f, 0.92f);
        hoverEffect.pressedHighlightColor = new Color(0.96f, 0.9f, 0.88f, 0.95f);
        hoverEffect.normalTextColor = new Color(0.92f, 0.94f, 0.98f, 0.95f);
        hoverEffect.hoverTextColor = new Color(0.07f, 0.08f, 0.11f, 1f);

        return button;
    }

    private static GameObject CreateMenuPlayIndicator(Transform parent)
    {
        var indicatorSprite = TryLoadSprite(MenuPlayIndicatorPath);
        if (indicatorSprite == null)
        {
            return null;
        }

        var indicatorGo = CreateUiObject("Play Indicator", parent);
        var rect = indicatorGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-48f, 0f);
        rect.sizeDelta = new Vector2(46f, 46f);

        var image = indicatorGo.AddComponent<Image>();
        image.sprite = indicatorSprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = new Color(0.85f, 0.08f, 0.06f, 0.82f);
        image.raycastTarget = false;

        indicatorGo.SetActive(false);
        return indicatorGo;
    }

    private static CanvasGroup CreateGameLogo(Transform canvas, out GameObject titleObject)
    {
        var logoSprite = TryLoadSprite(LogoPath);
        if (logoSprite == null)
        {
            titleObject = null;
            return null;
        }

        var logoGo = CreateUiObject("Game Logo", canvas);
        titleObject = logoGo;

        var rect = logoGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(125f, -116f);
        rect.sizeDelta = new Vector2(620f, 320f);
        rect.localRotation = Quaternion.Euler(0f, 0f, -5f);

        var canvasGroup = logoGo.AddComponent<CanvasGroup>();
        var image = logoGo.AddComponent<Image>();
        image.sprite = logoSprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = false;
        return canvasGroup;
    }

    private static TextMeshProUGUI CreateTmpLogoText(string name, Transform parent, string value, int fontSize, Color color, float rotation)
    {
        var go = CreateUiObject(name, parent);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold | FontStyles.Italic;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = color;
        text.raycastTarget = false;

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(4f, -4f);
        return text;
    }

    private static void CreateLogoUnderline(Transform parent)
    {
        var underline = CreateUiObject("Fall Underline", parent);
        var rect = underline.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -214f);
        rect.sizeDelta = new Vector2(250f, 8f);
        rect.localRotation = Quaternion.Euler(0f, 0f, -9f);

        var image = underline.AddComponent<Image>();
        image.color = new Color(0.9f, 0.06f, 0.04f, 0.78f);
        image.raycastTarget = false;
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

    private static GameObject CreatePressAnyButton(Transform canvas)
    {
        var prompt = CreateUiObject("Press Any Button", canvas);
        var rect = prompt.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(140f, 65f);
        rect.sizeDelta = new Vector2(280f, 32f);

        var text = prompt.AddComponent<Text>();
        text.text = "Press any button";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 19;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = new Color(1f, 1f, 1f, 0.75f);
        text.raycastTarget = false;
        return prompt;
    }

    private static SettingsPanelController CreateSettingsPanel(Transform canvas)
    {
        var panelRoot = CreateUiObject("Settings Panel", canvas);
        panelRoot.SetActive(false);
        StretchFull(panelRoot.GetComponent<RectTransform>());

        var background = CreateUiObject("Settings Background", panelRoot.transform);
        var backgroundRect = background.GetComponent<RectTransform>();
        StretchFull(backgroundRect);
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        var backgroundImage = background.AddComponent<Image>();
        var backgroundSprite = TryLoadSprite(SettingsBackgroundPath);
        if (backgroundSprite != null)
        {
            backgroundImage.sprite = backgroundSprite;
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.preserveAspect = false;
            backgroundImage.color = Color.white;
        }
        else
        {
            backgroundImage.sprite = TryLoadMainMenuBackgroundSprite();
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.preserveAspect = false;
            backgroundImage.color = new Color(0.72f, 0.78f, 0.88f, 1f);
        }

        backgroundImage.raycastTarget = false;

        var dim = CreateUiObject("Settings Dim Blocker", panelRoot.transform);
        StretchFull(dim.GetComponent<RectTransform>());
        var dimImage = dim.AddComponent<Image>();
        dimImage.color = new Color(0.02f, 0.04f, 0.08f, 0.22f);
        dimImage.raycastTarget = true;

        CreateSettingsLogo(panelRoot.transform);

        var window = CreateUiObject("Settings Window", panelRoot.transform);
        var windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(1450f, 860f);
        windowRect.anchoredPosition = new Vector2(130f, 10f);
        var windowImage = window.AddComponent<Image>();
        windowImage.sprite = TryLoadSprite(SettingsPanelBgPath);
        windowImage.type = Image.Type.Simple;
        windowImage.preserveAspect = false;
        windowImage.color = windowImage.sprite != null ? Color.white : new Color(0.025f, 0.07f, 0.13f, 0.92f);
        windowImage.raycastTarget = true;

        var windowShadow = window.AddComponent<Shadow>();
        windowShadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        windowShadow.effectDistance = new Vector2(5f, -5f);

        if (windowImage.sprite == null)
        {
            var outline = window.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.35f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        var title = CreateTmpLabel(window.transform, "Настройки", 60, TextAlignmentOptions.Center);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 315f);
        titleRect.sizeDelta = new Vector2(640f, 68f);
        title.color = Color.white;
        title.raycastTarget = false;

        var underline = CreateUiObject("Settings Red Underline", window.transform);
        var underlineRect = underline.GetComponent<RectTransform>();
        underlineRect.anchorMin = new Vector2(0.5f, 0.5f);
        underlineRect.anchorMax = new Vector2(0.5f, 0.5f);
        underlineRect.pivot = new Vector2(0.5f, 0.5f);
        underlineRect.anchoredPosition = new Vector2(0f, 280f);
        underlineRect.sizeDelta = new Vector2(190f, 5f);
        underlineRect.localRotation = Quaternion.Euler(0f, 0f, -4f);
        var underlineImage = underline.AddComponent<Image>();
        underlineImage.color = new Color(0.9f, 0.08f, 0.06f, 1f);
        underlineImage.raycastTarget = false;

        Button videoTab = CreateSettingsTab(window.transform, "Видео", false, new Vector2(-480f, 145f), out var videoTabImage, out var videoTabText);
        Button audioTab = CreateSettingsTab(window.transform, "Аудио", true, new Vector2(-480f, 77f), out var audioTabImage, out var audioTabText);
        Button gameTab = CreateSettingsTab(window.transform, "Игра", false, new Vector2(-480f, 9f), out var gameTabImage, out var gameTabText);

        var divider = CreateUiObject("Settings Divider", window.transform);
        var dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0.5f, 0.5f);
        dividerRect.anchorMax = new Vector2(0.5f, 0.5f);
        dividerRect.pivot = new Vector2(0.5f, 0.5f);
        dividerRect.anchoredPosition = new Vector2(-300f, -20f);
        dividerRect.sizeDelta = new Vector2(2f, 500f);
        var dividerImage = divider.AddComponent<Image>();
        dividerImage.color = new Color(1f, 1f, 1f, 0.22f);
        dividerImage.raycastTarget = false;

        var videoContent = CreateUiObject("Video Settings Content", window.transform);
        StretchFull(videoContent.GetComponent<RectTransform>());
        videoContent.SetActive(false);

        Button screenModeSelector = CreateSettingsSelectorRow(videoContent.transform, "Режим экрана", "Полный экран", 155f, out var screenModeValueText);
        Button resolutionSelector = CreateSettingsSelectorRow(videoContent.transform, "Разрешение", "1920x1080", 100f, out var resolutionValueText);
        Button refreshRateSelector = CreateSettingsSelectorRow(videoContent.transform, "Частота обновления", "60", 45f, out var refreshRateValueText);
        Button gameLookSelector = CreateSettingsSelectorRow(videoContent.transform, "Внешний вид игры", "Чистый", -10f, out var gameLookValueText);
        Button interfaceStyleSelector = CreateSettingsSelectorRow(videoContent.transform, "Стиль интерфейса", "Классический", -65f, out var interfaceStyleValueText);
        Toggle rewindVhsFilterToggle = CreateSettingsToggleRow(videoContent.transform, "VHS фильтр при перемотке", true, -140f);
        Toggle runInBackgroundToggle = CreateSettingsToggleRow(videoContent.transform, "Работать в фоновом режиме", false, -190f);
        Toggle characterAnimationsToggle = CreateSettingsToggleRow(videoContent.transform, "Анимация персонажей", true, -240f);
        Toggle backgroundAnimationsToggle = CreateSettingsToggleRow(videoContent.transform, "Анимация фонов", true, -290f);

        var audioContent = CreateUiObject("Audio Settings Content", window.transform);
        StretchFull(audioContent.GetComponent<RectTransform>());

        var audioTitle = CreateTmpLabel(audioContent.transform, "Громкость", 32, TextAlignmentOptions.Left);
        var audioTitleRect = audioTitle.GetComponent<RectTransform>();
        audioTitleRect.anchorMin = new Vector2(0.5f, 0.5f);
        audioTitleRect.anchorMax = new Vector2(0.5f, 0.5f);
        audioTitleRect.pivot = new Vector2(0f, 0.5f);
        audioTitleRect.anchoredPosition = new Vector2(-210f, 160f);
        audioTitleRect.sizeDelta = new Vector2(410f, 46f);
        audioTitle.color = Color.white;

        Slider master = CreateSettingsSliderRow(audioContent.transform, "Общая", 0f, 1f, 0.8f, 95f);
        Slider music = CreateSettingsSliderRow(audioContent.transform, "Музыка", 0f, 1f, 0.8f, 35f);
        Slider sfx = CreateSettingsSliderRow(audioContent.transform, "Звуки", 0f, 1f, 0.8f, -25f);
        Slider ambient = CreateSettingsSliderRow(audioContent.transform, "Окружение", 0f, 1f, 0.8f, -85f);
        Toggle musicDuringPauseToggle = CreateSettingsToggleRow(audioContent.transform, "Музыка во время паузы", false, -155f);

        var gameContent = CreateUiObject("Game Settings Content", window.transform);
        StretchFull(gameContent.GetComponent<RectTransform>());
        gameContent.SetActive(false);

        Button languageSelector = CreateSettingsSelectorRow(gameContent.transform, "Язык", "Русский", 155f, out var languageValueText);
        Button fontSizeModeSelector = CreateSettingsSelectorRow(gameContent.transform, "Шрифт", "Мелкий", 100f, out var fontSizeModeValueText);
        Button skipModeSelector = CreateSettingsSelectorRow(gameContent.transform, "Пропускать", "Виденное", 45f, out var skipModeValueText);
        Button skipBehaviorSelector = CreateSettingsSelectorRow(gameContent.transform, "Режим пропуска", "Классический", -10f, out var skipBehaviorValueText);
        Slider textSpeedSlider = CreateSettingsValueSliderRow(gameContent.transform, "Скорость текста", 20f, 100f, 50f, -85f, "50 симв./сек.", out var textSpeedValueText);
        Slider autoForwardDelaySlider = CreateSettingsValueSliderRow(gameContent.transform, "Задержка автоперехода", 50f, 500f, 250f, -145f, "250 %", out var autoForwardDelayValueText);
        Toggle skipAfterChoicesToggle = CreateSettingsToggleRow(gameContent.transform, "Пропуск после выборов", false, -215f);
        Toggle autoForwardToggle = CreateSettingsToggleRowAt(gameContent.transform, "Автопереход", false, -210f, 50f, -265f, 220f);
        Toggle autoSaveToggle = CreateSettingsToggleRowAt(gameContent.transform, "Автосохранение", true, 180f, 520f, -265f, 230f);
        Toggle showHintsToggle = CreateSettingsToggleRow(gameContent.transform, "Показывать подсказки", true, -315f);

        var backButton = CreateBackButton(panelRoot.transform);

        var settingsController = panelRoot.AddComponent<SettingsPanelController>();
        settingsController.root = panelRoot;
        settingsController.settingsTitleText = title;
        settingsController.videoContent = videoContent;
        settingsController.audioContent = audioContent;
        settingsController.gameContent = gameContent;
        settingsController.videoTabImage = videoTabImage;
        settingsController.audioTabImage = audioTabImage;
        settingsController.gameTabImage = gameTabImage;
        settingsController.videoTabText = videoTabText;
        settingsController.audioTabText = audioTabText;
        settingsController.gameTabText = gameTabText;
        settingsController.activeTabSprite = TryLoadSprite(SettingsTabActivePath);
        settingsController.inactiveTabSprite = TryLoadSprite(SettingsTabInactivePath);
        settingsController.masterVolumeSlider = master;
        settingsController.musicVolumeSlider = music;
        settingsController.sfxVolumeSlider = sfx;
        settingsController.ambientVolumeSlider = ambient;
        settingsController.musicDuringPauseToggle = musicDuringPauseToggle;
        settingsController.screenModeValueText = screenModeValueText;
        settingsController.resolutionValueText = resolutionValueText;
        settingsController.refreshRateValueText = refreshRateValueText;
        settingsController.gameLookValueText = gameLookValueText;
        settingsController.interfaceStyleValueText = interfaceStyleValueText;
        settingsController.rewindVhsFilterToggle = rewindVhsFilterToggle;
        settingsController.runInBackgroundToggle = runInBackgroundToggle;
        settingsController.characterAnimationsToggle = characterAnimationsToggle;
        settingsController.backgroundAnimationsToggle = backgroundAnimationsToggle;
        settingsController.languageValueText = languageValueText;
        settingsController.fontSizeModeValueText = fontSizeModeValueText;
        settingsController.skipModeValueText = skipModeValueText;
        settingsController.skipBehaviorValueText = skipBehaviorValueText;
        settingsController.textSpeedSlider = textSpeedSlider;
        settingsController.textSpeedValueText = textSpeedValueText;
        settingsController.autoForwardDelaySlider = autoForwardDelaySlider;
        settingsController.autoForwardDelayValueText = autoForwardDelayValueText;
        settingsController.skipAfterChoicesToggle = skipAfterChoicesToggle;
        settingsController.autoForwardToggle = autoForwardToggle;
        settingsController.autoSaveToggle = autoSaveToggle;
        settingsController.showHintsToggle = showHintsToggle;

        UnityEventTools.AddPersistentListener(videoTab.onClick, settingsController.ShowVideoTab);
        UnityEventTools.AddPersistentListener(audioTab.onClick, settingsController.ShowAudioTab);
        UnityEventTools.AddPersistentListener(gameTab.onClick, settingsController.ShowGameTab);
        UnityEventTools.AddPersistentListener(screenModeSelector.onClick, settingsController.CycleScreenMode);
        UnityEventTools.AddPersistentListener(resolutionSelector.onClick, settingsController.CycleResolution);
        UnityEventTools.AddPersistentListener(refreshRateSelector.onClick, settingsController.CycleRefreshRate);
        UnityEventTools.AddPersistentListener(gameLookSelector.onClick, settingsController.CycleGameLook);
        UnityEventTools.AddPersistentListener(interfaceStyleSelector.onClick, settingsController.CycleInterfaceStyle);
        UnityEventTools.AddPersistentListener(languageSelector.onClick, settingsController.CycleLanguage);
        UnityEventTools.AddPersistentListener(fontSizeModeSelector.onClick, settingsController.CycleFontSizeMode);
        UnityEventTools.AddPersistentListener(skipModeSelector.onClick, settingsController.CycleSkipMode);
        UnityEventTools.AddPersistentListener(skipBehaviorSelector.onClick, settingsController.CycleSkipBehavior);
        UnityEventTools.AddPersistentListener(master.onValueChanged, settingsController.OnMasterVolumeChanged);
        UnityEventTools.AddPersistentListener(music.onValueChanged, settingsController.OnMusicVolumeChanged);
        UnityEventTools.AddPersistentListener(sfx.onValueChanged, settingsController.OnSfxVolumeChanged);
        UnityEventTools.AddPersistentListener(ambient.onValueChanged, settingsController.OnAmbientVolumeChanged);
        UnityEventTools.AddPersistentListener(musicDuringPauseToggle.onValueChanged, settingsController.OnMusicDuringPauseChanged);
        UnityEventTools.AddPersistentListener(rewindVhsFilterToggle.onValueChanged, settingsController.OnRewindVhsFilterChanged);
        UnityEventTools.AddPersistentListener(runInBackgroundToggle.onValueChanged, settingsController.OnRunInBackgroundChanged);
        UnityEventTools.AddPersistentListener(characterAnimationsToggle.onValueChanged, settingsController.OnCharacterAnimationsChanged);
        UnityEventTools.AddPersistentListener(backgroundAnimationsToggle.onValueChanged, settingsController.OnBackgroundAnimationsChanged);
        UnityEventTools.AddPersistentListener(textSpeedSlider.onValueChanged, settingsController.OnTextSpeedChanged);
        UnityEventTools.AddPersistentListener(autoForwardDelaySlider.onValueChanged, settingsController.OnAutoForwardDelayChanged);
        UnityEventTools.AddPersistentListener(skipAfterChoicesToggle.onValueChanged, settingsController.OnSkipAfterChoicesChanged);
        UnityEventTools.AddPersistentListener(autoForwardToggle.onValueChanged, settingsController.OnAutoForwardChanged);
        UnityEventTools.AddPersistentListener(autoSaveToggle.onValueChanged, settingsController.OnAutoSaveChanged);
        UnityEventTools.AddPersistentListener(showHintsToggle.onValueChanged, settingsController.OnShowHintsChanged);
        UnityEventTools.AddPersistentListener(backButton.onClick, settingsController.Hide);

        return settingsController;
    }

    private static GameObject CreateInfoOverlayPanel(
        Transform canvas,
        MainMenuController controller,
        string panelName,
        string titleText,
        string bodyText,
        Vector2 windowSize,
        bool isAboutPanel)
    {
        var panelRoot = CreateUiObject(panelName, canvas);
        panelRoot.SetActive(false);
        StretchFull(panelRoot.GetComponent<RectTransform>());

        var dim = CreateUiObject(panelName + " Dim Blocker", panelRoot.transform);
        StretchFull(dim.GetComponent<RectTransform>());
        var dimImage = dim.AddComponent<Image>();
        dimImage.color = new Color(0.02f, 0.04f, 0.08f, 0.45f);
        dimImage.raycastTarget = true;

        var window = CreateUiObject(panelName + " Window", panelRoot.transform);
        var windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.anchoredPosition = Vector2.zero;
        windowRect.sizeDelta = windowSize;

        var windowImage = window.AddComponent<Image>();
        windowImage.sprite = TryLoadSprite(SettingsPanelBgPath);
        windowImage.type = Image.Type.Simple;
        windowImage.preserveAspect = false;
        windowImage.color = windowImage.sprite != null ? Color.white : new Color(0.025f, 0.07f, 0.13f, 0.94f);
        windowImage.raycastTarget = true;

        var windowShadow = window.AddComponent<Shadow>();
        windowShadow.effectColor = new Color(0f, 0f, 0f, 0.58f);
        windowShadow.effectDistance = new Vector2(5f, -5f);

        var outline = window.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.35f);
        outline.effectDistance = new Vector2(2f, -2f);

        var title = CreateTmpLabel(window.transform, titleText, 52, TextAlignmentOptions.Center);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -44f);
        titleRect.sizeDelta = new Vector2(520f, 64f);
        title.color = Color.white;
        title.raycastTarget = false;

        var underline = CreateUiObject(titleText + " Red Underline", window.transform);
        var underlineRect = underline.GetComponent<RectTransform>();
        underlineRect.anchorMin = new Vector2(0.5f, 1f);
        underlineRect.anchorMax = new Vector2(0.5f, 1f);
        underlineRect.pivot = new Vector2(0.5f, 0.5f);
        underlineRect.anchoredPosition = new Vector2(0f, -116f);
        underlineRect.sizeDelta = new Vector2(170f, 5f);
        underlineRect.localRotation = Quaternion.Euler(0f, 0f, -4f);
        var underlineImage = underline.AddComponent<Image>();
        underlineImage.color = new Color(0.9f, 0.08f, 0.06f, 1f);
        underlineImage.raycastTarget = false;

        var body = CreateTmpLabel(window.transform, bodyText, isAboutPanel ? 28 : 26, TextAlignmentOptions.Left);
        var bodyRect = body.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0.5f, 0.5f);
        bodyRect.anchorMax = new Vector2(0.5f, 0.5f);
        bodyRect.pivot = new Vector2(0.5f, 0.5f);
        bodyRect.anchoredPosition = isAboutPanel ? new Vector2(0f, -20f) : new Vector2(0f, -18f);
        bodyRect.sizeDelta = isAboutPanel ? new Vector2(660f, 250f) : new Vector2(660f, 320f);
        body.fontStyle = FontStyles.Normal;
        body.lineSpacing = 10f;
        body.color = new Color(1f, 1f, 1f, 0.9f);
        body.raycastTarget = false;

        var closeButton = CreateStyledButton(window.transform, "Назад", new Vector2(190f, 58f));
        var closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 0f);
        closeRect.anchorMax = new Vector2(1f, 0f);
        closeRect.pivot = new Vector2(1f, 0f);
        closeRect.anchoredPosition = new Vector2(-54f, 42f);
        AddStyledButtonHoverEffect(closeButton);

        if (isAboutPanel)
        {
            UnityEventTools.AddPersistentListener(closeButton.onClick, controller.CloseAbout);
        }
        else
        {
            UnityEventTools.AddPersistentListener(closeButton.onClick, controller.CloseHelp);
        }

        return panelRoot;
    }

    private static GameObject CreateLoadPanel(
        Transform canvas,
        MainMenuController controller,
        out TextMeshProUGUI saveTitleText,
        out TextMeshProUGUI saveMetaText,
        out TextMeshProUGUI savePreviewText,
        out Button saveButton)
    {
        var panelRoot = CreateUiObject("Load Panel", canvas);
        panelRoot.SetActive(false);
        StretchFull(panelRoot.GetComponent<RectTransform>());

        var dim = CreateUiObject("Load Dim Blocker", panelRoot.transform);
        StretchFull(dim.GetComponent<RectTransform>());
        var dimImage = dim.AddComponent<Image>();
        dimImage.color = new Color(0.02f, 0.04f, 0.08f, 0.45f);
        dimImage.raycastTarget = true;

        var window = CreateUiObject("Load Window", panelRoot.transform);
        var windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.anchoredPosition = Vector2.zero;
        windowRect.sizeDelta = new Vector2(820f, 520f);

        var windowImage = window.AddComponent<Image>();
        windowImage.sprite = TryLoadSprite(SettingsPanelBgPath);
        windowImage.type = Image.Type.Simple;
        windowImage.preserveAspect = false;
        windowImage.color = windowImage.sprite != null ? Color.white : new Color(0.025f, 0.07f, 0.13f, 0.94f);
        windowImage.raycastTarget = true;

        var windowShadow = window.AddComponent<Shadow>();
        windowShadow.effectColor = new Color(0f, 0f, 0f, 0.58f);
        windowShadow.effectDistance = new Vector2(5f, -5f);

        var outline = window.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.35f);
        outline.effectDistance = new Vector2(2f, -2f);

        var title = CreateTmpLabel(window.transform, "Загрузить", 52, TextAlignmentOptions.Center);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -44f);
        titleRect.sizeDelta = new Vector2(520f, 64f);
        title.color = Color.white;
        title.raycastTarget = false;

        var underline = CreateUiObject("Load Red Underline", window.transform);
        var underlineRect = underline.GetComponent<RectTransform>();
        underlineRect.anchorMin = new Vector2(0.5f, 1f);
        underlineRect.anchorMax = new Vector2(0.5f, 1f);
        underlineRect.pivot = new Vector2(0.5f, 0.5f);
        underlineRect.anchoredPosition = new Vector2(0f, -116f);
        underlineRect.sizeDelta = new Vector2(170f, 5f);
        underlineRect.localRotation = Quaternion.Euler(0f, 0f, -4f);
        var underlineImage = underline.AddComponent<Image>();
        underlineImage.color = new Color(0.9f, 0.08f, 0.06f, 1f);
        underlineImage.raycastTarget = false;

        saveButton = CreateSaveCardButton(window.transform);
        var cardRect = saveButton.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = new Vector2(0f, -34f);
        cardRect.sizeDelta = new Vector2(640f, 180f);

        var accent = CreateUiObject("Save Card Red Accent", saveButton.transform);
        var accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 0.5f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(6f, 0f);
        var accentImage = accent.AddComponent<Image>();
        accentImage.color = new Color(0.9f, 0.08f, 0.06f, 1f);
        accentImage.raycastTarget = false;

        saveTitleText = CreateTmpLabel(saveButton.transform, "Сохранение не найдено", 28, TextAlignmentOptions.Left);
        var saveTitleRect = saveTitleText.GetComponent<RectTransform>();
        saveTitleRect.anchorMin = new Vector2(0f, 1f);
        saveTitleRect.anchorMax = new Vector2(1f, 1f);
        saveTitleRect.pivot = new Vector2(0f, 1f);
        saveTitleRect.offsetMin = new Vector2(32f, -58f);
        saveTitleRect.offsetMax = new Vector2(-28f, -18f);
        saveTitleText.color = Color.white;
        saveTitleText.raycastTarget = false;

        saveMetaText = CreateTmpLabel(saveButton.transform, string.Empty, 18, TextAlignmentOptions.Left);
        var saveMetaRect = saveMetaText.GetComponent<RectTransform>();
        saveMetaRect.anchorMin = new Vector2(0f, 1f);
        saveMetaRect.anchorMax = new Vector2(1f, 1f);
        saveMetaRect.pivot = new Vector2(0f, 1f);
        saveMetaRect.offsetMin = new Vector2(32f, -88f);
        saveMetaRect.offsetMax = new Vector2(-28f, -62f);
        saveMetaText.fontStyle = FontStyles.Normal;
        saveMetaText.color = new Color(1f, 1f, 1f, 0.62f);
        saveMetaText.raycastTarget = false;

        savePreviewText = CreateTmpLabel(saveButton.transform, "Начните игру и выполните сохранение.", 21, TextAlignmentOptions.Left);
        var savePreviewRect = savePreviewText.GetComponent<RectTransform>();
        savePreviewRect.anchorMin = new Vector2(0f, 0f);
        savePreviewRect.anchorMax = new Vector2(1f, 0f);
        savePreviewRect.pivot = new Vector2(0f, 0f);
        savePreviewRect.offsetMin = new Vector2(32f, 28f);
        savePreviewRect.offsetMax = new Vector2(-28f, 86f);
        savePreviewText.fontStyle = FontStyles.Normal;
        savePreviewText.color = new Color(1f, 1f, 1f, 0.86f);
        savePreviewText.raycastTarget = false;

        UnityEventTools.AddPersistentListener(saveButton.onClick, controller.LoadSelectedSave);

        var backButton = CreateStyledButton(window.transform, "Назад", new Vector2(190f, 58f));
        var backRect = backButton.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(1f, 0f);
        backRect.anchorMax = new Vector2(1f, 0f);
        backRect.pivot = new Vector2(1f, 0f);
        backRect.anchoredPosition = new Vector2(-54f, 42f);
        AddStyledButtonHoverEffect(backButton);
        UnityEventTools.AddPersistentListener(backButton.onClick, controller.CloseLoadPanel);

        return panelRoot;
    }

    private static Button CreateSaveCardButton(Transform parent)
    {
        var buttonGo = CreateUiObject("Save Card Button", parent);
        var image = buttonGo.AddComponent<Image>();
        image.color = new Color(0.015f, 0.045f, 0.095f, 0.88f);
        image.raycastTarget = true;

        var outline = buttonGo.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.22f);
        outline.effectDistance = new Vector2(1f, -1f);

        var button = buttonGo.AddComponent<Button>();
        button.targetGraphic = image;

        var colors = button.colors;
        colors.normalColor = new Color(0.015f, 0.045f, 0.095f, 0.88f);
        colors.highlightedColor = new Color(0.08f, 0.08f, 0.12f, 0.96f);
        colors.pressedColor = new Color(0.12f, 0.04f, 0.06f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.015f, 0.03f, 0.055f, 0.58f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        return button;
    }

    private static GameObject CreateExitConfirmPanel(Transform canvas, MainMenuController controller)
    {
        var panelRoot = CreateUiObject("Exit Confirm Panel", canvas);
        panelRoot.SetActive(false);
        StretchFull(panelRoot.GetComponent<RectTransform>());

        var dim = CreateUiObject("Exit Confirm Dim Blocker", panelRoot.transform);
        StretchFull(dim.GetComponent<RectTransform>());
        var dimImage = dim.AddComponent<Image>();
        dimImage.color = new Color(0.02f, 0.04f, 0.08f, 0.45f);
        dimImage.raycastTarget = true;

        var window = CreateUiObject("Exit Confirm Window", panelRoot.transform);
        var windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.anchoredPosition = Vector2.zero;
        windowRect.sizeDelta = new Vector2(620f, 320f);

        var windowImage = window.AddComponent<Image>();
        windowImage.sprite = TryLoadSprite(SettingsPanelBgPath);
        windowImage.type = Image.Type.Simple;
        windowImage.preserveAspect = false;
        windowImage.color = windowImage.sprite != null ? Color.white : new Color(0.025f, 0.07f, 0.13f, 0.94f);
        windowImage.raycastTarget = true;

        var windowShadow = window.AddComponent<Shadow>();
        windowShadow.effectColor = new Color(0f, 0f, 0f, 0.58f);
        windowShadow.effectDistance = new Vector2(5f, -5f);

        var outline = window.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.35f);
        outline.effectDistance = new Vector2(2f, -2f);

        var title = CreateTmpLabel(window.transform, "Выйти из игры?", 42, TextAlignmentOptions.Center);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -52f);
        titleRect.sizeDelta = new Vector2(520f, 54f);
        title.color = Color.white;
        title.raycastTarget = false;

        var body = CreateTmpLabel(window.transform, "Несохранённый прогресс может быть потерян.", 24, TextAlignmentOptions.Center);
        var bodyRect = body.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0.5f, 0.5f);
        bodyRect.anchorMax = new Vector2(0.5f, 0.5f);
        bodyRect.pivot = new Vector2(0.5f, 0.5f);
        bodyRect.anchoredPosition = new Vector2(0f, 20f);
        bodyRect.sizeDelta = new Vector2(520f, 64f);
        body.fontStyle = FontStyles.Normal;
        body.color = new Color(1f, 1f, 1f, 0.85f);
        body.raycastTarget = false;

        var yesButton = CreateStyledButton(window.transform, "Да", new Vector2(170f, 58f));
        var yesRect = yesButton.GetComponent<RectTransform>();
        yesRect.anchorMin = new Vector2(0.5f, 0f);
        yesRect.anchorMax = new Vector2(0.5f, 0f);
        yesRect.pivot = new Vector2(0.5f, 0f);
        yesRect.anchoredPosition = new Vector2(-105f, 46f);
        AddStyledButtonHoverEffect(yesButton);
        UnityEventTools.AddPersistentListener(yesButton.onClick, controller.ConfirmExit);

        var noButton = CreateStyledButton(window.transform, "Нет", new Vector2(170f, 58f));
        var noRect = noButton.GetComponent<RectTransform>();
        noRect.anchorMin = new Vector2(0.5f, 0f);
        noRect.anchorMax = new Vector2(0.5f, 0f);
        noRect.pivot = new Vector2(0.5f, 0f);
        noRect.anchoredPosition = new Vector2(105f, 46f);
        AddStyledButtonHoverEffect(noButton);
        UnityEventTools.AddPersistentListener(noButton.onClick, controller.CloseExitConfirm);

        return panelRoot;
    }

    private static GameObject CreateNotificationPanel(Transform canvas, out TextMeshProUGUI notificationText)
    {
        var panelRoot = CreateUiObject("Notification Panel", canvas);
        panelRoot.SetActive(false);
        var rect = panelRoot.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(140f, 120f);
        rect.sizeDelta = new Vector2(320f, 48f);

        var image = panelRoot.AddComponent<Image>();
        image.color = new Color(0.025f, 0.07f, 0.13f, 0.85f);
        image.raycastTarget = false;

        var outline = panelRoot.AddComponent<Outline>();
        outline.effectColor = new Color(0.9f, 0.08f, 0.06f, 0.75f);
        outline.effectDistance = new Vector2(2f, -2f);

        notificationText = CreateTmpLabel(panelRoot.transform, "Нет сохранения", 20, TextAlignmentOptions.Center);
        notificationText.fontStyle = FontStyles.Normal;
        notificationText.color = Color.white;
        notificationText.raycastTarget = false;
        StretchFull(notificationText.GetComponent<RectTransform>(), 18f, 18f, 4f, 4f);

        return panelRoot;
    }

    private static void AssignMainMenuControllerReferences(
        MainMenuController controller,
        GameObject aboutPanel,
        GameObject helpPanel,
        GameObject exitConfirmPanel,
        GameObject loadPanel,
        TextMeshProUGUI loadSaveTitleText,
        TextMeshProUGUI loadSaveMetaText,
        TextMeshProUGUI loadSavePreviewText,
        Button loadSaveButton,
        GameObject notificationPanel,
        TextMeshProUGUI notificationText)
    {
        var serializedController = new SerializedObject(controller);
        serializedController.FindProperty("aboutPanel").objectReferenceValue = aboutPanel;
        serializedController.FindProperty("helpPanel").objectReferenceValue = helpPanel;
        serializedController.FindProperty("exitConfirmPanel").objectReferenceValue = exitConfirmPanel;
        serializedController.FindProperty("loadPanel").objectReferenceValue = loadPanel;
        serializedController.FindProperty("loadSaveTitleText").objectReferenceValue = loadSaveTitleText;
        serializedController.FindProperty("loadSaveMetaText").objectReferenceValue = loadSaveMetaText;
        serializedController.FindProperty("loadSavePreviewText").objectReferenceValue = loadSavePreviewText;
        serializedController.FindProperty("loadSaveButton").objectReferenceValue = loadSaveButton;
        serializedController.FindProperty("notificationPanel").objectReferenceValue = notificationPanel;
        serializedController.FindProperty("notificationText").objectReferenceValue = notificationText;
        serializedController.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddStyledButtonHoverEffect(Button button)
    {
        var image = button.GetComponent<Image>();
        var label = button.GetComponentInChildren<Text>();
        var hoverEffect = button.gameObject.AddComponent<MainMenuButtonHoverEffect>();
        hoverEffect.highlightImage = image;
        hoverEffect.labelText = label;
        hoverEffect.normalHighlightColor = new Color(0.02f, 0.06f, 0.12f, 0.95f);
        hoverEffect.hoverHighlightColor = new Color(0.72f, 0.12f, 0.1f, 0.96f);
        hoverEffect.pressedHighlightColor = new Color(0.9f, 0.08f, 0.06f, 1f);
        hoverEffect.normalTextColor = new Color(0.96f, 0.97f, 1f, 0.98f);
        hoverEffect.hoverTextColor = Color.white;
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

    private static void CreateSettingsLogo(Transform parent)
    {
        var logoSprite = TryLoadSprite(LogoPath);
        if (logoSprite == null)
        {
            return;
        }

        var logo = CreateUiObject("Settings Logo", parent);
        var rect = logo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-780f, 275f);
        rect.sizeDelta = new Vector2(280f, 160f);
        rect.localRotation = Quaternion.Euler(0f, 0f, -5f);

        var image = logo.AddComponent<Image>();
        image.sprite = logoSprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = false;
    }

    private static TextMeshProUGUI CreateTmpLabel(Transform parent, string textValue, int fontSize, TextAlignmentOptions alignment)
    {
        var textGo = CreateUiObject("Text", parent);
        StretchFull(textGo.GetComponent<RectTransform>());
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = textValue;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateSettingsTab(
        Transform parent,
        string label,
        bool active,
        Vector2 position,
        out Image image,
        out TextMeshProUGUI text)
    {
        var tab = CreateUiObject(label + " Tab", parent);
        var rect = tab.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(250f, 54f);

        image = tab.AddComponent<Image>();
        image.sprite = TryLoadSprite(active ? SettingsTabActivePath : SettingsTabInactivePath);
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = image.sprite != null
            ? new Color(1f, 1f, 1f, active ? 0.95f : 0.80f)
            : active ? new Color(0.86f, 0.16f, 0.14f, 0.92f) : new Color(0.02f, 0.06f, 0.12f, 0.58f);
        image.raycastTarget = true;

        var button = tab.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;

        if (!active && image.sprite == null)
        {
            var outline = tab.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.18f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        text = CreateTmpLabel(tab.transform, label, 22, TextAlignmentOptions.Center);
        text.color = active ? Color.white : new Color(1f, 1f, 1f, 0.92f);
        text.raycastTarget = false;
        return button;
    }

    private static void CreatePlaceholderRow(Transform parent, string label, string value, float y)
    {
        var labelText = CreateTmpLabel(parent, label, 23, TextAlignmentOptions.Left);
        var labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(-210f, y);
        labelRect.sizeDelta = new Vector2(240f, 36f);
        labelText.color = new Color(1f, 1f, 1f, 0.92f);

        var box = CreateUiObject(label + " Placeholder", parent);
        var boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.anchoredPosition = new Vector2(250f, y);
        boxRect.sizeDelta = new Vector2(410f, 42f);
        var boxImage = box.AddComponent<Image>();
        boxImage.color = new Color(0.015f, 0.035f, 0.075f, 0.75f);
        boxImage.raycastTarget = false;

        var valueText = CreateTmpLabel(box.transform, value, 20, TextAlignmentOptions.Center);
        valueText.color = new Color(1f, 1f, 1f, 0.75f);
    }

    private static Slider CreateSettingsSliderRow(Transform parent, string label, float min, float max, float value, float y)
    {
        var labelText = CreateTmpLabel(parent, label, 23, TextAlignmentOptions.Left);
        var labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(-210f, y);
        labelRect.sizeDelta = new Vector2(240f, 36f);
        labelText.color = new Color(1f, 1f, 1f, 0.92f);

        var slider = CreateSlider(parent, min, max, value);
        var sliderRect = slider.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = new Vector2(250f, y);
        sliderRect.sizeDelta = new Vector2(410f, 24f);
        return slider;
    }

    private static Button CreateSettingsSelectorRow(Transform parent, string label, string value, float y, out TextMeshProUGUI valueText)
    {
        var labelText = CreateTmpLabel(parent, label, 23, TextAlignmentOptions.Left);
        var labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(-210f, y);
        labelRect.sizeDelta = new Vector2(300f, 36f);
        labelText.color = new Color(1f, 1f, 1f, 0.92f);

        var selector = CreateUiObject(label + " Selector", parent);
        var selectorRect = selector.GetComponent<RectTransform>();
        selectorRect.anchorMin = new Vector2(0.5f, 0.5f);
        selectorRect.anchorMax = new Vector2(0.5f, 0.5f);
        selectorRect.pivot = new Vector2(0.5f, 0.5f);
        selectorRect.anchoredPosition = new Vector2(250f, y);
        selectorRect.sizeDelta = new Vector2(410f, 42f);

        var selectorImage = selector.AddComponent<Image>();
        selectorImage.color = new Color(0.015f, 0.035f, 0.075f, 0.75f);
        selectorImage.raycastTarget = true;

        var button = selector.AddComponent<Button>();
        button.targetGraphic = selectorImage;
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.88f);
        colors.pressedColor = new Color(0.95f, 0.95f, 0.95f, 0.78f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.45f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        valueText = CreateTmpLabel(selector.transform, value, 20, TextAlignmentOptions.Center);
        valueText.color = new Color(1f, 1f, 1f, 0.86f);
        valueText.raycastTarget = false;
        StretchFull(valueText.GetComponent<RectTransform>(), 18f, 18f, 0f, 0f);
        return button;
    }

    private static Slider CreateSettingsValueSliderRow(
        Transform parent,
        string label,
        float min,
        float max,
        float value,
        float y,
        string valueLabel,
        out TextMeshProUGUI valueText)
    {
        var labelText = CreateTmpLabel(parent, label, 23, TextAlignmentOptions.Left);
        var labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(-210f, y);
        labelRect.sizeDelta = new Vector2(300f, 36f);
        labelText.color = new Color(1f, 1f, 1f, 0.92f);

        var slider = CreateSlider(parent, min, max, value);
        slider.wholeNumbers = true;
        var sliderRect = slider.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = new Vector2(200f, y);
        sliderRect.sizeDelta = new Vector2(300f, 24f);

        valueText = CreateTmpLabel(parent, valueLabel, 20, TextAlignmentOptions.Left);
        var valueRect = valueText.GetComponent<RectTransform>();
        valueRect.anchorMin = new Vector2(0.5f, 0.5f);
        valueRect.anchorMax = new Vector2(0.5f, 0.5f);
        valueRect.pivot = new Vector2(0f, 0.5f);
        valueRect.anchoredPosition = new Vector2(380f, y);
        valueRect.sizeDelta = new Vector2(170f, 36f);
        valueText.color = new Color(1f, 1f, 1f, 0.86f);
        return slider;
    }

    private static Toggle CreateSettingsToggleRow(Transform parent, string label, bool value, float y)
    {
        var labelText = CreateTmpLabel(parent, label, 23, TextAlignmentOptions.Left);
        var labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(-210f, y);
        labelRect.sizeDelta = new Vector2(360f, 36f);
        labelText.color = new Color(1f, 1f, 1f, 0.92f);

        var toggle = CreateToggle(parent);
        toggle.SetIsOnWithoutNotify(value);
        var toggleRect = toggle.GetComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0.5f, 0.5f);
        toggleRect.anchorMax = new Vector2(0.5f, 0.5f);
        toggleRect.pivot = new Vector2(0.5f, 0.5f);
        toggleRect.anchoredPosition = new Vector2(250f, y);
        toggleRect.sizeDelta = new Vector2(30f, 30f);
        return toggle;
    }

    private static Toggle CreateSettingsToggleRowAt(
        Transform parent,
        string label,
        bool value,
        float labelX,
        float toggleX,
        float y,
        float labelWidth)
    {
        var labelText = CreateTmpLabel(parent, label, 23, TextAlignmentOptions.Left);
        var labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(labelX, y);
        labelRect.sizeDelta = new Vector2(labelWidth, 36f);
        labelText.color = new Color(1f, 1f, 1f, 0.92f);

        var toggle = CreateToggle(parent);
        toggle.SetIsOnWithoutNotify(value);
        var toggleRect = toggle.GetComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0.5f, 0.5f);
        toggleRect.anchorMax = new Vector2(0.5f, 0.5f);
        toggleRect.pivot = new Vector2(0.5f, 0.5f);
        toggleRect.anchoredPosition = new Vector2(toggleX, y);
        toggleRect.sizeDelta = new Vector2(30f, 30f);
        return toggle;
    }

    private static Toggle CreateFullscreenRow(Transform parent, float y)
    {
        var labelText = CreateTmpLabel(parent, "Полный экран", 23, TextAlignmentOptions.Left);
        var labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(-210f, y);
        labelRect.sizeDelta = new Vector2(240f, 36f);
        labelText.color = new Color(1f, 1f, 1f, 0.92f);

        var toggle = CreateToggle(parent);
        var toggleRect = toggle.GetComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0.5f, 0.5f);
        toggleRect.anchorMax = new Vector2(0.5f, 0.5f);
        toggleRect.pivot = new Vector2(0.5f, 0.5f);
        toggleRect.anchoredPosition = new Vector2(50f, y);
        toggleRect.sizeDelta = new Vector2(30f, 30f);
        return toggle;
    }

    private static Button CreateBackButton(Transform parent)
    {
        var button = CreateStyledButton(parent, "Назад", new Vector2(200f, 60f));
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-735f, -430f);

        var image = button.GetComponent<Image>();
        var sprite = TryLoadSprite(SettingsBackButtonPath);
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
        }

        return button;
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
        bgRect.sizeDelta = new Vector2(0f, 9f);
        var bgImage = background.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.65f);
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
        fillImage.color = new Color(0.9f, 0.1f, 0.08f, 1f);
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
        handleImage.color = Color.white;

        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        return slider;
    }

    private static Toggle CreateToggle(Transform parent)
    {
        var toggleGo = CreateUiObject("Settings Toggle", parent);
        toggleGo.GetComponent<RectTransform>().sizeDelta = new Vector2(30f, 30f);

        var background = toggleGo.AddComponent<Image>();
        background.color = new Color(0.015f, 0.035f, 0.075f, 0.85f);

        var outline = toggleGo.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.35f);
        outline.effectDistance = new Vector2(1f, -1f);

        var toggle = toggleGo.AddComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.interactable = true;

        var checkmarkGo = CreateUiObject("Checkmark", toggleGo.transform);
        StretchFull(checkmarkGo.GetComponent<RectTransform>(), 7f, 7f, 7f, 7f);
        var checkmark = checkmarkGo.AddComponent<Image>();
        checkmark.color = new Color(0.9f, 0.08f, 0.06f, 1f);
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
        colors.normalColor = new Color(0.02f, 0.06f, 0.12f, 0.95f);
        colors.highlightedColor = new Color(0.72f, 0.12f, 0.1f, 0.96f);
        colors.pressedColor = new Color(0.9f, 0.08f, 0.06f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.06f, 0.05f, 0.08f, 0.5f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        var text = CreateLabel(buttonGo.transform, label, 24, TextAnchor.MiddleCenter);
        text.color = new Color(0.96f, 0.97f, 1f, 0.98f);
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
