using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using DirectShowLib;

namespace DjiPtz.Core.DirectShow;

/// <summary>
/// Da acceso a los controles UVC de una cámara (IAMCameraControl / IAMVideoProcAmp)
/// SIN construir un grafo de captura ni conectar pines de vídeo.
///
/// Esto es intencional: el filtro se obtiene enlazando (BindToObject) el
/// IMoniker del dispositivo directamente a IBaseFilter, igual que hace el
/// propio diálogo "Control de cámara" de Windows/OBS. No se llama a
/// IFilterGraph2.Render ni a IMediaControl.Run, así que no se abre el stream
/// de vídeo y el dispositivo no queda en uso exclusivo: OBS puede seguir
/// capturando al mismo tiempo.
/// </summary>
public sealed class CameraControlService : IDisposable
{
    private readonly IBaseFilter _filter;
    private readonly IAMCameraControl? _cameraControl;
    private readonly IAMVideoProcAmp? _videoProcAmp;
    private bool _disposed;

    private CameraControlService(IBaseFilter filter)
    {
        _filter = filter;
        _cameraControl = filter as IAMCameraControl;
        _videoProcAmp = filter as IAMVideoProcAmp;
    }

    public bool SupportsCameraControl => _cameraControl is not null;
    public bool SupportsVideoProcAmp => _videoProcAmp is not null;

    public static CameraControlService Open(CameraDeviceInfo device)
    {
        int hr = NativeMethods.CreateBindCtx(0, out IBindCtx bindCtx);
        Marshal.ThrowExceptionForHR(hr);
        try
        {
            Guid iidBaseFilter = typeof(IBaseFilter).GUID;
            device.Device.Mon.BindToObject(bindCtx, null, ref iidBaseFilter, out object filterObj);
            return new CameraControlService((IBaseFilter)filterObj);
        }
        finally
        {
            Marshal.ReleaseComObject(bindCtx);
        }
    }

    public CameraControlRange GetRange(CameraControlProperty property)
    {
        EnsureCameraControl();
        int hr = _cameraControl!.GetRange(property, out int min, out int max, out int step, out int def, out CameraControlFlags flags);
        Marshal.ThrowExceptionForHR(hr);
        return new CameraControlRange(min, max, step, def, flags);
    }

    public (int Value, CameraControlFlags Flags) Get(CameraControlProperty property)
    {
        EnsureCameraControl();
        int hr = _cameraControl!.Get(property, out int value, out CameraControlFlags flags);
        Marshal.ThrowExceptionForHR(hr);
        return (value, flags);
    }

    public void Set(CameraControlProperty property, int value, CameraControlFlags flags = CameraControlFlags.Manual)
    {
        EnsureCameraControl();
        int hr = _cameraControl!.Set(property, value, flags);
        Marshal.ThrowExceptionForHR(hr);
    }

    public VideoProcAmpRange GetRange(VideoProcAmpProperty property)
    {
        EnsureVideoProcAmp();
        int hr = _videoProcAmp!.GetRange(property, out int min, out int max, out int step, out int def, out VideoProcAmpFlags flags);
        Marshal.ThrowExceptionForHR(hr);
        return new VideoProcAmpRange(min, max, step, def, flags);
    }

    public (int Value, VideoProcAmpFlags Flags) Get(VideoProcAmpProperty property)
    {
        EnsureVideoProcAmp();
        int hr = _videoProcAmp!.Get(property, out int value, out VideoProcAmpFlags flags);
        Marshal.ThrowExceptionForHR(hr);
        return (value, flags);
    }

    public void Set(VideoProcAmpProperty property, int value, VideoProcAmpFlags flags = VideoProcAmpFlags.Manual)
    {
        EnsureVideoProcAmp();
        int hr = _videoProcAmp!.Set(property, value, flags);
        Marshal.ThrowExceptionForHR(hr);
    }

    private void EnsureCameraControl()
    {
        if (_cameraControl is null)
            throw new NotSupportedException("Este dispositivo no expone IAMCameraControl.");
    }

    private void EnsureVideoProcAmp()
    {
        if (_videoProcAmp is null)
            throw new NotSupportedException("Este dispositivo no expone IAMVideoProcAmp.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Marshal.ReleaseComObject(_filter);
    }
}
