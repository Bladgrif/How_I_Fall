using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class VNAutoDialogueSettingsInstaller
{
    private const string ScenePath = "Assets/HowIFall/Scenes/VNPrototype.unity";

    [MenuItem("How I Fall/Install VN Auto Dialogue Settings UI")]
    public static void Install()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        VNDialogueController controller = Object.FindFirstObjectByType<VNDialogueController>();
        if (controller == null || controller.vnSettingsPanel == null || controller.vnFullscreenToggle == null || controller.vnTextSpeedSlider == null)
        {
            throw new System.InvalidOperationException("VN settings UI references are incomplete.");
        }

        RemoveExisting(controller.vnSettingsPanel.transform, "Auto Forward Toggle");
        RemoveExisting(controller.vnSettingsPanel.transform, "Auto Forward Delay Slider");

        Toggle autoToggle = Object.Instantiate(controller.vnFullscreenToggle, controller.vnSettingsPanel.transform);
        autoToggle.name = "Auto Forward Toggle";
        autoToggle.onValueChanged.RemoveAllListeners();
        autoToggle.SetIsOnWithoutNotify(false);
        SetPosition(autoToggle.transform as RectTransform, new Vector2(-245f, -150f));
        TextMeshProUGUI toggleLabel = autoToggle.GetComponentInChildren<TextMeshProUGUI>(true);
        if (toggleLabel != null)
        {
            toggleLabel.text = "Авто-пролистывание";
        }

        Slider autoDelay = Object.Instantiate(controller.vnTextSpeedSlider, controller.vnSettingsPanel.transform);
        autoDelay.name = "Auto Forward Delay Slider";
        autoDelay.onValueChanged.RemoveAllListeners();
        autoDelay.minValue = 50f;
        autoDelay.maxValue = 500f;
        autoDelay.wholeNumbers = true;
        autoDelay.SetValueWithoutNotify(250f);
        SetPosition(autoDelay.transform as RectTransform, new Vector2(235f, -205f));

        GameObject delayLabel = new GameObject("Auto Forward Delay Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        delayLabel.transform.SetParent(controller.vnSettingsPanel.transform, false);
        RectTransform labelRect = delayLabel.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0.5f);
        labelRect.anchorMax = new Vector2(0f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(90f, -205f);
        labelRect.sizeDelta = new Vector2(190f, 34f);
        TextMeshProUGUI sourceLabel = controller.speakerText;
        TextMeshProUGUI label = delayLabel.GetComponent<TextMeshProUGUI>();
        if (sourceLabel != null)
        {
            label.font = sourceLabel.font;
            label.fontSharedMaterial = sourceLabel.fontSharedMaterial;
            label.fontSize = sourceLabel.fontSize;
            label.color = sourceLabel.color;
        }
        label.alignment = TextAlignmentOptions.Left;
        label.text = "Задержка Auto (0,5–5 с)";

        controller.vnAutoForwardToggle = autoToggle;
        controller.vnAutoForwardDelaySlider = autoDelay;
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("VN Auto Dialogue settings UI installed.");
    }

    private static void RemoveExisting(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
        {
            Object.DestroyImmediate(child.gameObject);
        }
    }

    private static void SetPosition(RectTransform rect, Vector2 position)
    {
        if (rect != null)
        {
            rect.anchoredPosition = position;
        }
    }
}
