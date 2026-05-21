using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MainMenuButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public Image highlightImage;
    public Text labelText;
    public GameObject playIndicator;
    public AudioClip clickSfx;
    public bool selectedByDefault;

    public Color normalHighlightColor = new Color(0f, 0f, 0f, 0f);
    public Color hoverHighlightColor = new Color(0.45f, 0.18f, 0.58f, 0.35f);
    public Color pressedHighlightColor = new Color(0.58f, 0.22f, 0.72f, 0.48f);

    public Color normalTextColor = new Color(0.9f, 0.87f, 0.96f, 0.98f);
    public Color hoverTextColor = new Color(1f, 0.95f, 1f, 1f);

    private bool _isPointerInside;

    private void Awake()
    {
        if (selectedByDefault)
        {
            ApplyHoverState();
        }
        else
        {
            ApplyNormalState();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isPointerInside = true;
        ApplyHoverState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isPointerInside = false;
        if (selectedByDefault)
        {
            ApplyHoverState();
        }
        else
        {
            ApplyNormalState();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (highlightImage != null)
        {
            highlightImage.color = pressedHighlightColor;
        }

        PlaySfx(clickSfx);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_isPointerInside)
        {
            ApplyHoverState();
        }
        else
        {
            ApplyNormalState();
        }
    }

    private void ApplyNormalState()
    {
        if (highlightImage != null)
        {
            highlightImage.color = normalHighlightColor;
        }

        if (labelText != null)
        {
            labelText.color = normalTextColor;
        }

        if (playIndicator != null)
        {
            playIndicator.SetActive(false);
        }
    }

    private void ApplyHoverState()
    {
        if (highlightImage != null)
        {
            highlightImage.color = hoverHighlightColor;
        }

        if (labelText != null)
        {
            labelText.color = hoverTextColor;
        }

        if (playIndicator != null)
        {
            playIndicator.SetActive(true);
        }
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(clip);
        }
    }
}
