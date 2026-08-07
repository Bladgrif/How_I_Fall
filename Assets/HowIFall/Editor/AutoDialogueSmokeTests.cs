using System;
using UnityEditor;
using UnityEngine;

public static class AutoDialogueSmokeTests
{
    [MenuItem("How I Fall/Tests/Run Auto Dialogue Smoke Tests")]
    public static void RunFromMenu()
    {
        RunBatchMode();
        Debug.Log("How I Fall auto dialogue smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        Require(Mathf.Approximately(VNDialogueController.GetAutoForwardDelaySeconds(50f), 0.5f), "Auto delay minimum must be 0.5 seconds.");
        Require(Mathf.Approximately(VNDialogueController.GetAutoForwardDelaySeconds(250f), 2.5f), "Auto delay default must be 2.5 seconds.");
        Require(Mathf.Approximately(VNDialogueController.GetAutoForwardDelaySeconds(500f), 5f), "Auto delay maximum must be 5 seconds.");
        Require(Mathf.Approximately(VNDialogueController.GetAutoForwardDelaySeconds(-1f), 0.5f), "Auto delay must clamp below the stored range.");
        Require(Mathf.Approximately(VNDialogueController.GetAutoForwardDelaySeconds(999f), 5f), "Auto delay must clamp above the stored range.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
