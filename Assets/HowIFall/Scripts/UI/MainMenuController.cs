using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuController : MonoBehaviour
{
    private const string PrototypeSceneName = "VNPrototype";

    public void StartGame()
    {
        SceneManager.LoadScene(PrototypeSceneName);
    }

    public void OpenSettings()
    {
        Debug.Log("Settings is not implemented yet");
    }

    public void ExitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#endif
    }
}
