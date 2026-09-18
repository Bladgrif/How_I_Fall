using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine.EventSystems;
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

    private static readonly Color WindowColor = new Color(0.018f, 0.041f, 0.056f, 0.925f);
    private static readonly Color HeaderColor = new Color(0.025f, 0.060f, 0.078f, 0.30f);
    private static readonly Color RowColor = Color.clear;
    private static readonly Color FocusedRowColor = new Color(0.20f, 0.52f, 0.70f, 0.20f);
    private static readonly Color ControlColor = new Color(0.035f, 0.090f, 0.12f, 0.78f);
    private static readonly Color AccentColor = new Color(0.42f, 0.78f, 0.96f, 1f);
    private static readonly Color PrimaryText = new Color(0.97f, 0.98f, 1f, 1f);
    private static readonly Color SecondaryText = new Color(0.74f, 0.80f, 0.88f, 1f);

    private readonly Dictionary<string, Slider> sliders = new Dictionary<string, Slider>();
    private readonly Dictionary<string, TextMeshProUGUI> sliderValues = new Dictionary<string, TextMeshProUGUI>();
    private readonly Dictionary<string, Toggle> toggles = new Dictionary<string, Toggle>();
    private readonly Dictionary<string, TextMeshProUGUI> toggleValues = new Dictionary<string, TextMeshProUGUI>();
    private readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>();
    private readonly Dictionary<string, TMP_Dropdown> dropdowns = new Dictionary<string, TMP_Dropdown>();
    private readonly Dictionary<string, TextMeshProUGUI> cycleValues = new Dictionary<string, TextMeshProUGUI>();
    private readonly Dictionary<string, Button> cyclePreviousButtons = new Dictionary<string, Button>();
    private readonly Dictionary<string, IReadOnlyList<string>> cycleOptions = new Dictionary<string, IReadOnlyList<string>>();
    private readonly Dictionary<string, int> cycleIndices = new Dictionary<string, int>();
    private readonly List<GameObject> categories = new List<GameObject>();
    private readonly List<Button> categoryButtons = new List<Button>();
    private readonly Dictionary<Selectable, string> contextualHints = new Dictionary<Selectable, string>();
    public int ActiveCategory { get; private set; }
    private GameObject root;
    private PreferencesController controller;
    private TMP_Dropdown activeDropdown;
    private int dropdownClosedFrame = -1;
    private bool isBound;
    private Image focusedRow;
    private TextMeshProUGUI contextualHintText;

    public string ContextId { get; private set; }
    public static IReadOnlyList<string> VisibleControlIds => ControlIds;
    public bool IsVisible => root != null && root.activeSelf;
    public bool IsAnyDropdownExpanded => dropdowns.Values.Any(dropdown => dropdown != null && dropdown.IsExpanded);
    /// <summary>Prevents a Back press that closes a dropdown from also closing the parent modal.</summary>
    public bool IsHandlingDropdownCancel => IsAnyDropdownExpanded || Time.frameCount == dropdownClosedFrame;
    public string CurrentContextHint => contextualHintText != null ? contextualHintText.text : string.Empty;

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
    public Button GetCyclePreviousButton(string id) => cyclePreviousButtons.TryGetValue(id, out Button value) ? value : null;
    public TMP_Dropdown GetDropdown(string id) => dropdowns.TryGetValue(id, out TMP_Dropdown value) ? value : null;
    public string GetDisplayedValue(string id)
    {
        if (sliderValues.TryGetValue(id, out TextMeshProUGUI sliderValue)) return sliderValue.text;
        if (cycleValues.TryGetValue(id, out TextMeshProUGUI cycleValue)) return cycleValue.text;
        if (toggleValues.TryGetValue(id, out TextMeshProUGUI toggleValue)) return toggleValue.text;
        return dropdowns.TryGetValue(id, out TMP_Dropdown dropdown) && dropdown.options.Count > dropdown.value
            ? dropdown.options[dropdown.value].text : string.Empty;
    }

    private void Update()
    {
        TMP_Dropdown expanded = dropdowns.Values.FirstOrDefault(dropdown => dropdown != null && dropdown.IsExpanded);
        if (expanded != null)
        {
            activeDropdown = expanded;
            return;
        }

        if (activeDropdown != null)
        {
            Focus(activeDropdown);
            activeDropdown = null;
            dropdownClosedFrame = Time.frameCount;
        }

        RefreshFocusedPresentation();
    }

    public void Bind(PreferencesController sharedController)
    {
        controller = sharedController;
        if (isBound || controller == null) return;
        isBound = true;
        buttons["reset"].onClick.AddListener(controller.Reset);
        buttons["back"].onClick.AddListener(controller.Close);
        buttons["apply"].onClick.AddListener(controller.Apply);
        BindDropdown(ScreenModeId, controller.SetScreenMode);
        BindDropdown(ResolutionId, controller.SetResolution);
        BindToggle(SkipUnseenId, controller.SetSkipUnseen, value => value ? "Вкл. — можно всё" : "Выкл. — только виденное");
        BindToggle(SkipAfterChoicesId, controller.SetSkipAfterChoices);
        BindToggle(AutosaveId, controller.SetAutoSave);
        BindToggle(ShowQuickMenuId, controller.SetShowQuickMenu);
        BindSlider(MasterVolumeId, controller.SetMasterVolume, PreferencesFormatting.Percent);
        BindSlider(MusicVolumeId, controller.SetMusicVolume, PreferencesFormatting.Percent);
        BindSlider(SfxVolumeId, controller.SetSfxVolume, PreferencesFormatting.Percent);
        BindSlider(TextSpeedId, controller.SetTextSpeed, PreferencesFormatting.TextSpeed);
        sliders[AutoForwardDelayId].onValueChanged.AddListener(value => controller.SetAutoForwardDelay(PreferencesFormatting.AutoForwardDelayStored(value)));
        sliders[AutoForwardDelayId].onValueChanged.AddListener(value => SetSliderValue(AutoForwardDelayId, PreferencesFormatting.AutoForwardDelay(PreferencesFormatting.AutoForwardDelayStored(value))));
        BindCycle(TextSizeId, value => controller.SetDialogueTextScale(PreferencesFormatting.TextScaleValue(value)));
        BindSlider(TextboxOpacityId, controller.SetTextboxOpacity, PreferencesFormatting.Percent);
    }

    public void SetVisible(bool visible)
    {
        if (root == null) return;
        root.SetActive(visible);
        if (visible)
        {
            root.transform.SetAsLastSibling();
            SelectCategory(0);
            FocusDefaultControl();
        }
    }

    /// <summary>Assigns deterministic keyboard/controller focus when this modal opens.</summary>
    public void FocusDefaultControl()
    {
        Focus(categoryButtons[0]);
    }

    private static void Focus(Selectable control)
    {
        if (control == null || !control.isActiveAndEnabled || !control.interactable)
        {
            return;
        }

        EventSystem eventSystem = EventSystem.current ?? FindFirstObjectByType<EventSystem>();
        eventSystem?.SetSelectedGameObject(control.gameObject);
    }

    public void Refresh(PreferencesState settings)
    {
        bool applyWasSelected = EventSystem.current != null
            && EventSystem.current.currentSelectedGameObject == buttons["apply"].gameObject;
        buttons["apply"].interactable = controller != null && controller.IsDirty;
        if (!buttons["apply"].interactable && applyWasSelected) Focus(buttons["back"]);
        SetDropdown(ScreenModeId, settings.screenMode);
        RefreshResolutionOptions(settings);
        SetDropdown(ResolutionId, settings.resolution);
        SetToggle(SkipUnseenId, settings.skipMode == "Всё", settings.skipMode == "Всё" ? "Вкл. — можно всё" : "Выкл. — только виденное");
        SetToggle(SkipAfterChoicesId, settings.skipAfterChoices);
        SetToggle(AutosaveId, settings.autoSave);
        SetToggle(ShowQuickMenuId, settings.showQuickMenu);
        SetSlider(MasterVolumeId, settings.masterVolume, PreferencesFormatting.Percent(settings.masterVolume));
        SetSlider(MusicVolumeId, settings.musicVolume, PreferencesFormatting.Percent(settings.musicVolume));
        SetSlider(SfxVolumeId, settings.sfxVolume, PreferencesFormatting.Percent(settings.sfxVolume));
        SetSlider(TextSpeedId, settings.textSpeed, PreferencesFormatting.TextSpeed(settings.textSpeed));
        SetSlider(AutoForwardDelayId, PreferencesFormatting.AutoForwardDelaySeconds(settings.autoForwardDelay), PreferencesFormatting.AutoForwardDelay(settings.autoForwardDelay));
        SetCycle(TextSizeId, PreferencesFormatting.TextScaleLabel(settings.dialogueTextScale));
        SetSlider(TextboxOpacityId, settings.textboxOpacity, PreferencesFormatting.Percent(settings.textboxOpacity));
    }

    /// <summary>
    /// Windowed lists only resolutions that fit the current desktop, so the visible draft
    /// always matches the value Apply persists.
    /// </summary>
    private void RefreshResolutionOptions(PreferencesState settings)
    {
        if (!dropdowns.TryGetValue(ResolutionId, out TMP_Dropdown dropdown) || dropdown == null) return;
        IReadOnlyList<string> allowed = controller != null
            ? controller.GetResolutionOptions(settings.screenMode)
            : PreferencesOptions.Resolutions;
        if (dropdown.options.Count == allowed.Count)
        {
            bool identical = true;
            for (int i = 0; i < allowed.Count; i++)
            {
                if (dropdown.options[i].text != allowed[i])
                {
                    identical = false;
                    break;
                }
            }

            if (identical) return;
        }

        dropdown.ClearOptions();
        List<TMP_Dropdown.OptionData> optionData = new List<TMP_Dropdown.OptionData>();
        foreach (string option in allowed) optionData.Add(new TMP_Dropdown.OptionData(option));
        dropdown.AddOptions(optionData);
    }

    private void Build(string contextId)
    {
        ContextId = contextId;
        root = gameObject;
        Stretch(root.GetComponent<RectTransform>());
        Image dim = root.AddComponent<Image>();
        dim.color = new Color(0.012f, 0.022f, 0.030f, 0.68f);
        dim.raycastTarget = true;

        GameObject window = CreateUi(root.transform, "Preferences Window");
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.anchoredPosition = new Vector2(0f, 6f);
        windowRect.sizeDelta = new Vector2(1760f, 980f);
        window.AddComponent<Image>().color = WindowColor;
        CreateHeader(window.transform);
        CreateFooter(window.transform);
        CreateContent(window.transform);
        root.SetActive(false);
    }

    private void CreateHeader(Transform window)
    {
        GameObject header = CreateUi(window, "Header");
        RectTransform rect = header.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0.5f, 1f); rect.sizeDelta = new Vector2(0f, 126f);
        header.AddComponent<Image>().color = Color.clear;
        TextMeshProUGUI title = Text(header.transform, "Title", "НАСТРОЙКИ", 52f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        title.characterSpacing = 4f;
        Stretch(title.rectTransform, 58f, 58f, 18f, 8f);
        GameObject accent = CreateUi(header.transform, "Accent");
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 0f); accentRect.anchorMax = new Vector2(0.40f, 0f); accentRect.pivot = new Vector2(0f, 0f); accentRect.anchoredPosition = new Vector2(58f, 0f); accentRect.sizeDelta = new Vector2(-58f, 2f);
        accent.AddComponent<Image>().color = new Color(0.42f, 0.78f, 0.96f, 0.64f);
    }

    private void CreateFooter(Transform window)
    {
        GameObject footer = CreateUi(window, "Footer");
        RectTransform rect = footer.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(1f, 0f); rect.pivot = new Vector2(0.5f, 0f); rect.sizeDelta = new Vector2(0f, 92f);
        footer.AddComponent<Image>().color = HeaderColor;
        RegisterHint(FooterButton(footer.transform, "reset", "СБРОСИТЬ", new Vector2(0f, 0.5f), new Vector2(56f, 0f)), "Вернуть значения черновика к рекомендуемым по умолчанию.");
        RegisterHint(FooterButton(footer.transform, "back", "НАЗАД", new Vector2(1f, 0.5f), new Vector2(-260f, 0f)), "Закрыть настройки без применения изменений черновика.");
        RegisterHint(FooterButton(footer.transform, "apply", "ПРИМЕНИТЬ", new Vector2(1f, 0.5f), new Vector2(-56f, 0f)), "Сохранить изменения черновика и оставить настройки открытыми.");
    }

    private void CreateContent(Transform window)
    {
        GameObject content = CreateUi(window, "Preferences Categories");
        Stretch(content.GetComponent<RectTransform>(), 58f, 58f, 150f, 140f);
        GameObject railObject = CreateUi(content.transform, "Category Tabs");
        Transform rail = railObject.transform;
        RectTransform railRect = railObject.GetComponent<RectTransform>();
        railRect.anchorMin = new Vector2(0f, 1f); railRect.anchorMax = new Vector2(1f, 1f);
        railRect.pivot = new Vector2(0.5f, 1f); railRect.anchoredPosition = Vector2.zero; railRect.sizeDelta = new Vector2(0f, 76f);
        HorizontalLayoutGroup tabLayout = railObject.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 18f; tabLayout.childAlignment = TextAnchor.MiddleCenter;
        tabLayout.childControlWidth = true; tabLayout.childControlHeight = true;
        tabLayout.childForceExpandWidth = true; tabLayout.childForceExpandHeight = true;
        string[] names = { "Экран", "Звук", "Текст", "Игра" };
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            Button button = Button(rail, "Category " + i, names[i].ToUpperInvariant(), 280f);
            button.colors = CategoryColors();
            button.GetComponent<LayoutElement>().preferredHeight = 64f;
            TextMeshProUGUI tabLabel = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tabLabel != null) { tabLabel.fontSize = 22f; tabLabel.characterSpacing = 2.5f; }
            Outline tabOutline = button.gameObject.AddComponent<Outline>();
            tabOutline.effectDistance = new Vector2(1f, -1f);
            RegisterHint(button, CategoryHint(i));
            categoryButtons.Add(button);
            buttons["category_" + i] = button;
            button.onClick.AddListener(() => SelectCategory(index));
            Transform page = CreateColumn(content.transform, "Category Content " + i);
            Stretch((RectTransform)page, 24f, 24f, 0f, 102f);
            page.GetComponent<VerticalLayoutGroup>().spacing = 10f;
            categories.Add(page.gameObject);
            Section(page, names[i].ToUpperInvariant());
        }
        Transform screen = categories[0].transform;
        DropdownRow(screen, ScreenModeId, "Режим экрана", PreferencesOptions.ScreenModes, "Выберите полноэкранный или оконный режим. Изменение применится только после «Применить».");
        DropdownRow(screen, ResolutionId, "Разрешение", PreferencesOptions.Resolutions, "Выберите удобный размер окна. В оконном режиме показываются только значения, которые помещаются на рабочий стол.");
        Transform audio = categories[1].transform;
        SliderRow(audio, MasterVolumeId, "Общая громкость", 0f, 1f, false, true, "Общий уровень звука. Слева тише, справа громче.");
        SliderRow(audio, MusicVolumeId, "Музыка", 0f, 1f, false, true, "Громкость музыкального сопровождения.");
        SliderRow(audio, SfxVolumeId, "Эффекты", 0f, 1f, false, true, "Громкость интерфейсных и сценических эффектов.");
        Transform text = categories[2].transform;
        SliderRow(text, TextSpeedId, "Скорость текста", 20f, 100f, true, true, "Как быстро проявляется текст: слева спокойнее, справа быстрее.");
        SliderRow(text, AutoForwardDelayId, "Задержка авто", 0.5f, 5f, false, true, "Сколько времени Auto ждёт после полной строки.");
        CycleRow(text, TextSizeId, "Размер текста", PreferencesFormatting.TextScaleLabels, "Размер реплик в окне диалога.");
        SliderRow(text, TextboxOpacityId, "Прозрачность окна", 0f, 1f, false, true, "Прозрачность фона текста: слева легче видеть сцену, справа выше контраст.");
        Transform game = categories[3].transform;
        ToggleRow(game, SkipUnseenId, "Пропуск непрочитанного", "Разрешить Skip для текста, который ещё не был прочитан.");
        ToggleRow(game, SkipAfterChoicesId, "Пропуск после выбора", "Продолжать Skip после сделанного выбора.");
        ToggleRow(game, AutosaveId, "Автосохранение", "Автоматически сохранять прогресс в поддерживаемых точках.");
        ToggleRow(game, ShowQuickMenuId, "Быстрое меню", "Показывать компактные команды чтения во время игры.");
        CreateContextualHint(window.transform);
        SelectCategory(0);
    }

    public void SelectCategory(int index)
    {
        if (index < 0 || index >= categories.Count) return;
        foreach (TMP_Dropdown dropdown in dropdowns.Values) dropdown.Hide();
        activeDropdown = null;
        ActiveCategory = index;
        for (int i = 0; i < categories.Count; i++)
        {
            categories[i].SetActive(i == index);
            categoryButtons[i].GetComponent<Image>().color = i == index
                ? new Color(0.10f, 0.27f, 0.36f, 0.50f)
                : new Color(0.025f, 0.07f, 0.095f, 0.14f);
            Outline tabOutline = categoryButtons[i].GetComponent<Outline>();
            if (tabOutline != null)
            {
                tabOutline.effectColor = i == index
                    ? new Color(0.48f, 0.84f, 1f, 0.74f)
                    : new Color(0.22f, 0.40f, 0.52f, 0.08f);
            }
        }
        Selectable[] controls = categories[index].GetComponentsInChildren<Selectable>()
            .Where(control => control.gameObject.activeInHierarchy).ToArray();
        for (int i = 0; i < categoryButtons.Count; i++)
        {
            Navigation nav = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = categoryButtons[Mathf.Max(0, i - 1)],
                selectOnDown = i + 1 < categoryButtons.Count ? categoryButtons[i + 1] : buttons["reset"],
                selectOnRight = controls.FirstOrDefault() };
            categoryButtons[i].navigation = nav;
        }
        for (int i = 0; i < controls.Length; i++)
        {
            Navigation nav = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = i == 0 ? categoryButtons[index] : controls[i - 1],
                selectOnDown = i + 1 < controls.Length ? controls[i + 1] : buttons["back"] };
            if (!(controls[i] is Slider)) nav.selectOnLeft = categoryButtons[index];
            if (i + 1 < controls.Length && controls[i] is Button) nav.selectOnRight = controls[i + 1];
            controls[i].navigation = nav;
        }
        Button[] footer = { buttons["reset"], buttons["back"], buttons["apply"] };
        for (int i = 0; i < footer.Length; i++)
            footer[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = i > 0 ? footer[i - 1] : null,
                selectOnRight = i + 1 < footer.Length ? footer[i + 1] : null,
                selectOnUp = controls.LastOrDefault() ?? categoryButtons[index] };
        Focus(categoryButtons[index]);
    }

    private void CreateContextualHint(Transform window)
    {
        GameObject hint = CreateUi(window, "Focused Control Hint");
        RectTransform rect = hint.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f); rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f); rect.anchoredPosition = new Vector2(0f, 96f);
        rect.sizeDelta = new Vector2(-116f, 42f);
        Image background = hint.AddComponent<Image>();
        background.color = new Color(0.06f, 0.13f, 0.17f, 0.62f);
        contextualHintText = Text(hint.transform, "Text", "Выберите раздел, чтобы увидеть доступные параметры.", 16f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        contextualHintText.color = SecondaryText;
        Stretch(contextualHintText.rectTransform, 16f, 16f);
    }

    private void RegisterHint(Selectable control, string hint)
    {
        if (control != null) contextualHints[control] = hint;
    }

    private static string CategoryHint(int index)
    {
        return index switch
        {
            0 => "Режим экрана и размер окна. Изменения остаются в черновике до применения.",
            1 => "Настройте баланс звука для чтения и сцен.",
            2 => "Сделайте темп и читаемость текста комфортными для себя.",
            _ => "Параметры поведения Skip, автосохранения и компактного меню."
        };
    }

    private void RefreshFocusedPresentation()
    {
        EventSystem eventSystem = EventSystem.current ?? FindFirstObjectByType<EventSystem>();
        Selectable selected = eventSystem != null && eventSystem.currentSelectedGameObject != null
            ? eventSystem.currentSelectedGameObject.GetComponent<Selectable>()
            : null;
        if (selected == null || !contextualHints.TryGetValue(selected, out string hint)) return;

        if (contextualHintText != null) contextualHintText.text = hint;
        Image row = FindRowImage(selected.transform);
        if (row == focusedRow) return;
        if (focusedRow != null) focusedRow.color = RowColor;
        focusedRow = row;
        if (focusedRow != null) focusedRow.color = FocusedRowColor;
    }

    private static Image FindRowImage(Transform control)
    {
        for (Transform current = control; current != null; current = current.parent)
        {
            if (current.name.StartsWith("Row [", StringComparison.Ordinal)) return current.GetComponent<Image>();
        }
        return null;
    }

    private static Transform CreateColumn(Transform parent, string name)
    {
        GameObject column = CreateUi(parent, name);
        VerticalLayoutGroup layout = column.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0); layout.spacing = 6f; layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        LayoutElement element = column.AddComponent<LayoutElement>(); element.flexibleWidth = 1f; element.flexibleHeight = 1f;
        return column.transform;
    }

    private void Section(Transform parent, string value)
    {
        TextMeshProUGUI text = Text(parent, "Section " + value, value, 30f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        text.color = new Color(0.54f, 0.78f, 1f, 1f);
        LayoutElement layout = text.gameObject.AddComponent<LayoutElement>(); layout.minHeight = 54f; layout.preferredHeight = 54f;
    }

    private void CycleRow(Transform parent, string id, string label, IReadOnlyList<string> options, string hint)
    {
        Transform control = Row(parent, id, label);
        GameObject owner = CreateUi(control, id + " Selector");
        HorizontalLayoutGroup layout = owner.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 4f; layout.childAlignment = TextAnchor.MiddleRight; layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
        LayoutElement ownerLayout = owner.AddComponent<LayoutElement>(); ownerLayout.preferredWidth = 380f; ownerLayout.minHeight = 42f;
        Button previous = Button(owner.transform, "Previous", "‹", 34f);
        TextMeshProUGUI value = Text(owner.transform, "Value", "—", 16f, FontStyles.Normal, TextAlignmentOptions.Center); value.color = PrimaryText;
        LayoutElement valueLayout = value.gameObject.AddComponent<LayoutElement>(); valueLayout.preferredWidth = 292f; valueLayout.minHeight = 42f;
        Button next = Button(owner.transform, "Next", "›", 34f);
        buttons[id] = next; cyclePreviousButtons[id] = previous;
        RegisterHint(previous, hint); RegisterHint(next, hint);
        cycleValues[id] = value; cycleOptions[id] = options; cycleIndices[id] = 0;
        previous.onClick.AddListener(() => Cycle(id, -1));
        next.onClick.AddListener(() => Cycle(id, 1));
    }

    private void DropdownRow(Transform parent, string id, string label, IReadOnlyList<string> options, string hint)
    {
        Transform control = Row(parent, id, label);
        dropdowns[id] = Dropdown(control, id, options);
        RegisterHint(dropdowns[id], hint);
    }

    private void ToggleRow(Transform parent, string id, string label, string hint)
    {
        Transform control = Row(parent, id, label);
        GameObject owner = CreateUi(control, id + " Toggle");
        Image background = owner.AddComponent<Image>(); background.color = ControlColor;
        Toggle toggle = owner.AddComponent<Toggle>(); toggle.targetGraphic = background; toggle.colors = Colors();
        LayoutElement layout = owner.AddComponent<LayoutElement>(); layout.preferredWidth = 380f; layout.minHeight = 40f; layout.preferredHeight = 40f;
        GameObject mark = CreateUi(owner.transform, "Mark");
        RectTransform markRect = mark.GetComponent<RectTransform>(); markRect.anchorMin = markRect.anchorMax = new Vector2(0f, 0.5f); markRect.pivot = new Vector2(0f, 0.5f); markRect.anchoredPosition = new Vector2(12f, 0f); markRect.sizeDelta = new Vector2(18f, 18f);
        Image markImage = mark.AddComponent<Image>(); markImage.color = AccentColor; toggle.graphic = markImage;
        TextMeshProUGUI value = Text(owner.transform, "Value", "Выкл.", 16f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft); value.color = PrimaryText; Stretch(value.rectTransform, 44f, 10f);
        toggles[id] = toggle; toggleValues[id] = value;
        RegisterHint(toggle, hint);
    }

    private void SliderRow(Transform parent, string id, string label, float min, float max, bool wholeNumbers, bool showValue, string hint)
    {
        Transform control = Row(parent, id, label);
        control.GetComponent<LayoutElement>().preferredWidth = 520f;
        control.GetComponent<LayoutElement>().minWidth = 520f;
        GameObject owner = CreateUi(control, id + " Slider");
        HorizontalLayoutGroup layout = owner.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f; layout.childAlignment = TextAnchor.MiddleRight; layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
        LayoutElement ownerLayout = owner.AddComponent<LayoutElement>(); ownerLayout.preferredWidth = 520f; ownerLayout.minHeight = 40f;
        Slider slider = CreateSlider(owner.transform, min, max, wholeNumbers);
        LayoutElement sliderLayout = slider.gameObject.AddComponent<LayoutElement>(); sliderLayout.preferredWidth = showValue ? 368f : 520f; sliderLayout.minHeight = 36f;
        sliders[id] = slider;
        RegisterHint(slider, hint);
        if (!showValue) return;
        TextMeshProUGUI value = Text(owner.transform, "Value", "—", 16f, FontStyles.Normal, TextAlignmentOptions.MidlineRight); value.color = SecondaryText;
        LayoutElement valueLayout = value.gameObject.AddComponent<LayoutElement>(); valueLayout.preferredWidth = 128f; valueLayout.minHeight = 34f; sliderValues[id] = value;
    }

    private Transform Row(Transform parent, string id, string label)
    {
        GameObject row = CreateUi(parent, "Row [" + id + "]"); row.AddComponent<Image>().color = RowColor;
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 8, 8); layout.spacing = 28f; layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
        LayoutElement rowLayout = row.AddComponent<LayoutElement>(); rowLayout.minHeight = 82f; rowLayout.preferredHeight = 82f;
        TextMeshProUGUI labelText = Text(row.transform, "Label", label, 23f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft); labelText.color = PrimaryText; labelText.enableWordWrapping = true; labelText.overflowMode = TextOverflowModes.Ellipsis;
        LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>(); labelLayout.minWidth = 340f; labelLayout.flexibleWidth = 1f;
        GameObject control = CreateUi(row.transform, "Control");
        HorizontalLayoutGroup controlLayout = control.AddComponent<HorizontalLayoutGroup>(); controlLayout.childAlignment = TextAnchor.MiddleRight; controlLayout.childControlWidth = true; controlLayout.childControlHeight = true; controlLayout.childForceExpandWidth = false; controlLayout.childForceExpandHeight = false;
        LayoutElement width = control.AddComponent<LayoutElement>(); width.preferredWidth = 380f; width.minWidth = 380f;
        return control.transform;
    }

    private Button FooterButton(Transform parent, string id, string label, Vector2 anchor, Vector2 position)
    {
        Button button = Button(parent, label + " Button", label, 184f);
        RectTransform rect = button.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = anchor; rect.pivot = anchor; rect.anchoredPosition = position; rect.sizeDelta = new Vector2(188f, 44f);
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
        LayoutElement layout = owner.AddComponent<LayoutElement>(); layout.preferredWidth = 380f; layout.minHeight = 44f; layout.preferredHeight = 44f;
        TextMeshProUGUI caption = Text(owner.transform, "Label", "—", 18f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft); caption.color = PrimaryText; Stretch(caption.rectTransform, 16f, 44f);
        CreateDropdownChevron(owner.transform);
        RectTransform template = DropdownTemplate(owner.transform);
        dropdown.template = template; dropdown.captionText = caption; dropdown.itemText = template.GetComponentInChildren<TextMeshProUGUI>(true);
        List<TMP_Dropdown.OptionData> optionData = new List<TMP_Dropdown.OptionData>(); foreach (string option in options) optionData.Add(new TMP_Dropdown.OptionData(option));
        dropdown.AddOptions(optionData); return dropdown;
    }

    private static void CreateDropdownChevron(Transform parent)
    {
        GameObject owner = CreateUi(parent, "Dropdown Chevron");
        RectTransform ownerRect = owner.GetComponent<RectTransform>();
        ownerRect.anchorMin = new Vector2(1f, 0.5f); ownerRect.anchorMax = new Vector2(1f, 0.5f);
        ownerRect.pivot = new Vector2(1f, 0.5f); ownerRect.anchoredPosition = new Vector2(-18f, 0f); ownerRect.sizeDelta = new Vector2(18f, 12f);
        CreateChevronArm(owner.transform, "Left Arm", new Vector2(4.5f, 7f), -42f);
        CreateChevronArm(owner.transform, "Right Arm", new Vector2(13.5f, 7f), 42f);
    }

    private static void CreateChevronArm(Transform parent, string name, Vector2 position, float angle)
    {
        GameObject arm = CreateUi(parent, name); RectTransform rect = arm.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f); rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position; rect.sizeDelta = new Vector2(9f, 2f); rect.localEulerAngles = new Vector3(0f, 0f, angle);
        Image image = arm.AddComponent<Image>(); image.color = SecondaryText; image.raycastTarget = false;
    }

    private RectTransform DropdownTemplate(Transform parent)
    {
        GameObject owner = CreateUi(parent, "Template"); RectTransform template = owner.GetComponent<RectTransform>();
        template.anchorMin = new Vector2(0f, 0f); template.anchorMax = new Vector2(1f, 0f); template.pivot = new Vector2(0.5f, 1f); template.anchoredPosition = new Vector2(0f, -4f); template.sizeDelta = new Vector2(0f, 176f);
        owner.AddComponent<Image>().color = new Color(0.18f, 0.21f, 0.25f, 1f); ScrollRect scroll = owner.AddComponent<ScrollRect>();
        GameObject viewport = CreateUi(owner.transform, "Viewport"); Stretch(viewport.GetComponent<RectTransform>(), 3f, 3f); viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); viewport.AddComponent<RectMask2D>();
        GameObject content = CreateUi(viewport.transform, "Content"); RectTransform contentRect = content.GetComponent<RectTransform>(); contentRect.anchorMin = new Vector2(0f, 1f); contentRect.anchorMax = new Vector2(1f, 1f); contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 40f);
        scroll.viewport = viewport.GetComponent<RectTransform>(); scroll.content = contentRect; scroll.horizontal = false;
        GameObject item = CreateUi(content.transform, "Item"); Image itemImage = item.AddComponent<Image>(); itemImage.color = new Color(0.24f, 0.29f, 0.35f, 1f); Toggle toggle = item.AddComponent<Toggle>(); toggle.targetGraphic = itemImage; toggle.colors = Colors(); RectTransform itemRect = item.GetComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0f, 0.5f); itemRect.anchorMax = new Vector2(1f, 0.5f); itemRect.sizeDelta = new Vector2(0f, 34f);
        TextMeshProUGUI label = Text(item.transform, "Item Label", "Option", 16f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft); label.color = PrimaryText; Stretch(label.rectTransform, 14f, 34f);
        GameObject checkmark = CreateUi(item.transform, "Item Checkmark"); RectTransform checkRect = checkmark.GetComponent<RectTransform>(); checkRect.anchorMin = checkRect.anchorMax = new Vector2(1f, 0.5f); checkRect.pivot = new Vector2(1f, 0.5f); checkRect.anchoredPosition = new Vector2(-12f, 0f); checkRect.sizeDelta = new Vector2(14f, 14f); Image checkImage = checkmark.AddComponent<Image>(); checkImage.color = AccentColor; toggle.graphic = checkImage;
        owner.SetActive(false); return template;
    }

    private Slider CreateSlider(Transform parent, float min, float max, bool wholeNumbers)
    {
        GameObject owner = CreateUi(parent, "Slider"); owner.AddComponent<Image>().color = Color.clear; Slider slider = owner.AddComponent<Slider>(); slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = wholeNumbers;
        GameObject track = CreateUi(owner.transform, "Track"); Stretch(track.GetComponent<RectTransform>(), 0f, 0f, 14f, 14f); track.AddComponent<Image>().color = new Color(0.10f, 0.12f, 0.15f, 1f);
        GameObject fillArea = CreateUi(owner.transform, "Fill Area"); Stretch(fillArea.GetComponent<RectTransform>(), 6f, 6f, 14f, 14f); GameObject fill = CreateUi(fillArea.transform, "Fill"); Stretch(fill.GetComponent<RectTransform>()); fill.AddComponent<Image>().color = AccentColor;
        GameObject handleArea = CreateUi(owner.transform, "Handle Slide Area"); Stretch(handleArea.GetComponent<RectTransform>(), 6f, 6f, 7f, 7f); GameObject handle = CreateUi(handleArea.transform, "Handle"); RectTransform handleRect = handle.GetComponent<RectTransform>(); handleRect.anchorMin = handleRect.anchorMax = new Vector2(0f, 0.5f); handleRect.sizeDelta = new Vector2(12f, -10f); Image handleImage = handle.AddComponent<Image>(); handleImage.color = new Color(0.92f, 0.96f, 1f, 1f);
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
    private void BindDropdown(string id, Action<string> apply) { dropdowns[id].onValueChanged.AddListener(index => apply(dropdowns[id].options[index].text)); }
    private void BindCycle(string id, Action<string> apply) { buttons[id].onClick.AddListener(() => apply(CycleValue(id))); cyclePreviousButtons[id].onClick.AddListener(() => apply(CycleValue(id))); }
    private void Cycle(string id, int direction) { if (!cycleOptions.TryGetValue(id, out IReadOnlyList<string> options) || options.Count == 0) return; cycleIndices[id] = (cycleIndices[id] + direction + options.Count) % options.Count; SetCycleText(id); }
    private string CycleValue(string id) => cycleOptions.TryGetValue(id, out IReadOnlyList<string> options) && options.Count > 0 ? options[cycleIndices[id]] : string.Empty;
    private void SetCycle(string id, string value) { if (!cycleOptions.TryGetValue(id, out IReadOnlyList<string> options)) return; cycleIndices[id] = Mathf.Max(0, options.ToList().FindIndex(option => option == value)); SetCycleText(id); }
    private void SetCycleText(string id) { if (cycleValues.TryGetValue(id, out TextMeshProUGUI text)) text.text = CycleValue(id); }
    private void SetDropdown(string id, string value) { if (dropdowns.TryGetValue(id, out TMP_Dropdown dropdown)) { int index = dropdown.options.FindIndex(option => option.text == value); dropdown.SetValueWithoutNotify(Mathf.Max(0, index)); } }
    private void SetToggle(string id, bool value, string label = null) { if (toggles.TryGetValue(id, out Toggle toggle)) toggle.SetIsOnWithoutNotify(value); if (toggleValues.TryGetValue(id, out TextMeshProUGUI text)) text.text = label ?? (value ? "Вкл." : "Выкл."); }
    private void SetSlider(string id, float value, string label = null) { if (sliders.TryGetValue(id, out Slider slider)) slider.SetValueWithoutNotify(value); SetSliderValue(id, label); }
    private void SetSliderValue(string id, string value) { if (sliderValues.TryGetValue(id, out TextMeshProUGUI text)) text.text = value ?? string.Empty; }
    private static GameObject CreateUi(Transform parent, string name) { GameObject result = new GameObject(name, typeof(RectTransform)); result.layer = parent.gameObject.layer; result.transform.SetParent(parent, false); return result; }
    private static TextMeshProUGUI Text(Transform parent, string name, string value, float size, FontStyles style, TextAlignmentOptions alignment) { TextMeshProUGUI text = CreateUi(parent, name).AddComponent<TextMeshProUGUI>(); text.text = value; text.fontSize = size; text.fontStyle = style; text.alignment = alignment; text.color = PrimaryText; text.enableWordWrapping = false; text.raycastTarget = false; return text; }
    private static ColorBlock Colors() { ColorBlock colors = ColorBlock.defaultColorBlock; colors.normalColor = Color.white; colors.highlightedColor = new Color(0.74f, 0.88f, 1f, 1f); colors.pressedColor = new Color(0.48f, 0.67f, 0.85f, 1f); colors.selectedColor = new Color(0.64f, 0.82f, 1f, 1f); colors.disabledColor = new Color(0.45f, 0.48f, 0.54f, 0.68f); colors.colorMultiplier = 1f; return colors; }
    private static ColorBlock CategoryColors() { ColorBlock colors = Colors(); colors.highlightedColor = new Color(0.62f, 0.82f, 0.92f, 0.90f); colors.pressedColor = new Color(0.28f, 0.56f, 0.72f, 0.90f); colors.selectedColor = new Color(0.36f, 0.66f, 0.80f, 0.86f); return colors; }
    private static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float bottom = 0f, float top = 0f) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); }
}
