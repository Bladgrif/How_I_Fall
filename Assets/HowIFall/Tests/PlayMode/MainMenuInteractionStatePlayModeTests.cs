using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HowIFall.PlayModeTests
{
    public sealed class MainMenuInteractionStatePlayModeTests
    {
        [UnityTest]
        public IEnumerator MouseExitAndNavigation_KeepOneTransientActiveAction()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            MainMenuController menu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.That(menu, Is.Not.Null);
            Button[] actions = menu.PlayerFacingActionButtons.ToArray();
            MainMenuButtonHoverEffect[] effects = actions.Select(b => b.GetComponent<MainMenuButtonHoverEffect>()).ToArray();
            EventSystem system = EventSystem.current;
            bool originalContinue = actions[0].interactable;
            try
            {
                actions[0].interactable = true;
                menu.ApplyPlayerFacingPresentation();
                system.SetSelectedGameObject(null);
                foreach (var effect in effects) { effect.OnPointerExit(null); effect.OnDeselect(null); }
                Color normal = effects[1].CurrentLabelColor;
                Assert.That(effects.All(e => e.CurrentLabelColor == normal && !e.IsFocusAccentVisible), Is.True);

                system.SetSelectedGameObject(actions[3].gameObject);
                Assert.That(effects[3].IsInteractionVisible, Is.True);
                ExecuteEvents.Execute(actions[1].gameObject, new PointerEventData(system), ExecuteEvents.pointerEnterHandler);
                Assert.That(system.currentSelectedGameObject, Is.EqualTo(actions[1].gameObject));
                Assert.That(effects.Count(e => e.IsInteractionVisible), Is.EqualTo(1));
                Assert.That(effects[1].CurrentLabelColor, Is.Not.EqualTo(normal));
                Assert.That(((Image)actions[1].targetGraphic).color.a, Is.InRange(0.01f, 0.06f));
                ExecuteEvents.Execute(actions[1].gameObject, new PointerEventData(system), ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(actions[1].gameObject, new PointerEventData(system), ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(actions[1].gameObject, new PointerEventData(system), ExecuteEvents.pointerExitHandler);
                Assert.That(system.currentSelectedGameObject, Is.EqualTo(actions[1].gameObject), "Keep the navigation anchor.");
                Assert.That(effects.Count(e => e.IsInteractionVisible), Is.Zero, "Mouse selection must not retain hover.");
                Assert.That(effects[1].CurrentLabelColor, Is.EqualTo(normal));

                ExecuteEvents.Execute(actions[1].gameObject,
                    new AxisEventData(system) { moveDir = MoveDirection.Down, moveVector = Vector2.down }, ExecuteEvents.moveHandler);
                Assert.That(system.currentSelectedGameObject, Is.EqualTo(actions[2].gameObject));
                Assert.That(effects[2].IsInteractionVisible, Is.True);
                Assert.That(effects.Count(e => e.IsInteractionVisible), Is.EqualTo(1));
                // Switch to navigation while the pointer is still over a different item.
                ExecuteEvents.Execute(actions[1].gameObject, new PointerEventData(system), ExecuteEvents.pointerEnterHandler);
                ExecuteEvents.Execute(actions[1].gameObject,
                    new AxisEventData(system) { moveDir = MoveDirection.Down, moveVector = Vector2.down }, ExecuteEvents.moveHandler);
                Assert.That(effects[1].IsInteractionVisible, Is.False);
                Assert.That(effects[2].IsInteractionVisible, Is.True);
                Assert.That(effects.Count(e => e.IsInteractionVisible), Is.EqualTo(1));

                ExecuteEvents.Execute(actions[1].gameObject,
                    new PointerEventData(system) { delta = Vector2.right }, ExecuteEvents.pointerMoveHandler);
                Assert.That(system.currentSelectedGameObject, Is.EqualTo(actions[1].gameObject));
                Assert.That(effects[1].IsInteractionVisible, Is.True);
                Assert.That(effects.Count(e => e.IsInteractionVisible), Is.EqualTo(1));
                effects[1].OnPointerExit(null);
                Assert.That(effects.Count(e => e.IsInteractionVisible), Is.Zero);

                actions[0].interactable = false;
                effects[0].RefreshState();
                Color disabled = effects[0].CurrentLabelColor;
                effects[0].OnPointerEnter(new PointerEventData(system));
                effects[0].OnSelect(new BaseEventData(system));
                Assert.That(actions[0].interactable, Is.False);
                Assert.That(effects[0].CurrentLabelColor, Is.EqualTo(disabled).And.Not.EqualTo(normal));
                Assert.That(effects[0].IsInteractionVisible, Is.False);
                Assert.That(effects.All(e => !e.IsFocusAccentVisible), Is.True);
                effects[0].OnPointerExit(null);
                system.SetSelectedGameObject(null);
                menu.FocusDefaultAction();
                Assert.That(system.currentSelectedGameObject, Is.EqualTo(actions[1].gameObject));
                Assert.That(effects.Count(e => e.IsInteractionVisible), Is.EqualTo(1));
            }
            finally
            {
                actions[0].interactable = originalContinue;
                menu.RefreshContinueAvailability();
            }
        }
    }
}
