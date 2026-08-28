using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
        Require(menu.settingsButton != null && menu.mainMenuButton != null, "Quick Menu Settings and Menu references are required.");
        menu.ApplyPlayerFacingPresentation();
        Button[] expectedOrder =
        {
            menu.historyButton, menu.skipButton, menu.autoButton, menu.saveButton,
            menu.quickSaveButton, menu.quickLoadButton, menu.settingsButton, menu.mainMenuButton
        };
        Button[] actualOrder = menu.root.GetComponentsInChildren<Button>(true)
            .Where(button => button.transform.parent == menu.root.transform && button.gameObject.activeSelf)
            .OrderBy(button => button.transform.GetSiblingIndex())
            .ToArray();
        Require(actualOrder.SequenceEqual(expectedOrder),
            "Quick Menu normal order must be History / Skip / Auto / Save / QSave / QLoad / Preferences / Menu.");
        Require(!menu.loadButton.gameObject.activeSelf, "Manual Load must be absent from the normal Quick Menu.");
        Require(menu.charactersButton != null
            && !menu.charactersButton.gameObject.activeSelf
            && !menu.charactersButton.transform.IsChildOf(menu.root.transform),
            "Characters must remain a hidden deferred launcher outside the Quick Menu strip.");
        Require(menu.mainMenuButton.GetComponentInChildren<TextMeshProUGUI>(true).text == "Меню",
            "Direct Main Menu action must be replaced by the Game Menu route.");
        Require(typeof(VNDialogueController).GetMethod(nameof(VNDialogueController.RequestQuickLoad)) != null, "Quick Load must use the VN controller entry point.");
        Require(typeof(ManualSaveLoadPanel).GetMethod(nameof(ManualSaveLoadPanel.RequestQuickLoad)) != null, "Quick Load must use the existing ManualSaveLoadPanel pipeline.");
        VerifyPreferencesModalVisibilityOwnership();
    }

    private static void VerifyPreferencesModalVisibilityOwnership()
    {
        const string quickMenuPreferenceKey = "hif_show_quick_menu";
        bool preferenceKeyExisted = PlayerPrefs.HasKey(quickMenuPreferenceKey);
        int persistedPreferenceBefore = PlayerPrefs.GetInt(quickMenuPreferenceKey, int.MinValue);
        GameSettings runtimeSettings = SettingsManager.Instance != null ? SettingsManager.Instance.CurrentSettings : null;
        System.Reflection.FieldInfo runtimePreferenceField = runtimeSettings?.GetType().GetField("showQuickMenu");
        bool? runtimePreferenceBefore = runtimePreferenceField != null
            ? (bool?)runtimePreferenceField.GetValue(runtimeSettings)
            : null;

        GameObject owner = new GameObject("Quick Menu Preferences Modal Ownership Test");
        GameObject root = new GameObject("Quick Menu Root");
        root.transform.SetParent(owner.transform, false);
        VNQuickMenu menu = owner.AddComponent<VNQuickMenu>();
        menu.root = root;

        try
        {
            root.SetActive(true);
            menu.SetPreferencesModalHidden(true);
            Require(!root.activeSelf, "Gameplay Preferences must temporarily hide the Quick Menu root.");

            menu.SetPlayerInterfaceHidden(true);
            menu.SetPreferencesModalHidden(false);
            Require(!root.activeSelf, "Closing Preferences must not force the Quick Menu visible through the H/clean-view blocker.");

            menu.SetPlayerInterfaceHidden(false);
            bool expectedVisible = !runtimePreferenceBefore.HasValue || runtimePreferenceBefore.Value;
            Require(root.activeSelf == expectedVisible, "Closing Preferences must restore the current effective Quick Menu visibility policy.");

            Require(PlayerPrefs.HasKey(quickMenuPreferenceKey) == preferenceKeyExisted
                && PlayerPrefs.GetInt(quickMenuPreferenceKey, int.MinValue) == persistedPreferenceBefore,
                "Preferences modal ownership must not mutate the persistent Quick Menu preference.");
            Require(!runtimePreferenceBefore.HasValue
                || (bool)runtimePreferenceField.GetValue(runtimeSettings) == runtimePreferenceBefore.Value,
                "Preferences modal ownership must not mutate the runtime Quick Menu preference.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
