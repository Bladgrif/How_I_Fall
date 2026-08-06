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

    [MenuItem("How I Fall/Save System/Install Clean Manual Save UI")]
    public static void InstallFromMenu()
    {
        Install();
        Debug.Log("[SAVE INSTALL] Clean manual Save/Load system installed.");
    }

    public static void RunBatchMode()
    {
        Install();
        Debug.Log("[SAVE INSTALL] BATCH COMPLETE");
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

        Canvas canvas = FindInScene<Canvas>(scene);
        MainMenuController controller = FindInScene<MainMenuController>(scene);
        if (canvas == null || controller == null)
        {
            throw new InvalidOperationException("MainMenu must contain Canvas and MainMenuController.");
        }

        ManualSaveLoadPanel panel = InstantiatePanel(panelPrefab, canvas.transform);
        SaveManager saveManager = EnsureSceneSaveManager(scene, registry);

        Button continueButton = ReplaceButton(
            scene,
            "Продолжить Button",
            "Continue Button",
            "Продолжить");
        UnityEventTools.AddPersistentListener(continueButton.onClick, controller.ContinueFromLatestSave);

        Button loadButton = ReplaceButton(
            scene,
            "Загрузить Button",
            "Load Button",
            "Загрузить");
        UnityEventTools.AddPersistentListener(loadButton.onClick, panel.OpenLoad);

        controller.manualSaveLoadPanel = panel;
        controller.dialogueRegistry = registry;
        controller.continueButton = continueButton;
        saveManager.ConfigureRegistry(registry);

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(saveManager);
        CleanSceneReferences(scene);
        ValidateScene(scene, false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void InstallVnScene(GameObject panelPrefab, DialogueSceneRegistry registry)
    {
        Scene scene = EditorSceneManager.OpenScene(VnScenePath, OpenSceneMode.Single);
        RemoveOldSaveObjects(scene);

        Canvas canvas = FindInScene<Canvas>(scene);
        VNDialogueController controller = FindInScene<VNDialogueController>(scene);
        if (canvas == null || controller == null)
        {
            throw new InvalidOperationException("VNPrototype must contain Canvas and VNDialogueController.");
        }

        ManualSaveLoadPanel panel = InstantiatePanel(panelPrefab, canvas.transform);
        SaveManager saveManager = EnsureSceneSaveManager(scene, registry);

        Button saveButton = ReplaceButton(
            scene,
            "Сохр. Button",
            "Save Button",
            "Сохр.");
        UnityEventTools.AddPersistentListener(saveButton.onClick, panel.OpenSave);

        Button loadButton = ReplaceButton(
            scene,
            "Загр. Button",
            "Load Button",
            "Загр.");
        UnityEventTools.AddPersistentListener(loadButton.onClick, panel.OpenLoad);

        controller.manualSaveLoadPanel = panel;
        saveManager.ConfigureRegistry(registry);

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(saveManager);
        CleanSceneReferences(scene);
        ValidateScene(scene, true);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static GameObject BuildPanelPrefab()
    {
        EnsureAssetFolder("Assets/HowIFall", "Prefabs");
        EnsureAssetFolder("Assets/HowIFall/Prefabs", "UI");

        GameObject root = CreateUiObject("Manual Save Load Panel", null);
        try
        {
            Stretch(root.GetComponent<RectTransform>());
            Image rootImage = root.AddComponent<Image>();
            rootImage.color = new Color(0.015f, 0.025f, 0.05f, 0.93f);
            rootImage.raycastTarget = true;

            CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
            ManualSaveLoadPanel panel = root.AddComponent<ManualSaveLoadPanel>();
            panel.canvasGroup = canvasGroup;

            GameObject window = CreateUiObject("Window", root.transform);
            RectTransform windowRect = window.GetComponent<RectTransform>();
            windowRect.anchorMin = windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.sizeDelta = new Vector2(1500f, 840f);
            Image windowImage = window.AddComponent<Image>();
            windowImage.color = new Color(0.045f, 0.065f, 0.105f, 0.98f);
            Outline outline = window.AddComponent<Outline>();
            outline.effectColor = new Color(0.42f, 0.52f, 0.72f, 0.75f);
            outline.effectDistance = new Vector2(2f, -2f);

            panel.titleText = CreateText(
                "Title",
                window.transform,
                "Загрузка",
                44,
                TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -42f),
                new Vector2(700f, 70f));

            panel.closeButton = CreateButton(
                "Close Button",
                window.transform,
                "Закрыть",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-105f, -42f),
                new Vector2(170f, 58f));

            GameObject gridObject = CreateUiObject("Slots Grid", window.transform);
            RectTransform gridRect = gridObject.GetComponent<RectTransform>();
            gridRect.anchorMin = gridRect.anchorMax = new Vector2(0.5f, 0.5f);
            gridRect.anchoredPosition = new Vector2(0f, -20f);
            gridRect.sizeDelta = new Vector2(1320f, 620f);
            GridLayoutGroup grid = gridObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(420f, 290f);
            grid.spacing = new Vector2(30f, 26f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.MiddleCenter;

            var slotViews = new ManualSaveSlotView[SaveManager.SlotCount];
            for (int i = 0; i < slotViews.Length; i++)
            {
                slotViews[i] = CreateSlotView(gridObject.transform, i + 1);
            }

            panel.slotViews = slotViews;
            panel.statusText = CreateText(
                "Status",
                window.transform,
                string.Empty,
                24,
                TextAlignmentOptions.Center,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 28f),
                new Vector2(1000f, 44f));

            BuildConfirmation(panel, root.transform);

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

    private static ManualSaveSlotView CreateSlotView(Transform parent, int slotIndex)
    {
        GameObject slotObject = CreateUiObject($"Manual Slot {slotIndex}", parent);
        Image background = slotObject.AddComponent<Image>();
        background.color = new Color(0.08f, 0.105f, 0.16f, 1f);
        Outline outline = slotObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.25f, 0.34f, 0.52f, 0.9f);
        outline.effectDistance = new Vector2(1f, -1f);

        Button button = slotObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.88f, 0.93f, 1f, 1f);
        colors.pressedColor = new Color(0.72f, 0.82f, 1f, 1f);
        colors.disabledColor = new Color(0.55f, 0.55f, 0.6f, 0.45f);
        button.colors = colors;

        ManualSaveSlotView view = slotObject.AddComponent<ManualSaveSlotView>();
        view.button = button;

        GameObject previewObject = CreateUiObject("Preview", slotObject.transform);
        RectTransform previewRect = previewObject.GetComponent<RectTransform>();
        previewRect.anchorMin = new Vector2(0f, 0.28f);
        previewRect.anchorMax = new Vector2(1f, 1f);
        previewRect.offsetMin = new Vector2(12f, 0f);
        previewRect.offsetMax = new Vector2(-12f, -12f);
        view.previewImage = previewObject.AddComponent<Image>();
        view.previewImage.color = new Color(0.03f, 0.04f, 0.07f, 1f);
        view.previewImage.raycastTarget = false;

        view.slotNumberText = CreateText(
            "Slot Number",
            slotObject.transform,
            $"Слот {slotIndex}",
            25,
            TextAlignmentOptions.Left,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(90f, 54f),
            new Vector2(160f, 40f));

        view.dateText = CreateText(
            "Date",
            slotObject.transform,
            string.Empty,
            21,
            TextAlignmentOptions.Right,
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-130f, 54f),
            new Vector2(230f, 40f));

        view.emptyText = CreateText(
            "Empty",
            previewObject.transform,
            "Пустой слот",
            26,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(320f, 60f));

        return view;
    }

    private static void BuildConfirmation(ManualSaveLoadPanel panel, Transform parent)
    {
        GameObject confirmation = CreateUiObject("Overwrite Confirmation", parent);
        Stretch(confirmation.GetComponent<RectTransform>());
        Image dim = confirmation.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.78f);
        dim.raycastTarget = true;

        GameObject window = CreateUiObject("Confirmation Window", confirmation.transform);
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(620f, 260f);
        Image windowImage = window.AddComponent<Image>();
        windowImage.color = new Color(0.065f, 0.085f, 0.135f, 1f);

        panel.confirmationText = CreateText(
            "Confirmation Text",
            window.transform,
            "Перезаписать слот?",
            30,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -72f),
            new Vector2(540f, 70f));

        panel.confirmationYesButton = CreateButton(
            "Yes Button",
            window.transform,
            "Да",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(-125f, 58f),
            new Vector2(190f, 64f));

        panel.confirmationNoButton = CreateButton(
            "No Button",
            window.transform,
            "Нет",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(125f, 58f),
            new Vector2(190f, 64f));

        panel.confirmationRoot = confirmation;
        confirmation.SetActive(false);
    }

    private static ManualSaveLoadPanel InstantiatePanel(GameObject prefab, Transform canvas)
    {
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
            "VN SaveLoad Panel",
            "Manual Save Load Panel"
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

    private static Button ReplaceButton(
        Scene scene,
        string oldName,
        string newName,
        string newLabel)
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
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);

                foreach (Button button in transform.GetComponents<Button>())
                {
                    for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
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
                            UnityEventTools.RemovePersistentListener(button.onClick, i);
                        }
                    }
                }
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
                        if (button.onClick.GetPersistentTarget(i) == null
                            || string.IsNullOrEmpty(button.onClick.GetPersistentMethodName(i)))
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

        if (panels.Length != 1 || panels[0].slotViews == null || panels[0].slotViews.Length != SaveManager.SlotCount)
        {
            throw new InvalidOperationException($"Scene '{scene.path}' must contain one six-slot ManualSaveLoadPanel.");
        }

        if (managers.Length != 1)
        {
            throw new InvalidOperationException($"Scene '{scene.path}' must contain exactly one SaveManager component, found {managers.Length}.");
        }

        if (expectVnController && FindInScene<VNDialogueController>(scene) == null)
        {
            throw new InvalidOperationException("VNPrototype has no VNDialogueController.");
        }

        Debug.Log($"[SAVE INSTALL] VALID '{scene.path}' missingScripts=0 invalidEvents=0 managers=1 panels=1 slots=6.");
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
        image.color = new Color(0.16f, 0.22f, 0.34f, 1f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI buttonText = CreateText(
            "Label",
            buttonObject.transform,
            label,
            25,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero);
        Stretch(buttonText.rectTransform);
        return button;
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
