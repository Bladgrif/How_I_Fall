using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VNPrototypeDebugStatsBuilder
{
    private const string VNPrototypeScenePath = "Assets/HowIFall/Scenes/VNPrototype.unity";
    private const string DebugStatsPanelName = "Debug Stats Panel";
    private const string DebugStatsControllerName = "Debug Stats Panel Controller";

    [MenuItem("How I Fall/Configure VN Debug Stats Panel")]
    public static void ConfigureDebugStatsPanel()
    {
        var scene = EditorSceneManager.OpenScene(VNPrototypeScenePath);
        GameObject debugStatsPanel = FindSceneObject(DebugStatsPanelName);

        if (debugStatsPanel == null)
        {
            Debug.LogError("VNPrototypeDebugStatsBuilder: Debug Stats Panel was not found in VNPrototype.");
            return;
        }

        GameObject controllerObject = FindSceneObject(DebugStatsControllerName);

        if (controllerObject == null)
        {
            controllerObject = new GameObject(DebugStatsControllerName);
        }

        DebugStatsPanelController controller = controllerObject.GetComponent<DebugStatsPanelController>();

        if (controller == null)
        {
            controller = controllerObject.AddComponent<DebugStatsPanelController>();
        }

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("root").objectReferenceValue = debugStatsPanel;
        serializedController.FindProperty("visibleByDefault").boolValue = false;
        serializedController.ApplyModifiedProperties();

        debugStatsPanel.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("VNPrototype Debug Stats Panel was configured.");
    }

    public static void ConfigureDebugStatsPanelBatch()
    {
        ConfigureDebugStatsPanel();
    }

    private static GameObject FindSceneObject(string objectName)
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.name == objectName && go.scene.IsValid())
            {
                return go;
            }
        }

        return null;
    }
}
