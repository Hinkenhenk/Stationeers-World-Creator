using System;
using System.Globalization;

namespace WorldCreator;

internal static class OrbitPreviewFraming
{
    /// <summary>Accepts complete, finite orbit inputs without replacing a user's temporary editing text.</summary>
    internal static bool TryReadOrbit(string? distance, string? shape, out double semiMajorAxis, out double eccentricity)
    {
        semiMajorAxis = eccentricity = 0;
        return double.TryParse(distance, NumberStyles.Float, CultureInfo.InvariantCulture, out semiMajorAxis)
            && double.TryParse(shape, NumberStyles.Float, CultureInfo.InvariantCulture, out eccentricity)
            && Finite(semiMajorAxis) && semiMajorAxis > 0 && Finite(eccentricity) && eccentricity >= 0 && eccentricity < 1;
    }

    /// <summary>Fits the same orbital scene in every camera preset without changing object positions when the view changes.</summary>
    internal static double HalfHeight(double eccentricity, double aspect, double zoom)
    {
        if (!Finite(eccentricity) || eccentricity < 0 || eccentricity >= 1
            || !Finite(aspect) || aspect <= 0 || !Finite(zoom) || zoom <= 0)
            throw new ArgumentOutOfRangeException(nameof(eccentricity), "Preview orbit, aspect and zoom must be valid finite values.");
        return 1.12 * (1 + eccentricity) / Math.Min(1, aspect) / zoom;
    }

    /// <summary>Rejects numbers that cannot define a camera frustum.</summary>
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
