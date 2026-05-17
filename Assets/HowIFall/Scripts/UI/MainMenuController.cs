using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuController : MonoBehaviour
{
    private const string PrototypeSceneName = "VNPrototype";
    public SettingsPanelController settingsPanel;

    public void StartGame()
    {
        GameState.EnsureInstance().ResetState();
        SceneManager.LoadScene(PrototypeSceneName);
    }

    public void ContinueGame()
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogWarning("SaveManager.Instance is missing.");
            return;
        }

        if (SaveManager.Instance.Load())
        {
            SceneManager.LoadScene(PrototypeSceneName);
            return;
        }

        Debug.LogWarning("No save file found.");
    }

    public void DeleteSave()
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogWarning("SaveManager.Instance is missing.");
            return;
        }

        SaveManager.Instance.DeleteSave();
    }

    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.Show();
            return;
        }

        Debug.LogWarning("SettingsPanelController is not assigned.");
    }

    public void OpenAbout()
    {
        Debug.Log("About screen is not implemented yet.");
    }

    public void OpenHelp()
    {
        Debug.Log("Help screen is not implemented yet.");
    }

    public void ExitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#endif
    }
}
