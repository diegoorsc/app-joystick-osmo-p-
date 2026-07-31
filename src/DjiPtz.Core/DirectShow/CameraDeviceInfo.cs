using DirectShowLib;

namespace DjiPtz.Core.DirectShow;

/// <summary>
/// Representa un dispositivo de la categoría VideoInputDevice (WDM/UVC) tal
/// como lo ve DirectShow. Envuelve el DsDevice subyacente porque su Moniker
/// solo es válido mientras el DsDevice no se haya liberado (Dispose).
/// </summary>
public sealed class CameraDeviceInfo : IDisposable
{
    internal DsDevice Device { get; }

    public string Name => Device.Name ?? "(sin nombre)";
    public string DevicePath => Device.DevicePath ?? string.Empty;

    internal CameraDeviceInfo(DsDevice device)
    {
        Device = device;
    }

    public void Dispose() => Device.Dispose();

    public override string ToString() => Name;
}
