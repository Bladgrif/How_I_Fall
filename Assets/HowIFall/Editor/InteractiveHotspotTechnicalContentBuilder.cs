using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Creates the isolated TECH DEMO ONLY / NOT CANON hotspot fixture and its continuation.</summary>
public static class InteractiveHotspotTechnicalContentBuilder
{
    public const string InteractiveScenePath = "Assets/HowIFall/Resources/InteractiveHotspot/TechnicalInteractiveRoom.asset";
    public const string CompletionScenePath = "Assets/HowIFall/Data/Dialogues/interactive_hotspot_complete.asset";
    private const string RegistryPath = "Assets/HowIFall/Data/Dialogues/DialogueSceneRegistry.asset";

    public static void Build()
    {
        EnsureFolder("Assets/HowIFall", "Resources");
        EnsureFolder("Assets/HowIFall/Resources", "InteractiveHotspot");
        DialogueSceneData completion = GetOrCreateCompletionScene();
        InteractiveSceneData room = AssetDatabase.LoadAssetAtPath<InteractiveSceneData>(InteractiveScenePath);
        if (room == null) { room = ScriptableObject.CreateInstance<InteractiveSceneData>(); AssetDatabase.CreateAsset(room, InteractiveScenePath); }
        room.sceneId = "interactive_room";
        room.displayName = "Interactive Room — TECH DEMO ONLY / NOT CANON";
        room.background = null;
        room.completionNextScene = completion;
        room.hotspots = new List<InteractiveHotspotData>
        {
            CreateLaptop(),
            CreateDoor(),
            CreateWindow()
        };
        EditorUtility.SetDirty(room);
        Register(completion);
        AssetDatabase.SaveAssets();
    }

    private static InteractiveHotspotData CreateLaptop()
    {
        return new InteractiveHotspotData
        {
            hotspotId = "test_laptop",
            displayName = "TEST:Laptop",
            normalizedRect = new Rect(0.12f, 0.28f, 0.22f, 0.24f),
            availabilityConditions = new List<ChoiceCondition>(),
            requiredCompletedHotspotIds = new List<string>(),
            oneShot = true,
            outcome = new InteractiveHotspotOutcome
            {
                feedbackText = "TEST:computer_checked = true",
                stateChanges = new List<InteractiveStateChange>()
            }
        };
    }

    private static InteractiveHotspotData CreateDoor()
    {
        return new InteractiveHotspotData
        {
            hotspotId = "test_door",
            displayName = "TEST:Door",
            normalizedRect = new Rect(0.67f, 0.18f, 0.17f, 0.56f),
            availabilityConditions = new List<ChoiceCondition>(),
            requiredCompletedHotspotIds = new List<string> { "test_laptop" },
            oneShot = false,
            outcome = new InteractiveHotspotOutcome
            {
                feedbackText = "TEST: Door exit",
                completeScene = true,
                stateChanges = new List<InteractiveStateChange>()
            }
        };
    }

    private static InteractiveHotspotData CreateWindow()
    {
        return new InteractiveHotspotData
        {
            hotspotId = "test_window",
            displayName = "TEST:Window",
            normalizedRect = new Rect(0.40f, 0.54f, 0.20f, 0.26f),
            availabilityConditions = new List<ChoiceCondition>(),
            requiredCompletedHotspotIds = new List<string>(),
            oneShot = true,
            outcome = new InteractiveHotspotOutcome
            {
                feedbackText = "TEST:window_checked = true",
                stateChanges = new List<InteractiveStateChange>()
            }
        };
    }

    private static DialogueSceneData GetOrCreateCompletionScene()
    {
        DialogueSceneData scene = AssetDatabase.LoadAssetAtPath<DialogueSceneData>(CompletionScenePath);
        if (scene == null) { scene = ScriptableObject.CreateInstance<DialogueSceneData>(); AssetDatabase.CreateAsset(scene, CompletionScenePath); }
        scene.sceneId = "interactive_hotspot_complete";
        scene.displayName = "TECH DEMO ONLY / NOT CANON";
        scene.backgroundMusic = null;
        scene.stopMusicOnStart = false;
        scene.defaultNextScene = null;
        scene.choices = new List<DialogueChoice>();
        scene.lines = new List<DialogueLine> { new DialogueLine { lineId = "interactive_hotspot_complete_line", speaker = string.Empty, text = "TEST: Interactive Room complete." } };
        EditorUtility.SetDirty(scene);
        return scene;
    }

    private static void Register(DialogueSceneData scene)
    {
        DialogueSceneRegistry registry = AssetDatabase.LoadAssetAtPath<DialogueSceneRegistry>(RegistryPath);
        if (registry == null || scene == null) throw new System.InvalidOperationException("Interactive hotspot fixture requires DialogueSceneRegistry.");
        if (!registry.scenes.Contains(scene)) { registry.scenes.Add(scene); EditorUtility.SetDirty(registry); }
    }

    private static void EnsureFolder(string parent, string child) { if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child); }
}
