using NUnit.Framework;
using UnityEngine.InputSystem;

namespace HowIFall.PlayModeTests
{
    public sealed class VNInputMapControllerTests : InputTestFixture
    {
        [Test]
        public void CloseOrCancel_RecognizesVirtualGamepadWithoutKeyboard()
        {
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            Press(gamepad.buttonEast);

            Assert.That(
                VNInputMap.WasPressedThisFrame(VNInputAction.CloseOrCancel, null, gamepad),
                Is.True);
        }

        [Test]
        public void CloseOrCancel_StillRecognizesEscape()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Press(keyboard.escapeKey);

            Assert.That(
                VNInputMap.WasPressedThisFrame(VNInputAction.CloseOrCancel, keyboard, null),
                Is.True);
        }

        [Test]
        public void CloseOrCancel_RecognizesVirtualRightMouseButton()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Press(mouse.rightButton);

            Assert.That(
                VNInputMap.WasPressedThisFrame(VNInputAction.CloseOrCancel, null, null, mouse),
                Is.True);
        }

        // Physical wheel forward / away from the player produces positive scrollY
        // in the Unity InputSystem wheel convention and must read as ReadingForward;
        // physical wheel backward / toward the player reads as ReadingBack.
        [TestCase(120f, VNInputAction.ReadingForward)]
        [TestCase(-120f, VNInputAction.ReadingBack)]
        public void ReadingNavigation_RecognizesVirtualMouseWheel(float scrollY, VNInputAction expectedAction)
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Set(mouse.scroll, new UnityEngine.Vector2(0f, scrollY));

            Assert.That(VNInputMap.WasPressedThisFrame(expectedAction, null, null, mouse), Is.True);
            Assert.That(
                VNInputMap.WasPressedThisFrame(
                    expectedAction == VNInputAction.ReadingBack ? VNInputAction.ReadingForward : VNInputAction.ReadingBack,
                    null,
                    null,
                    mouse),
                Is.False);
        }

        [Test]
        public void ReadingNavigation_MapsScrollMagnitudeToOneActionState()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Set(mouse.scroll, new UnityEngine.Vector2(0f, 960f));

            Assert.That(VNInputMap.WasPressedThisFrame(VNInputAction.ReadingForward, null, null, mouse), Is.True);
            Assert.That(VNInputMap.WasPressedThisFrame(VNInputAction.ReadingBack, null, null, mouse), Is.False);
        }
    }
}
