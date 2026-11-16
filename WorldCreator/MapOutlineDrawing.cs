using System;
using System.Collections.Generic;

namespace WorldCreator;

/// <summary>Draws map outlines as clipped pixel runs without rotating Unity's GUI clipping rectangles.</summary>
internal static class MapOutlineDrawing
{
    /// <summary>Returns horizontal or vertical runs within the viewport, even when zoom puts endpoints offscreen.</summary>
    internal static IEnumerable<(int X, int Y, int Width, int Height)> Runs(
        double fromX, double fromY, double toX, double toY, int width, int height)
    {
        if (width <= 0 || height <= 0 || width > 16384 || height > 16384 ||
            !Finite(fromX) || !Finite(fromY) || !Finite(toX) || !Finite(toY))
            throw new ArgumentOutOfRangeException(nameof(width), "Outline coordinates must be finite and the viewport bounded.");
        var vertical = Math.Abs(toY - fromY) > Math.Abs(toX - fromX);
        var fromMajor = vertical ? fromY : fromX;
        var toMajor = vertical ? toY : toX;
        var fromMinor = vertical ? fromX : fromY;
        var toMinor = vertical ? toX : toY;
        var majorLimit = vertical ? height : width;
        var minorLimit = vertical ? width : height;
        var first = (int)Math.Max(0, Math.Min(majorLimit, Math.Round(Math.Min(fromMajor, toMajor))));
        var last = (int)Math.Min(majorLimit - 1, Math.Max(-1, Math.Round(Math.Max(fromMajor, toMajor))));
        var runStart = -1;
        var runMinor = -1;
        for (var major = first; major <= last; major++)
        {
            var t = fromMajor == toMajor ? 0 : (major - fromMajor) / (toMajor - fromMajor);
            var minorValue = Math.Round(fromMinor + (toMinor - fromMinor) * t);
            var minor = minorValue < 0 || minorValue >= minorLimit ? -1 : (int)minorValue;
            if (minor == runMinor && minor >= 0) continue;
            if (runStart >= 0)
                yield return vertical ? (runMinor, runStart, 1, major - runStart) : (runStart, runMinor, major - runStart, 1);
            runStart = minor >= 0 ? major : -1;
            runMinor = minor;
        }
        if (runStart >= 0)
            yield return vertical ? (runMinor, runStart, 1, last - runStart + 1) : (runStart, runMinor, last - runStart + 1, 1);
    }

    /// <summary>Rejects coordinates that cannot be projected onto a finite GUI viewport.</summary>
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
