using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ManualSaveSystemV1SmokeTests
{
    private sealed class TestContext : IDisposable
    {
        public readonly string DirectoryPath;
        public readonly GameObject ManagerObject;
        public readonly SaveManager Manager;
        public readonly DialogueSceneRegistry Registry;
        public readonly DialogueSceneData MainScene;
        public readonly DialogueSceneData NextScene;

        public TestContext()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), "HowIFall_SaveV1_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);

            NextScene = ScriptableObject.CreateInstance<DialogueSceneData>();
            NextScene.sceneId = "scene_next";
            NextScene.lines.Add(new DialogueLine { lineId = "next_0", text = "Next" });

            MainScene = ScriptableObject.CreateInstance<DialogueSceneData>();
            MainScene.sceneId = "scene_main";
            MainScene.lines.Add(new DialogueLine { lineId = "line_0", text = "First" });
            MainScene.lines.Add(new DialogueLine { lineId = "line_1", text = "Second" });
            MainScene.choices.Add(new DialogueChoice { text = "Choice", resultText = "Result", nextScene = NextScene });
            MainScene.defaultNextScene = NextScene;

            Registry = ScriptableObject.CreateInstance<DialogueSceneRegistry>();
            Registry.scenes.Add(MainScene);
            Registry.scenes.Add(NextScene);

            ManagerObject = new GameObject("ManualSaveSystemV1SmokeTests");
            Manager = ManagerObject.AddComponent<SaveManager>();
            Manager.ConfigureRegistry(Registry);
            Manager.ConfigureSaveDirectoryForTests(DirectoryPath);
        }

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(ManagerObject);
            UnityEngine.Object.DestroyImmediate(Registry);
            UnityEngine.Object.DestroyImmediate(MainScene);
            UnityEngine.Object.DestroyImmediate(NextScene);

            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, true);
            }
        }
    }

    [MenuItem("How I Fall/Tests/Run Manual Save v1 Smoke Tests")]
    public static void RunFromMenu()
    {
        Run();
        Debug.Log("How I Fall manual Save v1 smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        Run();
        Debug.Log("How I Fall manual Save v1 smoke tests passed.");
    }

    private static void Run()
    {
        using var context = new TestContext();

        TestCorruptJson(context);
        TestUnsupportedVersion(context);
        TestWrongSlotIndex(context);
        TestMissingScene(context);
        TestMissingLine(context);
        TestMissingPreviewIsLoadable(context);
        TestInvalidSelectedChoiceIndex(context);
        TestNullSelectedChoice(context);
        TestPendingNextSceneIsComputed(context);
        TestInvalidPendingNextScene(context);
        TestContinueIgnoresNewerInvalidSave(context);
        TestGameStateRollbackWhenInPlaceRestoreFails(context);
        TestFailedPendingRestoreResetsState(context);
    }

    private static void TestCorruptJson(TestContext context)
    {
        ResetFiles(context);
        File.WriteAllText(context.Manager.GetSlotJsonPath(1), "{ this is not valid json");

        ManualSaveSlotInfo slot = context.Manager.GetSlot(1);
        Require(slot.IsOccupied, "Corrupt JSON was not reported as occupied.");
        Require(!slot.IsLoadable, "Corrupt JSON was loadable.");
        Require(!string.IsNullOrEmpty(slot.Error), "Corrupt JSON has no validation error.");
    }

    private static void TestUnsupportedVersion(TestContext context)
    {
        ResetFiles(context);
        SaveData data = CreateValidData(1);
        data.version = SaveData.CurrentVersion + 1;
        WriteData(context, data);

        Require(!context.Manager.GetSlot(1).IsLoadable, "Unsupported save version was loadable.");
    }

    private static void TestWrongSlotIndex(TestContext context)
    {
        ResetFiles(context);
        SaveData data = CreateValidData(1);
        data.slotIndex = 2;
        WriteData(context, data, 1);

        Require(!context.Manager.GetSlot(1).IsLoadable, "JSON with a mismatched slotIndex was loadable.");
    }

    private static void TestMissingScene(TestContext context)
    {
        ResetFiles(context);
        SaveData data = CreateValidData(1);
        data.sceneId = "scene_missing";
        WriteData(context, data);

        Require(!context.Manager.GetSlot(1).IsLoadable, "Save with a missing scene was loadable.");
    }

    private static void TestMissingLine(TestContext context)
    {
        ResetFiles(context);
        SaveData data = CreateValidData(1);
        data.lineId = "line_missing";
        data.lineIndex = 0;
        WriteData(context, data);

        Require(!context.Manager.GetSlot(1).IsLoadable, "Save with a missing non-empty lineId used fallback and became loadable.");
    }

    private static void TestMissingPreviewIsLoadable(TestContext context)
    {
        ResetFiles(context);
        SaveData data = CreateValidData(1);
        WriteData(context, data);

        ManualSaveSlotInfo slot = context.Manager.GetSlot(1);
        Require(slot.IsLoadable, "A missing preview incorrectly blocked loading.");
        Require(string.IsNullOrEmpty(slot.PreviewPath), "Missing preview produced a non-empty PreviewPath.");
    }

    private static void TestInvalidSelectedChoiceIndex(TestContext context)
    {
        ResetFiles(context);
        SaveData data = CreateValidChoiceData(1);
        data.selectedChoiceIndex = 99;
        WriteData(context, data);

        Require(!context.Manager.GetSlot(1).IsLoadable, "Invalid selectedChoiceIndex was loadable.");
    }

    private static void TestNullSelectedChoice(TestContext context)
    {
        ResetFiles(context);
        DialogueChoice originalChoice = context.MainScene.choices[0];
        context.MainScene.choices[0] = null;

        try
        {
            WriteData(context, CreateValidChoiceData(1));
            Require(!context.Manager.GetSlot(1).IsLoadable, "Null selected DialogueChoice was loadable.");
        }
        finally
        {
            context.MainScene.choices[0] = originalChoice;
        }
    }

    private static void TestInvalidPendingNextScene(TestContext context)
    {
        ResetFiles(context);
        SaveData data = CreateValidChoiceData(1);
        data.pendingNextSceneId = "scene_missing";
        WriteData(context, data);

        Require(!context.Manager.GetSlot(1).IsLoadable, "Invalid pendingNextSceneId was loadable.");
    }

    private static void TestPendingNextSceneIsComputed(TestContext context)
    {
        ResetFiles(context);
        SaveData data = CreateValidChoiceData(1);
        data.pendingNextSceneId = string.Empty;
        WriteData(context, data);

        ManualSaveSlotInfo slot = context.Manager.GetSlot(1);
        Require(slot.IsLoadable, "Empty pendingNextSceneId was not computed from the selected choice.");
        Require(slot.Data.pendingNextSceneId == "scene_next", "Computed pendingNextSceneId is incorrect.");
    }

    private static void TestContinueIgnoresNewerInvalidSave(TestContext context)
    {
        ResetFiles(context);
        SaveData validOlder = CreateValidData(1);
        validOlder.createdAtUtc = "2026-01-01T00:00:00.0000000Z";
        WriteData(context, validOlder);

        SaveData invalidNewer = CreateValidData(2);
        invalidNewer.createdAtUtc = "2026-12-31T00:00:00.0000000Z";
        invalidNewer.lineId = "line_missing";
        WriteData(context, invalidNewer);

        MethodInfo method = typeof(SaveManager).GetMethod(
            "FindLatestLoadableSlot",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Require(method != null, "FindLatestLoadableSlot test hook was not found.");

        var latest = method.Invoke(context.Manager, null) as ManualSaveSlotInfo;
        Require(latest != null && latest.SlotIndex == 1, "Continue did not ignore the newer invalid save.");
    }

    private static void TestGameStateRollbackWhenInPlaceRestoreFails(TestContext context)
    {
        GameObject gameStateObject = new GameObject("Rollback GameState");
        GameState gameState = gameStateObject.AddComponent<GameState>();

        try
        {
            gameState.currentSceneId = "original_scene";
            gameState.currentLineId = "original_line";
            gameState.currentLineIndex = 12;
            gameState.selectedChoiceIndex = 2;
            gameState.choiceResultActive = true;
            gameState.pendingNextSceneId = "original_next";
            gameState.lust = 1;
            gameState.romance = 2;
            gameState.purity = 3;
            gameState.corruptionLevel = 4;
            gameState.selfControl = 5;
            gameState.suspicion = 6;
            gameState.trustMasha = 7;
            gameState.trustArtem = 8;
            gameState.leraInterest = 9;

            SaveData loaded = CreateValidChoiceData(1);
            loaded.lust = 101;
            loaded.suspicion = 106;

            MethodInfo method = typeof(SaveManager).GetMethod(
                "TryApplyInPlace",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(method != null, "TryApplyInPlace test hook was not found.");

            object[] arguments =
            {
                loaded,
                1,
                gameState,
                new Func<bool>(() => false),
                null
            };
            bool restored = (bool)method.Invoke(context.Manager, arguments);

            Require(!restored, "Failed dialogue restoration reported success.");
            Require((string)arguments[4] == "VNDialogueController.RestoreFromGameState() returned false.", "Rollback did not return the expected error.");
            Require(!context.Manager.HasPendingSceneRestore && context.Manager.PendingSlotIndex == 0, "Pending load was not cleared after rollback.");
            Require(gameState.currentSceneId == "original_scene", "Rollback lost currentSceneId.");
            Require(gameState.currentLineId == "original_line", "Rollback lost currentLineId.");
            Require(gameState.currentLineIndex == 12, "Rollback lost currentLineIndex.");
            Require(gameState.selectedChoiceIndex == 2 && gameState.choiceResultActive, "Rollback lost choice state.");
            Require(gameState.pendingNextSceneId == "original_next", "Rollback lost pendingNextSceneId.");
            Require(gameState.lust == 1 && gameState.romance == 2 && gameState.purity == 3, "Rollback lost primary GameState values.");
            Require(gameState.corruptionLevel == 4 && gameState.selfControl == 5 && gameState.suspicion == 6, "Rollback lost secondary GameState values.");
            Require(gameState.trustMasha == 7 && gameState.trustArtem == 8 && gameState.leraInterest == 9, "Rollback lost relationship values.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameStateObject);
        }
    }

    private static void TestFailedPendingRestoreResetsState(TestContext context)
    {
        GameObject gameStateObject = new GameObject("Pending Restore GameState");
        GameState gameState = gameStateObject.AddComponent<GameState>();
        GameState previousInstance = GameState.Instance;
        SetGameStateInstance(gameState);

        try
        {
            gameState.currentSceneId = "loaded_scene";
            gameState.currentLineId = "loaded_line";
            gameState.currentLineIndex = 8;
            gameState.selectedChoiceIndex = 0;
            gameState.choiceResultActive = true;
            gameState.pendingNextSceneId = "loaded_next";
            gameState.lust = 10;
            gameState.suspicion = 11;
            gameState.trustMasha = 12;

            MethodInfo beginMethod = typeof(SaveManager).GetMethod(
                "BeginPendingSceneRestore",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(beginMethod != null, "BeginPendingSceneRestore test hook was not found.");
            beginMethod.Invoke(context.Manager, new object[] { 3 });

            context.Manager.FailPendingSceneRestoreAndReset();

            Require(!context.Manager.HasPendingSceneRestore && context.Manager.PendingSlotIndex == 0, "Failed pending restore did not clear pending state.");
            Require(gameState.currentSceneId == string.Empty && gameState.currentLineId == string.Empty && gameState.currentLineIndex == 0, "Failed pending restore left loaded dialogue position in GameState.");
            Require(gameState.selectedChoiceIndex == -1 && !gameState.choiceResultActive && gameState.pendingNextSceneId == string.Empty, "Failed pending restore left loaded choice state in GameState.");
            Require(gameState.lust == 0 && gameState.suspicion == 0 && gameState.trustMasha == 0 && gameState.selfControl == 5, "Failed pending restore left loaded gameplay values in GameState.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameStateObject);
            SetGameStateInstance(previousInstance);
        }
    }

    private static void SetGameStateInstance(GameState gameState)
    {
        FieldInfo field = typeof(GameState).GetField(
            "<Instance>k__BackingField",
            BindingFlags.Static | BindingFlags.NonPublic);
        Require(field != null, "GameState.Instance backing field was not found.");
        field.SetValue(null, gameState);
    }

    private static SaveData CreateValidData(int slotIndex)
    {
        return new SaveData
        {
            version = SaveData.CurrentVersion,
            slotIndex = slotIndex,
            createdAtUtc = "2026-08-06T10:00:00.0000000Z",
            sceneId = "scene_main",
            lineId = "line_0",
            lineIndex = 0,
            selectedChoiceIndex = -1,
            choiceResultActive = false,
            pendingNextSceneId = string.Empty,
            previewFileName = $"slot_{slotIndex:D2}.png",
            selfControl = 5
        };
    }

    private static SaveData CreateValidChoiceData(int slotIndex)
    {
        SaveData data = CreateValidData(slotIndex);
        data.selectedChoiceIndex = 0;
        data.choiceResultActive = true;
        data.pendingNextSceneId = "scene_next";
        return data;
    }

    private static void WriteData(TestContext context, SaveData data, int? fileSlotIndex = null)
    {
        int slotIndex = fileSlotIndex ?? data.slotIndex;
        File.WriteAllText(context.Manager.GetSlotJsonPath(slotIndex), JsonUtility.ToJson(data, true));
    }

    private static void ResetFiles(TestContext context)
    {
        for (int slotIndex = 1; slotIndex <= SaveManager.SlotCount; slotIndex++)
        {
            DeleteIfExists(context.Manager.GetSlotJsonPath(slotIndex));
            DeleteIfExists(context.Manager.GetSlotPreviewPath(slotIndex));
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
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
