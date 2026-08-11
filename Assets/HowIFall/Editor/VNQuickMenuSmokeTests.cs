using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VNQuickMenuSmokeTests
{
    private const string VnScenePath = "Assets/HowIFall/Scenes/VNPrototype.unity";

    [MenuItem("How I Fall/Tests/Run Quick Menu Smoke Tests")]
    public static void RunFromMenu()
    {
        RunBatchMode();
        Debug.Log("How I Fall quick menu smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        EditorSceneManager.OpenScene(VnScenePath);
        VNQuickMenu menu = UnityEngine.Object.FindFirstObjectByType<VNQuickMenu>(FindObjectsInactive.Include);
        Require(menu != null, "VNPrototype must contain one VNQuickMenu.");
        Require(UnityEngine.Object.FindObjectsByType<VNQuickMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1, "VNPrototype must contain a single Quick Menu.");
        Require(menu.dialogueController != null && menu.root != null, "Quick Menu controller/root references are required.");
        Require(menu.historyButton != null && menu.skipButton != null && menu.autoButton != null, "Quick Menu History, Skip and Auto references are required.");
        Require(menu.saveButton != null && menu.quickSaveButton != null && menu.quickLoadButton != null && menu.loadButton != null, "Quick Menu save references are required.");
        Require(menu.settingsButton != null && menu.mainMenuButton != null, "Quick Menu Settings and Main Menu references are required; Characters is runtime-created.");
        Require(typeof(VNDialogueController).GetMethod(nameof(VNDialogueController.RequestQuickLoad)) != null, "Quick Load must use the VN controller entry point.");
        Require(typeof(ManualSaveLoadPanel).GetMethod(nameof(ManualSaveLoadPanel.RequestQuickLoad)) != null, "Quick Load must use the existing ManualSaveLoadPanel pipeline.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
