using System;
using UnityEditor;
using UnityEngine;

public static class RelationshipFeedbackSmokeTests
{
    [MenuItem("How I Fall/Tests/Run Relationship Feedback Smoke Tests")]
    public static void RunFromMenu()
    {
        RunBatchMode();
        Debug.Log("How I Fall relationship feedback smoke tests passed.");
    }

    public static void RunBatchMode()
    {
        Require(RelationshipFeedback.GetCueKind(null) == RelationshipCueKind.None, "A missing choice must not create a cue.");
        Require(RelationshipFeedback.GetCueKind(new DialogueChoice()) == RelationshipCueKind.None, "Zero relationship deltas must not create a cue.");
        Require(RelationshipFeedback.GetCueKind(new DialogueChoice { trustMashaDelta = 1 }) == RelationshipCueKind.Positive, "A positive relationship delta must use the positive cue.");
        Require(RelationshipFeedback.GetCueKind(new DialogueChoice { trustArtemDelta = -1 }) == RelationshipCueKind.Negative, "A negative relationship delta must use the negative cue.");
        Require(RelationshipFeedback.GetCueKind(new DialogueChoice { leraInterestDelta = 1, trustArtemDelta = -1 }) == RelationshipCueKind.Mixed, "Mixed relationship changes must remain distinguishable without text.");
        Require(RelationshipFeedback.GetCueKind(new DialogueChoice { romanceDelta = 1, suspicionDelta = -1 }) == RelationshipCueKind.None, "Unrelated stat deltas must not create a relationship cue.");
        TestApplyChoiceMatchesCue();
    }

    private static void TestApplyChoiceMatchesCue()
    {
        GameObject gameObject = new GameObject("RelationshipFeedbackSmokeGameState");
        DialogueChoice choice = new DialogueChoice
        {
            trustMashaDelta = 2,
            trustArtemDelta = -3,
            leraInterestDelta = 4
        };

        try
        {
            GameState gameState = gameObject.AddComponent<GameState>();
            gameState.ApplyChoice(choice);
            Require(gameState.trustMasha == 2 && gameState.trustArtem == -3 && gameState.leraInterest == 4, "ApplyChoice must apply relationship deltas before the cue is shown.");
            Require(RelationshipFeedback.GetCueKind(choice) == RelationshipCueKind.Mixed, "Applied relationship changes must have matching cue semantics.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
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
