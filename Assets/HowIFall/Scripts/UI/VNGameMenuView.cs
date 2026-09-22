using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum VNGameMenuAction
{
    Save,
    Load,
    Preferences,
    History,
    Rollback,
    Characters,
    MainMenu,
    EndReplay,
    Quit,
    Return
}

/// <summary>Runtime-built, scene-local presentation for the gameplay navigation menu (UI Target v1 left glass panel).</summary>
public sealed class VNGameMenuView : MonoBehaviour
{
    // Art-first modal: the scene stays visible behind a light scrim; modal
    // contrast comes from the panel itself, input blocking from the raycast.
    private static readonly Color OverlayColor = new Color(0.005f, 0.012f, 0.025f, 0.20f);
    private static readonly Color AccentColor = new Color(0.30f, 0.58f, 0.80f, 1f);
    // Approved interaction language: selection/focus is cyan, never red.
    private static readonly Color FocusAccentColor = new Color(0.008f, 0.851f, 0.976f, 1f);
    private static readonly Color TaglineDashColor = new Color(0.03f, 0.88f, 0.96f, 0.95f);
    private static readonly Color TaglineTextColor = new Color(0.53f, 0.65f, 0.73f, 0.78f);
    private static readonly Color ChevronColor = new Color(0.62f, 0.76f, 0.88f, 0.85f);
    private static readonly Color EnabledLabelColor = new Color(0.94f, 0.96f, 1f, 1f);
    private static readonly Color DisabledLabelColor = new Color(0.55f, 0.60f, 0.68f, 0.38f);
    private static readonly Color RowPlateColor = new Color(0.55f, 0.70f, 0.90f, 0.05f);
    private static readonly Color ReturnPlateColor = new Color(0.55f, 0.70f, 0.90f, 0.07f);

    // UI Target v1: one cohesive full-height left glass panel whose right edge
    // is the single outer containment edge for Header, Navigation and Footer.
    private const float PanelWidthFraction = 0.258f;
    // Row column insets as panel-width fractions keep the deep typographic
    // left edge and >=22px side insets from 1280x720 up.
    private const float ColumnLeftFraction = 0.197f;
    private const float ColumnRightInsetFraction = 0.075f;
    private const float RowHeight = 62f;
    private const float RowSpacing = 6f;
    private const string TaglineText = "SAME HALLS\nDIFFERENT YOU";

    private readonly Dictionary<VNGameMenuAction, Button> buttons = new Dictionary<VNGameMenuAction, Button>();
    private readonly Dictionary<VNGameMenuAction, TextMeshProUGUI> labels = new Dictionary<VNGameMenuAction, TextMeshProUGUI>();
    private readonly Dictionary<VNGameMenuAction, GameObject> activeMarkers = new Dictionary<VNGameMenuAction, GameObject>();
    private readonly Dictionary<VNGameMenuAction, GameObject> focusMarkers = new Dictionary<VNGameMenuAction, GameObject>();
    private readonly Dictionary<VNGameMenuAction, CanvasGroup> chevronGroups = new Dictionary<VNGameMenuAction, CanvasGroup>();
    private GameObject root;
    private RectTransform saveLoadContentHost;
    private GameObject confirmationRoot;
    private TextMeshProUGUI confirmationText;
    private Button confirmationYesButton;
    private Button confirmationNoButton;

    public bool IsVisible => root != null && root.activeSelf;
    public bool IsConfirmationVisible => confirmationRoot != null && confirmationRoot.activeSelf;
    public Button ConfirmationYesButton => confirmationYesButton;
    public Button ConfirmationNoButton => confirmationNoButton;
    public RectTransform SaveLoadContentHost => saveLoadContentHost;
    public bool IsSaveLoadContentVisible => saveLoadContentHost != null && saveLoadContentHost.gameObject.activeSelf;

    public static VNGameMenuView Create(Transform contextTransform)
    {
        if (contextTransform == null)
        {
            Debug.LogError("[GAME MENU] Cannot create view: context transform is missing.");
            return null;
        }

        Canvas canvas = contextTransform.GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[GAME MENU] Cannot create view: VN Canvas was not found.");
            return null;
        }

        GameObject owner = CreateUiObject(canvas.transform, "VN Game Menu Runtime View");
        owner.layer = canvas.gameObject.layer;
        owner.transform.SetAsLastSibling();
        VNGameMenuView view = owner.AddComponent<VNGameMenuView>();
        view.Build();
        return view;
    }

    public Button GetButton(VNGameMenuAction action)
    {
        return buttons.TryGetValue(action, out Button button) ? button : null;
    }

    public TextMeshProUGUI GetActionLabel(VNGameMenuAction action)
    {
        return labels.TryGetValue(action, out TextMeshProUGUI label) ? label : null;
    }

    public bool IsActionVisible(VNGameMenuAction action)
    {
        Button button = GetButton(action);
        return button != null && button.gameObject.activeSelf;
    }

    public bool IsActionActive(VNGameMenuAction action)
    {
        return activeMarkers.TryGetValue(action, out GameObject marker) && marker.activeSelf;
    }

    public int VisibleFocusMarkerCount => focusMarkers.Values.Count(marker => marker != null && marker.activeInHierarchy);

    public void SetReplayMode(bool replay)
    {
        SetActionVisible(VNGameMenuAction.Save, !replay);
        SetActionVisible(VNGameMenuAction.Load, !replay);
        SetActionVisible(VNGameMenuAction.History, replay);
        SetActionVisible(VNGameMenuAction.Characters, false);
        SetActionVisible(VNGameMenuAction.MainMenu, !replay);
        SetActionVisible(VNGameMenuAction.EndReplay, replay);
    }

    public void SetVisible(bool visible)
    {
        if (root == null)
        {
            return;
        }

        root.SetActive(visible);
        if (visible)
        {
            root.transform.SetAsLastSibling();
            FocusDefaultAction();
            RefreshFocusMarkers();
        }
        else
        {
            HideConfirmation();
            RefreshFocusMarkers();
        }
    }

    /// <summary>
    /// Keeps unavailable actions obviously muted: the Button ColorBlock dims only
    /// the plate, so the label itself must also step down in contrast while no
    /// active or focus state may present an enabled-looking action.
    /// </summary>
    public void RefreshEnabledPresentation()
    {
        foreach (KeyValuePair<VNGameMenuAction, Button> pair in buttons)
        {
            bool interactable = pair.Value != null && pair.Value.interactable;
            if (labels.TryGetValue(pair.Key, out TextMeshProUGUI label) && label != null)
            {
                Color target = interactable ? EnabledLabelColor : DisabledLabelColor;
                if (label.color != target)
                {
                    label.color = target;
                }
            }

            if (chevronGroups.TryGetValue(pair.Key, out CanvasGroup chevron) && chevron != null)
            {
                chevron.alpha = interactable ? 1f : 0.35f;
            }
        }
    }

    public void SetSaveLoadSection(
        VNGameMenuAction? activeAction,
        bool confirmationOpen = false,
        bool operationInProgress = false)
    {
        bool hasSection = activeAction == VNGameMenuAction.Save || activeAction == VNGameMenuAction.Load;
        if (saveLoadContentHost != null)
        {
            saveLoadContentHost.gameObject.SetActive(hasSection);
        }

        foreach (KeyValuePair<VNGameMenuAction, Button> pair in buttons)
        {
            bool isActive = hasSection && pair.Key == activeAction.Value;
            if (activeMarkers.TryGetValue(pair.Key, out GameObject marker))
            {
                marker.SetActive(isActive);
            }

            pair.Value.interactable = !hasSection || (!confirmationOpen && !operationInProgress);
        }

        RefreshEnabledPresentation();
    }

    public void ShowConfirmation(string message)
    {
        if (confirmationRoot == null)
        {
            return;
        }

        confirmationText.text = message ?? string.Empty;
        confirmationRoot.SetActive(true);
        confirmationRoot.transform.SetAsLastSibling();
        FocusConfirmationCancel();
    }

    public void FocusDefaultAction()
    {
        Button fallback = GetButton(VNGameMenuAction.Return);
        EventSystem eventSystem = EventSystem.current ?? FindFirstObjectByType<EventSystem>();
        if (fallback != null && fallback.isActiveAndEnabled && fallback.interactable)
        {
            eventSystem?.SetSelectedGameObject(fallback.gameObject);
        }

        RefreshFocusMarkers();
    }

    private void Update()
    {
        if (root != null && root.activeSelf)
        {
            RefreshEnabledPresentation();
            RefreshFocusMarkers();
        }
    }

    /// <summary>Synchronizes the visual marker with the sole EventSystem selection.</summary>
    public void RefreshFocusMarkers()
    {
        GameObject selected = (EventSystem.current ?? FindFirstObjectByType<EventSystem>())?.currentSelectedGameObject;
        foreach (KeyValuePair<VNGameMenuAction, GameObject> pair in focusMarkers)
        {
            Button button = GetButton(pair.Key);
            bool focused = root != null
                && root.activeSelf
                && button != null
                && button.interactable
                && button.gameObject.activeInHierarchy
                && selected == button.gameObject;
            if (pair.Value != null && pair.Value.activeSelf != focused)
            {
                pair.Value.SetActive(focused);
            }
        }
    }

    public void HideConfirmation()
    {
        confirmationRoot?.SetActive(false);
    }

    private void Build()
    {
        root = gameObject;
        RectTransform rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);
        Image dim = root.AddComponent<Image>();
        dim.color = OverlayColor;
        dim.raycastTarget = true;

        GameObject window = CreateUiObject(root.transform, "Game Menu Window");
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = Vector2.zero;
        windowRect.anchorMax = new Vector2(PanelWidthFraction, 1f);
        windowRect.offsetMin = Vector2.zero;
        windowRect.offsetMax = Vector2.zero;
        Image panel = window.AddComponent<Image>();
        panel.sprite = CreateNavyPanelSprite();
        panel.raycastTarget = true;

        CreateHeader(window.transform);
        CreateNavigation(window.transform);
        CreateFooter(window.transform);
        CreateSaveLoadContentHost(root.transform);
        CreateConfirmation(root.transform);
        root.SetActive(false);
    }

    private void CreateSaveLoadContentHost(Transform root)
    {
        GameObject host = CreateUiObject(root, "Save Load Content Host");
        saveLoadContentHost = host.GetComponent<RectTransform>();
        saveLoadContentHost.anchorMin = new Vector2(0.295f, 0.055f);
        saveLoadContentHost.anchorMax = new Vector2(0.955f, 0.945f);
        saveLoadContentHost.offsetMin = Vector2.zero;
        saveLoadContentHost.offsetMax = Vector2.zero;
        host.AddComponent<RectMask2D>();
        host.SetActive(false);
    }

    private void CreateHeader(Transform window)
    {
        GameObject header = CreateUiObject(window, "Header");
        RectTransform rect = header.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.63f);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // The authored logo sprite is Main-Menu-scene-bound, so the panel opens
        // with the typographic brand block instead of reproducing the target's
        // generated logo artwork (TECH DEMO ONLY / NOT CANON per the target doc).
        TextMeshProUGUI wordmark = CreateText(header.transform, "Wordmark", "HOW I\nFALL", 50f, FontStyles.Bold | FontStyles.Italic, TextAlignmentOptions.TopLeft, EnabledLabelColor);
        wordmark.characterSpacing = 4f;
        wordmark.lineSpacing = 62f;
        AnchorTopLeft(wordmark.rectTransform, 0.20f, -40f, new Vector2(360f, 150f));

        CreateTaglineBlock(header.transform, 0.20f, -296f, -312f, 17f);
    }

    private void CreateFooter(Transform window)
    {
        GameObject footer = CreateUiObject(window, "Footer");
        RectTransform rect = footer.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.015f);
        rect.anchorMax = new Vector2(1f, 0.10f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        CreateTaglineBlock(footer.transform, ColumnLeftFraction, -6f, -22f, 16f);
    }

    private static void CreateTaglineBlock(Transform parent, float xFraction, float dashTop, float textTop, float fontSize)
    {
        GameObject dash = CreateSurface(parent, "Tagline Dash", TaglineDashColor);
        dash.GetComponent<Image>().raycastTarget = false;
        AnchorTopLeft(dash.GetComponent<RectTransform>(), xFraction, dashTop, new Vector2(46f, 4f));

        TextMeshProUGUI tagline = CreateText(parent, "Tagline", TaglineText, fontSize, FontStyles.Normal, TextAlignmentOptions.TopLeft, TaglineTextColor);
        tagline.characterSpacing = 22f;
        tagline.lineSpacing = 113f;
        AnchorTopLeft(tagline.rectTransform, xFraction, textTop, new Vector2(340f, 60f));
    }

    private void CreateNavigation(Transform window)
    {
        GameObject navigation = CreateUiObject(window, "Navigation");
        RectTransform rect = navigation.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.045f);
        rect.anchorMax = new Vector2(1f, 0.63f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        GameObject primaryActions = CreateUiObject(navigation.transform, "Primary Actions");
        RectTransform primaryRect = primaryActions.GetComponent<RectTransform>();
        primaryRect.anchorMin = new Vector2(ColumnLeftFraction, 0.335f);
        primaryRect.anchorMax = new Vector2(1f - ColumnRightInsetFraction, 1f);
        primaryRect.offsetMin = Vector2.zero;
        primaryRect.offsetMax = new Vector2(0f, -16f);

        VerticalLayoutGroup layout = primaryActions.AddComponent<VerticalLayoutGroup>();
        layout.spacing = RowSpacing;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateActionButton(primaryActions.transform, VNGameMenuAction.Save, "Сохранить");
        CreateActionButton(primaryActions.transform, VNGameMenuAction.Load, "Загрузить");
        CreateActionButton(primaryActions.transform, VNGameMenuAction.Preferences, "Настройки");
        CreateActionButton(primaryActions.transform, VNGameMenuAction.History, "История");
        CreateActionButton(primaryActions.transform, VNGameMenuAction.Characters, "Персонажи");
        CreateActionButton(primaryActions.transform, VNGameMenuAction.MainMenu, "Главное меню");
        CreateActionButton(primaryActions.transform, VNGameMenuAction.EndReplay, "Завершить повтор");
        CreateActionButton(primaryActions.transform, VNGameMenuAction.Quit, "Выйти");

        GameObject returnArea = CreateUiObject(navigation.transform, "Return Area");
        RectTransform returnAreaRect = returnArea.GetComponent<RectTransform>();
        returnAreaRect.anchorMin = new Vector2(ColumnLeftFraction, 0.145f);
        returnAreaRect.anchorMax = new Vector2(1f - ColumnRightInsetFraction, 0.31f);
        returnAreaRect.offsetMin = new Vector2(0f, 4f);
        returnAreaRect.offsetMax = new Vector2(0f, -6f);

        GameObject separator = CreateSurface(returnArea.transform, "Separator", new Color(0.30f, 0.58f, 0.80f, 0.45f));
        separator.GetComponent<Image>().raycastTarget = false;
        RectTransform separatorRect = separator.GetComponent<RectTransform>();
        separatorRect.anchorMin = new Vector2(0f, 1f);
        separatorRect.anchorMax = Vector2.one;
        separatorRect.pivot = new Vector2(0.5f, 1f);
        separatorRect.sizeDelta = new Vector2(0f, 2f);

        CreateActionButton(returnArea.transform, VNGameMenuAction.Return, "Вернуться в игру");
        Button returnButton = buttons[VNGameMenuAction.Return];
        returnButton.GetComponent<Image>().color = ReturnPlateColor;
        RectTransform returnRect = returnButton.GetComponent<RectTransform>();
        returnRect.anchorMin = new Vector2(0f, 0f);
        returnRect.anchorMax = new Vector2(1f, 0.66f);
        returnRect.offsetMin = Vector2.zero;
        returnRect.offsetMax = Vector2.zero;

        labels[VNGameMenuAction.Return].fontSize = 21f;
        Stretch(labels[VNGameMenuAction.Return].rectTransform, 46f, 12f, 0f, 0f);

        GameObject playIcon = CreateUiObject(returnButton.transform, "Play Icon");
        Image playIconImage = playIcon.AddComponent<Image>();
        playIconImage.sprite = CreatePlayIconSprite();
        playIconImage.color = new Color(0.56f, 0.76f, 0.96f, 0.95f);
        playIconImage.raycastTarget = false;
        RectTransform playIconRect = playIcon.GetComponent<RectTransform>();
        playIconRect.anchorMin = playIconRect.anchorMax = new Vector2(0f, 0.5f);
        playIconRect.pivot = new Vector2(0f, 0.5f);
        playIconRect.anchoredPosition = new Vector2(16f, 0f);
        playIconRect.sizeDelta = new Vector2(15f, 15f);
    }

    private void CreateConfirmation(Transform parent)
    {
        confirmationRoot = CreateSurface(parent, "Game Menu Confirmation", new Color(0f, 0f, 0f, 0.76f));
        Stretch(confirmationRoot.GetComponent<RectTransform>());
        confirmationRoot.GetComponent<Image>().raycastTarget = true;

        GameObject window = CreateSurface(confirmationRoot.transform, "Confirmation Window", new Color(0.012f, 0.022f, 0.035f, 0.98f));
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(620f, 250f);
        Outline outline = window.AddComponent<Outline>();
        outline.effectColor = new Color(0.30f, 0.58f, 0.80f, 0.42f);
        outline.effectDistance = new Vector2(1f, -1f);

        confirmationText = CreateText(window.transform, "Prompt", string.Empty, 22f, FontStyles.Normal, TextAlignmentOptions.Center, Color.white);
        confirmationText.rectTransform.anchorMin = new Vector2(0.08f, 0.40f);
        confirmationText.rectTransform.anchorMax = new Vector2(0.92f, 0.82f);
        confirmationText.rectTransform.offsetMin = Vector2.zero;
        confirmationText.rectTransform.offsetMax = Vector2.zero;

        GameObject accent = CreateSurface(window.transform, "Confirmation Accent", AccentColor);
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = Vector2.one;
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.sizeDelta = new Vector2(0f, 3f);
        accent.GetComponent<Image>().raycastTarget = false;

        confirmationYesButton = CreateConfirmationButton(window.transform, "Да", new Vector2(0.36f, 0.20f), true);
        confirmationNoButton = CreateConfirmationButton(window.transform, "Нет", new Vector2(0.64f, 0.20f), false);
        Button[] confirmationButtons = { confirmationYesButton, confirmationNoButton };
        confirmationYesButton.GetComponent<MainMenuButtonHoverEffect>().ConfigureExclusiveActions(confirmationButtons);
        confirmationNoButton.GetComponent<MainMenuButtonHoverEffect>().ConfigureExclusiveActions(confirmationButtons);
        confirmationRoot.SetActive(false);
    }

    private void CreateActionButton(Transform parent, VNGameMenuAction action, string label)
    {
        GameObject buttonObject = CreateSurface(parent, action + " Button", RowPlateColor);
        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredHeight = RowHeight;
        layout.minHeight = 42f;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        button.colors = CreateButtonColors();

        TextMeshProUGUI text = CreateText(buttonObject.transform, "Label", label, 23f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, EnabledLabelColor);
        Stretch(text.rectTransform, 30f, 44f, 0f, 0f);

        GameObject activeMarker = CreateSurface(buttonObject.transform, "Active Marker", AccentColor);
        activeMarker.GetComponent<Image>().raycastTarget = false;
        RectTransform markerRect = activeMarker.GetComponent<RectTransform>();
        markerRect.anchorMin = Vector2.zero;
        markerRect.anchorMax = new Vector2(0f, 1f);
        markerRect.pivot = new Vector2(0f, 0.5f);
        markerRect.sizeDelta = new Vector2(5f, 0f);
        activeMarker.SetActive(false);

        GameObject focusMarker = CreateSurface(buttonObject.transform, "Focus Marker", FocusAccentColor);
        focusMarker.GetComponent<Image>().raycastTarget = false;
        RectTransform focusMarkerRect = focusMarker.GetComponent<RectTransform>();
        focusMarkerRect.anchorMin = Vector2.zero;
        focusMarkerRect.anchorMax = new Vector2(0f, 1f);
        focusMarkerRect.pivot = new Vector2(0f, 0.5f);
        focusMarkerRect.sizeDelta = new Vector2(6f, 0f);
        focusMarker.SetActive(false);
        focusMarkers[action] = focusMarker;
        AddFocusMarkerEvents(buttonObject);

        chevronGroups[action] = CreateChevron(buttonObject.transform);

        buttons[action] = button;
        labels[action] = text;
        activeMarkers[action] = activeMarker;
    }

    private static CanvasGroup CreateChevron(Transform buttonTransform)
    {
        GameObject chevron = CreateUiObject(buttonTransform, "Chevron");
        RectTransform chevronRect = chevron.GetComponent<RectTransform>();
        chevronRect.anchorMin = chevronRect.anchorMax = new Vector2(1f, 0.5f);
        chevronRect.pivot = new Vector2(1f, 0.5f);
        chevronRect.anchoredPosition = new Vector2(-18f, 0f);
        chevronRect.sizeDelta = new Vector2(9f, 14f);
        CreateChevronStroke(chevron.transform, 1f);
        CreateChevronStroke(chevron.transform, -1f);
        return chevron.AddComponent<CanvasGroup>();
    }

    private static void CreateChevronStroke(Transform parent, float sign)
    {
        GameObject stroke = CreateUiObject(parent, "Stroke");
        Image image = stroke.AddComponent<Image>();
        image.color = ChevronColor;
        image.raycastTarget = false;
        RectTransform rect = stroke.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(2.5f, 8.5f);
        rect.anchoredPosition = new Vector2(-1.75f, sign * 2.9f);
        rect.localRotation = Quaternion.Euler(0f, 0f, sign * 45f);
    }

    private void AddFocusMarkerEvents(GameObject buttonObject)
    {
        EventTrigger trigger = buttonObject.AddComponent<EventTrigger>();
        trigger.triggers = new List<EventTrigger.Entry>();
        AddFocusMarkerEvent(trigger, EventTriggerType.Select, RefreshFocusMarkers);
        AddFocusMarkerEvent(trigger, EventTriggerType.Deselect, RefreshFocusMarkers);
    }

    private static void AddFocusMarkerEvent(EventTrigger trigger, EventTriggerType eventType, UnityEngine.Events.UnityAction action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    private static Button CreateConfirmationButton(Transform parent, string label, Vector2 anchor, bool destructive)
    {
        GameObject buttonObject = CreateSurface(parent, label + " Button", Color.clear);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(164f, 46f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        button.transition = Selectable.Transition.None;
        TextMeshProUGUI text = CreateText(buttonObject.transform, "Label", label, 19f, FontStyles.Normal, TextAlignmentOptions.Center, Color.white);
        Stretch(text.rectTransform);
        MainMenuButtonHoverEffect effect = buttonObject.AddComponent<MainMenuButtonHoverEffect>();
        effect.highlightImage = buttonObject.GetComponent<Image>();
        effect.useRedFocusText = destructive;
        effect.suppressFocusAccent = destructive;
        effect.Configure(destructive ? MainMenuButtonVisualRole.Destructive : MainMenuButtonVisualRole.Secondary);
        return button;
    }

    private void FocusConfirmationCancel()
    {
        EventSystem eventSystem = EventSystem.current ?? FindFirstObjectByType<EventSystem>();
        if (confirmationNoButton != null && eventSystem != null)
        {
            eventSystem.SetSelectedGameObject(confirmationNoButton.gameObject);
        }
    }

    private void SetActionVisible(VNGameMenuAction action, bool visible)
    {
        Button button = GetButton(action);
        if (button != null && button.gameObject.activeSelf != visible)
        {
            button.gameObject.SetActive(visible);
        }
    }

    private static GameObject CreateUiObject(Transform parent, string name)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static GameObject CreateSurface(Transform parent, string name, Color color)
    {
        GameObject result = CreateUiObject(parent, name);
        Image image = result.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return result;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        string value,
        float size,
        FontStyles style,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject textObject = CreateUiObject(parent, name);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static void AnchorTopLeft(RectTransform rect, float xFraction, float topOffset, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(xFraction, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(0f, topOffset);
        rect.sizeDelta = size;
    }

    private static Texture2D navyPanelTexture;

    /// <summary>
    /// UI Target v1 glass column ramp, built once: deep navy #0A1626 with a
    /// gentle left-to-right alpha falloff (0.94 → 0.78) so the panel reads as
    /// translucent glass while keeping its column readable over bright art.
    /// </summary>
    private static Sprite CreateNavyPanelSprite()
    {
        if (navyPanelTexture == null)
        {
            const int width = 256;
            const int height = 4;
            navyPanelTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            navyPanelTexture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[width * height];
            for (int x = 0; x < width; x++)
            {
                float t = x / (width - 1f);
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Lerp(247f, 230f, t));
                for (int y = 0; y < height; y++)
                {
                    // The texture is created in linear space, so the intended
                    // sRGB navy #0A1626 (10, 22, 38) is pre-converted with an
                    // inverse-gamma pow(2.2); storing the sRGB bytes directly
                    // would render as a washed-out light slate blue.
                    pixels[y * width + x] = new Color32(0, 3, 8, alpha);
                }
            }

            navyPanelTexture.SetPixels32(pixels);
            navyPanelTexture.Apply(false, true);
        }

        Sprite sprite = Sprite.Create(navyPanelTexture, new Rect(0f, 0f, 256f, 4f), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "HIF Navy Panel Runtime";
        return sprite;
    }

    private static Texture2D playIconTexture;

    /// <summary>Right-pointing triangle for the integrated Return action, built once at runtime.</summary>
    private static Sprite CreatePlayIconSprite()
    {
        if (playIconTexture == null)
        {
            const int size = 24;
            playIconTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float halfHeight = (x - 4f) * (9f / 16f);
                    bool inside = x >= 4 && x <= 20 && Mathf.Abs(y - 11.5f) <= halfHeight + 0.5f;
                    pixels[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }

            playIconTexture.SetPixels32(pixels);
            playIconTexture.Apply(false, true);
        }

        Sprite sprite = Sprite.Create(playIconTexture, new Rect(0f, 0f, 24f, 24f), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "HIF Play Icon Runtime";
        return sprite;
    }

    private static ColorBlock CreateButtonColors()
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white;
        // Multipliers above 1 let a quiet translucent plate brighten into a
        // clearly visible hover/focus plate while staying calm at rest.
        colors.highlightedColor = new Color(1.6f, 1.8f, 2.1f, 2.2f);
        colors.pressedColor = new Color(1.2f, 1.35f, 1.55f, 1.9f);
        colors.selectedColor = new Color(1.5f, 1.7f, 2.0f, 2.2f);
        colors.disabledColor = new Color(0.45f, 0.50f, 0.60f, 1.0f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        return colors;
    }

    private static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float bottom = 0f, float top = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
