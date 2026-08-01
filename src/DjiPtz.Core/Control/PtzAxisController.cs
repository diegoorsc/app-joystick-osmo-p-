namespace DjiPtz.Core.Control;

/// <summary>
/// Integra una velocidad normalizada (-1.0 a 1.0, ya pasada por
/// <see cref="AxisDeadzoneCurve"/>) en incrementos pequeños sobre el valor
/// absoluto real de una propiedad UVC (Pan, Tilt o Zoom), respetando el
/// rango Min/Max y el Step que reporta la propia cámara. Esto es lo que
/// convierte un control que solo admite posiciones absolutas en un
/// comportamiento de joystick PTZ: joystick centrado = quieto, desviado =
/// se mueve mientras se mantenga desviado, más desviado = más rápido.
///
/// También soporta "SeekTowards", un desplazamiento suave hacia un valor
/// objetivo a la misma velocidad máxima configurada — la misma primitiva
/// que usarán "Center Gimbal" y, más adelante, los presets PTZ.
/// </summary>
public sealed class PtzAxisController
{
    private double _accumulator;

    public int Min { get; }
    public int Max { get; }
    public int Step { get; }
    public int DefaultValue { get; }
    public int CurrentValue { get; private set; }

    /// <summary>Unidades del rango de la propiedad que se recorren por segundo a máxima velocidad de joystick.</summary>
    public double MaxSpeedUnitsPerSecond { get; set; }

    public bool Inverted { get; set; }

    public PtzAxisController(int min, int max, int step, int defaultValue, int currentValue, double maxSpeedUnitsPerSecond)
    {
        if (max < min) throw new ArgumentException("Max debe ser >= Min.");

        Min = min;
        Max = max;
        Step = step <= 0 ? 1 : step;
        DefaultValue = Math.Clamp(defaultValue, min, max);
        MaxSpeedUnitsPerSecond = maxSpeedUnitsPerSecond;

        CurrentValue = Math.Clamp(currentValue, min, max);
        _accumulator = CurrentValue;
    }

    /// <summary>
    /// Aplica una velocidad normalizada (salida de AxisDeadzoneCurve) durante
    /// deltaSeconds. Devuelve true si CurrentValue cambió (y por tanto hay
    /// que enviarlo a la cámara).
    /// </summary>
    public bool ApplyJogVelocity(double normalizedVelocity, double deltaSeconds)
    {
        if (normalizedVelocity == 0.0)
        {
            return false;
        }

        double effective = Inverted ? -normalizedVelocity : normalizedVelocity;
        _accumulator += effective * MaxSpeedUnitsPerSecond * deltaSeconds;
        _accumulator = Math.Clamp(_accumulator, Min, Max);

        return CommitAccumulator();
    }

    /// <summary>
    /// Mueve suavemente hacia targetValue a MaxSpeedUnitsPerSecond. Devuelve
    /// true si CurrentValue cambió. Devuelve false también cuando ya se ha
    /// alcanzado el objetivo (no queda nada por hacer).
    /// </summary>
    public bool SeekTowards(int targetValue, double deltaSeconds)
    {
        double target = Math.Clamp(targetValue, Min, Max);
        double diff = target - _accumulator;

        if (diff == 0.0)
        {
            return false;
        }

        double maxStep = MaxSpeedUnitsPerSecond * deltaSeconds;
        if (Math.Abs(diff) <= maxStep || maxStep <= 0)
        {
            _accumulator = target;
        }
        else
        {
            _accumulator += Math.Sign(diff) * maxStep;
        }

        return CommitAccumulator();
    }

    public bool HasReached(int targetValue) => RoundToStep(Math.Clamp(targetValue, Min, Max)) == CurrentValue;

    /// <summary>Sincroniza el controlador con un valor leído directamente de la cámara (p. ej. al arrancar).</summary>
    public void SyncTo(int value)
    {
        CurrentValue = Math.Clamp(value, Min, Max);
        _accumulator = CurrentValue;
    }

    private bool CommitAccumulator()
    {
        int rounded = RoundToStep(_accumulator);
        if (rounded == CurrentValue)
        {
            return false;
        }

        CurrentValue = rounded;
        return true;
    }

    private int RoundToStep(double value)
    {
        int steps = (int)Math.Round((value - Min) / Step, MidpointRounding.AwayFromZero);
        int result = Min + steps * Step;
        return Math.Clamp(result, Min, Max);
    }
}
