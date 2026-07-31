using Windows.Gaming.Input;

namespace DjiPtz.Core.Gamepad;

/// <summary>
/// Enumera mandos usando Windows.Gaming.Input.RawGameController, la API que
/// mejor cubre Xbox, PlayStation (DualShock/DualSense) y mandos genéricos
/// USB/Bluetooth: expone ejes y botones "en crudo" (sin asumir qué eje es
/// cada joystick), que es justo lo que necesitamos para dejar que el
/// usuario asigne manualmente cada eje a Pan/Tilt/Zoom.
///
/// Windows.Gaming.Input.Gamepad (XInput) se expone también solo a modo
/// informativo: los mandos de Xbox aparecen en ambas listas, pero un mando
/// PlayStation nativo normalmente solo aparece como RawGameController.
/// </summary>
public static class GamepadEnumerator
{
    public static IReadOnlyList<RawGameController> ListRawControllers()
        => RawGameController.RawGameControllers;

    public static IReadOnlyList<Windows.Gaming.Input.Gamepad> ListXInputGamepads()
        => Windows.Gaming.Input.Gamepad.Gamepads;
}
