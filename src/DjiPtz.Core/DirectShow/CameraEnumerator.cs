using DirectShowLib;

namespace DjiPtz.Core.DirectShow;

/// <summary>
/// Enumera las cámaras UVC/WDM visibles para DirectShow (la misma lista que
/// ve OBS Studio como "dispositivo de captura de vídeo").
/// </summary>
public static class CameraEnumerator
{
    public static IReadOnlyList<CameraDeviceInfo> ListVideoInputDevices()
    {
        var devices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
        var result = new List<CameraDeviceInfo>(devices.Length);
        foreach (var device in devices)
        {
            result.Add(new CameraDeviceInfo(device));
        }
        return result;
    }

    /// <summary>
    /// Intenta localizar automáticamente la DJI Osmo Pocket 3 (o cualquier
    /// dispositivo DJI) por el nombre que reporta Windows.
    /// </summary>
    public static CameraDeviceInfo? TryFindDji(IReadOnlyList<CameraDeviceInfo> devices)
    {
        foreach (var device in devices)
        {
            if (device.Name.Contains("Pocket", StringComparison.OrdinalIgnoreCase) ||
                device.Name.Contains("DJI", StringComparison.OrdinalIgnoreCase) ||
                device.Name.Contains("OSMO", StringComparison.OrdinalIgnoreCase))
            {
                return device;
            }
        }
        return null;
    }
}
