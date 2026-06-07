using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SceneFlowManager : MonoBehaviour
{
    private const string MainMenuSceneName = "MainMenu";
    private const string VNPrototypeSceneName = "VNPrototype";

    public static SceneFlowManager Instance { get; private set; }

    public static SceneFlowManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        SceneFlowManager existing = FindFirstObjectByType<SceneFlowManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject managerGo = new GameObject("SceneFlowManager");
        return managerGo.AddComponent<SceneFlowManager>();
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

    public void StartNewGame()
    {
        GameState.EnsureInstance().ResetState();
        SceneManager.LoadScene(VNPrototypeSceneName, LoadSceneMode.Single);
    }

    public void ContinueGame()
    {
        if (SaveManager.Instance != null && SaveManager.Instance.LoadLatestSave())
        {
            LoadLoadedGameScene();
            return;
        }

        Debug.LogWarning("No save file found.");
    }

    public void LoadLoadedGameScene()
    {
        SceneManager.LoadScene(VNPrototypeSceneName, LoadSceneMode.Single);
    }

    public void ReturnToMainMenu()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
    }

    public void QuitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#endif
    }
}
