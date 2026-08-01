namespace DjiPtz.Core.Gamepad;

/// <summary>
/// Fuente de una entrada "de un solo sentido" (0.0 = suelto, 1.0 = a fondo),
/// como un botón (L1) o un gatillo analógico (L2). Se usa para controles
/// tipo T/W (zoom in / zoom out) con dos entradas independientes en vez de
/// un único eje bidireccional.
/// </summary>
public interface IUnipolarSource
{
    double Read();
}
