using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class TimedNarrativeBeatSmokeTests
{
    private const string ExpectedEndPrototypeText = "\u041a\u043e\u043d\u0435\u0446 Unity-\u043f\u0440\u043e\u0442\u043e\u0442\u0438\u043f\u0430.";
    private const string ExpectedSuccessText = "TEST: success";
    private const string ExpectedTimeoutText = "TEST: timeout";

    [MenuItem("How I Fall/Tests/Run Timed Narrative Beat Smoke Tests")]
    public static void RunFromMenu()
    {
        RunBatchMode();
        Debug.Log("How I Fall timed narrative beat smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        TestTerminalAndDemoResultContentContract();
        TestManualSuccessAndExactlyOnce();
        TestImmediateTimeoutAndRejectedStarts();
        TestConflictAndLifecycleCleanup();
        Require(SaveData.CurrentVersion == 3, "Timed beat must preserve SaveData v3.");
    }

    private static void TestTerminalAndDemoResultContentContract()
    {
        FieldInfo endTextField = typeof(VNDialogueController).GetField(
            "EndPrototypeText",
            BindingFlags.NonPublic | BindingFlags.Static);
        Require(endTextField != null, "VN terminal text contract must exist.");
        Require(
            string.Equals((string)endTextField.GetRawConstantValue(), ExpectedEndPrototypeText, StringComparison.Ordinal),
            "VN terminal text must use the readable Russian contract, not mojibake.");

        DialogueSceneData success = AssetDatabase.LoadAssetAtPath<DialogueSceneData>(
            "Assets/HowIFall/Data/Dialogues/timed_demo_success.asset");
        DialogueSceneData timeout = AssetDatabase.LoadAssetAtPath<DialogueSceneData>(
            "Assets/HowIFall/Data/Dialogues/timed_demo_timeout.asset");
        RequireSceneHasExactFirstLine(success, ExpectedSuccessText, "Success demo target");
        RequireSceneHasExactFirstLine(timeout, ExpectedTimeoutText, "Timeout demo target");
    }

    private static void TestManualSuccessAndExactlyOnce()
    {
        using (TimedBeatFixture fixture = new TimedBeatFixture())
        {
            Require(fixture.beat.TryStartBeat(fixture.ValidDefinition(5f)), "A valid definition must start.");
            Require(fixture.beat.State == TimedNarrativeBeatState.Running, "A valid beat must enter Running.");
            Require(fixture.dialogue.HasActiveSpecialMode, "A running beat must own a special-mode lease.");
            Require(!fixture.dialogue.CanAdvanceDialogue && !fixture.dialogue.CanSave && !fixture.dialogue.CanLoad, "BlockingExclusive must gate dialogue and save/load.");
            Require(!fixture.dialogue.CanOpenQuickMenu && !fixture.dialogue.CanOpenBacklog && !fixture.dialogue.CanOpenSettings, "BlockingExclusive must gate modal UI actions.");
            Require(!fixture.dialogue.TryHideInterface(), "Clean view must be rejected during a beat.");
            Require(fixture.panel.activeSelf, "Running beat UI must be visible.");
            Require(!fixture.beat.TryStartBeat(fixture.ValidDefinition(5f)), "A second start while running must be rejected.");

            Require(fixture.beat.ResolveFromManualAction(), "Manual action must resolve the beat.");
            Require(fixture.beat.State == TimedNarrativeBeatState.Resolved, "Manual action must resolve exactly once.");
            Require(!fixture.dialogue.HasActiveSpecialMode && fixture.dialogue.CanAdvanceDialogue, "Manual resolution must release the lease.");
            Require(!fixture.panel.activeSelf, "Manual resolution must hide beat UI.");
            Require(ReferenceEquals(fixture.dialogue.sceneData, fixture.success), "Manual action must use the normal success scene route.");
            RequireSceneHasExactFirstLine(fixture.dialogue.sceneData, ExpectedSuccessText, "Manual success route");
            fixture.dialogue.AdvanceDialogue();
            Require(ReferenceEquals(fixture.dialogue.sceneData, fixture.success), "The resolution event must not also advance past the success result scene.");
            Require(!fixture.beat.ResolveFromManualAction(), "Second manual callback must be a no-op.");
            Require(ReferenceEquals(fixture.dialogue.sceneData, fixture.success), "Second manual callback must not route again.");
        }
    }

    private static void TestImmediateTimeoutAndRejectedStarts()
    {
        using (TimedBeatFixture fixture = new TimedBeatFixture())
        {
            Require(fixture.beat.TryStartBeat(fixture.ValidDefinition(0f)), "Non-positive runtime duration must resolve through the safe timeout path.");
            Require(fixture.beat.State == TimedNarrativeBeatState.Resolved, "Non-positive duration must not leave a running beat.");
            Require(!fixture.dialogue.HasActiveSpecialMode && !fixture.panel.activeSelf, "Immediate timeout must release lease and hide UI.");
            Require(ReferenceEquals(fixture.dialogue.sceneData, fixture.timeout), "Immediate timeout must route to timeout scene.");
            RequireSceneHasExactFirstLine(fixture.dialogue.sceneData, ExpectedTimeoutText, "Timeout route");
            fixture.dialogue.AdvanceDialogue();
            Require(ReferenceEquals(fixture.dialogue.sceneData, fixture.timeout), "The resolution event must not also advance past the timeout result scene.");
            Require(!fixture.beat.ResolveFromManualAction(), "Callback after timeout must be a no-op.");

            Require(!fixture.beat.TryStartBeat(null), "Null definition must fail cleanly.");
            Require(!fixture.panel.activeSelf && !fixture.dialogue.HasActiveSpecialMode, "Rejected start must leave no visible UI or lease.");
        }
    }

    private static void TestConflictAndLifecycleCleanup()
    {
        using (TimedBeatFixture fixture = new TimedBeatFixture())
        {
            GameObject competingOwner = new GameObject("TimedBeatCompetingOwner");
            try
            {
                Require(fixture.dialogue.TryEnterSpecialMode(competingOwner, SpecialModePolicy.BlockingExclusive, out SpecialModeLease lease), "Conflict fixture must acquire first lease.");
                Require(!fixture.beat.TryStartBeat(fixture.ValidDefinition(5f)), "Special-mode conflict must reject timed beat start.");
                Require(!fixture.panel.activeSelf, "Conflict rejection must hide UI.");
                Require(fixture.dialogue.ExitSpecialMode(lease), "Conflict fixture must clean up lease.");

                Require(fixture.beat.TryStartBeat(fixture.ValidDefinition(5f)), "Lifecycle fixture must start.");
                UnityEngine.Object.DestroyImmediate(fixture.beat.gameObject);
                Require(!fixture.dialogue.HasActiveSpecialMode, "Destroyed beat controller must not leave a stale lease.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(competingOwner);
            }
        }
    }

    private sealed class TimedBeatFixture : IDisposable
    {
        public readonly GameObject controllerObject;
        public readonly VNDialogueController dialogue;
        public readonly GameObject beatObject;
        public readonly TimedNarrativeBeatController beat;
        public readonly GameObject panel;
        public readonly DialogueSceneData success;
        public readonly DialogueSceneData timeout;

        public TimedBeatFixture()
        {
            controllerObject = CreateUiObject("TimedBeatDialogueController");
            dialogue = controllerObject.AddComponent<VNDialogueController>();
            ConfigureDialogueUi(dialogue);
            success = CreateScene("timed_smoke_success", ExpectedSuccessText);
            timeout = CreateScene("timed_smoke_timeout", ExpectedTimeoutText);
            DialogueSceneRegistry registry = ScriptableObject.CreateInstance<DialogueSceneRegistry>();
            registry.scenes = new List<DialogueSceneData> { success, timeout };
            dialogue.sceneRegistry = registry;

            beatObject = CreateUiObject("TimedBeatController");
            beat = beatObject.AddComponent<TimedNarrativeBeatController>();
            panel = CreateUiObject("TimedBeatPanel");
            TextMeshProUGUI prompt = panel.AddComponent<TextMeshProUGUI>();
            GameObject actionObject = CreateUiObject("TimedBeatAction", panel.transform);
            actionObject.AddComponent<Image>();
            Button action = actionObject.AddComponent<Button>();
            CreateUiObject("Label", actionObject.transform).AddComponent<TextMeshProUGUI>();
            TextMeshProUGUI remaining = CreateUiObject("TimedBeatRemaining", panel.transform).AddComponent<TextMeshProUGUI>();
            Slider progress = CreateUiObject("TimedBeatProgress", panel.transform).AddComponent<Slider>();
            beat.dialogueController = dialogue;
            beat.rootPanel = panel;
            beat.promptText = prompt;
            beat.actionButton = action;
            beat.remainingTimeText = remaining;
            beat.progressSlider = progress;
            panel.SetActive(false);
        }

        public TimedNarrativeBeatDefinition ValidDefinition(float duration)
        {
            return new TimedNarrativeBeatDefinition
            {
                promptText = "TEST: timed beat",
                actionText = "Действовать",
                durationSeconds = duration,
                successNextScene = success,
                timeoutNextScene = timeout
            };
        }

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(panel);
            if (beatObject != null)
            {
                UnityEngine.Object.DestroyImmediate(beatObject);
            }

            UnityEngine.Object.DestroyImmediate(success);
            UnityEngine.Object.DestroyImmediate(timeout);
            UnityEngine.Object.DestroyImmediate(dialogue.sceneRegistry);
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }

        private static void ConfigureDialogueUi(VNDialogueController controller)
        {
            controller.speakerText = CreateUiObject("Speaker").AddComponent<TextMeshProUGUI>();
            controller.dialogueText = CreateUiObject("Dialogue").AddComponent<TextMeshProUGUI>();
            controller.backgroundImage = CreateUiObject("Background").AddComponent<Image>();
            controller.characterImage = CreateUiObject("Character").AddComponent<Image>();
            controller.nameBox = new GameObject("NameBox");
            controller.nextButton = CreateUiObject("Next").AddComponent<Button>();
            controller.choicePanel = new GameObject("ChoicePanel");
            controller.choicePanel.SetActive(false);
            controller.choiceDimOverlay = new GameObject("ChoiceOverlay");
            controller.choiceDimOverlay.SetActive(false);
            controller.choiceMashaButton = CreateUiObject("ChoiceOne").AddComponent<Button>();
            controller.choiceArtemButton = CreateUiObject("ChoiceTwo").AddComponent<Button>();
            controller.choiceLeraButton = CreateUiObject("ChoiceThree").AddComponent<Button>();
        }

        private static DialogueSceneData CreateScene(string sceneId, string text)
        {
            DialogueSceneData scene = ScriptableObject.CreateInstance<DialogueSceneData>();
            scene.sceneId = sceneId;
            scene.lines = new List<DialogueLine> { new DialogueLine { lineId = sceneId + "_line", text = text } };
            scene.choices = new List<DialogueChoice>();
            return scene;
        }
    }

    private static GameObject CreateUiObject(string name, Transform parent = null)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        if (parent != null)
        {
            result.transform.SetParent(parent, false);
        }

        return result;
    }

    private static void RequireSceneHasExactFirstLine(DialogueSceneData scene, string expectedText, string context)
    {
        Require(scene != null && scene.lines != null && scene.lines.Count > 0, $"{context} must contain a normal dialogue beat.");
        Require(string.Equals(scene.lines[0].text, expectedText, StringComparison.Ordinal), $"{context} must contain exact '{expectedText}'.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
