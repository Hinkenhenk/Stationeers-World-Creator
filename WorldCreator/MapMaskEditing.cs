using System;
using System.Collections.Generic;

namespace WorldCreator;

/// <summary>Maps pointer positions to exact mask pixels and joins individual-pixel strokes without gaps.</summary>
internal static class MapMaskEditing
{
    /// <summary>Uses pixel cells rather than nearest vertices, including the PNG's bottom-up row direction.</summary>
    internal static int PixelIndex(double u, double v, int width, int height)
    {
        if (width <= 0 || height <= 0 || double.IsNaN(u) || double.IsNaN(v) || double.IsInfinity(u) || double.IsInfinity(v))
            throw new ArgumentOutOfRangeException(nameof(u), "A mask needs finite coordinates and positive dimensions.");
        var x = Math.Min(width - 1, (int)(Math.Max(0, Math.Min(1, u)) * width));
        var y = Math.Min(height - 1, (int)(Math.Max(0, Math.Min(1, 1 - v)) * height));
        return checked(y * width + x);
    }

    /// <summary>Visits every pixel between two stroke samples using an integer line, without antialiasing.</summary>
    internal static IEnumerable<(int X, int Y)> Line(int x, int y, int endX, int endY)
    {
        var dx = Math.Abs(endX - x);
        var dy = -Math.Abs(endY - y);
        var stepX = x < endX ? 1 : -1;
        var stepY = y < endY ? 1 : -1;
        var error = dx + dy;
        while (true)
        {
            yield return (x, y);
            if (x == endX && y == endY) yield break;
            var twiceError = 2 * error;
            if (twiceError >= dy) { error += dy; x += stepX; }
            if (twiceError <= dx) { error += dx; y += stepY; }
        }
    }
}
