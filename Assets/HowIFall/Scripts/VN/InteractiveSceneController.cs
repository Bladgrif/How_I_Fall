using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Scene-local owner for a small authored interactive image scene. TECH fixtures remain non-canonical.</summary>
public sealed class InteractiveSceneController : MonoBehaviour
{
    private static readonly Color BackdropColor = new Color(0.012f, 0.028f, 0.055f, 0.96f);
    private static readonly Color ImageFrameColor = new Color(0.045f, 0.095f, 0.145f, 1f);
    private static readonly Color HotspotColor = new Color(0.16f, 0.44f, 0.62f, 0.48f);
    private static readonly Color TextColor = new Color(0.89f, 0.96f, 1f, 1f);
    private static Sprite runtimeBackgroundSprite;
    private VNDialogueController dialogueController;
    private GameObject root;
    private RectTransform displayedImageRect;
    private TextMeshProUGUI feedbackText;
    private readonly Dictionary<string, Button> hotspotButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
    private readonly HashSet<string> completedHotspotIds = new HashSet<string>(StringComparer.Ordinal);
    private InteractiveSceneData activeScene;
    private SpecialModeLease activeLease;
    private bool dialogueShellSuppressed;
    private int activationCount;

    public bool IsRunning => activeScene != null && activeLease != null;
    public bool IsRuntimeUiActive => root != null && root.activeInHierarchy;
    public int ActivationCount => activationCount;
    public RectTransform DisplayedImageRect => displayedImageRect;
    public InteractiveSceneData ActiveScene => activeScene;

    public static InteractiveSceneController TryCreateRuntime(VNDialogueController controller) => TryCreateRuntime(controller, out InteractiveSceneController result, out _) ? result : null;
    public static bool TryCreateRuntime(VNDialogueController controller, out InteractiveSceneController result, out string failureReason)
    {
        result = null; failureReason = string.Empty;
        if (controller == null) { failureReason = "controller not ready"; return false; }
        InteractiveSceneController existing = controller.GetComponent<InteractiveSceneController>();
        if (existing != null) { result = existing; return true; }
        Canvas canvas = controller.GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        if (canvas == null || !canvas.gameObject.activeInHierarchy) { failureReason = "Canvas/UI unavailable"; return false; }
        InteractiveSceneController created = controller.gameObject.AddComponent<InteractiveSceneController>();
        created.InitializeRuntime(controller, canvas);
        if (created.root == null) { Destroy(created); failureReason = "Canvas/UI unavailable"; return false; }
        result = created; return true;
    }

    private void InitializeRuntime(VNDialogueController controller, Canvas canvas) { dialogueController = controller; BuildRuntimeUi(canvas); root.SetActive(false); }

    public bool TryStart(InteractiveSceneData scene, out string failureReason)
    {
        failureReason = string.Empty;
        if (dialogueController == null) { failureReason = "controller not ready"; return false; }
        if (scene == null) { failureReason = "null interactive scene data"; return false; }
        if (SceneFlowManager.IsReplayModeActive) { failureReason = "Replay active"; return false; }
        if (IsRunning) { failureReason = "interactive scene already active"; return false; }
        if (root == null || displayedImageRect == null) { failureReason = "Canvas/UI unavailable"; return false; }
        if (!scene.TryValidate(dialogueController, out string diagnostic)) { failureReason = "interactive scene data invalid: " + diagnostic; return false; }
        if (!dialogueController.TryEnterSpecialMode(this, SpecialModePolicy.InteractiveScene, out SpecialModeLease lease)) { failureReason = dialogueController.HasActiveSpecialMode ? "another special mode active" : "lease rejected"; return false; }
        if (!dialogueController.TrySuppressDialogueShell(this)) { dialogueController.ExitSpecialMode(lease); failureReason = "dialogue shell unavailable"; return false; }
        activeScene = scene; activeLease = lease; dialogueShellSuppressed = true; activationCount = 0; completedHotspotIds.Clear();
        BuildHotspots(); feedbackText.text = "Select an available technical hotspot."; root.SetActive(true); root.transform.SetAsLastSibling(); Refresh(); return true;
    }

    public bool TryActivateHotspot(string hotspotId)
    {
        if (!IsRunning || string.IsNullOrWhiteSpace(hotspotId) || activeScene.hotspots == null) return false;
        InteractiveHotspotData hotspot = activeScene.hotspots.Find(item => item != null && item.hotspotId == hotspotId);
        GameState state = GameState.Instance;
        if (hotspot == null || state == null || !hotspot.IsAvailable(state, completedHotspotIds) || hotspot.outcome == null || !hotspot.outcome.TryApply(state)) return false;
        activationCount++;
        completedHotspotIds.Add(hotspot.hotspotId);
        feedbackText.text = string.IsNullOrWhiteSpace(hotspot.outcome.feedbackText) ? hotspot.displayName + " completed." : hotspot.outcome.feedbackText;
        DialogueSceneData nextScene = hotspot.outcome.nextScene;
        bool completesScene = hotspot.outcome.completeScene || nextScene != null;
        Refresh();
        return completesScene ? Complete(nextScene ?? activeScene.completionNextScene) : true;
    }

    public bool IsHotspotAvailable(string hotspotId) => TryGetHotspot(hotspotId, out InteractiveHotspotData hotspot) && GameState.Instance != null && hotspot.IsAvailable(GameState.Instance, completedHotspotIds);
    public bool IsHotspotCompleted(string hotspotId) => TryGetHotspot(hotspotId, out InteractiveHotspotData hotspot) && hotspot.IsCompleted(completedHotspotIds);
    public Button GetHotspotButton(string hotspotId) => hotspotButtons.TryGetValue(hotspotId, out Button button) ? button : null;

    public void Refresh()
    {
        if (!IsRunning || activeScene.hotspots == null) return;
        GameState state = GameState.Instance;
        foreach (InteractiveHotspotData hotspot in activeScene.hotspots)
        {
            if (hotspot == null || !hotspotButtons.TryGetValue(hotspot.hotspotId, out Button button) || button == null) continue;
            bool completed = hotspot.IsCompleted(completedHotspotIds);
            bool available = state != null && hotspot.IsAvailable(state, completedHotspotIds);
            button.interactable = available;
            Image image = button.GetComponent<Image>();
            if (image != null) image.color = completed ? new Color(0.15f, 0.22f, 0.27f, 0.52f) : available ? HotspotColor : new Color(0.09f, 0.13f, 0.18f, 0.43f);
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = completed ? hotspot.displayName + "\nCOMPLETED" : available ? hotspot.displayName : hotspot.displayName + "\nLOCKED";
        }
    }

    private bool Complete(DialogueSceneData nextScene)
    {
        if (!IsRunning) return false;
        SpecialModeLease lease = activeLease; activeLease = null; activeScene = null; completedHotspotIds.Clear(); root.SetActive(false);
        if (dialogueShellSuppressed) { dialogueController.ReleaseDialogueShellSuppression(this); dialogueShellSuppressed = false; }
        if (lease != null) dialogueController.ExitSpecialMode(lease);
        return nextScene == null || dialogueController.TryRouteToScene(nextScene);
    }

    private bool TryGetHotspot(string hotspotId, out InteractiveHotspotData hotspot)
    {
        hotspot = activeScene != null && activeScene.hotspots != null ? activeScene.hotspots.Find(item => item != null && item.hotspotId == hotspotId) : null;
        return hotspot != null;
    }

    private void BuildRuntimeUi(Canvas canvas)
    {
        root = CreateUiObject(canvas.transform, "Interactive Hotspot Runtime View"); Stretch(root.GetComponent<RectTransform>()); root.transform.SetAsLastSibling();
        Image backdrop = root.AddComponent<Image>(); backdrop.color = BackdropColor; backdrop.raycastTarget = true;
        TextMeshProUGUI title = CreateText(root.transform, "Technical Label", "TECH DEMO ONLY / NOT CANON", 22f, FontStyles.Bold, TextAlignmentOptions.Center, TextColor);
        SetAnchors(title.rectTransform, new Vector2(0.1f, 0.93f), new Vector2(0.9f, 0.985f));
        GameObject imageContainer = CreateUiObject(root.transform, "Aspect Fit Image Container"); SetAnchors(imageContainer.GetComponent<RectTransform>(), new Vector2(0.055f, 0.095f), new Vector2(0.945f, 0.90f));
        GameObject imageObject = CreateUiObject(imageContainer.transform, "Displayed Interactive Image"); displayedImageRect = imageObject.GetComponent<RectTransform>(); Stretch(displayedImageRect);
        Image image = imageObject.AddComponent<Image>(); image.sprite = GetRuntimeBackgroundSprite(); image.preserveAspect = true; image.raycastTarget = true;
        AspectRatioFitter fitter = imageObject.AddComponent<AspectRatioFitter>(); fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fitter.aspectRatio = 16f / 9f;
        Outline outline = imageObject.AddComponent<Outline>(); outline.effectColor = ImageFrameColor; outline.effectDistance = new Vector2(3f, -3f);
        feedbackText = CreateText(root.transform, "Feedback", string.Empty, 20f, FontStyles.Normal, TextAlignmentOptions.Center, TextColor); SetAnchors(feedbackText.rectTransform, new Vector2(0.12f, 0.02f), new Vector2(0.88f, 0.075f));
    }

    private void BuildHotspots()
    {
        foreach (Button button in hotspotButtons.Values) if (button != null) Destroy(button.gameObject);
        hotspotButtons.Clear();
        foreach (InteractiveHotspotData hotspot in activeScene.hotspots)
        {
            GameObject objectRoot = CreateUiObject(displayedImageRect, "Hotspot " + hotspot.hotspotId); RectTransform rect = objectRoot.GetComponent<RectTransform>(); rect.anchorMin = hotspot.normalizedRect.min; rect.anchorMax = hotspot.normalizedRect.max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            Image image = objectRoot.AddComponent<Image>(); image.color = HotspotColor; Button button = objectRoot.AddComponent<Button>();
            ColorBlock colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.30f, 1.38f, 1.32f, 1f); colors.selectedColor = new Color(1.30f, 1.38f, 1.32f, 1f); colors.pressedColor = new Color(0.72f, 0.88f, 0.98f, 1f); colors.disabledColor = new Color(0.48f, 0.54f, 0.60f, 0.70f); colors.colorMultiplier = 1f; button.colors = colors;
            TextMeshProUGUI label = CreateText(objectRoot.transform, "Label", hotspot.displayName, 18f, FontStyles.Bold, TextAlignmentOptions.Center, TextColor); Stretch(label.rectTransform, 8f, 8f, 8f, 8f);
            string hotspotId = hotspot.hotspotId; button.onClick.AddListener(() => TryActivateHotspot(hotspotId)); hotspotButtons.Add(hotspotId, button);
        }
    }

    private void OnDisable() { CleanupWithoutRouting(); }
    private void OnDestroy() { CleanupWithoutRouting(); }
    private void CleanupWithoutRouting()
    {
        if (activeLease == null) return;
        SpecialModeLease lease = activeLease; activeLease = null; activeScene = null; completedHotspotIds.Clear();
        if (dialogueShellSuppressed && dialogueController != null) { dialogueController.ReleaseDialogueShellSuppression(this); dialogueShellSuppressed = false; }
        dialogueController?.ExitSpecialMode(lease); if (root != null) root.SetActive(false);
    }

    private static GameObject CreateUiObject(Transform parent, string name) { GameObject result = new GameObject(name, typeof(RectTransform)); result.transform.SetParent(parent, false); return result; }
    private static TextMeshProUGUI CreateText(Transform parent, string name, string value, float size, FontStyles style, TextAlignmentOptions alignment, Color color)
    { GameObject result = CreateUiObject(parent, name); TextMeshProUGUI text = result.AddComponent<TextMeshProUGUI>(); text.font = TMP_Settings.defaultFontAsset; text.text = value; text.fontSize = size; text.fontStyle = style; text.alignment = alignment; text.color = color; text.enableWordWrapping = true; return text; }
    private static void Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); }
    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
    private static Sprite GetRuntimeBackgroundSprite()
    {
        if (runtimeBackgroundSprite != null) return runtimeBackgroundSprite;
        const int width = 320, height = 180; Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "InteractiveHotspotTechnicalRoom" }; Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++) { float shade = Mathf.Lerp(0.035f, 0.11f, y / (float)(height - 1)); for (int x = 0; x < width; x++) { float edge = Mathf.Clamp01(Mathf.Min(x, width - 1 - x, y, height - 1 - y) / 18f); pixels[y * width + x] = new Color(shade * edge, (shade + 0.025f) * edge, (shade + 0.065f) * edge, 1f); } }
        texture.SetPixels(pixels); texture.Apply(false, true); runtimeBackgroundSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), height); runtimeBackgroundSprite.name = "InteractiveHotspotTechnicalRoom"; runtimeBackgroundSprite.hideFlags = HideFlags.HideAndDontSave; return runtimeBackgroundSprite;
    }
}
