using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class ManualSaveSystemV1SmokeTests
{
    private static readonly Color ActiveTabOutlineColor = new Color(0.28f, 0.54f, 0.76f, 0.62f);
    private static readonly Color InactiveTabOutlineColor = new Color(0.16f, 0.25f, 0.34f, 0.34f);

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
            MainScene.displayName = "Основная сцена";
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

    [MenuItem("How I Fall/Tests/Run Save Backend v3 Smoke Tests")]
    public static void RunFromMenu()
    {
        Run();
        Debug.Log("How I Fall Save backend v3 smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        Run();
        Debug.Log("How I Fall Save backend v3 smoke tests passed.");
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
        TestDisplayNameAndFallback(context);
        TestInvalidSelectedChoiceIndex(context);
        TestNullSelectedChoice(context);
        TestPendingNextSceneIsComputed(context);
        TestTerminalChoiceRejectsRegisteredPendingScene(context);
        TestInvalidPendingNextScene(context);
        TestContinueIgnoresNewerInvalidSave(context);
        TestGameStateRollbackWhenInPlaceRestoreFails(context);
        TestFailedPendingRestoreResetsState(context);
        TestDeleteJsonPreviewAndTemporaryFiles(context);
        TestDeleteWithoutPreview(context);
        TestDeleteOrphanPreview(context);
        TestDeleteRejectsInvalidSlotIndex(context);
        TestDeleteFailureDoesNotTouchOtherSlots(context);
        TestDeleteLastValidSaveClearsContinue(context);
        TestDeleteKeepsNeighbourSlot(context);
        TestExistingV1ManualRemainsLoadable(context);
        TestV1RejectedOutsideManual(context);
        TestNewRecordsUseV3AndCorrectType(context);
        TestTypeMismatchIsRejected(context);
        TestSlotTypePathsDoNotIntersect(context);
        TestManualWrappersRemainManual(context);
        TestAutoFillsSlotsOneThroughSix(context);
        TestQuickFillsSlotsOneThroughSix(context);
        TestSeventhRotationOverwritesOldest(context);
        TestCorruptRotationTargetPrecedesValidSlots(context);
        TestAutoRotationDoesNotChangeQuickOrManual(context);
        TestQuickRotationDoesNotChangeAutoOrManual(context);
        TestGenericDeleteDoesNotTouchOtherTypes(context);
        TestContinueSelectsNewestAcrossAllTypes(context);
        TestContinueIgnoresNewerInvalidAcrossTypes(context);
        TestRollbackWorksForAutoAndQuick(context);
        TestTabbedSaveLoadPrefab(context);
    }

    private static void TestTabbedSaveLoadPrefab(TestContext context)
    {
        const string prefabPath = "Assets/HowIFall/Prefabs/UI/ManualSaveLoadPanel.prefab";
        ResetFiles(context);
        WriteData(context, SaveSlotType.Manual, CreateValidData(1, SaveSlotType.Manual));
        WriteData(context, SaveSlotType.Auto, CreateValidData(1, SaveSlotType.Auto));
        WriteData(context, SaveSlotType.Quick, CreateValidData(1, SaveSlotType.Quick));
        File.WriteAllText(context.Manager.GetSlotJsonPath(SaveSlotType.Auto, 2), "{ corrupt json");

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            ManualSaveLoadPanel panel = root.GetComponent<ManualSaveLoadPanel>();
            Require(panel != null, "Shared Save/Load prefab has no ManualSaveLoadPanel.");
            Require(panel.visualVersion == 7, "Shared Save/Load prefab visual version is not current.");
            Require(panel.subtitleText != null && panel.slotTypeHintText != null, "Tabbed panel subtitle or hint reference is missing.");
            Require(panel.manualTabButton != null && panel.autoTabButton != null && panel.quickTabButton != null, "One or more tab button references are missing.");
            Require(panel.slotViews != null && panel.slotViews.Length == SaveManager.SlotsPerPage, "Tabbed panel does not contain six slot views.");
            Require(panel.manualPaginationRoot != null && panel.previousManualPageButton != null && panel.nextManualPageButton != null,
                "Manual pagination references are missing.");
            Require(panel.manualPageButtons != null && panel.manualPageButtons.Length == SaveManager.ManualPageCount,
                "Manual pagination must have ten direct page buttons.");
            Require(panel.CurrentSlotType == SaveSlotType.Manual, "Tabbed panel serialized default is not Manual.");
            Require(
                new[] { panel.manualTabButton, panel.autoTabButton, panel.quickTabButton }.Distinct().Count() == 3,
                "Tabbed panel references duplicate tab buttons.");
            Require(root.GetComponentsInChildren<UnityEngine.UI.Button>(true).Count(button =>
                    button.name == "Manual Tab Button"
                    || button.name == "Auto Tab Button"
                    || button.name == "Quick Tab Button") == 3,
                "Shared prefab contains duplicate or missing tab objects.");

            MethodInfo paletteMethod = typeof(ManualSaveLoadPanel).GetMethod(
                "ApplyPlayerFacingPalette",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Require(paletteMethod != null, "Save/Load player-facing palette method is missing.");
            paletteMethod.Invoke(panel, null);
            Transform topAccent = panel.windowRect.GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "Red Accent");
            Color accentColor = topAccent.GetComponent<UnityEngine.UI.Image>().color;
            Require(accentColor.b > accentColor.r && accentColor.g > accentColor.r,
                "Save/Load top accent must use the shared blue-gray palette.");

            for (int i = 0; i < panel.slotViews.Length; i++)
            {
                panel.slotViews[i].Initialize(panel, i + 1);
            }

            RenderPanelForSmoke(panel, context.Manager, SaveSlotType.Manual, false);
            VerifyTabVisualState(panel, SaveSlotType.Manual);
            Require(panel.subtitleText.text == "РУЧНЫЕ СОХРАНЕНИЯ", "Manual subtitle is incorrect.");
            Require(panel.slotViews[0].slotNumberText.text == "1", "Manual occupied local index is incorrect.");
            Require(panel.slotViews[1].emptyText.text == "Пусто", "Manual empty label is incorrect.");
            Require(!panel.slotViews[1].backgroundSlotNumberText.gameObject.activeSelf, "Empty cards must not display giant background slot numbers.");
            Require(panel.slotViews[0].button.interactable && !panel.slotViews[1].button.interactable, "Manual Load interaction is incorrect.");

            RenderPanelForSmoke(panel, context.Manager, SaveSlotType.Auto, false);
            VerifyTabVisualState(panel, SaveSlotType.Auto);
            Require(panel.subtitleText.text == "АВТОСОХРАНЕНИЯ", "Auto subtitle is incorrect.");
            Require(panel.slotViews[0].slotNumberText.text == "1", "Auto occupied local index is incorrect.");
            Require(panel.slotViews[2].emptyText.text == "Пусто", "Auto empty label is incorrect.");
            Require(panel.slotViews[1].emptyText.text == "Недоступное сохранение", "Corrupt Auto card label is incorrect.");
            Require(!panel.slotViews[1].button.interactable && panel.slotViews[1].deleteButton.gameObject.activeSelf, "Corrupt Auto slot is not load-disabled/delete-enabled.");

            RenderPanelForSmoke(panel, context.Manager, SaveSlotType.Quick, false);
            VerifyTabVisualState(panel, SaveSlotType.Quick);
            Require(panel.subtitleText.text == "БЫСТРЫЕ СОХРАНЕНИЯ", "Quick subtitle is incorrect.");
            Require(panel.slotViews[0].slotNumberText.text == "1", "Quick occupied local index is incorrect.");
            Require(panel.slotViews[1].emptyText.text == "Пусто", "Quick empty label is incorrect.");

            RenderPanelForSmoke(panel, context.Manager, SaveSlotType.Manual, true);
            Require(panel.slotViews.All(view => view.button.interactable), "Manual cards are not writable in Save mode.");
            Require(!panel.slotTypeHintText.gameObject.activeSelf, "Manual Save unexpectedly shows a type hint.");

            RenderPanelForSmoke(panel, context.Manager, SaveSlotType.Auto, true);
            Require(panel.slotViews.All(view => !view.button.interactable), "Auto cards are primary-clickable in Save mode.");
            Require(panel.slotTypeHintText.text == "Автосохранения создаются игрой автоматически" && panel.slotTypeHintText.gameObject.activeSelf, "Auto Save hint is incorrect.");

            RenderPanelForSmoke(panel, context.Manager, SaveSlotType.Quick, true);
            Require(panel.slotViews.All(view => !view.button.interactable), "Quick cards are primary-clickable in Save mode.");
            Require(panel.slotTypeHintText.text == "Быстрые сохранения создаются отдельной командой" && panel.slotTypeHintText.gameObject.activeSelf, "Quick Save hint is incorrect.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void VerifyTabVisualState(ManualSaveLoadPanel panel, SaveSlotType activeType)
    {
        VerifyTabVisual(panel.manualTabButton, activeType == SaveSlotType.Manual, "Manual");
        VerifyTabVisual(panel.autoTabButton, activeType == SaveSlotType.Auto, "Auto");
        VerifyTabVisual(panel.quickTabButton, activeType == SaveSlotType.Quick, "Quick");
    }

    private static void VerifyTabVisual(UnityEngine.UI.Button button, bool active, string label)
    {
        Transform accent = button != null ? button.transform.Find("Active Accent") : null;
        Require(accent != null && accent.gameObject.activeSelf == active, $"{label} Active Accent state is incorrect.");

        UnityEngine.UI.Outline outline = button != null ? button.GetComponent<UnityEngine.UI.Outline>() : null;
        Require(outline != null, $"{label} tab has no Outline.");
        Color expected = active ? ActiveTabOutlineColor : InactiveTabOutlineColor;
        Require(ColorsApproximatelyEqual(outline.effectColor, expected), $"{label} tab Outline color is incorrect.");
    }

    private static bool ColorsApproximatelyEqual(Color left, Color right)
    {
        return Mathf.Abs(left.r - right.r) < 0.001f
            && Mathf.Abs(left.g - right.g) < 0.001f
            && Mathf.Abs(left.b - right.b) < 0.001f
            && Mathf.Abs(left.a - right.a) < 0.001f;
    }

    private static void RenderPanelForSmoke(
        ManualSaveLoadPanel panel,
        SaveManager manager,
        SaveSlotType type,
        bool saveMode)
    {
        FieldInfo typeField = typeof(ManualSaveLoadPanel).GetField(
            "currentSlotType",
            BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo modeField = typeof(ManualSaveLoadPanel).GetField(
            "mode",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo presentationMethod = typeof(ManualSaveLoadPanel).GetMethod(
            "ApplySlotTypePresentation",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Require(typeField != null && modeField != null && presentationMethod != null, "Tabbed panel smoke hooks are unavailable.");

        typeField.SetValue(panel, type);
        modeField.SetValue(panel, Enum.Parse(modeField.FieldType, saveMode ? "Save" : "Load"));
        presentationMethod.Invoke(panel, null);
        for (int i = 0; i < panel.slotViews.Length; i++)
        {
            int globalSlot = type == SaveSlotType.Manual
                ? ManualSaveLoadPanel.GetGlobalManualSlot(panel.CurrentManualPage, i + 1)
                : i + 1;
            panel.slotViews[i].Render(manager.GetSlot(type, globalSlot), saveMode, i + 1);
        }
    }

    private static void TestCorruptJson(TestContext context)
    {
        ResetFiles(context);
        File.WriteAllText(context.Manager.GetSlotJsonPath(1), "{ this is not valid json");

        SaveSlotInfo slot = context.Manager.GetSlot(1);
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

        SaveSlotInfo slot = context.Manager.GetSlot(1);
        Require(slot.IsLoadable, "A missing preview incorrectly blocked loading.");
        Require(string.IsNullOrEmpty(slot.PreviewPath), "Missing preview produced a non-empty PreviewPath.");
    }

    private static void TestDisplayNameAndFallback(TestContext context)
    {
        ResetFiles(context);
        WriteData(context, CreateValidData(1));
        Require(context.Manager.GetSlot(1).DisplayName == "Основная сцена", "Slot UI metadata did not use DialogueSceneData.displayName.");

        context.MainScene.displayName = "   ";
        Require(context.Manager.GetSlot(1).DisplayName == "Без названия", "Empty displayName did not use the safe fallback.");
        context.MainScene.displayName = "Основная сцена";
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

    private static void TestTerminalChoiceRejectsRegisteredPendingScene(TestContext context)
    {
        ResetFiles(context);
        DialogueChoice choice = context.MainScene.choices[0];
        DialogueSceneData originalChoiceNextScene = choice.nextScene;
        DialogueSceneData originalDefaultNextScene = context.MainScene.defaultNextScene;
        choice.nextScene = null;
        context.MainScene.defaultNextScene = null;

        try
        {
            SaveData data = CreateValidChoiceData(1);
            data.pendingNextSceneId = context.NextScene.sceneId;
            WriteData(context, data);

            Require(
                !context.Manager.GetSlot(1).IsLoadable,
                "A terminal choice accepted another registered scene as pendingNextSceneId.");
        }
        finally
        {
            choice.nextScene = originalChoiceNextScene;
            context.MainScene.defaultNextScene = originalDefaultNextScene;
        }
    }

    private static void TestPendingNextSceneIsComputed(TestContext context)
    {
        ResetFiles(context);
        SaveData data = CreateValidChoiceData(1);
        data.pendingNextSceneId = string.Empty;
        WriteData(context, data);

        SaveSlotInfo slot = context.Manager.GetSlot(1);
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

        var latest = method.Invoke(context.Manager, null) as SaveSlotInfo;
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

    private static void TestDeleteJsonPreviewAndTemporaryFiles(TestContext context)
    {
        ResetFiles(context);
        string jsonPath = context.Manager.GetSlotJsonPath(1);
        string previewPath = context.Manager.GetSlotPreviewPath(1);
        WriteData(context, CreateValidData(1));
        File.WriteAllBytes(previewPath, new byte[] { 1, 2, 3 });
        File.WriteAllText(jsonPath + ".tmp", "temporary json");
        File.WriteAllBytes(previewPath + ".tmp", new byte[] { 4, 5, 6 });

        Require(context.Manager.DeleteSlot(1), "DeleteSlot failed for a complete slot.");
        Require(!File.Exists(jsonPath), "DeleteSlot left the JSON file.");
        Require(!File.Exists(previewPath), "DeleteSlot left the PNG file.");
        Require(!File.Exists(jsonPath + ".tmp"), "DeleteSlot left the JSON temporary file.");
        Require(!File.Exists(previewPath + ".tmp"), "DeleteSlot left the PNG temporary file.");
    }

    private static void TestDeleteWithoutPreview(TestContext context)
    {
        ResetFiles(context);
        string jsonPath = context.Manager.GetSlotJsonPath(1);
        WriteData(context, CreateValidData(1));

        Require(context.Manager.DeleteSlot(1), "DeleteSlot failed when the preview was absent.");
        Require(!File.Exists(jsonPath), "DeleteSlot left JSON when the preview was absent.");
    }

    private static void TestDeleteOrphanPreview(TestContext context)
    {
        ResetFiles(context);
        string previewPath = context.Manager.GetSlotPreviewPath(1);
        File.WriteAllBytes(previewPath, new byte[] { 7, 8, 9 });

        Require(context.Manager.GetSlot(1).IsOccupied, "An orphan PNG was not exposed as an occupied slot.");
        Require(context.Manager.DeleteSlot(1), "DeleteSlot failed for an orphan PNG.");
        Require(!File.Exists(previewPath), "DeleteSlot left the orphan PNG.");
        Require(!context.Manager.GetSlot(1).IsOccupied, "The orphan PNG slot did not become empty.");
    }

    private static void TestDeleteRejectsInvalidSlotIndex(TestContext context)
    {
        ResetFiles(context);
        string jsonPath = context.Manager.GetSlotJsonPath(1);
        WriteData(context, CreateValidData(1));

        Require(!context.Manager.DeleteSlot(0), "DeleteSlot accepted slot index 0.");
        Require(!context.Manager.DeleteSlot(SaveManager.ManualSlotCount + 1), "DeleteSlot accepted a slot index above the manual capacity.");
        Require(File.Exists(jsonPath), "Invalid DeleteSlot call changed a valid slot.");
    }

    private static void TestDeleteFailureDoesNotTouchOtherSlots(TestContext context)
    {
        ResetFiles(context);
        string lockedJsonPath = context.Manager.GetSlotJsonPath(1);
        string neighbourJsonPath = context.Manager.GetSlotJsonPath(2);
        string neighbourPreviewPath = context.Manager.GetSlotPreviewPath(2);
        WriteData(context, CreateValidData(2));
        File.WriteAllBytes(neighbourPreviewPath, new byte[] { 10, 11, 12 });
        string neighbourJson = File.ReadAllText(neighbourJsonPath);

        // File.Delete on a directory throws on both Windows and Linux. Unlike an
        // open file, this does not depend on the platform's file-sharing semantics.
        Directory.CreateDirectory(lockedJsonPath);
        try
        {
            Require(!context.Manager.DeleteSlot(1), "DeleteSlot reported success despite a filesystem deletion error.");
            Require(File.Exists(neighbourJsonPath), "Failed deletion removed the neighbouring JSON.");
            Require(File.Exists(neighbourPreviewPath), "Failed deletion removed the neighbouring PNG.");
            Require(File.ReadAllText(neighbourJsonPath) == neighbourJson, "Failed deletion changed the neighbouring JSON.");
        }
        finally
        {
            if (Directory.Exists(lockedJsonPath))
            {
                Directory.Delete(lockedJsonPath);
            }
        }
    }

    private static void TestDeleteLastValidSaveClearsContinue(TestContext context)
    {
        ResetFiles(context);
        WriteData(context, CreateValidData(1));

        Require(context.Manager.HasAnyValidSave(), "The valid setup slot was not visible to Continue.");
        Require(context.Manager.DeleteSlot(1), "DeleteSlot failed for the last valid save.");
        Require(!context.Manager.HasAnyValidSave(), "Continue still found a valid save after deleting the last slot.");
    }

    private static void TestDeleteKeepsNeighbourSlot(TestContext context)
    {
        ResetFiles(context);
        string neighbourJsonPath = context.Manager.GetSlotJsonPath(2);
        string neighbourPreviewPath = context.Manager.GetSlotPreviewPath(2);
        WriteData(context, CreateValidData(1));
        WriteData(context, CreateValidData(2));
        File.WriteAllBytes(context.Manager.GetSlotPreviewPath(1), new byte[] { 13 });
        File.WriteAllBytes(neighbourPreviewPath, new byte[] { 14, 15 });
        string neighbourJson = File.ReadAllText(neighbourJsonPath);
        byte[] neighbourPreview = File.ReadAllBytes(neighbourPreviewPath);

        Require(context.Manager.DeleteSlot(1), "DeleteSlot failed while checking the neighbouring slot.");
        Require(File.Exists(neighbourJsonPath), "DeleteSlot removed the neighbouring JSON.");
        Require(File.Exists(neighbourPreviewPath), "DeleteSlot removed the neighbouring PNG.");
        Require(File.ReadAllText(neighbourJsonPath) == neighbourJson, "DeleteSlot changed the neighbouring JSON.");
        Require(
            File.ReadAllBytes(neighbourPreviewPath).SequenceEqual(neighbourPreview),
            "DeleteSlot changed the neighbouring PNG.");
    }

    private static void TestExistingV1ManualRemainsLoadable(TestContext context)
    {
        ResetFiles(context);
        SaveData legacy = CreateValidData(1, SaveSlotType.Manual);
        legacy.version = 1;
        string originalJson = WriteV1Data(context, SaveSlotType.Manual, legacy);

        SaveSlotInfo slot = context.Manager.GetSlot(SaveSlotType.Manual, 1);
        Require(slot.IsLoadable, $"Existing v1 Manual save was rejected: {slot.Error}");
        Require(slot.Data.version == SaveData.CurrentVersion, "v1 Manual save was not upgraded in memory to the current schema.");
        Require(slot.Data.slotType == SaveSlotType.Manual, "v1 Manual save was not treated as Manual in memory.");
        Require(File.ReadAllText(context.Manager.GetSlotJsonPath(1)) == originalJson, "Reading v1 Manual rewrote the source JSON.");
    }

    private static void TestV1RejectedOutsideManual(TestContext context)
    {
        ResetFiles(context);
        SaveData auto = CreateValidData(1, SaveSlotType.Auto);
        auto.version = 1;
        WriteV1Data(context, SaveSlotType.Auto, auto);
        SaveData quick = CreateValidData(1, SaveSlotType.Quick);
        quick.version = 1;
        WriteV1Data(context, SaveSlotType.Quick, quick);

        Require(!context.Manager.GetSlot(SaveSlotType.Auto, 1).IsLoadable, "v1 save in Auto was accepted.");
        Require(!context.Manager.GetSlot(SaveSlotType.Quick, 1).IsLoadable, "v1 save in Quick was accepted.");
    }

    private static void TestNewRecordsUseV3AndCorrectType(TestContext context)
    {
        MethodInfo method = typeof(SaveManager).GetMethod("CreateSaveData", BindingFlags.Static | BindingFlags.NonPublic);
        Require(method != null, "CreateSaveData test hook was not found.");

        GameObject gameStateObject = new GameObject("SaveData v3 capture");
        GameState gameState = gameStateObject.AddComponent<GameState>();
        try
        {
            foreach (SaveSlotType type in new[] { SaveSlotType.Manual, SaveSlotType.Auto, SaveSlotType.Quick })
            {
                string previewName = GetExpectedPreviewFileName(type, 2);
                var data = method.Invoke(
                    null,
                    new object[] { gameState, type, 2, "scene_main", "line_0", 0, previewName }) as SaveData;
                Require(data != null, $"CreateSaveData returned null for {type}.");
                Require(data.version == 3, $"New {type} record was not created as v3.");
                Require(data.backlogEntries != null && data.backlogEntries.Count == 0, $"New {type} record did not initialize an empty backlog snapshot.");
                Require(data.slotType == type, $"New {type} record contains slotType {data.slotType}.");
                Require(data.previewFileName == previewName, $"New {type} record contains an incorrect previewFileName.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameStateObject);
        }
    }

    private static void TestTypeMismatchIsRejected(TestContext context)
    {
        ResetFiles(context);
        SaveData data = CreateValidData(1, SaveSlotType.Quick);
        WriteData(context, SaveSlotType.Auto, data);

        SaveSlotInfo slot = context.Manager.GetSlot(SaveSlotType.Auto, 1);
        Require(slot.IsOccupied && !slot.IsLoadable, "Type mismatch between Auto path and Quick JSON was accepted.");
    }

    private static void TestSlotTypePathsDoNotIntersect(TestContext context)
    {
        string manual = context.Manager.GetSlotJsonPath(SaveSlotType.Manual, 1);
        string auto = context.Manager.GetSlotJsonPath(SaveSlotType.Auto, 1);
        string quick = context.Manager.GetSlotJsonPath(SaveSlotType.Quick, 1);

        Require(manual != auto && manual != quick && auto != quick, "Save slot type JSON paths intersect.");
        Require(Path.GetFileName(manual) == "slot_01.json", "Manual filename changed.");
        Require(Path.GetFileName(auto) == "auto_01.json", "Auto filename is incorrect.");
        Require(Path.GetFileName(quick) == "quick_01.json", "Quick filename is incorrect.");
        Require(Path.GetFileName(Path.GetDirectoryName(auto)) == "Auto", "Auto directory is incorrect.");
        Require(Path.GetFileName(Path.GetDirectoryName(quick)) == "Quick", "Quick directory is incorrect.");
    }

    private static void TestManualWrappersRemainManual(TestContext context)
    {
        ResetFiles(context);
        WriteData(context, CreateValidData(1, SaveSlotType.Manual));

        SaveSlotInfo wrapper = context.Manager.GetSlot(1);
        SaveSlotInfo generic = context.Manager.GetSlot(SaveSlotType.Manual, 1);
        Require(wrapper.IsLoadable && generic.IsLoadable, "Manual GetSlot wrapper stopped loading a Manual save.");
        Require(wrapper.SlotType == SaveSlotType.Manual, "Manual GetSlot wrapper returned another slot type.");
        Require(context.Manager.GetSlotJsonPath(1) == context.Manager.GetSlotJsonPath(SaveSlotType.Manual, 1), "Manual JSON path wrapper changed.");
        Require(context.Manager.GetSlotPreviewPath(1) == context.Manager.GetSlotPreviewPath(SaveSlotType.Manual, 1), "Manual preview path wrapper changed.");
    }

    private static void TestAutoFillsSlotsOneThroughSix(TestContext context)
    {
        ResetFiles(context);
        for (int expected = 1; expected <= SaveManager.SlotCount; expected++)
        {
            int selected = SimulateRotationWrite(context, SaveSlotType.Auto, expected);
            Require(selected == expected, $"Auto rotation selected slot {selected}; expected {expected}.");
        }
    }

    private static void TestQuickFillsSlotsOneThroughSix(TestContext context)
    {
        ResetFiles(context);
        for (int expected = 1; expected <= SaveManager.SlotCount; expected++)
        {
            int selected = SimulateRotationWrite(context, SaveSlotType.Quick, expected);
            Require(selected == expected, $"Quick rotation selected slot {selected}; expected {expected}.");
        }
    }

    private static void TestSeventhRotationOverwritesOldest(TestContext context)
    {
        ResetFiles(context);
        for (int slotIndex = 1; slotIndex <= SaveManager.SlotCount; slotIndex++)
        {
            SaveData data = CreateValidData(slotIndex, SaveSlotType.Auto);
            int day = slotIndex == 4 ? 1 : slotIndex + 1;
            data.createdAtUtc = $"2026-01-{day:D2}T00:00:00.0000000Z";
            WriteData(context, SaveSlotType.Auto, data);
        }

        Require(InvokeRotationTarget(context, SaveSlotType.Auto) == 4, "Seventh Auto save did not select the oldest valid slot.");

        ResetFiles(context);
        for (int slotIndex = 1; slotIndex <= SaveManager.SlotCount; slotIndex++)
        {
            SaveData data = CreateValidData(slotIndex, SaveSlotType.Quick);
            data.createdAtUtc = "2026-02-01T00:00:00.0000000Z";
            WriteData(context, SaveSlotType.Quick, data);
        }

        Require(InvokeRotationTarget(context, SaveSlotType.Quick) == 1, "Equal rotation timestamps did not select the smaller slot index.");
    }

    private static void TestCorruptRotationTargetPrecedesValidSlots(TestContext context)
    {
        ResetFiles(context);
        for (int slotIndex = 1; slotIndex <= SaveManager.SlotCount; slotIndex++)
        {
            WriteData(context, SaveSlotType.Auto, CreateValidData(slotIndex, SaveSlotType.Auto));
        }

        File.WriteAllText(context.Manager.GetSlotJsonPath(SaveSlotType.Auto, 3), "{ corrupt json");
        Require(InvokeRotationTarget(context, SaveSlotType.Auto) == 3, "Corrupt occupied Auto slot was not selected before valid slots.");
    }

    private static void TestAutoRotationDoesNotChangeQuickOrManual(TestContext context)
    {
        ResetFiles(context);
        WriteData(context, SaveSlotType.Manual, CreateValidData(1, SaveSlotType.Manual));
        WriteData(context, SaveSlotType.Quick, CreateValidData(1, SaveSlotType.Quick));
        string manualBefore = File.ReadAllText(context.Manager.GetSlotJsonPath(SaveSlotType.Manual, 1));
        string quickBefore = File.ReadAllText(context.Manager.GetSlotJsonPath(SaveSlotType.Quick, 1));

        SimulateRotationWrite(context, SaveSlotType.Auto, 1);

        Require(File.ReadAllText(context.Manager.GetSlotJsonPath(SaveSlotType.Manual, 1)) == manualBefore, "Auto rotation changed Manual.");
        Require(File.ReadAllText(context.Manager.GetSlotJsonPath(SaveSlotType.Quick, 1)) == quickBefore, "Auto rotation changed Quick.");
    }

    private static void TestQuickRotationDoesNotChangeAutoOrManual(TestContext context)
    {
        ResetFiles(context);
        WriteData(context, SaveSlotType.Manual, CreateValidData(1, SaveSlotType.Manual));
        WriteData(context, SaveSlotType.Auto, CreateValidData(1, SaveSlotType.Auto));
        string manualBefore = File.ReadAllText(context.Manager.GetSlotJsonPath(SaveSlotType.Manual, 1));
        string autoBefore = File.ReadAllText(context.Manager.GetSlotJsonPath(SaveSlotType.Auto, 1));

        SimulateRotationWrite(context, SaveSlotType.Quick, 1);

        Require(File.ReadAllText(context.Manager.GetSlotJsonPath(SaveSlotType.Manual, 1)) == manualBefore, "Quick rotation changed Manual.");
        Require(File.ReadAllText(context.Manager.GetSlotJsonPath(SaveSlotType.Auto, 1)) == autoBefore, "Quick rotation changed Auto.");
    }

    private static void TestGenericDeleteDoesNotTouchOtherTypes(TestContext context)
    {
        ResetFiles(context);
        foreach (SaveSlotType type in new[] { SaveSlotType.Manual, SaveSlotType.Auto, SaveSlotType.Quick })
        {
            WriteData(context, type, CreateValidData(1, type));
            File.WriteAllBytes(context.Manager.GetSlotPreviewPath(type, 1), new byte[] { 1, 2, 3 });
        }

        Require(context.Manager.DeleteSlot(SaveSlotType.Auto, 1), "Generic Auto DeleteSlot failed.");
        Require(!File.Exists(context.Manager.GetSlotJsonPath(SaveSlotType.Auto, 1)), "Generic delete left Auto JSON.");
        Require(File.Exists(context.Manager.GetSlotJsonPath(SaveSlotType.Manual, 1)), "Generic Auto delete removed Manual JSON.");
        Require(File.Exists(context.Manager.GetSlotJsonPath(SaveSlotType.Quick, 1)), "Generic Auto delete removed Quick JSON.");
    }

    private static void TestContinueSelectsNewestAcrossAllTypes(TestContext context)
    {
        ResetFiles(context);
        SaveData manual = CreateValidData(1, SaveSlotType.Manual);
        manual.createdAtUtc = "2026-01-01T00:00:00.0000000Z";
        WriteData(context, SaveSlotType.Manual, manual);
        SaveData pageTwoManual = CreateValidData(7, SaveSlotType.Manual);
        pageTwoManual.createdAtUtc = "2026-04-01T00:00:00.0000000Z";
        WriteData(context, SaveSlotType.Manual, pageTwoManual);
        SaveData auto = CreateValidData(2, SaveSlotType.Auto);
        auto.createdAtUtc = "2026-03-01T00:00:00.0000000Z";
        WriteData(context, SaveSlotType.Auto, auto);
        SaveData quick = CreateValidData(3, SaveSlotType.Quick);
        quick.createdAtUtc = "2026-02-01T00:00:00.0000000Z";
        WriteData(context, SaveSlotType.Quick, quick);

        SaveSlotInfo latest = InvokeLatest(context);
        Require(latest != null && latest.SlotType == SaveSlotType.Manual && latest.SlotIndex == 7,
            "Continue did not choose the newest valid Manual save beyond page 1.");

        pageTwoManual.createdAtUtc = manual.createdAtUtc;
        WriteData(context, SaveSlotType.Manual, pageTwoManual);
        auto.createdAtUtc = manual.createdAtUtc;
        quick.createdAtUtc = manual.createdAtUtc;
        WriteData(context, SaveSlotType.Auto, auto);
        WriteData(context, SaveSlotType.Quick, quick);
        latest = InvokeLatest(context);
        Require(latest != null && latest.SlotType == SaveSlotType.Manual, "Continue tie-break did not prefer Manual over Quick and Auto.");

        Require(context.Manager.DeleteSlot(SaveSlotType.Manual, 1), "Could not remove Manual slot 1 while testing Continue tie-break.");
        Require(context.Manager.DeleteSlot(SaveSlotType.Manual, 7), "Could not remove Manual slot 7 while testing Continue tie-break.");
        latest = InvokeLatest(context);
        Require(latest != null && latest.SlotType == SaveSlotType.Quick, "Continue tie-break did not prefer Quick over Auto.");

        Require(context.Manager.DeleteSlot(SaveSlotType.Quick, 3), "Could not remove Quick while testing Continue tie-break.");
        latest = InvokeLatest(context);
        Require(latest != null && latest.SlotType == SaveSlotType.Auto, "Continue tie-break did not fall back to Auto.");
    }

    private static void TestContinueIgnoresNewerInvalidAcrossTypes(TestContext context)
    {
        ResetFiles(context);
        SaveData manual = CreateValidData(1, SaveSlotType.Manual);
        manual.createdAtUtc = "2026-01-01T00:00:00.0000000Z";
        WriteData(context, SaveSlotType.Manual, manual);
        SaveData invalidAuto = CreateValidData(1, SaveSlotType.Auto);
        invalidAuto.createdAtUtc = "2026-12-01T00:00:00.0000000Z";
        invalidAuto.lineId = "missing_line";
        WriteData(context, SaveSlotType.Auto, invalidAuto);

        SaveSlotInfo latest = InvokeLatest(context);
        Require(latest != null && latest.SlotType == SaveSlotType.Manual, "Continue selected a newer invalid Auto save.");
    }

    private static void TestRollbackWorksForAutoAndQuick(TestContext context)
    {
        GameObject gameStateObject = new GameObject("Auto Quick rollback GameState");
        GameState gameState = gameStateObject.AddComponent<GameState>();
        MethodInfo method = typeof(SaveManager).GetMethod("TryApplyInPlace", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(method != null, "TryApplyInPlace test hook was not found.");

        try
        {
            foreach (SaveSlotType type in new[] { SaveSlotType.Auto, SaveSlotType.Quick })
            {
                gameState.currentSceneId = "original_scene";
                gameState.currentLineId = "original_line";
                gameState.currentLineIndex = 5;
                gameState.lust = 7;

                SaveData loaded = CreateValidData(1, type);
                loaded.lust = 99;
                object[] arguments = { loaded, 1, gameState, new Func<bool>(() => false), null };
                bool restored = (bool)method.Invoke(context.Manager, arguments);

                Require(!restored, $"Failed {type} restore reported success.");
                Require(gameState.currentSceneId == "original_scene" && gameState.currentLineId == "original_line", $"{type} rollback lost dialogue position.");
                Require(gameState.currentLineIndex == 5 && gameState.lust == 7, $"{type} rollback lost GameState values.");
                Require(!context.Manager.HasPendingSceneRestore, $"{type} rollback left pending load state.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameStateObject);
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
        return CreateValidData(slotIndex, SaveSlotType.Manual);
    }

    private static SaveData CreateValidData(int slotIndex, SaveSlotType type)
    {
        return new SaveData
        {
            version = SaveData.CurrentVersion,
            slotType = type,
            slotIndex = slotIndex,
            createdAtUtc = "2026-08-06T10:00:00.0000000Z",
            sceneId = "scene_main",
            lineId = "line_0",
            lineIndex = 0,
            selectedChoiceIndex = -1,
            choiceResultActive = false,
            pendingNextSceneId = string.Empty,
            previewFileName = GetExpectedPreviewFileName(type, slotIndex),
            backlogEntries = new System.Collections.Generic.List<BacklogEntryData>(),
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

    private static void WriteData(TestContext context, SaveSlotType pathType, SaveData data, int? fileSlotIndex = null)
    {
        int slotIndex = fileSlotIndex ?? data.slotIndex;
        File.WriteAllText(context.Manager.GetSlotJsonPath(pathType, slotIndex), JsonUtility.ToJson(data, true));
    }

    private static string WriteV1Data(TestContext context, SaveSlotType pathType, SaveData data)
    {
        string json = JsonUtility.ToJson(data, true);
        json = Regex.Replace(
            json,
            "^\\s*\"slotType\"\\s*:\\s*\\d+\\s*,?\\s*\\r?\\n",
            string.Empty,
            RegexOptions.Multiline);
        File.WriteAllText(context.Manager.GetSlotJsonPath(pathType, data.slotIndex), json);
        return json;
    }

    private static int SimulateRotationWrite(TestContext context, SaveSlotType type, int sequence)
    {
        int slotIndex = InvokeRotationTarget(context, type);
        Require(slotIndex > 0, $"Rotation returned no target for {type}.");
        SaveData data = CreateValidData(slotIndex, type);
        data.createdAtUtc = $"2026-04-{sequence:D2}T00:00:00.0000000Z";
        WriteData(context, type, data);
        return slotIndex;
    }

    private static int InvokeRotationTarget(TestContext context, SaveSlotType type)
    {
        MethodInfo method = typeof(SaveManager).GetMethod("SelectRotationTargetSlot", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(method != null, "SelectRotationTargetSlot test hook was not found.");
        return (int)method.Invoke(context.Manager, new object[] { type });
    }

    private static SaveSlotInfo InvokeLatest(TestContext context)
    {
        MethodInfo method = typeof(SaveManager).GetMethod("FindLatestLoadableSlot", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(method != null, "FindLatestLoadableSlot test hook was not found.");
        return method.Invoke(context.Manager, null) as SaveSlotInfo;
    }

    private static string GetExpectedPreviewFileName(SaveSlotType type, int slotIndex)
    {
        return type switch
        {
            SaveSlotType.Manual => $"slot_{slotIndex:D2}.png",
            SaveSlotType.Auto => $"auto_{slotIndex:D2}.png",
            SaveSlotType.Quick => $"quick_{slotIndex:D2}.png",
            _ => string.Empty
        };
    }

    private static void ResetFiles(TestContext context)
    {
        foreach (SaveSlotType type in new[] { SaveSlotType.Manual, SaveSlotType.Auto, SaveSlotType.Quick })
        {
            for (int slotIndex = 1; slotIndex <= SaveManager.SlotCount; slotIndex++)
            {
                string jsonPath = context.Manager.GetSlotJsonPath(type, slotIndex);
                string previewPath = context.Manager.GetSlotPreviewPath(type, slotIndex);
                DeleteIfExists(jsonPath);
                DeleteIfExists(previewPath);
                DeleteIfExists(jsonPath + ".tmp");
                DeleteIfExists(previewPath + ".tmp");
            }
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
