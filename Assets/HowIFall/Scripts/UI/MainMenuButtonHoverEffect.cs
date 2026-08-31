using TMPro;
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
    IPointerDownHandler,
    IPointerUpHandler,
    ISelectHandler,
    IDeselectHandler
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
    private bool isPointerInside;
    private bool isSelected;
    private MainMenuButtonVisualRole role;

    public MainMenuButtonVisualRole Role => role;
    public Color CurrentLabelColor => labelGraphic != null ? labelGraphic.color : Color.clear;
    public bool IsFocusAccentVisible => focusAccent != null && focusAccent.gameObject.activeSelf;
    public Color FocusAccentColor => focusAccent != null ? focusAccent.color : Color.clear;
    public Vector2 FocusAccentSize => focusAccent != null ? focusAccent.rectTransform.sizeDelta : Vector2.zero;

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
            // The marker belongs to the text row, not to its lower edge. Keeping
            // its pivot in the vertical centre prevents the accent from reading
            // as visibly lower than the label on Main Menu actions.
            accentRect.anchorMin = new Vector2(0f, 0.5f);
            accentRect.anchorMax = new Vector2(0f, 0.5f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.anchoredPosition = new Vector2(0f, 0f);
            accentRect.sizeDelta = new Vector2(6f, 24f);
            focusAccent.raycastTarget = false;
        }
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
        RefreshState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerInside = false;
        RefreshState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
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
        isSelected = true;
        RefreshState();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        RefreshState();
    }

    private void ApplyNormalState()
    {
        Apply(RoleNormalBackground(), RoleNormalText());
    }

    private void ApplyHoverState()
    {
        Apply(Color.clear, useRedFocusText
            ? new Color(0.92f, 0.20f, 0.25f, 1f)
            : RoleHoverText());
    }

    private void ApplyPressedState()
    {
        Apply(RolePressedBackground(), Color.white);
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
            focusAccent.color = new Color(0.78f, 0.18f, 0.22f, 0.96f);
            focusAccent.gameObject.SetActive(!suppressFocusAccent && (isPointerInside || isSelected));
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
