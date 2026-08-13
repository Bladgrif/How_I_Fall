using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class QuickMenuVisibilitySmokeTests
{
    private const string PreferenceKey = "hif_show_quick_menu";

    [MenuItem("How I Fall/Tests/Run Quick Menu Visibility B03 Smoke Tests")]
    public static void RunFromMenu()
    {
        RunBatchMode();
        Debug.Log("How I Fall Quick Menu visibility B03 smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        Require(new GameSettings().showQuickMenu, "GameSettings must default Quick Menu visibility to ON.");
        Require(!typeof(SaveData).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(field => field.Name == nameof(GameSettings.showQuickMenu)), "Quick Menu preference must not be stored in SaveData.");
        Require(SaveData.CurrentVersion == 3, "B03 must preserve SaveData v3.");
        VerifyPersistenceAndReset();
        VerifySharedPreferencesTruth();
        VerifyEffectiveVisibilityAndBlockers();
        Require(VNInputMap.AllBindings.Count > 0, "Quick Menu visibility must not change keyboard input wiring.");
    }

    private static void VerifySharedPreferencesTruth()
    {
        SettingsManager manager = GetSettingsManager();
        GameSettings previousSettings = manager.settings;
        try
        {
            manager.settings = new GameSettings();
            var service = new PreferencesService(manager);
            service.SetShowQuickMenu(false);
            Require(!service.Current.showQuickMenu && !manager.CurrentSettings.showQuickMenu,
                "Main Menu and gameplay Preferences must use the same B03 truth.");
            service.Reset();
            Require(service.Current.showQuickMenu, "Shared Preferences Reset must restore B03 to ON.");
        }
        finally
        {
            manager.settings = previousSettings;
        }
    }

    private static void VerifyPersistenceAndReset()
    {
        SettingsManager manager = GetSettingsManager();
        bool hadValue = PlayerPrefs.HasKey(PreferenceKey);
        int previousValue = PlayerPrefs.GetInt(PreferenceKey, 1);
        GameSettings previousSettings = manager.settings;

        try
        {
            PlayerPrefs.SetInt(PreferenceKey, 0);
            manager.LoadSettings();
            Require(!manager.settings.showQuickMenu, "PlayerPrefs must load hif_show_quick_menu.");

            manager.SetShowQuickMenu(true);
            Require(PlayerPrefs.GetInt(PreferenceKey) == 1, "Setter must persist hif_show_quick_menu immediately.");

            manager.SetShowQuickMenu(false);
            manager.ResetSettings();
            Require(manager.settings.showQuickMenu && PlayerPrefs.GetInt(PreferenceKey) == 1, "Reset must restore Quick Menu visibility to ON.");
        }
        finally
        {
            manager.settings = previousSettings;
            if (hadValue)
            {
                PlayerPrefs.SetInt(PreferenceKey, previousValue);
            }
            else
            {
                PlayerPrefs.DeleteKey(PreferenceKey);
            }
            PlayerPrefs.Save();
        }
    }

    private static void VerifyEffectiveVisibilityAndBlockers()
    {
        SettingsManager manager = GetSettingsManager();
        GameSettings previousSettings = manager.settings;
        GameObject controllerObject = new GameObject("B03 Controller");
        GameObject quickMenuObject = new GameObject("B03 Quick Menu Host");
        GameObject root = new GameObject("B03 Quick Menu Root");
        GameObject owner = new GameObject("B03 Special Owner");

        try
        {
            manager.settings = new GameSettings();
            VNDialogueController controller = controllerObject.AddComponent<VNDialogueController>();
            VNQuickMenu quickMenu = quickMenuObject.AddComponent<VNQuickMenu>();
            quickMenu.dialogueController = controller;
            quickMenu.root = root;

            manager.SetShowQuickMenu(false);
            quickMenu.RefreshEffectiveVisibility();
            Require(!root.activeSelf, "OFF must hide the Quick Menu root.");
            manager.SetShowQuickMenu(true);
            quickMenu.RefreshEffectiveVisibility();
            Require(root.activeSelf, "ON must restore the Quick Menu root without blockers.");

            quickMenu.SetPlayerInterfaceHidden(true);
            Require(root.activeSelf == false && manager.settings.showQuickMenu, "H must hide the root without changing the preference.");
            manager.SetShowQuickMenu(false);
            quickMenu.SetPlayerInterfaceHidden(false);
            Require(!root.activeSelf, "OFF followed by H restore must keep the root hidden.");
            manager.SetShowQuickMenu(true);
            quickMenu.RefreshEffectiveVisibility();
            Require(root.activeSelf, "Latest ON preference must win after H restore.");

            Require(controller.TryEnterSpecialMode(owner, SpecialModePolicy.BlockingExclusive, out SpecialModeLease lease), "B03 special-mode fixture must enter.");
            quickMenu.RefreshEffectiveVisibility();
            Require(!root.activeSelf && manager.settings.showQuickMenu, "Special Mode must hide the root without changing the preference.");
            manager.SetShowQuickMenu(false);
            Require(!root.activeSelf, "Changing preference under Special Mode must not show the root.");
            Require(controller.ExitSpecialMode(lease), "B03 special-mode fixture must exit.");
            quickMenu.RefreshEffectiveVisibility();
            Require(!root.activeSelf, "OFF must remain hidden after Special Mode exits.");
            manager.SetShowQuickMenu(true);
            quickMenu.RefreshEffectiveVisibility();
            Require(root.activeSelf, "Latest ON preference must restore only after the blocker exits.");

            quickMenu.SetPreferencesModalHidden(true);
            manager.SetShowQuickMenu(false);
            manager.SetShowQuickMenu(true);
            Require(!root.activeSelf && manager.settings.showQuickMenu,
                "Changing B03 under Preferences must update truth without revealing the Quick Menu.");
            quickMenu.SetGameMenuModalHidden(true);
            quickMenu.SetPreferencesModalHidden(false);
            Require(!root.activeSelf && manager.settings.showQuickMenu,
                "Closing Preferences must not override the Game Menu blocker or mutate B03.");
            manager.SetShowQuickMenu(false);
            quickMenu.SetGameMenuModalHidden(false);
            Require(!root.activeSelf && !manager.settings.showQuickMenu,
                "Closing Game Menu must not force visibility when B03 is OFF.");
        }
        finally
        {
            manager.settings = previousSettings;
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(quickMenuObject);
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }
    }

    private static SettingsManager GetSettingsManager()
    {
        if (SettingsManager.Instance != null)
        {
            return SettingsManager.Instance;
        }

        SettingsManager manager = new GameObject("B03 Settings Manager").AddComponent<SettingsManager>();
        typeof(SettingsManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, manager);
        return manager;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
