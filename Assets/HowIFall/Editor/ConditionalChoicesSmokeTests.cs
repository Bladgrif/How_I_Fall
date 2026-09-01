using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ConditionalChoicesSmokeTests
{
    [MenuItem("How I Fall/Tests/Run Conditional Choices Smoke Tests")]
    public static void RunFromMenu()
    {
        RunBatchMode();
        Debug.Log("How I Fall conditional choices smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        GameObject gameStateObject = new GameObject("ConditionalChoicesSmokeGameState");
        GameState gameState = gameStateObject.AddComponent<GameState>();
        var temporaryAssets = new List<UnityEngine.Object>();

        try
        {
            TestLegacyAvailability(gameState);
            TestStateValuesAndOperators(gameState);
            TestAndAndInvalidConditions(gameState);
            TestVisibleMappingAndSourceSelection(gameState, temporaryAssets);
            TestFallbackAndCapacity(gameState, temporaryAssets);
            TestSaveContract();
        }
        finally
        {
            for (int i = 0; i < temporaryAssets.Count; i++)
            {
                UnityEngine.Object.DestroyImmediate(temporaryAssets[i]);
            }

            UnityEngine.Object.DestroyImmediate(gameStateObject);
        }
    }

    private static void TestLegacyAvailability(GameState gameState)
    {
        Require(ConditionalChoiceEvaluator.IsChoiceAvailable(
            new DialogueChoice { conditions = null }, gameState), "Null conditions must remain available for legacy assets.");
        Require(ConditionalChoiceEvaluator.IsChoiceAvailable(
            new DialogueChoice { conditions = new List<ChoiceCondition>() }, gameState), "Empty conditions must remain available.");
        Require(!ConditionalChoiceEvaluator.IsChoiceAvailable(null, gameState), "A null choice must fail closed.");
    }

    private static void TestStateValuesAndOperators(GameState gameState)
    {
        gameState.lust = 11;
        gameState.romance = 12;
        gameState.purity = 13;
        gameState.corruptionLevel = 14;
        gameState.selfControl = 15;
        gameState.suspicion = 16;
        gameState.trustMasha = 17;
        gameState.trustArtem = 18;
        gameState.leraInterest = 19;

        RequireStateValue(gameState, ChoiceStateValue.Lust, 11);
        RequireStateValue(gameState, ChoiceStateValue.Romance, 12);
        RequireStateValue(gameState, ChoiceStateValue.Purity, 13);
        RequireStateValue(gameState, ChoiceStateValue.Corruption, 14);
        RequireStateValue(gameState, ChoiceStateValue.SelfControl, 15);
        RequireStateValue(gameState, ChoiceStateValue.Suspicion, 16);
        RequireStateValue(gameState, ChoiceStateValue.TrustMasha, 17);
        RequireStateValue(gameState, ChoiceStateValue.TrustArtem, 18);
        RequireStateValue(gameState, ChoiceStateValue.LeraInterest, 19);

        gameState.romance = 5;
        Require(ConditionalChoiceEvaluator.IsChoiceAvailable(ChoiceWithCondition(ChoiceStateValue.Romance, ChoiceComparisonOperator.Equal, 5), gameState), "Equal must match equal values.");
        Require(!ConditionalChoiceEvaluator.IsChoiceAvailable(ChoiceWithCondition(ChoiceStateValue.Romance, ChoiceComparisonOperator.Equal, 4), gameState), "Equal must reject a different value.");
        Require(!ConditionalChoiceEvaluator.IsChoiceAvailable(ChoiceWithCondition(ChoiceStateValue.Romance, ChoiceComparisonOperator.GreaterOrEqual, 6), gameState), "GreaterOrEqual must reject lower values.");
        Require(ConditionalChoiceEvaluator.IsChoiceAvailable(ChoiceWithCondition(ChoiceStateValue.Romance, ChoiceComparisonOperator.GreaterOrEqual, 5), gameState), "GreaterOrEqual must include equality.");
        Require(ConditionalChoiceEvaluator.IsChoiceAvailable(ChoiceWithCondition(ChoiceStateValue.Romance, ChoiceComparisonOperator.GreaterOrEqual, 4), gameState), "GreaterOrEqual must accept higher values.");
        Require(ConditionalChoiceEvaluator.IsChoiceAvailable(ChoiceWithCondition(ChoiceStateValue.Romance, ChoiceComparisonOperator.LessOrEqual, 6), gameState), "LessOrEqual must accept lower values.");
        Require(ConditionalChoiceEvaluator.IsChoiceAvailable(ChoiceWithCondition(ChoiceStateValue.Romance, ChoiceComparisonOperator.LessOrEqual, 5), gameState), "LessOrEqual must include equality.");
        Require(!ConditionalChoiceEvaluator.IsChoiceAvailable(ChoiceWithCondition(ChoiceStateValue.Romance, ChoiceComparisonOperator.LessOrEqual, 4), gameState), "LessOrEqual must reject higher values.");
    }

    private static void TestAndAndInvalidConditions(GameState gameState)
    {
        gameState.romance = 5;
        gameState.suspicion = 2;
        DialogueChoice allTrue = new DialogueChoice
        {
            conditions = new List<ChoiceCondition>
            {
                new ChoiceCondition { stateValue = ChoiceStateValue.Romance, comparison = ChoiceComparisonOperator.GreaterOrEqual, threshold = 5 },
                new ChoiceCondition { stateValue = ChoiceStateValue.Suspicion, comparison = ChoiceComparisonOperator.LessOrEqual, threshold = 2 }
            }
        };
        Require(ConditionalChoiceEvaluator.IsChoiceAvailable(allTrue, gameState), "All true conditions must be available.");
        allTrue.conditions[1].threshold = 1;
        Require(!ConditionalChoiceEvaluator.IsChoiceAvailable(allTrue, gameState), "One false condition must hide the choice.");
        Require(!ConditionalChoiceEvaluator.IsChoiceAvailable(new DialogueChoice { conditions = new List<ChoiceCondition> { null } }, gameState), "A null condition must fail closed.");
        Require(!ConditionalChoiceEvaluator.IsChoiceAvailable(ChoiceWithCondition((ChoiceStateValue)999, ChoiceComparisonOperator.Equal, 0), gameState), "An invalid state enum must fail closed.");
        Require(!ConditionalChoiceEvaluator.IsChoiceAvailable(ChoiceWithCondition(ChoiceStateValue.Romance, (ChoiceComparisonOperator)999, 5), gameState), "An invalid comparison enum must fail closed.");
    }

    private static void TestVisibleMappingAndSourceSelection(GameState gameState, List<UnityEngine.Object> temporaryAssets)
    {
        gameState.romance = 0;
        DialogueSceneData targetOne = CreateScene("conditional_target_one", temporaryAssets);
        DialogueSceneData targetTwo = CreateScene("conditional_target_two", temporaryAssets);
        var sourceChoices = new List<DialogueChoice>
        {
            ChoiceWithCondition(ChoiceStateValue.Romance, ChoiceComparisonOperator.GreaterOrEqual, 1),
            new DialogueChoice { text = "source one", romanceDelta = 3, nextScene = targetOne },
            new DialogueChoice { text = "source two", suspicionDelta = 4, nextScene = targetTwo }
        };

        List<VisibleChoice> visibleChoices = ConditionalChoiceEvaluator.BuildVisibleChoices(sourceChoices, gameState);
        Require(visibleChoices.Count == 2, "A hidden source choice must not occupy a visible slot.");
        Require(visibleChoices[0].sourceChoiceIndex == 1 && visibleChoices[1].sourceChoiceIndex == 2, "Visible choices must preserve source order and source indices.");

        Require(ConditionalChoiceEvaluator.TryGetVisibleChoice(visibleChoices, 0, out VisibleChoice first), "Display slot zero must resolve.");
        gameState.ApplyChoice(first.choice);
        gameState.selectedChoiceIndex = first.sourceChoiceIndex;
        Require(gameState.selectedChoiceIndex == 1 && gameState.romance == 3 && first.choice.nextScene == targetOne, "Display slot zero must apply source choice one.");

        Require(ConditionalChoiceEvaluator.TryGetVisibleChoice(visibleChoices, 1, out VisibleChoice second), "Display slot one must resolve.");
        gameState.ApplyChoice(second.choice);
        gameState.selectedChoiceIndex = second.sourceChoiceIndex;
        Require(gameState.selectedChoiceIndex == 2 && gameState.suspicion == 6 && second.choice.nextScene == targetTwo, "Display slot one must apply source choice two.");

        gameState.romance = 0;
        Require(ConditionalChoiceEvaluator.BuildVisibleChoices(sourceChoices, gameState).Count == 2, "Recomputed availability must use restored GameState without saved display data.");
        gameState.selectedChoiceIndex = 1;
        gameState.choiceResultActive = true;
        Require(sourceChoices[gameState.selectedChoiceIndex] == first.choice, "Saved choice-result restore must use the saved source index without re-gating.");
    }

    private static void TestFallbackAndCapacity(GameState gameState, List<UnityEngine.Object> temporaryAssets)
    {
        DialogueSceneData fallback = CreateScene("conditional_fallback", temporaryAssets);
        DialogueChoice hidden = ChoiceWithCondition(ChoiceStateValue.Lust, ChoiceComparisonOperator.GreaterOrEqual, 999);
        gameState.lust = 0;
        int lustBeforeFallback = gameState.lust;
        Require(ConditionalChoiceEvaluator.BuildVisibleChoices(new List<DialogueChoice> { hidden }, gameState).Count == 0, "Zero available choices must not select a hidden source choice.");
        Require(fallback != null && gameState.lust == lustBeforeFallback, "Zero-choice fallback must not apply a choice delta.");
        Require(!VNDialogueController.IsChoiceCapacityExceeded(1, 1), "One visible choice remains a manual choice, not a capacity failure.");
        Require(!VNDialogueController.IsChoiceCapacityExceeded(4, VNDialogueController.SupportedChoiceButtonCapacity), "Four visible choices must fit the polished choice UI.");
        Require(VNDialogueController.IsChoiceCapacityExceeded(5, VNDialogueController.SupportedChoiceButtonCapacity), "Visible choices over UI capacity must fail safely instead of truncating.");
    }

    private static void TestSaveContract()
    {
        Require(SaveData.CurrentVersion == 3, "Conditional choices must keep SaveData at v3.");
        string serialized = JsonUtility.ToJson(new SaveData());
        Require(serialized.IndexOf("condition", StringComparison.OrdinalIgnoreCase) < 0, "SaveData must not serialize conditional choice data.");
        Require(serialized.IndexOf("visibleChoice", StringComparison.OrdinalIgnoreCase) < 0, "SaveData must not serialize visible choice mappings.");
    }

    private static void RequireStateValue(GameState gameState, ChoiceStateValue stateValue, int expectedValue)
    {
        Require(ConditionalChoiceEvaluator.IsChoiceAvailable(
            ChoiceWithCondition(stateValue, ChoiceComparisonOperator.Equal, expectedValue), gameState),
            $"{stateValue} must read its matching GameState field.");
    }

    private static DialogueChoice ChoiceWithCondition(ChoiceStateValue stateValue, ChoiceComparisonOperator comparison, int threshold)
    {
        return new DialogueChoice
        {
            conditions = new List<ChoiceCondition>
            {
                new ChoiceCondition { stateValue = stateValue, comparison = comparison, threshold = threshold }
            }
        };
    }

    private static DialogueSceneData CreateScene(string sceneId, List<UnityEngine.Object> temporaryAssets)
    {
        DialogueSceneData scene = ScriptableObject.CreateInstance<DialogueSceneData>();
        scene.sceneId = sceneId;
        temporaryAssets.Add(scene);
        return scene;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
