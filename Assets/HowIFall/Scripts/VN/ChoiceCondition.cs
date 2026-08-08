using System;
using System.Collections.Generic;
using UnityEngine;

public enum ChoiceStateValue
{
    Lust,
    Romance,
    Purity,
    Corruption,
    SelfControl,
    Suspicion,
    TrustMasha,
    TrustArtem,
    LeraInterest
}

public enum ChoiceComparisonOperator
{
    Equal,
    GreaterOrEqual,
    LessOrEqual
}

[Serializable]
public sealed class ChoiceCondition
{
    public ChoiceStateValue stateValue;
    public ChoiceComparisonOperator comparison;
    public int threshold;
}

/// <summary>
/// Evaluates numeric dialogue-choice conditions without changing GameState.
/// </summary>
public static class ConditionalChoiceEvaluator
{
    public static bool IsChoiceAvailable(DialogueChoice choice, GameState gameState)
    {
        if (choice == null || gameState == null)
        {
            Debug.LogWarning("[CHOICE CONDITIONS] Cannot evaluate a null choice or GameState.");
            return false;
        }

        if (choice.conditions == null || choice.conditions.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < choice.conditions.Count; i++)
        {
            ChoiceCondition condition = choice.conditions[i];
            if (condition == null)
            {
                Debug.LogWarning($"[CHOICE CONDITIONS] Choice '{choice.text}' contains a null condition at index {i}.");
                return false;
            }

            if (!gameState.TryGetChoiceStateValue(condition.stateValue, out int currentValue))
            {
                Debug.LogWarning($"[CHOICE CONDITIONS] Choice '{choice.text}' uses unsupported state value '{condition.stateValue}'.");
                return false;
            }

            bool matches;
            switch (condition.comparison)
            {
                case ChoiceComparisonOperator.Equal:
                    matches = currentValue == condition.threshold;
                    break;
                case ChoiceComparisonOperator.GreaterOrEqual:
                    matches = currentValue >= condition.threshold;
                    break;
                case ChoiceComparisonOperator.LessOrEqual:
                    matches = currentValue <= condition.threshold;
                    break;
                default:
                    Debug.LogWarning($"[CHOICE CONDITIONS] Choice '{choice.text}' uses unsupported comparison '{condition.comparison}'.");
                    return false;
            }

            if (!matches)
            {
                return false;
            }
        }

        return true;
    }

    public static List<VisibleChoice> BuildVisibleChoices(IList<DialogueChoice> sourceChoices, GameState gameState)
    {
        var visibleChoices = new List<VisibleChoice>();
        if (sourceChoices == null)
        {
            return visibleChoices;
        }

        for (int sourceChoiceIndex = 0; sourceChoiceIndex < sourceChoices.Count; sourceChoiceIndex++)
        {
            DialogueChoice choice = sourceChoices[sourceChoiceIndex];
            if (IsChoiceAvailable(choice, gameState))
            {
                visibleChoices.Add(new VisibleChoice(choice, sourceChoiceIndex));
            }
        }

        return visibleChoices;
    }

    public static bool TryGetVisibleChoice(
        IList<VisibleChoice> visibleChoices,
        int displaySlot,
        out VisibleChoice visibleChoice)
    {
        visibleChoice = null;
        if (visibleChoices == null || displaySlot < 0 || displaySlot >= visibleChoices.Count)
        {
            return false;
        }

        visibleChoice = visibleChoices[displaySlot];
        return visibleChoice != null && visibleChoice.choice != null;
    }
}

public sealed class VisibleChoice
{
    public readonly DialogueChoice choice;
    public readonly int sourceChoiceIndex;

    public VisibleChoice(DialogueChoice choice, int sourceChoiceIndex)
    {
        this.choice = choice;
        this.sourceChoiceIndex = sourceChoiceIndex;
    }
}
