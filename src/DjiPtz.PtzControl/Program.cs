using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms;
using DirectShowLib;
using DjiPtz.Core.Control;
using DjiPtz.Core.DirectShow;
using DjiPtz.PtzControl.Input;

namespace DjiPtz.PtzControl;

/// <summary>
/// Junta la camara (Fase 1) con controles de mouse y teclado en un unico loop
/// que mapea Pan/Tilt/Zoom sin necesitar un mando: la rueda del mouse mueve
/// el Pan (cada "click" de scroll desplaza el Pan una cantidad configurable,
/// con un barrido suave hasta llegar), las flechas Arriba/Abajo del teclado
/// mueven el Tilt mientras se mantengan pulsadas (comportamiento de joystick:
/// mas tiempo pulsado = mas movimiento) y las teclas +/- controlan el Zoom
/// de la misma forma.
/// </summary>
internal static class Program
{
    private const double DefaultHardwareUpdateHz = 15.0;

    [STAThread]
    private static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("=================================================================");
        Console.WriteLine(" DJI Pocket 3 PTZ Controller - Control por Mouse + Teclado");
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

        double panRangeSpan = panRange.Max - panRange.Min;
        double defaultPanUnitsPerNotch = Math.Max(panRange.Step, panRangeSpan / 150.0);
        double panUnitsPerNotch = PromptDouble(
            $"\nCuanto se mueve el Pan por cada \"click\" de rueda del mouse (Enter = {defaultPanUnitsPerNotch:F0}): ",
            defaultPanUnitsPerNotch, panRange.Step, Math.Max(panRange.Step, panRangeSpan));

        double panTiltSeconds = PromptDouble("Segundos para barrer el rango completo de Pan/Tilt a maxima velocidad (Enter = 8, mas alto = mas lento): ", 8.0, 0.5, 60.0);
        double zoomSeconds = PromptDouble("Segundos para barrer el rango completo de Zoom a maxima velocidad (Enter = 8, mas alto = mas lento): ", 8.0, 0.5, 60.0);
        double hardwareHz = PromptDouble("Frecuencia maxima de envio de comandos a la camara, en Hz (Enter = 15; mas bajo = mas fluido pero menos reactivo): ", DefaultHardwareUpdateHz, 2.0, 50.0);
        double hardwareIntervalSeconds = 1.0 / hardwareHz;

        var panCtrl = new PtzAxisController(panRange.Min, panRange.Max, panRange.Step, panRange.Default, panCurrent, (panRange.Max - panRange.Min) / panTiltSeconds);
        var tiltCtrl = new PtzAxisController(tiltRange.Min, tiltRange.Max, tiltRange.Step, tiltRange.Default, tiltCurrent, (tiltRange.Max - tiltRange.Min) / panTiltSeconds)
        {
            // Confirmado con hardware real: en esta camara el sentido de Tilt
            // queda al reves del esperado (flecha arriba deberia inclinar
            // hacia arriba). Se puede volver a invertir en caliente con la
            // tecla 2.
            Inverted = true,
        };
        var zoomCtrl = new PtzAxisController(zoomRange.Min, zoomRange.Max, zoomRange.Step, zoomRange.Default, zoomCurrent, (zoomRange.Max - zoomRange.Min) / zoomSeconds);

        int? panSeekTarget = null;
        int? tiltSeekTarget = null;
        int? zoomSeekTarget = null;

        int lastSentPan = panCtrl.CurrentValue;
        int lastSentTilt = tiltCtrl.CurrentValue;
        int lastSentZoom = zoomCtrl.CurrentValue;
        double lastPanSentAt = 0, lastTiltSentAt = 0, lastZoomSentAt = 0;

        Console.WriteLine("\nControles: [Rueda del mouse] Pan  [Flecha Arriba/Abajo] Tilt  [+/-] Zoom");
        Console.WriteLine("Teclas: [C] Center Gimbal  [Z] Reset Zoom/1x  [1] Invertir Pan  [2] Invertir Tilt  [3] Invertir Zoom  [Q] Salir");
        Console.WriteLine("Pulsa una tecla para empezar...");
        Console.ReadKey(true);

        using var mouseWheel = new MouseWheelHook();

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

            int scrollDelta = mouseWheel.ConsumeDelta();
            if (scrollDelta != 0)
            {
                double notches = scrollDelta / 120.0;
                double signedStep = notches * panUnitsPerNotch * (panCtrl.Inverted ? -1 : 1);
                int baseValue = panSeekTarget ?? panCtrl.CurrentValue;
                panSeekTarget = (int)Math.Clamp(Math.Round(baseValue + signedStep), panCtrl.Min, panCtrl.Max);
            }

            double tiltInput = NativeKeyboard.ArrowUp ? 1.0 : NativeKeyboard.ArrowDown ? -1.0 : 0.0;
            double zoomInput = NativeKeyboard.ZoomIn ? 1.0 : NativeKeyboard.ZoomOut ? -1.0 : 0.0;

            if (tiltInput != 0.0) tiltSeekTarget = null;
            if (zoomInput != 0.0) zoomSeekTarget = null;

            if (panSeekTarget is int pt) panCtrl.SeekTowards(pt, dt);
            _ = tiltSeekTarget is int tt ? tiltCtrl.SeekTowards(tt, dt) : tiltCtrl.ApplyJogVelocity(tiltInput, dt);
            _ = zoomSeekTarget is int zt ? zoomCtrl.SeekTowards(zt, dt) : zoomCtrl.ApplyJogVelocity(zoomInput, dt);

            if (panSeekTarget is int ptt && panCtrl.HasReached(ptt)) panSeekTarget = null;
            if (tiltSeekTarget is int ttt && tiltCtrl.HasReached(ttt)) tiltSeekTarget = null;
            if (zoomSeekTarget is int ztt && zoomCtrl.HasReached(ztt)) zoomSeekTarget = null;

            // El valor interno (panCtrl.CurrentValue, etc.) se integra cada frame para
            // que el movimiento sea suave, pero el envio real a la camara se limita a
            // "hardwareHz" veces por segundo: mandarle una posicion nueva cada 20ms
            // (50 veces/seg) es demasiado para el motor del gimbal y se ve a saltos.
            try
            {
                if (panCtrl.CurrentValue != lastSentPan && nowSeconds - lastPanSentAt >= hardwareIntervalSeconds)
                {
                    service.Set(CameraControlProperty.Pan, panCtrl.CurrentValue);
                    lastSentPan = panCtrl.CurrentValue;
                    lastPanSentAt = nowSeconds;
                }
                if (tiltCtrl.CurrentValue != lastSentTilt && nowSeconds - lastTiltSentAt >= hardwareIntervalSeconds)
                {
                    service.Set(CameraControlProperty.Tilt, tiltCtrl.CurrentValue);
                    lastSentTilt = tiltCtrl.CurrentValue;
                    lastTiltSentAt = nowSeconds;
                }
                if (zoomCtrl.CurrentValue != lastSentZoom && nowSeconds - lastZoomSentAt >= hardwareIntervalSeconds)
                {
                    service.Set(CameraControlProperty.Zoom, zoomCtrl.CurrentValue);
                    lastSentZoom = zoomCtrl.CurrentValue;
                    lastZoomSentAt = nowSeconds;
                }
                lastError = string.Empty;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }

            frame++;
            if (frame % 3 == 0)
            {
                Render(cameraName, panCtrl, tiltCtrl, zoomCtrl, panUnitsPerNotch, panTiltSeconds, zoomSeconds, hardwareHz, tiltInput, zoomInput, panSeekTarget, tiltSeekTarget, zoomSeekTarget, lastError);
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
        double panUnitsPerNotch,
        double panTiltSeconds,
        double zoomSeconds,
        double hardwareHz,
        double tiltInput,
        double zoomInput,
        int? panSeekTarget,
        int? tiltSeekTarget,
        int? zoomSeekTarget,
        string lastError)
    {
        Console.Clear();
        Console.WriteLine("DJI Pocket 3 PTZ Controller - Control por Mouse + Teclado");
        Console.WriteLine($"Camara: {cameraName}\n");

        Console.WriteLine($"PAN   min={pan.Min,5} max={pan.Max,5} step={pan.Step,3}  valor={pan.CurrentValue,6}  invertido={(pan.Inverted ? "Si" : "No")}{(panSeekTarget is not null ? "  [moviendose...]" : "")}");
        Console.WriteLine($"TILT  min={tilt.Min,5} max={tilt.Max,5} step={tilt.Step,3}  valor={tilt.CurrentValue,6}  invertido={(tilt.Inverted ? "Si" : "No")}{(tiltSeekTarget is not null ? "  [centrando...]" : "")}");
        Console.WriteLine($"ZOOM  min={zoom.Min,5} max={zoom.Max,5} step={zoom.Step,3}  valor={zoom.CurrentValue,6}  invertido={(zoom.Inverted ? "Si" : "No")}{(zoomSeekTarget is not null ? "  [reseteando...]" : "")}");

        Console.WriteLine($"\nPan por click de scroll: {panUnitsPerNotch:F0} unidades");
        Console.WriteLine($"Flechas Arriba/Abajo (Tilt): {(tiltInput > 0 ? "Arriba" : tiltInput < 0 ? "Abajo" : "-")}");
        Console.WriteLine($"Teclas +/- (Zoom): {(zoomInput > 0 ? "+" : zoomInput < 0 ? "-" : "-")}");

        Console.WriteLine($"\nEnvio a camara: {hardwareHz:F0} Hz");
        Console.WriteLine($"Velocidad Pan/Tilt: {pan.MaxSpeedUnitsPerSecond:F1} u/s (barrido completo en {panTiltSeconds:F1}s)");
        Console.WriteLine($"Velocidad Zoom:     {zoom.MaxSpeedUnitsPerSecond:F1} u/s (barrido completo en {zoomSeconds:F1}s)");

        if (!string.IsNullOrEmpty(lastError))
        {
            Console.WriteLine($"\nULTIMO ERROR AL ENVIAR A LA CAMARA: {lastError}");
        }

        Console.WriteLine("\nControles: [Rueda del mouse] Pan  [Flecha Arriba/Abajo] Tilt  [+/-] Zoom");
        Console.WriteLine("Teclas: [C] Center Gimbal  [Z] Reset Zoom/1x  [1] Invertir Pan  [2] Invertir Tilt  [3] Invertir Zoom  [Q] Salir");
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
