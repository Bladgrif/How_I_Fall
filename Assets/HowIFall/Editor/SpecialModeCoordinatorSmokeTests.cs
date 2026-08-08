using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class SpecialModeCoordinatorSmokeTests
{
    [MenuItem("How I Fall/Tests/Run Special Mode Coordinator Smoke Tests")]
    public static void RunFromMenu()
    {
        RunBatchMode();
        Debug.Log("How I Fall special mode coordinator smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        TestDefaultPermissionsAndBlockingPolicy();
        TestLeasesAndDiagnostics();
        TestDestroyedOwnerAndHostCleanup();
        TestControllerIntegrationGates();
        Require(SaveData.CurrentVersion == 3, "Special-mode work must preserve SaveData v3.");
    }

    private static void TestDefaultPermissionsAndBlockingPolicy()
    {
        var diagnostics = new List<string>();
        var coordinator = new SpecialModeCoordinator(diagnostic: diagnostics.Add);

        Require(!coordinator.HasActiveOwner, "Coordinator must start without an owner.");
        Require(!coordinator.IsDialogueAdvanceBlocked && !coordinator.IsAutoBlocked && !coordinator.IsSkipBlocked, "No owner must preserve VN progression.");
        Require(coordinator.CanSave && coordinator.CanLoad && coordinator.CanOpenQuickMenu && coordinator.CanOpenBacklog && coordinator.CanOpenSettings && coordinator.CanReturnToMainMenu, "No owner must preserve normal permissions.");

        GameObject owner = new GameObject("SpecialModeSmokeOwner");
        try
        {
            Require(coordinator.TryEnter(owner, SpecialModePolicy.BlockingExclusive, out SpecialModeLease lease), "BlockingExclusive must enter for a valid owner.");
            Require(lease != null && coordinator.HasActiveOwner, "Successful entry must return an active lease.");
            Require(coordinator.IsDialogueAdvanceBlocked && coordinator.IsAutoBlocked && coordinator.IsSkipBlocked, "BlockingExclusive must block dialogue, Auto and Skip.");
            Require(!coordinator.CanSave && !coordinator.CanLoad && !coordinator.CanOpenQuickMenu && !coordinator.CanOpenBacklog && !coordinator.CanOpenSettings && !coordinator.CanReturnToMainMenu, "BlockingExclusive must deny every Phase-1 capability.");
            Require(!coordinator.TryRequestEscapeCancel(), "BlockingExclusive must deny Escape cancellation.");
            Require(coordinator.Exit(lease), "The exact active lease must exit.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    private static void TestLeasesAndDiagnostics()
    {
        var diagnostics = new List<string>();
        var coordinator = new SpecialModeCoordinator(() => "ordinary choice", diagnostics.Add);
        GameObject firstOwner = new GameObject("SpecialModeFirstOwner");
        GameObject secondOwner = new GameObject("SpecialModeSecondOwner");
        try
        {
            Require(!coordinator.TryEnter(firstOwner, SpecialModePolicy.BlockingExclusive, out _), "An open normal blocker must reject special entry.");
            Require(!coordinator.TryEnter(firstOwner, default, out _), "Default policy must be rejected and fail closed.");

            coordinator = new SpecialModeCoordinator(diagnostic: diagnostics.Add);
            Require(coordinator.TryEnter(firstOwner, SpecialModePolicy.BlockingExclusive, out SpecialModeLease activeLease), "Valid entry must succeed.");
            Require(!coordinator.TryEnter(firstOwner, SpecialModePolicy.BlockingExclusive, out _), "Duplicate same-owner entry must be rejected.");
            Require(!coordinator.TryEnter(secondOwner, SpecialModePolicy.BlockingExclusive, out _), "Competing entry must be rejected.");
            Require(coordinator.HasActiveOwner, "Rejected entries must retain the real owner.");

            var otherCoordinator = new SpecialModeCoordinator(diagnostic: diagnostics.Add);
            Require(otherCoordinator.TryEnter(secondOwner, SpecialModePolicy.BlockingExclusive, out SpecialModeLease wrongLease), "Independent coordinator must provide a wrong lease fixture.");
            Require(!coordinator.Exit(wrongLease), "Wrong coordinator lease must be rejected.");
            Require(coordinator.HasActiveOwner, "Wrong exit must not release the active owner.");
            Require(coordinator.Exit(activeLease), "Exact lease must exit.");
            Require(!coordinator.Exit(activeLease), "Stale lease must be rejected.");
            otherCoordinator.ForceClearForHostLifecycle("smoke cleanup");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(firstOwner);
            UnityEngine.Object.DestroyImmediate(secondOwner);
        }

        Require(diagnostics.Exists(message => message.Contains("[SPECIAL MODE]")), "Coordinator diagnostics must use the required prefix.");
    }

    private static void TestDestroyedOwnerAndHostCleanup()
    {
        var diagnostics = new List<string>();
        var coordinator = new SpecialModeCoordinator(diagnostic: diagnostics.Add);
        GameObject owner = new GameObject("SpecialModeDestroyedOwner");
        Require(coordinator.TryEnter(owner, SpecialModePolicy.BlockingExclusive, out _), "Destroyed-owner fixture must enter.");
        UnityEngine.Object.DestroyImmediate(owner);
        Require(!coordinator.HasActiveOwner, "Destroyed Unity owner must be detected and cleared.");

        owner = new GameObject("SpecialModeForceClearOwner");
        try
        {
            Require(coordinator.TryEnter(owner, SpecialModePolicy.BlockingExclusive, out _), "Force-clear fixture must enter.");
            coordinator.ForceClearForHostLifecycle("smoke host cleanup");
            Require(!coordinator.HasActiveOwner, "ForceClearForHostLifecycle must leave no owner.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    private static void TestControllerIntegrationGates()
    {
        GameObject controllerObject = new GameObject("SpecialModeControllerSmoke");
        GameObject owner = new GameObject("SpecialModeControllerOwner");
        GameObject choice = new GameObject("SpecialModeChoiceBlocker");
        GameObject backlog = new GameObject("SpecialModeBacklogBlocker");
        GameObject settings = new GameObject("SpecialModeSettingsBlocker");
        GameObject confirm = new GameObject("SpecialModeConfirmBlocker");
        GameObject panelObject = new GameObject("SpecialModeSaveLoadBlocker");
        GameObject quickMenuObject = new GameObject("SpecialModeQuickMenu");
        GameObject quickMenuRoot = new GameObject("SpecialModeQuickMenuRoot");

        try
        {
            VNDialogueController controller = controllerObject.AddComponent<VNDialogueController>();
            ManualSaveLoadPanel saveLoadPanel = panelObject.AddComponent<ManualSaveLoadPanel>();
            controller.choicePanel = choice;
            controller.backlogPanel = backlog;
            controller.vnSettingsPanel = settings;
            controller.confirmExitPanel = confirm;
            controller.manualSaveLoadPanel = saveLoadPanel;

            RequireRejectedWhileOpen(controller, owner, choice, "choice");
            RequireRejectedWhileOpen(controller, owner, backlog, "backlog");
            RequireRejectedWhileOpen(controller, owner, settings, "settings");
            RequireRejectedWhileOpen(controller, owner, confirm, "confirm exit");
            RequireRejectedWhileOpen(controller, owner, panelObject, "manual save/load");

            controller.SetSkip(true);
            Require(controller.IsSkipEnabled, "Skip fixture must preserve selected runtime state before special entry.");
            Require(controller.TryEnterSpecialMode(owner, SpecialModePolicy.BlockingExclusive, out SpecialModeLease lease), "Controller bridge must enter BlockingExclusive.");
            Require(!controller.CanAdvanceDialogue && !controller.CanSave && !controller.CanLoad && !controller.CanOpenBacklog && !controller.CanOpenSettings && !controller.CanOpenQuickMenu && !controller.CanReturnToMainMenu, "Controller permissions must reflect the active policy.");
            controller.ToggleSkip();
            Require(controller.IsSkipEnabled, "Special entry must pause Skip without clearing skipEnabled.");

            controller.backlogPanel.SetActive(false);
            controller.ShowBacklog();
            Require(!controller.backlogPanel.activeSelf, "ShowBacklog must be denied during BlockingExclusive.");
            controller.confirmExitPanel.SetActive(false);
            controller.ShowConfirmExit();
            Require(!controller.confirmExitPanel.activeSelf, "ShowConfirmExit must be denied during BlockingExclusive.");

            VNQuickMenu quickMenu = quickMenuObject.AddComponent<VNQuickMenu>();
            quickMenu.dialogueController = controller;
            quickMenu.root = quickMenuRoot;
            quickMenu.RefreshSpecialModeVisibility();
            Require(!quickMenuRoot.activeSelf, "BlockingExclusive must hide the Quick Menu root.");

            Require(controller.ExitSpecialMode(lease), "Controller bridge must exit the exact lease.");
            Require(controller.CanAdvanceDialogue && controller.CanSave && controller.CanLoad && controller.CanOpenQuickMenu, "Controller permissions must restore after exit.");
            quickMenu.RefreshSpecialModeVisibility();
            Require(quickMenuRoot.activeSelf, "Quick Menu root must return after special exit.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(quickMenuRoot);
            UnityEngine.Object.DestroyImmediate(quickMenuObject);
            UnityEngine.Object.DestroyImmediate(panelObject);
            UnityEngine.Object.DestroyImmediate(confirm);
            UnityEngine.Object.DestroyImmediate(settings);
            UnityEngine.Object.DestroyImmediate(backlog);
            UnityEngine.Object.DestroyImmediate(choice);
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }

    private static void RequireRejectedWhileOpen(VNDialogueController controller, UnityEngine.Object owner, GameObject blocker, string name)
    {
        blocker.SetActive(true);
        Require(!controller.TryEnterSpecialMode(owner, SpecialModePolicy.BlockingExclusive, out _), $"Special entry must reject active {name}.");
        blocker.SetActive(false);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
