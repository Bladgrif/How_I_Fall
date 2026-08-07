using TMPro;
using UnityEngine;

public class DebugStatsView : MonoBehaviour
{
    public GameObject root;
    public TextMeshProUGUI statsText;
    private bool warnedMissingGameState;

    private void Start()
    {
        if (root == null)
        {
            root = gameObject;
        }
    }

    private void Update()
    {
        if (VNInputMap.WasPressedThisFrame(VNInputAction.ToggleDebugStatsView))
        {
            root.SetActive(!root.activeSelf);
        }

        if (statsText == null)
        {
            return;
        }

        if (GameState.Instance == null)
        {
            if (!warnedMissingGameState)
            {
                Debug.LogWarning("DebugStatsView: GameState.Instance is missing.", this);
                warnedMissingGameState = true;
            }

            return;
        }

        warnedMissingGameState = false;
        GameState gameState = GameState.Instance;

        statsText.text =
            "DEBUG STATS\n" +
            $"lust: {gameState.lust}\n" +
            $"romance: {gameState.romance}\n" +
            $"purity: {gameState.purity}\n" +
            $"corruption: {gameState.corruptionLevel}\n" +
            $"self_control: {gameState.selfControl}\n" +
            $"suspicion: {gameState.suspicion}\n\n" +
            $"trust_masha: {gameState.trustMasha}\n" +
            $"trust_artem: {gameState.trustArtem}\n" +
            $"lera_interest: {gameState.leraInterest}";
    }
}
