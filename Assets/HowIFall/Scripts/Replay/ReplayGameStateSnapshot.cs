public sealed class ReplayGameStateSnapshot
{
    public int lust;
    public int romance;
    public int purity;
    public int corruptionLevel;
    public int selfControl;
    public int suspicion;
    public int trustMasha;
    public int trustArtem;
    public int leraInterest;
    public string currentSceneId;
    public int currentLineIndex;
    public string currentLineId;
    public int selectedChoiceIndex;
    public bool choiceResultActive;
    public string pendingNextSceneId;

    public static ReplayGameStateSnapshot Capture(GameState state)
    {
        if (state == null)
        {
            return null;
        }

        return new ReplayGameStateSnapshot
        {
            lust = state.lust,
            romance = state.romance,
            purity = state.purity,
            corruptionLevel = state.corruptionLevel,
            selfControl = state.selfControl,
            suspicion = state.suspicion,
            trustMasha = state.trustMasha,
            trustArtem = state.trustArtem,
            leraInterest = state.leraInterest,
            currentSceneId = state.currentSceneId,
            currentLineIndex = state.currentLineIndex,
            currentLineId = state.currentLineId,
            selectedChoiceIndex = state.selectedChoiceIndex,
            choiceResultActive = state.choiceResultActive,
            pendingNextSceneId = state.pendingNextSceneId
        };
    }

    public void Restore(GameState state)
    {
        if (state == null)
        {
            return;
        }

        state.lust = lust;
        state.romance = romance;
        state.purity = purity;
        state.corruptionLevel = corruptionLevel;
        state.selfControl = selfControl;
        state.suspicion = suspicion;
        state.trustMasha = trustMasha;
        state.trustArtem = trustArtem;
        state.leraInterest = leraInterest;
        state.currentSceneId = currentSceneId;
        state.currentLineIndex = currentLineIndex;
        state.currentLineId = currentLineId;
        state.selectedChoiceIndex = selectedChoiceIndex;
        state.choiceResultActive = choiceResultActive;
        state.pendingNextSceneId = pendingNextSceneId;
    }
}
