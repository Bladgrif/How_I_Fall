public enum CharacterRelationshipSource
{
    None = 0,
    TrustMasha = 1,
    TrustArtem = 2,
    LeraInterest = 3
}

/// <summary>Closed technical bridge from a profile to the current GameState.</summary>
public static class CharacterRelationshipResolver
{
    public static bool TryResolve(GameState gameState, CharacterRelationshipSource source, out int value)
    {
        value = 0;
        if (gameState == null)
        {
            return false;
        }

        switch (source)
        {
            case CharacterRelationshipSource.None:
                return false;
            case CharacterRelationshipSource.TrustMasha:
                value = gameState.trustMasha;
                return true;
            case CharacterRelationshipSource.TrustArtem:
                value = gameState.trustArtem;
                return true;
            case CharacterRelationshipSource.LeraInterest:
                value = gameState.leraInterest;
                return true;
            default:
                return false;
        }
    }
}
