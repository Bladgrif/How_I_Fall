using System;
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
        Require(VNDialogueController.IsAllTextSkipMode("???"), "Existing all-text setting must be recognized.");
        Require(VNDialogueController.IsAllTextSkipMode("All"), "English all-text setting must be recognized.");
        Require(!VNDialogueController.IsAllTextSkipMode("????????"), "Seen-only setting must remain the safe default.");

        // Choice result is synthetic text. Runtime never calls Choose automatically:
        // it only resumes after a manual choice when skipAfterChoices is true.
        PlayerPrefs.DeleteKey(TestStoreKey);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
