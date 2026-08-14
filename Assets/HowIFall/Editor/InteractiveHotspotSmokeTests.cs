using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class InteractiveHotspotSmokeTests
{
    [MenuItem("How I Fall/Tests/Run Interactive Hotspot Smoke Tests")]
    public static void RunFromMenu() { RunBatchMode(); Debug.Log("[INTERACTIVE HOTSPOT] Smoke tests passed."); }

    public static void RunBatchMode()
    {
        GameObject canvasObject = new GameObject("InteractiveHotspotSmokeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        GameObject eventSystemObject = new GameObject("InteractiveHotspotSmokeEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        GameObject controllerObject = new GameObject("InteractiveHotspotSmokeController");
        try
        {
            VNDialogueController dialogue = controllerObject.AddComponent<VNDialogueController>();
            dialogue.dialogueUiRoot = new GameObject("InteractiveHotspotSmokeDialogueRoot");
            dialogue.dialogueUiRoot.transform.SetParent(canvasObject.transform, false);
            GameState state = GameState.EnsureInstance();
            int initialSuspicion = state.suspicion;
            int initialTrustMasha = state.trustMasha;
            InteractiveSceneData scene = CreateScene();
            Require(InteractiveSceneController.TryCreateRuntime(dialogue, out InteractiveSceneController interactive, out string createFailure), createFailure);
            Require(interactive.TryStart(scene, out string startFailure), startFailure);
            Require(interactive.IsRuntimeUiActive && dialogue.IsDialogueShellSuppressed && !dialogue.CanAdvanceDialogue, "Interactive scene must own the normal dialogue shell and input.");
            Require(interactive.IsHotspotAvailable("laptop"), "Laptop must start available.");
            Require(!interactive.IsHotspotAvailable("door"), "Door must start unavailable.");
            Click(interactive.GetHotspotButton("laptop"));
            Require(interactive.IsHotspotCompleted("laptop") && state.suspicion == initialSuspicion, "Laptop completion must remain local to the interactive scene.");
            Require(interactive.IsHotspotAvailable("door"), "Door must refresh after local Laptop completion.");
            Click(interactive.GetHotspotButton("window"));
            int activationCount = interactive.ActivationCount;
            Require(interactive.IsHotspotCompleted("window") && state.trustMasha == initialTrustMasha, "Window completion must not modify canonical GameState.");
            ExecuteEvents.Execute<IPointerClickHandler>(interactive.GetHotspotButton("window").gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            Require(interactive.ActivationCount == activationCount, "Disabled one-shot Window must not run twice.");
            Require(SaveData.CurrentVersion == 3, "Interactive hotspots must preserve SaveData v3.");
            Debug.Log("[INTERACTIVE HOTSPOT] Smoke tests passed.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(controllerObject);
            UnityEngine.Object.DestroyImmediate(eventSystemObject);
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }
    }

    private static InteractiveSceneData CreateScene()
    {
        InteractiveSceneData scene = ScriptableObject.CreateInstance<InteractiveSceneData>();
        scene.sceneId = "smoke_room";
        scene.displayName = "TECH DEMO ONLY / NOT CANON";
        scene.hotspots = new List<InteractiveHotspotData>
        {
            Hotspot("laptop", true),
            Hotspot("door", false, "laptop"),
            Hotspot("window", true)
        };
        return scene;
    }

    private static InteractiveHotspotData Hotspot(string id, bool oneShot, params string[] required)
    {
        return new InteractiveHotspotData
        {
            hotspotId = id,
            displayName = id,
            normalizedRect = new Rect(.1f, .1f, .2f, .2f),
            availabilityConditions = new List<ChoiceCondition>(),
            requiredCompletedHotspotIds = new List<string>(required),
            oneShot = oneShot,
            outcome = new InteractiveHotspotOutcome { stateChanges = new List<InteractiveStateChange>() }
        };
    }

    private static void Click(Button button)
    {
        Require(button != null && button.interactable, "Expected an interactable hotspot button.");
        ExecuteEvents.Execute<IPointerClickHandler>(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
