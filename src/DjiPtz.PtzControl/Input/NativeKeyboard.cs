using System.Runtime.InteropServices;

namespace DjiPtz.PtzControl.Input;

/// <summary>
/// Lee el estado actual (pulsado/no pulsado) de teclas del teclado fisico via
/// GetAsyncKeyState, de forma global (no depende de que la consola tenga el
/// foco), igual que XInput permitia leer el estado del mando en cada frame.
/// Esto es lo que permite tratar "flecha mantenida" como una velocidad
/// continua, en vez de un evento de tecla suelta como Console.ReadKey.
/// </summary>
internal static class NativeKeyboard
{
    private const int VK_UP = 0x26;
    private const int VK_DOWN = 0x28;
    private const int VK_ADD = 0x6B;
    private const int VK_SUBTRACT = 0x6D;
    private const int VK_OEM_PLUS = 0xBB;
    private const int VK_OEM_MINUS = 0xBD;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private static bool IsDown(int vKey) => (GetAsyncKeyState(vKey) & 0x8000) != 0;

    public static bool ArrowUp => IsDown(VK_UP);
    public static bool ArrowDown => IsDown(VK_DOWN);
    public static bool ZoomIn => IsDown(VK_ADD) || IsDown(VK_OEM_PLUS);
    public static bool ZoomOut => IsDown(VK_SUBTRACT) || IsDown(VK_OEM_MINUS);
}
