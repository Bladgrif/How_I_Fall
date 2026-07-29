using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class SaveSystemSmokeTests
{
    [MenuItem("How I Fall/Tests/Run Save System Smoke Tests")]
    public static void RunFromMenu()
    {
        Run();
        Debug.Log("How I Fall save system smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        Run();
        Debug.Log("How I Fall save system smoke tests passed.");
    }

    private static void Run()
    {
        TestLegacyMigration();
        TestFutureVersionRejection();
        TestAtomicWriteAndBackupRecovery();
    }

    private static void TestLegacyMigration()
    {
        var legacy = new SaveData
        {
            version = 0,
            currentLineIndex = -5,
            currentSceneId = null
        };

        Require(legacy.TryMigrateToCurrentVersion(out string error), error);
        Require(legacy.version == SaveData.CurrentVersion, "Legacy save was not upgraded.");
        Require(legacy.currentLineIndex == 0, "Negative line index was not normalized.");
        Require(legacy.currentSceneId == string.Empty, "Null scene ID was not normalized.");
    }

    private static void TestFutureVersionRejection()
    {
        var future = new SaveData { version = SaveData.CurrentVersion + 1 };
        Require(!future.TryMigrateToCurrentVersion(out string error), "A future save version was accepted.");
        Require(!string.IsNullOrEmpty(error), "Future-version rejection did not provide a diagnostic.");
    }

    private static void TestAtomicWriteAndBackupRecovery()
    {
        string directory = Path.Combine(Path.GetTempPath(), "HowIFallSaveTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "save.json");

        try
        {
            var first = new SaveData { currentSceneId = "first", currentLineIndex = 3 };
            var second = new SaveData { currentSceneId = "second", currentLineIndex = 7 };

            Require(InvokeWrite(path, first), "Initial save write failed.");
            Require(InvokeWrite(path, second), "Replacement save write failed.");
            Require(File.Exists(path + ".bak"), "Replacement did not create a backup.");

            SaveData current = InvokeRead(path);
            Require(current != null && current.currentSceneId == "second", "Current save content is incorrect.");

            File.WriteAllText(path, "not valid json");
            SaveData recovered = InvokeRead(path);
            Require(recovered != null && recovered.currentSceneId == "first", "Backup recovery failed.");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private static bool InvokeWrite(string path, SaveData data)
    {
        MethodInfo method = GetSaveManagerMethod("WriteSaveData");
        return (bool)method.Invoke(null, new object[] { path, data });
    }

    private static SaveData InvokeRead(string path)
    {
        MethodInfo method = GetSaveManagerMethod("ReadSaveData");
        return (SaveData)method.Invoke(null, new object[] { path });
    }

    private static MethodInfo GetSaveManagerMethod(string name)
    {
        MethodInfo method = typeof(SaveManager).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        Require(method != null, $"SaveManager.{name} was not found.");
        return method;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
