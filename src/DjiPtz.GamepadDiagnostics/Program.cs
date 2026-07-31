using DjiPtz.Core.Gamepad;
using Windows.Gaming.Input;

Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine("=================================================================");
Console.WriteLine(" DJI Pocket 3 PTZ Controller - Diagnostico de Gamepad (Fase 3)");
Console.WriteLine("=================================================================\n");

var raw = GamepadEnumerator.ListRawControllers();
var xinput = GamepadEnumerator.ListXInputGamepads();

Console.WriteLine($"Mandos detectados via RawGameController (API universal: Xbox, PlayStation y genericos): {raw.Count}");
for (int i = 0; i < raw.Count; i++)
{
    var c = raw[i];
    Console.WriteLine($"  [R{i}] {c.DisplayName}");
    Console.WriteLine($"        VID=0x{c.HardwareVendorId:X4} PID=0x{c.HardwareProductId:X4}  Ejes={c.AxisCount}  Botones={c.ButtonCount}  Switches(D-Pad)={c.SwitchCount}");
}

Console.WriteLine($"\nMandos detectados via Windows.Gaming.Input.Gamepad (compatibles XInput): {xinput.Count}");
for (int i = 0; i < xinput.Count; i++)
{
    Console.WriteLine($"  [X{i}] Mando XInput #{i}");
}

if (raw.Count == 0 && xinput.Count == 0)
{
    Console.WriteLine("\nNo se detecto ningun mando.");
    Console.WriteLine("- Conecta el mando por USB, o emparejalo por Bluetooth desde Configuracion de Windows.");
    Console.WriteLine("- Pulsa algun boton del mando para 'despertarlo'.");
    Console.WriteLine("- Vuelve a ejecutar este programa.");
    return;
}

Console.WriteLine("\nNota: un mando puede aparecer SOLO como [R#] (RawGameController) o SOLO como [X#] (XInput),");
Console.WriteLine("nunca los dos a la vez. Windows enruta cada dispositivo por una via u otra segun como se");
Console.WriteLine("identifique el hardware; ambas vias son validas para este proyecto.");

Console.Write("\nEscribe el mando a monitorizar (ej: R0 o X0): ");
string? input = Console.ReadLine()?.Trim().ToUpperInvariant();

if (string.IsNullOrEmpty(input) || input.Length < 2 || (input[0] != 'R' && input[0] != 'X') || !int.TryParse(input.Substring(1), out int idx))
{
    Console.WriteLine("Seleccion no valida. Usa el formato R0, R1, X0, etc.");
    return;
}

if (input[0] == 'R')
{
    if (idx < 0 || idx >= raw.Count)
    {
        Console.WriteLine("Indice fuera de rango.");
        return;
    }
    MonitorRaw(new GamepadReader(raw[idx]));
}
else
{
    if (idx < 0 || idx >= xinput.Count)
    {
        Console.WriteLine("Indice fuera de rango.");
        return;
    }
    MonitorXInput(xinput[idx]);
}

return;

static string Bar(double value, double min, double max, int width = 40)
{
    double t = (value - min) / (max - min);
    int pos = Math.Clamp((int)Math.Round(t * width), 0, width);
    return new string('#', pos).PadRight(width, '.');
}

static void WaitForKeyToStart()
{
    Console.WriteLine("Mueve cada eje del mando (joysticks, gatillos) y observa que numero cambia.");
    Console.WriteLine("Pulsa Q para salir.\n");
    Console.WriteLine("Pulsa una tecla para empezar...");
    Console.ReadKey(true);
}

static void MonitorRaw(GamepadReader reader)
{
    Console.WriteLine($"\nMonitorizando (RawGameController): {reader.DisplayName}");
    WaitForKeyToStart();

    while (true)
    {
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Q) break;
        }

        var snap = reader.Read();

        Console.Clear();
        Console.WriteLine($"Monitorizando (RawGameController): {reader.DisplayName}   (Q para salir)\n");

        Console.WriteLine("EJES  (rango 0.0 - 1.0 tal como los reporta Windows; un joystick centrado suele marcar ~0.5):");
        for (int i = 0; i < snap.Axes.Length; i++)
        {
            double v = snap.Axes[i];
            Console.WriteLine($"  axis[{i}] = {v,7:F3}  [{Bar(v, 0, 1)}]");
        }

        Console.WriteLine("\nBOTONES pulsados:");
        var pressed = new List<int>();
        for (int i = 0; i < snap.Buttons.Length; i++)
        {
            if (snap.Buttons[i]) pressed.Add(i);
        }
        Console.WriteLine("  " + (pressed.Count == 0 ? "(ninguno)" : string.Join(", ", pressed.Select(b => $"button[{b}]"))));

        if (snap.Switches.Length > 0)
        {
            Console.WriteLine("\nSWITCHES / D-Pad:");
            for (int i = 0; i < snap.Switches.Length; i++)
            {
                Console.WriteLine($"  switch[{i}] = {snap.Switches[i]}");
            }
        }

        Thread.Sleep(80);
    }

    Console.WriteLine("\nSaliendo del monitor de gamepad.");
}

static void MonitorXInput(Gamepad gamepad)
{
    Console.WriteLine("\nMonitorizando (Windows.Gaming.Input.Gamepad / XInput)");
    WaitForKeyToStart();

    while (true)
    {
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Q) break;
        }

        GamepadReading r = gamepad.GetCurrentReading();

        Console.Clear();
        Console.WriteLine("Monitorizando (Windows.Gaming.Input.Gamepad / XInput)   (Q para salir)\n");

        Console.WriteLine("STICK IZQUIERDO  (rango -1.0 a 1.0, centrado en 0.0):");
        Console.WriteLine($"  X = {r.LeftThumbstickX,7:F3}  [{Bar(r.LeftThumbstickX, -1, 1)}]");
        Console.WriteLine($"  Y = {r.LeftThumbstickY,7:F3}  [{Bar(r.LeftThumbstickY, -1, 1)}]");

        Console.WriteLine("\nSTICK DERECHO  (rango -1.0 a 1.0, centrado en 0.0):");
        Console.WriteLine($"  X = {r.RightThumbstickX,7:F3}  [{Bar(r.RightThumbstickX, -1, 1)}]");
        Console.WriteLine($"  Y = {r.RightThumbstickY,7:F3}  [{Bar(r.RightThumbstickY, -1, 1)}]");

        Console.WriteLine("\nGATILLOS  (rango 0.0 a 1.0):");
        Console.WriteLine($"  LT = {r.LeftTrigger,7:F3}  [{Bar(r.LeftTrigger, 0, 1)}]");
        Console.WriteLine($"  RT = {r.RightTrigger,7:F3}  [{Bar(r.RightTrigger, 0, 1)}]");

        Console.WriteLine($"\nBOTONES: {r.Buttons}");

        Thread.Sleep(80);
    }

    Console.WriteLine("\nSaliendo del monitor de gamepad.");
}
