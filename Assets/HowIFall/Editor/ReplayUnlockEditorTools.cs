using UnityEditor;
using UnityEngine;

public static class ReplayUnlockEditorTools
{
    public const string TestReplayId = "test_replay_v1";

    [MenuItem("How I Fall/Replay/Unlock TEST Replay")]
    public static void UnlockTestReplay()
    {
        bool changed = ReplayUnlockRegistry.Default.Unlock(TestReplayId);
        Debug.Log(changed
            ? "[REPLAY] TEST Replay unlocked in the development profile."
            : "[REPLAY] TEST Replay was already unlocked or the profile write failed.");
    }

    [MenuItem("How I Fall/Replay/Reset TEST Replay Unlock")]
    public static void ResetTestReplayUnlock()
    {
        ReplayUnlockRegistry.Default.ResetForTests();
        ReplayUnlockRegistry.ResetDefaultInstanceForTests();
        Debug.Log("[REPLAY] Development replay unlock profile reset.");
    }
}
