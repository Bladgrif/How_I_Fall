using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HowIFall.PlayModeTests
{
    public sealed class PreferencesInteractionPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator MasterVolume_RealSliderPointerDragAndMoveEvents_ApplyAndClamp()
        {
            var canvasObject = new GameObject("PreferencesInteractionCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var eventObject = new GameObject("PreferencesInteractionEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var managerObject = SettingsManager.Instance == null ? new GameObject("PreferencesInteractionSettings", typeof(SettingsManager)) : null;
            SettingsManager manager = SettingsManager.Instance;
            GameSettings previous = manager.settings;
            GameObject host = new GameObject("PreferencesInteractionHost", typeof(RectTransform));
            host.transform.SetParent(canvasObject.transform, false);
            SharedPreferencesView view = SharedPreferencesView.Create(host.transform, "PlayModeInteraction");
            var controller = new PreferencesController(new PreferencesService(), view);
            controller.Initialize();
            manager.settings = new GameSettings { masterVolume = 0.2f };
            controller.Open();
            yield return null;
            Canvas.ForceUpdateCanvases();

            Slider slider = view.GetSlider(SharedPreferencesView.MasterVolumeId);
            Assert.That(slider, Is.Not.Null);
            float initial = slider.value;
            Vector3[] corners = new Vector3[4];
            slider.GetComponent<RectTransform>().GetWorldCorners(corners);
            Vector2 dragPoint = RectTransformUtility.WorldToScreenPoint(null, Vector3.Lerp(corners[0], corners[3], 0.82f));
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = dragPoint };
            ExecuteEvents.Execute<IPointerDownHandler>(slider.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute<IDragHandler>(slider.gameObject, pointer, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute<IEndDragHandler>(slider.gameObject, pointer, ExecuteEvents.endDragHandler);
            Assert.That(slider.value, Is.GreaterThan(initial));
            Assert.That(manager.CurrentSettings.masterVolume, Is.EqualTo(slider.value).Within(0.001f));

            EventSystem.current.SetSelectedGameObject(slider.gameObject);
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            Press(gamepad.dpad.right);
            ExecuteEvents.Execute<IMoveHandler>(slider.gameObject, new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Right }, ExecuteEvents.moveHandler);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(slider.gameObject));
            Assert.That(manager.CurrentSettings.masterVolume, Is.EqualTo(slider.value).Within(0.001f));

            slider.value = slider.maxValue;
            ExecuteEvents.Execute<IMoveHandler>(slider.gameObject, new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Right }, ExecuteEvents.moveHandler);
            Assert.That(slider.value, Is.EqualTo(slider.maxValue));
            slider.value = slider.minValue;
            ExecuteEvents.Execute<IMoveHandler>(slider.gameObject, new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Left }, ExecuteEvents.moveHandler);
            Assert.That(slider.value, Is.EqualTo(slider.minValue));

            manager.settings = previous;
            Object.Destroy(view.gameObject); Object.Destroy(host); Object.Destroy(canvasObject); Object.Destroy(eventObject);
            if (managerObject != null) Object.Destroy(managerObject);
        }
    }
}
