using System;

namespace WorldCreator;

internal static class OrbitPreviewTiming
{
    /// <summary>Advances one orbit per minute in orbital views, or one body rotation per minute in Landing site at 1x.</summary>
    internal static double PhaseStep(double period, double rotationSpeed, bool landing, double multiplier, double seconds)
    {
        if (!Finite(period) || period <= 0 || !Finite(multiplier) || multiplier <= 0 || !Finite(seconds) || seconds <= 0) return 0;
        var cycles = landing ? period * Math.Abs(rotationSpeed) / 360 : 1;
        if (!Finite(cycles) || cycles <= 0) return 0;
        var step = 360 * multiplier * seconds / (60 * cycles);
        return Finite(step) ? step : 0;
    }

    /// <summary>Rejects a broken draft rather than advancing its preview with invalid angles.</summary>
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
