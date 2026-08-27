using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PlayerUiGraphicalE2ERunner
{
    private const string ActiveKey = "HowIFall.PlayerUiE2E.Active";
    private const string StageKey = "HowIFall.PlayerUiE2E.Stage";
    private const string NextTimeKey = "HowIFall.PlayerUiE2E.NextTime";
    private const string CounterKey = "HowIFall.PlayerUiE2E.Counter";
    private const string NextStageKey = "HowIFall.PlayerUiE2E.NextStage";
    private const string CapturePathKey = "HowIFall.PlayerUiE2E.CapturePath";
    private const string RunStartedKey = "HowIFall.PlayerUiE2E.RunStartedUtc";
    private const string ErrorsKey = "HowIFall.PlayerUiE2E.Errors";
    private const string DirectoryKey = "HowIFall.PlayerUiE2E.Directory";
    private const string ResultPath = "player_ui_graphical_result.txt";
    private static readonly Vector2Int QaResolution = new Vector2Int(1920, 1080);

    static PlayerUiGraphicalE2ERunner()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        Application.logMessageReceived -= CaptureLog;
        Application.logMessageReceived += CaptureLog;
    }

    [MenuItem("How I Fall/Tests/Run Player UI Graphical E2E")]
    public static void StartAutomatedPlayMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("Play Mode is already active.");
        }

        string root = Directory.GetCurrentDirectory();
        string proofDirectory = Path.Combine(root, "QAArtifacts", "GraphicalE2E", "PlayerUi");
        if (Directory.Exists(proofDirectory)) Directory.Delete(proofDirectory, true);
        string resultPath = Path.Combine(root, ResultPath);
        if (File.Exists(resultPath)) File.Delete(resultPath);

        CleanupTestDirectory();
        string saveDirectory = Path.Combine(Path.GetTempPath(), "HowIFall_PlayerUiE2E_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(saveDirectory);
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetString(StageKey, "WaitMainMenu");
        SessionState.SetString(NextStageKey, string.Empty);
        SessionState.SetString(CapturePathKey, string.Empty);
        SessionState.SetString(RunStartedKey, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        SessionState.SetString(ErrorsKey, string.Empty);
        SessionState.SetString(DirectoryKey, saveDirectory);
        SessionState.SetInt(CounterKey, 0);
        SetDelay(1d);

        EditorSceneManager.OpenScene("Assets/HowIFall/Scenes/MainMenu.unity", OpenSceneMode.Single);
        Debug.Log("[PLAYER UI E2E] START: entering Play Mode from MainMenu.");
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(ActiveKey, false)
            || !EditorApplication.isPlaying
            || EditorApplication.timeSinceStartup < SessionState.GetFloat(NextTimeKey, 0f)) return;

        try
        {
            switch (SessionState.GetString(StageKey, string.Empty))
            {
                case "WaitMainMenu": WaitMainMenu(); break;
                case "WaitMainMenuFade": WaitMainMenuFade(); break;
                case "OpenMainPreferences": OpenMainPreferences(); break;
                case "WaitMainPreferences": WaitMainPreferences(); break;
                case "OpenScreenMode": OpenDropdown(SharedPreferencesView.ScreenModeId, "OpenResolution"); break;
                case "OpenResolution": OpenDropdown(SharedPreferencesView.ResolutionId, "StartGameplay"); break;
                case "CaptureDropdown": CaptureDropdown(); break;
                case "StartGameplay": StartGameplay(); break;
                case "WaitGameplay": WaitGameplay(); break;
                case "WaitGameplayPreferences": WaitGameplayPreferences(); break;
                case "WaitScreenshot": WaitScreenshot(); break;
            }
        }
        catch (Exception exception)
        {
            Fail("stage=" + SessionState.GetString(StageKey, string.Empty) + "\n" + exception);
        }
    }

    private static void WaitMainMenu()
    {
        if (SceneManager.GetActiveScene().name != "MainMenu") { Retry("MainMenu scene did not become active."); return; }
        MainMenuController menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        SaveManager saves = SaveManager.Instance;
        if (menu == null || saves == null || UnityEngine.Object.FindFirstObjectByType<Canvas>() == null)
        {
            Retry("Main Menu runtime UI is not ready.");
            return;
        }

        saves.ConfigureSaveDirectoryForTests(SessionState.GetString(DirectoryKey, string.Empty));
        menu.RefreshContinueAvailability();
        ConfigureGameViewResolution(QaResolution);
        if (!IsQaResolutionReady()) { Retry("Game View did not switch to 1920x1080."); return; }
        Require(UnityEngine.Object.FindObjectsByType<SaveManager>(FindObjectsSortMode.None).Length == 1, "MainMenu has more than one SaveManager.");
        SessionState.SetString(StageKey, "WaitMainMenuFade");
        ResetCounter();
        SetDelay(0.25d);
    }

    private static void WaitMainMenuFade()
    {
        MainMenuAnimator animator = UnityEngine.Object.FindFirstObjectByType<MainMenuAnimator>();
        if (animator != null && animator.menuCanvasGroup != null && animator.menuCanvasGroup.alpha < 0.99f)
        {
            Retry("Main Menu fade-in did not complete.");
            return;
        }

        MainMenuController menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        Require(menu != null, "MainMenuController disappeared before the initial-focus capture.");
        foreach (MainMenuButtonHoverEffect effect in UnityEngine.Object.FindObjectsByType<MainMenuButtonHoverEffect>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            effect.OnPointerExit(null);
        }
        menu.FocusDefaultAction();
        Capture("main_menu_1920x1080.png", "OpenMainPreferences");
    }

    private static void OpenMainPreferences()
    {
        MainMenuController menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        Require(menu != null, "MainMenuController disappeared before opening Preferences.");
        menu.OpenSettings();
        SessionState.SetString(StageKey, "WaitMainPreferences");
        ResetCounter();
        SetDelay(0.4d);
    }

    private static void WaitMainPreferences()
    {
        SharedPreferencesView view = FindVisiblePreferences();
        if (view == null) { Retry("Main Menu Preferences did not become visible."); return; }
        Require(view.GetDropdown(SharedPreferencesView.ScreenModeId) != null, "Screen Mode dropdown is missing.");
        Require(view.GetDropdown(SharedPreferencesView.ResolutionId) != null, "Resolution dropdown is missing.");
        Capture("main_menu_preferences_1920x1080.png", "OpenScreenMode");
    }

    private static void OpenDropdown(string id, string nextStage)
    {
        SharedPreferencesView view = FindVisiblePreferences();
        Require(view != null, "Preferences closed before dropdown capture.");
        if (id == SharedPreferencesView.ResolutionId)
        {
            view.GetDropdown(SharedPreferencesView.ScreenModeId)?.Hide();
        }
        TMP_Dropdown dropdown = view.GetDropdown(id);
        Require(dropdown != null && dropdown.IsActive(), $"Dropdown '{id}' is unavailable.");
        dropdown.Show();
        SessionState.SetString(StageKey, "CaptureDropdown");
        SessionState.SetString(NextStageKey, nextStage);
        SessionState.SetString(CapturePathKey, id == SharedPreferencesView.ScreenModeId
            ? "main_menu_preferences_screen_mode_open_1920x1080.png"
            : "main_menu_preferences_resolution_open_1920x1080.png");
        ResetCounter();
        SetDelay(0.4d);
    }

    private static void CaptureDropdown()
    {
        // Show() has opened the real TMP popup; the delay from OpenDropdown
        // gives its layout a frame to rebuild before this queued capture.
        Require(FindVisiblePreferences() != null, "Preferences closed before dropdown capture.");
        string next = SessionState.GetString(NextStageKey, string.Empty);
        string file = SessionState.GetString(CapturePathKey, string.Empty);
        Require(!string.IsNullOrEmpty(file) && !string.IsNullOrEmpty(next), "Dropdown capture state is incomplete.");
        Capture(file, next);
    }

    private static void StartGameplay()
    {
        SharedPreferencesView view = FindVisiblePreferences();
        Require(view != null, "Preferences closed before gameplay transition.");
        view.GetDropdown(SharedPreferencesView.ResolutionId)?.Hide();
        view.GetButton("back")?.onClick.Invoke();
        MainMenuController menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        Require(menu != null, "MainMenuController is unavailable for gameplay transition.");
        menu.StartGame();
        SessionState.SetString(StageKey, "WaitGameplay");
        ResetCounter();
        SetDelay(0.8d);
    }

    private static void WaitGameplay()
    {
        VNDialogueController dialogue = VNDialogueController.Instance;
        if (SceneManager.GetActiveScene().name != SaveManager.GameplaySceneName || dialogue == null || !dialogue.IsRuntimeReady)
        {
            Retry("Gameplay dialogue runtime is not ready.");
            return;
        }

        Require(SaveManager.Instance != null, "Gameplay SaveManager is missing.");
        if (!dialogue.IsGameMenuOpen) dialogue.OpenGameMenu();
        if (!dialogue.IsGameMenuOpen) { Retry("Gameplay menu did not open."); return; }
        VNGameMenuView view = dialogue.GameMenuController != null ? dialogue.GameMenuController.View : null;
        Require(view != null, "Gameplay menu view is missing.");
        view.GetButton(VNGameMenuAction.Preferences)?.onClick.Invoke();
        SessionState.SetString(StageKey, "WaitGameplayPreferences");
        ResetCounter();
        SetDelay(0.5d);
    }

    private static void WaitGameplayPreferences()
    {
        VNDialogueController dialogue = VNDialogueController.Instance;
        SharedPreferencesView view = FindVisiblePreferences();
        if (dialogue == null || !dialogue.IsPreferencesOpen || view == null)
        {
            Retry("Gameplay Preferences did not become visible.");
            return;
        }

        Capture("gameplay_preferences_1920x1080.png", "Complete");
    }

    private static void Capture(string fileName, string nextStage)
    {
        Require(IsQaResolutionReady(), $"Capture requires 1920x1080, actual {Screen.width}x{Screen.height}.");
        string path = Path.Combine(Directory.GetCurrentDirectory(), "QAArtifacts", "GraphicalE2E", "PlayerUi", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        SessionState.SetString(CapturePathKey, path);
        SessionState.SetString(NextStageKey, nextStage);
        SessionState.SetString(StageKey, "WaitScreenshot");
        ResetCounter();
        SetDelay(0.25d);
    }

    private static void WaitScreenshot()
    {
        string path = SessionState.GetString(CapturePathKey, string.Empty);
        if (!File.Exists(path) || new FileInfo(path).Length == 0) { Retry("Queued screenshot was not written: " + path); return; }
        DateTime runStarted = DateTime.Parse(SessionState.GetString(RunStartedKey, string.Empty), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        Require(File.GetLastWriteTimeUtc(path) >= runStarted, "Screenshot predates this run: " + path);
        VerifyImageDimensions(path, QaResolution.x, QaResolution.y);
        string next = SessionState.GetString(NextStageKey, string.Empty);
        if (next == "Complete") Success();
        else
        {
            SessionState.SetString(StageKey, next);
            ResetCounter();
            SetDelay(0.35d);
        }
    }

    private static SharedPreferencesView FindVisiblePreferences()
    {
        SharedPreferencesView[] views = UnityEngine.Object.FindObjectsByType<SharedPreferencesView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (SharedPreferencesView view in views)
            if (view != null && view.IsVisible) return view;
        return null;
    }

    private static bool IsQaResolutionReady() => Screen.width == QaResolution.x && Screen.height == QaResolution.y;

    private static void Retry(string message)
    {
        int attempts = SessionState.GetInt(CounterKey, 0) + 1;
        SessionState.SetInt(CounterKey, attempts);
        Require(attempts < 80, message + " Timed out after 80 attempts.");
        SetDelay(0.15d);
    }

    private static void ResetCounter() => SessionState.SetInt(CounterKey, 0);
    private static void SetDelay(double seconds) => SessionState.SetFloat(NextTimeKey, (float)(EditorApplication.timeSinceStartup + seconds));

    private static void VerifyImageDimensions(string path, int width, int height)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
        try
        {
            Require(texture.LoadImage(File.ReadAllBytes(path), true), "Screenshot is not a readable PNG: " + path);
            Require(texture.width == width && texture.height == height, $"Screenshot size is {texture.width}x{texture.height}, expected {width}x{height}.");
        }
        finally { UnityEngine.Object.Destroy(texture); }
    }

    private static void ConfigureGameViewResolution(Vector2Int resolution)
    {
        try
        {
            Assembly assembly = typeof(EditorWindow).Assembly;
            Type gameViewType = assembly.GetType("UnityEditor.GameView", true);
            Type sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
            Type sizeType = assembly.GetType("UnityEditor.GameViewSize", true);
            Type sizeKindType = assembly.GetType("UnityEditor.GameViewSizeType", true);
            Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singletonType.GetProperty("instance", BindingFlags.Static | BindingFlags.Public).GetValue(null, null);
            object group = sizesType.GetMethod("GetGroup", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(sizes, new[] { Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType", true), "Standalone") });
            MethodInfo countMethod = group.GetType().GetMethod("GetTotalCount", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo getSize = group.GetType().GetMethod("GetGameViewSize", BindingFlags.Instance | BindingFlags.Public);
            int count = (int)countMethod.Invoke(group, null);
            int index = -1;
            for (int i = 0; i < count; i++)
            {
                object size = getSize.Invoke(group, new object[] { i });
                int width = (int)sizeType.GetProperty("width").GetValue(size, null);
                int height = (int)sizeType.GetProperty("height").GetValue(size, null);
                if (width == resolution.x && height == resolution.y) { index = i; break; }
            }
            if (index < 0)
            {
                object size = Activator.CreateInstance(sizeType, Enum.Parse(sizeKindType, "FixedResolution"), resolution.x, resolution.y, "How I Fall QA 1920x1080");
                group.GetType().GetMethod("AddCustomSize", BindingFlags.Instance | BindingFlags.Public).Invoke(group, new[] { size });
                index = count;
            }
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(gameView, index, null);
            gameView.Repaint();
            Screen.SetResolution(resolution.x, resolution.y, FullScreenMode.Windowed);
        }
        catch (Exception exception) { throw new InvalidOperationException("Could not configure Game View resolution.", exception); }
    }

    private static void Success()
    {
        Require(string.IsNullOrEmpty(SessionState.GetString(ErrorsKey, string.Empty)), "Unity Console contained errors:\n" + SessionState.GetString(ErrorsKey, string.Empty));
        WriteResult("PASS", "all five PlayerUi screenshots captured at 1920x1080");
        CleanupTestDirectory();
        SessionState.SetString(StageKey, "ExitSuccess");
        EditorApplication.isPlaying = false;
    }

    private static void Fail(string details)
    {
        WriteResult("FAIL", details);
        CleanupTestDirectory();
        Debug.LogError("[PLAYER UI E2E] FAILURE: " + details);
        SessionState.SetString(StageKey, "ExitFailure");
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        else { SessionState.SetBool(ActiveKey, false); EditorApplication.Exit(1); }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false) || state != PlayModeStateChange.EnteredEditMode) return;
        string stage = SessionState.GetString(StageKey, string.Empty);
        SessionState.SetBool(ActiveKey, false);
        if (stage == "ExitSuccess") EditorApplication.Exit(0);
        if (stage == "ExitFailure") EditorApplication.Exit(1);
    }

    private static void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (!SessionState.GetBool(ActiveKey, false)
            || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            || condition.StartsWith("[PLAYER UI E2E] FAILURE", StringComparison.Ordinal)) return;
        string errors = SessionState.GetString(ErrorsKey, string.Empty);
        if (errors.Length < 12000) SessionState.SetString(ErrorsKey, errors + condition + "\n");
    }

    private static void WriteResult(string status, string details)
    {
        File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), ResultPath),
            $"status={status}\n" + $"timeUtc={DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)}\n" + $"details={details}\n");
    }

    private static void CleanupTestDirectory()
    {
        string directory = SessionState.GetString(DirectoryKey, string.Empty);
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
