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
    private const string UiClickSfxWavPath = "Assets/HowIFall/Audio/SFX/ui_click.wav";
    private const string UiClickSfxMp3Path = "Assets/HowIFall/Audio/SFX/ui_click.mp3";
    private const string UiClickSfxOggPath = "Assets/HowIFall/Audio/SFX/ui_click.ogg";
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
        var clickSfx = TryLoadAudioClip(UiClickSfxWavPath, UiClickSfxMp3Path, UiClickSfxOggPath);

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

        var menuCanvasGroup = CreateMainMenuRoot(canvas.transform, mainMenuController, clickSfx);
        var titleCanvasGroup = CreateGameLogo(canvas.transform, out var titleObject);
        var pressAnyObject = CreatePressAnyButton(canvas.transform);
        var footerObject = CreateFooter(canvas.transform);

        var settingsPanelController = CreateSettingsPanel(canvas.transform);
        settingsPanelController.objectsToHideWhenOpen = new[] { titleObject, pressAnyObject, footerObject };
        mainMenuController.settingsPanel = settingsPanelController;

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

    private static CanvasGroup CreateMainMenuRoot(Transform canvas, MainMenuController controller, AudioClip clickSfx)
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
        labels = new[] { "Start", "Continue", "Settings", "About", "Help", "Exit" };
        var methods = new System.Action<Button>[]
        {
            b => UnityEventTools.AddPersistentListener(b.onClick, controller.StartGame),
            b => UnityEventTools.AddPersistentListener(b.onClick, controller.ContinueGame),
            b => UnityEventTools.AddPersistentListener(b.onClick, controller.OpenSettings),
            b => UnityEventTools.AddPersistentListener(b.onClick, controller.OpenAbout),
            b => UnityEventTools.AddPersistentListener(b.onClick, controller.OpenHelp),
            b => UnityEventTools.AddPersistentListener(b.onClick, controller.ExitGame)
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

            var button = CreateMenuButton(row.transform, labels[i], clickSfx);
            methods[i](button);

            if (false && i == 1)
            {
                CreateQuickSaveStatus(row.transform);
            }

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

    private static void CreateQuickSaveStatus(Transform parent)
    {
        var statusGo = CreateUiObject("Quick Save Status", parent);
        var statusRect = statusGo.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(1f, 0f);
        statusRect.pivot = new Vector2(0.5f, 0f);
        statusRect.offsetMin = new Vector2(30f, 3f);
        statusRect.offsetMax = new Vector2(-18f, 23f);

        var statusText = statusGo.AddComponent<Text>();
        statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        statusText.fontSize = 14;
        statusText.alignment = TextAnchor.LowerRight;
        statusText.color = new Color(0.08f, 0.12f, 0.22f, 0.72f);
        statusText.raycastTarget = false;
        statusText.text = "quick save: ...";

        var statusView = statusGo.AddComponent<QuickSaveStatusView>();
        statusView.statusText = statusText;
    }

    private static Button CreateMenuButton(Transform parent, string label, AudioClip clickSfx)
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
        hoverEffect.clickSfx = clickSfx;

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
        text.fontSize = 15;
        text.alignment = TextAnchor.MiddleRight;
        text.color = new Color(0.1f, 0.12f, 0.18f, 0.35f);
        text.raycastTarget = false;
        return footer;
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
        windowRect.sizeDelta = new Vector2(1080f, 700f);
        windowRect.anchoredPosition = new Vector2(160f, 30f);
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

        var title = CreateTmpLabel(window.transform, "Settings", 58, TextAlignmentOptions.Center);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 300f);
        titleRect.sizeDelta = new Vector2(420f, 68f);
        title.color = Color.white;
        title.raycastTarget = false;

        var underline = CreateUiObject("Settings Red Underline", window.transform);
        var underlineRect = underline.GetComponent<RectTransform>();
        underlineRect.anchorMin = new Vector2(0.5f, 0.5f);
        underlineRect.anchorMax = new Vector2(0.5f, 0.5f);
        underlineRect.pivot = new Vector2(0.5f, 0.5f);
        underlineRect.anchoredPosition = new Vector2(0f, 264f);
        underlineRect.sizeDelta = new Vector2(180f, 5f);
        underlineRect.localRotation = Quaternion.Euler(0f, 0f, -4f);
        var underlineImage = underline.AddComponent<Image>();
        underlineImage.color = new Color(0.9f, 0.08f, 0.06f, 1f);
        underlineImage.raycastTarget = false;

        string[] tabs = { "Graphics", "Audio", "Gameplay", "Controls", "Language" };
        for (int i = 0; i < tabs.Length; i++)
        {
            CreateSettingsTab(window.transform, tabs[i], tabs[i] == "Audio", new Vector2(-395f, 175f - i * 58f));
        }

        var divider = CreateUiObject("Settings Divider", window.transform);
        var dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0.5f, 0.5f);
        dividerRect.anchorMax = new Vector2(0.5f, 0.5f);
        dividerRect.pivot = new Vector2(0.5f, 0.5f);
        dividerRect.anchoredPosition = new Vector2(-245f, 20f);
        dividerRect.sizeDelta = new Vector2(2f, 500f);
        var dividerImage = divider.AddComponent<Image>();
        dividerImage.color = new Color(1f, 1f, 1f, 0.22f);
        dividerImage.raycastTarget = false;

        CreatePlaceholderRow(window.transform, "Resolution", "1920 x 1080 (16:9)", 175f);
        CreatePlaceholderRow(window.transform, "Window Mode", "Windowed Fullscreen", 119f);
        Slider master = CreateSettingsSliderRow(window.transform, "Master Volume", 0f, 1f, 1f, 63f);
        Slider music = CreateSettingsSliderRow(window.transform, "Music Volume", 0f, 1f, 1f, 7f);
        Slider sfx = CreateSettingsSliderRow(window.transform, "SFX Volume", 0f, 1f, 1f, -49f);
        Slider textSpeed = CreateSettingsSliderRow(window.transform, "Text Speed", 0.25f, 3f, 1f, -105f);
        Toggle fullscreenToggle = CreateFullscreenRow(window.transform, -161f);

        var resetButton = CreateStyledButton(window.transform, "Reset", new Vector2(160f, 46f));
        var resetRect = resetButton.GetComponent<RectTransform>();
        resetRect.anchorMin = new Vector2(0.5f, 0.5f);
        resetRect.anchorMax = new Vector2(0.5f, 0.5f);
        resetRect.pivot = new Vector2(0.5f, 0.5f);
        resetRect.anchoredPosition = new Vector2(310f, -255f);

        var backButton = CreateBackButton(panelRoot.transform);

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
        rect.anchoredPosition = new Vector2(-735f, 285f);
        rect.sizeDelta = new Vector2(330f, 190f);
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

    private static void CreateSettingsTab(Transform parent, string label, bool active, Vector2 position)
    {
        var tab = CreateUiObject(label + " Tab", parent);
        var rect = tab.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = active ? new Vector2(220f, 48f) : new Vector2(210f, 46f);

        var image = tab.AddComponent<Image>();
        image.sprite = TryLoadSprite(active ? SettingsTabActivePath : SettingsTabInactivePath);
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = image.sprite != null
            ? new Color(1f, 1f, 1f, active ? 0.95f : 0.72f)
            : active ? new Color(0.86f, 0.16f, 0.14f, 0.92f) : new Color(0.02f, 0.06f, 0.12f, 0.58f);
        image.raycastTarget = false;

        var text = CreateTmpLabel(tab.transform, label, 21, TextAlignmentOptions.Center);
        text.color = active ? Color.white : new Color(1f, 1f, 1f, 0.82f);
        text.raycastTarget = false;
    }

    private static void CreatePlaceholderRow(Transform parent, string label, string value, float y)
    {
        var labelText = CreateTmpLabel(parent, label, 22, TextAlignmentOptions.Left);
        var labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(-155f, y);
        labelRect.sizeDelta = new Vector2(220f, 40f);
        labelText.color = new Color(1f, 1f, 1f, 0.75f);

        var box = CreateUiObject(label + " Placeholder", parent);
        var boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.anchoredPosition = new Vector2(205f, y);
        boxRect.sizeDelta = new Vector2(360f, 42f);
        var boxImage = box.AddComponent<Image>();
        boxImage.color = new Color(0f, 0f, 0f, 0.55f);
        boxImage.raycastTarget = false;

        var valueText = CreateTmpLabel(box.transform, value, 20, TextAlignmentOptions.Center);
        valueText.color = new Color(1f, 1f, 1f, 0.75f);
    }

    private static Slider CreateSettingsSliderRow(Transform parent, string label, float min, float max, float value, float y)
    {
        var labelText = CreateTmpLabel(parent, label, 22, TextAlignmentOptions.Left);
        var labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(-155f, y);
        labelRect.sizeDelta = new Vector2(220f, 40f);
        labelText.color = new Color(0.95f, 0.97f, 1f, 0.96f);

        var slider = CreateSlider(parent, min, max, value);
        var sliderRect = slider.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = new Vector2(205f, y);
        sliderRect.sizeDelta = new Vector2(360f, 24f);
        return slider;
    }

    private static Toggle CreateFullscreenRow(Transform parent, float y)
    {
        var labelText = CreateTmpLabel(parent, "Fullscreen", 22, TextAlignmentOptions.Left);
        var labelRect = labelText.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(-155f, y);
        labelRect.sizeDelta = new Vector2(220f, 40f);
        labelText.color = new Color(0.95f, 0.97f, 1f, 0.96f);

        var toggle = CreateToggle(parent);
        var toggleRect = toggle.GetComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0.5f, 0.5f);
        toggleRect.anchorMax = new Vector2(0.5f, 0.5f);
        toggleRect.pivot = new Vector2(0.5f, 0.5f);
        toggleRect.anchoredPosition = new Vector2(35f, y);
        toggleRect.sizeDelta = new Vector2(30f, 30f);
        return toggle;
    }

    private static Button CreateBackButton(Transform parent)
    {
        var button = CreateStyledButton(parent, "Back", new Vector2(200f, 60f));
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
        var toggleGo = CreateUiObject("Fullscreen Toggle", parent);
        toggleGo.GetComponent<RectTransform>().sizeDelta = new Vector2(32f, 32f);

        var background = toggleGo.AddComponent<Image>();
        background.color = new Color(0.01f, 0.03f, 0.06f, 0.95f);

        var outline = toggleGo.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.18f);
        outline.effectDistance = new Vector2(1f, -1f);

        var toggle = toggleGo.AddComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.interactable = true;

        var checkmarkGo = CreateUiObject("Checkmark", toggleGo.transform);
        StretchFull(checkmarkGo.GetComponent<RectTransform>(), 9f, 9f, 9f, 9f);
        var checkmark = checkmarkGo.AddComponent<Image>();
        checkmark.color = new Color(0.9f, 0.1f, 0.08f, 1f);
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
