using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using DirectShowLib;
using DjiPtz.Core.Control;
using DjiPtz.Core.DirectShow;

namespace DjiPtz.WebControl;

/// <summary>
/// Sirve una pagina web (WebUi.Html) en la red local para controlar
/// Pan/Tilt/Zoom de la Pocket 3 desde el navegador de un celular: no hace
/// falta Raspberry Pi ni gamepad, solo el PC con Windows donde la camara
/// esta conectada por USB (modo Webcam) y en la misma WiFi que el telefono.
/// Reutiliza exactamente la misma capa de control (AxisDeadzoneCurve +
/// PtzAxisController) que DjiPtz.PtzControl: el joystick tactil de la pagina
/// manda un valor analogico -1..1 por eje, igual que un stick fisico.
/// </summary>
internal static class Program
{
    private const double DefaultHardwareUpdateHz = 15.0;

    private sealed class PadState
    {
        public double X;
        public double Y;
        public DateTime LastUpdateUtc = DateTime.MinValue;
    }

    private sealed class ZoomState
    {
        public double Dir;
        public DateTime LastUpdateUtc = DateTime.MinValue;
    }

    [STAThread]
    private static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("=================================================================");
        Console.WriteLine(" DJI Pocket 3 - Control remoto por pagina web (mouse/gamepad no hacen falta)");
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

        double deadzone = PromptDouble("\nDeadzone del joystick tactil en % (Enter = 15): ", 15.0, 0.0, 90.0) / 100.0;
        double exponent = PromptDouble("Curva progresiva/exponente (Enter = 2.5, 1.0 = lineal): ", 2.5, 0.1, 6.0);
        double panTiltSeconds = PromptDouble("Segundos para barrer el rango completo de Pan/Tilt a maxima velocidad (Enter = 8, mas alto = mas lento): ", 8.0, 0.5, 60.0);
        double zoomSeconds = PromptDouble("Segundos para barrer el rango completo de Zoom a maxima velocidad (Enter = 8, mas alto = mas lento): ", 8.0, 0.5, 60.0);
        double hardwareHz = PromptDouble("Frecuencia maxima de envio de comandos a la camara, en Hz (Enter = 15; mas bajo = mas fluido pero menos reactivo): ", DefaultHardwareUpdateHz, 2.0, 50.0);
        int port = (int)PromptDouble("Puerto TCP para la pagina web (Enter = 8080): ", 8080, 1024, 65535);
        double hardwareIntervalSeconds = 1.0 / hardwareHz;

        var curve = new AxisDeadzoneCurve { Deadzone = deadzone, Exponent = exponent };

        var panCtrl = new PtzAxisController(panRange.Min, panRange.Max, panRange.Step, panRange.Default, panCurrent, (panRange.Max - panRange.Min) / panTiltSeconds);
        var tiltCtrl = new PtzAxisController(tiltRange.Min, tiltRange.Max, tiltRange.Step, tiltRange.Default, tiltCurrent, (tiltRange.Max - tiltRange.Min) / panTiltSeconds)
        {
            Inverted = true,
        };
        var zoomCtrl = new PtzAxisController(zoomRange.Min, zoomRange.Max, zoomRange.Step, zoomRange.Default, zoomCurrent, (zoomRange.Max - zoomRange.Min) / zoomSeconds);

        var pad = new PadState();
        var zoom = new ZoomState();
        var stateLock = new object();
        bool centerRequested = false;
        bool zoomResetRequested = false;

        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        var app = builder.Build();

        app.MapGet("/", () => Results.Content(WebUi.Html, "text/html; charset=utf-8"));

        app.MapPost("/api/pantilt", (PanTiltRequest body) =>
        {
            lock (stateLock)
            {
                pad.X = Math.Clamp(body.X, -1.0, 1.0);
                pad.Y = Math.Clamp(body.Y, -1.0, 1.0);
                pad.LastUpdateUtc = DateTime.UtcNow;
            }
            return Results.Ok();
        });

        app.MapPost("/api/zoom", (ZoomRequest body) =>
        {
            lock (stateLock)
            {
                zoom.Dir = Math.Clamp(body.Dir, -1.0, 1.0);
                zoom.LastUpdateUtc = DateTime.UtcNow;
            }
            return Results.Ok();
        });

        app.MapPost("/api/center", () =>
        {
            lock (stateLock) { centerRequested = true; }
            return Results.Ok();
        });

        app.MapPost("/api/zoomreset", () =>
        {
            lock (stateLock) { zoomResetRequested = true; }
            return Results.Ok();
        });

        app.MapPost("/api/invert", (InvertRequest body) =>
        {
            switch (body.Axis)
            {
                case "pan": panCtrl.Inverted = body.Value; break;
                case "tilt": tiltCtrl.Inverted = body.Value; break;
                case "zoom": zoomCtrl.Inverted = body.Value; break;
            }
            return Results.Ok();
        });

        app.MapGet("/api/status", () => Results.Json(new StatusResponse(
            panCtrl.CurrentValue, tiltCtrl.CurrentValue, zoomCtrl.CurrentValue,
            panCtrl.Inverted, tiltCtrl.Inverted, zoomCtrl.Inverted)));

        var serverTask = app.RunAsync();

        Console.WriteLine("\nServidor web iniciado. Abri esta direccion desde el navegador del celular");
        Console.WriteLine("(el celular tiene que estar conectado a la MISMA red WiFi que esta PC):\n");
        foreach (var ip in GetLanIPv4Addresses())
        {
            Console.WriteLine($"    http://{ip}:{port}");
        }
        Console.WriteLine("\nSi no aparece ninguna direccion arriba, revisa que el PC este conectado por");
        Console.WriteLine("WiFi/Ethernet a la misma red que el celular, y que el firewall de Windows no");
        Console.WriteLine("este bloqueando el puerto (la primera vez Windows suele preguntar; hay que permitirlo).");
        Console.WriteLine("\nPulsa Q + Enter en esta consola para salir.\n");

        var stopwatch = Stopwatch.StartNew();
        double lastSeconds = stopwatch.Elapsed.TotalSeconds;
        int? panSeekTarget = null;
        int? tiltSeekTarget = null;
        int? zoomSeekTarget = null;
        int lastSentPan = panCtrl.CurrentValue;
        int lastSentTilt = tiltCtrl.CurrentValue;
        int lastSentZoom = zoomCtrl.CurrentValue;
        double lastPanSentAt = 0, lastTiltSentAt = 0, lastZoomSentAt = 0;
        string lastError = string.Empty;
        var inputTimeout = TimeSpan.FromMilliseconds(400);

        var quit = false;
        var consoleThread = new Thread(() =>
        {
            while (!quit)
            {
                string? line = Console.ReadLine();
                if (line?.Trim().Equals("Q", StringComparison.OrdinalIgnoreCase) == true)
                {
                    quit = true;
                }
            }
        })
        { IsBackground = true };
        consoleThread.Start();

        while (!quit)
        {
            double nowSeconds = stopwatch.Elapsed.TotalSeconds;
            double dt = nowSeconds - lastSeconds;
            lastSeconds = nowSeconds;
            var utcNow = DateTime.UtcNow;

            double rawX, rawY, rawZoom;
            bool doCenter, doZoomReset;
            lock (stateLock)
            {
                bool padFresh = utcNow - pad.LastUpdateUtc <= inputTimeout;
                bool zoomFresh = utcNow - zoom.LastUpdateUtc <= inputTimeout;
                rawX = padFresh ? pad.X : 0.0;
                rawY = padFresh ? pad.Y : 0.0;
                rawZoom = zoomFresh ? zoom.Dir : 0.0;
                doCenter = centerRequested;
                doZoomReset = zoomResetRequested;
                centerRequested = false;
                zoomResetRequested = false;
            }

            if (doCenter)
            {
                panSeekTarget = panCtrl.DefaultValue;
                tiltSeekTarget = tiltCtrl.DefaultValue;
            }
            if (doZoomReset)
            {
                zoomSeekTarget = zoomCtrl.DefaultValue;
            }

            double panInput = curve.Apply(rawX);
            double tiltInput = curve.Apply(rawY);
            double zoomInput = curve.Apply(rawZoom);

            if (panInput != 0.0) panSeekTarget = null;
            if (tiltInput != 0.0) tiltSeekTarget = null;
            if (zoomInput != 0.0) zoomSeekTarget = null;

            _ = panSeekTarget is int pt ? panCtrl.SeekTowards(pt, dt) : panCtrl.ApplyJogVelocity(panInput, dt);
            _ = tiltSeekTarget is int tt ? tiltCtrl.SeekTowards(tt, dt) : tiltCtrl.ApplyJogVelocity(tiltInput, dt);
            _ = zoomSeekTarget is int zt ? zoomCtrl.SeekTowards(zt, dt) : zoomCtrl.ApplyJogVelocity(zoomInput, dt);

            if (panSeekTarget is int ptt && panCtrl.HasReached(ptt)) panSeekTarget = null;
            if (tiltSeekTarget is int ttt && tiltCtrl.HasReached(ttt)) tiltSeekTarget = null;
            if (zoomSeekTarget is int ztt && zoomCtrl.HasReached(ztt)) zoomSeekTarget = null;

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

            if (!string.IsNullOrEmpty(lastError))
            {
                Console.WriteLine($"ULTIMO ERROR AL ENVIAR A LA CAMARA: {lastError}");
            }

            Thread.Sleep(20);
        }

        Console.WriteLine("\nCerrando servidor web...");
        app.StopAsync().GetAwaiter().GetResult();
        serverTask.GetAwaiter().GetResult();
        Console.WriteLine("Saliendo. La camara se queda en la ultima posicion enviada.");
    }

    private static IReadOnlyList<string> GetLanIPv4Addresses()
    {
        var result = new List<string>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            var props = nic.GetIPProperties();
            foreach (var addr in props.UnicastAddresses)
            {
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    result.Add(addr.Address.ToString());
                }
            }
        }
        return result;
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

    private sealed record PanTiltRequest(double X, double Y);
    private sealed record ZoomRequest(double Dir);
    private sealed record InvertRequest(string Axis, bool Value);
    private sealed record StatusResponse(int Pan, int Tilt, int Zoom, bool PanInverted, bool TiltInverted, bool ZoomInverted);
}
