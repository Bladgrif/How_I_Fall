using System;
using UnityEditor;
using UnityEngine;

public static class SettingsTruthSmokeTests
{
    [MenuItem("How I Fall/Tests/Run Settings Truth Smoke Tests")]
    public static void RunFromMenu()
    {
        RunBatchMode();
        Debug.Log("How I Fall settings truth smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        Require(SettingsManager.GetFullScreenMode("?????? ?????") == FullScreenMode.ExclusiveFullScreen, "Fullscreen setting must resolve to exclusive fullscreen.");
        Require(SettingsManager.GetFullScreenMode("????") == FullScreenMode.Windowed, "Windowed setting must resolve to windowed mode.");
        Require(SettingsManager.GetFullScreenMode("??? ?????") == FullScreenMode.FullScreenWindow, "Borderless setting must resolve to borderless fullscreen.");
        Require(SettingsManager.TryParseResolution("1920x1080", out int width, out int height) && width == 1920 && height == 1080, "Valid resolution must be parsed for Screen.SetResolution.");
        Require(!SettingsManager.TryParseResolution("invalid", out _, out _), "Invalid resolution must not reach Screen.SetResolution.");
        Require(!SettingsManager.TryParseResolution("0x1080", out _, out _), "Non-positive resolution must not reach Screen.SetResolution.");
        Require(Mathf.Approximately(VNDialogueController.GetAutoForwardDelaySeconds(250f), 2.5f), "Auto-forward delay must remain a runtime delay.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
