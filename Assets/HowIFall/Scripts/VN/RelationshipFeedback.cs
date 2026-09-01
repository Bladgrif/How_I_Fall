public enum RelationshipCueKind
{
    None,
    Positive,
    Negative,
    Mixed
}

public static class RelationshipFeedback
{
    public static RelationshipCueKind GetCueKind(DialogueChoice choice)
    {
        if (choice == null)
        {
            return RelationshipCueKind.None;
        }

        bool hasPositive = choice.trustMashaDelta > 0
            || choice.trustArtemDelta > 0
            || choice.leraInterestDelta > 0;
        bool hasNegative = choice.trustMashaDelta < 0
            || choice.trustArtemDelta < 0
            || choice.leraInterestDelta < 0;

        if (hasPositive && hasNegative)
        {
            return RelationshipCueKind.Mixed;
        }

        if (hasPositive)
        {
            return RelationshipCueKind.Positive;
        }

        return hasNegative ? RelationshipCueKind.Negative : RelationshipCueKind.None;
    }
}
