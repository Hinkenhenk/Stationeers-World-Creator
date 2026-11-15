using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    /// <summary>Rejects edited numbers outside the serializer type or a known game-supported domain.</summary>
    private static void ValidateNumericField(string label, string value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || double.IsNaN(number) || double.IsInfinity(number))
            throw new InvalidOperationException("Enter a finite number using a decimal point: " + label);
        var path = label.ToLowerInvariant();
        var leaf = path.Substring(path.LastIndexOf('/') + 1).Trim().TrimStart('@');
        var integer = leaf == "seed" || leaf == "priority" || leaf == "world list priority" || leaf == "firststormdelay" || (path.Contains("/cooldown /") || path.Contains("/cooldown/"));
        var colorChannel = path.Contains("region") && (leaf == "r" || leaf == "g" || leaf == "b");
        if ((integer || colorChannel) && !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            throw new InvalidOperationException("Enter a whole number between " + int.MinValue + " and " + int.MaxValue + ": " + label);
        if (colorChannel)
            RequireNumericRange(label, number, 0, 255);
        if (leaf == "latitude")
            RequireNumericRange(label, number, -90, 90);
        if (leaf == "longitude")
            RequireNumericRange(label, number, -180, 180);
        if (leaf == "eccentricity" && (number < 0 || number >= 1))
            throw new InvalidOperationException("An elliptical orbit needs eccentricity from 0 up to, but excluding, 1: " + label);
        if (leaf == "period" || leaf == "semimajoraxisau" || leaf == "semimajoraxiskm" || path.EndsWith("/volume / value", StringComparison.Ordinal))
            RequireNumericRange(label, number, double.Epsilon, double.MaxValue);
        if (leaf == "oredensity")
            RequireNumericRange(label, number, 0, 10);
        if (path.Contains("/temperature/key") && leaf == "time")
            RequireNumericRange(label, number, 0, 180);
        if (path.Contains("solarradiationtemperature") && path.Contains("/key") && leaf == "time")
            RequireNumericRange(label, number, 0, 100);
        if (path.Contains("/globalatmosphere/temperature/key") && leaf == "value")
            RequireNumericRange(label, number, 0, float.MaxValue);
        if ((path.Contains("/gas") && leaf == "quantity") || leaf == "solarconstant" || leaf == "primary star irradiance (w/m²)" || leaf == "maximum sun intensity" || leaf == "maxsunintensity" || leaf == "celestial body scale" || leaf == "skybox scale" || leaf == "bodyscale" || leaf == "skyboxscale")
            RequireNumericRange(label, number, 0, float.MaxValue);
        // These fields deserialize as Single; orbit periods/distances and clock offsets use Double.
        var single = path.Contains("/key") || path.Contains("/gas") && leaf == "quantity" || leaf.StartsWith("gravity", StringComparison.Ordinal) || path.Contains("/startdelay /") || path.Contains("/duration /") || leaf == "latitude" || leaf == "longitude" || leaf == "inclination" || leaf == "eccentricity" || leaf == "oredensity";
        if (single && (Math.Abs(number) > float.MaxValue || number != 0 && (float)number == 0))
            throw new InvalidOperationException("This value is outside the game's supported 32-bit number range: " + label);
        if (path.Contains("clock offset") || path.Contains("/timeoffset/"))
        {
            var seconds = path.Contains("days") ? number * 86400 : path.Contains("hours") ? number * 3600 : path.Contains("minutes") ? number * 60 : number;
            if (double.IsInfinity(seconds))
                throw new InvalidOperationException("The clock offset is too large to convert to seconds: " + label);
        }
    }

    /// <summary>Reports a supported numeric interval without silently changing a player's draft.</summary>
    private static void RequireNumericRange(string label, double number, double minimum, double maximum)
    {
        if (number < minimum || number > maximum)
            throw new InvalidOperationException(label + " must be between " + minimum.ToString("G", CultureInfo.InvariantCulture) + " and " + maximum.ToString("G", CultureInfo.InvariantCulture) + ".");
    }

    /// <summary>Checks changed minimum/maximum pairs while retaining the game's negative-maximum sentinel.</summary>
    private static IEnumerable<(bool IsError, string Text)> ValidateNumericRanges(IEnumerable<Field> fields)
    {
        foreach (var group in fields.Where(field => field.IsNumeric).GroupBy(field => field.Label.Substring(0, field.Label.LastIndexOf('/') + 1)))
        {
            if (!group.Any(field => field.Value != field.Original))
                continue;
            var minimum = group.FirstOrDefault(field => field.Label.EndsWith(" / Min", StringComparison.Ordinal));
            var maximum = group.FirstOrDefault(field => field.Label.EndsWith(" / Max", StringComparison.Ordinal));
            if (minimum == null || maximum == null || !double.TryParse(minimum.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var min) || !double.TryParse(maximum.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var max))
                continue;
            if (max >= 0 && min > max)
                yield return (true, "Minimum cannot exceed maximum: " + group.Key.TrimEnd('/', ' '));
        }
    }
}
