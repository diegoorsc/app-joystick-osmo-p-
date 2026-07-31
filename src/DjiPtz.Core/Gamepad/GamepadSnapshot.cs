using Windows.Gaming.Input;

namespace DjiPtz.Core.Gamepad;

/// <summary>
/// Lectura instantánea de todos los ejes/botones/switches de un
/// RawGameController. Los ejes vienen normalizados por Windows en el rango
/// [0.0, 1.0] (un joystick centrado suele reportar ~0.5 en cada eje).
/// </summary>
public sealed record GamepadSnapshot(
    double[] Axes,
    bool[] Buttons,
    GameControllerSwitchPosition[] Switches,
    ulong Timestamp);
