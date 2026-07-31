using Windows.Gaming.Input;

namespace DjiPtz.Core.Gamepad;

/// <summary>
/// Envuelve un RawGameController concreto y ofrece una lectura tipada de
/// todos sus ejes/botones/switches en cada instante.
/// </summary>
public sealed class GamepadReader
{
    private readonly RawGameController _controller;

    public GamepadReader(RawGameController controller)
    {
        _controller = controller;
    }

    public string DisplayName => _controller.DisplayName;
    public int AxisCount => _controller.AxisCount;
    public int ButtonCount => _controller.ButtonCount;
    public int SwitchCount => _controller.SwitchCount;
    public ushort HardwareVendorId => _controller.HardwareVendorId;
    public ushort HardwareProductId => _controller.HardwareProductId;

    public GamepadSnapshot Read()
    {
        var buttons = new bool[_controller.ButtonCount];
        var switches = new GameControllerSwitchPosition[_controller.SwitchCount];
        var axes = new double[_controller.AxisCount];

        ulong timestamp = _controller.GetCurrentReading(buttons, switches, axes);

        return new GamepadSnapshot(axes, buttons, switches, timestamp);
    }
}
