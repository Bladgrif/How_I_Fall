using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Editor-only installer and launcher for the isolated timed-beat technical demo.</summary>
public static class TimedNarrativeBeatDemoInstaller
{
    private const string VnScenePath = "Assets/HowIFall/Scenes/VNPrototype.unity";
    private const string HostName = "Timed Narrative Beat Controller (Technical Only)";
    private const string PanelName = "Timed Narrative Beat Panel (Technical Demo)";

    [MenuItem("How I Fall/Timed Narrative Beat/Install Technical Demo")]
    public static void InstallFromMenu()
    {
        Install();
        Debug.Log("[TIMED BEAT] Technical demo installed. It is not part of the normal story route.");
    }

    [MenuItem("How I Fall/Timed Narrative Beat/Run Technical Demo (Play Mode)")]
    public static void RunTechnicalDemoFromMenu()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[TIMED BEAT] Enter Play Mode before running the technical demo.");
            return;
        }

        TimedNarrativeBeatController beat = UnityEngine.Object.FindFirstObjectByType<TimedNarrativeBeatController>();
        VNDialogueController dialogue = VNDialogueController.Instance;
        DialogueSceneData start = AssetDatabase.LoadAssetAtPath<DialogueSceneData>(TimedNarrativeBeatDemoContentBuilder.StartScenePath);
        if (beat == null || dialogue == null || start == null || !dialogue.TryRouteToScene(start) || !beat.TryStartTechnicalDemo())
        {
            Debug.LogError("[TIMED BEAT] Technical demo could not start. Install the demo and use the VNPrototype scene.");
        }
    }

    public static void Install()
    {
        TimedNarrativeBeatDemoContentBuilder.Build();
        Scene scene = EditorSceneManager.OpenScene(VnScenePath, OpenSceneMode.Single);
        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        VNDialogueController dialogue = UnityEngine.Object.FindFirstObjectByType<VNDialogueController>();
        if (canvas == null || dialogue == null)
        {
            throw new InvalidOperationException("Timed narrative beat demo requires Canvas and VNDialogueController in VNPrototype.");
        }

        GameObject oldHost = GameObject.Find(HostName);
        if (oldHost != null)
        {
            UnityEngine.Object.DestroyImmediate(oldHost);
        }

        GameObject oldPanel = GameObject.Find(PanelName);
        if (oldPanel != null)
        {
            UnityEngine.Object.DestroyImmediate(oldPanel);
        }

        GameObject host = CreateUiObject(HostName, canvas.transform);
        host.SetActive(false);
        TimedNarrativeBeatController beat = host.AddComponent<TimedNarrativeBeatController>();
        GameObject panel = CreatePanel(canvas.transform, dialogue.dialogueText != null ? dialogue.dialogueText.font : TMP_Settings.defaultFontAsset, out TextMeshProUGUI prompt, out Button action, out TextMeshProUGUI remaining, out Slider progress);

        beat.dialogueController = dialogue;
        beat.rootPanel = panel;
        beat.promptText = prompt;
        beat.actionButton = action;
        beat.remainingTimeText = remaining;
        beat.progressSlider = progress;
        beat.demoDefinition = new TimedNarrativeBeatDefinition
        {
            promptText = "TEST: timed beat",
            actionText = "Действовать",
            durationSeconds = 5f,
            successNextScene = AssetDatabase.LoadAssetAtPath<DialogueSceneData>(TimedNarrativeBeatDemoContentBuilder.SuccessScenePath),
            timeoutNextScene = AssetDatabase.LoadAssetAtPath<DialogueSceneData>(TimedNarrativeBeatDemoContentBuilder.TimeoutScenePath)
        };
        panel.SetActive(false);
        host.SetActive(true);

        EditorUtility.SetDirty(beat);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static GameObject CreatePanel(Transform parent, TMP_FontAsset font, out TextMeshProUGUI prompt, out Button action, out TextMeshProUGUI remaining, out Slider progress)
    {
        GameObject panel = CreateUiObject(PanelName, parent);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(900f, 360f);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.035f, 0.06f, 0.1f, 0.97f);

        prompt = CreateText("Prompt", panel.transform, font, 42, TextAlignmentOptions.Center, new Vector2(0f, 92f), new Vector2(800f, 80f));
        remaining = CreateText("Remaining Time", panel.transform, font, 30, TextAlignmentOptions.Center, new Vector2(0f, 18f), new Vector2(300f, 50f));
        progress = CreateProgress(panel.transform);
        action = CreateActionButton(panel.transform, font);
        return panel;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions alignment, Vector2 position, Vector2 dimensions)
    {
        GameObject textObject = CreateUiObject(name, parent);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = new Color(0.93f, 0.96f, 1f, 1f);
        text.raycastTarget = false;
        return text;
    }

    private static Slider CreateProgress(Transform parent)
    {
        GameObject root = CreateUiObject("Progress", parent);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -34f);
        rect.sizeDelta = new Vector2(700f, 24f);
        Image background = root.AddComponent<Image>();
        background.color = new Color(0.1f, 0.15f, 0.22f, 1f);
        Slider slider = root.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        GameObject fillArea = CreateUiObject("Fill Area", root.transform);
        Stretch(fillArea.GetComponent<RectTransform>(), new Vector2(8f, 4f), new Vector2(-8f, -4f));
        GameObject fill = CreateUiObject("Fill", fillArea.transform);
        Stretch(fill.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.29f, 0.63f, 0.9f, 1f);
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.targetGraphic = background;
        return slider;
    }

    private static Button CreateActionButton(Transform parent, TMP_FontAsset font)
    {
        GameObject root = CreateUiObject("Action", parent);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -120f);
        rect.sizeDelta = new Vector2(380f, 72f);
        Image image = root.AddComponent<Image>();
        image.color = new Color(0.17f, 0.42f, 0.65f, 1f);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;
        TextMeshProUGUI label = CreateText("Label", root.transform, font, 32, TextAlignmentOptions.Center, Vector2.zero, new Vector2(360f, 60f));
        label.text = "Действовать";
        return button;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
