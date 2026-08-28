using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ManualSaveSlotView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    ISelectHandler,
    IDeselectHandler
{
    private static readonly Color OccupiedColor = new Color(0.052f, 0.071f, 0.1f, 0.97f);
    private static readonly Color EmptySaveColor = new Color(0.052f, 0.063f, 0.082f, 0.82f);
    private static readonly Color EmptyLoadColor = new Color(0.042f, 0.049f, 0.062f, 0.72f);
    private static readonly Color HoverColor = new Color(0.072f, 0.1f, 0.142f, 0.98f);
    private static readonly Color InvalidColor = new Color(0.09f, 0.05f, 0.065f, 0.9f);

    public Button button;
    public RectTransform cardRect;
    public Image backgroundImage;
    public Image hoverAccentImage;
    public Image occupiedAccentImage;
    public Image previewFrameImage;
    public Image placeholderOverlayImage;
    public Outline cardOutline;
    public Image previewImage;
    public TextMeshProUGUI slotNumberText;
    public TextMeshProUGUI sceneNameText;
    public TextMeshProUGUI dateText;
    public TextMeshProUGUI emptyText;
    public TextMeshProUGUI backgroundSlotNumberText;
    public Button deleteButton;

    private ManualSaveLoadPanel panel;
    private int slotIndex;
    private Sprite previewSprite;
    private bool pointerInside;
    private bool pointerDown;
    private bool isLoadable;
    private bool isOccupied;
    private float hoverAmount;
    private bool hasEventSystemFocus;

    public bool HasEventSystemFocus => hasEventSystemFocus;
    public bool IsLoadable => isLoadable;
    public bool IsOccupied => isOccupied;

    public void Initialize(ManualSaveLoadPanel owner, int index)
    {
        panel = owner;
        slotIndex = index;

        if (cardRect == null)
        {
            cardRect = transform as RectTransform;
        }

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveListener(HandleDeleteClick);
            deleteButton.onClick.AddListener(HandleDeleteClick);
        }
    }

    public void Render(SaveSlotInfo info, bool saveMode)
    {
        SaveSlotType slotType = info != null ? info.SlotType : SaveSlotType.Manual;
        isOccupied = info != null && info.IsOccupied;
        isLoadable = info != null && info.IsLoadable;

        if (slotNumberText != null)
        {
            slotNumberText.text = GetSlotLabel(slotType, slotIndex);
        }

        if (sceneNameText != null)
        {
            sceneNameText.text = isLoadable ? info.DisplayName : string.Empty;
            sceneNameText.gameObject.SetActive(isLoadable);
        }

        if (dateText != null)
        {
            dateText.text = isLoadable ? info.DisplayDate : string.Empty;
            dateText.gameObject.SetActive(isLoadable);
        }

        if (emptyText != null)
        {
            emptyText.gameObject.SetActive(!isLoadable);
            emptyText.text = isOccupied
                ? "Недоступное сохранение"
                : GetEmptyLabel(slotType);
            emptyText.color = isOccupied
                ? new Color(0.86f, 0.48f, 0.52f, 0.88f)
                : new Color(0.48f, 0.55f, 0.63f, 0.68f);
        }

        if (backgroundSlotNumberText != null)
        {
            backgroundSlotNumberText.text = slotIndex.ToString("00");
            backgroundSlotNumberText.gameObject.SetActive(!isLoadable);
            backgroundSlotNumberText.color = isOccupied
                ? new Color(0.45f, 0.18f, 0.22f, 0.13f)
                : new Color(0.34f, 0.45f, 0.57f, 0.11f);
        }

        if (placeholderOverlayImage != null)
        {
            placeholderOverlayImage.gameObject.SetActive(!isLoadable);
        }

        if (occupiedAccentImage != null)
        {
            occupiedAccentImage.gameObject.SetActive(isOccupied);
            occupiedAccentImage.color = isLoadable
                ? new Color(0.28f, 0.57f, 0.82f, 0.78f)
                : new Color(0.58f, 0.19f, 0.25f, 0.56f);
        }

        if (button != null)
        {
            button.interactable = saveMode
                ? slotType == SaveSlotType.Manual
                : isLoadable;
        }

        if (deleteButton != null)
        {
            deleteButton.gameObject.SetActive(isOccupied);
            deleteButton.interactable = isOccupied;
        }

        ApplyPreview(isLoadable ? info.PreviewPath : string.Empty);
        ApplyVisualState(true);
    }

    private static string GetSlotLabel(SaveSlotType slotType, int index)
    {
        return slotType switch
        {
            SaveSlotType.Auto => $"Авто {index}",
            SaveSlotType.Quick => $"Быстрое {index}",
            _ => $"Слот {index}"
        };
    }

    private static string GetEmptyLabel(SaveSlotType slotType)
    {
        return slotType switch
        {
            SaveSlotType.Auto => "Нет автосохранения",
            SaveSlotType.Quick => "Нет быстрого сохранения",
            _ => "Пустой слот"
        };
    }

    public void OnSelect(BaseEventData eventData)
    {
        hasEventSystemFocus = true;
        ApplyVisualState(true);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        hasEventSystemFocus = false;
        ApplyVisualState(true);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        pointerDown = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && IsCardInteractive())
        {
            pointerDown = true;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pointerDown = false;
    }

    private void Update()
    {
        float target = pointerInside && IsCardInteractive() ? 1f : 0f;
        hoverAmount = Mathf.MoveTowards(hoverAmount, target, Time.unscaledDeltaTime * 8f);
        hasEventSystemFocus = button != null
            && EventSystem.current != null
            && EventSystem.current.currentSelectedGameObject == button.gameObject;
        ApplyVisualState(false);
    }

    private bool IsCardInteractive()
    {
        return button != null && button.interactable;
    }

    private void ApplyVisualState(bool immediate)
    {
        bool interactive = IsCardInteractive();
        if (immediate)
        {
            hoverAmount = pointerInside && interactive ? 1f : 0f;
        }

        float focusAmount = hasEventSystemFocus && interactive ? 1f : 0f;
        float emphasisAmount = Mathf.Max(hoverAmount, focusAmount);

        Color restingColor = isLoadable
            ? OccupiedColor
            : isOccupied
                ? InvalidColor
                : interactive
                    ? EmptySaveColor
                    : EmptyLoadColor;

        if (backgroundImage != null)
        {
            backgroundImage.color = Color.Lerp(restingColor, HoverColor, emphasisAmount);
        }

        if (hoverAccentImage != null)
        {
            Color accent = hoverAccentImage.color;
            accent.a = Mathf.Max(hoverAmount * 0.09f, focusAmount * 0.18f);
            hoverAccentImage.color = accent;
        }

        if (cardOutline != null)
        {
            Color restingOutline = isLoadable
                ? new Color(0.22f, 0.39f, 0.56f, 0.52f)
                : isOccupied
                    ? new Color(0.48f, 0.18f, 0.23f, 0.42f)
                    : new Color(0.18f, 0.25f, 0.32f, interactive ? 0.34f : 0.23f);
            Color hoverOutline = new Color(0.33f, 0.58f, 0.79f, 0.74f);
            cardOutline.effectColor = Color.Lerp(restingOutline, hoverOutline, emphasisAmount);
        }

        if (previewFrameImage != null)
        {
            Color previewResting = isLoadable
                ? new Color(0.16f, 0.23f, 0.31f, 0.68f)
                : new Color(0.13f, 0.17f, 0.21f, interactive ? 0.46f : 0.34f);
            previewFrameImage.color = Color.Lerp(
                previewResting,
                new Color(0.2f, 0.31f, 0.42f, 0.72f),
                emphasisAmount * 0.8f);
        }

        if (cardRect != null)
        {
            float targetScale = pointerDown && interactive
                ? 0.992f
                : Mathf.Lerp(1f, 1.012f, emphasisAmount);
            cardRect.localScale = Vector3.one * targetScale;
        }
    }

    private void HandleClick()
    {
        pointerDown = false;
        panel?.OnSlotSelected(slotIndex);
    }

    private void HandleDeleteClick()
    {
        pointerDown = false;
        panel?.OnDeleteRequested(slotIndex);
    }

    private void ApplyPreview(string path)
    {
        ReleasePreview();

        if (previewImage == null)
        {
            return;
        }

        previewImage.sprite = null;
        previewImage.color = new Color(0.025f, 0.035f, 0.06f, 1f);

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                Destroy(texture);
                return;
            }

            previewSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));
            previewImage.sprite = previewSprite;
            previewImage.preserveAspect = true;
            previewImage.color = Color.white;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[SAVE UI] Preview '{path}' could not be loaded. {exception.Message}", this);
        }
    }

    private void OnDisable()
    {
        pointerInside = false;
        pointerDown = false;
        hoverAmount = 0f;
        hasEventSystemFocus = false;
        ApplyVisualState(true);
    }

    private void OnDestroy()
    {
        ReleasePreview();
    }

    private void ReleasePreview()
    {
        if (previewSprite == null)
        {
            return;
        }

        Texture2D texture = previewSprite.texture;
        Destroy(previewSprite);
        previewSprite = null;

        if (texture != null)
        {
            Destroy(texture);
        }
    }
}
