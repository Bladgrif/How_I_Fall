using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Creates the isolated TECH DEMO ONLY / NOT CANON hotspot fixture and its continuation.</summary>
public static class InteractiveHotspotTechnicalContentBuilder
{
    public const string InteractiveScenePath = "Assets/HowIFall/Resources/InteractiveHotspot/TechnicalInteractiveRoom.asset";
    public const string ShowcaseScenePath = "Assets/HowIFall/Resources/InteractiveHotspot/HotspotShowcaseRoom.asset";
    public const string ShowcaseBackgroundPath = "Assets/HowIFall/Art/Backgrounds/HotspotShowcaseRoom.png";
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
        room.initialFeedback = "Select an available technical hotspot.";
        room.feedbackSpeaker = null;
        room.completionNextScene = completion;
        room.hotspots = new List<InteractiveHotspotData>
        {
            CreateLaptop(),
            CreateDoor(),
            CreateWindow()
        };
        EditorUtility.SetDirty(room);
        Register(completion);
        BuildShowcase(completion);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Polished TECH showcase room over the approved clean background (target 08):
    /// three real hotspots, the door unlocks only after both local prerequisites.
    /// Content stays non-canonical and touches no canonical GameState.
    /// </summary>
    public static void BuildShowcase(DialogueSceneData completion = null)
    {
        EnsureFolder("Assets/HowIFall", "Resources");
        EnsureFolder("Assets/HowIFall/Resources", "InteractiveHotspot");
        if (completion == null) completion = GetOrCreateCompletionScene();
        Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>(ShowcaseBackgroundPath);
        if (background == null) throw new System.InvalidOperationException("Hotspot showcase background is missing: " + ShowcaseBackgroundPath);
        InteractiveSceneData showcase = AssetDatabase.LoadAssetAtPath<InteractiveSceneData>(ShowcaseScenePath);
        if (showcase == null) { showcase = ScriptableObject.CreateInstance<InteractiveSceneData>(); AssetDatabase.CreateAsset(showcase, ShowcaseScenePath); }
        showcase.sceneId = "hotspot_showcase_room";
        showcase.displayName = "Hotspot Showcase — TECH DEMO ONLY / NOT CANON";
        showcase.background = background;
        showcase.initialFeedback = "Здесь всё на своих местах.\nТолько вот с чего начать?";
        showcase.feedbackSpeaker = "Алекс";
        showcase.completionNextScene = completion;
        showcase.hotspots = new List<InteractiveHotspotData>
        {
            new InteractiveHotspotData
            {
                hotspotId = "showcase_laptop",
                displayName = "Ноутбук",
                iconId = "laptop",
                normalizedRect = new Rect(0.014952f, 0.417851f, 0.147129f, 0.157601f),
                availabilityConditions = new List<ChoiceCondition>(),
                requiredCompletedHotspotIds = new List<string>(),
                oneShot = true,
                outcome = new InteractiveHotspotOutcome
                {
                    feedbackText = "Ноутбук осмотрен. Для тех-демо этого достаточно.",
                    stateChanges = new List<InteractiveStateChange>()
                }
            },
            new InteractiveHotspotData
            {
                hotspotId = "showcase_notes",
                displayName = "Записки",
                iconId = "notes",
                normalizedRect = new Rect(0.482656f, 0.543040f, 0.197249f, 0.111051f),
                availabilityConditions = new List<ChoiceCondition>(),
                requiredCompletedHotspotIds = new List<string>(),
                oneShot = true,
                outcome = new InteractiveHotspotOutcome
                {
                    feedbackText = "Записки прочитаны. Техническая проверка пройдена.",
                    stateChanges = new List<InteractiveStateChange>()
                }
            },
            new InteractiveHotspotData
            {
                hotspotId = "showcase_door",
                displayName = "Дверь",
                iconId = "door",
                normalizedRect = new Rect(0.80861f, 0.54304f, 0.16125f, 0.27014f),
                availabilityConditions = new List<ChoiceCondition>(),
                requiredCompletedHotspotIds = new List<string> { "showcase_laptop", "showcase_notes" },
                oneShot = false,
                outcome = new InteractiveHotspotOutcome
                {
                    feedbackText = "Дверь открыта. Демонстрация завершена.",
                    completeScene = true,
                    stateChanges = new List<InteractiveStateChange>()
                }
            }
        };
        EditorUtility.SetDirty(showcase);
        Register(completion);
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
