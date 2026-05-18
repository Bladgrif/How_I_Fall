using UnityEngine;
using UnityEngine.UI;

public class QuickSaveStatusView : MonoBehaviour
{
    public Text statusText;

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (statusText == null)
        {
            statusText = GetComponent<Text>();
        }

        if (statusText == null)
        {
            return;
        }

        bool hasSave = SaveManager.Instance != null && SaveManager.Instance.HasSave();
        statusText.text = hasSave ? "quick save: есть сохранение" : "quick save: нет сохранения";
    }
}
