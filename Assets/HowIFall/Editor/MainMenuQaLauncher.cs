using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MainMenuQaLauncher
{
    private const string MainMenuScenePath = "Assets/HowIFall/Scenes/MainMenu.unity";
    private const string PendingNoSaveVariantKey = "HowIFall.MainMenuQa.PendingNoSaveVariant";
    private static string temporarySaveDirectory;

    static MainMenuQaLauncher()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update -= ApplyNoSaveVariantWhenReady;
    }

    [MenuItem("How I Fall/QA/Main Menu")]
    public static void OpenMainMenu()
    {
        Launch(false);
    }

    [MenuItem("How I Fall/QA/Main Menu - Continue Unavailable")]
    public static void OpenMainMenuWithoutSaves()
    {
        Launch(true);
    }

    private static void Launch(bool continueUnavailable)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[MAIN MENU QA] Stop the current Play Mode session before starting Main Menu QA.");
            return;
        }

        SessionState.SetBool(PendingNoSaveVariantKey, continueUnavailable);
        EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingNoSaveVariantKey, false))
        {
            EditorApplication.update -= ApplyNoSaveVariantWhenReady;
            EditorApplication.update += ApplyNoSaveVariantWhenReady;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= ApplyNoSaveVariantWhenReady;
            SessionState.SetBool(PendingNoSaveVariantKey, false);
        }
    }

    private static void ApplyNoSaveVariantWhenReady()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.update -= ApplyNoSaveVariantWhenReady;
            return;
        }

        MainMenuController menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
        SaveManager saveManager = SaveManager.Instance;
        if (menu == null || saveManager == null || SceneManager.GetActiveScene().path != MainMenuScenePath)
        {
            return;
        }

        temporarySaveDirectory = Path.Combine(Path.GetTempPath(), "HowIFall_MainMenuQa_NoSaves_" + Guid.NewGuid().ToString("N"));
        saveManager.ConfigureSaveDirectoryForTests(temporarySaveDirectory);
        menu.RefreshContinueAvailability();
        Debug.Log("[MAIN MENU QA] Continue Unavailable variant is ready. Real user saves were not touched.");
        EditorApplication.update -= ApplyNoSaveVariantWhenReady;
    }
}
