using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class VNPrototypeBacklogUiBuilder
{
    private const string VNPrototypeScenePath = "Assets/HowIFall/Scenes/VNPrototype.unity";

    [MenuItem("How I Fall/Build VN Backlog UI")]
    public static void BuildBacklogUi()
    {
        var scene = EditorSceneManager.OpenScene(VNPrototypeScenePath);
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        VNDialogueController controller = Object.FindFirstObjectByType<VNDialogueController>();

        if (canvas == null)
        {
            Debug.LogError("VNPrototypeBacklogUiBuilder: Canvas was not found in VNPrototype.");
            return;
        }

        if (controller == null)
        {
            Debug.LogError("VNPrototypeBacklogUiBuilder: VNDialogueController was not found in VNPrototype.");
            return;
        }

        Transform existingPanel = canvas.transform.Find("Backlog Panel");

        if (existingPanel != null)
        {
            Object.DestroyImmediate(existingPanel.gameObject);
        }

        Transform existingButton = canvas.transform.Find("Backlog Button");

        if (existingButton != null)
        {
            Object.DestroyImmediate(existingButton.gameObject);
        }

        Transform existingQuickMenu = canvas.transform.Find("VN Quick Menu");

        if (existingQuickMenu != null)
        {
            Object.DestroyImmediate(existingQuickMenu.gameObject);
        }

        Transform existingNotificationPanel = canvas.transform.Find("Notification Panel");

        if (existingNotificationPanel != null)
        {
            Object.DestroyImmediate(existingNotificationPanel.gameObject);
        }

        Transform existingConfirmExitPanel = canvas.transform.Find("Confirm Exit Panel");

        if (existingConfirmExitPanel != null)
        {
            Object.DestroyImmediate(existingConfirmExitPanel.gameObject);
        }

        Transform existingSettingsPanel = canvas.transform.Find("VN Settings Panel");

        if (existingSettingsPanel != null)
        {
            Object.DestroyImmediate(existingSettingsPanel.gameObject);
        }

        CreateQuickMenu(canvas.transform, controller);
        GameObject notificationPanel = CreateNotificationPanel(canvas.transform, out TextMeshProUGUI notificationText);
        notificationPanel.SetActive(false);

        GameObject settingsPanel = CreateSettingsPanel(
            canvas.transform,
            out Slider masterVolumeSlider,
            out Slider musicVolumeSlider,
            out Slider sfxVolumeSlider,
            out Slider textSpeedSlider,
            out Toggle fullscreenToggle,
            out Button settingsCloseButton,
            out Button settingsResetButton);
        settingsPanel.SetActive(false);

        GameObject confirmExitPanel = CreateConfirmExitPanel(
            canvas.transform,
            out Button confirmExitYesButton,
            out Button confirmExitNoButton);
        confirmExitPanel.SetActive(false);

        GameObject backlogPanel = CreateBacklogPanel(canvas.transform, out TextMeshProUGUI backlogText, out Button closeButton);
        backlogPanel.transform.SetAsLastSibling();
        backlogPanel.SetActive(false);

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("backlogPanel").objectReferenceValue = backlogPanel;
        serializedController.FindProperty("backlogText").objectReferenceValue = backlogText;
        serializedController.FindProperty("backlogCloseButton").objectReferenceValue = closeButton;
        serializedController.FindProperty("notificationPanel").objectReferenceValue = notificationPanel;
        serializedController.FindProperty("notificationText").objectReferenceValue = notificationText;
        serializedController.FindProperty("confirmExitPanel").objectReferenceValue = confirmExitPanel;
        serializedController.FindProperty("confirmExitYesButton").objectReferenceValue = confirmExitYesButton;
        serializedController.FindProperty("confirmExitNoButton").objectReferenceValue = confirmExitNoButton;
        serializedController.FindProperty("vnSettingsPanel").objectReferenceValue = settingsPanel;
        serializedController.FindProperty("vnMasterVolumeSlider").objectReferenceValue = masterVolumeSlider;
        serializedController.FindProperty("vnMusicVolumeSlider").objectReferenceValue = musicVolumeSlider;
        serializedController.FindProperty("vnSfxVolumeSlider").objectReferenceValue = sfxVolumeSlider;
        serializedController.FindProperty("vnTextSpeedSlider").objectReferenceValue = textSpeedSlider;
        serializedController.FindProperty("vnFullscreenToggle").objectReferenceValue = fullscreenToggle;
        serializedController.FindProperty("vnSettingsCloseButton").objectReferenceValue = settingsCloseButton;
        serializedController.FindProperty("vnSettingsResetButton").objectReferenceValue = settingsResetButton;
        serializedController.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("VNPrototype backlog UI was created and assigned.");
    }

    public static void BuildBacklogUiBatch()
    {
        BuildBacklogUi();
    }

    private static GameObject CreateBacklogPanel(Transform parent, out TextMeshProUGUI backlogText, out Button closeButton)
    {
        GameObject panel = CreateUiObject("Backlog Panel", parent);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        StretchToParent(panelRect);

        Image dimImage = panel.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.75f);
        dimImage.raycastTarget = true;

        GameObject window = CreateUiObject("Backlog Window", panel.transform);
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.anchoredPosition = Vector2.zero;
        windowRect.sizeDelta = new Vector2(1200f, 760f);

        Image windowImage = window.AddComponent<Image>();
        windowImage.color = new Color(0.025f, 0.018f, 0.045f, 0.9f);
        windowImage.raycastTarget = true;

        Shadow shadow = window.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(5f, -5f);

        CreateAccentLine(window.transform);
        CreateTitle(window.transform);
        CreateScrollView(window.transform, out backlogText);
        closeButton = CreateCloseButton(window.transform);

        return panel;
    }

    private static void CreateQuickMenu(Transform parent, VNDialogueController controller)
    {
        GameObject menu = CreateUiObject("VN Quick Menu", parent);
        RectTransform rect = menu.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-36f, -28f);
        rect.sizeDelta = new Vector2(590f, 48f);

        Image background = menu.AddComponent<Image>();
        background.color = new Color(0.02f, 0.014f, 0.04f, 0.22f);
        background.raycastTarget = false;

        HorizontalLayoutGroup layout = menu.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 9f;
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        Button saveButton = CreateQuickMenuButton(menu.transform, "Быстр. сохр.", 110f);
        UnityEventTools.AddPersistentListener(saveButton.onClick, controller.SaveGame);

        Button loadButton = CreateQuickMenuButton(menu.transform, "Быстр. загр.", 110f);
        UnityEventTools.AddPersistentListener(loadButton.onClick, controller.LoadGame);

        Button backlogButton = CreateQuickMenuButton(menu.transform, "История", 100f);
        UnityEventTools.AddPersistentListener(backlogButton.onClick, controller.ShowBacklog);

        Button settingsButton = CreateQuickMenuButton(menu.transform, "Настройки", 120f);
        UnityEventTools.AddPersistentListener(settingsButton.onClick, controller.OpenSettings);

        Button menuButton = CreateQuickMenuButton(menu.transform, "Меню", 90f);
        UnityEventTools.AddPersistentListener(menuButton.onClick, controller.ShowConfirmExit);
    }

    private static Button CreateQuickMenuButton(Transform parent, string labelText, float width)
    {
        GameObject buttonGo = CreateUiObject($"{labelText} Button", parent);
        RectTransform rect = buttonGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, 42f);

        Image image = buttonGo.AddComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = true;

        Button button = buttonGo.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.04f, 0.025f, 0.07f, 0.58f);
        colors.highlightedColor = new Color(0.38f, 0.18f, 0.5f, 0.78f);
        colors.pressedColor = new Color(0.5f, 0.22f, 0.65f, 0.9f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.02f, 0.02f, 0.03f, 0.35f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TextMeshProUGUI label = CreateText("Text", buttonGo.transform, labelText, 19, new Color(0.9f, 0.86f, 0.96f, 0.95f));
        label.alignment = TextAlignmentOptions.Center;
        StretchToParent(label.rectTransform);

        return button;
    }

    private static GameObject CreateNotificationPanel(Transform parent, out TextMeshProUGUI notificationText)
    {
        GameObject panel = CreateUiObject("Notification Panel", parent);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -92f);
        rect.sizeDelta = new Vector2(360f, 54f);

        Image image = panel.AddComponent<Image>();
        image.color = new Color(0.025f, 0.018f, 0.045f, 0.82f);
        image.raycastTarget = false;

        Shadow shadow = panel.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        shadow.effectDistance = new Vector2(3f, -3f);

        notificationText = CreateText("Notification Text", panel.transform, string.Empty, 22, new Color(0.94f, 0.9f, 0.98f, 1f));
        notificationText.alignment = TextAlignmentOptions.Center;
        StretchToParent(notificationText.rectTransform);

        return panel;
    }

    private static GameObject CreateConfirmExitPanel(
        Transform parent,
        out Button yesButton,
        out Button noButton)
    {
        GameObject panel = CreateUiObject("Confirm Exit Panel", parent);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        StretchToParent(panelRect);

        Image dimImage = panel.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.7f);
        dimImage.raycastTarget = true;

        GameObject window = CreateUiObject("Confirm Exit Window", panel.transform);
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.anchoredPosition = Vector2.zero;
        windowRect.sizeDelta = new Vector2(640f, 300f);

        Image windowImage = window.AddComponent<Image>();
        windowImage.color = new Color(0.025f, 0.018f, 0.045f, 0.94f);
        windowImage.raycastTarget = true;

        Shadow shadow = window.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(5f, -5f);

        TextMeshProUGUI title = CreateText("Title", window.transform, "Вернуться в главное меню?", 30, new Color(0.94f, 0.9f, 0.98f, 1f));
        title.alignment = TextAlignmentOptions.Center;
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -48f);
        titleRect.sizeDelta = new Vector2(-80f, 54f);

        TextMeshProUGUI message = CreateText("Message", window.transform, "Несохранённый прогресс может быть потерян.", 22, new Color(0.82f, 0.78f, 0.9f, 1f));
        message.alignment = TextAlignmentOptions.Center;
        RectTransform messageRect = message.rectTransform;
        messageRect.anchorMin = new Vector2(0f, 1f);
        messageRect.anchorMax = new Vector2(1f, 1f);
        messageRect.pivot = new Vector2(0.5f, 1f);
        messageRect.anchoredPosition = new Vector2(0f, -112f);
        messageRect.sizeDelta = new Vector2(-80f, 46f);

        GameObject buttonRow = CreateUiObject("Button Row", window.transform);
        RectTransform rowRect = buttonRow.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0f);
        rowRect.anchorMax = new Vector2(0.5f, 0f);
        rowRect.pivot = new Vector2(0.5f, 0f);
        rowRect.anchoredPosition = new Vector2(0f, 42f);
        rowRect.sizeDelta = new Vector2(300f, 54f);

        HorizontalLayoutGroup layout = buttonRow.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        yesButton = CreateDialogButton(buttonRow.transform, "Да", 130f);
        noButton = CreateDialogButton(buttonRow.transform, "Нет", 130f);

        return panel;
    }

    private static GameObject CreateSettingsPanel(
        Transform parent,
        out Slider masterVolumeSlider,
        out Slider musicVolumeSlider,
        out Slider sfxVolumeSlider,
        out Slider textSpeedSlider,
        out Toggle fullscreenToggle,
        out Button closeButton,
        out Button resetButton)
    {
        GameObject panel = CreateUiObject("VN Settings Panel", parent);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        StretchToParent(panelRect);

        Image dimImage = panel.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.7f);
        dimImage.raycastTarget = true;

        GameObject window = CreateUiObject("VN Settings Window", panel.transform);
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.anchoredPosition = Vector2.zero;
        windowRect.sizeDelta = new Vector2(760f, 560f);

        Image windowImage = window.AddComponent<Image>();
        windowImage.color = new Color(0.025f, 0.018f, 0.045f, 0.94f);
        windowImage.raycastTarget = true;

        Shadow shadow = window.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(5f, -5f);

        TextMeshProUGUI title = CreateText("Title", window.transform, "Настройки", 38, new Color(0.94f, 0.9f, 0.98f, 1f));
        title.alignment = TextAlignmentOptions.Center;
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -30f);
        titleRect.sizeDelta = new Vector2(-80f, 58f);

        GameObject content = CreateUiObject("Settings Content", window.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = new Vector2(0f, -112f);
        contentRect.sizeDelta = new Vector2(-120f, 320f);

        VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 16f;
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childControlWidth = false;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = false;
        contentLayout.childForceExpandHeight = false;

        masterVolumeSlider = CreateSettingsSlider(content.transform, "Master Volume", 0f, 1f, 1f);
        musicVolumeSlider = CreateSettingsSlider(content.transform, "Music Volume", 0f, 1f, 1f);
        sfxVolumeSlider = CreateSettingsSlider(content.transform, "SFX Volume", 0f, 1f, 1f);
        textSpeedSlider = CreateSettingsSlider(content.transform, "Text Speed", 0.25f, 3f, 1f);
        fullscreenToggle = CreateSettingsToggle(content.transform, "Fullscreen");

        GameObject buttonRow = CreateUiObject("Button Row", window.transform);
        RectTransform rowRect = buttonRow.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(1f, 0f);
        rowRect.anchorMax = new Vector2(1f, 0f);
        rowRect.pivot = new Vector2(1f, 0f);
        rowRect.anchoredPosition = new Vector2(-48f, 38f);
        rowRect.sizeDelta = new Vector2(300f, 54f);

        HorizontalLayoutGroup buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 18f;
        buttonLayout.childAlignment = TextAnchor.MiddleRight;
        buttonLayout.childControlWidth = false;
        buttonLayout.childControlHeight = false;
        buttonLayout.childForceExpandWidth = false;
        buttonLayout.childForceExpandHeight = false;

        resetButton = CreateDialogButton(buttonRow.transform, "Сбросить", 140f);
        closeButton = CreateDialogButton(buttonRow.transform, "Закрыть", 140f);

        return panel;
    }

    private static Slider CreateSettingsSlider(Transform parent, string labelText, float minValue, float maxValue, float value)
    {
        GameObject row = CreateSettingsRow(parent, labelText);

        GameObject sliderGo = CreateUiObject($"{labelText} Slider", row.transform);
        RectTransform sliderRect = sliderGo.GetComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(360f, 32f);

        Slider slider = sliderGo.AddComponent<Slider>();
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.value = value;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;
        slider.interactable = true;

        GameObject background = CreateUiObject("Background", sliderGo.transform);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(1f, 0.5f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.anchoredPosition = Vector2.zero;
        backgroundRect.sizeDelta = new Vector2(0f, 8f);

        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = new Color(0.12f, 0.1f, 0.18f, 1f);
        backgroundImage.raycastTarget = true;

        GameObject fillArea = CreateUiObject("Fill Area", sliderGo.transform);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0f);
        fillAreaRect.anchorMax = new Vector2(1f, 1f);
        fillAreaRect.offsetMin = new Vector2(0f, 0f);
        fillAreaRect.offsetMax = new Vector2(0f, 0f);

        GameObject fill = CreateUiObject("Fill", fillArea.transform);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0.5f);
        fillRect.anchorMax = new Vector2(1f, 0.5f);
        fillRect.pivot = new Vector2(0.5f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = new Vector2(0f, 8f);

        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.58f, 0.32f, 0.78f, 1f);
        fillImage.raycastTarget = false;

        GameObject handleArea = CreateUiObject("Handle Slide Area", sliderGo.transform);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(9f, 0f);
        handleAreaRect.offsetMax = new Vector2(-9f, 0f);

        GameObject handle = CreateUiObject("Handle", handleArea.transform);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(18f, 24f);

        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = new Color(0.92f, 0.88f, 0.98f, 1f);
        handleImage.raycastTarget = true;

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;

        return slider;
    }

    private static Toggle CreateSettingsToggle(Transform parent, string labelText)
    {
        GameObject row = CreateSettingsRow(parent, labelText);

        GameObject toggleGo = CreateUiObject($"{labelText} Toggle", row.transform);
        RectTransform toggleRect = toggleGo.GetComponent<RectTransform>();
        toggleRect.sizeDelta = new Vector2(360f, 32f);

        Toggle toggle = toggleGo.AddComponent<Toggle>();
        toggle.interactable = true;

        GameObject background = CreateUiObject("Background", toggleGo.transform);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0f, 0.5f);
        backgroundRect.pivot = new Vector2(0f, 0.5f);
        backgroundRect.anchoredPosition = new Vector2(0f, 0f);
        backgroundRect.sizeDelta = new Vector2(28f, 28f);

        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = new Color(0.12f, 0.1f, 0.18f, 1f);
        backgroundImage.raycastTarget = true;

        GameObject checkmark = CreateUiObject("Checkmark", background.transform);
        RectTransform checkmarkRect = checkmark.GetComponent<RectTransform>();
        checkmarkRect.anchorMin = new Vector2(0.5f, 0.5f);
        checkmarkRect.anchorMax = new Vector2(0.5f, 0.5f);
        checkmarkRect.pivot = new Vector2(0.5f, 0.5f);
        checkmarkRect.anchoredPosition = Vector2.zero;
        checkmarkRect.sizeDelta = new Vector2(18f, 18f);

        Image checkmarkImage = checkmark.AddComponent<Image>();
        checkmarkImage.color = new Color(0.58f, 0.32f, 0.78f, 1f);
        checkmarkImage.raycastTarget = false;

        toggle.targetGraphic = backgroundImage;
        toggle.graphic = checkmarkImage;

        return toggle;
    }

    private static GameObject CreateSettingsRow(Transform parent, string labelText)
    {
        GameObject row = CreateUiObject($"{labelText} Row", parent);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(620f, 42f);

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 24f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI label = CreateText("Label", row.transform, labelText, 22, new Color(0.82f, 0.78f, 0.9f, 1f));
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.rectTransform.sizeDelta = new Vector2(220f, 36f);

        return row;
    }

    private static Button CreateDialogButton(Transform parent, string labelText, float width)
    {
        return CreateQuickMenuButton(parent, labelText, width);
    }

    private static void CreateAccentLine(Transform parent)
    {
        GameObject line = CreateUiObject("Backlog Accent Line", parent);
        RectTransform rect = line.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(3f, -48f);

        Image image = line.AddComponent<Image>();
        image.color = new Color(0.55f, 0.22f, 0.8f, 0.65f);
        image.raycastTarget = false;
    }

    private static void CreateTitle(Transform parent)
    {
        TextMeshProUGUI title = CreateText("Title", parent, "История", 42, new Color(0.94f, 0.9f, 0.98f, 1f));
        title.alignment = TextAlignmentOptions.Center;

        RectTransform rect = title.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -28f);
        rect.sizeDelta = new Vector2(-120f, 68f);
    }

    private static void CreateScrollView(Transform parent, out TextMeshProUGUI backlogText)
    {
        GameObject scrollView = CreateUiObject("Backlog Scroll View", parent);
        RectTransform scrollRectTransform = scrollView.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0f, 0f);
        scrollRectTransform.anchorMax = new Vector2(1f, 1f);
        scrollRectTransform.offsetMin = new Vector2(82f, 112f);
        scrollRectTransform.offsetMax = new Vector2(-82f, -118f);

        Image scrollBackground = scrollView.AddComponent<Image>();
        scrollBackground.color = new Color(0.04f, 0.03f, 0.07f, 0.55f);
        scrollBackground.raycastTarget = true;

        ScrollRect scrollRect = scrollView.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 32f;

        GameObject viewport = CreateUiObject("Viewport", scrollView.transform);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(24f, 20f);
        viewportRect.offsetMax = new Vector2(-42f, -20f);

        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewportImage.raycastTarget = true;

        RectMask2D mask = viewport.AddComponent<RectMask2D>();
        mask.padding = Vector4.zero;

        GameObject content = CreateUiObject("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layoutGroup = content.AddComponent<VerticalLayoutGroup>();
        layoutGroup.childAlignment = TextAnchor.UpperLeft;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.padding = new RectOffset(0, 0, 0, 0);

        ContentSizeFitter contentSizeFitter = content.AddComponent<ContentSizeFitter>();
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        backlogText = CreateText("Backlog Text", content.transform, string.Empty, 28, new Color(0.9f, 0.86f, 0.96f, 1f));
        backlogText.alignment = TextAlignmentOptions.TopLeft;
        backlogText.enableWordWrapping = true;
        backlogText.overflowMode = TextOverflowModes.Overflow;
        backlogText.raycastTarget = false;

        LayoutElement textLayout = backlogText.gameObject.AddComponent<LayoutElement>();
        textLayout.flexibleWidth = 1f;
        textLayout.minHeight = 80f;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        GameObject scrollbar = CreateVerticalScrollbar(scrollView.transform);
        scrollRect.verticalScrollbar = scrollbar.GetComponent<Scrollbar>();
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = -8f;
    }

    private static GameObject CreateVerticalScrollbar(Transform parent)
    {
        GameObject scrollbarGo = CreateUiObject("Vertical Scrollbar", parent);
        RectTransform scrollbarRect = scrollbarGo.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.offsetMin = new Vector2(-20f, 20f);
        scrollbarRect.offsetMax = new Vector2(-8f, -20f);

        Image scrollbarImage = scrollbarGo.AddComponent<Image>();
        scrollbarImage.color = new Color(0.1f, 0.08f, 0.14f, 0.8f);
        scrollbarImage.raycastTarget = true;

        Scrollbar scrollbar = scrollbarGo.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        GameObject slidingArea = CreateUiObject("Sliding Area", scrollbarGo.transform);
        RectTransform slidingRect = slidingArea.GetComponent<RectTransform>();
        StretchToParent(slidingRect);

        GameObject handle = CreateUiObject("Handle", slidingArea.transform);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        StretchToParent(handleRect);

        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = new Color(0.55f, 0.32f, 0.72f, 0.85f);
        handleImage.raycastTarget = true;

        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleImage;

        return scrollbarGo;
    }

    private static Button CreateCloseButton(Transform parent)
    {
        GameObject buttonGo = CreateUiObject("Backlog Close Button", parent);
        RectTransform rect = buttonGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-82f, 34f);
        rect.sizeDelta = new Vector2(190f, 54f);

        Image image = buttonGo.AddComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = true;

        Button button = buttonGo.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.08f, 0.06f, 0.12f, 0.95f);
        colors.highlightedColor = new Color(0.34f, 0.16f, 0.46f, 0.95f);
        colors.pressedColor = new Color(0.45f, 0.2f, 0.58f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.04f, 0.03f, 0.06f, 0.65f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TextMeshProUGUI label = CreateText("Text", buttonGo.transform, "Закрыть", 24, new Color(0.94f, 0.9f, 0.98f, 1f));
        label.alignment = TextAlignmentOptions.Center;
        StretchToParent(label.rectTransform);

        return button;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, int fontSize, Color color)
    {
        GameObject textGo = CreateUiObject(name, parent);
        TextMeshProUGUI textComponent = textGo.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.color = color;
        textComponent.raycastTarget = false;
        return textComponent;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
