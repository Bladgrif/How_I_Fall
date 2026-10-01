using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Save/Load strip and delete controls keep one strong interaction owner:
/// pointer hover transfers the EventSystem selection to the hovered control,
/// so keyboard/controller selection and mouse hover can never present two
/// controls as equally selected, and Submit always acts on the control the UI
/// presents as selected. Pointer exit keeps the transferred selection, matching
/// the root Game Menu hover contract. Disabled or inactive controls never take
/// the selection.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class PointerEnterSelectionTransfer : MonoBehaviour, IPointerEnterHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        Button button = GetComponent<Button>();
        if (button == null || !button.isActiveAndEnabled || !button.interactable)
        {
            return;
        }

        EventSystem eventSystem = EventSystem.current != null
            ? EventSystem.current
            : FindFirstObjectByType<EventSystem>();
        if (eventSystem != null && eventSystem.currentSelectedGameObject != gameObject)
        {
            eventSystem.SetSelectedGameObject(gameObject);
        }
    }
}
