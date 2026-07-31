using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace DjiPtz.Core.DirectShow;

/// <summary>
/// P/Invoke puntual a ole32.dll. Solo se usa CreateBindCtx: crear un IBindCtx
/// es el paso previo necesario para enlazar (bind) un IMoniker de dispositivo
/// a su IBaseFilter sin construir ni ejecutar un grafo DirectShow completo,
/// que es justo lo que evita "apropiarse" del stream de vídeo que ya usa OBS.
/// </summary>
internal static class NativeMethods
{
    [DllImport("ole32.dll", ExactSpelling = true, PreserveSig = true)]
    internal static extern int CreateBindCtx(uint reserved, out IBindCtx ppbc);
}
