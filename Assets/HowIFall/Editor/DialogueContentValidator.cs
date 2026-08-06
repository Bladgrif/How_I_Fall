using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DialogueContentValidator
{
    private const string RegistryPath = "Assets/HowIFall/Data/Dialogues/DialogueSceneRegistry.asset";

    public static int Validate()
    {
        DialogueSceneRegistry registry = AssetDatabase.LoadAssetAtPath<DialogueSceneRegistry>(RegistryPath);
        if (registry == null)
        {
            return LogError($"Dialogue content: registry is missing at '{RegistryPath}'.");
        }

        if (registry.scenes == null || registry.scenes.Count == 0)
        {
            return LogError("Dialogue content: registry does not contain a starting scene.");
        }

        int issues = 0;
        var registeredScenes = new HashSet<DialogueSceneData>();
        var scenesById = new Dictionary<string, DialogueSceneData>(StringComparer.Ordinal);

        for (int i = 0; i < registry.scenes.Count; i++)
        {
            DialogueSceneData scene = registry.scenes[i];
            string location = $"Dialogue registry entry {i}";

            if (scene == null)
            {
                issues += LogError($"{location}: scene reference is missing.");
                continue;
            }

            if (!registeredScenes.Add(scene))
            {
                issues += LogError($"{location}: scene '{scene.name}' is registered more than once.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(scene.sceneId))
            {
                issues += LogError($"Dialogue scene '{scene.name}': sceneId is empty.");
            }
            else if (scenesById.TryGetValue(scene.sceneId, out DialogueSceneData duplicate))
            {
                issues += LogError(
                    $"Dialogue scenes '{duplicate.name}' and '{scene.name}' use the same sceneId '{scene.sceneId}'.");
            }
            else
            {
                scenesById.Add(scene.sceneId, scene);
            }

            issues += ValidateScene(scene);
        }

        foreach (DialogueSceneData scene in registeredScenes)
        {
            issues += ValidateSceneReferences(scene, registeredScenes);
        }

        DialogueSceneData startingScene = registry.scenes[0];
        if (startingScene != null)
        {
            var reachableScenes = new HashSet<DialogueSceneData>();
            CollectReachableScenes(startingScene, reachableScenes);

            foreach (DialogueSceneData scene in registeredScenes)
            {
                if (!reachableScenes.Contains(scene))
                {
                    issues += LogWarning(
                        $"Dialogue scene '{scene.name}' ({scene.sceneId}) is registered but unreachable from starting scene '{startingScene.name}'.");
                }
            }
        }

        return issues;
    }

    private static int ValidateScene(DialogueSceneData scene)
    {
        int issues = 0;

        if (scene.lines == null || scene.lines.Count == 0)
        {
            issues += LogError($"Dialogue scene '{scene.name}': no dialogue lines are defined.");
        }
        else
        {
            var lineIds = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < scene.lines.Count; i++)
            {
                DialogueLine line = scene.lines[i];
                if (line == null)
                {
                    issues += LogError($"Dialogue scene '{scene.name}', line {i}: line data is missing.");
                }
                else if (string.IsNullOrWhiteSpace(line.text))
                {
                    issues += LogError($"Dialogue scene '{scene.name}', line {i}: text is empty.");
                }

                if (line != null && string.IsNullOrWhiteSpace(line.lineId))
                {
                    issues += LogError($"Dialogue scene '{scene.name}', line {i}: lineId is empty.");
                }
                else if (line != null && !lineIds.Add(line.lineId))
                {
                    issues += LogError(
                        $"Dialogue scene '{scene.name}': lineId '{line.lineId}' is used more than once.");
                }
            }
        }

        if (scene.choices == null)
        {
            return issues + LogError($"Dialogue scene '{scene.name}': choices list is missing.");
        }

        for (int i = 0; i < scene.choices.Count; i++)
        {
            DialogueChoice choice = scene.choices[i];
            if (choice == null)
            {
                issues += LogError($"Dialogue scene '{scene.name}', choice {i}: choice data is missing.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(choice.text))
            {
                issues += LogError($"Dialogue scene '{scene.name}', choice {i}: choice text is empty.");
            }

            if (string.IsNullOrWhiteSpace(choice.resultText))
            {
                issues += LogWarning($"Dialogue scene '{scene.name}', choice {i}: result text is empty.");
            }
        }

        return issues;
    }

    private static int ValidateSceneReferences(
        DialogueSceneData scene,
        HashSet<DialogueSceneData> registeredScenes)
    {
        int issues = 0;

        if (scene.defaultNextScene != null && !registeredScenes.Contains(scene.defaultNextScene))
        {
            issues += LogError(
                $"Dialogue scene '{scene.name}': default transition points to unregistered scene '{scene.defaultNextScene.name}'.");
        }

        if (scene.choices == null)
        {
            return issues;
        }

        for (int i = 0; i < scene.choices.Count; i++)
        {
            DialogueSceneData target = scene.choices[i]?.nextScene;
            if (target != null && !registeredScenes.Contains(target))
            {
                issues += LogError(
                    $"Dialogue scene '{scene.name}', choice {i}: transition points to unregistered scene '{target.name}'.");
            }
        }

        return issues;
    }

    private static void CollectReachableScenes(
        DialogueSceneData scene,
        HashSet<DialogueSceneData> reachableScenes)
    {
        if (scene == null || !reachableScenes.Add(scene))
        {
            return;
        }

        CollectReachableScenes(scene.defaultNextScene, reachableScenes);

        if (scene.choices == null)
        {
            return;
        }

        foreach (DialogueChoice choice in scene.choices)
        {
            CollectReachableScenes(choice?.nextScene, reachableScenes);
        }
    }

    private static int LogError(string message)
    {
        Debug.LogError(message);
        return 1;
    }

    private static int LogWarning(string message)
    {
        Debug.LogWarning(message);
        return 0;
    }
}
