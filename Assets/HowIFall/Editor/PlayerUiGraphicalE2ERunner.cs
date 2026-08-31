using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class PlayerUiGraphicalE2ERunner
{
    private const string ActiveKey = "HowIFall.PlayerUiE2E.Active";
    private const string StageKey = "HowIFall.PlayerUiE2E.Stage";
    private const string NextTimeKey = "HowIFall.PlayerUiE2E.NextTime";
    private const string CounterKey = "HowIFall.PlayerUiE2E.Counter";
    private const string NextStageKey = "HowIFall.PlayerUiE2E.NextStage";
    private const string CapturePathKey = "HowIFall.PlayerUiE2E.CapturePath";
    private const string CaptureWidthKey = "HowIFall.PlayerUiE2E.CaptureWidth";
    private const string CaptureHeightKey = "HowIFall.PlayerUiE2E.CaptureHeight";
    private const string RunStartedKey = "HowIFall.PlayerUiE2E.RunStartedUtc";
    private const string ErrorsKey = "HowIFall.PlayerUiE2E.Errors";
    private const string DirectoryKey = "HowIFall.PlayerUiE2E.Directory";
    private const string ResultPath = "player_ui_graphical_result.txt";
    private static readonly Vector2Int QaResolution = new Vector2Int(1920, 1080);
    private static readonly Vector2Int ResponsiveQaResolution = new Vector2Int(1280, 720);
    private static readonly List<UnityEngine.Object> RuntimeFixtures = new List<UnityEngine.Object>();

    private const string LongReadingFixtureText = "Это длинная нейтральная реплика для проверки чтения на 1920×1080. При масштабе текста 125 % она переносится на несколько строк, остаётся внутри окна и не сталкивается с быстрым меню.";

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
                case "CaptureMainMenuAlternate": CaptureMainMenuAlternate(); break;
                case "OpenMainPreferences": OpenMainPreferences(); break;
                case "WaitMainPreferences": WaitMainPreferences(); break;
                case "OpenScreenMode": FocusSelector(SharedPreferencesView.ScreenModeId, "OpenResolution", "main_menu_preferences_screen_mode_selected_1920x1080.png"); break;
                case "OpenResolution": FocusSelector(SharedPreferencesView.ResolutionId, "FocusSlider", "main_menu_preferences_resolution_selected_1920x1080.png"); break;
                case "FocusSlider": FocusSlider(); break;
                case "CloseMainPreferences": CloseMainPreferences(); break;
                case "OpenMainLoad": OpenMainLoad(); break;
                case "WaitMainLoad": WaitMainLoad(); break;
                case "CloseMainLoad": CloseMainLoad(); break;
                case "OpenMainQuitConfirmation": OpenMainQuitConfirmation(); break;
                case "WaitMainQuitConfirmation": WaitMainQuitConfirmation(); break;
                case "CaptureMainQuitYesFocus": CaptureMainQuitYesFocus(); break;
                case "CloseMainQuitConfirmation": CloseMainQuitConfirmation(); break;
                case "CaptureResponsiveMainMenu": CaptureResponsiveMainMenu(); break;
                case "StartGameplay": StartGameplay(); break;
                case "WaitGameplay": WaitGameplay(); break;
                case "PrepareLongDialogue": PrepareLongDialogue(); break;
                case "OpenReadingChoices": OpenReadingChoices(); break;
                case "PrepareBacklog": PrepareBacklog(); break;
                case "PrepareAuto": PrepareAuto(); break;
                case "PrepareSkip": PrepareSkip(); break;
                case "PrepareHideUi": PrepareHideUi(); break;
                case "RestoreAfterHideUi": RestoreAfterHideUi(); break;
                case "CaptureGameMenuAlternateFocus": CaptureGameMenuAlternateFocus(); break;
                case "OpenGameplayPreferences": OpenGameplayPreferences(); break;
                case "WaitGameplayPreferences": WaitGameplayPreferences(); break;
                case "PrepareResponsivePreferences": PrepareResponsivePreferences(); break;
                case "CaptureResponsivePreferences": CaptureResponsivePreferences(); break;
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
        Capture("main_menu_1920x1080.png", "CaptureMainMenuAlternate");
    }

    private static void CaptureMainMenuAlternate()
    {
        MainMenuController menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        Require(menu != null, "MainMenuController disappeared before alternate-focus capture.");
        menu.FocusSettingsAction();
        Capture("main_menu_settings_focus_1920x1080.png", "OpenMainPreferences");
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
        Require(view.GetButton(SharedPreferencesView.ScreenModeId) != null, "Screen Mode selector is missing.");
        Require(view.GetButton(SharedPreferencesView.ResolutionId) != null, "Resolution selector is missing.");
        Capture("main_menu_preferences_1920x1080.png", "OpenScreenMode");
    }

    private static void FocusSelector(string id, string nextStage, string fileName)
    {
        SharedPreferencesView view = FindVisiblePreferences();
        Require(view != null, "Preferences closed before selector capture.");
        Button selector = view.GetButton(id);
        Require(selector != null && selector.IsActive(), $"Selector '{id}' is unavailable.");
        EventSystem.current.SetSelectedGameObject(selector.gameObject);
        Capture(fileName, nextStage);
    }

    private static void FocusSlider()
    {
        SharedPreferencesView view = FindVisiblePreferences();
        Slider slider = view != null ? view.GetSlider(SharedPreferencesView.MasterVolumeId) : null;
        Require(slider != null, "Master Volume slider is missing.");
        EventSystem.current.SetSelectedGameObject(slider.gameObject);
        Capture("main_menu_preferences_slider_focus_1920x1080.png", "CloseMainPreferences");
    }

    private static void CloseMainPreferences()
    {
        SharedPreferencesView view = FindVisiblePreferences();
        Require(view != null, "Preferences closed before Main Menu route proof.");
        view.GetButton("back")?.onClick.Invoke();
        SessionState.SetString(StageKey, "OpenMainLoad");
        SetDelay(0.3d);
    }

    private static void OpenMainLoad()
    {
        MainMenuController menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        Require(menu != null, "MainMenuController is unavailable before Load proof.");
        menu.OpenManualLoad();
        SessionState.SetString(StageKey, "WaitMainLoad");
        ResetCounter();
        SetDelay(0.4d);
    }

    private static void WaitMainLoad()
    {
        MainMenuController menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        if (menu == null || menu.manualSaveLoadPanel == null || !menu.manualSaveLoadPanel.IsOpen)
        {
            Retry("Main Menu Load did not become visible.");
            return;
        }

        Capture("main_menu_load_1920x1080.png", "CloseMainLoad");
    }

    private static void CloseMainLoad()
    {
        MainMenuController menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        Require(menu != null && menu.manualSaveLoadPanel != null, "Main Menu Load panel disappeared before close.");
        menu.manualSaveLoadPanel.closeButton.onClick.Invoke();
        SessionState.SetString(StageKey, "OpenMainQuitConfirmation");
        SetDelay(0.3d);
    }

    private static void OpenMainQuitConfirmation()
    {
        MainMenuController menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        Require(menu != null, "MainMenuController is unavailable before Quit proof.");
        menu.OpenExitConfirm();
        SessionState.SetString(StageKey, "WaitMainQuitConfirmation");
        ResetCounter();
        SetDelay(0.3d);
    }

    private static void WaitMainQuitConfirmation()
    {
        MainMenuController menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        GameObject exitPanel = menu != null
            ? typeof(MainMenuController).GetField("exitConfirmPanel", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(menu) as GameObject
            : null;
        if (exitPanel == null || !exitPanel.activeSelf)
        {
            Retry("Main Menu Quit confirmation did not become visible.");
            return;
        }

        Require(EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null,
            "Quit confirmation did not assign safe cancel focus.");
        Button cancel = exitPanel.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(button => HasPersistentRoute(button, nameof(MainMenuController.CloseExitConfirm)));
        Require(cancel != null && EventSystem.current.currentSelectedGameObject == cancel.gameObject,
            "Quit confirmation default focus must be the safe Нет action.");
        Capture("main_menu_quit_confirmation_1920x1080.png", "CaptureMainQuitYesFocus");
    }

    private static void CaptureMainQuitYesFocus()
    {
        MainMenuController menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        GameObject exitPanel = menu != null
            ? typeof(MainMenuController).GetField("exitConfirmPanel", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(menu) as GameObject
            : null;
        Require(exitPanel != null && exitPanel.activeSelf, "Quit confirmation closed before alternate focus proof.");
        Button confirm = exitPanel.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(button => HasPersistentRoute(button, nameof(MainMenuController.ConfirmExit)));
        Require(confirm != null, "Quit confirmation destructive action is missing.");
        confirm.Select();
        Require(EventSystem.current.currentSelectedGameObject == confirm.gameObject,
            "Quit confirmation alternate focus did not select Да.");
        Capture("main_menu_quit_confirmation_yes_focus_1920x1080.png", "CloseMainQuitConfirmation");
    }

    private static void CloseMainQuitConfirmation()
    {
        MainMenuController menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        Require(menu != null, "MainMenuController is unavailable before responsive Main Menu proof.");
        menu.CloseExitConfirm();
        ConfigureGameViewResolution(ResponsiveQaResolution);
        SessionState.SetString(StageKey, "CaptureResponsiveMainMenu");
        ResetCounter();
        SetDelay(0.4d);
    }

    private static void CaptureResponsiveMainMenu()
    {
        if (Screen.width != ResponsiveQaResolution.x || Screen.height != ResponsiveQaResolution.y)
        {
            Retry("Game View did not switch to 1280x720 for Main Menu responsive proof.");
            return;
        }

        MainMenuController menu = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        Require(menu != null, "MainMenuController disappeared before 1280x720 Main Menu proof.");
        menu.FocusDefaultAction();
        Capture("main_menu_1280x720.png", "StartGameplay");
    }

    private static void StartGameplay()
    {
        ConfigureGameViewResolution(QaResolution);
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
        CompleteTyping(dialogue);
        Capture("gameplay_dialogue_standard_1920x1080.png", "PrepareLongDialogue");
    }

    private static void PrepareLongDialogue()
    {
        VNDialogueController dialogue = RequireGameplayDialogue();
        LoadRuntimeFixture(dialogue, LongReadingFixtureText, new List<DialogueChoice>());
        SettingsManager.Instance.SetDialogueTextScale(1.25f);
        CompleteTyping(dialogue);
        Capture("gameplay_dialogue_long_125pct_1920x1080.png", "OpenReadingChoices");
    }

    private static void OpenReadingChoices()
    {
        VNDialogueController dialogue = RequireGameplayDialogue();
        LoadRuntimeFixture(dialogue, "Выбор остаётся отдельным читаемым действием.", new List<DialogueChoice>
        {
            new DialogueChoice { text = "Продолжить проверку с длинным вариантом, который корректно переносится на две строки." },
            new DialogueChoice { text = "Открыть историю после проверки фокуса." },
            new DialogueChoice { text = "Оставить режим чтения без изменения состояния." }
        });
        InvokePrivate(dialogue, "ShowChoices", false);
        Require(EventSystem.current != null && EventSystem.current.currentSelectedGameObject == dialogue.choiceMashaButton.gameObject,
            "The first visible choice did not receive EventSystem focus.");
        Capture("gameplay_choice_focus_1920x1080.png", "PrepareBacklog");
    }

    private static void PrepareBacklog()
    {
        VNDialogueController dialogue = RequireGameplayDialogue();
        List<DialogueLine> lines = new List<DialogueLine>();
        for (int i = 1; i <= 12; i++)
        {
            lines.Add(new DialogueLine
            {
                lineId = "history_" + i,
                speaker = i % 3 == 0 ? string.Empty : "Рассказчик",
                text = "Нейтральная строка истории " + i + " проверяет читаемый перенос и вертикальную навигацию."
            });
        }

        LoadRuntimeFixture(dialogue, lines, new List<DialogueChoice>());
        for (int i = 1; i < lines.Count; i++)
        {
            CompleteTyping(dialogue);
            dialogue.AdvanceDialogue();
        }

        CompleteTyping(dialogue);
        dialogue.ShowBacklog();
        Capture("gameplay_backlog_1920x1080.png", "PrepareAuto");
    }

    private static void PrepareAuto()
    {
        VNDialogueController dialogue = RequireGameplayDialogue();
        dialogue.HideBacklog();
        LoadRuntimeFixture(dialogue, "Авто остаётся активным, но не меняет эту строку до истечения существующей задержки.", new List<DialogueChoice>());
        CompleteTyping(dialogue);
        dialogue.SetAutoForward(true);
        Require(dialogue.IsAutoForwardEnabledState, "Auto did not become active for the reading proof.");
        Capture("gameplay_auto_active_1920x1080.png", "PrepareSkip");
    }

    private static void PrepareSkip()
    {
        VNDialogueController dialogue = RequireGameplayDialogue();
        dialogue.SetAutoForward(false);
        LoadRuntimeFixture(dialogue, "Пропуск включён и отображается до того, как существующий таймер продолжит чтение.", new List<DialogueChoice>());
        CompleteTyping(dialogue);
        SettingsManager.Instance.settings.skipMode = "Всё";
        dialogue.SetSkip(true);
        InvokePrivate(dialogue, "StopSkipTimer");
        Require(dialogue.IsSkipEnabled, "Skip did not become active for the reading proof.");
        Capture("gameplay_skip_active_1920x1080.png", "PrepareHideUi");
    }

    private static void PrepareHideUi()
    {
        VNDialogueController dialogue = RequireGameplayDialogue();
        dialogue.SetSkip(false);
        SettingsManager.Instance.settings.skipMode = "Виденное";
        Require(dialogue.TryHideInterface(), "Hide UI did not enter clean view from a stable reading state.");
        Capture("gameplay_hide_ui_1920x1080.png", "RestoreAfterHideUi");
    }

    private static void RestoreAfterHideUi()
    {
        VNDialogueController dialogue = RequireGameplayDialogue();
        dialogue.RestoreInterface();
        SettingsManager.Instance.SetDialogueTextScale(1f);
        if (!dialogue.IsGameMenuOpen) dialogue.OpenGameMenu();
        if (!dialogue.IsGameMenuOpen) { Retry("Gameplay menu did not open."); return; }
        VNGameMenuView view = dialogue.GameMenuController != null ? dialogue.GameMenuController.View : null;
        Require(view != null, "Gameplay menu view is missing.");
        VNQuickMenu quickMenu = UnityEngine.Object.FindFirstObjectByType<VNQuickMenu>(FindObjectsInactive.Include);
        Require(!dialogue.dialogueUiRoot.activeSelf, "Game Menu did not suppress the dialogue shell.");
        Require(quickMenu != null && !quickMenu.IsEffectivelyVisible, "Game Menu did not suppress the Quick Menu.");
        Require(!view.IsConfirmationVisible, "Game Menu root unexpectedly opened a confirmation.");
        Require(EventSystem.current != null
            && EventSystem.current.currentSelectedGameObject == view.GetButton(VNGameMenuAction.Return).gameObject,
            "Game Menu did not assign its deterministic Return focus.");
        Require(IsFocusMarkerVisible(view, VNGameMenuAction.Return),
            "Game Menu default Return focus has no visible focus marker.");
        Capture("game_menu_root_1920x1080.png", "CaptureGameMenuAlternateFocus");
    }

    private static void CaptureGameMenuAlternateFocus()
    {
        VNDialogueController dialogue = RequireGameplayDialogue();
        VNGameMenuView view = dialogue.GameMenuController != null ? dialogue.GameMenuController.View : null;
        Button preferences = view != null ? view.GetButton(VNGameMenuAction.Preferences) : null;
        Require(preferences != null && preferences.isActiveAndEnabled && preferences.interactable,
            "Game Menu Preferences action is unavailable for focus proof.");
        preferences.Select();
        Require(EventSystem.current != null && EventSystem.current.currentSelectedGameObject == preferences.gameObject,
            "Game Menu alternate focus did not select Preferences.");
        Require(IsFocusMarkerVisible(view, VNGameMenuAction.Preferences)
            && !IsFocusMarkerVisible(view, VNGameMenuAction.Return),
            "Game Menu focus marker did not move to Preferences.");
        Capture("game_menu_alternate_focus_1920x1080.png", "OpenGameplayPreferences");
    }

    private static void OpenGameplayPreferences()
    {
        VNDialogueController dialogue = RequireGameplayDialogue();
        VNGameMenuView view = dialogue.GameMenuController != null ? dialogue.GameMenuController.View : null;
        Require(view != null, "Game Menu view disappeared before opening Preferences.");
        view.GetButton(VNGameMenuAction.Preferences)?.onClick.Invoke();
        SessionState.SetString(StageKey, "WaitGameplayPreferences");
        ResetCounter();
        SetDelay(0.5d);
    }

    private static bool IsFocusMarkerVisible(VNGameMenuView view, VNGameMenuAction action)
    {
        Transform marker = view != null ? view.GetButton(action)?.transform.Find("Focus Marker") : null;
        return marker != null && marker.gameObject.activeSelf;
    }

    private static bool HasPersistentRoute(Button button, string methodName)
    {
        if (button == null) return false;
        for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
        {
            if (button.onClick.GetPersistentMethodName(index) == methodName)
                return true;
        }
        return false;
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

        Capture("gameplay_preferences_1920x1080.png", "PrepareResponsivePreferences");
    }

    private static void PrepareResponsivePreferences()
    {
        ConfigureGameViewResolution(ResponsiveQaResolution);
        SessionState.SetString(StageKey, "CaptureResponsivePreferences");
        ResetCounter();
        SetDelay(0.4d);
    }

    private static void CaptureResponsivePreferences()
    {
        if (Screen.width != ResponsiveQaResolution.x || Screen.height != ResponsiveQaResolution.y)
        {
            Retry("Game View did not switch to 1280x720 for Preferences responsive proof.");
            return;
        }
        Capture("gameplay_preferences_1280x720.png", "Complete");
    }

    private static void Capture(string fileName, string nextStage)
    {
        Require(Screen.width > 0 && Screen.height > 0, "Capture requires a valid Game View size.");
        string path = Path.Combine(Directory.GetCurrentDirectory(), "QAArtifacts", "GraphicalE2E", "PlayerUi", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        SessionState.SetString(CapturePathKey, path);
        // Screen.height can reflect the embedded Game View viewport (for
        // example 951px) while CaptureScreenshot writes the configured
        // standalone target (1080px). Validate against the QA target rather
        // than the editor chrome-adjusted viewport size.
        Vector2Int captureTarget = Screen.width >= QaResolution.x
            ? QaResolution
            : ResponsiveQaResolution;
        SessionState.SetInt(CaptureWidthKey, captureTarget.x);
        SessionState.SetInt(CaptureHeightKey, captureTarget.y);
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
        VerifyImageDimensions(path, SessionState.GetInt(CaptureWidthKey, QaResolution.x), SessionState.GetInt(CaptureHeightKey, QaResolution.y));
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
        WriteResult("PASS", "all required PlayerUi screenshots captured, including 1280x720 Preferences");
        DestroyRuntimeFixtures();
        CleanupTestDirectory();
        SessionState.SetString(StageKey, "ExitSuccess");
        EditorApplication.isPlaying = false;
    }

    private static void Fail(string details)
    {
        WriteResult("FAIL", details);
        DestroyRuntimeFixtures();
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

    private static VNDialogueController RequireGameplayDialogue()
    {
        VNDialogueController dialogue = VNDialogueController.Instance;
        Require(dialogue != null && dialogue.IsRuntimeReady, "Gameplay dialogue runtime is unavailable for reading proof.");
        return dialogue;
    }

    private static void LoadRuntimeFixture(VNDialogueController dialogue, string text, List<DialogueChoice> choices)
    {
        LoadRuntimeFixture(dialogue, new List<DialogueLine>
        {
            new DialogueLine { lineId = "reading_1", speaker = string.Empty, text = text }
        }, choices);
    }

    private static void LoadRuntimeFixture(VNDialogueController dialogue, List<DialogueLine> lines, List<DialogueChoice> choices)
    {
        DialogueSceneData fixture = ScriptableObject.CreateInstance<DialogueSceneData>();
        fixture.hideFlags = HideFlags.DontSave;
        fixture.sceneId = "TECH_DEMO_CORE_READING"; // transient runtime fixture; never an authored DialogueSceneData asset.
        fixture.lines = lines;
        fixture.choices = choices;
        RuntimeFixtures.Add(fixture);
        InvokePrivate(dialogue, "LoadDialogueScene", fixture, 0, false);
        CompleteTyping(dialogue);
    }

    private static void CompleteTyping(VNDialogueController dialogue)
    {
        InvokePrivate(dialogue, "CompleteTyping");
    }

    private static void InvokePrivate(VNDialogueController dialogue, string methodName, params object[] arguments)
    {
        MethodInfo method = null;
        foreach (MethodInfo candidate in typeof(VNDialogueController).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            if (candidate.Name == methodName && candidate.GetParameters().Length == arguments.Length)
            {
                method = candidate;
                break;
            }
        }

        Require(method != null, "VNDialogueController private method is missing: " + methodName);
        method.Invoke(dialogue, arguments);
    }

    private static void DestroyRuntimeFixtures()
    {
        foreach (UnityEngine.Object fixture in RuntimeFixtures)
        {
            if (fixture != null)
            {
                UnityEngine.Object.Destroy(fixture);
            }
        }

        RuntimeFixtures.Clear();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
