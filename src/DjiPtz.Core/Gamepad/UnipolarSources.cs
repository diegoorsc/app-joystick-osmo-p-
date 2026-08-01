using Windows.Gaming.Input;

namespace DjiPtz.Core.Gamepad;

/// <summary>Siempre 0.0 — para cuando una función (p. ej. Zoom In/Out por botón) no está en uso.</summary>
public sealed class ConstantUnipolarSource : IUnipolarSource
{
    public double Read() => 0.0;
}

/// <summary>Lee un botón concreto de un RawGameController como 0.0/1.0.</summary>
public sealed class RawButtonUnipolarSource : IUnipolarSource
{
    private readonly GamepadReader _reader;
    private readonly int _buttonIndex;

    public RawButtonUnipolarSource(GamepadReader reader, int buttonIndex)
    {
        _reader = reader;
        _buttonIndex = buttonIndex;
    }

    public double Read()
    {
        var snap = _reader.Read();
        return (_buttonIndex >= 0 && _buttonIndex < snap.Buttons.Length && snap.Buttons[_buttonIndex]) ? 1.0 : 0.0;
    }
}

/// <summary>Lee un eje concreto de un RawGameController directamente como 0.0-1.0 (sin convertir a -1..1), para gatillos analógicos.</summary>
public sealed class RawAxisUnipolarSource : IUnipolarSource
{
    private readonly GamepadReader _reader;
    private readonly int _axisIndex;

    public RawAxisUnipolarSource(GamepadReader reader, int axisIndex)
    {
        _reader = reader;
        _axisIndex = axisIndex;
    }

    public double Read()
    {
        var snap = _reader.Read();
        if (_axisIndex < 0 || _axisIndex >= snap.Axes.Length) return 0.0;
        return Math.Clamp(snap.Axes[_axisIndex], 0.0, 1.0);
    }
}

/// <summary>Botón L1 (LeftShoulder) de un mando XInput, como 0.0/1.0.</summary>
public sealed class XInputLeftShoulderSource : IUnipolarSource
{
    private readonly Windows.Gaming.Input.Gamepad _gamepad;

    public XInputLeftShoulderSource(Windows.Gaming.Input.Gamepad gamepad) => _gamepad = gamepad;

    public double Read() => _gamepad.GetCurrentReading().Buttons.HasFlag(GamepadButtons.LeftShoulder) ? 1.0 : 0.0;
}

/// <summary>Gatillo L2 (LeftTrigger) de un mando XInput, analógico 0.0-1.0.</summary>
public sealed class XInputLeftTriggerSource : IUnipolarSource
{
    private readonly Windows.Gaming.Input.Gamepad _gamepad;

    public XInputLeftTriggerSource(Windows.Gaming.Input.Gamepad gamepad) => _gamepad = gamepad;

    public double Read() => _gamepad.GetCurrentReading().LeftTrigger;
}
