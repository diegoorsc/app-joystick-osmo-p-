namespace DjiPtz.Core.Control;

/// <summary>
/// Convierte la posición cruda de un eje de joystick (-1.0 a 1.0, centrado
/// en 0.0) en una velocidad normalizada, aplicando una zona muerta (deadzone)
/// y una curva progresiva/exponencial: cerca del centro apenas hay
/// velocidad, y esta crece cada vez más rápido cuanto más se aleja el
/// joystick del centro. Así, un ligero desplazamiento produce un movimiento
/// lento y un desplazamiento grande produce un movimiento rápido, en vez de
/// saltos escalonados.
/// </summary>
public sealed class AxisDeadzoneCurve
{
    private double _deadzone = 0.15;
    private double _exponent = 2.5;

    /// <summary>Fracción del recorrido (0.0 a &lt;1.0) que se ignora cerca del centro.</summary>
    public double Deadzone
    {
        get => _deadzone;
        set => _deadzone = Math.Clamp(value, 0.0, 0.95);
    }

    /// <summary>
    /// Exponente de la curva progresiva. 1.0 = lineal. Mayor que 1.0 = más
    /// progresiva (más control fino cerca del centro, más velocidad en los
    /// extremos).
    /// </summary>
    public double Exponent
    {
        get => _exponent;
        set => _exponent = Math.Max(0.1, value);
    }

    /// <summary>
    /// Aplica deadzone + curva a un valor crudo en [-1.0, 1.0]. Devuelve una
    /// velocidad normalizada también en [-1.0, 1.0].
    /// </summary>
    public double Apply(double rawValue)
    {
        double clamped = Math.Clamp(rawValue, -1.0, 1.0);
        double magnitude = Math.Abs(clamped);

        if (magnitude <= _deadzone)
        {
            return 0.0;
        }

        double normalized = (magnitude - _deadzone) / (1.0 - _deadzone);
        double curved = Math.Pow(normalized, _exponent);
        return Math.Sign(clamped) * curved;
    }
}
