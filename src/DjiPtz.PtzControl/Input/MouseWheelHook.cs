using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DjiPtz.PtzControl.Input;

/// <summary>
/// Hook de bajo nivel (WH_MOUSE_LL) para capturar la rueda del mouse de forma
/// global, sin depender de que la consola tenga el foco ni de una ventana
/// propia visible. El Application.DoEvents() que ya corre en el loop
/// principal es el que bombea los mensajes que hacen falta para que el hook
/// reciba WM_MOUSEWHEEL.
/// </summary>
internal sealed class MouseWheelHook : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEWHEEL = 0x020A;

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT Point;
        public int MouseData;
        public int Flags;
        public int Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private readonly LowLevelMouseProc _proc;
    private readonly IntPtr _hookId;
    private int _accumulatedDelta;

    public MouseWheelHook()
    {
        _proc = HookCallback;

        using var currentProcess = Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule!;
        _hookId = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(currentModule.ModuleName), 0);

        if (_hookId == IntPtr.Zero)
        {
            throw new InvalidOperationException("No se pudo instalar el hook de rueda de mouse (SetWindowsHookEx).");
        }
    }

    /// <summary>Devuelve el scroll acumulado (en multiplos de 120 = WHEEL_DELTA) desde la ultima llamada, y lo resetea a 0.</summary>
    public int ConsumeDelta() => Interlocked.Exchange(ref _accumulatedDelta, 0);

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_MOUSEWHEEL)
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            int delta = (short)((data.MouseData >> 16) & 0xFFFF);
            Interlocked.Add(ref _accumulatedDelta, delta);
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose() => UnhookWindowsHookEx(_hookId);
}
