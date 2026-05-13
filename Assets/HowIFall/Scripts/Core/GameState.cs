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
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ApplyChoice(DialogueChoice choice)
    {
        if (choice == null)
        {
            return;
        }

        lust += choice.lustDelta;
        romance += choice.romanceDelta;
        purity += choice.purityDelta;
        corruptionLevel += choice.corruptionDelta;
        selfControl += choice.selfControlDelta;
        suspicion += choice.suspicionDelta;
        trustMasha += choice.trustMashaDelta;
        trustArtem += choice.trustArtemDelta;
        leraInterest += choice.leraInterestDelta;
    }
}
