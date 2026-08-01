using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms;
using DirectShowLib;
using DjiPtz.Core.Control;
using DjiPtz.Core.DirectShow;
using DjiPtz.Core.Gamepad;
using Windows.Gaming.Input;

namespace DjiPtz.PtzControl;

/// <summary>
/// Fase 4: junta la cámara (Fase 1) y el mando (Fase 3) en un único loop de
/// control que mapea joystick -> Pan/Tilt/Zoom con deadzone y curva
/// progresiva, moviendo la cámara en pequeños incrementos mientras el stick
/// permanezca desviado (comportamiento de joystick PTZ profesional, no de
/// posiciones absolutas).
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("=================================================================");
        Console.WriteLine(" DJI Pocket 3 PTZ Controller - Control por Joystick (Fase 4)");
        Console.WriteLine("=================================================================\n");

        using var service = SelectCamera(out string cameraName);
        if (service is null) return;

        if (!TryReadRange(service, CameraControlProperty.Pan, out var panRange, out int panCurrent) ||
            !TryReadRange(service, CameraControlProperty.Tilt, out var tiltRange, out int tiltCurrent) ||
            !TryReadRange(service, CameraControlProperty.Zoom, out var zoomRange, out int zoomCurrent))
        {
            Console.WriteLine("\nEsta camara no expone Pan/Tilt/Zoom via IAMCameraControl. No se puede continuar.");
            return;
        }

        Console.WriteLine($"\nPan  : min={panRange.Min} max={panRange.Max} step={panRange.Step} default={panRange.Default} actual={panCurrent}");
        Console.WriteLine($"Tilt : min={tiltRange.Min} max={tiltRange.Max} step={tiltRange.Step} default={tiltRange.Default} actual={tiltCurrent}");
        Console.WriteLine($"Zoom : min={zoomRange.Min} max={zoomRange.Max} step={zoomRange.Step} default={zoomRange.Default} actual={zoomCurrent}");

        IPtzStickSource? stickSource = SelectGamepad();
        if (stickSource is null) return;

        double deadzone = PromptDouble("\nDeadzone en % (Enter = 15): ", 15.0, 0.0, 90.0) / 100.0;
        double exponent = PromptDouble("Curva progresiva/exponente (Enter = 2.5, 1.0 = lineal): ", 2.5, 0.1, 6.0);
        double panTiltSeconds = PromptDouble("Segundos para barrer el rango completo de Pan/Tilt a maxima velocidad (Enter = 2.5): ", 2.5, 0.2, 30.0);
        double zoomSeconds = PromptDouble("Segundos para barrer el rango completo de Zoom a maxima velocidad (Enter = 4.0): ", 4.0, 0.2, 30.0);

        var curve = new AxisDeadzoneCurve { Deadzone = deadzone, Exponent = exponent };

        var panCtrl = new PtzAxisController(panRange.Min, panRange.Max, panRange.Step, panRange.Default, panCurrent, (panRange.Max - panRange.Min) / panTiltSeconds);
        var tiltCtrl = new PtzAxisController(tiltRange.Min, tiltRange.Max, tiltRange.Step, tiltRange.Default, tiltCurrent, (tiltRange.Max - tiltRange.Min) / panTiltSeconds);
        var zoomCtrl = new PtzAxisController(zoomRange.Min, zoomRange.Max, zoomRange.Step, zoomRange.Default, zoomCurrent, (zoomRange.Max - zoomRange.Min) / zoomSeconds);

        int? panSeekTarget = null;
        int? tiltSeekTarget = null;
        int? zoomSeekTarget = null;

        Console.WriteLine("\nTeclas: [C] Center Gimbal  [Z] Reset Zoom/1x  [1] Invertir Pan  [2] Invertir Tilt  [3] Invertir Zoom  [Q] Salir");
        Console.WriteLine("Pulsa una tecla para empezar...");
        Console.ReadKey(true);

        var stopwatch = Stopwatch.StartNew();
        double lastSeconds = stopwatch.Elapsed.TotalSeconds;
        int frame = 0;
        string lastError = string.Empty;

        while (true)
        {
            Application.DoEvents();

            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                switch (key)
                {
                    case ConsoleKey.Q:
                        goto exitLoop;
                    case ConsoleKey.C:
                        panSeekTarget = panCtrl.DefaultValue;
                        tiltSeekTarget = tiltCtrl.DefaultValue;
                        break;
                    case ConsoleKey.Z:
                        zoomSeekTarget = zoomCtrl.DefaultValue;
                        break;
                    case ConsoleKey.D1:
                        panCtrl.Inverted = !panCtrl.Inverted;
                        break;
                    case ConsoleKey.D2:
                        tiltCtrl.Inverted = !tiltCtrl.Inverted;
                        break;
                    case ConsoleKey.D3:
                        zoomCtrl.Inverted = !zoomCtrl.Inverted;
                        break;
                }
            }

            double nowSeconds = stopwatch.Elapsed.TotalSeconds;
            double dt = nowSeconds - lastSeconds;
            lastSeconds = nowSeconds;

            var stick = stickSource.Read();
            double panInput = curve.Apply(stick.LeftX);
            double tiltInput = curve.Apply(stick.LeftY);
            double zoomInput = curve.Apply(stick.RightY);

            if (panInput != 0.0) panSeekTarget = null;
            if (tiltInput != 0.0) tiltSeekTarget = null;
            if (zoomInput != 0.0) zoomSeekTarget = null;

            bool panChanged = panSeekTarget is int pt
                ? panCtrl.SeekTowards(pt, dt)
                : panCtrl.ApplyJogVelocity(panInput, dt);

            bool tiltChanged = tiltSeekTarget is int tt
                ? tiltCtrl.SeekTowards(tt, dt)
                : tiltCtrl.ApplyJogVelocity(tiltInput, dt);

            bool zoomChanged = zoomSeekTarget is int zt
                ? zoomCtrl.SeekTowards(zt, dt)
                : zoomCtrl.ApplyJogVelocity(zoomInput, dt);

            if (panSeekTarget is int ptt && panCtrl.HasReached(ptt)) panSeekTarget = null;
            if (tiltSeekTarget is int ttt && tiltCtrl.HasReached(ttt)) tiltSeekTarget = null;
            if (zoomSeekTarget is int ztt && zoomCtrl.HasReached(ztt)) zoomSeekTarget = null;

            try
            {
                if (panChanged) service.Set(CameraControlProperty.Pan, panCtrl.CurrentValue);
                if (tiltChanged) service.Set(CameraControlProperty.Tilt, tiltCtrl.CurrentValue);
                if (zoomChanged) service.Set(CameraControlProperty.Zoom, zoomCtrl.CurrentValue);
                lastError = string.Empty;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }

            frame++;
            if (frame % 3 == 0)
            {
                Render(cameraName, panCtrl, tiltCtrl, zoomCtrl, curve, panTiltSeconds, zoomSeconds, stick, panInput, tiltInput, zoomInput, panSeekTarget, tiltSeekTarget, zoomSeekTarget, lastError);
            }

            Thread.Sleep(20);
        }

        exitLoop:
        Console.WriteLine("\nSaliendo. La camara se queda en la ultima posicion enviada.");
    }

    private static void Render(
        string cameraName,
        PtzAxisController pan,
        PtzAxisController tilt,
        PtzAxisController zoom,
        AxisDeadzoneCurve curve,
        double panTiltSeconds,
        double zoomSeconds,
        PtzStickFrame stick,
        double panInput,
        double tiltInput,
        double zoomInput,
        int? panSeekTarget,
        int? tiltSeekTarget,
        int? zoomSeekTarget,
        string lastError)
    {
        Console.Clear();
        Console.WriteLine("DJI Pocket 3 PTZ Controller - Control por Joystick (Fase 4)");
        Console.WriteLine($"Camara: {cameraName}\n");

        Console.WriteLine($"PAN   min={pan.Min,5} max={pan.Max,5} step={pan.Step,3}  valor={pan.CurrentValue,6}  invertido={(pan.Inverted ? "Si" : "No")}{(panSeekTarget is not null ? "  [centrando...]" : "")}");
        Console.WriteLine($"TILT  min={tilt.Min,5} max={tilt.Max,5} step={tilt.Step,3}  valor={tilt.CurrentValue,6}  invertido={(tilt.Inverted ? "Si" : "No")}{(tiltSeekTarget is not null ? "  [centrando...]" : "")}");
        Console.WriteLine($"ZOOM  min={zoom.Min,5} max={zoom.Max,5} step={zoom.Step,3}  valor={zoom.CurrentValue,6}  invertido={(zoom.Inverted ? "Si" : "No")}{(zoomSeekTarget is not null ? "  [reseteando...]" : "")}");

        Console.WriteLine($"\nStick izquierdo:  X={stick.LeftX,6:F2}  Y={stick.LeftY,6:F2}   -> panIn={panInput,5:F2}  tiltIn={tiltInput,5:F2}");
        Console.WriteLine($"Stick derecho:    X={stick.RightX,6:F2}  Y={stick.RightY,6:F2}   -> zoomIn={zoomInput,5:F2}");

        Console.WriteLine($"\nDeadzone: {curve.Deadzone * 100:F0}%   Curva (exponente): {curve.Exponent:F1}");
        Console.WriteLine($"Velocidad Pan/Tilt: {pan.MaxSpeedUnitsPerSecond:F1} u/s (barrido completo en {panTiltSeconds:F1}s)");
        Console.WriteLine($"Velocidad Zoom:     {zoom.MaxSpeedUnitsPerSecond:F1} u/s (barrido completo en {zoomSeconds:F1}s)");

        if (!string.IsNullOrEmpty(lastError))
        {
            Console.WriteLine($"\nULTIMO ERROR AL ENVIAR A LA CAMARA: {lastError}");
        }

        Console.WriteLine("\nTeclas: [C] Center Gimbal  [Z] Reset Zoom/1x  [1] Invertir Pan  [2] Invertir Tilt  [3] Invertir Zoom  [Q] Salir");
    }

    private static bool TryReadRange(CameraControlService service, CameraControlProperty property, out CameraControlRange range, out int current)
    {
        try
        {
            range = service.GetRange(property);
            (current, _) = service.Get(property);
            return true;
        }
        catch
        {
            range = default;
            current = 0;
            return false;
        }
    }

    private static CameraControlService? SelectCamera(out string cameraName)
    {
        cameraName = string.Empty;

        var devices = CameraEnumerator.ListVideoInputDevices();
        if (devices.Count == 0)
        {
            Console.WriteLine("No se detecto ningun dispositivo de captura de video.");
            return null;
        }

        Console.WriteLine("Camaras detectadas:");
        for (int i = 0; i < devices.Count; i++)
        {
            Console.WriteLine($"  [{i}] {devices[i].Name}");
        }

        var auto = CameraEnumerator.TryFindDji(devices);
        int selected = -1;
        if (auto is not null)
        {
            selected = devices.ToList().IndexOf(auto);
            Console.Write($"\nDetectada automaticamente: [{selected}] {auto.Name}. Pulsa ENTER para usarla, o escribe otro numero: ");
        }
        else
        {
            Console.Write("\nEscribe el numero de camara a usar: ");
        }

        string? input = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(input))
        {
            if (!int.TryParse(input, out selected) || selected < 0 || selected >= devices.Count)
            {
                Console.WriteLine("Seleccion no valida.");
                return null;
            }
        }

        if (selected < 0)
        {
            Console.WriteLine("No se selecciono ninguna camara.");
            return null;
        }

        cameraName = devices[selected].Name;
        return CameraControlService.Open(devices[selected]);
    }

    private static IPtzStickSource? SelectGamepad()
    {
        IReadOnlyList<RawGameController> raw = Array.Empty<RawGameController>();
        IReadOnlyList<Windows.Gaming.Input.Gamepad> xinput = Array.Empty<Windows.Gaming.Input.Gamepad>();

        for (int attempt = 0; attempt < 30; attempt++)
        {
            Application.DoEvents();
            raw = GamepadEnumerator.ListRawControllers();
            xinput = GamepadEnumerator.ListXInputGamepads();
            if (raw.Count > 0 || xinput.Count > 0) break;
            Thread.Sleep(100);
        }

        Console.WriteLine($"\nMandos RawGameController: {raw.Count}");
        for (int i = 0; i < raw.Count; i++)
        {
            Console.WriteLine($"  [R{i}] {raw[i].DisplayName}  Ejes={raw[i].AxisCount}");
        }

        Console.WriteLine($"Mandos XInput: {xinput.Count}");
        for (int i = 0; i < xinput.Count; i++)
        {
            Console.WriteLine($"  [X{i}] Mando XInput #{i}");
        }

        if (raw.Count == 0 && xinput.Count == 0)
        {
            Console.WriteLine("No se detecto ningun mando.");
            return null;
        }

        Console.Write("\nEscribe el mando a usar (ej: R0 o X0): ");
        string? input = Console.ReadLine()?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(input) || input.Length < 2 || (input[0] != 'R' && input[0] != 'X') || !int.TryParse(input.Substring(1), out int idx))
        {
            Console.WriteLine("Seleccion no valida.");
            return null;
        }

        if (input[0] == 'X')
        {
            if (idx < 0 || idx >= xinput.Count)
            {
                Console.WriteLine("Indice fuera de rango.");
                return null;
            }
            Console.WriteLine("Usando mando XInput: stick izquierdo -> Pan/Tilt, stick derecho (Y) -> Zoom.");
            return new XInputStickSource(xinput[idx]);
        }

        if (idx < 0 || idx >= raw.Count)
        {
            Console.WriteLine("Indice fuera de rango.");
            return null;
        }

        var reader = new GamepadReader(raw[idx]);
        Console.WriteLine("\nEste mando no indica que eje es cada joystick. Usa la Fase 3 (DjiPtz.GamepadDiagnostics)");
        Console.WriteLine("si no lo has hecho ya, para saber que axis[N] corresponde a cada stick.");

        int leftX = (int)PromptDouble("Indice del eje X del stick izquierdo (Enter = 0): ", 0, 0, 63);
        int leftY = (int)PromptDouble("Indice del eje Y del stick izquierdo (Enter = 1): ", 1, 0, 63);
        int rightX = (int)PromptDouble("Indice del eje X del stick derecho (Enter = 2): ", 2, 0, 63);
        int rightY = (int)PromptDouble("Indice del eje Y del stick derecho (Enter = 3): ", 3, 0, 63);

        return new RawStickSource(reader, leftX, leftY, rightX, rightY);
    }

    private static double PromptDouble(string prompt, double defaultValue, double min, double max)
    {
        Console.Write(prompt);
        string? input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue;
        }

        if (double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            return Math.Clamp(value, min, max);
        }

        Console.WriteLine($"Valor no valido, usando por defecto {defaultValue}.");
        return defaultValue;
    }
}
