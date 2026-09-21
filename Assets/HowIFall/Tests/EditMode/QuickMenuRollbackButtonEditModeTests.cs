using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Regression for the rollback migration: the side Game Menu lost its rollback
/// entry, so the Quick Menu strip must own a runtime-built compact "Назад"
/// action that routes through the same TryRollback contract as the mouse wheel.
/// </summary>
public class QuickMenuRollbackButtonEditModeTests
{
    [Test]
    public void ApplyPlayerFacingPresentation_BuildsRollbackFirst_AndKeepsStripCompact()
    {
        GameObject owner = new GameObject("QuickMenuRollbackOwner", typeof(RectTransform));
        GameObject root = new GameObject("QuickMenuRollbackRoot", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        root.transform.SetParent(owner.transform, false);
        try
        {
            VNQuickMenu menu = owner.AddComponent<VNQuickMenu>();
            menu.root = root;
            menu.historyButton = CreateStripButton(root.transform, "History");
            menu.skipButton = CreateStripButton(root.transform, "Skip");
            menu.autoButton = CreateStripButton(root.transform, "Auto");
            menu.quickSaveButton = CreateStripButton(root.transform, "QuickSave");

            menu.ApplyPlayerFacingPresentation();

            Assert.That(menu.rollbackButton, Is.Not.Null, "Rollback button must be built at runtime.");
            Assert.That(menu.rollbackButton.transform.parent, Is.EqualTo(root.transform),
                "Rollback button must be a direct member of the Quick Menu strip.");
            Assert.That(menu.rollbackButton.gameObject.activeSelf, Is.True,
                "Rollback button must be visible in ordinary reading.");
            TextMeshProUGUI label = menu.rollbackButton.GetComponentInChildren<TextMeshProUGUI>(true);
            Assert.That(label, Is.Not.Null, "Rollback button must expose a TMP label.");
            Assert.That(label.text, Is.EqualTo("Назад"), "Rollback action must keep the compact player-facing label.");
            Assert.That(menu.rollbackButton.transform.GetSiblingIndex(), Is.EqualTo(0),
                "Rollback must sit first in the reading-action strip.");

            Button[] visibleButtons = root.GetComponentsInChildren<Button>(true)
                .Where(button => button.transform.parent == root.transform && button.gameObject.activeSelf)
                .OrderBy(button => button.transform.GetSiblingIndex())
                .ToArray();
            Assert.That(visibleButtons, Is.EqualTo(new[]
            {
                menu.rollbackButton, menu.historyButton, menu.skipButton, menu.autoButton, menu.quickSaveButton
            }), "Strip order must be Rollback / History / Skip / Auto / Quick Save.");
            Assert.That(((RectTransform)root.transform).sizeDelta.x, Is.GreaterThanOrEqualTo(450f),
                "Strip width must cover its label-driven buttons instead of overflowing the row.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void EnsureRollbackButton_IsIdempotent()
    {
        GameObject owner = new GameObject("QuickMenuRollbackIdempotentOwner", typeof(RectTransform));
        GameObject root = new GameObject("QuickMenuRollbackIdempotentRoot", typeof(RectTransform));
        root.transform.SetParent(owner.transform, false);
        try
        {
            VNQuickMenu menu = owner.AddComponent<VNQuickMenu>();
            menu.root = root;

            Assert.That(menu.EnsureRollbackButton(), Is.True, "First ensure call must build the rollback button.");
            Assert.That(menu.EnsureRollbackButton(), Is.False, "Second ensure call must reuse the existing button.");
            menu.ApplyPlayerFacingPresentation();
            Assert.That(menu.EnsureRollbackButton(), Is.False, "Presentation application must not duplicate the rollback button.");
            Assert.That(root.GetComponentsInChildren<Button>(true)
                .Count(button => button == menu.rollbackButton), Is.EqualTo(1),
                "Quick Menu strip must contain exactly one rollback button.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    [Test]
    public void RollbackClick_WithoutDialogueController_DoesNotThrow()
    {
        GameObject owner = new GameObject("QuickMenuRollbackClickOwner", typeof(RectTransform));
        GameObject root = new GameObject("QuickMenuRollbackClickRoot", typeof(RectTransform));
        root.transform.SetParent(owner.transform, false);
        try
        {
            VNQuickMenu menu = owner.AddComponent<VNQuickMenu>();
            menu.root = root;
            menu.ApplyPlayerFacingPresentation();

            Assert.That(menu.rollbackButton, Is.Not.Null, "Rollback button must exist before the click probe.");
            Assert.DoesNotThrow(() => menu.rollbackButton.onClick.Invoke(),
                "A missing dialogue controller must degrade to a no-op, not an exception.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    private static Button CreateStripButton(Transform parent, string label)
    {
        GameObject buttonObject = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = label;
        return buttonObject.GetComponent<Button>();
    }
}
