using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VNPrototypeAudioListenerBuilder
{
    private const string VNPrototypeScenePath = "Assets/HowIFall/Scenes/VNPrototype.unity";

    [MenuItem("How I Fall/Configure VN Audio Listener")]
    public static void ConfigureAudioListener()
    {
        var scene = EditorSceneManager.OpenScene(VNPrototypeScenePath);
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("VNPrototypeAudioListenerBuilder: Main Camera was not found.");
            return;
        }

        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);

        foreach (AudioListener listener in listeners)
        {
            if (listener.gameObject != mainCamera.gameObject)
            {
                Object.DestroyImmediate(listener);
            }
        }

        if (mainCamera.GetComponent<AudioListener>() == null)
        {
            mainCamera.gameObject.AddComponent<AudioListener>();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("VNPrototype AudioListener was configured on Main Camera.");
    }

    public static void ConfigureAudioListenerBatch()
    {
        ConfigureAudioListener();
    }
}
