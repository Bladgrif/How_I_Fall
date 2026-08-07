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
        Require(string.IsNullOrEmpty(RelationshipFeedback.Build(new DialogueChoice())), "Zero relationship deltas must not create feedback.");
        Require(RelationshipFeedback.Build(null) == string.Empty, "A missing choice must not create feedback.");

        RequireFeedback(new DialogueChoice { trustMashaDelta = 1 }, "\u041c\u0430\u0448\u0430 \u2014 \u043e\u0442\u043d\u043e\u0448\u0435\u043d\u0438\u044f \u0443\u043b\u0443\u0447\u0448\u0438\u043b\u0438\u0441\u044c");
        RequireFeedback(new DialogueChoice { trustMashaDelta = -1 }, "\u041c\u0430\u0448\u0430 \u2014 \u043e\u0442\u043d\u043e\u0448\u0435\u043d\u0438\u044f \u0443\u0445\u0443\u0434\u0448\u0438\u043b\u0438\u0441\u044c");
        RequireFeedback(new DialogueChoice { trustArtemDelta = 1 }, "\u0410\u0440\u0442\u0451\u043c \u2014 \u043e\u0442\u043d\u043e\u0448\u0435\u043d\u0438\u044f \u0443\u043b\u0443\u0447\u0448\u0438\u043b\u0438\u0441\u044c");
        RequireFeedback(new DialogueChoice { trustArtemDelta = -1 }, "\u0410\u0440\u0442\u0451\u043c \u2014 \u043e\u0442\u043d\u043e\u0448\u0435\u043d\u0438\u044f \u0443\u0445\u0443\u0434\u0448\u0438\u043b\u0438\u0441\u044c");
        RequireFeedback(new DialogueChoice { leraInterestDelta = 1 }, "\u041b\u0435\u0440\u0430 \u2014 \u043e\u0442\u043d\u043e\u0448\u0435\u043d\u0438\u044f \u0443\u043b\u0443\u0447\u0448\u0438\u043b\u0438\u0441\u044c");
        RequireFeedback(new DialogueChoice { leraInterestDelta = -1 }, "\u041b\u0435\u0440\u0430 \u2014 \u043e\u0442\u043d\u043e\u0448\u0435\u043d\u0438\u044f \u0443\u0445\u0443\u0434\u0448\u0438\u043b\u0438\u0441\u044c");

        string combined = RelationshipFeedback.Build(new DialogueChoice
        {
            trustMashaDelta = 1,
            trustArtemDelta = -1,
            leraInterestDelta = 1
        });
        Require(combined == "\u041c\u0430\u0448\u0430 \u2014 \u043e\u0442\u043d\u043e\u0448\u0435\u043d\u0438\u044f \u0443\u043b\u0443\u0447\u0448\u0438\u043b\u0438\u0441\u044c\n\u0410\u0440\u0442\u0451\u043c \u2014 \u043e\u0442\u043d\u043e\u0448\u0435\u043d\u0438\u044f \u0443\u0445\u0443\u0434\u0448\u0438\u043b\u0438\u0441\u044c\n\u041b\u0435\u0440\u0430 \u2014 \u043e\u0442\u043d\u043e\u0448\u0435\u043d\u0438\u044f \u0443\u043b\u0443\u0447\u0448\u0438\u043b\u0438\u0441\u044c", "Combined feedback must use Masha, Artem, Lera order.");
        Require(!ContainsDigit(combined), "Relationship feedback must not reveal delta values.");

        Require(string.IsNullOrEmpty(RelationshipFeedback.Build(new DialogueChoice
        {
            lustDelta = 1,
            romanceDelta = -1,
            purityDelta = 1,
            corruptionDelta = 1,
            selfControlDelta = -1,
            suspicionDelta = 1
        })), "Unrelated stat deltas must not create relationship feedback.");

        TestApplyChoiceMatchesFeedback();
    }

    private static void TestApplyChoiceMatchesFeedback()
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
            Require(gameState.trustMasha == 2 && gameState.trustArtem == -3 && gameState.leraInterest == 4, "ApplyChoice must apply relationship deltas before feedback is shown.");
            Require(!string.IsNullOrEmpty(RelationshipFeedback.Build(choice)), "Applied relationship changes must have matching feedback.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    private static void RequireFeedback(DialogueChoice choice, string expected)
    {
        Require(RelationshipFeedback.Build(choice) == expected, "Relationship feedback text is incorrect.");
    }

    private static bool ContainsDigit(string value)
    {
        foreach (char character in value)
        {
            if (char.IsDigit(character))
            {
                return true;
            }
        }

        return false;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
