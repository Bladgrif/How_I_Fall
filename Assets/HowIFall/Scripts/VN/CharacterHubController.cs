using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public sealed class CharacterHubFixture
{
    public CharacterProfileDefinition definition;
    public bool locked;
}

/// <summary>Runtime-only ordinary modal for the non-canonical Character Hub fixture.</summary>
public sealed class CharacterHubController : MonoBehaviour
{
    private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.88f);
    private static readonly Color PanelColor = new Color(0.035f, 0.075f, 0.115f, 0.98f);
    private static readonly Color PanelBorderColor = new Color(0.22f, 0.43f, 0.57f, 0.8f);
    private static readonly Color SelectedCardColor = new Color(0.10f, 0.31f, 0.43f, 1f);
    private static readonly Color NormalCardColor = new Color(0.07f, 0.14f, 0.20f, 1f);
    private static readonly Color LockedCardColor = new Color(0.12f, 0.13f, 0.15f, 1f);
    private static readonly Color LockedTextColor = new Color(0.58f, 0.61f, 0.64f, 1f);

    public VNDialogueController dialogueController;
    public GameObject panel;
    public GameObject centralPanel;
    public Button testCharacterAButton;
    public Button testCharacterBButton;
    public Button closeButton;
    public TextMeshProUGUI selectedNameText;
    public Image portraitImage;
    public GameObject portraitPlaceholder;
    public TextMeshProUGUI biographyText;
    public TextMeshProUGUI relationshipText;
    public TextMeshProUGUI lockedText;
    public CharacterHubFixture[] fixtures;

    private readonly List<CharacterHubFixture> validFixtures = new List<CharacterHubFixture>();
    private CharacterHubFixture selectedFixture;
    private Image visibleCardImage;
    private Image lockedCardImage;
    private Outline visibleCardOutline;
    private Text visibleCardLabel;
    private Text lockedCardLabel;
    private TextMeshProUGUI portraitPlaceholderText;

    public bool IsOpen => panel != null && panel.activeSelf;
    public IReadOnlyList<CharacterHubFixture> ValidFixtures => validFixtures;
    public CharacterHubFixture SelectedFixture => selectedFixture;

    public static CharacterHubController TryCreateRuntime(VNDialogueController dialogueController)
    {
        if (dialogueController == null)
        {
            return null;
        }

        CharacterHubController existing = dialogueController.GetComponent<CharacterHubController>();
        if (existing != null)
        {
            return existing;
        }

        CharacterHubTechnicalConfig config = Resources.Load<CharacterHubTechnicalConfig>(CharacterHubTechnicalConfig.ResourcesPath);
        if (!TryCreateFixtures(config, out CharacterHubFixture[] runtimeFixtures, out string diagnostic))
        {
            Debug.LogWarning("[CHARACTER HUB] Bootstrap unavailable: " + diagnostic, dialogueController);
            return null;
        }

        Canvas canvas = dialogueController.GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[CHARACTER HUB] Bootstrap unavailable: VN Canvas was not found.", dialogueController);
            return null;
        }

        CharacterHubController hub = dialogueController.gameObject.AddComponent<CharacterHubController>();
        hub.InitializeRuntime(dialogueController, canvas, runtimeFixtures);
        return hub;
    }

    public static bool TryCreateFixtures(CharacterHubTechnicalConfig config, out CharacterHubFixture[] runtimeFixtures, out string diagnostic)
    {
        runtimeFixtures = null;
        if (config == null || config.visibleProfile == null || config.lockedProfile == null)
        {
            diagnostic = "TechnicalCharacterHubConfig or one of its profiles is missing.";
            return false;
        }

        runtimeFixtures = new[]
        {
            new CharacterHubFixture { definition = config.visibleProfile },
            new CharacterHubFixture { definition = config.lockedProfile, locked = true }
        };
        return TryBuildValidFixtures(runtimeFixtures, new List<CharacterHubFixture>(), out diagnostic);
    }

    private void InitializeRuntime(VNDialogueController controller, Canvas canvas, CharacterHubFixture[] runtimeFixtures)
    {
        dialogueController = controller;
        fixtures = runtimeFixtures;
        BuildRuntimeUi(canvas);
        BindRuntimeActions();
        panel.SetActive(false);
    }

    private void BuildRuntimeUi(Canvas canvas)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        panel = CreatePanel(canvas.transform, "Character Hub Runtime Panel", OverlayColor, Vector2.zero, Vector2.one, Vector2.zero);

        centralPanel = CreatePanel(panel.transform, "Character Hub Content", PanelColor, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(940f, 540f));
        centralPanel.GetComponent<Image>().raycastTarget = true;
        Outline panelOutline = centralPanel.AddComponent<Outline>();
        panelOutline.effectColor = PanelBorderColor;
        panelOutline.effectDistance = new Vector2(2f, -2f);

        CreateText(centralPanel.transform, "CHARACTERS", 30, new Vector2(-390f, 220f), new Vector2(350f, 48f), TextAlignmentOptions.Left, FontStyles.Bold);
        TextMeshProUGUI techDemo = CreateText(centralPanel.transform, "TECH DEMO ONLY · NOT CANON", 14, new Vector2(145f, 221f), new Vector2(390f, 36f), TextAlignmentOptions.Right);
        techDemo.color = new Color(0.60f, 0.73f, 0.82f, 1f);

        CreatePanel(centralPanel.transform, "Column Divider", new Color(0.22f, 0.38f, 0.48f, 0.65f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(2f, 400f)).GetComponent<RectTransform>().anchoredPosition = new Vector2(-140f, -5f);

        testCharacterAButton = CreateCharacterCard(centralPanel.transform, "TEST CHARACTER A", new Vector2(-310f, 110f), false, font, out visibleCardImage, out visibleCardOutline, out visibleCardLabel);
        testCharacterBButton = CreateCharacterCard(centralPanel.transform, "TEST CHARACTER B\nLOCKED", new Vector2(-310f, 26f), true, font, out lockedCardImage, out _, out lockedCardLabel);

        GameObject portraitFrame = CreatePanel(centralPanel.transform, "Portrait Placeholder Frame", new Color(0.07f, 0.13f, 0.18f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(220f, 290f));
        portraitFrame.GetComponent<RectTransform>().anchoredPosition = new Vector2(5f, 5f);
        Outline portraitOutline = portraitFrame.AddComponent<Outline>();
        portraitOutline.effectColor = new Color(0.28f, 0.49f, 0.60f, 0.75f);
        portraitOutline.effectDistance = new Vector2(1f, -1f);
        portraitPlaceholder = portraitFrame;
        portraitPlaceholderText = CreateText(portraitFrame.transform, "NO PORTRAIT\nTECH DEMO", 20, Vector2.zero, new Vector2(190f, 90f), TextAlignmentOptions.Center, FontStyles.Bold);
        portraitPlaceholderText.color = new Color(0.65f, 0.76f, 0.83f, 1f);

        selectedNameText = CreateText(centralPanel.transform, string.Empty, 27, new Vector2(255f, 110f), new Vector2(320f, 48f), TextAlignmentOptions.Left, FontStyles.Bold);
        biographyText = CreateText(centralPanel.transform, string.Empty, 20, new Vector2(255f, 42f), new Vector2(320f, 90f), TextAlignmentOptions.TopLeft);
        relationshipText = CreateText(centralPanel.transform, string.Empty, 19, new Vector2(255f, -62f), new Vector2(320f, 38f), TextAlignmentOptions.Left);
        lockedText = CreateText(centralPanel.transform, string.Empty, 18, new Vector2(255f, -108f), new Vector2(320f, 36f), TextAlignmentOptions.Left, FontStyles.Bold);
        lockedText.color = LockedTextColor;

        closeButton = CreateActionButton(centralPanel.transform, "Close", new Vector2(350f, -215f), font);
    }

    private void BindRuntimeActions()
    {
        Bind(testCharacterAButton, () => SelectFixture(0));
        Bind(testCharacterBButton, () => SelectFixture(1));
        Bind(closeButton, () => dialogueController?.CloseCharacterHub());
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        obj.GetComponent<Image>().color = color;
        return obj;
    }

    private static Button CreateCharacterCard(Transform parent, string label, Vector2 position, bool locked, Font font, out Image image, out Outline outline, out Text text)
    {
        GameObject obj = new GameObject(label.Replace("\n", " ") + " Card", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(270f, 68f);
        image = obj.GetComponent<Image>();
        image.color = locked ? LockedCardColor : NormalCardColor;
        Button button = obj.GetComponent<Button>();
        button.targetGraphic = image;
        outline = obj.GetComponent<Outline>();
        outline.effectColor = locked ? new Color(0.28f, 0.30f, 0.32f, 0.65f) : new Color(0.18f, 0.40f, 0.54f, 0.55f);
        outline.effectDistance = new Vector2(1f, -1f);
        text = CreateLegacyText(obj.transform, label, 17, Vector2.zero, new Vector2(245f, 58f), font);
        text.alignment = TextAnchor.MiddleLeft;
        text.color = locked ? LockedTextColor : Color.white;
        return button;
    }

    private static Button CreateActionButton(Transform parent, string label, Vector2 position, Font font)
    {
        GameObject obj = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(150f, 46f);
        obj.GetComponent<Image>().color = new Color(0.12f, 0.31f, 0.43f, 1f);
        Text text = CreateLegacyText(obj.transform, label, 18, Vector2.zero, new Vector2(140f, 40f), font);
        text.alignment = TextAnchor.MiddleCenter;
        return obj.GetComponent<Button>();
    }

    private static TextMeshProUGUI CreateText(Transform parent, string value, float size, Vector2 position, Vector2 dimensions, TextAlignmentOptions alignment, FontStyles style = FontStyles.Normal)
    {
        GameObject obj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static Text CreateLegacyText(Transform parent, string value, int size, Vector2 position, Vector2 dimensions, Font font)
    {
        GameObject obj = new GameObject("Label", typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        Text text = obj.GetComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = Color.white;
        return text;
    }

    public bool Open()
    {
        if (panel == null)
        {
            Debug.LogWarning("[CHARACTER HUB] Panel is not assigned.", this);
            return false;
        }

        if (!TryBuildValidFixtures(fixtures, validFixtures, out string diagnostic))
        {
            Debug.LogWarning("[CHARACTER HUB] " + diagnostic, this);
            return false;
        }

        selectedFixture = validFixtures[0];
        panel.SetActive(true);
        Refresh();
        return true;
    }

    public bool Hide()
    {
        if (!IsOpen)
        {
            return false;
        }

        panel.SetActive(false);
        return true;
    }

    public bool SelectFixture(int index)
    {
        if (!IsOpen || index < 0 || index >= validFixtures.Count)
        {
            return false;
        }

        selectedFixture = validFixtures[index];
        Refresh();
        return true;
    }

    public void Refresh()
    {
        if (!IsOpen || selectedFixture == null || selectedFixture.definition == null)
        {
            return;
        }

        CharacterProfileDefinition definition = selectedFixture.definition;
        bool locked = selectedFixture.locked;
        selectedNameText.text = locked ? "LOCKED" : definition.displayName;
        biographyText.text = locked ? "Biography: LOCKED" : definition.biography;
        relationshipText.text = locked ? "Relationship: LOCKED" : BuildRelationshipText(definition);
        lockedText.gameObject.SetActive(locked);
        lockedText.text = locked ? "LOCKED PROFILE" : string.Empty;
        portraitPlaceholderText.text = locked ? "LOCKED\nTECH DEMO" : "NO PORTRAIT\nTECH DEMO";
        RefreshCharacterListPresentation();

        if (portraitImage != null)
        {
            portraitImage.sprite = locked ? null : definition.portrait;
            portraitImage.enabled = !locked && definition.portrait != null;
        }
    }

    private void RefreshCharacterListPresentation()
    {
        bool visibleSelected = selectedFixture != null && !selectedFixture.locked;
        visibleCardImage.color = visibleSelected ? SelectedCardColor : NormalCardColor;
        visibleCardOutline.effectColor = visibleSelected ? new Color(0.52f, 0.82f, 0.96f, 1f) : new Color(0.18f, 0.40f, 0.54f, 0.55f);
        visibleCardOutline.effectDistance = visibleSelected ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
        visibleCardLabel.color = Color.white;
        lockedCardImage.color = LockedCardColor;
        lockedCardLabel.color = LockedTextColor;
    }

    public static bool TryBuildValidFixtures(IEnumerable<CharacterHubFixture> source, List<CharacterHubFixture> destination, out string diagnostic)
    {
        destination.Clear();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (source == null)
        {
            diagnostic = "No character fixtures are assigned.";
            return false;
        }

        foreach (CharacterHubFixture fixture in source)
        {
            CharacterProfileDefinition definition = fixture != null ? fixture.definition : null;
            if (definition == null || string.IsNullOrWhiteSpace(definition.characterId))
            {
                diagnostic = "A character fixture is null or has an empty characterId.";
                return false;
            }

            if (!ids.Add(definition.characterId))
            {
                diagnostic = "Duplicate characterId '" + definition.characterId + "' was rejected.";
                return false;
            }

            destination.Add(fixture);
        }

        diagnostic = destination.Count > 0 ? string.Empty : "No valid character fixtures are assigned.";
        return destination.Count > 0;
    }

    private static string BuildRelationshipText(CharacterProfileDefinition definition)
    {
        if (!CharacterRelationshipResolver.TryResolve(GameState.Instance, definition.relationshipSource, out int value))
        {
            return "Relationship: —";
        }

        return "Relationship: " + value;
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }
}
