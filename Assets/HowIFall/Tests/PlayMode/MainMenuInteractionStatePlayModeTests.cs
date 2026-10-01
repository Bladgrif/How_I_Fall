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
                AdvanceAll(effects, MainMenuButtonHoverEffect.InteractionFadeDuration);
                Color normal = effects[1].CurrentLabelColor;
                Assert.That(effects.All(e => e.CurrentLabelColor == normal && !e.IsFocusAccentVisible), Is.True);

                float originalTimeScale = Time.timeScale;
                try
                {
                    Time.timeScale = 0f;
                    effects[1].OnPointerEnter(new PointerEventData(system));
                    yield return new WaitForSecondsRealtime(0.04f);
                    Assert.That(((Image)actions[1].targetGraphic).color.a, Is.GreaterThan(0f),
                        "Main Menu hover fade stopped when scaled time was paused.");
                }
                finally
                {
                    Time.timeScale = originalTimeScale;
                    effects[1].OnPointerExit(null);
                    AdvanceAll(effects, MainMenuButtonHoverEffect.InteractionFadeDuration);
                    system.SetSelectedGameObject(null);
                }

                system.SetSelectedGameObject(actions[3].gameObject);
                AdvanceAll(effects, MainMenuButtonHoverEffect.InteractionFadeDuration);
                Assert.That(effects[3].IsInteractionVisible, Is.True);
                ExecuteEvents.Execute(actions[1].gameObject, new PointerEventData(system), ExecuteEvents.pointerEnterHandler);
                Assert.That(system.currentSelectedGameObject, Is.EqualTo(actions[1].gameObject));
                Assert.That(effects.Count(e => e.IsInteractionVisible), Is.EqualTo(1));
                AdvanceAll(effects, MainMenuButtonHoverEffect.InteractionFadeDuration * 0.5f);
                Assert.That(((Image)actions[1].targetGraphic).color.a, Is.InRange(0.05f, 0.35f),
                    "Hover plate must fade in before reaching its final alpha.");
                Assert.That(effects[1].FocusAccentColor.a, Is.InRange(0.1f, 0.9f),
                    "Cyan accent must share the hover fade.");
                Assert.That(((Image)actions[3].targetGraphic).color.a, Is.InRange(0.05f, 0.35f),
                    "Previous action must fade out when pointer moves to another row.");
                AdvanceAll(effects, MainMenuButtonHoverEffect.InteractionFadeDuration);
                Assert.That(effects[3].IsFocusAccentVisible, Is.False, "Old action retained a focus accent after fade-out.");
                Assert.That(effects[1].CurrentLabelColor, Is.Not.EqualTo(normal));
                Assert.That(((Image)actions[1].targetGraphic).color.a, Is.InRange(0.25f, 0.55f),
                    "Hover must show the target v1 translucent glass plate.");
                ExecuteEvents.Execute(actions[1].gameObject, new PointerEventData(system), ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(actions[1].gameObject, new PointerEventData(system), ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(actions[1].gameObject, new PointerEventData(system), ExecuteEvents.pointerExitHandler);
                Assert.That(system.currentSelectedGameObject, Is.EqualTo(actions[1].gameObject), "Keep the navigation anchor.");
                Assert.That(effects.Count(e => e.IsInteractionVisible), Is.Zero, "Mouse selection must not retain hover.");
                AdvanceAll(effects, MainMenuButtonHoverEffect.InteractionFadeDuration * 0.5f);
                Assert.That(((Image)actions[1].targetGraphic).color.a, Is.InRange(0.05f, 0.35f),
                    "Pointer exit must fade toward normal rather than snap off.");
                AdvanceAll(effects, MainMenuButtonHoverEffect.InteractionFadeDuration);
                Assert.That(effects[1].CurrentLabelColor, Is.EqualTo(normal));
                Assert.That(effects[1].IsFocusAccentVisible || effects[1].IsSelectionGlowVisible, Is.False);

                ExecuteEvents.Execute(actions[1].gameObject,
                    new AxisEventData(system) { moveDir = MoveDirection.Down, moveVector = Vector2.down }, ExecuteEvents.moveHandler);
                AdvanceAll(effects, MainMenuButtonHoverEffect.InteractionFadeDuration);
                Assert.That(system.currentSelectedGameObject, Is.EqualTo(actions[2].gameObject));
                Assert.That(effects[2].IsInteractionVisible, Is.True);
                Assert.That(effects.Count(e => e.IsInteractionVisible), Is.EqualTo(1));
                // Switch to navigation while the pointer is still over a different item.
                ExecuteEvents.Execute(actions[1].gameObject, new PointerEventData(system), ExecuteEvents.pointerEnterHandler);
                ExecuteEvents.Execute(actions[1].gameObject,
                    new AxisEventData(system) { moveDir = MoveDirection.Down, moveVector = Vector2.down }, ExecuteEvents.moveHandler);
                AdvanceAll(effects, MainMenuButtonHoverEffect.InteractionFadeDuration);
                Assert.That(effects[1].IsInteractionVisible, Is.False);
                Assert.That(effects[2].IsInteractionVisible, Is.True);
                Assert.That(effects.Count(e => e.IsInteractionVisible), Is.EqualTo(1));

                ExecuteEvents.Execute(actions[1].gameObject,
                    new PointerEventData(system) { delta = Vector2.right }, ExecuteEvents.pointerMoveHandler);
                AdvanceAll(effects, MainMenuButtonHoverEffect.InteractionFadeDuration);
                Assert.That(system.currentSelectedGameObject, Is.EqualTo(actions[1].gameObject));
                Assert.That(effects[1].IsInteractionVisible, Is.True);
                Assert.That(effects.Count(e => e.IsInteractionVisible), Is.EqualTo(1));
                effects[1].OnPointerExit(null);
                AdvanceAll(effects, MainMenuButtonHoverEffect.InteractionFadeDuration);
                Assert.That(effects.Count(e => e.IsInteractionVisible), Is.Zero);

                effects[0].OnPointerEnter(new PointerEventData(system));
                AdvanceAll(effects, MainMenuButtonHoverEffect.InteractionFadeDuration * 0.5f);
                actions[0].interactable = false;
                effects[0].RefreshState();
                Color disabled = effects[0].CurrentLabelColor;
                effects[0].OnPointerEnter(new PointerEventData(system));
                effects[0].OnSelect(new BaseEventData(system));
                Assert.That(actions[0].interactable, Is.False);
                Assert.That(effects[0].CurrentLabelColor, Is.EqualTo(disabled).And.Not.EqualTo(normal));
                Assert.That(effects[0].IsInteractionVisible, Is.False);
                Assert.That(((Image)actions[0].targetGraphic).color.a, Is.Zero,
                    "Disabling Continue must clear a partial plate immediately.");
                Assert.That(effects.All(e => !e.IsFocusAccentVisible), Is.True);
                effects[0].OnPointerExit(null);
                actions[1].GetComponent<MainMenuButtonHoverEffect>().OnPointerEnter(new PointerEventData(system));
                AdvanceAll(effects, MainMenuButtonHoverEffect.InteractionFadeDuration * 0.3f);
                system.SetSelectedGameObject(null);
                actions[1].gameObject.SetActive(false);
                actions[1].gameObject.SetActive(true);
                Assert.That(((Image)actions[1].targetGraphic).color.a, Is.Zero,
                    "Re-enabled action flashed a stale half-faded plate.");
                Assert.That(effects[1].IsFocusAccentVisible || effects[1].IsSelectionGlowVisible, Is.False);
                menu.FocusDefaultAction();
                AdvanceAll(effects, MainMenuButtonHoverEffect.InteractionFadeDuration);
                Assert.That(system.currentSelectedGameObject, Is.EqualTo(actions[1].gameObject));
                Assert.That(effects.Count(e => e.IsInteractionVisible), Is.EqualTo(1));
            }
            finally
            {
                actions[0].interactable = originalContinue;
                menu.RefreshContinueAvailability();
            }
        }

        private static void AdvanceAll(MainMenuButtonHoverEffect[] effects, float seconds)
        {
            foreach (MainMenuButtonHoverEffect effect in effects)
            {
                effect.AdvanceInteractionFade(seconds);
            }
        }
    }
}
