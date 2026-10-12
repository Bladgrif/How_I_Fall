using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SaveSlotFocusPresentationEditModeTests
{
    private readonly List<GameObject> views = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject view in views) Object.DestroyImmediate(view);
        views.Clear();
    }

    [Test]
    public void FocusedEmptySaveCard_PresentsStrongFocusBorderAndPlate()
    {
        ManualSaveSlotView view = CreateView(out Outline outline, out Image background, out Image hoverAccent);
        // Default SaveSlotInfo is an empty Manual slot: interactive in Save mode.
        view.Render(new SaveSlotInfo(), true, 1);

        Assert.That(view.button.interactable, Is.True, "Empty Manual slot must stay interactive in Save mode.");
        Color restingBackground = background.color;
        Vector2 restingOutlineDistance = outline.effectDistance;
        Assert.That(restingOutlineDistance, Is.EqualTo(new Vector2(1f, -1f)), "Resting card must keep the thin one-pixel frame.");

        view.OnSelect(null);

        Assert.That(view.HasEventSystemFocus, Is.True);
        Assert.That(outline.effectDistance, Is.EqualTo(new Vector2(2f, -2f)),
            "Focused card must switch to the stronger two-pixel focus border.");
        Assert.That(outline.effectColor.a, Is.GreaterThanOrEqualTo(0.9f),
            "Focused card border must be clearly visible.");
        Assert.That(background.color, Is.Not.EqualTo(restingBackground),
            "Focused card plate must step away from its resting color.");
        Assert.That(hoverAccent.color.a, Is.GreaterThanOrEqualTo(0.25f),
            "Focused card accent underline must be visible.");

        view.OnDeselect(null);

        Assert.That(view.HasEventSystemFocus, Is.False);
        Assert.That(outline.effectDistance, Is.EqualTo(new Vector2(1f, -1f)),
            "Deselect must return the card to the resting frame.");
        Assert.That(background.color, Is.EqualTo(restingBackground),
            "Deselect must restore the resting plate.");
    }

    [Test]
    public void DisabledCard_NeverPresentsFocusState()
    {
        ManualSaveSlotView view = CreateView(out Outline outline, out Image background, out _);
        // Default SaveSlotInfo is an empty Manual slot: not loadable, so Load mode disables it.
        view.Render(new SaveSlotInfo(), false, 1);

        Assert.That(view.button.interactable, Is.False, "Empty slot must be disabled in Load mode.");
        Color restingBackground = background.color;

        view.OnSelect(null);

        Assert.That(outline.effectDistance, Is.EqualTo(new Vector2(1f, -1f)),
            "Disabled card must keep the resting frame instead of a focus border.");
        Assert.That(outline.effectColor.a, Is.LessThan(0.6f),
            "Disabled card must not gain the bright focus border color.");
        Assert.That(background.color, Is.EqualTo(restingBackground),
            "Disabled card must keep its muted resting plate.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void UnavailableSlot_WarningIsDecorativeReusableAndKeepsExistingActions(bool saveMode)
    {
        ManualSaveSlotView view = CreateView(out _, out Image background, out _);
        SaveSlotInfo invalid = SlotInfo(true, false);
        view.Render(invalid, saveMode, 2);

        Transform warning = view.emptyText.transform.parent.Find("Unavailable Warning");
        Assert.That(warning, Is.Not.Null, "Occupied unavailable must differ from empty by a glyph, not only color.");
        Assert.That(warning.gameObject.activeSelf, Is.True);
        Assert.That(warning.GetComponentsInChildren<Image>().Length, Is.EqualTo(5));
        foreach (Image stroke in warning.GetComponentsInChildren<Image>())
            Assert.That(stroke.raycastTarget, Is.False, "Warning must not intercept card/delete input.");
        Assert.That(warning.GetComponentsInChildren<Button>(), Is.Empty);
        Assert.That(view.emptyText.text, Is.EqualTo("Недоступное сохранение"));
        Assert.That(view.emptyText.fontStyle, Is.EqualTo(FontStyles.Normal));
        Assert.That(view.button.interactable, Is.EqualTo(saveMode));
        Assert.That(view.deleteButton.gameObject.activeSelf && view.deleteButton.interactable, Is.True);
        Assert.That(background.color.b, Is.GreaterThan(background.color.r), "Invalid resting card stays navy, not a red plate.");

        view.Render(invalid, saveMode, 2);
        Assert.That(view.emptyText.transform.parent.Find("Unavailable Warning"), Is.SameAs(warning));
        Assert.That(warning.childCount, Is.EqualTo(5), "Refresh must not accumulate glyph geometry.");

        view.Render(new SaveSlotInfo(), saveMode, 2);
        Assert.That(warning.gameObject.activeSelf, Is.False);
        Assert.That(view.emptyText.text, Is.EqualTo("Пусто"));
        Assert.That(view.emptyText.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
        Assert.That(view.deleteButton.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void UnavailableSaveFocus_RemainsBrighterThanWarningAndDoesNotHideIt()
    {
        ManualSaveSlotView view = CreateView(out Outline outline, out _, out Image accent);
        view.Render(SlotInfo(true, false), true, 1);
        view.OnSelect(null);

        Assert.That(view.emptyText.transform.parent.Find("Unavailable Warning").gameObject.activeSelf, Is.True);
        Assert.That(outline.effectDistance, Is.EqualTo(new Vector2(2f, -2f)));
        Assert.That(outline.effectColor.g, Is.GreaterThan(outline.effectColor.r), "Interaction remains cyan even on invalid Manual Save.");
        Assert.That(outline.effectColor.grayscale, Is.GreaterThan(view.emptyText.color.grayscale));
        Assert.That(accent.color.grayscale, Is.LessThan(outline.effectColor.grayscale), "Full-card accent must not compete with the focus frame.");
    }

    [Test]
    public void ValidSlotWithoutPreview_HidesWarningAndKeepsMetadataAndLoadAvailable()
    {
        ManualSaveSlotView view = CreateView(out _, out _, out _);
        view.Render(SlotInfo(true, false), false, 1);
        SaveSlotInfo valid = SlotInfo(true, true);
        typeof(SaveSlotInfo).GetProperty(nameof(SaveSlotInfo.DisplayName)).SetValue(valid, "TECH DEMO ONLY");
        typeof(SaveSlotInfo).GetProperty(nameof(SaveSlotInfo.DisplayDate)).SetValue(valid, "12.10.2026 10:24");
        view.Render(valid, false, 3);

        Assert.That(view.emptyText.transform.parent.Find("Unavailable Warning").gameObject.activeSelf, Is.False);
        Assert.That(view.emptyText.gameObject.activeSelf, Is.False);
        Assert.That(view.IsLoadable && view.button.interactable, Is.True, "Missing thumbnail must not classify a valid slot as invalid.");
        Assert.That(view.previewImage.sprite, Is.Null);
        Assert.That(view.sceneNameText.text, Is.EqualTo("TECH DEMO ONLY"));
        Assert.That(view.dateText.text, Is.EqualTo("12.10.2026 10:24"));
        Assert.That(view.slotNumberText.text, Is.EqualTo("3"));
    }

    private static SaveSlotInfo SlotInfo(bool occupied, bool loadable)
    {
        var info = new SaveSlotInfo();
        typeof(SaveSlotInfo).GetProperty(nameof(SaveSlotInfo.IsOccupied), BindingFlags.Instance | BindingFlags.Public).SetValue(info, occupied);
        typeof(SaveSlotInfo).GetProperty(nameof(SaveSlotInfo.IsLoadable), BindingFlags.Instance | BindingFlags.Public).SetValue(info, loadable);
        return info;
    }

    private TextMeshProUGUI Label(Transform parent, string name)
    {
        var owner = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        owner.transform.SetParent(parent, false);
        return owner.GetComponent<TextMeshProUGUI>();
    }

    private ManualSaveSlotView CreateView(out Outline outline, out Image background, out Image hoverAccent)
    {
        GameObject owner = new GameObject("SaveSlotFocusTestView", typeof(RectTransform));
        ManualSaveSlotView view = owner.AddComponent<ManualSaveSlotView>();
        views.Add(owner);

        view.emptyText = Label(owner.transform, "Empty");
        view.sceneNameText = Label(owner.transform, "Scene Name");
        view.slotNumberText = Label(owner.transform, "Slot Number");
        view.dateText = Label(owner.transform, "Date");
        GameObject previewOwner = new GameObject("Preview", typeof(RectTransform), typeof(Image));
        previewOwner.transform.SetParent(owner.transform, false);
        view.previewImage = previewOwner.GetComponent<Image>();
        GameObject deleteOwner = new GameObject("Delete", typeof(RectTransform), typeof(Image), typeof(Button));
        deleteOwner.transform.SetParent(owner.transform, false);
        view.deleteButton = deleteOwner.GetComponent<Button>();

        GameObject buttonOwner = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonOwner.transform.SetParent(owner.transform, false);
        view.button = buttonOwner.GetComponent<Button>();
        view.button.targetGraphic = buttonOwner.GetComponent<Image>();

        GameObject backgroundOwner = new GameObject("Background", typeof(RectTransform), typeof(Image));
        backgroundOwner.transform.SetParent(owner.transform, false);
        view.backgroundImage = backgroundOwner.GetComponent<Image>();
        background = view.backgroundImage;

        GameObject accentOwner = new GameObject("Hover Accent", typeof(RectTransform), typeof(Image));
        accentOwner.transform.SetParent(owner.transform, false);
        view.hoverAccentImage = accentOwner.GetComponent<Image>();
        hoverAccent = view.hoverAccentImage;

        GameObject outlineOwner = new GameObject("Card Outline", typeof(RectTransform), typeof(Outline));
        outlineOwner.transform.SetParent(owner.transform, false);
        view.cardOutline = outlineOwner.GetComponent<Outline>();
        outline = view.cardOutline;

        return view;
    }
}
