using Windows.Gaming.Input;

namespace DjiPtz.Core.Gamepad;

public sealed class XInputStickSource : IPtzStickSource
{
    private readonly Windows.Gaming.Input.Gamepad _gamepad;

    public XInputStickSource(Windows.Gaming.Input.Gamepad gamepad)
    {
        _gamepad = gamepad;
    }

    public PtzStickFrame Read()
    {
        GamepadReading r = _gamepad.GetCurrentReading();
        return new PtzStickFrame(r.LeftThumbstickX, r.LeftThumbstickY, r.RightThumbstickX, r.RightThumbstickY);
    }
}
