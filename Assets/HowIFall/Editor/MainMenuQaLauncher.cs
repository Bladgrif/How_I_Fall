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
    private const string PendingVariantKey = "HowIFall.PlayerUiQa.MainMenuVariant";
    private static string temporarySaveDirectory;

    private enum MainMenuVariant
    {
        MainMenu,
        ContinueUnavailable,
        Preferences,
        Help,
        Load,
        About,
        GalleryReplay,
        QuitConfirmation
    }

    static MainMenuQaLauncher()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update -= ApplyNoSaveVariantWhenReady;
    }

    [MenuItem("How I Fall/QA/Player UI/Main Menu")]
    public static void OpenMainMenu()
    {
        Launch(MainMenuVariant.MainMenu);
    }

    [MenuItem("How I Fall/QA/Main Menu - Continue Unavailable")]
    public static void OpenMainMenuWithoutSaves()
    {
        Launch(MainMenuVariant.ContinueUnavailable);
    }

    [MenuItem("How I Fall/QA/Player UI/Preferences")]
    public static void OpenPreferences()
    {
        Launch(MainMenuVariant.Preferences);
    }

    [MenuItem("How I Fall/QA/Player UI/Help")]
    public static void OpenHelp()
    {
        Launch(MainMenuVariant.Help);
    }

    [MenuItem("How I Fall/QA/Player UI/Main Menu - Load")]
    public static void OpenLoad()
    {
        Launch(MainMenuVariant.Load);
    }

    [MenuItem("How I Fall/QA/Player UI/Main Menu - About")]
    public static void OpenAbout()
    {
        Launch(MainMenuVariant.About);
    }

    [MenuItem("How I Fall/QA/Player UI/Main Menu - Gallery Replay")]
    public static void OpenGalleryReplay()
    {
        Launch(MainMenuVariant.GalleryReplay);
    }

    [MenuItem("How I Fall/QA/Player UI/Main Menu - Quit Confirmation")]
    public static void OpenQuitConfirmation()
    {
        Launch(MainMenuVariant.QuitConfirmation);
    }

    [MenuItem("How I Fall/QA/Player UI/Capture Current")]
    public static void CaptureCurrentPlayerUi()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[PLAYER UI QA] Enter Play Mode before capturing the current player UI.");
            return;
        }

        string directory = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "PlayerUiRuntimeAudit");
        Directory.CreateDirectory(directory);
        string sceneName = SceneManager.GetActiveScene().name.Replace(' ', '_');
        string path = Path.Combine(directory, sceneName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log("[PLAYER UI QA] Runtime screenshot queued: " + path);
    }

    private static void Launch(MainMenuVariant variant)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[MAIN MENU QA] Stop the current Play Mode session before starting Main Menu QA.");
            return;
        }

        SessionState.SetInt(PendingVariantKey, (int)variant);
        EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetInt(PendingVariantKey, -1) >= 0)
        {
            EditorApplication.update -= ApplyNoSaveVariantWhenReady;
            EditorApplication.update += ApplyNoSaveVariantWhenReady;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= ApplyNoSaveVariantWhenReady;
            SessionState.EraseInt(PendingVariantKey);
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

        MainMenuVariant variant = (MainMenuVariant)SessionState.GetInt(PendingVariantKey, (int)MainMenuVariant.MainMenu);
        switch (variant)
        {
            case MainMenuVariant.ContinueUnavailable:
                temporarySaveDirectory = Path.Combine(Path.GetTempPath(), "HowIFall_MainMenuQa_NoSaves_" + Guid.NewGuid().ToString("N"));
                saveManager.ConfigureSaveDirectoryForTests(temporarySaveDirectory);
                menu.RefreshContinueAvailability();
                Debug.Log("[PLAYER UI QA] Main Menu without saves is ready. Real user saves were not touched.");
                break;
            case MainMenuVariant.Preferences:
                menu.OpenSettings();
                Debug.Log("[PLAYER UI QA] Main Menu Preferences is ready.");
                break;
            case MainMenuVariant.Help:
                menu.OpenHelp();
                Debug.Log("[PLAYER UI QA] Main Menu Help is ready.");
                break;
            case MainMenuVariant.Load:
                menu.OpenManualLoad();
                Debug.Log("[PLAYER UI QA] Main Menu Load is ready.");
                break;
            case MainMenuVariant.About:
                menu.OpenAbout();
                Debug.Log("[PLAYER UI QA] Main Menu About is ready.");
                break;
            case MainMenuVariant.GalleryReplay:
                menu.OpenGallery();
                Debug.Log("[PLAYER UI QA] Main Menu Gallery/Replay is ready.");
                break;
            case MainMenuVariant.QuitConfirmation:
                menu.OpenExitConfirm();
                Debug.Log("[PLAYER UI QA] Main Menu Quit confirmation is ready.");
                break;
            default:
                Debug.Log("[PLAYER UI QA] Main Menu is ready.");
                break;
        }

        SessionState.EraseInt(PendingVariantKey);
        EditorApplication.update -= ApplyNoSaveVariantWhenReady;
    }
}
