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

    private Button button;
    private Outline outline;
    private Graphic labelGraphic;
    private bool isPointerInside;
    private bool isSelected;
    private MainMenuButtonVisualRole role;

    public MainMenuButtonVisualRole Role => role;
    public Color CurrentLabelColor => labelGraphic != null ? labelGraphic.color : Color.clear;

    private void Awake()
    {
        EnsureReferences();
        RefreshState();
    }

    private void EnsureReferences()
    {
        button ??= GetComponent<Button>();
        outline ??= GetComponent<Outline>();
        if (labelGraphic != null)
        {
            return;
        }

        if (labelText != null)
        {
            labelGraphic = labelText;
            return;
        }

        TMP_Text tmpLabel = GetComponentInChildren<TMP_Text>(true);
        labelGraphic = tmpLabel != null ? tmpLabel : GetComponentInChildren<Text>(true);
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
        Apply(RoleHoverBackground(), Color.white);
    }

    private void ApplyPressedState()
    {
        Apply(RolePressedBackground(), Color.white);
    }

    private void ApplyDisabledState()
    {
        Apply(new Color(0.04f, 0.07f, 0.11f, 0.60f), new Color(0.63f, 0.68f, 0.75f, 0.82f));
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

        if (outline != null)
        {
            bool activeAccent = isPointerInside || isSelected || role == MainMenuButtonVisualRole.Primary;
            outline.effectColor = activeAccent
                ? new Color(0.68f, 0.80f, 0.90f, 0.82f)
                : new Color(0.30f, 0.48f, 0.66f, 0.42f);
        }

        if (playIndicator != null)
        {
            playIndicator.SetActive(false);
        }
    }

    private Color RoleNormalBackground()
    {
        return role switch
        {
            MainMenuButtonVisualRole.Primary => new Color(0.10f, 0.21f, 0.32f, 0.98f),
            MainMenuButtonVisualRole.Destructive => new Color(0.055f, 0.10f, 0.16f, 0.88f),
            _ => new Color(0.025f, 0.07f, 0.12f, 0.78f)
        };
    }

    private Color RoleHoverBackground()
    {
        return role switch
        {
            MainMenuButtonVisualRole.Primary => new Color(0.16f, 0.30f, 0.43f, 1f),
            MainMenuButtonVisualRole.Destructive => new Color(0.10f, 0.19f, 0.28f, 0.96f),
            _ => new Color(0.075f, 0.17f, 0.27f, 0.96f)
        };
    }

    private Color RolePressedBackground()
    {
        return role switch
        {
            MainMenuButtonVisualRole.Primary => new Color(0.055f, 0.13f, 0.22f, 1f),
            MainMenuButtonVisualRole.Destructive => new Color(0.035f, 0.075f, 0.12f, 1f),
            _ => new Color(0.035f, 0.10f, 0.17f, 1f)
        };
    }

    private Color RoleNormalText()
    {
        return role == MainMenuButtonVisualRole.Secondary
            ? new Color(0.89f, 0.94f, 1f, 0.96f)
            : Color.white;
    }
}
