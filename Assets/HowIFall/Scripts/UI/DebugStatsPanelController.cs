using UnityEngine;
using UnityEngine.InputSystem;

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
        if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame && root != null)
        {
            root.SetActive(!root.activeSelf);
        }
    }
}
