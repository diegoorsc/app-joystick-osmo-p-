using System.Globalization;
using DirectShowLib;
using DjiPtz.Core.DirectShow;

Console.OutputEncoding = System.Text.Encoding.UTF8;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

Console.WriteLine("=================================================================");
Console.WriteLine(" DJI Pocket 3 PTZ Controller - Diagnostico UVC/DirectShow (Fase 1)");
Console.WriteLine("=================================================================\n");

var devices = CameraEnumerator.ListVideoInputDevices();

if (devices.Count == 0)
{
    Console.WriteLine("No se detecto ningun dispositivo de captura de video (categoria VideoInputDevice).");
    Console.WriteLine("Comprueba que la Osmo Pocket 3 este conectada por USB-C y en modo Webcam.");
    return;
}

Console.WriteLine("Camaras detectadas por DirectShow:\n");
for (int i = 0; i < devices.Count; i++)
{
    Console.WriteLine($"  [{i}] {devices[i].Name}");
    Console.WriteLine($"       DevicePath: {devices[i].DevicePath}");
}

var auto = CameraEnumerator.TryFindDji(devices);
int selected = -1;

if (auto is not null)
{
    int autoIndex = devices.ToList().IndexOf(auto);
    Console.WriteLine($"\nDetectada automaticamente como probable DJI: [{autoIndex}] {auto.Name}");
    Console.Write("Pulsa ENTER para usarla, o escribe el numero de otra camara de la lista: ");
    selected = autoIndex;
}
else
{
    Console.Write("\nNo se detecto automaticamente ninguna camara DJI. Escribe el numero de camara a usar: ");
}

string? input = Console.ReadLine();
if (!string.IsNullOrWhiteSpace(input))
{
    if (int.TryParse(input, out int idx) && idx >= 0 && idx < devices.Count)
    {
        selected = idx;
    }
    else
    {
        Console.WriteLine("Entrada no valida.");
        return;
    }
}

if (selected < 0)
{
    Console.WriteLine("No se selecciono ninguna camara. Saliendo.");
    return;
}

var deviceInfo = devices[selected];
Console.WriteLine($"\nEnlazando con la interfaz de control de: {deviceInfo.Name}");
Console.WriteLine("(solo se abre IBaseFilter para leer/escribir propiedades UVC; NO se abre el stream de video, OBS puede seguir usando la camara)\n");

using var service = CameraControlService.Open(deviceInfo);

Console.WriteLine($"IAMCameraControl soportado : {service.SupportsCameraControl}");
Console.WriteLine($"IAMVideoProcAmp soportado  : {service.SupportsVideoProcAmp}\n");

if (!service.SupportsCameraControl)
{
    Console.WriteLine("Este dispositivo no expone IAMCameraControl. Pan/Tilt/Zoom no estaran disponibles por esta via.");
}

var camProps = new[]
{
    CameraControlProperty.Pan,
    CameraControlProperty.Tilt,
    CameraControlProperty.Roll,
    CameraControlProperty.Zoom,
    CameraControlProperty.Exposure,
    CameraControlProperty.Iris,
    CameraControlProperty.Focus,
};

void PrintCameraControlTable()
{
    Console.WriteLine("---- Propiedades IAMCameraControl ----");
    Console.WriteLine($"{"Propiedad",-10} {"Min",8} {"Max",8} {"Step",6} {"Default",8} {"Current",8} {"Flags",-12} {"Estado"}");
    foreach (var prop in camProps)
    {
        try
        {
            var range = service.GetRange(prop);
            var (value, flags) = service.Get(prop);
            Console.WriteLine($"{prop,-10} {range.Min,8} {range.Max,8} {range.Step,6} {range.Default,8} {value,8} {flags,-12} OK");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{prop,-10} {"-",8} {"-",8} {"-",6} {"-",8} {"-",8} {"-",-12} No soportado ({ex.Message.Trim()})");
        }
    }
    Console.WriteLine();
}

PrintCameraControlTable();

Console.WriteLine("Comandos disponibles:");
Console.WriteLine("  set <propiedad> <valor>   ej: set Pan 5      (Pan, Tilt, Roll, Zoom, Exposure, Iris, Focus)");
Console.WriteLine("  get <propiedad>           ej: get Zoom");
Console.WriteLine("  refresh                    vuelve a leer y mostrar la tabla completa");
Console.WriteLine("  quit                       salir (libera el filtro, no afecta a OBS)");
Console.WriteLine();

while (true)
{
    Console.Write("> ");
    string? line = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(line)) continue;

    var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var cmd = parts[0].ToLowerInvariant();

    try
    {
        if (cmd is "quit" or "exit")
        {
            break;
        }

        if (cmd == "refresh")
        {
            PrintCameraControlTable();
            continue;
        }

        if (cmd == "get" && parts.Length == 2 && Enum.TryParse<CameraControlProperty>(parts[1], true, out var getProp))
        {
            var (value, flags) = service.Get(getProp);
            Console.WriteLine($"{getProp} = {value} ({flags})");
            continue;
        }

        if (cmd == "set" && parts.Length == 3
            && Enum.TryParse<CameraControlProperty>(parts[1], true, out var setProp)
            && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int newValue))
        {
            service.Set(setProp, newValue, CameraControlFlags.Manual);
            Console.WriteLine($"OK -> {setProp} = {newValue}");
            continue;
        }

        Console.WriteLine("Comando no reconocido. Usa: set <prop> <valor> | get <prop> | refresh | quit");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: {ex.Message}");
    }
}

Console.WriteLine("\nCerrando conexion de control. El stream de video de OBS no se ha visto afectado.");
