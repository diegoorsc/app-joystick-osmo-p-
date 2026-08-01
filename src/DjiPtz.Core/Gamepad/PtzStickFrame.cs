namespace DjiPtz.Core.Gamepad;

/// <summary>
/// Lectura normalizada de los dos joysticks, independiente de si el mando
/// se lee vía RawGameController o vía Windows.Gaming.Input.Gamepad (XInput).
/// Todos los valores están en [-1.0, 1.0], centrados en 0.0.
/// </summary>
public readonly record struct PtzStickFrame(
    double LeftX,
    double LeftY,
    double RightX,
    double RightY);
