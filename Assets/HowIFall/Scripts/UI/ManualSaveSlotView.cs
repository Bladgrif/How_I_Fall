using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ManualSaveSlotView : MonoBehaviour
{
    public Button button;
    public Image previewImage;
    public TextMeshProUGUI slotNumberText;
    public TextMeshProUGUI dateText;
    public TextMeshProUGUI emptyText;
    public Button deleteButton;

    private ManualSaveLoadPanel panel;
    private int slotIndex;
    private Sprite previewSprite;

    public void Initialize(ManualSaveLoadPanel owner, int index)
    {
        panel = owner;
        slotIndex = index;

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

    public void Render(ManualSaveSlotInfo info, bool saveMode)
    {
        bool occupied = info != null && info.IsOccupied;
        bool loadable = info != null && info.IsLoadable;

        if (slotNumberText != null)
        {
            slotNumberText.text = $"Слот {slotIndex}";
        }

        if (dateText != null)
        {
            dateText.text = loadable ? info.DisplayDate : string.Empty;
        }

        if (emptyText != null)
        {
            emptyText.gameObject.SetActive(!loadable);
            emptyText.text = occupied ? "Недоступное сохранение" : "Пустой слот";
        }

        if (button != null)
        {
            button.interactable = saveMode || loadable;
        }

        if (deleteButton != null)
        {
            deleteButton.gameObject.SetActive(occupied);
            deleteButton.interactable = occupied;
        }

        ApplyPreview(loadable ? info.PreviewPath : string.Empty);
    }

    private void HandleClick()
    {
        panel?.OnSlotSelected(slotIndex);
    }

    private void HandleDeleteClick()
    {
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
        previewImage.color = new Color(0.08f, 0.1f, 0.15f, 1f);

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
