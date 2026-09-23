using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum MainMenuButtonVisualRole
{
    Primary,
    Secondary,
    Destructive
}

public sealed class MainMenuButtonHoverEffect : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerMoveHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    ISelectHandler,
    IDeselectHandler,
    IMoveHandler
{
    public Image highlightImage;
    public Text labelText;
    public GameObject playIndicator;

    public Color normalHighlightColor = new Color(0f, 0f, 0f, 0f);
    public Color hoverHighlightColor = new Color(0.12f, 0.24f, 0.36f, 0.96f);
    public Color pressedHighlightColor = new Color(0.035f, 0.09f, 0.15f, 1f);

    public Color normalTextColor = new Color(0.89f, 0.94f, 1f, 0.96f);
    public Color hoverTextColor = Color.white;
    public bool useRedFocusText;
    public bool suppressFocusAccent;

    private Button button;
    private Outline outline;
    private Graphic labelGraphic;
    private Image focusAccent;
    private Image focusGlow;
    private bool isPointerInside;
    private bool isSelected;
    private MainMenuButtonVisualRole role;
    private IReadOnlyList<Button> mainMenuActions;
    private IReadOnlyList<Button> exclusiveActions;

    public MainMenuButtonVisualRole Role => role;
    public bool IsInteractionVisible => button != null && button.interactable && (isPointerInside || isSelected);
    public Color CurrentLabelColor => labelGraphic != null ? labelGraphic.color : Color.clear;
    public bool IsFocusAccentVisible => focusAccent != null && focusAccent.gameObject.activeSelf;
    public Color FocusAccentColor => focusAccent != null ? focusAccent.color : Color.clear;
    public Vector2 FocusAccentSize => focusAccent != null ? focusAccent.rectTransform.sizeDelta : Vector2.zero;
    public Vector2 FocusAccentAnchoredPosition => focusAccent != null ? focusAccent.rectTransform.anchoredPosition : Vector2.zero;
    public bool IsFocusGlowVisible => focusGlow != null && focusGlow.gameObject.activeSelf;
    public Color FocusGlowColor => focusGlow != null ? focusGlow.color : Color.clear;

    private void Awake()
    {
        EnsureReferences();
        RefreshState();
    }

    private void EnsureReferences()
    {
        button ??= GetComponent<Button>();
        outline ??= GetComponent<Outline>();
        if (labelGraphic == null && labelText != null)
        {
            labelGraphic = labelText;
        }

        if (labelGraphic == null)
        {
            TMP_Text tmpLabel = GetComponentInChildren<TMP_Text>(true);
            labelGraphic = tmpLabel != null ? tmpLabel : GetComponentInChildren<Text>(true);
        }

        bool accentNeedsInitialization = false;
        if (focusAccent == null)
        {
            Transform existingAccent = transform.Find("Focus Accent");
            if (existingAccent == null)
            {
                GameObject accent = new GameObject("Focus Accent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                accent.transform.SetParent(transform, false);
                focusAccent = accent.GetComponent<Image>();
                accentNeedsInitialization = true;
            }
            else
            {
                focusAccent = existingAccent.GetComponent<Image>();
            }
        }

        if (focusAccent != null && accentNeedsInitialization)
        {
            RectTransform accentRect = focusAccent.rectTransform;
            // UI Target v1 interaction language: a bright cyan bar as tall as the
            // action row, sitting flush with the row's left edge — the target's
            // luminous edge starts exactly where the highlighted row starts, so
            // no wash may remain visible between the plate edge and the bar.
            // The pivot stays in the vertical centre so the accent never reads
            // as lower than the label.
            accentRect.anchorMin = new Vector2(0f, 0.5f);
            accentRect.anchorMax = new Vector2(0f, 0.5f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.anchoredPosition = Vector2.zero;
            accentRect.sizeDelta = new Vector2(7f, 76f);
            focusAccent.raycastTarget = false;
        }

        if (focusAccent != null)
        {
            EnsureFocusGlow();
        }
    }

    private void EnsureFocusGlow()
    {
        // Neon emission: a soft cyan halo parented to the accent bar, so the
        // glow always shares the bar's exact enable state and can never float
        // alone over a normal or disabled row.
        Transform existingGlow = focusAccent.transform.Find("Focus Accent Glow");
        if (existingGlow != null)
        {
            focusGlow = existingGlow.GetComponent<Image>();
            return;
        }

        GameObject glow = new GameObject("Focus Accent Glow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        glow.transform.SetParent(focusAccent.transform, false);
        focusGlow = glow.GetComponent<Image>();
        focusGlow.sprite = CreateFocusGlowSprite();
        focusGlow.type = Image.Type.Simple;
        focusGlow.color = new Color(0.01f, 0.85f, 0.98f, 0.42f);
        focusGlow.raycastTarget = false;
        RectTransform glowRect = focusGlow.rectTransform;
        glowRect.anchorMin = glowRect.anchorMax = new Vector2(0.5f, 0.5f);
        glowRect.pivot = new Vector2(0.5f, 0.5f);
        glowRect.anchoredPosition = Vector2.zero;
        // Taller than the 76px row by design: the halo bleeds a few pixels past
        // the row into the 8px rhythm gap, which is what makes the bar read as
        // emitted light instead of a flat UI rectangle.
        glowRect.sizeDelta = new Vector2(30f, 92f);
    }

    private void OnEnable()
    {
        RefreshState();
    }

    public void Configure(MainMenuButtonVisualRole visualRole)
    {
        role = visualRole;
        RefreshState();
    }

    // Only root actions share this policy; modal buttons keep their styling.
    public void ConfigureMainMenuActions(IReadOnlyList<Button> actions)
    {
        mainMenuActions = actions;
        if (highlightImage == null && button != null)
        {
            highlightImage = button.targetGraphic as Image ?? GetComponent<Image>();
        }

        if (highlightImage != null)
        {
            // UI Target v1: the selected row's plate is a left-weighted luminous
            // ramp that evaporates toward the right, never a flat rectangle with
            // a hard end edge. The tint stays white so the sprite carries the hue.
            highlightImage.sprite = CreateNavSelectionRampSprite();
            highlightImage.type = Image.Type.Simple;
        }

        // Root navigation gets one small, stable HIF-colour anchor. This is
        // intentionally not a panel, particle system, or copied reference look.
        suppressFocusAccent = false;
        RefreshState();
    }

    public void ConfigureExclusiveActions(IReadOnlyList<Button> actions)
    {
        exclusiveActions = actions;
        RefreshState();
    }

    private void SetMainMenuInteraction(bool pointer)
    {
        foreach (Button action in mainMenuActions)
        {
            MainMenuButtonHoverEffect effect = action.GetComponent<MainMenuButtonHoverEffect>();
            if (effect == null) continue;
            effect.isPointerInside = effect == this && pointer;
            effect.isSelected = effect == this && !pointer;
            effect.RefreshState();
        }
    }

    private void SetExclusiveInteraction(bool pointer)
    {
        if (exclusiveActions == null) return;
        foreach (Button action in exclusiveActions)
        {
            MainMenuButtonHoverEffect effect = action != null ? action.GetComponent<MainMenuButtonHoverEffect>() : null;
            if (effect == null) continue;
            effect.isPointerInside = effect == this && pointer;
            effect.isSelected = effect == this;
            effect.RefreshState();
        }
    }

    public void OnMove(AxisEventData eventData)
    {
        if (mainMenuActions == null) return;
        // Button may already have moved selection, or stayed at a navigation edge.
        GameObject selected = EventSystem.current?.currentSelectedGameObject;
        MainMenuButtonHoverEffect effect = selected != null ? selected.GetComponent<MainMenuButtonHoverEffect>() : null;
        if (effect != null && effect.mainMenuActions == mainMenuActions)
            effect.SetMainMenuInteraction(false);
    }

    public void RefreshState()
    {
        EnsureReferences();
        if (button != null && !button.interactable)
        {
            ApplyDisabledState();
            return;
        }

        if (isPointerInside || isSelected)
        {
            ApplyHoverState();
            return;
        }

        ApplyNormalState();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerInside = true;
        // Mouse hover is an explicit current action. Select it as well so a
        // retained keyboard/controller selection cannot leave two menu actions
        // looking active after returning from a modal.
        if (button != null && button.isActiveAndEnabled && button.interactable)
        {
            (EventSystem.current ?? FindFirstObjectByType<EventSystem>())?.SetSelectedGameObject(button.gameObject);
        }
        if (mainMenuActions != null) SetMainMenuInteraction(true);
        if (exclusiveActions != null) SetExclusiveInteraction(true);
        RefreshState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerInside = false;
        RefreshState();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (mainMenuActions != null && eventData != null && eventData.delta.sqrMagnitude > 0f)
            OnPointerEnter(eventData);
    }

    private void OnDisable()
    {
        if (mainMenuActions == null) return;
        isPointerInside = false;
        isSelected = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (mainMenuActions != null) SetMainMenuInteraction(true);
        if (button == null || button.interactable)
        {
            ApplyPressedState();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        RefreshState();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (mainMenuActions != null)
        {
            SetMainMenuInteraction(false);
            return;
        }
        if (exclusiveActions != null)
        {
            SetExclusiveInteraction(false);
            return;
        }
        isSelected = true;
        RefreshState();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (mainMenuActions != null || exclusiveActions != null) isPointerInside = false;
        isSelected = false;
        RefreshState();
    }

    private void ApplyNormalState()
    {
        Apply(RoleNormalBackground(), RoleNormalText());
    }

    private void ApplyHoverState()
    {
        if (mainMenuActions != null)
        {
            // UI Target v1: hover/focus activates the left-weighted teal ramp
            // behind the row; the sprite carries the falloff, the tint the depth.
            Apply(TargetV1SelectedPlate(0.52f), new Color(0.88f, 0.96f, 1f, 1f));
            return;
        }
        // Modal confirmations carry selection on the button plate itself: one
        // visible selected state, no detached accent bar that reads as a divider.
        Apply(ModalSelectedBackground(), RoleHoverText());
    }

    private static Color TargetV1SelectedPlate(float alpha)
    {
        // The ramp sprite owns the teal-blue luminance, so the tint stays white;
        // alpha remains the single depth control for hover/pressed.
        return new Color(1f, 1f, 1f, alpha);
    }

    private static Texture2D navSelectionRampTexture;
    private const string NavSelectionRampSpriteName = "HIF Nav Selection Ramp Runtime";

    /// <summary>
    /// UI Target v1 selected-row ramp, built once: teal-blue luminance that is
    /// strongest at the row's left edge and fades linearly to transparent at the
    /// right edge, so the highlight has no hard rectangular end. The first ~12%
    /// burns from a hot neon cyan into the base teal, giving the plate a
    /// luminous leading edge next to the accent bar. The texture is created in
    /// linear space, so the intended sRGB teal #1C789E (28, 120, 158) is
    /// pre-converted with an inverse-gamma pow(2.2); storing the sRGB bytes
    /// directly would render as a washed-out pastel blue.
    /// </summary>
    internal static Sprite CreateNavSelectionRampSprite()
    {
        if (navSelectionRampTexture == null)
        {
            const int width = 256;
            const int height = 4;
            navSelectionRampTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            navSelectionRampTexture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[width * height];
            Color32 baseTeal = new Color32(2, 49, 89, 255);
            // Linear-space bytes for a hot cyan close to the accent hue
            // #02D9F9 (displays as ≈ #33DFF4 over the wash).
            Color32 hotCore = new Color32(7, 188, 231, 255);
            for (int x = 0; x < width; x++)
            {
                float t = x / (width - 1f);
                byte alpha = (byte)Mathf.RoundToInt(255f * (1f - t));
                float coreBlend = Mathf.Clamp01(1f - t / 0.12f);
                Color ramp = Color.Lerp(baseTeal, hotCore, coreBlend);
                ramp.a = alpha / 255f;
                for (int y = 0; y < height; y++)
                {
                    pixels[y * width + x] = ramp;
                }
            }

            navSelectionRampTexture.SetPixels32(pixels);
            navSelectionRampTexture.Apply(false, true);
        }

        Sprite sprite = Sprite.Create(navSelectionRampTexture, new Rect(0f, 0f, 256f, 4f), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = NavSelectionRampSpriteName;
        return sprite;
    }

    private static Texture2D focusGlowTexture;
    private const string FocusGlowSpriteName = "HIF Focus Glow Runtime";

    /// <summary>
    /// Soft horizontal falloff for the neon halo around the focus accent bar:
    /// white luminance only, so the Image tint stays the single source of the
    /// cyan hue (matching the accent bar's own color), and the alpha curve
    /// decays fast enough to read as emitted light rather than a second plate.
    /// </summary>
    internal static Sprite CreateFocusGlowSprite()
    {
        if (focusGlowTexture == null)
        {
            const int width = 64;
            const int height = 4;
            focusGlowTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            focusGlowTexture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[width * height];
            for (int x = 0; x < width; x++)
            {
                float t = x / (width - 1f);
                byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Pow(1f - t, 1.6f));
                for (int y = 0; y < height; y++)
                {
                    pixels[y * width + x] = new Color32(255, 255, 255, alpha);
                }
            }

            focusGlowTexture.SetPixels32(pixels);
            focusGlowTexture.Apply(false, true);
        }

        Sprite sprite = Sprite.Create(focusGlowTexture, new Rect(0f, 0f, 64f, 4f), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = FocusGlowSpriteName;
        return sprite;
    }

    private Color ModalSelectedBackground()
    {
        return role == MainMenuButtonVisualRole.Destructive
            ? new Color(0.46f, 0.15f, 0.19f, 0.42f)
            : new Color(0.20f, 0.42f, 0.60f, 0.36f);
    }

    private void ApplyPressedState()
    {
        Apply(mainMenuActions != null ? TargetV1SelectedPlate(0.58f) : RolePressedBackground(), Color.white);
    }

    private void ApplyDisabledState()
    {
        Apply(Color.clear, new Color(0.64f, 0.70f, 0.76f, 0.84f));

        if (outline != null)
        {
            outline.enabled = false;
        }

        if (focusAccent != null)
        {
            focusAccent.gameObject.SetActive(false);
        }

        if (focusGlow != null)
        {
            focusGlow.gameObject.SetActive(false);
        }
    }

    private void Apply(Color background, Color text)
    {
        if (highlightImage != null)
        {
            highlightImage.color = background;
        }

        if (labelGraphic != null)
        {
            labelGraphic.color = text;
        }

        if (outline != null) outline.enabled = false;

        if (playIndicator != null)
        {
            playIndicator.SetActive(false);
        }

        if (focusAccent != null)
        {
            // The accent bar is a root Main Menu navigation anchor only. Modal
            // buttons express focus through their plate, so the bar can never
            // float between confirmation actions as a perceived separator.
            focusAccent.color = new Color(0.01f, 0.85f, 0.98f, 0.98f);
            bool accentVisible = mainMenuActions != null
                && !suppressFocusAccent
                && (isPointerInside || isSelected);
            focusAccent.gameObject.SetActive(accentVisible);
            if (focusGlow != null)
            {
                focusGlow.gameObject.SetActive(accentVisible);
            }
        }
    }

    private Color RoleNormalBackground()
    {
        return role switch
        {
            _ => Color.clear
        };
    }

    private Color RoleHoverBackground()
    {
        return role switch
        {
            MainMenuButtonVisualRole.Primary => new Color(0.12f, 0.30f, 0.40f, 0.28f),
            MainMenuButtonVisualRole.Destructive => new Color(0.08f, 0.18f, 0.24f, 0.22f),
            _ => new Color(0.08f, 0.20f, 0.28f, 0.24f)
        };
    }

    private Color RolePressedBackground()
    {
        return role switch
        {
            MainMenuButtonVisualRole.Primary => new Color(0.05f, 0.15f, 0.21f, 0.40f),
            MainMenuButtonVisualRole.Destructive => new Color(0.04f, 0.11f, 0.16f, 0.34f),
            _ => new Color(0.04f, 0.12f, 0.18f, 0.36f)
        };
    }

    private Color RoleNormalText()
    {
        if (mainMenuActions != null) return new Color(0.88f, 0.91f, 0.95f, 0.98f);
        return role switch
        {
            MainMenuButtonVisualRole.Primary => Color.white,
            MainMenuButtonVisualRole.Destructive => new Color(0.78f, 0.80f, 0.84f, 0.92f),
            _ => new Color(0.89f, 0.94f, 1f, 0.96f)
        };
    }

    private Color RoleHoverText()
    {
        return role == MainMenuButtonVisualRole.Destructive
            ? new Color(0.94f, 0.84f, 0.85f, 1f)
            : Color.white;
    }
}
