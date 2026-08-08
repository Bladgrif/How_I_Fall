using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Builds isolated, non-canonical content used only by the timed-beat technical demo.</summary>
public static class TimedNarrativeBeatDemoContentBuilder
{
    public const string StartScenePath = "Assets/HowIFall/Data/Dialogues/timed_demo_start.asset";
    public const string SuccessScenePath = "Assets/HowIFall/Data/Dialogues/timed_demo_success.asset";
    public const string TimeoutScenePath = "Assets/HowIFall/Data/Dialogues/timed_demo_timeout.asset";
    private const string RegistryPath = "Assets/HowIFall/Data/Dialogues/DialogueSceneRegistry.asset";

    public static void Build()
    {
        DialogueSceneData start = CreateOrUpdate(StartScenePath, "timed_demo_start", "TEST: timed beat");
        DialogueSceneData success = CreateOrUpdate(SuccessScenePath, "timed_demo_success", "TEST: success");
        DialogueSceneData timeout = CreateOrUpdate(TimeoutScenePath, "timed_demo_timeout", "TEST: timeout");
        Register(start);
        Register(success);
        Register(timeout);
    }

    private static DialogueSceneData CreateOrUpdate(string path, string sceneId, string text)
    {
        DialogueSceneData scene = AssetDatabase.LoadAssetAtPath<DialogueSceneData>(path);
        if (scene == null)
        {
            scene = ScriptableObject.CreateInstance<DialogueSceneData>();
            AssetDatabase.CreateAsset(scene, path);
        }

        scene.sceneId = sceneId;
        scene.displayName = "TECH DEMO ONLY - NOT CANON";
        scene.backgroundMusic = null;
        scene.stopMusicOnStart = false;
        scene.defaultNextScene = null;
        scene.choices = new List<DialogueChoice>();
        scene.lines = new List<DialogueLine>
        {
            new DialogueLine { lineId = sceneId + "_line", speaker = string.Empty, text = text }
        };
        EditorUtility.SetDirty(scene);
        return scene;
    }

    private static void Register(DialogueSceneData scene)
    {
        DialogueSceneRegistry registry = AssetDatabase.LoadAssetAtPath<DialogueSceneRegistry>(RegistryPath);
        if (registry == null || scene == null)
        {
            throw new System.InvalidOperationException("Timed narrative beat demo requires DialogueSceneRegistry.");
        }

        if (!registry.scenes.Contains(scene))
        {
            registry.scenes.Add(scene);
            EditorUtility.SetDirty(registry);
        }
    }
}
