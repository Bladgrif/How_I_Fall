using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
    private const string InitialCreatedAtKey = "HowIFall.SaveE2E.InitialCreatedAt";
    private const string ErrorsKey = "HowIFall.SaveE2E.Errors";
    private const string ResultPath = "manual_save_playmode_result.txt";

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

        CleanNewSaveFiles();
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetString(StageKey, "WaitMainNewGame");
        SessionState.SetInt(CounterKey, 0);
        SessionState.SetString(SnapshotKey, string.Empty);
        SessionState.SetString(InitialCreatedAtKey, string.Empty);
        SessionState.SetString(ErrorsKey, string.Empty);
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
            case "OverwriteCancel":
                OverwriteCancel();
                break;
            case "OverwriteConfirm":
                OverwriteConfirm();
                break;
            case "WaitOverwrite":
                WaitOverwrite();
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
        ManualSaveSlotInfo slot = manager?.GetSlot(1);
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
        SessionState.SetString(SnapshotKey, JsonUtility.ToJson(slot.Data));
        SessionState.SetString(InitialCreatedAtKey, slot.Data.createdAtUtc);
        Pass("Slot 1 JSON and screenshot written");

        VNDialogueController.Instance.manualSaveLoadPanel.Close();
        SessionState.SetString(StageKey, "AdvanceAfterSave");
        SessionState.SetInt(CounterKey, 0);
        SetDelay(0.1d);
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
        SessionState.SetString(StageKey, "LoadInsideVn");
        SetDelay(0.1d);
    }

    private static void LoadInsideVn()
    {
        ManualSaveLoadPanel panel = VNDialogueController.Instance.manualSaveLoadPanel;
        panel.OpenLoad();
        Require(panel.slotViews[0].button.interactable, "Occupied slot is not interactable in Load mode.");
        panel.slotViews[0].button.onClick.Invoke();
        SessionState.SetString(StageKey, "VerifyInsideVn");
        SetDelay(0.3d);
    }

    private static void VerifyInsideVn()
    {
        VerifySnapshot("in-place VN load");
        Pass("In-place VN load restored line, choice and GameState");
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
        SessionState.SetString(StageKey, "OverwriteCancel");
        SetDelay(0.2d);
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
        ManualSaveSlotInfo slot = SaveManager.Instance.GetSlot(1);
        string previousTime = SessionState.GetString(InitialCreatedAtKey, string.Empty);
        if (!slot.IsLoadable || slot.Data.createdAtUtc == previousTime)
        {
            int attempts = SessionState.GetInt(CounterKey, 0) + 1;
            SessionState.SetInt(CounterKey, attempts);
            Require(attempts < 120, "Confirmed overwrite did not update slot 1.");
            SetDelay(0.1d);
            return;
        }

        Pass("Overwrite confirmation Yes replaced JSON and PNG");
        Success();
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
    }

    private static SaveData ReadSnapshot()
    {
        SaveData snapshot = JsonUtility.FromJson<SaveData>(SessionState.GetString(SnapshotKey, string.Empty));
        Require(snapshot != null, "Saved test snapshot is unavailable.");
        return snapshot;
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
        Debug.Log("[PLAY E2E] COMPLETE PASS: all manual Save/Load scenarios succeeded.");
        SessionState.SetString(StageKey, "ExitSuccess");
        EditorApplication.isPlaying = false;
    }

    private static void Fail(string message)
    {
        WriteResult("FAIL", message);
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
