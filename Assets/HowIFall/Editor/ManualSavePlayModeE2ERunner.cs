using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ManualSavePlayModeE2ERunner
{
    private const string ActiveKey = "HowIFall.SaveE2E.Active";
    private const string StageKey = "HowIFall.SaveE2E.Stage";
    private const string NextTimeKey = "HowIFall.SaveE2E.NextTime";
    private const string CounterKey = "HowIFall.SaveE2E.Counter";
    private const string SnapshotKey = "HowIFall.SaveE2E.Snapshot";
    private const string FutureSnapshotKey = "HowIFall.SaveE2E.FutureSnapshot";
    private const string InitialCreatedAtKey = "HowIFall.SaveE2E.InitialCreatedAt";
    private const string ErrorsKey = "HowIFall.SaveE2E.Errors";
    private const string DirectoryKey = "HowIFall.SaveE2E.Directory";
    private const string ResultPath = "manual_save_playmode_result.txt";
    private static readonly Vector2Int QaResolution = new Vector2Int(1920, 1080);

    static ManualSavePlayModeE2ERunner()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        Application.logMessageReceived -= CaptureLog;
        Application.logMessageReceived += CaptureLog;
    }

    [MenuItem("How I Fall/Tests/Run Manual Save Play Mode E2E")]
    public static void StartAutomatedPlayMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("Play Mode is already active.");
        }

        string resultPath = Path.Combine(Directory.GetCurrentDirectory(), ResultPath);
        if (File.Exists(resultPath))
        {
            File.Delete(resultPath);
        }

        CleanupTestDirectory();
        string directory = Path.Combine(
            Path.GetTempPath(),
            "HowIFall_ManualSaveE2E_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        ManualSaveSystemSceneInstaller.ValidateInstalledScenes();
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetString(StageKey, "WaitMainNewGame");
        SessionState.SetInt(CounterKey, 0);
        SessionState.SetString(SnapshotKey, string.Empty);
        SessionState.SetString(FutureSnapshotKey, string.Empty);
        SessionState.SetString(InitialCreatedAtKey, string.Empty);
        SessionState.SetString(ErrorsKey, string.Empty);
        SessionState.SetString(DirectoryKey, directory);
        SetDelay(1.0d);

        EditorSceneManager.OpenScene("Assets/HowIFall/Scenes/MainMenu.unity", OpenSceneMode.Single);
        Debug.Log("[PLAY E2E] START: entering Play Mode from MainMenu.");
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(ActiveKey, false)
            || !EditorApplication.isPlaying
            || EditorApplication.timeSinceStartup < SessionState.GetFloat(NextTimeKey, 0f))
        {
            return;
        }

        try
        {
            RunStage(SessionState.GetString(StageKey, string.Empty));
        }
        catch (Exception exception)
        {
            Fail(exception.ToString());
        }
    }

    private static void RunStage(string stage)
    {
        switch (stage)
        {
            case "WaitMainNewGame":
                WaitMainAndStartNewGame();
                break;
            case "WaitVnNewGame":
                WaitForNewVn();
                break;
            case "AdvanceToChoice":
                AdvanceToChoice();
                break;
            case "ChooseAndSave":
                ChooseAndSave();
                break;
            case "WaitInitialSave":
                WaitInitialSave();
                break;
            case "SetUiResolution":
                SetUiResolution();
                break;
            case "CaptureUiResolution":
                CaptureUiResolution();
                break;
            case "WaitUiScreenshot":
                WaitUiScreenshot();
                break;
            case "AdvanceAfterSave":
                AdvanceAfterSave();
                break;
            case "LoadInsideVn":
                LoadInsideVn();
                break;
            case "VerifyInsideVn":
                VerifyInsideVn();
                break;
            case "WaitMainForSlotLoad":
                WaitMainForSlotLoad();
                break;
            case "WaitVnFromMainLoad":
                WaitVnFromMainLoad();
                break;
            case "WaitMainBeforeRestart":
                WaitMainBeforeRestart();
                break;
            case "WaitMainContinue":
                WaitMainContinue();
                break;
            case "WaitVnContinue":
                WaitVnContinue();
                break;
            case "EscapeConfirmation":
                EscapeConfirmation();
                break;
            case "WaitEscapePanelClosed":
                WaitEscapePanelClosed();
                break;
            case "OverwriteCancel":
                OverwriteCancel();
                break;
            case "OverwriteConfirm":
                OverwriteConfirm();
                break;
            case "WaitOverwrite":
                WaitOverwrite();
                break;
            case "DeleteCancel":
                DeleteCancel();
                break;
            case "DeleteConfirm":
                DeleteConfirm();
                break;
            case "WaitDelete":
                WaitDelete();
                break;
            case "WaitMainAfterDelete":
                WaitMainAfterDelete();
                break;
        }
    }

    private static void WaitMainAndStartNewGame()
    {
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            SetDelay(0.25d);
            return;
        }

        MainMenuController menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
        SaveManager manager = SaveManager.Instance;
        Require(menu != null, "MainMenuController was not created in Play Mode.");
        Require(manager != null, "SaveManager was not created in MainMenu.");
        manager.ConfigureSaveDirectoryForTests(SessionState.GetString(DirectoryKey, string.Empty));
        Require(UnityEngine.Object.FindObjectsByType<SaveManager>(FindObjectsSortMode.None).Length == 1, "MainMenu contains more than one runtime SaveManager.");
        Require(!manager.GetSlot(1).IsOccupied, "Slot 1 is not empty at clean test start.");

        menu.manualSaveLoadPanel.OpenLoad();
        Require(menu.manualSaveLoadPanel.slotViews.Length == SaveManager.SlotCount, "Load panel does not contain six slots.");
        Require(!menu.manualSaveLoadPanel.slotViews[1].button.interactable, "Empty slot is interactable in Load mode.");
        menu.manualSaveLoadPanel.Close();
        Pass("Empty slot and six-slot Main Menu UI");

        SessionState.SetString(StageKey, "WaitVnNewGame");
        SetDelay(0.75d);
        menu.StartGame();
    }

    private static void WaitForNewVn()
    {
        VNDialogueController controller = VNDialogueController.Instance;
        if (SceneManager.GetActiveScene().name != SaveManager.GameplaySceneName
            || controller == null
            || GameState.Instance == null
            || string.IsNullOrEmpty(GameState.Instance.currentLineId))
        {
            SetDelay(0.25d);
            return;
        }

        Require(UnityEngine.Object.FindObjectsByType<SaveManager>(FindObjectsSortMode.None).Length == 1, "VNPrototype contains more than one runtime SaveManager.");
        Require(GameState.Instance.selectedChoiceIndex == -1, "New Game inherited a previous choice.");
        Require(controller.CaptureBacklogSnapshot().Count == 1, "New Game did not begin with only its first displayed line in History.");
        Pass("New Game opened VNPrototype with one SaveManager");
        SessionState.SetString(StageKey, "AdvanceToChoice");
        SessionState.SetInt(CounterKey, 0);
        SetDelay(0.1d);
    }

    private static void AdvanceToChoice()
    {
        VNDialogueController controller = VNDialogueController.Instance;
        Require(controller != null, "VNDialogueController disappeared while advancing.");

        if (controller.choicePanel != null && controller.choicePanel.activeSelf)
        {
            SessionState.SetString(StageKey, "ChooseAndSave");
            SetDelay(0.2d);
            return;
        }

        int attempts = SessionState.GetInt(CounterKey, 0) + 1;
        SessionState.SetInt(CounterKey, attempts);
        Require(attempts < 80, "Choice did not appear after 80 advance attempts.");
        controller.AdvanceDialogue();
        SetDelay(0.08d);
    }

    private static void ChooseAndSave()
    {
        VNDialogueController controller = VNDialogueController.Instance;
        Require(controller != null && controller.choiceMashaButton != null, "First choice button is unavailable.");
        controller.choiceMashaButton.onClick.Invoke();
        Require(GameState.Instance.selectedChoiceIndex == 0, "Choice index was not stored.");
        Require(GameState.Instance.choiceResultActive, "Choice result state was not stored.");
        Require(GameState.Instance.suspicion == 1 && GameState.Instance.trustMasha == 1, "Choice did not update GameState relations.");
        Pass("Choice changed GameState");

        ManualSaveLoadPanel panel = controller.manualSaveLoadPanel;
        Require(panel != null, "VN manual Save/Load panel is missing.");
        panel.OpenSave();
        Require(panel.slotViews[0].button.interactable, "Empty slot is not interactable in Save mode.");
        panel.slotViews[0].button.onClick.Invoke();

        SessionState.SetString(StageKey, "WaitInitialSave");
        SetDelay(0.25d);
    }

    private static void WaitInitialSave()
    {
        SaveManager manager = SaveManager.Instance;
        SaveSlotInfo slot = manager?.GetSlot(1);
        if (slot == null || !slot.IsLoadable || !File.Exists(manager.GetSlotPreviewPath(1)))
        {
            int attempts = SessionState.GetInt(CounterKey, 0) + 1;
            SessionState.SetInt(CounterKey, attempts);
            Require(attempts < 120, "Slot 1 was not written with JSON and PNG.");
            SetDelay(0.1d);
            return;
        }

        Require(Path.GetFileName(slot.JsonPath) == "slot_01.json", "Unexpected JSON file name.");
        Require(Path.GetFileName(slot.PreviewPath) == "slot_01.png", "Unexpected preview file name.");
        VerifyPreviewDimensions(slot.PreviewPath);
        VerifyOccupiedCard(VNDialogueController.Instance.manualSaveLoadPanel, slot);
        Require(slot.Data.version == 3 && slot.Data.backlogSnapshotAvailable, "Manual save did not persist a v3 backlog snapshot.");
        Require(slot.Data.backlogEntries != null && slot.Data.backlogEntries.Count > 0, "Manual save backlog snapshot is empty.");
        string savedResultText = slot.Data.backlogEntries[slot.Data.backlogEntries.Count - 1].text;
        Require(slot.Data.backlogEntries.Count(entry => entry != null && entry.text == savedResultText) == 1,
            "Choice result was duplicated before the Manual save was written.");
        SessionState.SetString(SnapshotKey, JsonUtility.ToJson(slot.Data));
        SessionState.SetString(InitialCreatedAtKey, slot.Data.createdAtUtc);
        Pass("Slot 1 JSON and screenshot written");

        SessionState.SetString(StageKey, "SetUiResolution");
        SessionState.SetInt(CounterKey, 0);
        SetDelay(0.2d);
    }

    private static void SetUiResolution()
    {
        Vector2Int resolution = QaResolution;
        ConfigureGameViewResolution(resolution);
        SessionState.SetInt(CounterKey, 0);
        SessionState.SetString(StageKey, "CaptureUiResolution");
        SetDelay(0.5d);
    }

    private static void CaptureUiResolution()
    {
        Vector2Int resolution = QaResolution;
        if (Screen.width != resolution.x || Screen.height != resolution.y)
        {
            int attempts = SessionState.GetInt(CounterKey, 0) + 1;
            SessionState.SetInt(CounterKey, attempts);
            Require(attempts < 40, $"Game View did not switch to {resolution.x}x{resolution.y}; actual {Screen.width}x{Screen.height}.");
            ConfigureGameViewResolution(resolution);
            SetDelay(0.2d);
            return;
        }

        ManualSaveLoadPanel panel = VNDialogueController.Instance.manualSaveLoadPanel;
        Require(panel != null && panel.IsOpen, "Save panel closed before UI screenshot capture.");
        VerifyPanelLayout(panel, resolution);

        string path = GetUiScreenshotPath(resolution);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        // The open panel and its layout were verified above; Unity captures this queued screenshot at frame end.
        ScreenCapture.CaptureScreenshot(path);
        SessionState.SetInt(CounterKey, 0);
        SessionState.SetString(StageKey, "WaitUiScreenshot");
        SetDelay(0.25d);
    }

    private static void WaitUiScreenshot()
    {
        Vector2Int resolution = QaResolution;
        string path = GetUiScreenshotPath(resolution);
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            int attempts = SessionState.GetInt(CounterKey, 0) + 1;
            SessionState.SetInt(CounterKey, attempts);
            Require(attempts < 80, $"UI screenshot was not written for {resolution.x}x{resolution.y}.");
            SetDelay(0.1d);
            return;
        }

        VerifyImageDimensions(path, resolution.x, resolution.y, "UI screenshot");
        Pass($"Save UI layout and screenshot {resolution.x}x{resolution.y}");

        ConfigureGameViewResolution(QaResolution);
        VNDialogueController.Instance.manualSaveLoadPanel.Close();
        SessionState.SetString(StageKey, "AdvanceAfterSave");
        SessionState.SetInt(CounterKey, 0);
        SetDelay(0.3d);
    }

    private static void AdvanceAfterSave()
    {
        int count = SessionState.GetInt(CounterKey, 0);
        if (count < 5)
        {
            VNDialogueController.Instance.AdvanceDialogue();
            SessionState.SetInt(CounterKey, count + 1);
            SetDelay(0.1d);
            return;
        }

        SaveData snapshot = ReadSnapshot();
        bool changed = GameState.Instance.currentSceneId != snapshot.sceneId
            || GameState.Instance.currentLineId != snapshot.lineId
            || !GameState.Instance.choiceResultActive;
        Require(changed, "Dialogue did not move beyond the saved position.");
        SessionState.SetString(FutureSnapshotKey, JsonUtility.ToJson(CaptureCurrentSnapshot()));
        SessionState.SetString(StageKey, "LoadInsideVn");
        SetDelay(0.1d);
    }

    private static void LoadInsideVn()
    {
        VNDialogueController controller = VNDialogueController.Instance;
        Require(controller.TryGetSavePosition(out string sceneId, out string lineId, out int lineIndex, out string positionError),
            $"Terminal VN position is unavailable before Manual Load: {positionError}");
        Require(sceneId == "classroom_choice_investigate", "Terminal VN scene is not classroom_choice_investigate before Manual Load.");
        DialogueSceneData terminalScene = controller.sceneRegistry.FindById(sceneId);
        Require(terminalScene != null && terminalScene.lines != null && terminalScene.lines.Count > 0,
            "Terminal VN scene has no real dialogue lines before Manual Load.");
        int lastValidLineIndex = terminalScene.lines.Count - 1;
        Require(lineIndex == lastValidLineIndex && lineId == terminalScene.lines[lastValidLineIndex].lineId,
            "Terminal VN save position does not remain on the last real dialogue line before Manual Load.");

        ManualSaveLoadPanel panel = controller.manualSaveLoadPanel;
        panel.OpenLoad();
        Require(panel.slotViews[0].button.interactable, "Occupied slot is not interactable in Load mode.");
        panel.slotViews[0].button.onClick.Invoke();
        Require(panel.IsConfirmationOpen, "VN Load did not open confirmation.");
        Require(panel.confirmationText.text == "\u0417\u0430\u0433\u0440\u0443\u0437\u0438\u0442\u044c \u044d\u0442\u043e \u0441\u043e\u0445\u0440\u0430\u043d\u0435\u043d\u0438\u0435? \u041d\u0435\u0441\u043e\u0445\u0440\u0430\u043d\u0451\u043d\u043d\u044b\u0439 \u043f\u0440\u043e\u0433\u0440\u0435\u0441\u0441 \u0431\u0443\u0434\u0435\u0442 \u043f\u043e\u0442\u0435\u0440\u044f\u043d.", "VN Load confirmation text is incorrect.");
        Require(GetButtonLabel(panel.confirmationYesButton) == "\u0417\u0430\u0433\u0440\u0443\u0437\u0438\u0442\u044c", "VN Load confirmation action label is incorrect.");
        panel.confirmationYesButton.onClick.Invoke();
        SessionState.SetString(StageKey, "VerifyInsideVn");
        SetDelay(0.3d);
    }

    private static void VerifyInsideVn()
    {
        VerifySnapshot("in-place VN load");
        SaveManager manager = SaveManager.Instance;
        SaveData futureSnapshot = ReadSnapshot(FutureSnapshotKey);
        SaveSlotInfo preLoadCheckpoint = manager.GetAllSlots(SaveSlotType.Auto)
            .Where(slot => slot.IsLoadable)
            .OrderByDescending(slot => slot.CreatedAtUtc)
            .FirstOrDefault();
        Require(preLoadCheckpoint != null, "Manual Load did not create a pre-load Auto checkpoint.");
        VerifySnapshotData(futureSnapshot, preLoadCheckpoint.Data, "Manual pre-load Auto checkpoint");

        SaveData savedSnapshot = ReadSnapshot();
        string expectedNextSceneId = savedSnapshot.pendingNextSceneId;
        VNDialogueController.Instance.AdvanceDialogue();
        if (GameState.Instance.choiceResultActive)
        {
            // The first click may only finish resultText typewriting.
            VNDialogueController.Instance.AdvanceDialogue();
        }
        Require(!string.IsNullOrEmpty(expectedNextSceneId)
                && GameState.Instance.currentSceneId == expectedNextSceneId
                && !GameState.Instance.choiceResultActive,
            "Advance after restored choice result did not enter the saved pending scene.");
        Require(GameState.Instance.suspicion == savedSnapshot.suspicion
                && GameState.Instance.trustMasha == savedSnapshot.trustMasha,
            "Advance after restored choice result reapplied choice effects.");

        Require(manager.LoadSlot(SaveSlotType.Auto, preLoadCheckpoint.SlotIndex),
            "Loading the Manual pre-load Auto checkpoint failed.");
        VerifyRuntimeSnapshot(futureSnapshot, "Manual pre-load Auto checkpoint restore");
        Pass("In-place Manual Load restored choice History without duplicates, advanced correctly, and restored pre-load History");
        SessionState.SetString(StageKey, "WaitMainForSlotLoad");
        SetDelay(0.75d);
        SceneFlowManager.EnsureInstance().ReturnToMainMenu();
    }

    private static void WaitMainForSlotLoad()
    {
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            SetDelay(0.25d);
            return;
        }

        MainMenuController menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
        Require(menu != null, "MainMenuController missing after return from VN.");
        menu.OpenManualLoad();
        Require(menu.manualSaveLoadPanel.slotViews[0].button.interactable, "Slot 1 is disabled in Main Menu Load mode.");
        menu.manualSaveLoadPanel.slotViews[0].button.onClick.Invoke();
        SessionState.SetString(StageKey, "WaitVnFromMainLoad");
        SetDelay(0.75d);
    }

    private static void WaitVnFromMainLoad()
    {
        if (SceneManager.GetActiveScene().name != SaveManager.GameplaySceneName || VNDialogueController.Instance == null)
        {
            SetDelay(0.25d);
            return;
        }

        VerifySnapshot("Main Menu slot load");
        Pass("Main Menu slot load restored line, choice and GameState");
        DeleteAllAutoTestSlots();
        SessionState.SetString(StageKey, "WaitMainBeforeRestart");
        SetDelay(0.75d);
        SceneFlowManager.EnsureInstance().ReturnToMainMenu();
    }

    private static void WaitMainBeforeRestart()
    {
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            SetDelay(0.25d);
            return;
        }

        SessionState.SetString(StageKey, "RestartRequested");
        Debug.Log("[PLAY E2E] Restarting Play Mode before Continue test.");
        EditorApplication.isPlaying = false;
    }

    private static void WaitMainContinue()
    {
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            SetDelay(0.25d);
            return;
        }

        MainMenuController menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
        if (menu == null || SaveManager.Instance == null)
        {
            SetDelay(0.25d);
            return;
        }

        SaveManager.Instance.ConfigureSaveDirectoryForTests(SessionState.GetString(DirectoryKey, string.Empty));
        Require(SaveManager.Instance.HasAnyValidSave(), "Continue found no valid save after Play Mode restart.");
        Require(menu.continueButton != null && menu.continueButton.interactable, "Continue button is disabled despite valid slot 1.");
        menu.continueButton.onClick.Invoke();
        SessionState.SetString(StageKey, "WaitVnContinue");
        SetDelay(0.75d);
    }

    private static void WaitVnContinue()
    {
        if (SceneManager.GetActiveScene().name != SaveManager.GameplaySceneName || VNDialogueController.Instance == null)
        {
            SetDelay(0.25d);
            return;
        }

        VerifySnapshot("Continue after Play Mode restart");
        Pass("Continue restored newest valid save after Play Mode restart");
        SessionState.SetString(StageKey, "EscapeConfirmation");
        SetDelay(0.2d);
    }

    private static void EscapeConfirmation()
    {
        VNDialogueController controller = VNDialogueController.Instance;
        ManualSaveLoadPanel panel = controller.manualSaveLoadPanel;
        Require(controller.TryGetSavePosition(out string beforeScene, out string beforeLine, out int beforeIndex, out string beforeError), $"Position unavailable before opening Save UI: {beforeError}");

        panel.OpenSave();
        Require(controller.TryGetSavePosition(out string afterScene, out string afterLine, out int afterIndex, out string afterError), $"Position unavailable after opening Save UI: {afterError}");
        Require(beforeScene == afterScene && beforeLine == afterLine && beforeIndex == afterIndex, "Opening Save UI advanced the dialogue.");
        panel.slotViews[0].button.onClick.Invoke();
        Require(panel.IsConfirmationOpen, "Occupied slot did not open confirmation for Escape test.");
        Require(panel.contentCanvasGroup != null && !panel.contentCanvasGroup.interactable, "Main Save UI remained interactive under confirmation.");

        Require(panel.HandleEscape(), "First Escape was not handled by Save UI.");
        Require(!panel.IsConfirmationOpen, "First Escape did not close confirmation.");
        Require(panel.IsOpen, "First Escape closed the whole Save UI together with confirmation.");

        Require(panel.HandleEscape(), "Second Escape was not handled by Save UI.");
        SessionState.SetString(StageKey, "WaitEscapePanelClosed");
        SessionState.SetInt(CounterKey, 0);
        SetDelay(0.05d);
    }

    private static void WaitEscapePanelClosed()
    {
        ManualSaveLoadPanel panel = VNDialogueController.Instance.manualSaveLoadPanel;
        if (panel.IsOpen)
        {
            int attempts = SessionState.GetInt(CounterKey, 0) + 1;
            SessionState.SetInt(CounterKey, attempts);
            Require(attempts < 40, "Second Escape did not close Save UI after fade.");
            SetDelay(0.05d);
            return;
        }

        Pass("Escape closed confirmation first and Save UI second without advancing dialogue");
        SessionState.SetString(StageKey, "OverwriteCancel");
        SetDelay(0.15d);
    }

    private static void OverwriteCancel()
    {
        ManualSaveLoadPanel panel = VNDialogueController.Instance.manualSaveLoadPanel;
        panel.OpenSave();
        panel.slotViews[0].button.onClick.Invoke();
        Require(panel.confirmationRoot != null && panel.confirmationRoot.activeSelf, "Occupied slot did not open overwrite confirmation.");
        panel.confirmationNoButton.onClick.Invoke();
        Require(!panel.confirmationRoot.activeSelf, "No did not close overwrite confirmation.");
        Require(SaveManager.Instance.GetSlot(1).Data.createdAtUtc == SessionState.GetString(InitialCreatedAtKey, string.Empty), "No changed the occupied slot.");
        Pass("Overwrite confirmation No preserved the slot");
        SessionState.SetString(StageKey, "OverwriteConfirm");
        SetDelay(0.2d);
    }

    private static void OverwriteConfirm()
    {
        ManualSaveLoadPanel panel = VNDialogueController.Instance.manualSaveLoadPanel;
        panel.slotViews[0].button.onClick.Invoke();
        Require(panel.confirmationRoot.activeSelf, "Second overwrite attempt did not open confirmation.");
        panel.confirmationYesButton.onClick.Invoke();
        SessionState.SetString(StageKey, "WaitOverwrite");
        SessionState.SetInt(CounterKey, 0);
        SetDelay(0.25d);
    }

    private static void WaitOverwrite()
    {
        SaveSlotInfo slot = SaveManager.Instance.GetSlot(1);
        string previousTime = SessionState.GetString(InitialCreatedAtKey, string.Empty);
        if (!slot.IsLoadable || slot.Data.createdAtUtc == previousTime)
        {
            int attempts = SessionState.GetInt(CounterKey, 0) + 1;
            SessionState.SetInt(CounterKey, attempts);
            Require(attempts < 120, "Confirmed overwrite did not update slot 1.");
            SetDelay(0.1d);
            return;
        }

        VerifyPreviewDimensions(slot.PreviewPath);
        Pass("Overwrite confirmation Yes replaced JSON and PNG");
        SessionState.SetString(StageKey, "DeleteCancel");
        SetDelay(0.2d);
    }

    private static void DeleteCancel()
    {
        ManualSaveLoadPanel panel = VNDialogueController.Instance.manualSaveLoadPanel;
        ManualSaveSlotView slotView = panel.slotViews[0];
        string jsonPath = SaveManager.Instance.GetSlotJsonPath(1);
        string previewPath = SaveManager.Instance.GetSlotPreviewPath(1);
        panel.OpenSave();

        Require(slotView.deleteButton != null, "Slot 1 has no delete button.");
        Require(slotView.deleteButton.gameObject.activeSelf && slotView.deleteButton.interactable, "Occupied slot does not show an active delete button.");
        ClickButtonThroughEventSystem(slotView.deleteButton);
        Require(panel.confirmationRoot.activeSelf, "Delete did not open confirmation.");
        Require(panel.confirmationText.text == "Удалить сохранение из слота 1?", "Delete confirmation text is incorrect.");
        Require(GetButtonLabel(panel.confirmationYesButton) == "Удалить", "Delete confirmation action label is incorrect.");
        Require(GetButtonLabel(panel.confirmationNoButton) == "Отмена", "Delete confirmation cancel label is incorrect.");

        panel.confirmationNoButton.onClick.Invoke();
        Require(!panel.confirmationRoot.activeSelf, "Delete Cancel did not close confirmation.");
        Require(File.Exists(jsonPath), "Delete Cancel removed the JSON file.");
        Require(File.Exists(previewPath), "Delete Cancel removed the PNG file.");
        Require(SaveManager.Instance.GetSlot(1).IsLoadable, "Delete Cancel changed slot 1.");
        Pass("Delete confirmation Cancel preserved JSON and PNG");
        SessionState.SetString(StageKey, "DeleteConfirm");
        SetDelay(0.2d);
    }

    private static void DeleteConfirm()
    {
        ManualSaveLoadPanel panel = VNDialogueController.Instance.manualSaveLoadPanel;
        ClickButtonThroughEventSystem(panel.slotViews[0].deleteButton);
        Require(panel.confirmationRoot.activeSelf, "Second Delete did not open confirmation.");
        Require(panel.confirmationText.text == "Удалить сохранение из слота 1?", "Second Delete opened the wrong confirmation action.");
        panel.confirmationYesButton.onClick.Invoke();
        SessionState.SetString(StageKey, "WaitDelete");
        SetDelay(0.1d);
    }

    private static void WaitDelete()
    {
        SaveManager manager = SaveManager.Instance;
        ManualSaveLoadPanel panel = VNDialogueController.Instance.manualSaveLoadPanel;
        ManualSaveSlotView slotView = panel.slotViews[0];
        string jsonPath = manager.GetSlotJsonPath(1);
        string previewPath = manager.GetSlotPreviewPath(1);

        Require(!File.Exists(jsonPath), "Confirmed Delete left the JSON file.");
        Require(!File.Exists(previewPath), "Confirmed Delete left the PNG file.");
        Require(!File.Exists(jsonPath + ".tmp"), "Confirmed Delete left the JSON temporary file.");
        Require(!File.Exists(previewPath + ".tmp"), "Confirmed Delete left the PNG temporary file.");
        Require(!manager.GetSlot(1).IsOccupied, "Deleted slot is still occupied.");
        Require(panel.statusText.text == "Слот 1 удалён", "Delete success status is incorrect.");
        Require(slotView.emptyText.gameObject.activeSelf && slotView.emptyText.text == "Пустой слот", "Deleted card did not become empty.");
        Require(!slotView.deleteButton.gameObject.activeSelf, "Empty slot still shows the delete button.");

        DeleteAllAutoTestSlots();

        panel.OpenLoad();
        Require(!slotView.button.interactable, "Deleted slot is interactable in Load mode.");
        Require(!slotView.deleteButton.gameObject.activeSelf, "Deleted slot shows Delete in Load mode.");
        Pass("Delete removed files and refreshed the slot in Load mode");

        panel.Close();
        SessionState.SetString(StageKey, "WaitMainAfterDelete");
        SetDelay(0.75d);
        SceneFlowManager.EnsureInstance().ReturnToMainMenu();
    }

    private static void WaitMainAfterDelete()
    {
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            SetDelay(0.25d);
            return;
        }

        MainMenuController menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
        if (menu == null || SaveManager.Instance == null)
        {
            SetDelay(0.25d);
            return;
        }

        // The explicit post-choice advance used by backlog E2E requests a normal
        // scene-transition autosave. Remove that test artifact after it settles.
        DeleteAllAutoTestSlots();
        Require(!SaveManager.Instance.HasAnyValidSave(), "Continue still finds a save after deleting the last valid slot.");
        menu.RefreshContinueAvailability();
        Require(menu.continueButton != null && !menu.continueButton.interactable, "Continue is enabled after deleting the last valid slot.");
        menu.manualSaveLoadPanel.OpenLoad();
        Require(!menu.manualSaveLoadPanel.slotViews[0].button.interactable, "Deleted Main Menu slot is interactable in Load mode.");
        Require(!menu.manualSaveLoadPanel.slotViews[0].deleteButton.gameObject.activeSelf, "Deleted Main Menu slot still shows Delete.");
        Pass("Deleting the last valid slot disabled Continue in Main Menu");
        Success();
    }

    private static void DeleteAllAutoTestSlots()
    {
        SaveManager manager = SaveManager.Instance;
        Require(manager != null, "SaveManager is missing while cleaning Auto slots from the Manual E2E environment.");

        for (int index = 1; index <= SaveManager.SlotCount; index++)
        {
            if (manager.GetSlot(SaveSlotType.Auto, index).IsOccupied)
            {
                Require(manager.DeleteSlot(SaveSlotType.Auto, index), $"Could not remove Auto slot {index} from the Manual E2E environment.");
            }
        }
    }

    private static void VerifyOccupiedCard(ManualSaveLoadPanel panel, SaveSlotInfo slot)
    {
        Require(panel != null && panel.slotViews != null && panel.slotViews.Length == SaveManager.SlotCount, "Save panel does not contain six card views.");
        ManualSaveSlotView occupied = panel.slotViews[0];
        Require(occupied != null, "Occupied slot view is missing.");
        Require(occupied.previewImage != null && occupied.previewImage.sprite != null, "Occupied card does not show preview.");
        Require(occupied.dateText != null && occupied.dateText.gameObject.activeSelf && occupied.dateText.text == slot.DisplayDate, "Occupied card does not show local date and time.");
        Require(occupied.sceneNameText != null && occupied.sceneNameText.gameObject.activeSelf && occupied.sceneNameText.text == "Первый урок", "Occupied card does not show DialogueSceneData.displayName.");
        Require(occupied.emptyText != null && !occupied.emptyText.gameObject.activeSelf, "Occupied card still shows its empty placeholder.");
        Require(occupied.deleteButton != null && occupied.deleteButton.gameObject.activeSelf, "Occupied card does not show Delete.");

        ManualSaveSlotView empty = panel.slotViews[1];
        Require(empty.button.interactable, "Empty slot is disabled in Save mode.");
        Require(empty.emptyText.gameObject.activeSelf && empty.emptyText.text == "Пустой слот", "Empty slot placeholder is incorrect.");
        Require(!empty.deleteButton.gameObject.activeSelf, "Empty slot shows Delete.");
    }

    private static void VerifyPanelLayout(ManualSaveLoadPanel panel, Vector2Int resolution)
    {
        Require(Screen.width == resolution.x && Screen.height == resolution.y, "Layout validation resolution does not match Game View.");
        Require(panel.windowRect != null && panel.statusText != null, "Panel layout references are missing.");
        Rect windowRect = GetScreenRect(panel.windowRect);
        Require(windowRect.xMin >= -2f && windowRect.yMin >= -2f, $"Save window is clipped at {resolution.x}x{resolution.y} (min).");
        Require(windowRect.xMax <= Screen.width + 2f && windowRect.yMax <= Screen.height + 2f, $"Save window is clipped at {resolution.x}x{resolution.y} (max).");

        Rect[] cardRects = panel.slotViews.Select(view => GetScreenRect(view.cardRect)).ToArray();
        Require(cardRects.Length == SaveManager.SlotCount, "Layout validation did not find six cards.");
        foreach (Rect rect in cardRects)
        {
            Require(rect.width > 200f && rect.height > 140f, $"Save card collapsed at {resolution.x}x{resolution.y}.");
            Require(rect.xMin >= 0f && rect.yMin >= 0f && rect.xMax <= Screen.width && rect.yMax <= Screen.height, $"Save card is outside screen at {resolution.x}x{resolution.y}.");
        }

        float[] firstRowX = cardRects.Take(3).Select(rect => rect.center.x).OrderBy(value => value).ToArray();
        Require(firstRowX[1] - firstRowX[0] > 200f && firstRowX[2] - firstRowX[1] > 200f, "Save cards are not arranged into three columns.");
        Require(Mathf.Abs(cardRects[0].center.y - cardRects[1].center.y) < 3f, "First card row is misaligned.");
        Require(Mathf.Abs(cardRects[3].center.y - cardRects[4].center.y) < 3f, "Second card row is misaligned.");
        Require(cardRects[0].center.y - cardRects[3].center.y > 140f, "Save cards are not arranged into two rows.");

        Rect statusRect = GetScreenRect(panel.statusText.rectTransform);
        float lowestCardBottom = cardRects.Min(rect => rect.yMin);
        Require(statusRect.yMax <= lowestCardBottom + 2f, $"Status text overlaps cards at {resolution.x}x{resolution.y}.");
    }

    private static Rect GetScreenRect(RectTransform rectTransform)
    {
        var corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        float minX = corners.Min(corner => corner.x);
        float maxX = corners.Max(corner => corner.x);
        float minY = corners.Min(corner => corner.y);
        float maxY = corners.Max(corner => corner.y);
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private static string GetUiScreenshotPath(Vector2Int resolution)
    {
        string fileName = $"manual_save_{resolution.x}x{resolution.y}.png";
        return Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(Application.dataPath),
            "QAArtifacts",
            "GraphicalE2E",
            "ManualSave",
            fileName));
    }

    private static void ConfigureGameViewResolution(Vector2Int resolution)
    {
        try
        {
            Assembly editorAssembly = typeof(EditorWindow).Assembly;
            Type gameViewType = editorAssembly.GetType("UnityEditor.GameView", true);
            Type gameViewSizesType = editorAssembly.GetType("UnityEditor.GameViewSizes", true);
            Type gameViewSizeType = editorAssembly.GetType("UnityEditor.GameViewSize", true);
            Type gameViewSizeModeType = editorAssembly.GetType("UnityEditor.GameViewSizeType", true);

            Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(gameViewSizesType);
            PropertyInfo instanceProperty = singletonType.GetProperty(
                "instance",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            object sizesInstance = instanceProperty?.GetValue(null);
            Require(sizesInstance != null, "Unity GameViewSizes singleton is unavailable.");

            MethodInfo getGroup = gameViewSizesType.GetMethod(
                "GetGroup",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Require(getGroup != null, "Unity GameViewSizes.GetGroup() is unavailable.");
            Type groupTypeEnum = getGroup.GetParameters()[0].ParameterType;
            object standaloneGroup = Enum.Parse(groupTypeEnum, "Standalone");
            object group = getGroup.Invoke(sizesInstance, new[] { standaloneGroup });
            Require(group != null, "Unity Standalone Game View size group is unavailable.");

            MethodInfo getTotalCount = group.GetType().GetMethod(
                "GetTotalCount",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo getGameViewSize = group.GetType().GetMethod(
                "GetGameViewSize",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo addCustomSize = group.GetType().GetMethod(
                "AddCustomSize",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Require(getTotalCount != null && getGameViewSize != null && addCustomSize != null, "Unity Game View size group API is incomplete.");

            PropertyInfo widthProperty = gameViewSizeType.GetProperty(
                "width",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            PropertyInfo heightProperty = gameViewSizeType.GetProperty(
                "height",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Require(widthProperty != null && heightProperty != null, "Unity GameViewSize dimensions are unavailable.");

            int selectedIndex = -1;
            int count = (int)getTotalCount.Invoke(group, null);
            for (int i = 0; i < count; i++)
            {
                object size = getGameViewSize.Invoke(group, new object[] { i });
                if ((int)widthProperty.GetValue(size) == resolution.x
                    && (int)heightProperty.GetValue(size) == resolution.y)
                {
                    selectedIndex = i;
                    break;
                }
            }

            if (selectedIndex < 0)
            {
                object fixedResolution = Enum.Parse(gameViewSizeModeType, "FixedResolution");
                ConstructorInfo constructor = gameViewSizeType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { gameViewSizeModeType, typeof(int), typeof(int), typeof(string) },
                    null);
                Require(constructor != null, "Unity GameViewSize constructor is unavailable.");
                object newSize = constructor.Invoke(new object[]
                {
                    fixedResolution,
                    resolution.x,
                    resolution.y,
                    $"How I Fall {resolution.x}x{resolution.y}"
                });
                addCustomSize.Invoke(group, new[] { newSize });
                selectedIndex = (int)getTotalCount.Invoke(group, null) - 1;
            }

            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            PropertyInfo selectedSizeProperty = gameViewType.GetProperty(
                "selectedSizeIndex",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Require(selectedSizeProperty != null && selectedSizeProperty.CanWrite, "Unity GameView.selectedSizeIndex is unavailable.");
            selectedSizeProperty.SetValue(gameView, selectedIndex);
            gameView.Repaint();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Could not configure Game View to {resolution.x}x{resolution.y}.", exception);
        }

        Screen.SetResolution(resolution.x, resolution.y, FullScreenMode.Windowed);
    }

    private static string GetButtonLabel(Button button)
    {
        TextMeshProUGUI label = button != null ? button.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        return label != null ? label.text : string.Empty;
    }

    private static void ClickButtonThroughEventSystem(Button button)
    {
        Require(button != null, "Cannot click a missing button.");
        Require(EventSystem.current != null, "EventSystem is unavailable for the UI click test.");
        var pointer = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left
        };
        GameObject handler = ExecuteEvents.ExecuteHierarchy(
            button.gameObject,
            pointer,
            ExecuteEvents.pointerClickHandler);
        Require(handler == button.gameObject, "Delete click was handled by the slot card instead of its child button.");
    }

    private static void VerifyPreviewDimensions(string previewPath)
    {
        VerifyImageDimensions(previewPath, SaveManager.PreviewWidth, SaveManager.PreviewHeight, "Preview");
    }

    private static void VerifyImageDimensions(string path, int expectedWidth, int expectedHeight, string label)
    {
        Require(File.Exists(path), $"{label} file '{path}' is missing.");
        byte[] pngBytes = File.ReadAllBytes(path);
        var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);

        try
        {
            Require(texture.LoadImage(pngBytes, true), $"{label} file '{path}' is not a readable PNG.");
            Require(
                texture.width == expectedWidth && texture.height == expectedHeight,
                $"{label} size is {texture.width}x{texture.height}; expected {expectedWidth}x{expectedHeight}.");
        }
        finally
        {
            UnityEngine.Object.Destroy(texture);
        }
    }

    private static void VerifySnapshot(string context)
    {
        SaveData snapshot = ReadSnapshot();
        GameState state = GameState.Instance;
        Require(state != null, $"GameState missing during {context}.");
        Require(state.currentSceneId == snapshot.sceneId, $"sceneId mismatch during {context}.");
        Require(state.currentLineId == snapshot.lineId, $"lineId mismatch during {context}.");
        Require(state.currentLineIndex == snapshot.lineIndex, $"lineIndex mismatch during {context}.");
        Require(state.selectedChoiceIndex == snapshot.selectedChoiceIndex, $"choice index mismatch during {context}.");
        Require(state.choiceResultActive == snapshot.choiceResultActive, $"choice result flag mismatch during {context}.");
        Require(state.pendingNextSceneId == snapshot.pendingNextSceneId, $"pending scene mismatch during {context}.");
        Require(state.suspicion == snapshot.suspicion, $"suspicion mismatch during {context}.");
        Require(state.trustMasha == snapshot.trustMasha, $"trustMasha mismatch during {context}.");

        VNDialogueController controller = VNDialogueController.Instance;
        Require(controller != null, $"VNDialogueController missing during {context}.");
        Require(controller.TryGetSavePosition(out string sceneId, out string lineId, out int lineIndex, out string error), $"Position unavailable during {context}: {error}");
        Require(sceneId == snapshot.sceneId && lineId == snapshot.lineId && lineIndex == snapshot.lineIndex, $"VN controller position mismatch during {context}.");
        VerifyBacklog(snapshot, controller, context);
    }

    private static SaveData ReadSnapshot()
    {
        return ReadSnapshot(SnapshotKey);
    }

    private static SaveData ReadSnapshot(string key)
    {
        SaveData snapshot = JsonUtility.FromJson<SaveData>(SessionState.GetString(key, string.Empty));
        Require(snapshot != null, "Saved test snapshot is unavailable.");
        return snapshot;
    }

    private static SaveData CaptureCurrentSnapshot()
    {
        VNDialogueController controller = VNDialogueController.Instance;
        GameState state = GameState.Instance;
        Require(controller != null && state != null, "Runtime state is unavailable for backlog capture.");
        Require(controller.TryGetSavePosition(out string sceneId, out string lineId, out int lineIndex, out string error),
            $"Runtime position is unavailable for backlog capture: {error}");

        return new SaveData
        {
            sceneId = sceneId,
            lineId = lineId,
            lineIndex = lineIndex,
            selectedChoiceIndex = state.selectedChoiceIndex,
            choiceResultActive = state.choiceResultActive,
            pendingNextSceneId = state.pendingNextSceneId,
            lust = state.lust,
            romance = state.romance,
            purity = state.purity,
            corruptionLevel = state.corruptionLevel,
            selfControl = state.selfControl,
            suspicion = state.suspicion,
            trustMasha = state.trustMasha,
            trustArtem = state.trustArtem,
            leraInterest = state.leraInterest,
            backlogEntries = controller.CaptureBacklogSnapshot().Select(entry => new BacklogEntryData
            {
                speaker = entry.speaker,
                text = entry.text
            }).ToList()
        };
    }

    private static void VerifyRuntimeSnapshot(SaveData expected, string context)
    {
        GameState state = GameState.Instance;
        VNDialogueController controller = VNDialogueController.Instance;
        Require(state != null && controller != null, $"Runtime state is missing during {context}.");
        Require(state.currentSceneId == expected.sceneId
                && state.currentLineId == expected.lineId
                && state.currentLineIndex == expected.lineIndex,
            $"Dialogue position mismatch during {context}.");
        Require(state.selectedChoiceIndex == expected.selectedChoiceIndex
                && state.choiceResultActive == expected.choiceResultActive
                && state.pendingNextSceneId == expected.pendingNextSceneId,
            $"Choice state mismatch during {context}.");
        Require(state.suspicion == expected.suspicion && state.trustMasha == expected.trustMasha,
            $"Relationship state mismatch during {context}.");
        VerifyBacklog(expected, controller, context);
    }

    private static void VerifySnapshotData(SaveData expected, SaveData actual, string context)
    {
        Require(actual != null, $"SaveData is missing during {context}.");
        Require(actual.sceneId == expected.sceneId
                && actual.lineId == expected.lineId
                && actual.lineIndex == expected.lineIndex,
            $"Saved dialogue position mismatch during {context}.");
        Require(actual.selectedChoiceIndex == expected.selectedChoiceIndex
                && actual.choiceResultActive == expected.choiceResultActive
                && actual.pendingNextSceneId == expected.pendingNextSceneId,
            $"Saved choice state mismatch during {context}.");
        Require(actual.suspicion == expected.suspicion && actual.trustMasha == expected.trustMasha,
            $"Saved relationship state mismatch during {context}.");
        Require(BacklogSignatures(actual).SequenceEqual(BacklogSignatures(expected)),
            $"Saved backlog mismatch during {context}.");
    }

    private static void VerifyBacklog(SaveData expected, VNDialogueController controller, string context)
    {
        Require(expected.backlogEntries != null, $"Expected backlog is missing during {context}.");
        var actual = controller.CaptureBacklogSnapshot();
        Require(actual.Count == expected.backlogEntries.Count,
            $"History count mismatch during {context}: actual {actual.Count}, expected {expected.backlogEntries.Count}.");
        for (int index = 0; index < actual.Count; index++)
        {
            BacklogEntryData entry = expected.backlogEntries[index];
            Require(entry != null
                    && actual[index].speaker == (entry.speaker ?? string.Empty)
                    && actual[index].text == entry.text,
                $"History entry {index} mismatch during {context}.");
        }

        if (expected.choiceResultActive)
        {
            string resultText = expected.backlogEntries[expected.backlogEntries.Count - 1].text;
            Require(actual.Count(entry => entry.text == resultText) == 1,
                $"Choice result was duplicated in History during {context}.");
        }
    }

    private static System.Collections.Generic.IEnumerable<string> BacklogSignatures(SaveData data)
    {
        return data?.backlogEntries == null
            ? Enumerable.Empty<string>()
            : data.backlogEntries.Where(entry => entry != null).Select(entry => $"{entry.speaker ?? string.Empty}\u001f{entry.text}");
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false) || state != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }

        string stage = SessionState.GetString(StageKey, string.Empty);
        if (stage == "RestartRequested")
        {
            EditorSceneManager.OpenScene("Assets/HowIFall/Scenes/MainMenu.unity", OpenSceneMode.Single);
            SessionState.SetString(StageKey, "WaitMainContinue");
            SetDelay(1.0d);
            EditorApplication.isPlaying = true;
            return;
        }

        if (stage == "ExitSuccess")
        {
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.Exit(0);
        }
        else if (stage == "ExitFailure")
        {
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.Exit(1);
        }
    }

    private static void Success()
    {
        string errors = SessionState.GetString(ErrorsKey, string.Empty);
        Require(string.IsNullOrEmpty(errors), "Unity Console contained errors:\n" + errors);
        WriteResult("PASS", string.Empty);
        CleanupTestDirectory();
        Debug.Log("[PLAY E2E] COMPLETE PASS: all manual Save/Load scenarios succeeded.");
        SessionState.SetString(StageKey, "ExitSuccess");
        EditorApplication.isPlaying = false;
    }

    private static void Fail(string message)
    {
        WriteResult("FAIL", message);
        CleanupTestDirectory();
        Debug.LogError("[PLAY E2E] FAILURE: " + message);
        SessionState.SetString(StageKey, "ExitFailure");

        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
        }
        else
        {
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.Exit(1);
        }
    }

    private static void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (!SessionState.GetBool(ActiveKey, false)
            || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            || condition.StartsWith("[PLAY E2E] FAILURE", StringComparison.Ordinal))
        {
            return;
        }

        if (condition.StartsWith("ArgumentOutOfRangeException", StringComparison.Ordinal)
            && stackTrace.Contains("UnityEditor.Search.SearchDatabase"))
        {
            // Unity Search can race startup indexing in a freshly imported
            // graphical test copy. It is editor-only and unrelated to Save/Load.
            return;
        }

        string errors = SessionState.GetString(ErrorsKey, string.Empty);
        if (errors.Length < 12000)
        {
            SessionState.SetString(ErrorsKey, errors + condition + "\n");
        }
    }

    private static void WriteResult(string status, string details)
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), ResultPath);
        File.WriteAllText(
            path,
            $"status={status}\n"
                + $"timeUtc={DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)}\n"
                + $"saveDirectory={Path.Combine(Application.persistentDataPath, "Saves")}\n"
                + $"details={details}\n");
    }

    private static void CleanNewSaveFiles()
    {
        string directory = Path.Combine(Application.persistentDataPath, "Saves");
        Directory.CreateDirectory(directory);

        for (int slot = 1; slot <= SaveManager.SlotCount; slot++)
        {
            string stem = Path.Combine(directory, $"slot_{slot:D2}");
            DeleteIfExists(stem + ".json");
            DeleteIfExists(stem + ".png");
            DeleteIfExists(stem + ".json.tmp");
            DeleteIfExists(stem + ".png.tmp");
        }
    }

    private static void CleanupTestDirectory()
    {
        string directory = SessionState.GetString(DirectoryKey, string.Empty);
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void SetDelay(double seconds)
    {
        SessionState.SetFloat(NextTimeKey, (float)(EditorApplication.timeSinceStartup + seconds));
    }

    private static void Pass(string scenario)
    {
        Debug.Log("[PLAY E2E] PASS: " + scenario);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
