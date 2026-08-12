using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The single player-facing Preferences hierarchy used by both Main Menu and gameplay.
/// It is built deterministically so the two contexts cannot serialize divergent layouts.
/// </summary>
public sealed class SharedPreferencesView : MonoBehaviour, IPreferencesView
{
    public const string ScreenModeId = "screen_mode";
    public const string ResolutionId = "resolution";
    public const string RunInBackgroundId = "run_in_background";
    public const string MuteAllId = "mute_all";
    public const string MasterVolumeId = "master_volume";
    public const string MusicVolumeId = "music_volume";
    public const string SfxVolumeId = "sfx_volume";
    public const string TextSpeedId = "text_speed";
    public const string AutoForwardDelayId = "auto_forward_delay";
    public const string SkipUnseenId = "skip_unseen";
    public const string SkipAfterChoicesId = "skip_after_choices";
    public const string SkipSpeedId = "skip_speed";
    public const string AutosaveId = "autosave";
    public const string TextSizeId = "text_size";
    public const string TextboxOpacityId = "textbox_opacity";

    private static readonly string[] ControlIds =
    {
        ScreenModeId, ResolutionId, RunInBackgroundId,
        MuteAllId, MasterVolumeId, MusicVolumeId, SfxVolumeId,
        TextSpeedId, AutoForwardDelayId,
        SkipUnseenId, SkipAfterChoicesId, SkipSpeedId, AutosaveId,
        TextSizeId, TextboxOpacityId
    };

    private static readonly Color WindowColor = new Color(0.018f, 0.035f, 0.075f, 0.985f);
    private static readonly Color RowColor = new Color(0.035f, 0.07f, 0.13f, 0.82f);
    private static readonly Color ControlColor = new Color(0.07f, 0.12f, 0.20f, 0.98f);
    private static readonly Color AccentColor = new Color(0.72f, 0.16f, 0.18f, 1f);
    private static readonly Color MutedTextColor = new Color(0.76f, 0.82f, 0.90f, 1f);

    private readonly Dictionary<string, Slider> sliders = new Dictionary<string, Slider>();
    private readonly Dictionary<string, TextMeshProUGUI> sliderValues = new Dictionary<string, TextMeshProUGUI>();
    private readonly Dictionary<string, Toggle> toggles = new Dictionary<string, Toggle>();
    private readonly Dictionary<string, TextMeshProUGUI> toggleValues = new Dictionary<string, TextMeshProUGUI>();
    private readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>();
    private readonly Dictionary<string, TextMeshProUGUI> buttonValues = new Dictionary<string, TextMeshProUGUI>();

    private GameObject root;
    private PreferencesController controller;
    private bool isBound;

    public string ContextId { get; private set; }
    public static IReadOnlyList<string> VisibleControlIds => ControlIds;
    public bool IsVisible => root != null && root.activeSelf;

    public static SharedPreferencesView Create(Transform contextTransform, string contextId)
    {
        if (contextTransform == null)
        {
            Debug.LogError($"[PREFERENCES] Cannot create shared view for '{contextId}': context transform is missing.");
            return null;
        }

        Canvas canvas = contextTransform.GetComponentInParent<Canvas>();
        Transform host = canvas != null ? canvas.transform : contextTransform.root;
        var owner = new GameObject($"Shared Preferences View [{contextId}]", typeof(RectTransform));
        owner.layer = host.gameObject.layer;
        owner.transform.SetParent(host, false);
        owner.transform.SetAsLastSibling();

        var view = owner.AddComponent<SharedPreferencesView>();
        view.Build(contextId);
        return view;
    }

    public bool HasControl(string controlId)
    {
        return sliders.ContainsKey(controlId) || toggles.ContainsKey(controlId) || buttons.ContainsKey(controlId);
    }

    public Slider GetSlider(string controlId) => sliders.TryGetValue(controlId, out Slider value) ? value : null;
    public Toggle GetToggle(string controlId) => toggles.TryGetValue(controlId, out Toggle value) ? value : null;
    public Button GetButton(string controlId) => buttons.TryGetValue(controlId, out Button value) ? value : null;
    public string GetDisplayedValue(string controlId)
    {
        if (sliderValues.TryGetValue(controlId, out TextMeshProUGUI sliderValue)) return sliderValue.text;
        if (toggleValues.TryGetValue(controlId, out TextMeshProUGUI toggleValue)) return toggleValue.text;
        return buttonValues.TryGetValue(controlId, out TextMeshProUGUI buttonValue) ? buttonValue.text : string.Empty;
    }

    public void Bind(PreferencesController sharedController)
    {
        controller = sharedController;
        if (isBound || controller == null)
        {
            return;
        }

        isBound = true;
        buttons["reset"].onClick.AddListener(controller.Reset);
        buttons["back"].onClick.AddListener(controller.Close);
        buttons[ScreenModeId].onClick.AddListener(controller.CycleScreenMode);
        buttons[ResolutionId].onClick.AddListener(controller.CycleResolution);
        buttons[SkipSpeedId].onClick.AddListener(controller.CycleSkipBehavior);

        toggles[RunInBackgroundId].onValueChanged.AddListener(controller.SetRunInBackground);
        toggles[RunInBackgroundId].onValueChanged.AddListener(value => SetToggle(RunInBackgroundId, value));
        toggles[MuteAllId].onValueChanged.AddListener(controller.SetMuteAll);
        toggles[MuteAllId].onValueChanged.AddListener(value => SetToggle(MuteAllId, value));
        toggles[SkipUnseenId].onValueChanged.AddListener(controller.SetSkipUnseen);
        toggles[SkipUnseenId].onValueChanged.AddListener(
            value => SetToggle(SkipUnseenId, value, value ? "Вкл. — можно всё" : "Выкл. — только виденное"));
        toggles[SkipAfterChoicesId].onValueChanged.AddListener(controller.SetSkipAfterChoices);
        toggles[SkipAfterChoicesId].onValueChanged.AddListener(value => SetToggle(SkipAfterChoicesId, value));
        toggles[AutosaveId].onValueChanged.AddListener(controller.SetAutoSave);
        toggles[AutosaveId].onValueChanged.AddListener(value => SetToggle(AutosaveId, value));

        sliders[MasterVolumeId].onValueChanged.AddListener(controller.SetMasterVolume);
        sliders[MasterVolumeId].onValueChanged.AddListener(value => SetSliderValueText(MasterVolumeId, PreferencesFormatting.Percent(value)));
        sliders[MusicVolumeId].onValueChanged.AddListener(controller.SetMusicVolume);
        sliders[MusicVolumeId].onValueChanged.AddListener(value => SetSliderValueText(MusicVolumeId, PreferencesFormatting.Percent(value)));
        sliders[SfxVolumeId].onValueChanged.AddListener(controller.SetSfxVolume);
        sliders[SfxVolumeId].onValueChanged.AddListener(value => SetSliderValueText(SfxVolumeId, PreferencesFormatting.Percent(value)));
        sliders[TextSpeedId].onValueChanged.AddListener(controller.SetTextSpeed);
        sliders[TextSpeedId].onValueChanged.AddListener(value => SetSliderValueText(TextSpeedId, PreferencesFormatting.TextSpeed(value)));
        sliders[AutoForwardDelayId].onValueChanged.AddListener(
            seconds => controller.SetAutoForwardDelay(PreferencesFormatting.AutoForwardDelayStored(seconds)));
        sliders[AutoForwardDelayId].onValueChanged.AddListener(
            seconds => SetSliderValueText(AutoForwardDelayId, PreferencesFormatting.AutoForwardDelay(PreferencesFormatting.AutoForwardDelayStored(seconds))));
        sliders[TextSizeId].onValueChanged.AddListener(
            value => controller.SetDialogueTextScale(Mathf.Round(value * 20f) / 20f));
        sliders[TextSizeId].onValueChanged.AddListener(
            value => SetSliderValueText(TextSizeId, PreferencesFormatting.TextScale(Mathf.Round(value * 20f) / 20f)));
        sliders[TextboxOpacityId].onValueChanged.AddListener(controller.SetTextboxOpacity);
        sliders[TextboxOpacityId].onValueChanged.AddListener(value => SetSliderValueText(TextboxOpacityId, PreferencesFormatting.Percent(value)));
    }

    public void SetVisible(bool visible)
    {
        if (root != null)
        {
            root.SetActive(visible);
            if (visible)
            {
                root.transform.SetAsLastSibling();
            }
        }
    }

    public void Refresh(PreferencesState settings)
    {
        SetButtonValue(ScreenModeId, settings.screenMode);
        SetButtonValue(ResolutionId, settings.resolution);
        SetButtonValue(SkipSpeedId, settings.skipBehavior);

        SetToggle(RunInBackgroundId, settings.runInBackground);
        SetToggle(MuteAllId, settings.muteAll);
        SetToggle(SkipUnseenId, settings.skipMode == "Всё", settings.skipMode == "Всё" ? "Вкл. — можно всё" : "Выкл. — только виденное");
        SetToggle(SkipAfterChoicesId, settings.skipAfterChoices);
        SetToggle(AutosaveId, settings.autoSave);

        SetSlider(MasterVolumeId, settings.masterVolume, PreferencesFormatting.Percent(settings.masterVolume));
        SetSlider(MusicVolumeId, settings.musicVolume, PreferencesFormatting.Percent(settings.musicVolume));
        SetSlider(SfxVolumeId, settings.sfxVolume, PreferencesFormatting.Percent(settings.sfxVolume));
        SetSlider(TextSpeedId, settings.textSpeed, PreferencesFormatting.TextSpeed(settings.textSpeed));
        SetSlider(
            AutoForwardDelayId,
            PreferencesFormatting.AutoForwardDelaySeconds(settings.autoForwardDelay),
            PreferencesFormatting.AutoForwardDelay(settings.autoForwardDelay));
        SetSlider(TextSizeId, settings.dialogueTextScale, PreferencesFormatting.TextScale(settings.dialogueTextScale));
        SetSlider(TextboxOpacityId, settings.textboxOpacity, PreferencesFormatting.Percent(settings.textboxOpacity));
    }

    private void Build(string contextId)
    {
        ContextId = contextId;
        root = gameObject;
        Stretch(root.GetComponent<RectTransform>());

        Image dim = root.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);
        dim.raycastTarget = true;

        GameObject window = CreateUiObject(root.transform, "Preferences Window");
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.055f, 0.05f);
        windowRect.anchorMax = new Vector2(0.945f, 0.95f);
        windowRect.offsetMin = Vector2.zero;
        windowRect.offsetMax = Vector2.zero;
        Image windowImage = window.AddComponent<Image>();
        windowImage.color = WindowColor;
        windowImage.raycastTarget = true;
        Outline outline = window.AddComponent<Outline>();
        outline.effectColor = new Color(0.55f, 0.68f, 0.90f, 0.24f);
        outline.effectDistance = new Vector2(1f, -1f);

        CreateHeader(window.transform);
        CreateFooter(window.transform);
        CreateScrollContent(window.transform);
        root.SetActive(false);
    }

    private void CreateHeader(Transform window)
    {
        GameObject header = CreateUiObject(window, "Header");
        RectTransform rect = header.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 72f);
        rect.anchoredPosition = Vector2.zero;
        Image image = header.AddComponent<Image>();
        image.color = new Color(0.025f, 0.055f, 0.105f, 0.98f);

        TextMeshProUGUI title = CreateText(header.transform, "Title", "НАСТРОЙКИ", 28f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        Stretch(title.rectTransform, 30f, 30f, 0f, 0f);

        GameObject accent = CreateUiObject(header.transform, "Accent");
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(1f, 0f);
        accentRect.pivot = new Vector2(0.5f, 0f);
        accentRect.sizeDelta = new Vector2(0f, 3f);
        accent.AddComponent<Image>().color = AccentColor;
    }

    private void CreateFooter(Transform window)
    {
        GameObject footer = CreateUiObject(window, "Footer");
        RectTransform rect = footer.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(0f, 74f);
        Image image = footer.AddComponent<Image>();
        image.color = new Color(0.025f, 0.055f, 0.105f, 0.98f);

        CreateFooterButton(footer.transform, "reset", "СБРОСИТЬ", new Vector2(0f, 0.5f), new Vector2(24f, 0f));
        CreateFooterButton(footer.transform, "back", "НАЗАД", new Vector2(1f, 0.5f), new Vector2(-24f, 0f));
    }

    private void CreateScrollContent(Transform window)
    {
        GameObject viewport = CreateUiObject(window, "Single Scroll Viewport");
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        Stretch(viewportRect, 26f, 54f, 82f, 82f);
        Image viewportRaycast = viewport.AddComponent<Image>();
        viewportRaycast.color = new Color(0f, 0f, 0f, 0.001f);
        viewportRaycast.raycastTarget = true;
        viewport.AddComponent<RectMask2D>();

        GameObject content = CreateUiObject(viewport.transform, "Preferences Content");
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = Vector2.one;
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        // Keep the final control comfortably above the sticky Reset/Back footer
        // when the scroll view reaches its lower limit.
        layout.padding = new RectOffset(16, 20, 16, 96);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Scrollbar scrollbar = CreateScrollbar(window);
        ScrollRect scrollRect = viewport.AddComponent<ScrollRect>();
        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 36f;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = 8f;

        AddSection(content.transform, "ДИСПЛЕЙ");
        AddCycleRow(content.transform, ScreenModeId, "Режим экрана");
        AddCycleRow(content.transform, ResolutionId, "Разрешение");
        AddToggleRow(content.transform, RunInBackgroundId, "Работа в фоне");

        AddSection(content.transform, "АУДИО");
        AddToggleRow(content.transform, MuteAllId, "Выключить весь звук");
        AddSliderRow(content.transform, MasterVolumeId, "Общая громкость", 0f, 1f);
        AddSliderRow(content.transform, MusicVolumeId, "Громкость музыки", 0f, 1f);
        AddSliderRow(content.transform, SfxVolumeId, "Громкость эффектов", 0f, 1f);

        AddSection(content.transform, "ДИАЛОГ И АВТО");
        AddSliderRow(content.transform, TextSpeedId, "Скорость текста", 20f, 100f, true);
        AddSliderRow(content.transform, AutoForwardDelayId, "Задержка автоперехода", 0.5f, 5f);

        AddSection(content.transform, "ПРОПУСК И СОХРАНЕНИЯ");
        AddToggleRow(content.transform, SkipUnseenId, "Разрешить пропуск непрочитанного");
        AddToggleRow(content.transform, SkipAfterChoicesId, "Возобновлять пропуск после выбора");
        AddCycleRow(content.transform, SkipSpeedId, "Скорость пропуска");
        AddToggleRow(content.transform, AutosaveId, "Автосохранение");

        AddSection(content.transform, "ДОСТУПНОСТЬ И ИНТЕРФЕЙС");
        AddSliderRow(content.transform, TextSizeId, "Размер текста диалога", 0.85f, 1.25f);
        AddSliderRow(content.transform, TextboxOpacityId, "Непрозрачность текстового окна", 0f, 1f);
    }

    private void AddSection(Transform parent, string title)
    {
        TextMeshProUGUI text = CreateText(parent, $"Section {title}", title, 20f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        text.color = new Color(0.92f, 0.55f, 0.56f, 1f);
        LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 42f;
        layout.preferredHeight = 42f;
    }

    private void AddCycleRow(Transform parent, string id, string label)
    {
        Transform control = CreateRow(parent, id, label);
        Button button = CreateButton(control, $"{id} Button", "—", 360f);
        buttons[id] = button;
        buttonValues[id] = button.GetComponentInChildren<TextMeshProUGUI>();
    }

    private void AddToggleRow(Transform parent, string id, string label)
    {
        Transform control = CreateRow(parent, id, label);
        GameObject toggleObject = CreateUiObject(control, $"{id} Toggle");
        Image background = toggleObject.AddComponent<Image>();
        background.color = ControlColor;
        Toggle toggle = toggleObject.AddComponent<Toggle>();
        toggle.targetGraphic = background;
        LayoutElement layout = toggleObject.AddComponent<LayoutElement>();
        layout.preferredWidth = 360f;
        layout.minHeight = 44f;
        layout.preferredHeight = 44f;

        GameObject markObject = CreateUiObject(toggleObject.transform, "Mark");
        RectTransform markRect = markObject.GetComponent<RectTransform>();
        markRect.anchorMin = new Vector2(0f, 0.5f);
        markRect.anchorMax = new Vector2(0f, 0.5f);
        markRect.pivot = new Vector2(0f, 0.5f);
        markRect.anchoredPosition = new Vector2(12f, 0f);
        markRect.sizeDelta = new Vector2(24f, 24f);
        Image mark = markObject.AddComponent<Image>();
        mark.color = AccentColor;
        toggle.graphic = mark;

        TextMeshProUGUI value = CreateText(toggleObject.transform, "Value", "Выкл.", 18f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        Stretch(value.rectTransform, 50f, 10f, 0f, 0f);
        toggles[id] = toggle;
        toggleValues[id] = value;
    }

    private void AddSliderRow(Transform parent, string id, string label, float min, float max, bool wholeNumbers = false)
    {
        Transform control = CreateRow(parent, id, label);
        GameObject sliderObject = CreateUiObject(control, $"{id} Slider");
        HorizontalLayoutGroup layout = sliderObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        LayoutElement controlLayout = sliderObject.AddComponent<LayoutElement>();
        controlLayout.preferredWidth = 430f;
        controlLayout.minHeight = 44f;

        Slider slider = CreateSlider(sliderObject.transform, min, max, wholeNumbers);
        LayoutElement sliderLayout = slider.gameObject.AddComponent<LayoutElement>();
        sliderLayout.preferredWidth = 286f;
        sliderLayout.minHeight = 34f;
        TextMeshProUGUI value = CreateText(sliderObject.transform, "Value", "—", 17f, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
        value.color = MutedTextColor;
        LayoutElement valueLayout = value.gameObject.AddComponent<LayoutElement>();
        valueLayout.preferredWidth = 128f;
        valueLayout.minHeight = 40f;
        sliders[id] = slider;
        sliderValues[id] = value;
    }

    private Transform CreateRow(Transform parent, string id, string label)
    {
        GameObject row = CreateUiObject(parent, $"Row [{id}]");
        Image rowImage = row.AddComponent<Image>();
        rowImage.color = RowColor;
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 7, 7);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.minHeight = 58f;
        rowLayout.preferredHeight = 58f;

        TextMeshProUGUI labelText = CreateText(row.transform, "Label", label, 18f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
        labelLayout.minWidth = 300f;
        labelLayout.flexibleWidth = 1f;

        GameObject control = CreateUiObject(row.transform, "Control");
        HorizontalLayoutGroup controlLayout = control.AddComponent<HorizontalLayoutGroup>();
        controlLayout.childAlignment = TextAnchor.MiddleRight;
        controlLayout.childControlWidth = true;
        controlLayout.childControlHeight = true;
        controlLayout.childForceExpandWidth = false;
        controlLayout.childForceExpandHeight = false;
        LayoutElement width = control.AddComponent<LayoutElement>();
        width.preferredWidth = 430f;
        width.minWidth = 360f;
        return control.transform;
    }

    private Button CreateFooterButton(Transform parent, string id, string label, Vector2 anchor, Vector2 position)
    {
        Button button = CreateButton(parent, $"{label} Button", label, 220f);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(220f, 46f);
        buttons[id] = button;
        return button;
    }

    private Button CreateButton(Transform parent, string name, string label, float preferredWidth)
    {
        GameObject buttonObject = CreateUiObject(parent, name);
        Image image = buttonObject.AddComponent<Image>();
        image.color = ControlColor;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = CreateButtonColors();
        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredWidth = preferredWidth;
        layout.minHeight = 44f;
        layout.preferredHeight = 44f;
        TextMeshProUGUI text = CreateText(buttonObject.transform, "Label", label, 18f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 12f, 12f, 0f, 0f);
        return button;
    }

    private Slider CreateSlider(Transform parent, float min, float max, bool wholeNumbers)
    {
        GameObject sliderObject = CreateUiObject(parent, "Slider");
        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = wholeNumbers;

        GameObject trackObject = CreateUiObject(sliderObject.transform, "Track");
        Stretch(trackObject.GetComponent<RectTransform>(), 0f, 0f, 14f, 14f);
        Image track = trackObject.AddComponent<Image>();
        track.color = new Color(0.12f, 0.17f, 0.25f, 1f);

        GameObject fillArea = CreateUiObject(sliderObject.transform, "Fill Area");
        Stretch(fillArea.GetComponent<RectTransform>(), 8f, 8f, 14f, 14f);
        GameObject fillObject = CreateUiObject(fillArea.transform, "Fill");
        Stretch(fillObject.GetComponent<RectTransform>());
        Image fill = fillObject.AddComponent<Image>();
        fill.color = AccentColor;

        GameObject handleArea = CreateUiObject(sliderObject.transform, "Handle Slide Area");
        Stretch(handleArea.GetComponent<RectTransform>(), 8f, 8f, 0f, 0f);
        GameObject handleObject = CreateUiObject(handleArea.transform, "Handle");
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(14f, 22f);
        Image handle = handleObject.AddComponent<Image>();
        handle.color = new Color(0.88f, 0.92f, 0.98f, 1f);

        slider.fillRect = fillObject.GetComponent<RectTransform>();
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private Scrollbar CreateScrollbar(Transform window)
    {
        GameObject scrollbarObject = CreateUiObject(window, "Scrollbar");
        RectTransform rect = scrollbarObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-18f, 0f);
        rect.sizeDelta = new Vector2(16f, -164f);
        Image background = scrollbarObject.AddComponent<Image>();
        background.color = new Color(0.05f, 0.09f, 0.15f, 0.9f);

        GameObject area = CreateUiObject(scrollbarObject.transform, "Sliding Area");
        Stretch(area.GetComponent<RectTransform>(), 2f, 2f, 2f, 2f);
        GameObject handleObject = CreateUiObject(area.transform, "Handle");
        Stretch(handleObject.GetComponent<RectTransform>());
        Image handle = handleObject.AddComponent<Image>();
        handle.color = new Color(0.62f, 0.70f, 0.82f, 0.92f);
        Scrollbar scrollbar = scrollbarObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handleObject.GetComponent<RectTransform>();
        scrollbar.targetGraphic = handle;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        return scrollbar;
    }

    private void SetButtonValue(string id, string value)
    {
        if (buttonValues.TryGetValue(id, out TextMeshProUGUI text))
        {
            text.text = string.IsNullOrEmpty(value) ? "—" : value;
        }
    }

    private void SetToggle(string id, bool value, string displayedValue = null)
    {
        if (toggles.TryGetValue(id, out Toggle toggle))
        {
            toggle.SetIsOnWithoutNotify(value);
        }

        if (toggleValues.TryGetValue(id, out TextMeshProUGUI text))
        {
            text.text = displayedValue ?? (value ? "Вкл." : "Выкл.");
        }
    }

    private void SetSlider(string id, float value, string displayedValue)
    {
        if (sliders.TryGetValue(id, out Slider slider))
        {
            slider.SetValueWithoutNotify(value);
        }

        SetSliderValueText(id, displayedValue);
    }

    private void SetSliderValueText(string id, string displayedValue)
    {
        if (sliderValues.TryGetValue(id, out TextMeshProUGUI text))
        {
            text.text = displayedValue;
        }
    }

    private static GameObject CreateUiObject(Transform parent, string name)
    {
        var result = new GameObject(name, typeof(RectTransform));
        result.layer = parent.gameObject.layer;
        result.transform.SetParent(parent, false);
        return result;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        string value,
        float size,
        FontStyles style,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUiObject(parent, name);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }

    private static ColorBlock CreateButtonColors()
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.65f);
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
