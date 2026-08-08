using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HideUiSmokeTests
{
    private const string VnScenePath = "Assets/HowIFall/Scenes/VNPrototype.unity";

    [MenuItem("How I Fall/Tests/Run Hide UI Smoke Tests")]
    public static void RunFromMenu()
    {
        RunBatchMode();
        Debug.Log("How I Fall Hide UI smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        VerifySceneCleanViewRoot();
        VerifyInputMap();
        VerifyRuntimeCleanView();
        Require(SaveData.CurrentVersion == 3, "Hide UI must preserve SaveData v3.");
        Require(!typeof(SaveData).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(field => field.Name.IndexOf("interface", StringComparison.OrdinalIgnoreCase) >= 0),
            "Hide UI transient state must not be serialized in SaveData.");
    }

    private static void VerifySceneCleanViewRoot()
    {
        EditorSceneManager.OpenScene(VnScenePath);
        VNDialogueController controller = UnityEngine.Object.FindFirstObjectByType<VNDialogueController>(FindObjectsInactive.Include);
        Require(controller != null && controller.dialogueUiRoot != null, "VNPrototype requires a dialogue UI root for clean view.");
        Require(IsInside(controller.speakerText.gameObject, controller.dialogueUiRoot), "Dialogue UI root must contain the speaker box.");
        Require(IsInside(controller.dialogueText.gameObject, controller.dialogueUiRoot), "Dialogue UI root must contain dialogue text.");
        Require(IsInside(controller.nextButton.gameObject, controller.dialogueUiRoot), "Dialogue UI root must contain Next.");
        Require(!IsInside(controller.backgroundImage.gameObject, controller.dialogueUiRoot), "Dialogue UI root must not hide the background.");
        Require(!IsInside(controller.characterImage.gameObject, controller.dialogueUiRoot), "Dialogue UI root must not hide the character sprite.");
        Require(!IsInside(controller.choicePanel, controller.dialogueUiRoot), "Dialogue UI root must not own choices.");
        Require(!IsInside(controller.backlogPanel, controller.dialogueUiRoot), "Dialogue UI root must not own backlog.");
        Require(!IsInside(controller.vnSettingsPanel, controller.dialogueUiRoot), "Dialogue UI root must not own settings.");
        Require(!IsInside(controller.confirmExitPanel, controller.dialogueUiRoot), "Dialogue UI root must not own confirmation panels.");
    }

    private static void VerifyInputMap()
    {
        VNInputBinding binding = VNInputMap.AllBindings.Single(candidate => candidate.Action == VNInputAction.ToggleInterfaceVisibility);
        Require(binding.BindingDescription == "H", "Clean view must use H.");
        Require(binding.ShowInHelp, "Clean view must be visible in Help.");
        Require(VNInputMap.BuildHelpText().Contains("H \u2014 \u0421\u043a\u0440\u044b\u0442\u044c / \u043f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u0438\u043d\u0442\u0435\u0440\u0444\u0435\u0439\u0441"), "Help must include the H clean-view binding.");
        Require(!VNInputMap.AllBindings.Single(candidate => candidate.Action == VNInputAction.ToggleDebugStatsView).ShowInHelp, "F2 must remain hidden from Help.");
        Require(!VNInputMap.AllBindings.Single(candidate => candidate.Action == VNInputAction.ToggleDebugStatsPanel).ShowInHelp, "F3 must remain hidden from Help.");
    }

    private static void VerifyRuntimeCleanView()
    {
        GameObject controllerObject = new GameObject("HideUiSmokeController");
        GameObject dialogueRoot = new GameObject("HideUiSmokeDialogueRoot");
        GameObject background = new GameObject("HideUiSmokeBackground");
        GameObject character = new GameObject("HideUiSmokeCharacter");
        GameObject quickMenuObject = new GameObject("HideUiSmokeQuickMenu");
        GameObject quickMenuRoot = new GameObject("HideUiSmokeQuickMenuRoot");
        GameObject choice = new GameObject("HideUiSmokeChoice");
        GameObject backlog = new GameObject("HideUiSmokeBacklog");
        GameObject settings = new GameObject("HideUiSmokeSettings");
        GameObject confirm = new GameObject("HideUiSmokeConfirm");
        GameObject notification = new GameObject("HideUiSmokeNotification");
        GameObject saveLoad = new GameObject("HideUiSmokeSaveLoad");
        GameObject owner = new GameObject("HideUiSmokeSpecialOwner");

        try
        {
            VNDialogueController controller = controllerObject.AddComponent<VNDialogueController>();
            VNQuickMenu quickMenu = quickMenuObject.AddComponent<VNQuickMenu>();
            controller.dialogueUiRoot = dialogueRoot;
            controller.choicePanel = choice;
            controller.backlogPanel = backlog;
            controller.vnSettingsPanel = settings;
            controller.confirmExitPanel = confirm;
            controller.notificationPanel = notification;
            controller.manualSaveLoadPanel = saveLoad.AddComponent<ManualSaveLoadPanel>();
            quickMenu.dialogueController = controller;
            quickMenu.root = quickMenuRoot;
            choice.SetActive(false);
            backlog.SetActive(false);
            settings.SetActive(false);
            confirm.SetActive(false);
            notification.SetActive(false);
            saveLoad.SetActive(false);

            Require(!controller.IsInterfaceHidden, "A new VN controller must start with its interface visible.");
            Require(controller.TryHideInterface(), "Normal stable dialogue state must enter clean view.");
            Require(controller.IsInterfaceHidden, "Clean view state must become active.");
            Require(!dialogueRoot.activeSelf && !quickMenuRoot.activeSelf, "Clean view must hide the dialogue shell and Quick Menu.");
            Require(background.activeSelf && character.activeSelf, "Clean view must leave authored background and character objects untouched.");
            Require(!controller.CanAdvanceDialogue && !controller.CanSave && !controller.CanLoad, "Clean view must deny progression, save and load.");
            Require(!controller.CanOpenQuickMenu && !controller.CanOpenBacklog && !controller.CanOpenSettings && !controller.CanReturnToMainMenu, "Clean view must deny UI and special-menu actions.");
            Require(!controller.TryEnterSpecialMode(owner, SpecialModePolicy.BlockingExclusive, out _), "Special mode must be rejected while the interface is hidden.");
            controller.AdvanceDialogue();
            controller.ShowBacklog();
            controller.OpenSettings();
            controller.ShowConfirmExit();
            Require(!backlog.activeSelf && !settings.activeSelf && !confirm.activeSelf, "Clean view must not open blocked panels.");

            controller.RestoreInterface();
            Require(!controller.IsInterfaceHidden && dialogueRoot.activeSelf && quickMenuRoot.activeSelf, "Restore must return the exact visible dialogue shell and Quick Menu.");
            Require(controller.TryHideInterface(), "H toggles must be repeatable from a restored stable state.");
            controller.RestoreInterface();
            Require(dialogueRoot.activeSelf && quickMenuRoot.activeSelf, "Two H toggles must return to the initial visible state.");

            RequireRejectedWhileOpen(controller, choice, "choice");
            RequireRejectedWhileOpen(controller, backlog, "backlog");
            RequireRejectedWhileOpen(controller, settings, "settings");
            RequireRejectedWhileOpen(controller, confirm, "confirmation");
            RequireRejectedWhileOpen(controller, notification, "notification");
            RequireRejectedWhileOpen(controller, saveLoad, "save/load");
            SetPrivate(controller, "isTyping", true);
            Require(!controller.TryHideInterface(), "Typing must reject clean view instead of completing text.");
            SetPrivate(controller, "isTyping", false);
            SetPrivate(controller, "quickSaveInProgress", true);
            Require(!controller.TryHideInterface(), "Screenshot save capture must reject clean view.");
            SetPrivate(controller, "quickSaveInProgress", false);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(saveLoad);
            UnityEngine.Object.DestroyImmediate(notification);
            UnityEngine.Object.DestroyImmediate(confirm);
            UnityEngine.Object.DestroyImmediate(settings);
            UnityEngine.Object.DestroyImmediate(backlog);
            UnityEngine.Object.DestroyImmediate(choice);
            UnityEngine.Object.DestroyImmediate(quickMenuRoot);
            UnityEngine.Object.DestroyImmediate(quickMenuObject);
            UnityEngine.Object.DestroyImmediate(character);
            UnityEngine.Object.DestroyImmediate(background);
            UnityEngine.Object.DestroyImmediate(dialogueRoot);
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }

    private static void RequireRejectedWhileOpen(VNDialogueController controller, GameObject blocker, string name)
    {
        blocker.SetActive(true);
        Require(!controller.TryHideInterface(), $"Clean view must reject active {name}.");
        blocker.SetActive(false);
    }

    private static bool IsInside(GameObject child, GameObject root)
    {
        return child != null && root != null && child.transform.IsChildOf(root.transform);
    }

    private static void SetPrivate(VNDialogueController controller, string fieldName, bool value)
    {
        FieldInfo field = typeof(VNDialogueController).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Require(field != null, $"Missing Hide UI runtime field '{fieldName}'.");
        field.SetValue(controller, value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
