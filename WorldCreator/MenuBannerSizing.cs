using System;

namespace WorldCreator;

internal static class MenuBannerSizing
{
    /// <summary>Fits readable banner rows where possible and reports the content height needed for scrolling.</summary>
    internal static (double RowHeight, double ContentHeight) Fit(double available, double preferred, int rows, double padding, double spacing)
    {
        rows = Math.Max(1, rows);
        var gaps = padding + spacing * (rows - 1);
        var height = Math.Min(preferred, Math.Max(Math.Min(80, preferred), (available - gaps) / rows));
        return (height, rows * height + gaps);
    }
}
