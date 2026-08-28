using System.Collections.Generic;
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
    Characters,
    MainMenu,
    EndReplay,
    Quit,
    Return
}

/// <summary>Runtime-built, scene-local presentation for the gameplay navigation menu.</summary>
public sealed class VNGameMenuView : MonoBehaviour
{
    private static readonly Color OverlayColor = new Color(0.005f, 0.012f, 0.025f, 0.94f);
    private static readonly Color LeftWashColor = new Color(0.008f, 0.024f, 0.050f, 0.76f);
    private static readonly Color NavigationColor = new Color(0.025f, 0.060f, 0.105f, 0.94f);
    private static readonly Color AccentColor = new Color(0.30f, 0.58f, 0.80f, 1f);

    private readonly Dictionary<VNGameMenuAction, Button> buttons = new Dictionary<VNGameMenuAction, Button>();
    private readonly Dictionary<VNGameMenuAction, GameObject> activeMarkers = new Dictionary<VNGameMenuAction, GameObject>();
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

    public bool IsActionVisible(VNGameMenuAction action)
    {
        Button button = GetButton(action);
        return button != null && button.gameObject.activeSelf;
    }

    public bool IsActionActive(VNGameMenuAction action)
    {
        return activeMarkers.TryGetValue(action, out GameObject marker) && marker.activeSelf;
    }

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
        }
        else
        {
            HideConfirmation();
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

        GameObject leftWash = CreateSurface(root.transform, "Left Background Wash", LeftWashColor);
        RectTransform leftWashRect = leftWash.GetComponent<RectTransform>();
        leftWashRect.anchorMin = Vector2.zero;
        leftWashRect.anchorMax = new Vector2(0.38f, 1f);
        leftWashRect.offsetMin = Vector2.zero;
        leftWashRect.offsetMax = Vector2.zero;

        GameObject window = CreateUiObject(root.transform, "Game Menu Window");
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.045f, 0.05f);
        windowRect.anchorMax = new Vector2(0.955f, 0.95f);
        windowRect.offsetMin = Vector2.zero;
        windowRect.offsetMax = Vector2.zero;

        CreateHeader(window.transform);
        CreateNavigation(window.transform);
        CreateSaveLoadContentHost(window.transform);
        CreateConfirmation(root.transform);
        root.SetActive(false);
    }

    private void CreateSaveLoadContentHost(Transform window)
    {
        GameObject host = CreateUiObject(window, "Save Load Content Host");
        saveLoadContentHost = host.GetComponent<RectTransform>();
        saveLoadContentHost.anchorMin = new Vector2(0.31f, 0.02f);
        saveLoadContentHost.anchorMax = new Vector2(1f, 0.98f);
        saveLoadContentHost.offsetMin = Vector2.zero;
        saveLoadContentHost.offsetMax = Vector2.zero;
        host.AddComponent<RectMask2D>();
        host.SetActive(false);
    }

    private void CreateHeader(Transform window)
    {
        GameObject header = CreateSurface(window, "Header", new Color(0.018f, 0.040f, 0.078f, 0.94f));
        RectTransform rect = header.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.86f);
        rect.anchorMax = new Vector2(0.29f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI title = CreateText(header.transform, "Title", "ИГРОВОЕ МЕНЮ", 28f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, Color.white);
        Stretch(title.rectTransform, 24f, 24f, 0f, 0f);

        GameObject accent = CreateSurface(header.transform, "Accent", AccentColor);
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = Vector2.zero;
        accentRect.anchorMax = new Vector2(1f, 0f);
        accentRect.pivot = new Vector2(0.5f, 0f);
        accentRect.sizeDelta = new Vector2(0f, 3f);
    }

    private void CreateNavigation(Transform window)
    {
        GameObject navigation = CreateSurface(window, "Navigation", NavigationColor);
        RectTransform rect = navigation.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.06f);
        rect.anchorMax = new Vector2(0.29f, 0.84f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Outline outline = navigation.AddComponent<Outline>();
        outline.effectColor = new Color(0.45f, 0.56f, 0.70f, 0.22f);
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject primaryActions = CreateUiObject(navigation.transform, "Primary Actions");
        RectTransform primaryRect = primaryActions.GetComponent<RectTransform>();
        primaryRect.anchorMin = new Vector2(0f, 0.22f);
        primaryRect.anchorMax = Vector2.one;
        primaryRect.offsetMin = new Vector2(20f, 18f);
        primaryRect.offsetMax = new Vector2(-20f, -20f);

        VerticalLayoutGroup layout = primaryActions.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 10f;
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
        returnAreaRect.anchorMin = Vector2.zero;
        returnAreaRect.anchorMax = new Vector2(1f, 0.19f);
        returnAreaRect.offsetMin = new Vector2(20f, 18f);
        returnAreaRect.offsetMax = new Vector2(-20f, -8f);

        GameObject separator = CreateSurface(returnArea.transform, "Separator", new Color(0.30f, 0.58f, 0.80f, 0.62f));
        RectTransform separatorRect = separator.GetComponent<RectTransform>();
        separatorRect.anchorMin = new Vector2(0f, 1f);
        separatorRect.anchorMax = Vector2.one;
        separatorRect.pivot = new Vector2(0.5f, 1f);
        separatorRect.sizeDelta = new Vector2(0f, 2f);

        CreateActionButton(returnArea.transform, VNGameMenuAction.Return, "Вернуться");
        RectTransform returnRect = buttons[VNGameMenuAction.Return].GetComponent<RectTransform>();
        returnRect.anchorMin = new Vector2(0f, 0f);
        returnRect.anchorMax = new Vector2(1f, 0.72f);
        returnRect.offsetMin = Vector2.zero;
        returnRect.offsetMax = Vector2.zero;
    }

    private void CreateConfirmation(Transform parent)
    {
        confirmationRoot = CreateSurface(parent, "Game Menu Confirmation", new Color(0f, 0f, 0f, 0.76f));
        Stretch(confirmationRoot.GetComponent<RectTransform>());
        confirmationRoot.GetComponent<Image>().raycastTarget = true;

        GameObject window = CreateSurface(confirmationRoot.transform, "Confirmation Window", new Color(0.025f, 0.055f, 0.095f, 1f));
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(620f, 264f);
        Outline outline = window.AddComponent<Outline>();
        outline.effectColor = new Color(0.30f, 0.58f, 0.80f, 0.72f);
        outline.effectDistance = new Vector2(2f, -2f);

        confirmationText = CreateText(window.transform, "Prompt", string.Empty, 22f, FontStyles.Normal, TextAlignmentOptions.Center, Color.white);
        confirmationText.rectTransform.anchorMin = new Vector2(0f, 0.40f);
        confirmationText.rectTransform.anchorMax = Vector2.one;
        confirmationText.rectTransform.offsetMin = new Vector2(34f, 0f);
        confirmationText.rectTransform.offsetMax = new Vector2(-34f, -26f);

        GameObject accent = CreateSurface(window.transform, "Confirmation Accent", AccentColor);
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = Vector2.one;
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.sizeDelta = new Vector2(0f, 3f);
        accent.GetComponent<Image>().raycastTarget = false;

        confirmationYesButton = CreateConfirmationButton(window.transform, "Да", new Vector2(0.35f, 0.20f), true);
        confirmationNoButton = CreateConfirmationButton(window.transform, "Нет", new Vector2(0.65f, 0.20f), false);
        confirmationRoot.SetActive(false);
    }

    private void CreateActionButton(Transform parent, VNGameMenuAction action, string label)
    {
        GameObject buttonObject = CreateSurface(parent, action + " Button", new Color(0.045f, 0.095f, 0.145f, 0.94f));
        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 50f;
        layout.minHeight = 42f;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        button.colors = CreateButtonColors();
        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.34f, 0.45f, 0.56f, 0.24f);
        outline.effectDistance = new Vector2(1f, -1f);

        TextMeshProUGUI text = CreateText(buttonObject.transform, "Label", label, 19f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, Color.white);
        Stretch(text.rectTransform, 18f, 18f, 0f, 0f);
        GameObject activeMarker = CreateSurface(buttonObject.transform, "Active Marker", AccentColor);
        RectTransform markerRect = activeMarker.GetComponent<RectTransform>();
        markerRect.anchorMin = Vector2.zero;
        markerRect.anchorMax = new Vector2(0f, 1f);
        markerRect.pivot = new Vector2(0f, 0.5f);
        markerRect.sizeDelta = new Vector2(5f, 0f);
        activeMarker.SetActive(false);

        GameObject focusMarker = CreateSurface(buttonObject.transform, "Focus Marker", new Color(0.84f, 0.18f, 0.22f, 1f));
        RectTransform focusMarkerRect = focusMarker.GetComponent<RectTransform>();
        focusMarkerRect.anchorMin = new Vector2(1f, 0f);
        focusMarkerRect.anchorMax = Vector2.one;
        focusMarkerRect.pivot = new Vector2(1f, 0.5f);
        focusMarkerRect.sizeDelta = new Vector2(5f, 0f);
        focusMarker.SetActive(false);
        AddFocusMarkerEvents(buttonObject, focusMarker);

        buttons[action] = button;
        activeMarkers[action] = activeMarker;
    }

    private static void AddFocusMarkerEvents(GameObject buttonObject, GameObject focusMarker)
    {
        EventTrigger trigger = buttonObject.AddComponent<EventTrigger>();
        trigger.triggers = new List<EventTrigger.Entry>();
        AddFocusMarkerEvent(trigger, EventTriggerType.Select, () => focusMarker.SetActive(true));
        AddFocusMarkerEvent(trigger, EventTriggerType.Deselect, () => focusMarker.SetActive(false));
    }

    private static void AddFocusMarkerEvent(EventTrigger trigger, EventTriggerType eventType, UnityEngine.Events.UnityAction action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    private static Button CreateConfirmationButton(Transform parent, string label, Vector2 anchor, bool destructive)
    {
        GameObject buttonObject = CreateSurface(
            parent,
            label + " Button",
            destructive ? new Color(0.34f, 0.075f, 0.105f, 1f) : new Color(0.075f, 0.11f, 0.17f, 1f));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.sizeDelta = new Vector2(150f, 48f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        button.colors = CreateButtonColors();
        TextMeshProUGUI text = CreateText(buttonObject.transform, "Label", label, 19f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        Stretch(text.rectTransform);
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

    private static ColorBlock CreateButtonColors()
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.80f, 0.91f, 1f, 1f);
        colors.pressedColor = new Color(0.52f, 0.72f, 0.88f, 1f);
        colors.selectedColor = new Color(0.72f, 0.86f, 0.98f, 1f);
        colors.disabledColor = new Color(0.45f, 0.48f, 0.52f, 0.72f);
        colors.colorMultiplier = 1f;
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
