using System;

namespace WorldCreator;

internal static class PressureEstimate
{
    internal readonly struct OrbitPoint
    {
        internal readonly double DistanceAu;
        internal readonly float Irradiance;
        internal readonly float Sunlight;
        /// <summary>Stores the calculated orbital distance, irradiance and normalized sunlight level for one sample.</summary>
        internal OrbitPoint(double distanceAu, float irradiance, float sunlight)
        {
            DistanceAu = distanceAu;
            Irradiance = irradiance;
            Sunlight = sunlight;
        }
    }

    /// <summary>Solves the elliptical orbit at a mean anomaly and calculates its irradiance.</summary>
    internal static OrbitPoint AtMeanAnomaly(double semiMajorAxisAu, double eccentricity, double solarConstant, double degrees)
    {
        if (!Finite(semiMajorAxisAu) || semiMajorAxisAu <= 0 || !Finite(eccentricity) || eccentricity < 0 || eccentricity >= 1 || !Finite(solarConstant) || solarConstant <= 0 || !Finite(degrees))
            throw new ArgumentOutOfRangeException("Use a positive orbit size and solar constant, and eccentricity from 0 to less than 1.");
        var mean = ((degrees % 360 + 360) % 360) * Math.PI / 180;
        var lower = 0d;
        var upper = 2 * Math.PI;
        // Bisection stays convergent even near e = 1, where a fixed Newton iteration count can fail.
        for (var iteration = 0; iteration < 60; iteration++)
        {
            var middle = (lower + upper) / 2;
            if (middle - eccentricity * Math.Sin(middle) < mean)
                lower = middle;
            else
                upper = middle;
        }

        var distance = semiMajorAxisAu * (1 - eccentricity * Math.Cos((lower + upper) / 2));
        var periapsis = semiMajorAxisAu * (1 - eccentricity);
        var apoapsis = semiMajorAxisAu * (1 + eccentricity);
        // Match the engine's float irradiance range and linear 0–100 mapping.
        var solar = (float)solarConstant;
        var minimum = (float)(solar / (apoapsis * apoapsis));
        var maximum = (float)(solar / (periapsis * periapsis));
        var irradiance = (float)(solar / (distance * distance));
        if (float.IsInfinity(maximum) || float.IsInfinity(irradiance) || float.IsNaN(irradiance))
            throw new ArgumentOutOfRangeException("Orbital irradiance is outside the display range.");
        // A circular/float-degenerate range has no varying input; use its lower curve endpoint.
        var sunlight = maximum > minimum ? Math.Max(0, Math.Min(100, (irradiance - minimum) * 100 / (maximum - minimum))) : 0;
        return new OrbitPoint(distance, irradiance, sunlight);
    }

    /// <summary>Estimates ideal-gas pressure from moles, kelvin and the atmosphere-cell volume.</summary>
    internal static double KPa(double gaseousMoles, double kelvin, double volumeLitres = 8000)
    {
        if (!Finite(gaseousMoles) || gaseousMoles < 0 || !Finite(kelvin) || kelvin < 0 || !Finite(volumeLitres) || volumeLitres <= 0)
            throw new ArgumentOutOfRangeException("Pressure requires finite non-negative gas moles and kelvin, and positive volume.");
        var pressure = gaseousMoles * 8.314462618 * kelvin / volumeLitres;
        if (!Finite(pressure))
            throw new ArgumentOutOfRangeException("The pressure estimate is too large.");
        return pressure;
    }

    /// <summary>Checks that a pressure-calculation input is neither infinity nor NaN.</summary>
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
