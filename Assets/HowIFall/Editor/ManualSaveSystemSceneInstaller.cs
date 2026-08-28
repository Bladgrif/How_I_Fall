using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ManualSaveSystemSceneInstaller
{
    private const string MainMenuScenePath = "Assets/HowIFall/Scenes/MainMenu.unity";
    private const string VnScenePath = "Assets/HowIFall/Scenes/VNPrototype.unity";
    private const string RegistryPath = "Assets/HowIFall/Data/Dialogues/DialogueSceneRegistry.asset";
    private const string PrefabFolder = "Assets/HowIFall/Prefabs/UI";
    private const string PrefabPath = PrefabFolder + "/ManualSaveLoadPanel.prefab";
    private const int PanelVisualVersion = 6;

    private static readonly Color DimColor = new Color(0.008f, 0.014f, 0.03f, 0.72f);
    private static readonly Color CardColor = new Color(0.052f, 0.071f, 0.1f, 0.97f);
    private static readonly Color ColdAccent = new Color(0.28f, 0.52f, 0.76f, 1f);
    private static readonly Color MutedRed = new Color(0.55f, 0.16f, 0.22f, 1f);
    private static readonly Color PrimaryText = new Color(0.91f, 0.95f, 1f, 1f);
    private static readonly Color SecondaryText = new Color(0.58f, 0.67f, 0.78f, 1f);

    [MenuItem("How I Fall/Save System/Install Clean Manual Save UI")]
    public static void InstallFromMenu()
    {
        Install();
        Debug.Log("[SAVE INSTALL] Manual Save/Load UI installed.");
    }

    [MenuItem("How I Fall/Save System/Validate Manual Save UI")]
    public static void ValidateFromMenu()
    {
        ValidateInstalledScenes();
        Debug.Log("[SAVE INSTALL] Manual Save/Load UI validation passed.");
    }

    public static void RunBatchMode()
    {
        Install();
        Debug.Log("[SAVE INSTALL] BATCH COMPLETE");
    }

    public static void RunValidationBatchMode()
    {
        ValidateInstalledScenes();
        Debug.Log("[SAVE INSTALL] VALIDATION BATCH COMPLETE");
    }

    public static void RunPrefabPolishBatchMode()
    {
        BuildPanelPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SAVE INSTALL] PREFAB POLISH COMPLETE");
    }

    public static void ValidateInstalledScenes()
    {
        Scene mainMenu = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        ValidateScene(mainMenu, false);
        Scene vn = EditorSceneManager.OpenScene(VnScenePath, OpenSceneMode.Single);
        ValidateScene(vn, true);
    }

    private static void Install()
    {
        DialogueSceneRegistry registry = AssetDatabase.LoadAssetAtPath<DialogueSceneRegistry>(RegistryPath);
        if (registry == null)
        {
            throw new InvalidOperationException($"DialogueSceneRegistry was not found at '{RegistryPath}'.");
        }

        GameObject panelPrefab = BuildPanelPrefab();
        InstallMainMenu(panelPrefab, registry);
        InstallVnScene(panelPrefab, registry);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void InstallMainMenu(GameObject panelPrefab, DialogueSceneRegistry registry)
    {
        Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        RemoveOldSaveObjects(scene);
        CleanSceneReferences(scene);

        Canvas canvas = FindInScene<Canvas>(scene);
        MainMenuController controller = FindInScene<MainMenuController>(scene);
        if (canvas == null || controller == null)
        {
            throw new InvalidOperationException("MainMenu must contain Canvas and MainMenuController.");
        }

        ManualSaveLoadPanel panel = InstantiatePanel(panelPrefab, canvas.transform);
        SaveManager saveManager = EnsureSceneSaveManager(scene, registry);

        Button continueButton = ReplaceButton(scene, "Продолжить Button", "Continue Button", "Продолжить");
        UnityEventTools.AddPersistentListener(continueButton.onClick, controller.ContinueFromLatestSave);

        Button loadButton = ReplaceButton(scene, "Загрузить Button", "Load Button", "Загрузить");
        UnityEventTools.AddPersistentListener(loadButton.onClick, panel.OpenLoad);

        controller.manualSaveLoadPanel = panel;
        controller.dialogueRegistry = registry;
        controller.continueButton = continueButton;
        saveManager.ConfigureRegistry(registry);

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(saveManager);
        ValidateScene(scene, false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void InstallVnScene(GameObject panelPrefab, DialogueSceneRegistry registry)
    {
        Scene scene = EditorSceneManager.OpenScene(VnScenePath, OpenSceneMode.Single);
        RemoveOldSaveObjects(scene);
        CleanSceneReferences(scene);

        Canvas canvas = FindInScene<Canvas>(scene);
        VNDialogueController controller = FindInScene<VNDialogueController>(scene);
        if (canvas == null || controller == null)
        {
            throw new InvalidOperationException("VNPrototype must contain Canvas and VNDialogueController.");
        }

        ManualSaveLoadPanel panel = InstantiatePanel(panelPrefab, canvas.transform);
        SaveManager saveManager = EnsureSceneSaveManager(scene, registry);

        Button saveButton = ReplaceButton(scene, "Сохр. Button", "Save Button", "Сохр.");
        UnityEventTools.AddPersistentListener(saveButton.onClick, panel.OpenSave);

        Button loadButton = ReplaceButton(scene, "Загр. Button", "Load Button", "Загр.");
        UnityEventTools.AddPersistentListener(loadButton.onClick, panel.OpenLoad);

        controller.manualSaveLoadPanel = panel;
        saveManager.ConfigureRegistry(registry);

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(saveManager);
        ValidateScene(scene, true);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static GameObject BuildPanelPrefab()
    {
        EnsureAssetFolder("Assets/HowIFall", "Prefabs");
        EnsureAssetFolder("Assets/HowIFall/Prefabs", "UI");

        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existingPrefab != null)
        {
            return UpgradeExistingPanelPrefab();
        }

        GameObject root = CreateUiObject("Manual Save Load Panel", null);
        try
        {
            ConfigurePanelRoot(root, out ManualSaveLoadPanel panel);
            BuildPanelContents(root.transform, panel);
            root.SetActive(true);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException($"Failed to create prefab '{PrefabPath}'.");
            }

            return prefab;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static GameObject UpgradeExistingPanelPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            ManualSaveLoadPanel panel = root.GetComponent<ManualSaveLoadPanel>();
            bool requiresRebuild = panel == null
                || panel.visualVersion != PanelVisualVersion
                || panel.windowRect == null
                || panel.contentCanvasGroup == null
                || panel.subtitleText == null
                || panel.slotTypeHintText == null
                || panel.manualTabButton == null
                || panel.autoTabButton == null
                || panel.quickTabButton == null
                || panel.statusCanvasGroup == null
                || panel.confirmationCanvasGroup == null
                || panel.confirmationWindow == null
                || panel.slotViews == null
                || panel.slotViews.Length != SaveManager.SlotCount
                || panel.slotViews.Any(view => !HasRequiredSlotReferences(view));

            if (!requiresRebuild)
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            }

            ConfigurePanelRoot(root, out panel);
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            BuildPanelContents(root.transform, panel);
            EditorUtility.SetDirty(panel);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    private static void ConfigurePanelRoot(GameObject root, out ManualSaveLoadPanel panel)
    {
        RectTransform rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);

        Image rootImage = GetOrAddComponent<Image>(root);
        rootImage.color = DimColor;
        rootImage.raycastTarget = true;

        CanvasGroup canvasGroup = GetOrAddComponent<CanvasGroup>(root);
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        panel = GetOrAddComponent<ManualSaveLoadPanel>(root);
        panel.visualVersion = PanelVisualVersion;
        panel.canvasGroup = canvasGroup;
    }

    private static void BuildPanelContents(Transform root, ManualSaveLoadPanel panel)
    {
        CreateDecorativeShape(
            "Cold Glow",
            root,
            new Color(0.1f, 0.24f, 0.4f, 0.08f),
            new Vector2(0f, 1f),
            new Vector2(0.7f, 1f),
            new Vector2(0f, -102f),
            new Vector2(0f, 204f));
        CreateDecorativeShape(
            "Muted Red Glow",
            root,
            new Color(0.42f, 0.07f, 0.11f, 0.075f),
            new Vector2(0.76f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 104f),
            new Vector2(0f, 208f));

        GameObject window = CreateUiObject("Window", root);
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(1580f, 940f);
        Image windowImage = window.AddComponent<Image>();
        windowImage.color = Color.white;
        windowImage.raycastTarget = true;
        UiVerticalGradient windowGradient = window.AddComponent<UiVerticalGradient>();
        windowGradient.topColor = new Color(0.045f, 0.078f, 0.125f, 0.95f);
        windowGradient.bottomColor = new Color(0.016f, 0.032f, 0.052f, 0.9f);

        Shadow shadow = window.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
        shadow.effectDistance = new Vector2(0f, -12f);
        Outline outline = window.AddComponent<Outline>();
        outline.effectColor = new Color(0.18f, 0.28f, 0.39f, 0.52f);
        outline.effectDistance = new Vector2(1f, -1f);
        CanvasGroup contentGroup = window.AddComponent<CanvasGroup>();

        panel.windowRect = windowRect;
        panel.contentCanvasGroup = contentGroup;

        GameObject header = CreateUiObject("Header Wash", window.transform);
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = Vector2.one;
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(0f, 180f);
        Image headerImage = header.AddComponent<Image>();
        headerImage.color = Color.white;
        headerImage.raycastTarget = false;
        UiVerticalGradient headerGradient = header.AddComponent<UiVerticalGradient>();
        headerGradient.topColor = new Color(0.12f, 0.22f, 0.34f, 0.3f);
        headerGradient.bottomColor = new Color(0.05f, 0.09f, 0.14f, 0f);

        GameObject headerSeparator = CreateUiObject("Header Separator", window.transform);
        RectTransform headerSeparatorRect = headerSeparator.GetComponent<RectTransform>();
        headerSeparatorRect.anchorMin = headerSeparatorRect.anchorMax = new Vector2(0.5f, 1f);
        headerSeparatorRect.anchoredPosition = new Vector2(0f, -164f);
        headerSeparatorRect.sizeDelta = new Vector2(1420f, 1f);
        Image headerSeparatorImage = headerSeparator.AddComponent<Image>();
        headerSeparatorImage.color = new Color(0.28f, 0.46f, 0.62f, 0.2f);
        headerSeparatorImage.raycastTarget = false;

        GameObject topHairline = CreateUiObject("Top Hairline", window.transform);
        RectTransform topHairlineRect = topHairline.GetComponent<RectTransform>();
        topHairlineRect.anchorMin = new Vector2(0f, 1f);
        topHairlineRect.anchorMax = Vector2.one;
        topHairlineRect.pivot = new Vector2(0.5f, 1f);
        topHairlineRect.sizeDelta = new Vector2(0f, 1f);
        Image topHairlineImage = topHairline.AddComponent<Image>();
        topHairlineImage.color = new Color(0.27f, 0.5f, 0.68f, 0.42f);
        topHairlineImage.raycastTarget = false;

        GameObject redAccent = CreateUiObject("Red Accent", window.transform);
        RectTransform redAccentRect = redAccent.GetComponent<RectTransform>();
        redAccentRect.anchorMin = redAccentRect.anchorMax = new Vector2(0f, 1f);
        redAccentRect.pivot = new Vector2(0f, 1f);
        redAccentRect.anchoredPosition = new Vector2(0f, -1f);
        redAccentRect.sizeDelta = new Vector2(175f, 2f);
        Image redAccentImage = redAccent.AddComponent<Image>();
        redAccentImage.color = new Color(MutedRed.r, MutedRed.g, MutedRed.b, 0.68f);
        redAccentImage.raycastTarget = false;

        panel.titleText = CreateText(
            "Title",
            window.transform,
            "Загрузка",
            48,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -42f),
            new Vector2(760f, 60f));
        panel.titleText.color = PrimaryText;
        panel.titleText.fontStyle = FontStyles.Bold;
        panel.titleText.characterSpacing = 2f;

        panel.subtitleText = CreateText(
            "Subtitle",
            window.transform,
            "РУЧНЫЕ СОХРАНЕНИЯ",
            17,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -82f),
            new Vector2(520f, 30f));
        panel.subtitleText.color = new Color(0.46f, 0.62f, 0.78f, 0.78f);
        panel.subtitleText.characterSpacing = 6f;

        BuildSlotTypeTabs(window.transform, panel);

        panel.slotTypeHintText = CreateText(
            "Slot Type Hint",
            window.transform,
            string.Empty,
            16,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -168f),
            new Vector2(900f, 24f));
        panel.slotTypeHintText.color = new Color(0.54f, 0.65f, 0.76f, 0.78f);
        panel.slotTypeHintText.fontStyle = FontStyles.Italic;
        panel.slotTypeHintText.gameObject.SetActive(false);

        panel.closeButton = CreateButton(
            "Close Button",
            window.transform,
            "← Назад",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-96f, -49f),
            new Vector2(152f, 44f));
        StyleNavigationButton(panel.closeButton);

        GameObject gridObject = CreateUiObject("Slots Grid", window.transform);
        RectTransform gridRect = gridObject.GetComponent<RectTransform>();
        gridRect.anchorMin = gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.anchoredPosition = new Vector2(0f, -64f);
        gridRect.sizeDelta = new Vector2(1422f, 680f);
        GridLayoutGroup grid = gridObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(458f, 330f);
        grid.spacing = new Vector2(24f, 20f);
        grid.padding = new RectOffset(0, 0, 0, 0);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.MiddleCenter;

        var slotViews = new ManualSaveSlotView[SaveManager.SlotCount];
        for (int i = 0; i < slotViews.Length; i++)
        {
            slotViews[i] = CreateSlotView(gridObject.transform, i + 1);
        }

        panel.slotViews = slotViews;
        GameObject statusToast = CreateUiObject("Status Toast", window.transform);
        RectTransform statusToastRect = statusToast.GetComponent<RectTransform>();
        statusToastRect.anchorMin = statusToastRect.anchorMax = new Vector2(0.5f, 0f);
        statusToastRect.anchoredPosition = new Vector2(0f, 25f);
        statusToastRect.sizeDelta = new Vector2(720f, 42f);
        Image statusToastImage = statusToast.AddComponent<Image>();
        statusToastImage.color = new Color(0.025f, 0.045f, 0.07f, 0.86f);
        statusToastImage.raycastTarget = false;
        Outline statusOutline = statusToast.AddComponent<Outline>();
        statusOutline.effectColor = new Color(0.24f, 0.42f, 0.58f, 0.32f);
        statusOutline.effectDistance = new Vector2(1f, -1f);
        CanvasGroup statusGroup = statusToast.AddComponent<CanvasGroup>();
        statusGroup.alpha = 0f;
        statusGroup.interactable = false;
        statusGroup.blocksRaycasts = false;

        panel.statusText = CreateText(
            "Status Text",
            statusToast.transform,
            string.Empty,
            20,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero);
        Stretch(panel.statusText.rectTransform);
        panel.statusText.color = new Color(0.7f, 0.8f, 0.94f, 1f);
        panel.statusCanvasGroup = statusGroup;
        panel.statusVisibleDuration = 1.75f;

        BuildConfirmation(panel, root);
    }

    private static void BuildSlotTypeTabs(Transform parent, ManualSaveLoadPanel panel)
    {
        GameObject tabBar = CreateUiObject("Save Type Tabs", parent);
        RectTransform tabBarRect = tabBar.GetComponent<RectTransform>();
        tabBarRect.anchorMin = tabBarRect.anchorMax = new Vector2(0.5f, 1f);
        tabBarRect.anchoredPosition = new Vector2(0f, -128f);
        tabBarRect.sizeDelta = new Vector2(830f, 42f);

        panel.manualTabButton = CreateSlotTypeTab(tabBar.transform, "Manual Tab Button", "РУЧНЫЕ", -278f, true);
        panel.autoTabButton = CreateSlotTypeTab(tabBar.transform, "Auto Tab Button", "АВТО", 0f, false);
        panel.quickTabButton = CreateSlotTypeTab(tabBar.transform, "Quick Tab Button", "БЫСТРЫЕ", 278f, false);
    }

    private static Button CreateSlotTypeTab(Transform parent, string name, string label, float x, bool active)
    {
        Button button = CreateButton(
            name,
            parent,
            label,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(x, 0f),
            new Vector2(262f, 40f));
        StyleSlotTypeTab(button, active);

        GameObject accent = CreateUiObject("Active Accent", button.transform);
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(1f, 0f);
        accentRect.pivot = new Vector2(0.5f, 0f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(-22f, 2f);
        Image accentImage = accent.AddComponent<Image>();
        accentImage.color = new Color(0.33f, 0.65f, 0.9f, 0.9f);
        accentImage.raycastTarget = false;
        accent.SetActive(active);
        return button;
    }

    private static ManualSaveSlotView CreateSlotView(Transform parent, int slotIndex)
    {
        GameObject slotObject = CreateUiObject($"Manual Slot {slotIndex}", parent);
        RectTransform slotRect = slotObject.GetComponent<RectTransform>();
        Image background = slotObject.AddComponent<Image>();
        background.color = CardColor;

        Shadow shadow = slotObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.34f);
        shadow.effectDistance = new Vector2(0f, -4f);
        Outline outline = slotObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.18f, 0.29f, 0.4f, 0.48f);
        outline.effectDistance = new Vector2(1f, -1f);

        Button button = slotObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;

        ManualSaveSlotView view = slotObject.AddComponent<ManualSaveSlotView>();
        view.button = button;
        view.cardRect = slotRect;
        view.backgroundImage = background;
        view.cardOutline = outline;

        GameObject hoverAccent = CreateUiObject("Hover Accent", slotObject.transform);
        RectTransform hoverAccentRect = hoverAccent.GetComponent<RectTransform>();
        Stretch(hoverAccentRect);
        hoverAccentRect.offsetMin = new Vector2(1f, 1f);
        hoverAccentRect.offsetMax = new Vector2(-1f, -1f);
        view.hoverAccentImage = hoverAccent.AddComponent<Image>();
        view.hoverAccentImage.color = new Color(0.25f, 0.52f, 0.72f, 0f);
        view.hoverAccentImage.raycastTarget = false;

        GameObject occupiedAccent = CreateUiObject("Occupied Accent", slotObject.transform);
        RectTransform occupiedAccentRect = occupiedAccent.GetComponent<RectTransform>();
        occupiedAccentRect.anchorMin = new Vector2(0f, 0f);
        occupiedAccentRect.anchorMax = new Vector2(0f, 1f);
        occupiedAccentRect.pivot = new Vector2(0f, 0.5f);
        occupiedAccentRect.anchoredPosition = new Vector2(0f, 0f);
        occupiedAccentRect.sizeDelta = new Vector2(3f, -2f);
        view.occupiedAccentImage = occupiedAccent.AddComponent<Image>();
        view.occupiedAccentImage.color = new Color(0.28f, 0.57f, 0.82f, 0.78f);
        view.occupiedAccentImage.raycastTarget = false;

        GameObject previewFrame = CreateUiObject("Preview Frame", slotObject.transform);
        RectTransform previewFrameRect = previewFrame.GetComponent<RectTransform>();
        previewFrameRect.anchorMin = previewFrameRect.anchorMax = new Vector2(0.5f, 1f);
        previewFrameRect.anchoredPosition = new Vector2(0f, -134f);
        previewFrameRect.sizeDelta = new Vector2(434f, 244f);
        Image previewFrameImage = previewFrame.AddComponent<Image>();
        previewFrameImage.color = new Color(0.15f, 0.21f, 0.27f, 0.56f);
        previewFrameImage.raycastTarget = false;
        view.previewFrameImage = previewFrameImage;

        GameObject previewObject = CreateUiObject("Preview", previewFrame.transform);
        RectTransform previewRect = previewObject.GetComponent<RectTransform>();
        Stretch(previewRect);
        previewRect.offsetMin = new Vector2(2f, 2f);
        previewRect.offsetMax = new Vector2(-2f, -2f);
        view.previewImage = previewObject.AddComponent<Image>();
        view.previewImage.color = new Color(0.035f, 0.045f, 0.058f, 1f);
        view.previewImage.raycastTarget = false;

        GameObject placeholderOverlay = CreateUiObject("Placeholder Gradient", previewObject.transform);
        RectTransform placeholderOverlayRect = placeholderOverlay.GetComponent<RectTransform>();
        Stretch(placeholderOverlayRect);
        view.placeholderOverlayImage = placeholderOverlay.AddComponent<Image>();
        view.placeholderOverlayImage.color = Color.white;
        view.placeholderOverlayImage.raycastTarget = false;
        UiVerticalGradient placeholderGradient = placeholderOverlay.AddComponent<UiVerticalGradient>();
        placeholderGradient.topColor = new Color(0.09f, 0.125f, 0.16f, 0.72f);
        placeholderGradient.bottomColor = new Color(0.035f, 0.047f, 0.06f, 0.82f);

        view.backgroundSlotNumberText = CreateText(
            "Background Slot Number",
            previewObject.transform,
            slotIndex.ToString("00"),
            76,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero);
        Stretch(view.backgroundSlotNumberText.rectTransform);
        view.backgroundSlotNumberText.color = new Color(0.34f, 0.45f, 0.57f, 0.11f);
        view.backgroundSlotNumberText.fontStyle = FontStyles.Bold;

        view.emptyText = CreateText(
            "Empty",
            previewObject.transform,
            "Пустой слот",
            23,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(370f, 66f));
        view.emptyText.color = new Color(0.48f, 0.55f, 0.63f, 0.68f);
        view.emptyText.fontStyle = FontStyles.Italic;

        view.sceneNameText = CreateText(
            "Scene Name",
            slotObject.transform,
            "Без названия",
            23,
            TextAlignmentOptions.Left,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(220f, 55f),
            new Vector2(410f, 30f));
        view.sceneNameText.color = PrimaryText;
        view.sceneNameText.fontStyle = FontStyles.Bold;
        view.sceneNameText.overflowMode = TextOverflowModes.Ellipsis;

        view.slotNumberText = CreateText(
            "Slot Number",
            slotObject.transform,
            $"Слот {slotIndex}",
            17,
            TextAlignmentOptions.Left,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(72f, 21f),
            new Vector2(112f, 24f));
        view.slotNumberText.color = new Color(0.54f, 0.65f, 0.78f, 0.9f);
        view.slotNumberText.fontStyle = FontStyles.Bold;

        view.dateText = CreateText(
            "Date",
            slotObject.transform,
            string.Empty,
            16,
            TextAlignmentOptions.Right,
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-130f, 21f),
            new Vector2(170f, 24f));
        view.dateText.color = new Color(0.5f, 0.58f, 0.67f, 0.82f);

        view.deleteButton = CreateDeleteButton(view);
        return view;
    }

    private static Button CreateDeleteButton(ManualSaveSlotView view)
    {
        Button deleteButton = CreateButton(
            "Delete Button",
            view.transform,
            "×",
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-21f, 21f),
            new Vector2(30f, 30f));
        StyleDeleteButton(deleteButton);

        TextMeshProUGUI label = deleteButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.fontSize = 21f;
            label.color = new Color(0.72f, 0.76f, 0.82f, 0.92f);
        }

        deleteButton.transform.SetAsLastSibling();
        return deleteButton;
    }

    private static void BuildConfirmation(ManualSaveLoadPanel panel, Transform parent)
    {
        GameObject confirmation = CreateUiObject("Save Confirmation", parent);
        Stretch(confirmation.GetComponent<RectTransform>());
        Image dim = confirmation.AddComponent<Image>();
        dim.color = new Color(0.004f, 0.007f, 0.016f, 0.82f);
        dim.raycastTarget = true;
        CanvasGroup confirmationGroup = confirmation.AddComponent<CanvasGroup>();

        GameObject window = CreateUiObject("Confirmation Window", confirmation.transform);
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(760f, 300f);
        Image windowImage = window.AddComponent<Image>();
        windowImage.color = Color.white;
        UiVerticalGradient confirmationGradient = window.AddComponent<UiVerticalGradient>();
        confirmationGradient.topColor = new Color(0.055f, 0.085f, 0.13f, 0.98f);
        confirmationGradient.bottomColor = new Color(0.025f, 0.043f, 0.07f, 0.98f);
        Shadow shadow = window.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(0f, -10f);
        Outline outline = window.AddComponent<Outline>();
        outline.effectColor = new Color(0.2f, 0.34f, 0.48f, 0.52f);
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject accent = CreateUiObject("Confirmation Accent", window.transform);
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = Vector2.one;
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.sizeDelta = new Vector2(0f, 2f);
        Image accentImage = accent.AddComponent<Image>();
        accentImage.color = new Color(MutedRed.r, MutedRed.g, MutedRed.b, 0.68f);
        accentImage.raycastTarget = false;

        panel.confirmationText = CreateText(
            "Confirmation Text",
            window.transform,
            "Перезаписать слот?",
            30,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -78f),
            new Vector2(680f, 96f));
        panel.confirmationText.color = PrimaryText;
        panel.confirmationText.enableWordWrapping = true;
        panel.confirmationText.overflowMode = TextOverflowModes.Overflow;

        panel.confirmationYesButton = CreateButton(
            "Yes Button",
            window.transform,
            "Перезаписать",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(-125f, 62f),
            new Vector2(210f, 62f));
        StyleButton(panel.confirmationYesButton, new Color(0.34f, 0.075f, 0.105f, 1f), MutedRed);

        panel.confirmationNoButton = CreateButton(
            "No Button",
            window.transform,
            "Отмена",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(125f, 62f),
            new Vector2(210f, 62f));
        StyleButton(panel.confirmationNoButton, new Color(0.075f, 0.11f, 0.17f, 1f), ColdAccent);

        panel.confirmationRoot = confirmation;
        panel.confirmationCanvasGroup = confirmationGroup;
        panel.confirmationWindow = windowRect;
        confirmation.SetActive(false);
    }

    private static ManualSaveLoadPanel InstantiatePanel(GameObject prefab, Transform canvas)
    {
        ManualSaveLoadPanel[] existingPanels = canvas.GetComponentsInChildren<ManualSaveLoadPanel>(true);
        ManualSaveLoadPanel matchingPanel = existingPanels.FirstOrDefault(panel =>
            string.Equals(
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(panel.gameObject),
                PrefabPath,
                StringComparison.Ordinal));

        foreach (ManualSaveLoadPanel existingPanel in existingPanels)
        {
            if (existingPanel != matchingPanel)
            {
                UnityEngine.Object.DestroyImmediate(existingPanel.gameObject);
            }
        }

        if (matchingPanel != null)
        {
            matchingPanel.gameObject.name = "Manual Save Load Panel";
            matchingPanel.gameObject.SetActive(false);
            return matchingPanel;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, canvas) as GameObject;
        if (instance == null)
        {
            throw new InvalidOperationException("ManualSaveLoadPanel prefab could not be instantiated.");
        }

        instance.name = "Manual Save Load Panel";
        instance.transform.SetAsLastSibling();
        ManualSaveLoadPanel panel = instance.GetComponent<ManualSaveLoadPanel>();
        instance.SetActive(false);
        return panel;
    }

    private static SaveManager EnsureSceneSaveManager(Scene scene, DialogueSceneRegistry registry)
    {
        SaveManager manager = FindInScene<SaveManager>(scene);
        if (manager == null)
        {
            Transform managers = FindTransform(scene, "Managers");
            GameObject owner = managers != null ? managers.gameObject : new GameObject("SaveManager");
            manager = owner.AddComponent<SaveManager>();
        }

        manager.ConfigureRegistry(registry);
        return manager;
    }

    private static void RemoveOldSaveObjects(Scene scene)
    {
        string[] oldNames =
        {
            "Main Menu SaveLoad Panel",
            "VN SaveLoad Panel"
        };

        foreach (string objectName in oldNames)
        {
            Transform existing;
            while ((existing = FindTransform(scene, objectName)) != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
        }
    }

    private static Button ReplaceButton(Scene scene, string oldName, string newName, string newLabel)
    {
        Transform oldTransform = FindTransform(scene, oldName);
        if (oldTransform == null)
        {
            Transform existing = FindTransform(scene, newName);
            if (existing != null && existing.TryGetComponent(out Button existingButton))
            {
                existingButton.onClick = new Button.ButtonClickedEvent();
                SetButtonLabel(existingButton, newLabel);
                return existingButton;
            }

            throw new InvalidOperationException($"Button '{oldName}' was not found in scene '{scene.path}'.");
        }

        Transform parent = oldTransform.parent;
        int siblingIndex = oldTransform.GetSiblingIndex();
        GameObject replacement = UnityEngine.Object.Instantiate(oldTransform.gameObject, parent);
        replacement.name = newName;
        replacement.transform.SetSiblingIndex(siblingIndex);
        Button button = replacement.GetComponent<Button>();
        if (button == null)
        {
            throw new InvalidOperationException($"Object '{oldName}' has no Button component.");
        }

        button.onClick = new Button.ButtonClickedEvent();
        SetButtonLabel(button, newLabel);
        UnityEngine.Object.DestroyImmediate(oldTransform.gameObject);
        return button;
    }

    private static void SetButtonLabel(Button button, string text)
    {
        TextMeshProUGUI tmp = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null)
        {
            tmp.text = text;
        }

        Text legacy = button.GetComponentInChildren<Text>(true);
        if (legacy != null)
        {
            legacy.text = text;
        }
    }

    private static void CleanSceneReferences(Scene scene)
    {
        string[] installerOwnedButtonNames =
        {
            "Continue Button",
            "Load Button",
            "Save Button",
            "Продолжить Button",
            "Загрузить Button",
            "Сохр. Button",
            "Загр. Button"
        };

        foreach (string buttonName in installerOwnedButtonNames)
        {
            Transform transform = FindTransform(scene, buttonName);
            if (transform != null && transform.TryGetComponent(out Button button))
            {
                button.onClick = new Button.ButtonClickedEvent();
                EditorUtility.SetDirty(button);
            }
        }
    }

    private static void ValidateScene(Scene scene, bool expectVnController)
    {
        int missingScriptCount = 0;
        int invalidEventCount = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                missingScriptCount += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);

                foreach (Button button in transform.GetComponents<Button>())
                {
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    {
                        UnityEngine.Object target = button.onClick.GetPersistentTarget(i);
                        string method = button.onClick.GetPersistentMethodName(i);
                        bool methodExists = target != null
                            && !string.IsNullOrEmpty(method)
                            && target.GetType().GetMethod(
                                method,
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null;

                        if (!methodExists)
                        {
                            invalidEventCount++;
                        }
                    }
                }
            }
        }

        ManualSaveLoadPanel[] panels = FindAllInScene<ManualSaveLoadPanel>(scene);
        SaveManager[] managers = FindAllInScene<SaveManager>(scene);
        if (missingScriptCount != 0 || invalidEventCount != 0)
        {
            throw new InvalidOperationException($"Scene '{scene.path}' contains missing scripts={missingScriptCount}, invalid events={invalidEventCount}.");
        }

        if (panels.Length != 1
            || panels[0].visualVersion != PanelVisualVersion
            || panels[0].slotViews == null
            || panels[0].slotViews.Length != SaveManager.SlotCount)
        {
            throw new InvalidOperationException($"Scene '{scene.path}' must contain one current six-slot ManualSaveLoadPanel.");
        }

        ManualSaveLoadPanel panel = panels[0];
        string panelPrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(panel.gameObject);
        if (!string.Equals(panelPrefabPath, PrefabPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Scene '{scene.path}' uses '{panelPrefabPath}' instead of common prefab '{PrefabPath}'.");
        }

        if (panel.canvasGroup == null
            || panel.contentCanvasGroup == null
             || panel.windowRect == null
             || panel.titleText == null
             || panel.subtitleText == null
             || panel.slotTypeHintText == null
             || panel.manualTabButton == null
             || panel.autoTabButton == null
             || panel.quickTabButton == null
             || panel.statusText == null
            || panel.statusCanvasGroup == null
            || panel.closeButton == null
            || panel.confirmationRoot == null
            || panel.confirmationCanvasGroup == null
            || panel.confirmationWindow == null
            || panel.confirmationText == null
            || panel.confirmationYesButton == null
            || panel.confirmationNoButton == null)
        {
            throw new InvalidOperationException($"Scene '{scene.path}' contains a manual save panel with missing mandatory references.");
        }

        if (panel.slotViews.Any(view => !HasRequiredSlotReferences(view)))
        {
            throw new InvalidOperationException($"Scene '{scene.path}' contains a manual save card with missing mandatory references.");
        }

        if (managers.Length != 1)
        {
            throw new InvalidOperationException($"Scene '{scene.path}' must contain exactly one SaveManager component, found {managers.Length}.");
        }

        if (expectVnController && FindInScene<VNDialogueController>(scene) == null)
        {
            throw new InvalidOperationException("VNPrototype has no VNDialogueController.");
        }

        Debug.Log($"[SAVE INSTALL] VALID '{scene.path}' prefab='{PrefabPath}' missingScripts=0 invalidEvents=0 managers=1 panels=1 slots=6.");
    }

    private static bool HasRequiredSlotReferences(ManualSaveSlotView view)
    {
        return view != null
            && view.button != null
            && view.cardRect != null
            && view.backgroundImage != null
            && view.hoverAccentImage != null
            && view.occupiedAccentImage != null
            && view.previewFrameImage != null
            && view.placeholderOverlayImage != null
            && view.cardOutline != null
            && view.previewImage != null
            && view.slotNumberText != null
            && view.sceneNameText != null
            && view.dateText != null
            && view.emptyText != null
            && view.backgroundSlotNumberText != null
            && view.deleteButton != null;
    }

    private static void CreateDecorativeShape(
        string name,
        Transform parent,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject shape = CreateUiObject(name, parent);
        RectTransform rect = shape.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        Image image = shape.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        if (parent != null)
        {
            gameObject.transform.SetParent(parent, false);
        }

        return gameObject;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        string text,
        int fontSize,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        GameObject textObject = CreateUiObject(name, parent);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = Color.white;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        GameObject buttonObject = CreateUiObject(name, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.075f, 0.11f, 0.17f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI buttonText = CreateText(
            "Label",
            buttonObject.transform,
            label,
            23,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero);
        Stretch(buttonText.rectTransform);
        buttonText.color = PrimaryText;
        return button;
    }

    private static void StyleButton(Button button, Color normal, Color accent)
    {
        if (button == null || !(button.targetGraphic is Image image))
        {
            return;
        }

        image.color = normal;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(
            Mathf.Lerp(1f, accent.r, 0.2f),
            Mathf.Lerp(1f, accent.g, 0.2f),
            Mathf.Lerp(1f, accent.b, 0.2f),
            1f);
        colors.pressedColor = new Color(0.78f, 0.82f, 0.9f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.45f, 0.48f, 0.55f, 0.5f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        Outline outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(accent.r, accent.g, accent.b, 0.7f);
        outline.effectDistance = new Vector2(1f, -1f);
    }

    private static void StyleNavigationButton(Button button)
    {
        if (button == null || !(button.targetGraphic is Image image))
        {
            return;
        }

        image.color = Color.white;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.045f, 0.075f, 0.11f, 0.48f);
        colors.highlightedColor = new Color(0.09f, 0.15f, 0.22f, 0.76f);
        colors.pressedColor = new Color(0.11f, 0.18f, 0.26f, 0.9f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.04f, 0.055f, 0.075f, 0.28f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.12f;
        button.colors = colors;

        Outline outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.28f, 0.47f, 0.64f, 0.46f);
        outline.effectDistance = new Vector2(1f, -1f);
    }

    private static void StyleDeleteButton(Button button)
    {
        if (button == null || !(button.targetGraphic is Image image))
        {
            return;
        }

        image.color = Color.white;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.055f, 0.068f, 0.085f, 0.78f);
        colors.highlightedColor = new Color(0.34f, 0.085f, 0.115f, 0.92f);
        colors.pressedColor = new Color(0.48f, 0.09f, 0.13f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.04f, 0.048f, 0.06f, 0.4f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.1f;
        button.colors = colors;

        Outline outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.22f, 0.27f, 0.33f, 0.34f);
        outline.effectDistance = new Vector2(1f, -1f);
    }

    private static void StyleSlotTypeTab(Button button, bool active)
    {
        if (button == null || !(button.targetGraphic is Image image))
        {
            return;
        }

        image.color = active
            ? new Color(0.075f, 0.145f, 0.22f, 0.96f)
            : new Color(0.032f, 0.055f, 0.085f, 0.72f);
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.16f, 1.16f, 1.16f, 1f);
        colors.pressedColor = new Color(0.86f, 0.9f, 0.96f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.45f, 0.48f, 0.55f, 0.5f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.1f;
        button.colors = colors;

        Outline outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = active
            ? new Color(0.28f, 0.54f, 0.76f, 0.62f)
            : new Color(0.16f, 0.25f, 0.34f, 0.34f);
        outline.effectDistance = new Vector2(1f, -1f);

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            text.fontSize = 17f;
            text.fontStyle = FontStyles.Bold;
            text.characterSpacing = 3f;
            text.color = active
                ? new Color(0.88f, 0.95f, 1f, 1f)
                : new Color(0.48f, 0.59f, 0.7f, 0.82f);
        }
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureAssetFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        return FindAllInScene<T>(scene).FirstOrDefault();
    }

    private static T[] FindAllInScene<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .ToArray();
    }

    private static Transform FindTransform(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform match = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform => transform.name == name);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}
