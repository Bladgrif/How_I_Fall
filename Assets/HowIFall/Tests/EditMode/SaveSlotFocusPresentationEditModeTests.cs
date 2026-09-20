using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class SaveSlotFocusPresentationEditModeTests
{
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

    private static ManualSaveSlotView CreateView(out Outline outline, out Image background, out Image hoverAccent)
    {
        GameObject owner = new GameObject("SaveSlotFocusTestView", typeof(RectTransform));
        ManualSaveSlotView view = owner.AddComponent<ManualSaveSlotView>();

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
