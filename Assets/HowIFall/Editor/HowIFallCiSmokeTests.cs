using System;
using UnityEditor;
using UnityEngine;

public static class HowIFallCiSmokeTests
{
    public static void RunAll()
    {
        try
        {
            Debug.Log("[CI] How I Fall smoke tests started.");

            Run("Dialogue backlog", DialogueBacklogSmokeTests.RunBatchMode);
            Run("Replay unlock registry", ReplayUnlockRegistrySmokeTests.RunBatchMode);
            Run("Gallery replay isolation", GalleryReplaySmokeTests.RunBatchMode);
            Run("Backlog restoration", BacklogRestorationSmokeTests.RunBatchMode);
            Run("Auto dialogue", AutoDialogueSmokeTests.RunBatchMode);
            Run("Skip dialogue", SkipDialogueSmokeTests.RunBatchMode);
            Run("Special mode coordinator", SpecialModeCoordinatorSmokeTests.RunBatchMode);
            Run("Timed narrative beat", TimedNarrativeBeatSmokeTests.RunBatchMode);
            Run("VN settings presenter", VNSettingsPresenterSmokeTests.RunBatchMode);
            Run("Settings truth", SettingsTruthSmokeTests.RunBatchMode);
            Run("Audio ambience", AudioAmbienceSmokeTests.RunBatchMode);
            Run("VN quick menu", VNQuickMenuSmokeTests.RunBatchMode);
            Run("Character Hub", CharacterHubSmokeTests.RunBatchMode);
            Run("VN input map", VNInputMapSmokeTests.RunBatchMode);
            Run("Hide UI", HideUiSmokeTests.RunBatchMode);
            Run("Relationship feedback", RelationshipFeedbackSmokeTests.RunBatchMode);
            Run("Conditional choices", ConditionalChoicesSmokeTests.RunBatchMode);
            Run("Save backend v3", ManualSaveSystemV1SmokeTests.RunBatchMode);
            Run("Project validator", () => RunWithoutLoggedErrors(HowIFallProjectValidator.ValidateProject));
            Run("Scene validation", ManualSaveSystemSceneInstaller.ValidateInstalledScenes);

            Debug.Log("[CI] How I Fall smoke tests passed.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Debug.LogError("[CI] How I Fall smoke tests failed.");
            EditorApplication.Exit(1);
        }
    }

    private static void Run(string name, Action test)
    {
        Debug.Log($"[CI] Running: {name}");
        test();
        Debug.Log($"[CI] Passed: {name}");
    }

    private static void RunWithoutLoggedErrors(Action action)
    {
        string loggedErrors = string.Empty;
        void CaptureError(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                loggedErrors += condition + "\n";
            }
        }

        Application.logMessageReceived += CaptureError;
        try
        {
            action();
        }
        finally
        {
            Application.logMessageReceived -= CaptureError;
        }

        if (!string.IsNullOrEmpty(loggedErrors))
        {
            throw new InvalidOperationException("Validation logged errors:\n" + loggedErrors);
        }
    }
}
