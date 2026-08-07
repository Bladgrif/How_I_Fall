using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class VNQuickMenuSceneInstaller
{
    private const string VnScenePath = "Assets/HowIFall/Scenes/VNPrototype.unity";
    private static readonly Color ButtonColor = new Color(0.035f, 0.07f, 0.11f, 0.82f);
    private static readonly Color TextColor = new Color(0.84f, 0.91f, 0.97f, 1f);

    [MenuItem("How I Fall/VN/Install Quick Menu")]
    public static void InstallFromMenu()
    {
        Install();
        Debug.Log("[QUICK MENU] Installed.");
    }

    public static void RunBatchMode()
    {
        Install();
        Debug.Log("[QUICK MENU] BATCH COMPLETE");
    }

    private static void Install()
    {
        Scene scene = EditorSceneManager.OpenScene(VnScenePath, OpenSceneMode.Single);
        Canvas canvas = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).FirstOrDefault();
        VNDialogueController controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<VNDialogueController>(true)).FirstOrDefault();
        if (canvas == null || controller == null)
        {
            throw new InvalidOperationException("VNPrototype must contain Canvas and VNDialogueController.");
        }

        Transform legacyRoot = canvas.transform.Find("VN Quick Menu");
        if (legacyRoot != null)
        {
            UnityEngine.Object.DestroyImmediate(legacyRoot.gameObject);
        }

        VNQuickMenu existing = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<VNQuickMenu>(true)).FirstOrDefault();
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        GameObject root = CreateUiObject("Quick Menu", canvas.transform);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0f);
        rootRect.anchorMax = new Vector2(0.5f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.anchoredPosition = new Vector2(0f, 22f);
        rootRect.sizeDelta = new Vector2(1180f, 38f);
        HorizontalLayoutGroup layout = root.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 5f;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        VNQuickMenu menu = root.AddComponent<VNQuickMenu>();
        menu.dialogueController = controller;
        menu.root = root;
        menu.historyButton = CreateButton(root.transform, "History Button", "История", 108f);
        menu.skipButton = CreateButton(root.transform, "Skip Button", "Пропуск", 94f);
        menu.autoButton = CreateButton(root.transform, "Auto Button", "Авто", 72f);
        menu.saveButton = CreateButton(root.transform, "Save Button", "Сохранить", 112f);
        menu.quickSaveButton = CreateButton(root.transform, "Quick Save Button", "Быстр. сохр.", 125f);
        menu.quickLoadButton = CreateButton(root.transform, "Quick Load Button", "Быстр. загр.", 125f);
        menu.loadButton = CreateButton(root.transform, "Load Button", "Загрузить", 112f);
        menu.settingsButton = CreateButton(root.transform, "Settings Button", "Настройки", 112f);
        menu.mainMenuButton = CreateButton(root.transform, "Main Menu Button", "Меню", 72f);

        // Keep the facade above the dialogue/background but below every existing modal overlay.
        root.transform.SetSiblingIndex(GetModalSiblingIndex(canvas.transform, controller));
        EditorUtility.SetDirty(menu);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static int GetModalSiblingIndex(Transform canvasTransform, VNDialogueController controller)
    {
        int firstModalSibling = canvasTransform.childCount;
        RegisterModalSibling(controller.choiceDimOverlay, canvasTransform, ref firstModalSibling);
        RegisterModalSibling(controller.backlogDimOverlay, canvasTransform, ref firstModalSibling);
        RegisterModalSibling(controller.vnSettingsDimOverlay, canvasTransform, ref firstModalSibling);
        RegisterModalSibling(controller.confirmExitPanel, canvasTransform, ref firstModalSibling);
        RegisterModalSibling(controller.manualSaveLoadPanel != null ? controller.manualSaveLoadPanel.gameObject : null, canvasTransform, ref firstModalSibling);
        return firstModalSibling;
    }

    private static void RegisterModalSibling(GameObject modal, Transform canvasTransform, ref int firstModalSibling)
    {
        if (modal != null && modal.transform.parent == canvasTransform)
        {
            firstModalSibling = Mathf.Min(firstModalSibling, modal.transform.GetSiblingIndex());
        }
    }

    private static Button CreateButton(Transform parent, string name, string label, float width)
    {
        GameObject buttonObject = CreateUiObject(name, parent);
        LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = width;
        Image image = buttonObject.AddComponent<Image>();
        image.color = ButtonColor;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.25f, 0.48f, 0.66f, 0.45f);
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject textObject = CreateUiObject("Label", buttonObject.transform);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        text.text = label;
        text.fontSize = 16;
        text.alignment = TextAlignmentOptions.Center;
        text.color = TextColor;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return button;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }
}
