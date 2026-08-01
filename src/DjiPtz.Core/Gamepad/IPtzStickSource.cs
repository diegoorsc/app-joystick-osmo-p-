namespace DjiPtz.Core.Gamepad;

/// <summary>Fuente de lectura de joysticks para el control PTZ, sea XInput o RawGameController.</summary>
public interface IPtzStickSource
{
    PtzStickFrame Read();
}
