using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class GalleryReplaySmokeTests
{
    [MenuItem("How I Fall/Tests/Run Gallery Replay Smoke Tests")]
    public static void RunFromMenu()
    {
        RunBatchMode();
    }

    public static void RunBatchMode()
    {
        TestDefinitionValidationAndLockedStart();
        TestSnapshotBacklogHistoryAndExactlyOnceRestore();
        TestSaveLoadDefenseAndQuickMenuPresentation();
        TestSaveSchemaRemainsV3AndReplayFree();
        Debug.Log("How I Fall gallery replay smoke tests passed.");
    }

    private static void TestDefinitionValidationAndLockedStart()
    {
        DialogueSceneData start = CreateScene("locked_test_start", "locked_test_line");
        DialogueSceneRegistry registry = ScriptableObject.CreateInstance<DialogueSceneRegistry>();
        registry.scenes.Add(start);
        ReplayEntryDefinition definition = ScriptableObject.CreateInstance<ReplayEntryDefinition>();
        definition.replayId = "locked_test_" + Guid.NewGuid().ToString("N");
        definition.displayName = "TEST REPLAY";
        definition.startScene = start;
        var definitions = new List<ReplayEntryDefinition> { definition };

        Require(SceneFlowManager.TryValidateReplayDefinition(definition, definitions, registry, out _), "Valid TEST definition was rejected.");
        ReplayEntryDefinition unregisteredClone = ScriptableObject.CreateInstance<ReplayEntryDefinition>();
        unregisteredClone.replayId = definition.replayId;
        unregisteredClone.displayName = definition.displayName;
        unregisteredClone.startScene = start;
        Require(!SceneFlowManager.TryValidateReplayDefinition(unregisteredClone, definitions, registry, out _),
            "Unregistered replay asset with a known ID was accepted.");
        UnityEngine.Object.DestroyImmediate(unregisteredClone);
        definitions.Add(definition);
        Require(!SceneFlowManager.TryValidateReplayDefinition(definition, definitions, registry, out _), "Duplicate replay ID was accepted.");
        definitions.RemoveAt(1);
        registry.scenes.Clear();
        Require(!SceneFlowManager.TryValidateReplayDefinition(definition, definitions, registry, out _), "Unregistered replay scene was accepted.");
        registry.scenes.Add(start);

        GameObject stateObject = new GameObject("Replay Locked State");
        GameState state = stateObject.AddComponent<GameState>();
        state.lust = 77;
        GameObject flowObject = new GameObject("Replay Locked Flow");
        SceneFlowManager flow = flowObject.AddComponent<SceneFlowManager>();
        SetStaticInstance(typeof(SceneFlowManager), flow);
        Require(!flow.TryStartReplay(definition, definitions, registry, out _), "Locked replay start was accepted.");
        Require(state.lust == 77 && !flow.IsReplayMode, "Locked replay start mutated campaign state.");

        SetStaticInstance(typeof(SceneFlowManager), null);
        UnityEngine.Object.DestroyImmediate(flowObject);
        UnityEngine.Object.DestroyImmediate(stateObject);
        UnityEngine.Object.DestroyImmediate(definition);
        UnityEngine.Object.DestroyImmediate(registry);
        UnityEngine.Object.DestroyImmediate(start);
    }

    private static void TestSnapshotBacklogHistoryAndExactlyOnceRestore()
    {
        DialogueSceneData start = CreateScene("replay_test_start", "replay_test_line");
        ReplayEntryDefinition definition = ScriptableObject.CreateInstance<ReplayEntryDefinition>();
        definition.replayId = "test_replay_v1";
        definition.displayName = "TEST REPLAY";
        definition.startScene = start;

        GameObject stateObject = new GameObject("Replay Snapshot State");
        GameState state = stateObject.AddComponent<GameState>();
        SeedState(state);
        ReplayGameStateSnapshot expected = ReplayGameStateSnapshot.Capture(state);

        GameObject controllerObject = new GameObject("Replay Snapshot Controller");
        VNDialogueController controller = controllerObject.AddComponent<VNDialogueController>();
        SetStaticInstance(typeof(VNDialogueController), controller);
        controller.ReplaceBacklogFromSnapshot(new[]
        {
            new DialogueBacklogEntry { text = "CAMPAIGN A" },
            new DialogueBacklogEntry { text = "CAMPAIGN B" }
        });

        var session = new ReplaySession(definition, state, controller);
        session.Activate(state);
        Require(controller.CaptureBacklogSnapshot().Count == 0, "Replay backlog did not start empty.");
        controller.ReplaceBacklogFromSnapshot(new[] { new DialogueBacklogEntry { text = "TEST REPLAY START" } });
        session.MarkSeen(start.sceneId, start.lines[0].lineId);
        Require(session.IsSeen(start.sceneId, start.lines[0].lineId), "Replay-local seen history did not record a fully shown line.");

        state.lust = -100;
        state.romance = -101;
        state.currentSceneId = "mutated_replay";
        Require(session.BeginEnding(), "Replay cleanup did not begin.");
        Require(session.RestoreCampaignState(state), "First campaign restore did not run.");
        AssertStateEquals(expected, state);
        string[] backlog = controller.CaptureBacklogSnapshot().Select(entry => entry.text).ToArray();
        Require(backlog.SequenceEqual(new[] { "CAMPAIGN A", "CAMPAIGN B" }), "Campaign backlog was merged or not restored exactly.");
        Require(!session.RestoreCampaignState(state), "Campaign state restored more than once.");
        session.MarkEnded();

        SetStaticInstance(typeof(VNDialogueController), null);
        UnityEngine.Object.DestroyImmediate(controllerObject);
        UnityEngine.Object.DestroyImmediate(stateObject);
        UnityEngine.Object.DestroyImmediate(definition);
        UnityEngine.Object.DestroyImmediate(start);
    }

    private static void TestSaveLoadDefenseAndQuickMenuPresentation()
    {
        string root = Path.Combine(Path.GetTempPath(), "HowIFall_ReplaySaveGuard_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            DialogueSceneData start = CreateScene("guard_start", "guard_line");
            ReplayEntryDefinition definition = ScriptableObject.CreateInstance<ReplayEntryDefinition>();
            definition.replayId = "test_replay_v1";
            definition.displayName = "TEST REPLAY";
            definition.startScene = start;

            GameObject stateObject = new GameObject("Replay Guard State");
            GameState state = stateObject.AddComponent<GameState>();
            SeedState(state);
            ReplayGameStateSnapshot expected = ReplayGameStateSnapshot.Capture(state);

            GameObject flowObject = new GameObject("Replay Guard Flow");
            SceneFlowManager flow = flowObject.AddComponent<SceneFlowManager>();
            SetStaticInstance(typeof(SceneFlowManager), flow);
            var session = new ReplaySession(definition, state, null);
            SetPrivateField(flow, "replaySession", session);
            session.Activate(state);

            GameObject saveObject = new GameObject("Replay Guard SaveManager");
            SaveManager saveManager = saveObject.AddComponent<SaveManager>();
            saveManager.ConfigureSaveDirectoryForTests(root);
            string sentinel = Path.Combine(root, "sentinel.bin");
            File.WriteAllText(sentinel, "unchanged");

            Require(!saveManager.SaveSlot(1, null), "Direct Manual Save bypassed replay guard.");
            Require(!saveManager.SaveAuto(null), "Direct Auto Save bypassed replay guard.");
            Require(!saveManager.SaveQuick(null), "Direct Quick Save bypassed replay guard.");
            Require(!saveManager.LoadSlot(1), "Direct Manual Load bypassed replay guard.");
            Require(!saveManager.LoadLatest(), "Direct Continue bypassed replay guard.");
            Require(File.ReadAllText(sentinel) == "unchanged", "Replay Save/Load guard changed test save files.");
            Require(!saveManager.HasPendingSceneRestore, "Replay load guard left a pending load.");

            GameObject controllerObject = new GameObject("Replay Guard Controller");
            VNDialogueController controller = controllerObject.AddComponent<VNDialogueController>();
            SetStaticInstance(typeof(VNDialogueController), controller);
            GameObject panelObject = new GameObject("Replay Guard Panel");
            panelObject.SetActive(false);
            ManualSaveLoadPanel panel = panelObject.AddComponent<ManualSaveLoadPanel>();
            controller.manualSaveLoadPanel = panel;
            bool preloadCallback = true;
            controller.RequestQuickSave();
            controller.RequestAutoSave();
            controller.RequestQuickLoad();
            controller.RequestPreLoadAutoSave(result => preloadCallback = result);
            panel.OpenSave();
            panel.OpenLoad();
            Require(!preloadCallback && !panel.IsOpen, "Controller/UI replay gate opened save UI or accepted pre-load save.");

            GameObject quickObject = new GameObject("Replay Guard Quick Menu");
            VNQuickMenu quick = quickObject.AddComponent<VNQuickMenu>();
            quick.dialogueController = controller;
            quick.saveButton = CreateButton("Save", quickObject.transform);
            quick.quickSaveButton = CreateButton("Quick Save", quickObject.transform);
            quick.quickLoadButton = CreateButton("Quick Load", quickObject.transform);
            quick.loadButton = CreateButton("Load", quickObject.transform);
            quick.historyButton = CreateButton("History", quickObject.transform);
            quick.skipButton = CreateButton("Skip", quickObject.transform);
            quick.autoButton = CreateButton("Auto", quickObject.transform);
            quick.settingsButton = CreateButton("Settings", quickObject.transform);
            quick.mainMenuButton = CreateButton("Main Menu", quickObject.transform);
            quick.RefreshReplayPresentation();
            Require(!quick.saveButton.gameObject.activeSelf && !quick.quickSaveButton.gameObject.activeSelf
                && !quick.quickLoadButton.gameObject.activeSelf && !quick.loadButton.gameObject.activeSelf,
                "Replay Quick Menu kept Save/Load actions visible.");
            Require(quick.historyButton.gameObject.activeSelf && quick.skipButton.gameObject.activeSelf
                && quick.autoButton.gameObject.activeSelf && !quick.settingsButton.gameObject.activeSelf
                && !quick.mainMenuButton.gameObject.activeSelf,
                "Replay Quick Menu did not preserve the compact reading-action composition.");

            session.BeginEnding();
            session.RestoreCampaignState(state);
            session.MarkEnded();
            SetPrivateField(flow, "replaySession", null);
            AssertStateEquals(expected, state);
            quick.RefreshReplayPresentation();
            Require(quick.quickSaveButton.gameObject.activeSelf && !quick.saveButton.gameObject.activeSelf
                && !quick.quickLoadButton.gameObject.activeSelf && !quick.loadButton.gameObject.activeSelf
                && !quick.settingsButton.gameObject.activeSelf && !quick.mainMenuButton.gameObject.activeSelf,
                "Quick Menu did not restore only Quick Save after replay.");

            SetStaticInstance(typeof(VNDialogueController), null);
            SetStaticInstance(typeof(SceneFlowManager), null);
            UnityEngine.Object.DestroyImmediate(quickObject);
            UnityEngine.Object.DestroyImmediate(panelObject);
            UnityEngine.Object.DestroyImmediate(controllerObject);
            UnityEngine.Object.DestroyImmediate(saveObject);
            UnityEngine.Object.DestroyImmediate(flowObject);
            UnityEngine.Object.DestroyImmediate(stateObject);
            UnityEngine.Object.DestroyImmediate(definition);
            UnityEngine.Object.DestroyImmediate(start);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static void TestSaveSchemaRemainsV3AndReplayFree()
    {
        Require(SaveData.CurrentVersion == 3, "Gallery/Replay changed SaveData.CurrentVersion.");
        string[] forbidden = { "replayId", "isReplay", "ReplayContext", "snapshot", "unlockedReplayIds" };
        var fields = typeof(SaveData).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(field => field.Name)
            .ToArray();
        Require(!forbidden.Any(name => fields.Contains(name)), "SaveData contains replay/profile state.");
    }

    private static DialogueSceneData CreateScene(string sceneId, string lineId)
    {
        DialogueSceneData scene = ScriptableObject.CreateInstance<DialogueSceneData>();
        scene.sceneId = sceneId;
        scene.displayName = "TECH DEMO ONLY - NOT CANON";
        scene.lines.Add(new DialogueLine { lineId = lineId, text = "TEST REPLAY" });
        return scene;
    }

    private static Button CreateButton(string label, Transform parent)
    {
        GameObject objectWithButton = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        objectWithButton.transform.SetParent(parent, false);
        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(objectWithButton.transform, false);
        labelObject.GetComponent<TextMeshProUGUI>().text = label;
        return objectWithButton.GetComponent<Button>();
    }

    private static void SeedState(GameState state)
    {
        state.lust = 11;
        state.romance = 12;
        state.purity = 13;
        state.corruptionLevel = 14;
        state.selfControl = 15;
        state.suspicion = 16;
        state.trustMasha = 17;
        state.trustArtem = 18;
        state.leraInterest = 19;
        state.currentSceneId = "campaign_scene";
        state.currentLineIndex = 7;
        state.currentLineId = "campaign_line";
        state.selectedChoiceIndex = 2;
        state.choiceResultActive = true;
        state.pendingNextSceneId = "campaign_next";
    }

    private static void AssertStateEquals(ReplayGameStateSnapshot expected, GameState actual)
    {
        Require(actual.lust == expected.lust && actual.romance == expected.romance && actual.purity == expected.purity, "Primary stats were not restored.");
        Require(actual.corruptionLevel == expected.corruptionLevel && actual.selfControl == expected.selfControl && actual.suspicion == expected.suspicion, "Secondary stats were not restored.");
        Require(actual.trustMasha == expected.trustMasha && actual.trustArtem == expected.trustArtem && actual.leraInterest == expected.leraInterest, "Relationship stats were not restored.");
        Require(actual.currentSceneId == expected.currentSceneId && actual.currentLineIndex == expected.currentLineIndex && actual.currentLineId == expected.currentLineId, "Campaign cursor was not restored.");
        Require(actual.selectedChoiceIndex == expected.selectedChoiceIndex && actual.choiceResultActive == expected.choiceResultActive
            && actual.pendingNextSceneId == expected.pendingNextSceneId, "Campaign choice state was not restored.");
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Require(field != null, $"Private test hook {target.GetType().Name}.{fieldName} was not found.");
        field.SetValue(target, value);
    }

    private static void SetStaticInstance(Type type, UnityEngine.Object value)
    {
        FieldInfo field = type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        Require(field != null, $"Static Instance backing field for {type.Name} was not found.");
        field.SetValue(null, value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
