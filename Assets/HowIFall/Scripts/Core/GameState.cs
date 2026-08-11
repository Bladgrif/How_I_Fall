using UnityEngine;

public class GameState : MonoBehaviour
{
    public static GameState Instance { get; private set; }

    public int lust;
    public int romance;
    public int purity;
    public int corruptionLevel;
    public int selfControl = 5;
    public int suspicion;
    public int trustMasha;
    public int trustArtem;
    public int leraInterest;

    public string currentSceneId;
    public int currentLineIndex;
    public string currentLineId;
    public int selectedChoiceIndex = -1;
    public bool choiceResultActive;
    public string pendingNextSceneId;

    public static GameState EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        Instance = FindFirstObjectByType<GameState>();

        if (Instance != null)
        {
            return Instance;
        }

        GameObject gameStateObject = new GameObject("GameState");
        Instance = gameStateObject.AddComponent<GameState>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[GAMESTATE] Duplicate GameState ignored on '{gameObject.name}'. Existing instance: '{Instance.gameObject.name}'.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log($"[GAMESTATE] GameState ready on '{gameObject.name}'. sceneId='{currentSceneId}', lineId='{currentLineId}'.", this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool TryGetChoiceStateValue(ChoiceStateValue stateValue, out int value)
    {
        switch (stateValue)
        {
            case ChoiceStateValue.Lust:
                value = lust;
                return true;
            case ChoiceStateValue.Romance:
                value = romance;
                return true;
            case ChoiceStateValue.Purity:
                value = purity;
                return true;
            case ChoiceStateValue.Corruption:
                value = corruptionLevel;
                return true;
            case ChoiceStateValue.SelfControl:
                value = selfControl;
                return true;
            case ChoiceStateValue.Suspicion:
                value = suspicion;
                return true;
            case ChoiceStateValue.TrustMasha:
                value = trustMasha;
                return true;
            case ChoiceStateValue.TrustArtem:
                value = trustArtem;
                return true;
            case ChoiceStateValue.LeraInterest:
                value = leraInterest;
                return true;
            default:
                value = 0;
                return false;
        }
    }

    public void ApplyChoice(DialogueChoice choice)
    {
        if (choice == null)
        {
            return;
        }

        ApplyChoiceStateDelta(choice.lustDelta, choice.romanceDelta, choice.purityDelta, choice.corruptionDelta,
            choice.selfControlDelta, choice.suspicionDelta, choice.trustMashaDelta, choice.trustArtemDelta, choice.leraInterestDelta);
    }

    /// <summary>Single typed mapping for the nine numeric authored state deltas.</summary>
    public void ApplyChoiceStateDelta(int lustDelta, int romanceDelta, int purityDelta, int corruptionDelta,
        int selfControlDelta, int suspicionDelta, int trustMashaDelta, int trustArtemDelta, int leraInterestDelta)
    {
        lust += lustDelta; romance += romanceDelta; purity += purityDelta; corruptionLevel += corruptionDelta;
        selfControl += selfControlDelta; suspicion += suspicionDelta; trustMasha += trustMashaDelta;
        trustArtem += trustArtemDelta; leraInterest += leraInterestDelta;
    }

    public void ResetState()
    {
        lust = 0;
        romance = 0;
        purity = 0;
        corruptionLevel = 0;
        selfControl = 5;
        suspicion = 0;
        trustMasha = 0;
        trustArtem = 0;
        leraInterest = 0;
        currentSceneId = string.Empty;
        currentLineIndex = 0;
        currentLineId = string.Empty;
        selectedChoiceIndex = -1;
        choiceResultActive = false;
        pendingNextSceneId = string.Empty;
        Debug.Log("[GAMESTATE] State reset for a new game.", this);
    }
}
