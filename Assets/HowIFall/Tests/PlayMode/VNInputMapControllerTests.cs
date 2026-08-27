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
    }
}
