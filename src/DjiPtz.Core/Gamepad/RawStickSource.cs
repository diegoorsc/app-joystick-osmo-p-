namespace DjiPtz.Core.Gamepad;

/// <summary>
/// Adaptador de un RawGameController a PtzStickFrame. Los índices de eje se
/// asignan manualmente (viendo la lectura en vivo de la Fase 3), porque
/// RawGameController no indica qué eje es cada joystick.
/// </summary>
public sealed class RawStickSource : IPtzStickSource
{
    private readonly GamepadReader _reader;

    public int LeftXAxis { get; }
    public int LeftYAxis { get; }
    public int RightXAxis { get; }
    public int RightYAxis { get; }

    public RawStickSource(GamepadReader reader, int leftXAxis, int leftYAxis, int rightXAxis, int rightYAxis)
    {
        _reader = reader;
        LeftXAxis = leftXAxis;
        LeftYAxis = leftYAxis;
        RightXAxis = rightXAxis;
        RightYAxis = rightYAxis;
    }

    public PtzStickFrame Read()
    {
        var snap = _reader.Read();
        return new PtzStickFrame(
            ToSigned(snap.Axes, LeftXAxis),
            ToSigned(snap.Axes, LeftYAxis),
            ToSigned(snap.Axes, RightXAxis),
            ToSigned(snap.Axes, RightYAxis));
    }

    private static double ToSigned(double[] axes, int index)
    {
        if (index < 0 || index >= axes.Length) return 0.0;
        // RawGameController reporta los ejes en [0.0, 1.0]; el PTZ trabaja en [-1.0, 1.0] centrado en 0.
        return (axes[index] - 0.5) * 2.0;
    }
}
