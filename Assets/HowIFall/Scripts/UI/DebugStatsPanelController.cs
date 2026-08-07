using UnityEngine;

public class DebugStatsPanelController : MonoBehaviour
{
    public GameObject root;
    public bool visibleByDefault = false;

    private void Start()
    {
        if (root == null)
        {
            root = gameObject;
        }

        root.SetActive(visibleByDefault);
    }

    private void Update()
    {
        if (VNInputMap.WasPressedThisFrame(VNInputAction.ToggleDebugStatsPanel) && root != null)
        {
            root.SetActive(!root.activeSelf);
        }
    }
}
