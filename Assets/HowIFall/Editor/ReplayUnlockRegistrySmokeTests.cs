using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ReplayUnlockRegistrySmokeTests
{
    [MenuItem("How I Fall/Tests/Run Replay Unlock Registry Smoke Tests")]
    public static void RunFromMenu()
    {
        RunBatchMode();
    }

    public static void RunBatchMode()
    {
        string root = Path.Combine(Path.GetTempPath(), "HowIFall_ReplayUnlock_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string profilePath = Path.Combine(root, ReplayUnlockRegistry.ProfileFileName);
        try
        {
            var missing = new ReplayUnlockRegistry(profilePath);
            Require(!missing.IsUnlocked("test_replay_v1"), "Missing profile did not fail closed.");
            Require(!missing.IsUnlocked(null) && !missing.Unlock(" "), "Invalid replay ID was accepted.");

            Require(missing.Unlock("test_replay_v1"), "First unlock did not change the registry.");
            string firstJson = File.ReadAllText(profilePath);
            Require(!missing.Unlock("test_replay_v1"), "Second unlock was not idempotent.");
            Require(File.ReadAllText(profilePath) == firstJson, "Idempotent unlock rewrote the profile.");

            var recreated = new ReplayUnlockRegistry(profilePath);
            Require(recreated.IsUnlocked("test_replay_v1"), "Unlock did not survive registry recreation.");
            Require(!profilePath.StartsWith(Path.Combine(root, "Saves"), StringComparison.OrdinalIgnoreCase), "Replay profile was placed under Saves.");

            File.WriteAllText(profilePath, "{ corrupt json");
            Require(!new ReplayUnlockRegistry(profilePath).IsUnlocked("test_replay_v1"), "Corrupt profile unlocked replay content.");

            File.WriteAllText(profilePath, "{\"version\":999,\"unlockedReplayIds\":[\"test_replay_v1\"]}");
            Require(!new ReplayUnlockRegistry(profilePath).IsUnlocked("test_replay_v1"), "Future profile version did not fail closed.");

            File.WriteAllText(profilePath, firstJson);
            string unrelatedSave = Path.Combine(root, "Saves", "slot_01.json");
            Directory.CreateDirectory(Path.GetDirectoryName(unrelatedSave));
            File.WriteAllText(unrelatedSave, "campaign");
            new ReplayUnlockRegistry(profilePath);
            Require(File.ReadAllText(unrelatedSave) == "campaign", "Registry recreation touched a campaign save.");
            File.Delete(unrelatedSave);
            Require(new ReplayUnlockRegistry(profilePath).IsUnlocked("test_replay_v1"), "Deleting a campaign save reset the profile unlock.");

            Debug.Log("How I Fall replay unlock registry smoke tests passed.");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
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
