using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class DebugStatsView : MonoBehaviour
{
    public VNStats stats;
    public GameObject root;
    public TextMeshProUGUI statsText;

    private void Start()
    {
        if (root == null)
        {
            root = gameObject;
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
        {
            root.SetActive(!root.activeSelf);
        }

        if (stats == null || statsText == null)
        {
            return;
        }

        statsText.text =
            "DEBUG STATS\n" +
            $"lust: {stats.lust}\n" +
            $"romance: {stats.romance}\n" +
            $"purity: {stats.purity}\n" +
            $"corruption: {stats.corruptionLevel}\n" +
            $"self_control: {stats.selfControl}\n" +
            $"suspicion: {stats.suspicion}\n\n" +
            $"trust_masha: {stats.trustMasha}\n" +
            $"trust_artem: {stats.trustArtem}\n" +
            $"lera_interest: {stats.leraInterest}";
    }
}
