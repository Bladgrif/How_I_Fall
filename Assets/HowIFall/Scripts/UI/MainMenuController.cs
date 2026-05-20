using UnityEngine;

public class MainMenuController : MonoBehaviour
{
    public SettingsPanelController settingsPanel;

    public void StartGame()
    {
        SceneFlowManager.EnsureInstance().StartNewGame();
    }

    public void ContinueGame()
    {
        SceneFlowManager.EnsureInstance().ContinueGame();
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
        Debug.Log("About is not implemented yet");
    }

    public void OpenHelp()
    {
        Debug.Log("Help is not implemented yet");
    }

    public void OpenGallery()
    {
        Debug.Log("Gallery is not implemented yet");
    }

    public void ExitGame()
    {
        SceneFlowManager.EnsureInstance().QuitGame();
    }
}
