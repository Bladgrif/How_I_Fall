using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class BacklogRestorationSmokeTests
{
    private sealed class TestContext : IDisposable
    {
        public readonly string DirectoryPath;
        public readonly DialogueSceneData Scene;
        public readonly DialogueSceneRegistry Registry;
        public readonly GameObject ManagerObject;
        public readonly SaveManager Manager;
        public readonly GameObject GameStateObject;
        public readonly GameState State;
        public readonly GameObject ControllerObject;
        public readonly VNDialogueController Controller;

        public TestContext()
        {
            DirectoryPath = Path.Combine(
                Path.GetTempPath(),
                "HowIFall_BacklogRestoration_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);

            Scene = ScriptableObject.CreateInstance<DialogueSceneData>();
            Scene.sceneId = "backlog_scene";
            Scene.displayName = "Backlog Test";
            Scene.lines.Add(new DialogueLine
            {
                lineId = "backlog_line_0",
                speaker = "Лера",
                text = "Текущая реплика"
            });

            Registry = ScriptableObject.CreateInstance<DialogueSceneRegistry>();
            Registry.scenes.Add(Scene);

            ManagerObject = new GameObject("Backlog Restoration SaveManager");
            Manager = ManagerObject.AddComponent<SaveManager>();
            Manager.ConfigureRegistry(Registry);
            Manager.ConfigureSaveDirectoryForTests(DirectoryPath);

            GameStateObject = new GameObject("Backlog Restoration GameState");
            State = GameStateObject.AddComponent<GameState>();
            SetStaticInstance(typeof(GameState), State);
            State.currentSceneId = Scene.sceneId;
            State.currentLineId = Scene.lines[0].lineId;
            State.currentLineIndex = 0;
            State.selectedChoiceIndex = -1;
            State.choiceResultActive = false;
            State.pendingNextSceneId = string.Empty;
            State.lust = 11;
            State.romance = 12;
            State.purity = 13;
            State.corruptionLevel = 14;
            State.selfControl = 15;
            State.suspicion = 16;
            State.trustMasha = 17;
            State.trustArtem = 18;
            State.leraInterest = 19;

            ControllerObject = new GameObject("Backlog Restoration VNDialogueController");
            Controller = ControllerObject.AddComponent<VNDialogueController>();
            SetStaticInstance(typeof(VNDialogueController), Controller);
            Controller.sceneData = Scene;
            Controller.sceneRegistry = Registry;
            SetPrivateField(Controller, "activeLines", Scene.lines);
            SetPrivateField(Controller, "currentLineIndex", 0);
        }

        public void Dispose()
        {
            SetStaticInstance(typeof(VNDialogueController), null);
            SetStaticInstance(typeof(GameState), null);
            UnityEngine.Object.DestroyImmediate(ControllerObject);
            UnityEngine.Object.DestroyImmediate(GameStateObject);
            UnityEngine.Object.DestroyImmediate(ManagerObject);
            UnityEngine.Object.DestroyImmediate(Registry);
            UnityEngine.Object.DestroyImmediate(Scene);

            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, true);
            }
        }
    }

    [MenuItem("How I Fall/Tests/Run Backlog Restoration Smoke Tests")]
    public static void RunFromMenu()
    {
        Run();
        Debug.Log("How I Fall backlog restoration smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        Run();
        Debug.Log("How I Fall backlog restoration smoke tests passed.");
    }

    private static void Run()
    {
        TestSnapshotApiCapacitySanitationAndFormatting();

        using var context = new TestContext();
        TestV3CaptureForManualAutoAndQuick(context);
        TestLoadReplacesInsteadOfMergingAndRollsBack(context);
        TestPendingSnapshotLifecycle(context);
        TestLegacyVersionsRemainLoadable(context);
        TestMalformedAndOversizedOptionalSnapshots(context);
        TestContinueKeepsSelectedSnapshot(context);
        TestScopedSuppressionResetsAfterFailure(context);
    }

    private static void TestSnapshotApiCapacitySanitationAndFormatting()
    {
        var backlog = new DialogueBacklog(DialogueBacklog.DefaultCapacity);
        backlog.Add("<Лера>", "До <двери> & обратно");

        List<DialogueBacklogEntry> captured = backlog.CaptureSnapshot();
        Require(captured.Count == 1, "Snapshot capture lost an entry.");
        Require(captured[0].speaker == "<Лера>" && captured[0].text == "До <двери> & обратно", "Snapshot changed raw speaker/text.");

        captured[0].text = "mutated copy";
        Require(backlog.CaptureSnapshot()[0].text == "До <двери> & обратно", "Snapshot exposed the mutable internal list.");

        var roundTrip = new DialogueBacklog(DialogueBacklog.DefaultCapacity);
        roundTrip.ReplaceFromSnapshot(backlog.CaptureSnapshot());
        Require(roundTrip.CaptureSnapshot()[0].text == "До <двери> & обратно", "Snapshot round-trip changed raw text.");
        string formatted = roundTrip.BuildRichText();
        Require(formatted.Contains("&lt;Лера&gt;") && formatted.Contains("&lt;двери&gt; &amp; обратно"), "Formatting after snapshot restore did not escape rich text.");

        string warning = string.Empty;
        roundTrip.ReplaceFromSnapshot(
            new DialogueBacklogEntry[]
            {
                null,
                new DialogueBacklogEntry { speaker = "Ignored", text = null },
                new DialogueBacklogEntry { speaker = "Ignored", text = "   " },
                new DialogueBacklogEntry { speaker = null, text = "Valid" },
                new DialogueBacklogEntry
                {
                    speaker = "Too long",
                    text = new string('x', DialogueBacklog.MaximumEntryTextLength + 1)
                }
            },
            message => warning = message);
        List<DialogueBacklogEntry> sanitized = roundTrip.CaptureSnapshot();
        Require(sanitized.Count == 1 && sanitized[0].speaker == string.Empty && sanitized[0].text == "Valid", "Null/empty snapshot sanitation is incorrect.");
        Require(warning.Contains(DialogueBacklog.MaximumEntryTextLength.ToString()), "Oversized snapshot entry did not produce an explicit warning.");

        var oversizedCount = new List<DialogueBacklogEntry>();
        for (int index = 0; index < 105; index++)
        {
            oversizedCount.Add(new DialogueBacklogEntry { speaker = "S", text = index.ToString() });
        }

        roundTrip.ReplaceFromSnapshot(oversizedCount);
        List<DialogueBacklogEntry> bounded = roundTrip.CaptureSnapshot();
        Require(bounded.Count == 100, "Snapshot capacity is not 100.");
        Require(bounded[0].text == "5" && bounded[99].text == "104", "Oversized snapshot did not keep the newest 100 entries.");
        roundTrip.Clear();
        Require(roundTrip.Count == 0, "Clear did not empty the backlog.");
    }

    private static void TestV3CaptureForManualAutoAndQuick(TestContext context)
    {
        context.Controller.ReplaceBacklogFromSnapshot(new[]
        {
            new DialogueBacklogEntry { speaker = "Маша", text = "До сохранения" },
            new DialogueBacklogEntry { speaker = "<Лера>", text = "Текущая <реплика> & знак" }
        });

        var preview = new Texture2D(8, 8, TextureFormat.RGB24, false);
        try
        {
            foreach (SaveSlotType type in new[] { SaveSlotType.Manual, SaveSlotType.Auto, SaveSlotType.Quick })
            {
                Require(context.Manager.SaveSlot(type, 1, preview), $"{type} v3 save failed.");
                SaveSlotInfo slot = context.Manager.GetSlot(type, 1);
                Require(slot.IsLoadable, $"{type} v3 save is not loadable: {slot.Error}");
                Require(slot.Data.version == 3 && slot.Data.sourceVersion == 3, $"{type} did not persist v3.");
                Require(slot.Data.backlogSnapshotAvailable, $"{type} snapshot was not recognized.");
                Require(slot.Data.backlogEntries.Count == 2, $"{type} snapshot entry count is incorrect.");
                Require(slot.Data.backlogEntries[1].speaker == "<Лера>" && slot.Data.backlogEntries[1].text == "Текущая <реплика> & знак", $"{type} changed raw backlog text during persistence.");
                Require(slot.Data.lust == 11 && slot.Data.romance == 12 && slot.Data.purity == 13, $"{type} changed unrelated primary SaveData fields.");
                Require(slot.Data.suspicion == 16 && slot.Data.trustMasha == 17 && slot.Data.leraInterest == 19, $"{type} changed unrelated state fields.");

                var restored = new DialogueBacklog(DialogueBacklog.DefaultCapacity);
                restored.ReplaceFromSnapshot(slot.Data.backlogEntries.Select(entry => new DialogueBacklogEntry
                {
                    speaker = entry.speaker,
                    text = entry.text
                }));
                Require(restored.BuildRichText().Contains("&lt;реплика&gt; &amp; знак"), $"{type} deserialize path bypassed safe formatting.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(preview);
        }
    }

    private static void TestLoadReplacesInsteadOfMergingAndRollsBack(TestContext context)
    {
        MethodInfo method = GetPrivateMethod(typeof(SaveManager), "TryApplyInPlace");
        SaveData loaded = CreateData(SaveSlotType.Manual, 2, 3, "saved-old", "saved-current");
        loaded.backlogSnapshotAvailable = true;

        context.Controller.ReplaceBacklogFromSnapshot(new[]
        {
            new DialogueBacklogEntry { text = "saved-old" },
            new DialogueBacklogEntry { text = "future-branch" }
        });
        object[] successArguments = { loaded, 2, context.State, new Func<bool>(() => true), null };
        Require((bool)method.Invoke(context.Manager, successArguments), "Successful in-place restore reported failure.");
        Require(Texts(context.Controller).SequenceEqual(new[] { "saved-old", "saved-current" }), "In-place Load merged future entries instead of replacing the backlog.");
        Require(!context.Manager.HasPendingSceneRestore, "Successful in-place restore left pending state.");

        context.State.lust = 31;
        context.Controller.ReplaceBacklogFromSnapshot(new[]
        {
            new DialogueBacklogEntry { text = "previous-session" }
        });
        loaded.lust = 99;
        object[] failureArguments = { loaded, 2, context.State, new Func<bool>(() => false), null };
        Require(!(bool)method.Invoke(context.Manager, failureArguments), "Failed in-place restore reported success.");
        Require(context.State.lust == 31, "Failed in-place restore did not restore previous GameState.");
        Require(Texts(context.Controller).SequenceEqual(new[] { "previous-session" }), "Failed in-place restore did not restore previous backlog.");
        Require(!context.Manager.HasPendingSceneRestore, "Failed in-place restore left pending state.");
    }

    private static void TestPendingSnapshotLifecycle(TestContext context)
    {
        SaveData data = CreateData(SaveSlotType.Quick, 4, 3, "one", "two");
        data.backlogSnapshotAvailable = true;
        InvokePrivate(context.Manager, "SetPendingBacklogRestore", data);
        InvokePrivate(context.Manager, "BeginPendingSceneRestore", 4);

        context.Manager.GetPendingBacklogRestore(out List<DialogueBacklogEntry> first, out bool firstAvailable);
        Require(firstAvailable && first.Count == 2, "Scene-reload pending snapshot was not transferred.");
        first[0].text = "mutated-copy";
        context.Manager.GetPendingBacklogRestore(out List<DialogueBacklogEntry> second, out bool secondAvailable);
        Require(secondAvailable && second[0].text == "one", "Pending snapshot transfer exposed mutable internal data.");

        context.Manager.CompletePendingSceneRestore();
        context.Manager.GetPendingBacklogRestore(out List<DialogueBacklogEntry> afterSuccess, out bool availableAfterSuccess);
        Require(!availableAfterSuccess && afterSuccess.Count == 0, "Pending snapshot was not cleared after success or was transferred more than once.");

        InvokePrivate(context.Manager, "SetPendingBacklogRestore", data);
        InvokePrivate(context.Manager, "BeginPendingSceneRestore", 4);
        context.Manager.FailPendingSceneRestoreAndReset();
        context.Manager.GetPendingBacklogRestore(out List<DialogueBacklogEntry> afterFailure, out bool availableAfterFailure);
        Require(!availableAfterFailure && afterFailure.Count == 0, "Pending snapshot was not cleared after failure.");

        context.Controller.ReplaceBacklogFromSnapshot(new[] { new DialogueBacklogEntry { text = "old session" } });
        context.Controller.ClearBacklog();
        context.Manager.ClearPendingLoad();
        Require(context.Controller.CaptureBacklogSnapshot().Count == 0, "New Game empty-backlog contract failed.");
    }

    private static void TestLegacyVersionsRemainLoadable(TestContext context)
    {
        SaveData v1 = CreateData(SaveSlotType.Manual, 3, 1);
        string v1Json = ToLegacyJson(v1, removeSlotType: true);
        WriteJson(context, SaveSlotType.Manual, 3, v1Json);
        SaveSlotInfo v1Slot = context.Manager.GetSlot(SaveSlotType.Manual, 3);
        Require(v1Slot.IsLoadable && v1Slot.Data.sourceVersion == 1 && v1Slot.Data.version == 3, $"v1 Manual save is not loadable: {v1Slot.Error}");
        Require(!v1Slot.Data.backlogSnapshotAvailable, "v1 unexpectedly acquired a backlog snapshot.");
        Require(File.ReadAllText(context.Manager.GetSlotJsonPath(SaveSlotType.Manual, 3)) == v1Json, "Reading v1 rewrote legacy JSON.");

        foreach (SaveSlotType type in new[] { SaveSlotType.Manual, SaveSlotType.Auto, SaveSlotType.Quick })
        {
            SaveData v2 = CreateData(type, 4, 2);
            string v2Json = ToLegacyJson(v2, removeSlotType: false);
            WriteJson(context, type, 4, v2Json);
            SaveSlotInfo slot = context.Manager.GetSlot(type, 4);
            Require(slot.IsLoadable, $"v2 {type} save is not loadable: {slot.Error}");
            Require(slot.Data.sourceVersion == 2 && slot.Data.version == 3, $"v2 {type} was not migrated in memory.");
            Require(!slot.Data.backlogSnapshotAvailable, $"v2 {type} unexpectedly acquired a backlog snapshot.");
            Require(File.ReadAllText(context.Manager.GetSlotJsonPath(type, 4)) == v2Json, $"Reading v2 {type} rewrote legacy JSON.");
        }
    }

    private static void TestMalformedAndOversizedOptionalSnapshots(TestContext context)
    {
        SaveData malformed = CreateData(SaveSlotType.Manual, 5, 3, "valid");
        string malformedJson = JsonUtility.ToJson(malformed, false);
        malformedJson = Regex.Replace(
            malformedJson,
            "\\\"backlogEntries\\\"\\s*:\\s*\\[[^\\]]*\\]",
            "\"backlogEntries\":{\"unexpected\":true}");
        WriteJson(context, SaveSlotType.Manual, 5, malformedJson);
        SaveSlotInfo malformedSlot = context.Manager.GetSlot(SaveSlotType.Manual, 5);
        Require(malformedSlot.IsLoadable, $"Malformed optional backlog invalidated the core save: {malformedSlot.Error}");
        Require(!malformedSlot.Data.backlogSnapshotAvailable && malformedSlot.Data.lust == malformed.lust, "Malformed optional backlog did not use the safe empty fallback.");

        SaveData oversized = CreateData(
            SaveSlotType.Quick,
            5,
            3,
            new string('x', DialogueBacklog.MaximumEntryTextLength + 1));
        WriteJson(context, SaveSlotType.Quick, 5, JsonUtility.ToJson(oversized, false));
        SaveSlotInfo oversizedSlot = context.Manager.GetSlot(SaveSlotType.Quick, 5);
        Require(oversizedSlot.IsLoadable && oversizedSlot.Data.backlogSnapshotAvailable, "Oversized optional entry invalidated the slot.");
        Require(oversizedSlot.Data.backlogEntries.Count == 0, "Oversized optional entry was silently retained or truncated.");

        SaveData nullSnapshot = CreateData(SaveSlotType.Auto, 5, 3);
        string nullJson = Regex.Replace(
            JsonUtility.ToJson(nullSnapshot, false),
            "\\\"backlogEntries\\\"\\s*:\\s*\\[[^\\]]*\\]",
            "\"backlogEntries\":null");
        WriteJson(context, SaveSlotType.Auto, 5, nullJson);
        SaveSlotInfo nullSlot = context.Manager.GetSlot(SaveSlotType.Auto, 5);
        Require(nullSlot.IsLoadable && !nullSlot.Data.backlogSnapshotAvailable, "Null optional snapshot did not use legacy-like fallback.");
    }

    private static void TestContinueKeepsSelectedSnapshot(TestContext context)
    {
        SaveData manual = CreateData(SaveSlotType.Manual, 6, 3, "manual-history");
        manual.createdAtUtc = "2099-08-07T10:00:00.0000000Z";
        WriteJson(context, SaveSlotType.Manual, 6, JsonUtility.ToJson(manual, false));

        SaveData auto = CreateData(SaveSlotType.Auto, 6, 3, "continue-history");
        auto.createdAtUtc = "2099-08-07T11:00:00.0000000Z";
        WriteJson(context, SaveSlotType.Auto, 6, JsonUtility.ToJson(auto, false));

        SaveSlotInfo latest = (SaveSlotInfo)GetPrivateMethod(typeof(SaveManager), "FindLatestLoadableSlot")
            .Invoke(context.Manager, null);
        Require(latest != null && latest.SlotType == SaveSlotType.Auto, "Continue did not select the newest valid slot.");
        Require(latest.Data.backlogSnapshotAvailable && latest.Data.backlogEntries.Single().text == "continue-history", "Continue did not retain the selected slot snapshot.");
    }

    private static void TestScopedSuppressionResetsAfterFailure(TestContext context)
    {
        context.Controller.ReplaceBacklogFromSnapshot(new[]
        {
            new DialogueBacklogEntry { text = "visible current line" }
        });

        MethodInfo add = GetPrivateMethod(typeof(VNDialogueController), "AddToBacklog");
        MethodInfo suppress = GetPrivateMethod(typeof(VNDialogueController), "RunWithoutBacklogCapture");
        suppress.Invoke(context.Controller, new object[]
        {
            new Func<bool>(() =>
            {
                add.Invoke(context.Controller, new object[] { string.Empty, "visible current line" });
                add.Invoke(context.Controller, new object[] { string.Empty, "choice result" });
                return true;
            })
        });
        Require(Texts(context.Controller).SequenceEqual(new[] { "visible current line" }), "Restore suppression duplicated current line or choice result.");

        try
        {
            suppress.Invoke(context.Controller, new object[]
            {
                new Func<bool>(() => throw new InvalidOperationException("expected suppression test"))
            });
        }
        catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException)
        {
        }

        add.Invoke(context.Controller, new object[] { string.Empty, "normal dialogue after failure" });
        Require(Texts(context.Controller).Last() == "normal dialogue after failure", "Backlog suppression remained active after an exception.");
    }

    private static SaveData CreateData(
        SaveSlotType type,
        int slotIndex,
        int version,
        params string[] backlogTexts)
    {
        return new SaveData
        {
            version = version,
            slotType = type,
            slotIndex = slotIndex,
            createdAtUtc = "2026-08-07T09:00:00.0000000Z",
            sceneId = "backlog_scene",
            lineId = "backlog_line_0",
            lineIndex = 0,
            selectedChoiceIndex = -1,
            choiceResultActive = false,
            pendingNextSceneId = string.Empty,
            previewFileName = GetPreviewName(type, slotIndex),
            backlogEntries = backlogTexts.Select(text => new BacklogEntryData
            {
                speaker = string.Empty,
                text = text
            }).ToList(),
            lust = 21,
            romance = 22,
            purity = 23,
            corruptionLevel = 24,
            selfControl = 25,
            suspicion = 26,
            trustMasha = 27,
            trustArtem = 28,
            leraInterest = 29
        };
    }

    private static string ToLegacyJson(SaveData data, bool removeSlotType)
    {
        string json = JsonUtility.ToJson(data, false);
        json = Regex.Replace(json, ",?\\\"backlogEntries\\\"\\s*:\\s*\\[[^\\]]*\\]", string.Empty);
        if (removeSlotType)
        {
            json = Regex.Replace(json, ",?\\\"slotType\\\"\\s*:\\s*\\d+", string.Empty);
        }

        return json;
    }

    private static void WriteJson(TestContext context, SaveSlotType type, int slotIndex, string json)
    {
        File.WriteAllText(context.Manager.GetSlotJsonPath(type, slotIndex), json);
    }

    private static string GetPreviewName(SaveSlotType type, int slotIndex)
    {
        return type switch
        {
            SaveSlotType.Manual => $"slot_{slotIndex:D2}.png",
            SaveSlotType.Auto => $"auto_{slotIndex:D2}.png",
            SaveSlotType.Quick => $"quick_{slotIndex:D2}.png",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    private static string[] Texts(VNDialogueController controller)
    {
        return controller.CaptureBacklogSnapshot().Select(entry => entry.text).ToArray();
    }

    private static MethodInfo GetPrivateMethod(Type type, string name)
    {
        MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
        Require(method != null, $"Private test hook {type.Name}.{name} was not found.");
        return method;
    }

    private static object InvokePrivate(object target, string methodName, params object[] arguments)
    {
        return GetPrivateMethod(target.GetType(), methodName).Invoke(target, arguments);
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
