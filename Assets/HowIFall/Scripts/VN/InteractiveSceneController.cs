using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Scene-local owner for a small authored interactive image scene. TECH fixtures remain non-canonical.</summary>
public sealed class InteractiveSceneController : MonoBehaviour
{
    private const string FallbackFontResourcePath = "Fonts & Materials/LiberationSans SDF - Fallback";
    // Palette follows the accepted Reading/History/Choice runtime language.
    private static readonly Color BackdropColor = new Color(0.012f, 0.028f, 0.055f, 0.96f);
    private static readonly Color ImageFrameColor = new Color(0.045f, 0.095f, 0.145f, 1f);
    private static readonly Color TextColor = new Color(0.89f, 0.96f, 1f, 1f);
    private static readonly Color ChipFillColor = new Color(0.035f, 0.09f, 0.155f, 0.82f);
    private static readonly Color ChipFocusedFillColor = new Color(0.06f, 0.14f, 0.22f, 0.9f);
    private static readonly Color ChipDimFillColor = new Color(0.03f, 0.06f, 0.10f, 0.62f);
    private static readonly Color RingAvailableColor = new Color(0.32f, 0.82f, 0.95f, 0.95f);
    private static readonly Color RingFocusedColor = new Color(0.58f, 0.95f, 1f, 1f);
    private static readonly Color RingLockedColor = new Color(0.50f, 0.58f, 0.66f, 0.55f);
    private static readonly Color RingLockedFocusedColor = new Color(0.80f, 0.87f, 0.94f, 0.95f);
    private static readonly Color RingCompletedColor = new Color(0.24f, 0.55f, 0.62f, 0.60f);
    private static readonly Color GlyphAvailableColor = new Color(0.88f, 0.96f, 1f, 1f);
    private static readonly Color GlyphLockedColor = new Color(0.56f, 0.64f, 0.72f, 0.75f);
    private static readonly Color GlyphCompletedColor = new Color(0.52f, 0.68f, 0.74f, 0.65f);
    private static readonly Color LabelTextColor = new Color(0.92f, 0.96f, 1f, 1f);
    private static readonly Color LabelLockedColor = new Color(0.64f, 0.71f, 0.78f, 0.85f);
    private static readonly Color LabelFillColor = new Color(0.035f, 0.09f, 0.155f, 0.78f);
    private static readonly Color LabelFocusedFillColor = new Color(0.075f, 0.17f, 0.26f, 0.88f);
    private static readonly Color MarkerEdgeColor = new Color(0.35f, 0.72f, 0.91f, 0.75f);
    private static readonly Color MarkerLockedEdgeColor = new Color(0.42f, 0.50f, 0.58f, 0.40f);
    private static readonly Color MarkerCompletedEdgeColor = new Color(0.30f, 0.52f, 0.58f, 0.35f);
    private static readonly Color AnchorDotColor = new Color(0.35f, 0.85f, 0.97f, 0.95f);
    private static readonly Color TechCaptionColor = new Color(0.75f, 0.84f, 0.92f, 0.72f);
    private static Sprite runtimeBackgroundSprite;
    private static TMP_FontAsset fallbackFont;
    private static Sprite circleSprite, ringSprite, pillSprite, strokeSprite, laptopGlyphSprite, notesGlyphSprite, doorGlyphSprite;
    private VNDialogueController dialogueController;
    private GameObject root;
    private RectTransform displayedImageRect;
    private Image backgroundImage;
    private readonly Dictionary<string, Button> hotspotButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
    private readonly Dictionary<string, MarkerVisuals> hotspotMarkers = new Dictionary<string, MarkerVisuals>(StringComparer.Ordinal);
    private readonly HashSet<string> completedHotspotIds = new HashSet<string>(StringComparer.Ordinal);
    private InteractiveSceneData activeScene;
    private SpecialModeLease activeLease;
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
        string initialFeedback = string.IsNullOrWhiteSpace(scene.initialFeedback) ? "Select an available technical hotspot." : scene.initialFeedback;
        if (!dialogueController.TryShowInteractiveSceneFeedback(scene.feedbackSpeaker, initialFeedback)) { dialogueController.ExitSpecialMode(lease); failureReason = "dialogue shell unavailable"; return false; }
        activeScene = scene; activeLease = lease; activationCount = 0; completedHotspotIds.Clear();
        backgroundImage.sprite = scene.background != null ? scene.background : GetRuntimeBackgroundSprite();
        BuildHotspots();
        root.SetActive(true); Refresh(); SelectInitialHotspot(); return true;
    }

    public bool TryActivateHotspot(string hotspotId)
    {
        if (!IsRunning || string.IsNullOrWhiteSpace(hotspotId) || activeScene.hotspots == null) return false;
        InteractiveHotspotData hotspot = activeScene.hotspots.Find(item => item != null && item.hotspotId == hotspotId);
        GameState state = GameState.Instance;
        if (hotspot == null || state == null || !hotspot.IsAvailable(state, completedHotspotIds) || hotspot.outcome == null || !hotspot.outcome.TryApply(state)) return false;
        activationCount++;
        completedHotspotIds.Add(hotspot.hotspotId);
        string feedback = string.IsNullOrWhiteSpace(hotspot.outcome.feedbackText) ? hotspot.displayName + " completed." : hotspot.outcome.feedbackText;
        dialogueController.TryShowInteractiveSceneFeedback(activeScene.feedbackSpeaker, feedback);
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
            bool available = state != null && hotspot.IsAvailable(state, completedHotspotIds);
            button.interactable = available;
            ApplyMarkerState(hotspot.hotspotId);
        }
        EnsureFocusOwner();
    }

    /// <summary>
    /// Restores a usable keyboard/controller focus owner when EventSystem selection is
    /// missing or stranded on a disabled hotspot (Game Menu close clears selection).
    /// A currently selected interactable hotspot is preserved.
    /// </summary>
    public void EnsureFocusOwner()
    {
        if (!IsRunning || root == null || !root.activeInHierarchy) return;
        if (dialogueController != null && dialogueController.IsGameMenuOpen) return;
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return;
        GameObject selected = eventSystem.currentSelectedGameObject;
        if (selected != null && selected.activeInHierarchy)
        {
            foreach (Button button in hotspotButtons.Values)
            {
                if (button != null && button.gameObject == selected)
                {
                    if (button.interactable) return;
                    break;
                }
            }
        }
        SelectInitialHotspot();
    }

    private void SelectInitialHotspot()
    {
        if (activeScene == null || activeScene.hotspots == null) return;
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return;
        foreach (InteractiveHotspotData hotspot in activeScene.hotspots)
        {
            if (hotspot == null || !hotspotButtons.TryGetValue(hotspot.hotspotId, out Button button) || button == null || !button.interactable) continue;
            button.Select();
            return;
        }
    }

    /// <summary>Deterministic marker presentation: the marker owns every visible state, the Selectable tint stays neutral.</summary>
    private void ApplyMarkerState(string hotspotId)
    {
        if (!hotspotMarkers.TryGetValue(hotspotId, out MarkerVisuals marker) || marker == null || marker.button == null) return;
        bool completed = IsHotspotCompleted(hotspotId);
        bool available = !completed && IsHotspotAvailable(hotspotId);
        bool selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == marker.button.gameObject;
        bool emphasized = marker.driver != null && marker.driver.pointerOver || selected;
        if (marker.chipFill != null) marker.chipFill.color = completed ? ChipDimFillColor : emphasized ? ChipFocusedFillColor : available ? ChipFillColor : ChipDimFillColor;
        if (marker.ring != null) marker.ring.color = completed ? RingCompletedColor : available ? (emphasized ? RingFocusedColor : RingAvailableColor) : emphasized ? RingLockedFocusedColor : RingLockedColor;
        if (marker.glyph != null) marker.glyph.color = completed ? GlyphCompletedColor : available || emphasized ? GlyphAvailableColor : GlyphLockedColor;
        if (marker.dot != null) marker.dot.color = available ? AnchorDotColor : Color.clear;
        if (marker.labelRoot != null) marker.labelRoot.SetActive(!completed);
        if (!completed && marker.label != null)
        {
            marker.label.color = available ? LabelTextColor : LabelLockedColor;
            if (marker.labelFill != null) marker.labelFill.color = emphasized && available ? LabelFocusedFillColor : LabelFillColor;
            if (marker.labelEdge != null) marker.labelEdge.color = completed ? MarkerCompletedEdgeColor : available ? (emphasized ? RingFocusedColor : MarkerEdgeColor) : MarkerLockedEdgeColor;
        }
        if (marker.markerRoot != null) marker.markerRoot.transform.localScale = emphasized && !completed ? new Vector3(1.06f, 1.06f, 1f) : Vector3.one;
    }

    private bool Complete(DialogueSceneData nextScene)
    {
        if (!IsRunning) return false;
        SpecialModeLease lease = activeLease; activeLease = null; activeScene = null; completedHotspotIds.Clear(); root.SetActive(false);
        ClearHotspotSelectionIfOwned();
        dialogueController.RestoreReadingAfterInteractiveSceneFeedback();
        if (lease != null) dialogueController.ExitSpecialMode(lease);
        return nextScene == null || dialogueController.TryRouteToScene(nextScene);
    }

    /// <summary>EventSystem keeps a stale selection on deactivated hotspot buttons; mirror the Game Menu close cleanup.</summary>
    private void ClearHotspotSelectionIfOwned()
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        if (selected == null) return;
        foreach (Button button in hotspotButtons.Values)
        {
            if (button != null && button.gameObject == selected) { eventSystem.SetSelectedGameObject(null); return; }
        }
    }

    private bool TryGetHotspot(string hotspotId, out InteractiveHotspotData hotspot)
    {
        hotspot = activeScene != null && activeScene.hotspots != null ? activeScene.hotspots.Find(item => item != null && item.hotspotId == hotspotId) : null;
        return hotspot != null;
    }

    private void BuildRuntimeUi(Canvas canvas)
    {
        Transform host = ResolveShellHost(canvas, out int shellBoxSiblingIndex);
        root = CreateUiObject(host, "Interactive Hotspot Runtime View"); Stretch(root.GetComponent<RectTransform>());
        if (shellBoxSiblingIndex >= 0) root.transform.SetSiblingIndex(shellBoxSiblingIndex); else root.transform.SetAsLastSibling();
        Image backdrop = root.AddComponent<Image>(); backdrop.color = BackdropColor; backdrop.raycastTarget = true;
        GameObject imageContainer = CreateUiObject(root.transform, "Aspect Fit Image Container"); Stretch(imageContainer.GetComponent<RectTransform>());
        GameObject imageObject = CreateUiObject(imageContainer.transform, "Displayed Interactive Image"); displayedImageRect = imageObject.GetComponent<RectTransform>(); Stretch(displayedImageRect);
        backgroundImage = imageObject.AddComponent<Image>(); backgroundImage.sprite = GetRuntimeBackgroundSprite(); backgroundImage.preserveAspect = true; backgroundImage.raycastTarget = true;
        AspectRatioFitter fitter = imageObject.AddComponent<AspectRatioFitter>(); fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fitter.aspectRatio = 16f / 9f;
        Outline outline = imageObject.AddComponent<Outline>(); outline.effectColor = ImageFrameColor; outline.effectDistance = new Vector2(3f, -3f);
        // Built last so the full-bleed background never covers the TECH caption.
        TextMeshProUGUI caption = CreateText(root.transform, "Tech Caption", "TECH DEMO ONLY / NOT CANON", 17f, FontStyles.Normal, TextAlignmentOptions.TopLeft, TechCaptionColor);
        SetAnchors(caption.rectTransform, new Vector2(0.012f, 0.966f), new Vector2(0.5f, 0.994f));
    }

    /// <summary>
    /// Hands-on integration correction: the Hotspot view renders inside the Reading shell
    /// layer — above the reading background, directly below the ordinary Dialogue Box — so
    /// hotspot feedback appears through the accepted Reading dialogue presentation instead
    /// of a private panel. Unexpected shell structure falls back to the top canvas layer.
    /// </summary>
    private Transform ResolveShellHost(Canvas canvas, out int shellBoxSiblingIndex)
    {
        shellBoxSiblingIndex = -1;
        Transform shellBox = dialogueController != null && dialogueController.dialogueUiRoot != null ? dialogueController.dialogueUiRoot.transform : null;
        if (shellBox == null || shellBox.parent == null || shellBox.parent == canvas.transform) return canvas.transform;
        shellBoxSiblingIndex = shellBox.GetSiblingIndex();
        return shellBox.parent;
    }

    private void BuildHotspots()
    {
        foreach (Button button in hotspotButtons.Values) if (button != null) Destroy(button.gameObject);
        hotspotButtons.Clear(); hotspotMarkers.Clear();
        List<Button> builtButtons = new List<Button>(activeScene.hotspots.Count);
        foreach (InteractiveHotspotData hotspot in activeScene.hotspots)
        {
            GameObject objectRoot = CreateUiObject(displayedImageRect, "Hotspot " + hotspot.hotspotId); RectTransform rect = objectRoot.GetComponent<RectTransform>(); rect.anchorMin = hotspot.normalizedRect.min; rect.anchorMax = hotspot.normalizedRect.max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            Image hitArea = objectRoot.AddComponent<Image>(); hitArea.color = Color.clear; hitArea.raycastTarget = true;
            Button button = objectRoot.AddComponent<Button>();
            button.targetGraphic = null;
            ColorBlock colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = Color.white; colors.selectedColor = Color.white; colors.pressedColor = Color.white; colors.disabledColor = Color.white; colors.colorMultiplier = 1f; colors.fadeDuration = 0f; button.colors = colors;
            MarkerVisuals marker = BuildMarker(objectRoot, hotspot);
            MarkerStateDriver driver = objectRoot.AddComponent<MarkerStateDriver>(); driver.owner = this; driver.hotspotId = hotspot.hotspotId;
            marker.button = button; marker.driver = driver;
            string hotspotId = hotspot.hotspotId; button.onClick.AddListener(() => TryActivateHotspot(hotspotId)); hotspotButtons.Add(hotspotId, button);
            hotspotMarkers.Add(hotspotId, marker);
            builtButtons.Add(button);
        }
        // Keyboard/controller navigation is confined to the scene's own controls:
        // geometric auto-navigation can land on hidden scene UI (Preferences or
        // Quick Menu rows stay interactable while inactive), so the hotspots are
        // wired explicitly in authored order instead.
        for (int i = 0; i < builtButtons.Count; i++)
        {
            Navigation navigation = builtButtons[i].navigation; navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnLeft = i > 0 ? builtButtons[i - 1] : null;
            navigation.selectOnUp = i > 0 ? builtButtons[i - 1] : null;
            navigation.selectOnRight = i < builtButtons.Count - 1 ? builtButtons[i + 1] : null;
            navigation.selectOnDown = i < builtButtons.Count - 1 ? builtButtons[i + 1] : null;
            builtButtons[i].navigation = navigation;
        }
    }

    /// <summary>Compact marker per target 07: chip with ring and glyph above an anchor dot, label pill to the right. All parts are raycast-transparent.</summary>
    private MarkerVisuals BuildMarker(GameObject objectRoot, InteractiveHotspotData hotspot)
    {
        MarkerVisuals marker = new MarkerVisuals();
        marker.markerRoot = CreateUiObject(objectRoot.transform, "Marker");
        RectTransform markerRect = marker.markerRoot.GetComponent<RectTransform>();
        markerRect.anchorMin = markerRect.anchorMax = new Vector2(0.5f, 1f); markerRect.pivot = new Vector2(0.5f, 1f); markerRect.sizeDelta = Vector2.zero; markerRect.anchoredPosition = Vector2.zero;
        marker.chipFill = CreateImage(marker.markerRoot.transform, "Chip Fill", GetCircleSprite(), Vector2.zero, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -35f), new Vector2(80f, 80f), ChipFillColor);
        marker.ring = CreateImage(marker.markerRoot.transform, "Chip Ring", GetRingSprite(), Vector2.zero, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -35f), new Vector2(80f, 80f), RingAvailableColor);
        Sprite glyphSprite = GetGlyphSprite(hotspot.iconId);
        if (glyphSprite != null) marker.glyph = CreateImage(marker.markerRoot.transform, "Glyph", glyphSprite, Vector2.zero, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -35f), new Vector2(46f, 46f), GlyphAvailableColor);
        marker.dot = CreateImage(marker.markerRoot.transform, "Anchor Dot", GetCircleSprite(), Vector2.zero, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -105f), new Vector2(20f, 20f), AnchorDotColor);
        marker.labelRoot = CreateUiObject(marker.markerRoot.transform, "Label");
        RectTransform labelRect = marker.labelRoot.GetComponent<RectTransform>();
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, 1f); labelRect.pivot = new Vector2(0f, 0.5f); labelRect.anchoredPosition = new Vector2(37f, -35f); labelRect.sizeDelta = new Vector2(130f, 54f);
        marker.labelFill = CreateImage(marker.labelRoot.transform, "Label Fill", GetPillSprite(), Vector2.zero, Vector2.zero, Vector2.zero, LabelFillColor); Stretch(marker.labelFill.rectTransform); marker.labelFill.type = Image.Type.Sliced;
        marker.labelEdge = CreateImage(marker.labelRoot.transform, "Label Edge", GetStrokeSprite(), Vector2.zero, Vector2.zero, Vector2.zero, MarkerEdgeColor); Stretch(marker.labelEdge.rectTransform, -2f, -2f, -2f, -2f); marker.labelEdge.type = Image.Type.Sliced;
        marker.label = CreateText(marker.labelRoot.transform, "Label Text", hotspot.displayName, 30f, FontStyles.Normal, TextAlignmentOptions.Center, LabelTextColor);
        Stretch(marker.label.rectTransform, 18f, 2f, 14f, 2f); marker.label.enableWordWrapping = false; marker.label.overflowMode = TextOverflowModes.Overflow;
        // Deterministic pill width: TMP preferred-width measurement of Cyrillic labels
        // on the not-yet-activated canvas undershoots and truncates the last glyph.
        float labelWidth = Mathf.Clamp(hotspot.displayName.Length * 18f + 48f, 130f, 340f);
        labelRect.sizeDelta = new Vector2(labelWidth, 54f);
        return marker;
    }

    private void OnDisable() { CleanupWithoutRouting(); }
    private void OnDestroy() { CleanupWithoutRouting(); }
    private void CleanupWithoutRouting()
    {
        if (activeLease == null) return;
        SpecialModeLease lease = activeLease; activeLease = null; activeScene = null; completedHotspotIds.Clear();
        dialogueController?.RestoreReadingAfterInteractiveSceneFeedback();
        dialogueController?.ExitSpecialMode(lease); if (root != null) root.SetActive(false);
        ClearHotspotSelectionIfOwned();
    }

    private sealed class MarkerVisuals
    {
        public Button button;
        public MarkerStateDriver driver;
        public GameObject markerRoot;
        public Image chipFill, ring, glyph, dot, labelFill, labelEdge;
        public TextMeshProUGUI label;
        public GameObject labelRoot;
    }

    /// <summary>Forwards pointer/selection changes so the marker can brighten without Selectable tint compounding.</summary>
    private sealed class MarkerStateDriver : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public InteractiveSceneController owner;
        public string hotspotId;
        public bool pointerOver;
        public void OnSelect(BaseEventData eventData) => owner?.ApplyMarkerState(hotspotId);
        public void OnDeselect(BaseEventData eventData) => owner?.ApplyMarkerState(hotspotId);
        public void OnPointerEnter(PointerEventData eventData) { pointerOver = true; owner?.ApplyMarkerState(hotspotId); }
        public void OnPointerExit(PointerEventData eventData) { pointerOver = false; owner?.ApplyMarkerState(hotspotId); }
    }

    private static GameObject CreateUiObject(Transform parent, string name) { GameObject result = new GameObject(name, typeof(RectTransform)); result.transform.SetParent(parent, false); return result; }
    private static Image CreateImage(Transform parent, string name, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Color color) { GameObject result = CreateUiObject(parent, name); Image image = result.AddComponent<Image>(); image.sprite = sprite; image.raycastTarget = false; image.color = color; RectTransform rect = image.rectTransform; rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = pivot; return image; }
    private static Image CreateImage(Transform parent, string name, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size, Color color)
    { Image image = CreateImage(parent, name, sprite, anchorMin, anchorMax, pivot, color); image.rectTransform.anchoredPosition = position; image.rectTransform.sizeDelta = size; return image; }
    private static void SetAnchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    { rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size; }
    private static TextMeshProUGUI CreateText(Transform parent, string name, string value, float size, FontStyles style, TextAlignmentOptions alignment, Color color)
    {
        GameObject result = CreateUiObject(parent, name); TextMeshProUGUI text = result.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset; text.text = value; text.fontSize = size; text.fontStyle = style; text.alignment = alignment; text.color = color; text.enableWordWrapping = true; text.raycastTarget = false;
        if (fallbackFont == null) fallbackFont = Resources.Load<TMP_FontAsset>(FallbackFontResourcePath);
        if (fallbackFont != null) { text.font = fallbackFont; text.fontSharedMaterial = fallbackFont.material; }
        return text;
    }
    private static void Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); }
    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }

    private static float SdRoundRect(float px, float py, float centerX, float centerY, float halfWidth, float halfHeight, float radius)
    {
        float qx = Mathf.Abs(px - centerX) - (halfWidth - radius); float qy = Mathf.Abs(py - centerY) - (halfHeight - radius);
        float outsideX = Mathf.Max(qx, 0f); float outsideY = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
    }

    private static Sprite BuildMaskSprite(string name, int size, Func<float, float, float> signedDistance)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        Color[] pixels = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - signedDistance(x + 0.5f - half, y + 0.5f - half)));
            }
        }
        texture.SetPixels(pixels); texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect, new Vector4(30f, 30f, 30f, 30f));
        sprite.name = name; sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Sprite GetCircleSprite() => circleSprite != null ? circleSprite : circleSprite = BuildMaskSprite("Runtime Hotspot Chip Circle", 96, (x, y) => Mathf.Sqrt(x * x + y * y) - 46f);

    private static Sprite GetRingSprite()
    {
        if (ringSprite != null) return ringSprite;
        const float ringRadius = 46f, strokeHalfWidth = 2.2f;
        return ringSprite = BuildMaskSprite("Runtime Hotspot Chip Ring", 96, (x, y) =>
        {
            float distance = Mathf.Sqrt(x * x + y * y) - ringRadius;
            float stroke = Mathf.Clamp01(strokeHalfWidth + 0.5f - Mathf.Abs(distance));
            float outerGlow = distance > strokeHalfWidth ? Mathf.Clamp01(1f - (distance - strokeHalfWidth) / 4.5f) * 0.4f : 0f;
            float innerGlow = distance < -strokeHalfWidth ? Mathf.Clamp01(1f - (-distance - strokeHalfWidth) / 5f) * 0.18f : 0f;
            return 0.5f - Mathf.Max(stroke, Mathf.Max(outerGlow, innerGlow));
        });
    }

    private static Sprite GetPillSprite()
    {
        if (pillSprite != null) return pillSprite;
        pillSprite = BuildMaskSprite("Runtime Hotspot Pill", 96, (x, y) => SdRoundRect(x, y, 0f, 0f, 48f, 48f, 44f));
        return pillSprite;
    }

    private static Sprite GetStrokeSprite()
    {
        if (strokeSprite != null) return strokeSprite;
        const float strokeHalfWidth = 0.85f;
        strokeSprite = BuildMaskSprite("Runtime Hotspot Stroke", 96, (x, y) =>
        {
            float distance = SdRoundRect(x, y, 0f, 0f, 48f, 48f, 24f);
            float stroke = Mathf.Clamp01(strokeHalfWidth + 0.5f - Mathf.Abs(distance));
            float outerGlow = distance > strokeHalfWidth ? Mathf.Clamp01(1f - (distance - strokeHalfWidth) / 3.5f) * 0.35f : 0f;
            float innerGlow = distance < -strokeHalfWidth ? Mathf.Clamp01(1f - (-distance - strokeHalfWidth) / 5f) * 0.16f : 0f;
            return 0.5f - Mathf.Max(stroke, Mathf.Max(outerGlow, innerGlow));
        });
        return strokeSprite;
    }

    /// <summary>Simple white line-art glyphs so showcase chips read like target 07; unknown ids keep a clean chip.</summary>
    private static Sprite GetGlyphSprite(string iconId)
    {
        if (string.Equals(iconId, "laptop", StringComparison.OrdinalIgnoreCase)) return laptopGlyphSprite != null ? laptopGlyphSprite : laptopGlyphSprite = BuildGlyphSprite("Runtime Hotspot Laptop Glyph", (x, y) =>
            Mathf.Min(
                Mathf.Abs(SdRoundRect(x, y, 0f, -3f, 15f, 11f, 2f)) - 1.6f,
                SdRoundRect(x, y, 0f, -14f, 20f, 2.2f, 1.5f)));
        if (string.Equals(iconId, "notes", StringComparison.OrdinalIgnoreCase))
        {
            return notesGlyphSprite != null ? notesGlyphSprite : notesGlyphSprite = BuildGlyphSprite("Runtime Hotspot Notes Glyph", (x, y) =>
            {
                float sheet = Mathf.Abs(SdRoundRect(x, y, 0f, 0f, 12.5f, 20f, 2f)) - 1.6f;
                float line1 = SdRoundRect(x, y, 0f, 9f, 7f, 1.3f, 1f);
                float line2 = SdRoundRect(x, y, 0f, 1.5f, 7f, 1.3f, 1f);
                float line3 = SdRoundRect(x, y, 0f, -6f, 7f, 1.3f, 1f);
                return Mathf.Min(sheet, Mathf.Min(line1, Mathf.Min(line2, line3)));
            });
        }
        if (string.Equals(iconId, "door", StringComparison.OrdinalIgnoreCase))
        {
            return doorGlyphSprite != null ? doorGlyphSprite : doorGlyphSprite = BuildGlyphSprite("Runtime Hotspot Door Glyph", (x, y) =>
            {
                float slab = Mathf.Abs(SdRoundRect(x, y, 0f, 0f, 10.5f, 22f, 2f)) - 1.6f;
                float handleX = x - 4.5f, handleY = y + 2f;
                float handle = Mathf.Sqrt(handleX * handleX + handleY * handleY) - 2.6f;
                return Mathf.Min(slab, handle);
            });
        }
        return null;
    }

    private static Sprite BuildGlyphSprite(string name, Func<float, float, float> signedDistance)
    {
        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false, true) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        Color[] pixels = new Color[64 * 64];
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                pixels[y * 64 + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - signedDistance(x + 0.5f - 32f, y + 0.5f - 32f)));
            }
        }
        texture.SetPixels(pixels); texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 64f);
        sprite.name = name; sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Sprite GetRuntimeBackgroundSprite()
    {
        if (runtimeBackgroundSprite != null) return runtimeBackgroundSprite;
        const int width = 320, height = 180; Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "InteractiveHotspotTechnicalRoom" }; Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++) { float shade = Mathf.Lerp(0.035f, 0.11f, y / (float)(height - 1)); for (int x = 0; x < width; x++) { float edge = Mathf.Clamp01(Mathf.Min(x, width - 1 - x, y, height - 1 - y) / 18f); pixels[y * width + x] = new Color(shade * edge, (shade + 0.025f) * edge, (shade + 0.065f) * edge, 1f); } }
        texture.SetPixels(pixels); texture.Apply(false, true); runtimeBackgroundSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), height); runtimeBackgroundSprite.name = "InteractiveHotspotTechnicalRoom"; runtimeBackgroundSprite.hideFlags = HideFlags.HideAndDontSave; return runtimeBackgroundSprite;
    }
}
