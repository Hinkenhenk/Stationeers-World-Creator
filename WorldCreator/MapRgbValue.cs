using System.Globalization;

namespace WorldCreator;

/// <summary>Parses the compact RGB editor without accepting partial or out-of-range channel values.</summary>
internal readonly struct MapRgbValue
{
    internal readonly byte R;
    internal readonly byte G;
    internal readonly byte B;
    /// <summary>Stores three validated eight-bit colour channels.</summary>
    internal MapRgbValue(byte r, byte g, byte b) { R = r; G = g; B = b; }
    /// <summary>Requires exactly three comma-separated integers between zero and 255.</summary>
    internal static bool TryParse(string text, out MapRgbValue rgb)
    {
        rgb = default;
        var parts = text.Split(',');
        if (parts.Length != 3 || !byte.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var r)
            || !byte.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var g)
            || !byte.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var b)) return false;
        rgb = new MapRgbValue(r, g, b);
        return true;
    }
}
