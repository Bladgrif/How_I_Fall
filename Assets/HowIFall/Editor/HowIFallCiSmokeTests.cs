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
            Run("VN settings presenter", VNSettingsPresenterSmokeTests.RunBatchMode);
            Run("Save backend v2", ManualSaveSystemV1SmokeTests.RunBatchMode);

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
}
