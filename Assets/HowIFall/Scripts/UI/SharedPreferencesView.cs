using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One deterministic player-facing Preferences view for Main Menu and gameplay.</summary>
public sealed class SharedPreferencesView : MonoBehaviour, IPreferencesView
{
    public const string ScreenModeId = "screen_mode";
    public const string ResolutionId = "resolution";
    public const string MasterVolumeId = "master_volume";
    public const string MusicVolumeId = "music_volume";
    public const string SfxVolumeId = "sfx_volume";
    public const string TextSpeedId = "text_speed";
    public const string AutoForwardDelayId = "auto_forward_delay";
    public const string TextSizeId = "text_size";
    public const string TextboxOpacityId = "textbox_opacity";
    public const string SkipUnseenId = "skip_unseen";
    public const string SkipAfterChoicesId = "skip_after_choices";
    public const string AutosaveId = "autosave";
    public const string ShowQuickMenuId = "show_quick_menu";

    private static readonly string[] ControlIds =
    {
        ScreenModeId, ResolutionId, MasterVolumeId, MusicVolumeId, SfxVolumeId,
        TextSpeedId, AutoForwardDelayId, TextSizeId, TextboxOpacityId,
        SkipUnseenId, SkipAfterChoicesId, AutosaveId, ShowQuickMenuId
    };

    private static readonly Color WindowColor = new Color(0.105f, 0.12f, 0.145f, 0.99f);
    private static readonly Color HeaderColor = new Color(0.13f, 0.15f, 0.18f, 1f);
    private static readonly Color RowColor = new Color(0.165f, 0.19f, 0.225f, 1f);
    private static readonly Color ControlColor = new Color(0.22f, 0.255f, 0.30f, 1f);
    private static readonly Color AccentColor = new Color(0.30f, 0.64f, 0.94f, 1f);
    private static readonly Color PrimaryText = new Color(0.97f, 0.98f, 1f, 1f);
    private static readonly Color SecondaryText = new Color(0.74f, 0.80f, 0.88f, 1f);

    private readonly Dictionary<string, Slider> sliders = new Dictionary<string, Slider>();
    private readonly Dictionary<string, TextMeshProUGUI> sliderValues = new Dictionary<string, TextMeshProUGUI>();
    private readonly Dictionary<string, Toggle> toggles = new Dictionary<string, Toggle>();
    private readonly Dictionary<string, TextMeshProUGUI> toggleValues = new Dictionary<string, TextMeshProUGUI>();
    private readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>();
    private readonly Dictionary<string, TMP_Dropdown> dropdowns = new Dictionary<string, TMP_Dropdown>();
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
        GameObject owner = new GameObject($"Shared Preferences View [{contextId}]", typeof(RectTransform));
        owner.layer = host.gameObject.layer;
        owner.transform.SetParent(host, false);
        owner.transform.SetAsLastSibling();
        SharedPreferencesView view = owner.AddComponent<SharedPreferencesView>();
        view.Build(contextId);
        return view;
    }

    public bool HasControl(string id) => sliders.ContainsKey(id) || toggles.ContainsKey(id) || buttons.ContainsKey(id) || dropdowns.ContainsKey(id);
    public Slider GetSlider(string id) => sliders.TryGetValue(id, out Slider value) ? value : null;
    public Toggle GetToggle(string id) => toggles.TryGetValue(id, out Toggle value) ? value : null;
    public Button GetButton(string id) => buttons.TryGetValue(id, out Button value) ? value : null;
    public TMP_Dropdown GetDropdown(string id) => dropdowns.TryGetValue(id, out TMP_Dropdown value) ? value : null;
    public string GetDisplayedValue(string id)
    {
        if (sliderValues.TryGetValue(id, out TextMeshProUGUI sliderValue)) return sliderValue.text;
        if (toggleValues.TryGetValue(id, out TextMeshProUGUI toggleValue)) return toggleValue.text;
        return dropdowns.TryGetValue(id, out TMP_Dropdown dropdown) && dropdown.options.Count > dropdown.value
            ? dropdown.options[dropdown.value].text : string.Empty;
    }

    public void Bind(PreferencesController sharedController)
    {
        controller = sharedController;
        if (isBound || controller == null) return;
        isBound = true;
        buttons["reset"].onClick.AddListener(controller.Reset);
        buttons["back"].onClick.AddListener(controller.Close);
        dropdowns[ScreenModeId].onValueChanged.AddListener(index => controller.SetScreenMode(DropdownValue(ScreenModeId, index)));
        dropdowns[ResolutionId].onValueChanged.AddListener(index => controller.SetResolution(DropdownValue(ResolutionId, index)));
        BindToggle(SkipUnseenId, controller.SetSkipUnseen, value => value ? "Вкл. — можно всё" : "Выкл. — только виденное");
        BindToggle(SkipAfterChoicesId, controller.SetSkipAfterChoices);
        BindToggle(AutosaveId, controller.SetAutoSave);
        BindToggle(ShowQuickMenuId, controller.SetShowQuickMenu);
        BindSlider(MasterVolumeId, controller.SetMasterVolume);
        BindSlider(MusicVolumeId, controller.SetMusicVolume);
        BindSlider(SfxVolumeId, controller.SetSfxVolume);
        BindSlider(TextSpeedId, controller.SetTextSpeed, PreferencesFormatting.TextSpeed);
        sliders[AutoForwardDelayId].onValueChanged.AddListener(value => controller.SetAutoForwardDelay(PreferencesFormatting.AutoForwardDelayStored(value)));
        sliders[AutoForwardDelayId].onValueChanged.AddListener(value => SetSliderValue(AutoForwardDelayId, PreferencesFormatting.AutoForwardDelay(PreferencesFormatting.AutoForwardDelayStored(value))));
        sliders[TextSizeId].onValueChanged.AddListener(value => controller.SetDialogueTextScale(Mathf.Round(value * 20f) / 20f));
        sliders[TextSizeId].onValueChanged.AddListener(value => SetSliderValue(TextSizeId, PreferencesFormatting.TextScale(Mathf.Round(value * 20f) / 20f)));
        BindSlider(TextboxOpacityId, controller.SetTextboxOpacity, PreferencesFormatting.Percent);
    }

    public void SetVisible(bool visible)
    {
        if (root == null) return;
        root.SetActive(visible);
        if (visible) root.transform.SetAsLastSibling();
    }

    public void Refresh(PreferencesState settings)
    {
        SetDropdown(ScreenModeId, settings.screenMode);
        SetDropdown(ResolutionId, settings.resolution);
        SetToggle(SkipUnseenId, settings.skipMode == "Всё", settings.skipMode == "Всё" ? "Вкл. — можно всё" : "Выкл. — только виденное");
        SetToggle(SkipAfterChoicesId, settings.skipAfterChoices);
        SetToggle(AutosaveId, settings.autoSave);
        SetToggle(ShowQuickMenuId, settings.showQuickMenu);
        SetSlider(MasterVolumeId, settings.masterVolume);
        SetSlider(MusicVolumeId, settings.musicVolume);
        SetSlider(SfxVolumeId, settings.sfxVolume);
        SetSlider(TextSpeedId, settings.textSpeed, PreferencesFormatting.TextSpeed(settings.textSpeed));
        SetSlider(AutoForwardDelayId, PreferencesFormatting.AutoForwardDelaySeconds(settings.autoForwardDelay), PreferencesFormatting.AutoForwardDelay(settings.autoForwardDelay));
        SetSlider(TextSizeId, settings.dialogueTextScale, PreferencesFormatting.TextScale(settings.dialogueTextScale));
        SetSlider(TextboxOpacityId, settings.textboxOpacity, PreferencesFormatting.Percent(settings.textboxOpacity));
    }

    private void Build(string contextId)
    {
        ContextId = contextId;
        root = gameObject;
        Stretch(root.GetComponent<RectTransform>());
        Image dim = root.AddComponent<Image>();
        dim.color = new Color(0.025f, 0.03f, 0.04f, 0.76f);
        dim.raycastTarget = true;

        GameObject window = CreateUi(root.transform, "Preferences Window");
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(1040f, 900f);
        window.AddComponent<Image>().color = WindowColor;
        Outline outline = window.AddComponent<Outline>();
        outline.effectColor = new Color(0.45f, 0.62f, 0.82f, 0.48f);
        outline.effectDistance = new Vector2(1f, -1f);
        CreateHeader(window.transform);
        CreateFooter(window.transform);
        CreateContent(window.transform);
        root.SetActive(false);
    }

    private void CreateHeader(Transform window)
    {
        GameObject header = CreateUi(window, "Header");
        RectTransform rect = header.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f, 1f); rect.sizeDelta = new Vector2(0f, 68f);
        header.AddComponent<Image>().color = HeaderColor;
        TextMeshProUGUI title = Text(header.transform, "Title", "НАСТРОЙКИ", 26f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        Stretch(title.rectTransform, 28f, 28f);
        GameObject accent = CreateUi(header.transform, "Accent");
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f); accentRect.anchorMax = new Vector2(1f, 0f); accentRect.pivot = new Vector2(0.5f, 0f); accentRect.sizeDelta = new Vector2(0f, 2f);
        accent.AddComponent<Image>().color = AccentColor;
    }

    private void CreateFooter(Transform window)
    {
        GameObject footer = CreateUi(window, "Footer");
        RectTransform rect = footer.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(1f, 0f); rect.pivot = new Vector2(0.5f, 0f); rect.sizeDelta = new Vector2(0f, 68f);
        footer.AddComponent<Image>().color = HeaderColor;
        FooterButton(footer.transform, "reset", "СБРОСИТЬ", new Vector2(0f, 0.5f), new Vector2(24f, 0f));
        FooterButton(footer.transform, "back", "НАЗАД", new Vector2(1f, 0.5f), new Vector2(-24f, 0f));
    }

    private void CreateContent(Transform window)
    {
        GameObject viewport = CreateUi(window, "Single Scroll Viewport");
        Stretch(viewport.GetComponent<RectTransform>(), 26f, 54f, 78f, 76f);
        Image viewportImage = viewport.AddComponent<Image>(); viewportImage.color = new Color(0f, 0f, 0f, 0.001f); viewportImage.raycastTarget = true;
        viewport.AddComponent<RectMask2D>();
        GameObject content = CreateUi(viewport.transform, "Preferences Content");
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f); contentRect.anchorMax = Vector2.one; contentRect.pivot = new Vector2(0.5f, 1f);
        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 14, 8, 80); layout.spacing = 7f; layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Scrollbar scrollbar = CreateScrollbar(window);
        ScrollRect scrollRect = viewport.AddComponent<ScrollRect>();
        scrollRect.content = contentRect; scrollRect.viewport = viewport.GetComponent<RectTransform>(); scrollRect.horizontal = false; scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped; scrollRect.scrollSensitivity = 32f; scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport; scrollRect.verticalScrollbarSpacing = 8f;

        Section(content.transform, "ЭКРАН");
        DropdownRow(content.transform, ScreenModeId, "Режим экрана", PreferencesOptions.ScreenModes);
        DropdownRow(content.transform, ResolutionId, "Разрешение", PreferencesOptions.Resolutions);
        Section(content.transform, "ЗВУК");
        SliderRow(content.transform, MasterVolumeId, "Общая громкость", 0f, 1f, false, false);
        SliderRow(content.transform, MusicVolumeId, "Музыка", 0f, 1f, false, false);
        SliderRow(content.transform, SfxVolumeId, "Эффекты", 0f, 1f, false, false);
        Section(content.transform, "ТЕКСТ");
        SliderRow(content.transform, TextSpeedId, "Скорость текста", 20f, 100f, true, true);
        SliderRow(content.transform, AutoForwardDelayId, "Скорость авто", 0.5f, 5f, false, true);
        SliderRow(content.transform, TextSizeId, "Размер текста", 0.85f, 1.25f, false, true);
        SliderRow(content.transform, TextboxOpacityId, "Прозрачность окна", 0f, 1f, false, true);
        Section(content.transform, "ИГРА");
        ToggleRow(content.transform, SkipUnseenId, "Пропуск непрочитанного");
        ToggleRow(content.transform, SkipAfterChoicesId, "Продолжать пропуск после выбора");
        ToggleRow(content.transform, AutosaveId, "Автосохранение");
        ToggleRow(content.transform, ShowQuickMenuId, "Показывать быстрое меню");
    }

    private void Section(Transform parent, string value)
    {
        TextMeshProUGUI text = Text(parent, "Section " + value, value, 17f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        text.color = new Color(0.54f, 0.78f, 1f, 1f);
        LayoutElement layout = text.gameObject.AddComponent<LayoutElement>(); layout.minHeight = 30f; layout.preferredHeight = 30f;
    }

    private void DropdownRow(Transform parent, string id, string label, IReadOnlyList<string> options) => dropdowns[id] = Dropdown(Row(parent, id, label), id, options);

    private void ToggleRow(Transform parent, string id, string label)
    {
        Transform control = Row(parent, id, label);
        GameObject owner = CreateUi(control, id + " Toggle");
        Image background = owner.AddComponent<Image>(); background.color = ControlColor;
        Toggle toggle = owner.AddComponent<Toggle>(); toggle.targetGraphic = background; toggle.colors = Colors();
        LayoutElement layout = owner.AddComponent<LayoutElement>(); layout.preferredWidth = 330f; layout.minHeight = 40f; layout.preferredHeight = 40f;
        GameObject mark = CreateUi(owner.transform, "Mark");
        RectTransform markRect = mark.GetComponent<RectTransform>(); markRect.anchorMin = markRect.anchorMax = new Vector2(0f, 0.5f); markRect.pivot = new Vector2(0f, 0.5f); markRect.anchoredPosition = new Vector2(12f, 0f); markRect.sizeDelta = new Vector2(18f, 18f);
        Image markImage = mark.AddComponent<Image>(); markImage.color = AccentColor; toggle.graphic = markImage;
        TextMeshProUGUI value = Text(owner.transform, "Value", "Выкл.", 16f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft); value.color = PrimaryText; Stretch(value.rectTransform, 44f, 10f);
        toggles[id] = toggle; toggleValues[id] = value;
    }

    private void SliderRow(Transform parent, string id, string label, float min, float max, bool wholeNumbers, bool showValue)
    {
        Transform control = Row(parent, id, label);
        GameObject owner = CreateUi(control, id + " Slider");
        HorizontalLayoutGroup layout = owner.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f; layout.childAlignment = TextAnchor.MiddleRight; layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
        LayoutElement ownerLayout = owner.AddComponent<LayoutElement>(); ownerLayout.preferredWidth = 330f; ownerLayout.minHeight = 40f;
        Slider slider = CreateSlider(owner.transform, min, max, wholeNumbers);
        LayoutElement sliderLayout = slider.gameObject.AddComponent<LayoutElement>(); sliderLayout.preferredWidth = showValue ? 220f : 330f; sliderLayout.minHeight = 28f;
        sliders[id] = slider;
        if (!showValue) return;
        TextMeshProUGUI value = Text(owner.transform, "Value", "—", 16f, FontStyles.Normal, TextAlignmentOptions.MidlineRight); value.color = SecondaryText;
        LayoutElement valueLayout = value.gameObject.AddComponent<LayoutElement>(); valueLayout.preferredWidth = 98f; valueLayout.minHeight = 34f; sliderValues[id] = value;
    }

    private Transform Row(Transform parent, string id, string label)
    {
        GameObject row = CreateUi(parent, "Row [" + id + "]"); row.AddComponent<Image>().color = RowColor;
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 7, 7); layout.spacing = 18f; layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
        LayoutElement rowLayout = row.AddComponent<LayoutElement>(); rowLayout.minHeight = 54f; rowLayout.preferredHeight = 54f;
        TextMeshProUGUI labelText = Text(row.transform, "Label", label, 17f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft); labelText.color = PrimaryText; labelText.enableWordWrapping = true; labelText.overflowMode = TextOverflowModes.Ellipsis;
        LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>(); labelLayout.minWidth = 360f; labelLayout.flexibleWidth = 1f;
        GameObject control = CreateUi(row.transform, "Control");
        HorizontalLayoutGroup controlLayout = control.AddComponent<HorizontalLayoutGroup>(); controlLayout.childAlignment = TextAnchor.MiddleRight; controlLayout.childControlWidth = true; controlLayout.childControlHeight = true; controlLayout.childForceExpandWidth = false; controlLayout.childForceExpandHeight = false;
        LayoutElement width = control.AddComponent<LayoutElement>(); width.preferredWidth = 330f; width.minWidth = 330f;
        return control.transform;
    }

    private Button FooterButton(Transform parent, string id, string label, Vector2 anchor, Vector2 position)
    {
        Button button = Button(parent, label + " Button", label, 184f);
        RectTransform rect = button.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = anchor; rect.pivot = anchor; rect.anchoredPosition = position; rect.sizeDelta = new Vector2(184f, 40f);
        buttons[id] = button; return button;
    }

    private Button Button(Transform parent, string name, string label, float width)
    {
        GameObject owner = CreateUi(parent, name); Image image = owner.AddComponent<Image>(); image.color = ControlColor;
        Button button = owner.AddComponent<Button>(); button.targetGraphic = image; button.colors = Colors();
        LayoutElement layout = owner.AddComponent<LayoutElement>(); layout.preferredWidth = width; layout.minHeight = 40f; layout.preferredHeight = 40f;
        TextMeshProUGUI text = Text(owner.transform, "Label", label, 16f, FontStyles.Bold, TextAlignmentOptions.Center); Stretch(text.rectTransform, 10f, 10f);
        return button;
    }

    private TMP_Dropdown Dropdown(Transform parent, string id, IReadOnlyList<string> options)
    {
        GameObject owner = CreateUi(parent, id + " Dropdown"); Image image = owner.AddComponent<Image>(); image.color = ControlColor;
        TMP_Dropdown dropdown = owner.AddComponent<TMP_Dropdown>(); dropdown.targetGraphic = image; dropdown.colors = Colors();
        LayoutElement layout = owner.AddComponent<LayoutElement>(); layout.preferredWidth = 330f; layout.minHeight = 40f; layout.preferredHeight = 40f;
        TextMeshProUGUI caption = Text(owner.transform, "Label", "—", 16f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft); caption.color = PrimaryText; Stretch(caption.rectTransform, 14f, 40f);
        TextMeshProUGUI arrow = Text(owner.transform, "Arrow", "⌄", 20f, FontStyles.Bold, TextAlignmentOptions.Center); arrow.color = SecondaryText;
        RectTransform arrowRect = arrow.rectTransform; arrowRect.anchorMin = new Vector2(1f, 0f); arrowRect.anchorMax = Vector2.one; arrowRect.pivot = new Vector2(1f, 0.5f); arrowRect.sizeDelta = new Vector2(34f, 0f);
        RectTransform template = DropdownTemplate(owner.transform);
        dropdown.template = template; dropdown.captionText = caption; dropdown.itemText = template.GetComponentInChildren<TextMeshProUGUI>(true);
        List<TMP_Dropdown.OptionData> optionData = new List<TMP_Dropdown.OptionData>(); foreach (string option in options) optionData.Add(new TMP_Dropdown.OptionData(option));
        dropdown.AddOptions(optionData); return dropdown;
    }

    private RectTransform DropdownTemplate(Transform parent)
    {
        GameObject owner = CreateUi(parent, "Template"); RectTransform template = owner.GetComponent<RectTransform>();
        template.anchorMin = new Vector2(0f, 0f); template.anchorMax = new Vector2(1f, 0f); template.pivot = new Vector2(0.5f, 1f); template.anchoredPosition = new Vector2(0f, -4f); template.sizeDelta = new Vector2(0f, 156f);
        owner.AddComponent<Image>().color = new Color(0.18f, 0.21f, 0.25f, 1f); ScrollRect scroll = owner.AddComponent<ScrollRect>();
        GameObject viewport = CreateUi(owner.transform, "Viewport"); Stretch(viewport.GetComponent<RectTransform>(), 3f, 3f, 3f, 3f); viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); viewport.AddComponent<RectMask2D>();
        GameObject content = CreateUi(viewport.transform, "Content"); RectTransform contentRect = content.GetComponent<RectTransform>(); contentRect.anchorMin = new Vector2(0f, 1f); contentRect.anchorMax = new Vector2(1f, 1f); contentRect.pivot = new Vector2(0.5f, 1f);
        VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>(); contentLayout.childControlWidth = true; contentLayout.childControlHeight = true; contentLayout.childForceExpandWidth = true; contentLayout.childForceExpandHeight = false;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize; scroll.viewport = viewport.GetComponent<RectTransform>(); scroll.content = contentRect; scroll.horizontal = false;
        GameObject item = CreateUi(content.transform, "Item"); Image itemImage = item.AddComponent<Image>(); itemImage.color = new Color(0.24f, 0.29f, 0.35f, 1f); Toggle toggle = item.AddComponent<Toggle>(); toggle.targetGraphic = itemImage; toggle.colors = Colors(); item.AddComponent<LayoutElement>().preferredHeight = 38f;
        TextMeshProUGUI label = Text(item.transform, "Item Label", "Option", 16f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft); label.color = PrimaryText; Stretch(label.rectTransform, 14f, 34f);
        GameObject checkmark = CreateUi(item.transform, "Item Checkmark"); RectTransform checkRect = checkmark.GetComponent<RectTransform>(); checkRect.anchorMin = checkRect.anchorMax = new Vector2(1f, 0.5f); checkRect.pivot = new Vector2(1f, 0.5f); checkRect.anchoredPosition = new Vector2(-12f, 0f); checkRect.sizeDelta = new Vector2(14f, 14f); Image checkImage = checkmark.AddComponent<Image>(); checkImage.color = AccentColor; toggle.graphic = checkImage;
        owner.SetActive(false); return template;
    }

    private Slider CreateSlider(Transform parent, float min, float max, bool wholeNumbers)
    {
        GameObject owner = CreateUi(parent, "Slider"); Slider slider = owner.AddComponent<Slider>(); slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = wholeNumbers;
        GameObject track = CreateUi(owner.transform, "Track"); Stretch(track.GetComponent<RectTransform>(), 0f, 0f, 12f, 12f); track.AddComponent<Image>().color = new Color(0.10f, 0.12f, 0.15f, 1f);
        GameObject fillArea = CreateUi(owner.transform, "Fill Area"); Stretch(fillArea.GetComponent<RectTransform>(), 6f, 6f, 12f, 12f); GameObject fill = CreateUi(fillArea.transform, "Fill"); Stretch(fill.GetComponent<RectTransform>()); fill.AddComponent<Image>().color = AccentColor;
        GameObject handleArea = CreateUi(owner.transform, "Handle Slide Area"); Stretch(handleArea.GetComponent<RectTransform>(), 6f, 6f); GameObject handle = CreateUi(handleArea.transform, "Handle"); RectTransform handleRect = handle.GetComponent<RectTransform>(); handleRect.sizeDelta = new Vector2(12f, 18f); Image handleImage = handle.AddComponent<Image>(); handleImage.color = new Color(0.92f, 0.96f, 1f, 1f);
        slider.fillRect = fill.GetComponent<RectTransform>(); slider.handleRect = handleRect; slider.targetGraphic = handleImage; slider.direction = Slider.Direction.LeftToRight; slider.colors = Colors(); return slider;
    }

    private Scrollbar CreateScrollbar(Transform window)
    {
        GameObject owner = CreateUi(window, "Scrollbar"); RectTransform rect = owner.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(1f, 0f); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(1f, 0.5f); rect.anchoredPosition = new Vector2(-18f, 0f); rect.sizeDelta = new Vector2(10f, -154f); owner.AddComponent<Image>().color = new Color(0.075f, 0.09f, 0.11f, 0.9f);
        GameObject area = CreateUi(owner.transform, "Sliding Area"); Stretch(area.GetComponent<RectTransform>(), 2f, 2f, 2f, 2f); GameObject handle = CreateUi(area.transform, "Handle"); Stretch(handle.GetComponent<RectTransform>()); Image handleImage = handle.AddComponent<Image>(); handleImage.color = new Color(0.48f, 0.63f, 0.79f, 0.94f);
        Scrollbar scrollbar = owner.AddComponent<Scrollbar>(); scrollbar.handleRect = handle.GetComponent<RectTransform>(); scrollbar.targetGraphic = handleImage; scrollbar.direction = Scrollbar.Direction.BottomToTop; return scrollbar;
    }

    private void BindToggle(string id, Action<bool> apply, Func<bool, string> label = null) { toggles[id].onValueChanged.AddListener(value => apply(value)); toggles[id].onValueChanged.AddListener(value => SetToggle(id, value, label?.Invoke(value))); }
    private void BindSlider(string id, Action<float> apply, Func<float, string> label = null) { sliders[id].onValueChanged.AddListener(value => apply(value)); if (label != null) sliders[id].onValueChanged.AddListener(value => SetSliderValue(id, label(value))); }
    private string DropdownValue(string id, int index) { TMP_Dropdown dropdown = GetDropdown(id); return dropdown != null && index >= 0 && index < dropdown.options.Count ? dropdown.options[index].text : string.Empty; }
    private void SetDropdown(string id, string value) { if (!dropdowns.TryGetValue(id, out TMP_Dropdown dropdown)) return; int index = dropdown.options.FindIndex(option => option.text == value); dropdown.SetValueWithoutNotify(index >= 0 ? index : 0); dropdown.RefreshShownValue(); }
    private void SetToggle(string id, bool value, string label = null) { if (toggles.TryGetValue(id, out Toggle toggle)) toggle.SetIsOnWithoutNotify(value); if (toggleValues.TryGetValue(id, out TextMeshProUGUI text)) text.text = label ?? (value ? "Вкл." : "Выкл."); }
    private void SetSlider(string id, float value, string label = null) { if (sliders.TryGetValue(id, out Slider slider)) slider.SetValueWithoutNotify(value); SetSliderValue(id, label); }
    private void SetSliderValue(string id, string value) { if (sliderValues.TryGetValue(id, out TextMeshProUGUI text)) text.text = value ?? string.Empty; }
    private static GameObject CreateUi(Transform parent, string name) { GameObject result = new GameObject(name, typeof(RectTransform)); result.layer = parent.gameObject.layer; result.transform.SetParent(parent, false); return result; }
    private static TextMeshProUGUI Text(Transform parent, string name, string value, float size, FontStyles style, TextAlignmentOptions alignment) { TextMeshProUGUI text = CreateUi(parent, name).AddComponent<TextMeshProUGUI>(); text.text = value; text.fontSize = size; text.fontStyle = style; text.alignment = alignment; text.color = PrimaryText; text.enableWordWrapping = false; text.raycastTarget = false; return text; }
    private static ColorBlock Colors() { ColorBlock colors = ColorBlock.defaultColorBlock; colors.normalColor = Color.white; colors.highlightedColor = new Color(0.74f, 0.88f, 1f, 1f); colors.pressedColor = new Color(0.48f, 0.67f, 0.85f, 1f); colors.selectedColor = new Color(0.64f, 0.82f, 1f, 1f); colors.disabledColor = new Color(0.45f, 0.48f, 0.54f, 0.68f); colors.colorMultiplier = 1f; return colors; }
    private static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float bottom = 0f, float top = 0f) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); }
}
