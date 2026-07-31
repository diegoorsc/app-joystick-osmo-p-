using DirectShowLib;

namespace DjiPtz.Core.DirectShow;

public readonly record struct CameraControlRange(
    int Min,
    int Max,
    int Step,
    int Default,
    CameraControlFlags Flags);

public readonly record struct VideoProcAmpRange(
    int Min,
    int Max,
    int Step,
    int Default,
    VideoProcAmpFlags Flags);
