using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class PlayerJourneyE2ELauncher
{
    [MenuItem("How I Fall/Tests/Run Player Journey E2E")]
    public static void Run()
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        var filter = new Filter
        {
            testMode = TestMode.PlayMode,
            assemblyNames = new[] { "HowIFall.PlayModeTests" },
            categoryNames = new[] { "PlayerJourneyE2E" }
        };

        api.Execute(new ExecutionSettings(filter));
        Debug.Log("[PLAYER JOURNEY E2E] PlayMode suite scheduled in Unity Test Runner.");
    }
}
