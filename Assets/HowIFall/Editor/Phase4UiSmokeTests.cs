using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class Phase4UiSmokeTests
{
    private const string MainMenuScenePath = "Assets/HowIFall/Scenes/MainMenu.unity";

    [MenuItem("How I Fall/Tests/Run Phase 4 UI Smoke Tests")]
    public static void RunFromMenu()
    {
        RunBatchMode();
        Debug.Log("How I Fall Phase 4 UI smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        VerifyMainMenuCleanup();
        VerifyDialogueSafeAreaConsumer();
        VerifySaveIsolation();
    }

    private static void VerifyMainMenuCleanup()
    {
        EditorSceneManager.OpenScene(MainMenuScenePath);
        MainMenuController controller = UnityEngine.Object.FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
        Require(controller != null, "MainMenu must contain MainMenuController.");
        Require(controller.ApplyPlayerFacingPresentation(), "Main Menu Phase 4 runtime wiring could not be applied.");

        Button[] buttons = controller.PlayerFacingActionButtons.ToArray();
        Require(buttons.Length == 5, "Main Menu must expose exactly five player-facing actions.");
        string[] labels = buttons.Select(GetButtonLabel).ToArray();
        string[] expectedLabels = { "Продолжить", "Новая игра", "Загрузить", "Настройки", "Выйти" };
        Require(labels.SequenceEqual(expectedLabels),
            "Main Menu order must be Continue / New Game / Load / Preferences / Quit. Actual: "
            + string.Join(" / ", labels));
        Require(controller.continueButton == buttons[0], "Continue must be the first action and keep its existing button wiring.");
        Require(typeof(MainMenuController).GetMethod(nameof(MainMenuController.RefreshContinueAvailability)) != null,
            "Continue truthful availability refresh was removed.");
        Require(controller.settingsPanel != null && typeof(IPreferencesView).IsAssignableFrom(controller.settingsPanel.GetType()),
            "Main Menu Preferences must use the existing shared Preferences view route.");
        Require(buttons.All(button => button.onClick.GetPersistentEventCount() > 0),
            "A Main Menu action lost its existing persistent route.");
        Require(labels.All(label => label != "Галерея" && label != "Gallery"),
            "Gallery leaked into the top-level Main Menu action set.");

        GameObject aboutPanel = GetPrivate<GameObject>(controller, "aboutPanel");
        GameObject helpPanel = GetPrivate<GameObject>(controller, "helpPanel");
        GameObject exitConfirmPanel = GetPrivate<GameObject>(controller, "exitConfirmPanel");
        Require(helpPanel != null && aboutPanel != null, "Help/About technical panels must remain available.");
        Require(exitConfirmPanel != null, "Quit confirmation must remain preserved.");
        Require(!FindButtonWithRoute(controller, nameof(MainMenuController.OpenHelp)).gameObject.activeInHierarchy
            && !FindButtonWithRoute(controller, nameof(MainMenuController.OpenAbout)).gameObject.activeInHierarchy,
            "Help/About must not remain player-facing Main Menu entries.");

        Transform commonContent = buttons[0].transform.parent.parent;
        Require(buttons.All(button => button.transform.parent.parent == commonContent),
            "Main Menu cleanup must reuse the authored action hierarchy instead of creating a second shell.");
        Require(commonContent.GetComponentInParent<Canvas>() != null,
            "Main Menu runtime presentation lost the authored full-screen Canvas/background context.");
    }

    private static void VerifyDialogueSafeAreaConsumer()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SettingsManager manager = GetSettingsManager();
        GameSettings previousSettings = manager.settings;
        GameObject canvas = new GameObject("Phase 4 Safe Area Canvas", typeof(RectTransform));
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1920f, 1080f);
        GameObject vnRoot = CreateRect(canvas.transform, "VN Root");
        Stretch(vnRoot.GetComponent<RectTransform>());
        GameObject dialogue = CreateRect(vnRoot.transform, "Dialogue Box");
        RectTransform dialogueRect = dialogue.GetComponent<RectTransform>();
        dialogueRect.anchorMin = new Vector2(0f, 0f);
        dialogueRect.anchorMax = new Vector2(1f, 0f);
        dialogueRect.pivot = new Vector2(0.5f, 0f);
        dialogueRect.anchoredPosition = new Vector2(0f, 92f);
        dialogueRect.sizeDelta = new Vector2(-440f, 180f);

        GameObject dialogueTextObject = CreateRect(dialogue.transform, "Dialogue Text");
        TextMeshProUGUI dialogueText = dialogueTextObject.AddComponent<TextMeshProUGUI>();
        dialogueText.text = "SAFE AREA MUST NOT MUTATE DIALOGUE";

        GameObject quickRoot = CreateRect(canvas.transform, "Quick Menu");
        RectTransform quickRect = quickRoot.GetComponent<RectTransform>();
        quickRect.anchorMin = quickRect.anchorMax = new Vector2(0.5f, 0f);
        quickRect.pivot = new Vector2(0.5f, 0f);
        quickRect.anchoredPosition = new Vector2(0f, 22f);
        quickRect.sizeDelta = new Vector2(1180f, 38f);

        GameObject controllerObject = new GameObject("Phase 4 Dialogue Controller");
        VNDialogueController dialogueController = controllerObject.AddComponent<VNDialogueController>();
        dialogueController.dialogueUiRoot = dialogue;
        dialogueController.dialogueText = dialogueText;
        GameObject quickOwner = new GameObject("Phase 4 Quick Menu Owner");
        VNQuickMenu quickMenu = quickOwner.AddComponent<VNQuickMenu>();
        quickMenu.dialogueController = dialogueController;
        quickMenu.root = quickRoot;

        try
        {
            manager.settings = new GameSettings { showQuickMenu = true };
            quickMenu.RefreshEffectiveVisibility();
            float visibleReserve = quickMenu.QuickMenuSafeAreaReserve;
            float visibleY = dialogueRect.anchoredPosition.y;
            Require(visibleReserve > 0f && quickRoot.activeSelf,
                "Quick Menu visible state must publish a measured positive dialogue reserve.");

            manager.SetShowQuickMenu(false);
            quickMenu.RefreshEffectiveVisibility();
            Require(Mathf.Approximately(quickMenu.QuickMenuSafeAreaReserve, 0f) && !quickRoot.activeSelf,
                "Persistently hidden Quick Menu must publish zero dialogue reserve.");
            Require(dialogueRect.anchoredPosition.y < visibleY,
                "Dialogue shell did not reclaim the bottom space when Quick Menu was hidden.");

            manager.SetShowQuickMenu(true);
            quickRect.sizeDelta = new Vector2(1180f, 54f);
            quickMenu.RefreshEffectiveVisibility();
            Require(quickMenu.QuickMenuSafeAreaReserve > visibleReserve,
                "Safe-area reserve did not react to a Quick Menu layout measurement change.");

            quickMenu.SetPreferencesModalHidden(true);
            Require(Mathf.Approximately(quickMenu.QuickMenuSafeAreaReserve, 0f),
                "Preferences blocker did not release the Quick Menu dialogue reserve.");
            Require(dialogueText.text == "SAFE AREA MUST NOT MUTATE DIALOGUE",
                "Safe-area updates mutated dialogue content/state.");
        }
        finally
        {
            manager.settings = previousSettings;
            UnityEngine.Object.DestroyImmediate(quickOwner);
            UnityEngine.Object.DestroyImmediate(controllerObject);
            UnityEngine.Object.DestroyImmediate(canvas);
        }
    }

    private static void VerifySaveIsolation()
    {
        Require(SaveData.CurrentVersion == 3, "Phase 4 must preserve SaveData.CurrentVersion == 3.");
        string json = JsonUtility.ToJson(new SaveData());
        Require(json.IndexOf("showQuickMenu", StringComparison.OrdinalIgnoreCase) < 0
            && json.IndexOf("safeArea", StringComparison.OrdinalIgnoreCase) < 0,
            "B03 or Quick Menu layout state leaked into campaign SaveData JSON.");
    }

    private static SettingsManager GetSettingsManager()
    {
        if (SettingsManager.Instance != null)
        {
            return SettingsManager.Instance;
        }

        SettingsManager manager = new GameObject("Phase 4 Settings Manager").AddComponent<SettingsManager>();
        typeof(SettingsManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, manager);
        return manager;
    }

    private static T GetPrivate<T>(object owner, string fieldName) where T : class
    {
        return typeof(MainMenuController).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner) as T;
    }

    private static string GetButtonLabel(Button button)
    {
        TextMeshProUGUI label = button.GetComponentsInChildren<TextMeshProUGUI>(true)
            .FirstOrDefault(text => text.gameObject.name == "Text")
            ?? button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            return label.text;
        }

        Text legacyLabel = button.GetComponentsInChildren<Text>(true)
            .FirstOrDefault(text => text.gameObject.name == "Text")
            ?? button.GetComponentInChildren<Text>(true);
        return legacyLabel != null ? legacyLabel.text : string.Empty;
    }

    private static Button FindButtonWithRoute(MainMenuController controller, string methodName)
    {
        return UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(button => Enumerable.Range(0, button.onClick.GetPersistentEventCount())
                .Any(index => button.onClick.GetPersistentTarget(index) == controller
                    && button.onClick.GetPersistentMethodName(index) == methodName));
    }

    private static GameObject CreateRect(Transform parent, string name)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
