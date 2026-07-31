using DjiPtz.Core.Gamepad;

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
    Console.WriteLine($"  [{i}] {c.DisplayName}");
    Console.WriteLine($"       VID=0x{c.HardwareVendorId:X4} PID=0x{c.HardwareProductId:X4}  Ejes={c.AxisCount}  Botones={c.ButtonCount}  Switches(D-Pad)={c.SwitchCount}");
}

Console.WriteLine($"\nMandos detectados via Windows.Gaming.Input.Gamepad (compatibles XInput, tipicamente mandos Xbox): {xinput.Count}");
if (xinput.Count == 0)
{
    Console.WriteLine("  (ninguno - normal si tu mando es PlayStation nativo o generico: se vera igualmente arriba, en RawGameController)");
}

if (raw.Count == 0)
{
    Console.WriteLine("\nNo se detecto ningun mando.");
    Console.WriteLine("- Conecta el mando por USB, o emparejalo por Bluetooth desde Configuracion de Windows.");
    Console.WriteLine("- Pulsa algun boton del mando para 'despertarlo'.");
    Console.WriteLine("- Vuelve a ejecutar este programa.");
    return;
}

Console.Write("\nEscribe el numero del mando (de la lista RawGameController) a monitorizar en vivo: ");
string? input = Console.ReadLine();
if (!int.TryParse(input, out int idx) || idx < 0 || idx >= raw.Count)
{
    Console.WriteLine("Seleccion no valida.");
    return;
}

var reader = new GamepadReader(raw[idx]);

Console.WriteLine($"\nMonitorizando: {reader.DisplayName}");
Console.WriteLine("Mueve cada eje del mando (joysticks, gatillos) y observa que numero cambia.");
Console.WriteLine("Pulsa Q para salir.\n");
Console.WriteLine("Pulsa una tecla para empezar...");
Console.ReadKey(true);

while (true)
{
    if (Console.KeyAvailable)
    {
        var key = Console.ReadKey(true);
        if (key.Key == ConsoleKey.Q) break;
    }

    var snap = reader.Read();

    Console.Clear();
    Console.WriteLine($"Monitorizando: {reader.DisplayName}   (Q para salir)\n");

    Console.WriteLine("EJES  (rango 0.0 - 1.0 tal como los reporta Windows; un joystick centrado suele marcar ~0.5):");
    for (int i = 0; i < snap.Axes.Length; i++)
    {
        double v = snap.Axes[i];
        int barLen = Math.Clamp((int)Math.Round(v * 40), 0, 40);
        string bar = new string('#', barLen).PadRight(40, '.');
        Console.WriteLine($"  axis[{i}] = {v:F3}  [{bar}]");
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
