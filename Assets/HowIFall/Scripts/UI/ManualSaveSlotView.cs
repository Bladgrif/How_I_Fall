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
    private static readonly Color FocusColor = new Color(0.085f, 0.135f, 0.20f, 0.98f);
    // Unavailable is occupied, not a red action plate. The label/glyph carry the
    // warning while the stronger cyan frame remains reserved for interaction.
    private static readonly Color InvalidColor = OccupiedColor;
    private static readonly Color UnavailableMessageColor = new Color(0.72f, 0.45f, 0.49f, 0.92f);
    private static readonly Color FocusOutlineColor = new Color(0.28f, 0.76f, 0.90f, 0.95f);

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
    private int localSlotIndex;
    private Sprite previewSprite;
    private bool pointerInside;
    private bool pointerDown;
    private bool isLoadable;
    private bool isOccupied;
    private float hoverAmount;
    private bool hasEventSystemFocus;
    private RectTransform unavailableWarning;
    private Vector2 placeholderPosition;
    private bool hasPlaceholderPosition;

    public bool HasEventSystemFocus => hasEventSystemFocus;
    public bool IsLoadable => isLoadable;
    public bool IsOccupied => isOccupied;
    public int GlobalSlotIndex => slotIndex;

    public void ApplyStoryMomentLayout()
    {
        if (previewFrameImage != null)
        {
            RectTransform previewRect = previewFrameImage.rectTransform;
            previewRect.anchoredPosition = new Vector2(0f, -92f);
            previewRect.sizeDelta = new Vector2(446f, 158f);
        }
        if (sceneNameText != null)
        {
            sceneNameText.rectTransform.anchoredPosition = new Vector2(232f, 48f);
            sceneNameText.rectTransform.sizeDelta = new Vector2(426f, 30f);
            sceneNameText.fontSize = 20f;
        }
        if (slotNumberText != null)
        {
            slotNumberText.rectTransform.anchoredPosition = new Vector2(76f, 18f);
            slotNumberText.fontSize = 17f;
        }
        if (dateText != null)
        {
            dateText.rectTransform.anchoredPosition = new Vector2(-130f, 18f);
            dateText.fontSize = 17f;
            // A slightly stronger tone than the resting prefab grey: the date is the
            // smallest card text and loses the most at the 1280x720 scale.
            dateText.color = new Color(0.60f, 0.70f, 0.80f, 0.95f);
        }
        if (emptyText != null) emptyText.fontSize = 20f;
    }

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

    public void Render(SaveSlotInfo info, bool saveMode, int localIndex)
    {
        localSlotIndex = localIndex;
        slotIndex = info != null ? info.SlotIndex : localIndex;
        SaveSlotType slotType = info != null ? info.SlotType : SaveSlotType.Manual;
        isOccupied = info != null && info.IsOccupied;
        isLoadable = info != null && info.IsLoadable;

        if (slotNumberText != null)
        {
            slotNumberText.text = isLoadable ? localSlotIndex.ToString() : string.Empty;
            slotNumberText.gameObject.SetActive(isLoadable);
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
            emptyText.text = isOccupied ? "Недоступное сохранение" : "Пусто";
            emptyText.color = isOccupied
                ? UnavailableMessageColor
                : new Color(0.48f, 0.55f, 0.63f, 0.68f);
        }
        ApplyUnavailableWarning(isOccupied && !isLoadable);

        if (backgroundSlotNumberText != null)
        {
            backgroundSlotNumberText.gameObject.SetActive(false);
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
                : new Color(0.52f, 0.25f, 0.30f, 0.45f);
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

    private void ApplyUnavailableWarning(bool visible)
    {
        if (emptyText == null) return;

        RectTransform label = emptyText.rectTransform;
        if (!hasPlaceholderPosition)
        {
            placeholderPosition = label.anchoredPosition;
            hasPlaceholderPosition = true;
        }
        label.anchoredPosition = placeholderPosition + (visible ? new Vector2(0f, -18f) : Vector2.zero);
        emptyText.fontStyle = visible ? FontStyles.Normal : FontStyles.Italic;

        if (visible && unavailableWarning == null)
        {
            // Runtime-only, font-independent line art: no sprite asset, serialized
            // reference, navigation target or added hit region.
            var owner = new GameObject("Unavailable Warning", typeof(RectTransform));
            unavailableWarning = owner.GetComponent<RectTransform>();
            unavailableWarning.SetParent(label.parent, false);
            unavailableWarning.anchorMin = label.anchorMin;
            unavailableWarning.anchorMax = label.anchorMax;
            unavailableWarning.anchoredPosition = placeholderPosition + new Vector2(0f, 22f);
            unavailableWarning.sizeDelta = new Vector2(28f, 24f);
            AddWarningStroke("Left", new Vector2(-12f, -9f), new Vector2(0f, 11f));
            AddWarningStroke("Right", new Vector2(0f, 11f), new Vector2(12f, -9f));
            AddWarningStroke("Base", new Vector2(12f, -9f), new Vector2(-12f, -9f));
            AddWarningStroke("Stem", new Vector2(0f, 4f), new Vector2(0f, -2f));
            AddWarningStroke("Dot", new Vector2(-1f, -5f), new Vector2(1f, -5f));
        }
        if (unavailableWarning != null) unavailableWarning.gameObject.SetActive(visible);
    }

    private void AddWarningStroke(string name, Vector2 from, Vector2 to)
    {
        var owner = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        RectTransform rect = owner.GetComponent<RectTransform>();
        rect.SetParent(unavailableWarning, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = (from + to) * 0.5f;
        rect.sizeDelta = new Vector2(Vector2.Distance(from, to), 2f);
        Vector2 direction = to - from;
        rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        UnityEngine.UI.Image image = owner.GetComponent<UnityEngine.UI.Image>();
        image.color = UnavailableMessageColor;
        image.raycastTarget = false;
    }

    public void OnSelect(BaseEventData eventData)
    {
        hasEventSystemFocus = true;
        panel?.OnSlotFocused(slotIndex);
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
        panel?.OnSlotFocused(slotIndex);
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
            // Keyboard focus gets its own stronger plate so the selected card is
            // unmistakable next to the lower info panel while staying quiet.
            backgroundImage.color = Color.Lerp(restingColor, focusAmount > 0f ? FocusColor : HoverColor, emphasisAmount);
        }

        if (hoverAccentImage != null)
        {
            // The authored full-card accent used a bright blue fill. Keep its
            // existing emphasis timing/strength, but let the frame own focus.
            Color accent = new Color(0.10f, 0.21f, 0.29f, 0f);
            accent.a = Mathf.Max(hoverAmount * 0.09f, focusAmount * 0.30f);
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
            cardOutline.effectColor = Color.Lerp(restingOutline, focusAmount > 0f ? FocusOutlineColor : hoverOutline, emphasisAmount);
            // A focus border must survive bright preview art: two offset pixels
            // instead of the thin one-pixel resting/hover frame.
            cardOutline.effectDistance = focusAmount > 0f ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
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
