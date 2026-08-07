using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class SkipDialogueSmokeTests
{
    private const string TestStoreKey = "hif_skip_dialogue_smoke_test";

    [MenuItem("How I Fall/Tests/Run Skip Dialogue Smoke Tests")]
    public static void RunFromMenu()
    {
        RunBatchMode();
        Debug.Log("How I Fall skip dialogue smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        PlayerPrefs.DeleteKey(TestStoreKey);

        Require(DialogueReadHistory.CreateKey("scene_a", "line_1") == "scene_a::line_1", "Seen key must use sceneId + lineId.");
        Require(string.IsNullOrEmpty(DialogueReadHistory.CreateKey("scene_a", string.Empty)), "Seen keys require a stable lineId.");

        DialogueReadHistory firstSession = new DialogueReadHistory(TestStoreKey);
        Require(!firstSession.IsSeen("scene_a", "line_1"), "Fresh test store must not mark lines as seen.");
        firstSession.MarkSeen("scene_a", "line_1");
        Require(firstSession.IsSeen("scene_a", "line_1"), "Fully shown line must be marked seen.");

        DialogueReadHistory nextSession = new DialogueReadHistory(TestStoreKey);
        Require(nextSession.IsSeen("scene_a", "line_1"), "Read history must survive a new runtime service instance.");
        Require(!nextSession.IsSeen("scene_a", "line_2"), "Seen-only skip must stop before an unread line.");

        Require(Mathf.Approximately(VNDialogueController.GetSkipCadenceSeconds(), 0.12f), "Skip cadence must be bounded and realtime-safe.");
        Require(VNDialogueController.IsAllTextSkipMode("\u0412\u0441\u0435"), "Existing all-text setting must be recognized.");
        Require(VNDialogueController.IsAllTextSkipMode("All"), "English all-text setting must be recognized.");
        Require(!VNDialogueController.IsAllTextSkipMode("\u0412\u0438\u0434\u0435\u043d\u043d\u043e\u0435"), "Seen-only setting must remain the safe default.");

        TestReadHistoryLifecycle();
        PlayerPrefs.DeleteKey(TestStoreKey);
    }

    private static void TestReadHistoryLifecycle()
    {
        GameObject gameObject = new GameObject("SkipDialogueSmokeController");
        DialogueSceneData scene = ScriptableObject.CreateInstance<DialogueSceneData>();
        scene.sceneId = "skip_smoke_scene";
        DialogueLine line = new DialogueLine { lineId = "skip_smoke_line" };

        try
        {
            VNDialogueController controller = gameObject.AddComponent<VNDialogueController>();
            FieldInfo historyField = typeof(VNDialogueController).GetField("readHistory", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo sceneField = typeof(VNDialogueController).GetField("displayedLineScene", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo lineField = typeof(VNDialogueController).GetField("displayedLine", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo markMethod = typeof(VNDialogueController).GetMethod("MarkDisplayedLineSeen", BindingFlags.Instance | BindingFlags.NonPublic);

            Require(historyField != null && sceneField != null && lineField != null && markMethod != null, "Read-history lifecycle members are missing.");

            DialogueReadHistory testHistory = new DialogueReadHistory(TestStoreKey);
            historyField.SetValue(controller, testHistory);
            sceneField.SetValue(controller, scene);
            lineField.SetValue(controller, line);
            markMethod.Invoke(controller, null);
            Require(testHistory.IsSeen(scene.sceneId, line.lineId), "Completing a real DialogueLine must mark its stable key as seen.");

            sceneField.SetValue(controller, null);
            lineField.SetValue(controller, null);
            markMethod.Invoke(controller, null);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(scene);
            UnityEngine.Object.DestroyImmediate(gameObject);
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
