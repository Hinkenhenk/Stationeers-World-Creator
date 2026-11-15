using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    /// <summary>Adds a constant base-temperature curve when the world does not already define one.</summary>
    internal void AddTemperatureCurve()
    {
        var atmosphere = _world.Element("GlobalAtmosphere") ?? throw new InvalidOperationException("The template has no global atmosphere definition.");
        if (HasTemperatureCurve)
            throw new InvalidOperationException("This world already has a temperature curve.");
        var curve = new XElement("Temperature", TemperatureKey("0"), TemperatureKey("180"));
        atmosphere.AddFirst(curve);
        AddEditableTree(AtmosphereFields, curve, "Atmosphere/GlobalAtmosphere");
        _structureChanged = true;
    }

    /// <summary>Inserts a curve point after validating and committing the curve's pending field edits.</summary>
    internal void AddCurveKey(List<Field> fields, string section, string curvePath)
    {
        var curve = FindEditableCurve(section, curvePath, out var worldPath);
        CommitCurveFields(fields, curvePath);
        var keys = curve.Elements("Key").ToArray();
        var input = NextCurveInput(curve, worldPath);
        var key = keys.Length == 0 ? new XElement(
            "Key",
            new XAttribute("Time", input),
            new XAttribute("Value", "0"),
            new XAttribute("InTangent", "0"),
            new XAttribute("OutTangent", "0"),
            new XAttribute("InWeight", "0"),
            new XAttribute("OutWeight", "0"),
            new XAttribute("WeightedMode", "None")
        ) : new XElement(keys[keys.Length - 1]);
        key.SetAttributeValue("Time", input);
        var next = curve.Elements("Key").FirstOrDefault(existing => double.TryParse((string? )existing.Attribute("Time"), NumberStyles.Float, CultureInfo.InvariantCulture, out var time) && double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var newTime) && time > newTime);
        if (next == null)
            curve.Add(key);
        else
            next.AddBeforeSelf(key);
        RebuildCurveFields(fields, curve, curvePath, worldPath);
        _structureChanged = true;
    }

    /// <summary>Inserts a base-temperature point using the selected solar angle and kelvin value.</summary>
    internal void AddTemperatureKey(List<Field> fields, string curvePath, double solarAngle, double temperatureKelvin)
    {
        if (double.IsNaN(solarAngle) || double.IsInfinity(solarAngle) || solarAngle < 0 || solarAngle > 180)
            throw new InvalidOperationException("Temperature curve time must be between 0° and 180° solar angle.");
        if (double.IsNaN(temperatureKelvin) || double.IsInfinity(temperatureKelvin) || temperatureKelvin < 0 || temperatureKelvin > float.MaxValue)
            throw new InvalidOperationException("Temperature must be zero kelvin or higher and fit the game's 32-bit number range.");
        var curve = FindEditableCurve("Atmosphere", curvePath, out var worldPath);
        if (!worldPath.EndsWith("/Temperature", StringComparison.Ordinal))
            throw new InvalidOperationException("Only the base temperature curve accepts chart points.");
        CommitCurveFields(fields, curvePath);
        if (curve.Elements("Key").Any(key => double.TryParse((string? )key.Attribute("Time"), NumberStyles.Float, CultureInfo.InvariantCulture, out var time) && Math.Abs(time - solarAngle) < 0.001))
            throw new InvalidOperationException("A temperature key already exists at that solar angle.");
        var template = curve.Elements("Key").OrderBy(key => Math.Abs(((double? )key.Attribute("Time") ?? 0) - solarAngle)).FirstOrDefault();
        var added = template == null ? new XElement(
            "Key",
            new XAttribute("InTangent", "0"),
            new XAttribute("OutTangent", "0"),
            new XAttribute("InWeight", "0"),
            new XAttribute("OutWeight", "0"),
            new XAttribute("WeightedMode", "None")
        ) : new XElement(template);
        added.SetAttributeValue("Time", solarAngle.ToString("0.########", CultureInfo.InvariantCulture));
        added.SetAttributeValue("Value", temperatureKelvin.ToString("0.########", CultureInfo.InvariantCulture));
        var next = curve.Elements("Key").FirstOrDefault(key => (double? )key.Attribute("Time") > solarAngle);
        if (next == null)
            curve.Add(added);
        else
            next.AddBeforeSelf(added);
        RebuildCurveFields(fields, curve, curvePath, worldPath);
        _structureChanged = true;
    }

    /// <summary>Removes a selected curve point and rebuilds the corresponding editable fields.</summary>
    internal void RemoveCurveKey(List<Field> fields, string section, string curvePath, int index)
    {
        var curve = FindEditableCurve(section, curvePath, out var worldPath);
        var keys = curve.Elements("Key").ToArray();
        if (keys.Length <= 1)
            throw new InvalidOperationException("A curve must keep at least one key.");
        if (index < 1 || index > keys.Length)
            throw new InvalidOperationException("This curve key no longer exists.");
        CommitCurveFields(fields, curvePath);
        keys[index - 1].Remove();
        RebuildCurveFields(fields, curve, curvePath, worldPath);
        _structureChanged = true;
    }

    /// <summary>Resolves an atmosphere or weather curve path back to its owning XML element.</summary>
    private XElement FindEditableCurve(string section, string curvePath, out string worldPath)
    {
        XElement root;
        string relativePath;
        if (section == "Atmosphere")
        {
            const string prefix = "Atmosphere/";
            if (!curvePath.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidOperationException("Atmosphere curve path is invalid.");
            relativePath = curvePath.Substring(prefix.Length);
            worldPath = relativePath;
            root = _world;
        }
        else if (section == "Weather")
        {
            var separator = curvePath.IndexOf('/');
            if (separator <= 0)
                throw new InvalidOperationException("Weather curve path is invalid.");
            var weatherId = curvePath.Substring(0, separator);
            var effect = _weather.FirstOrDefault(w => (string? )w.Event.Attribute("Id") == weatherId).Event ?? throw new InvalidOperationException("Weather effect is not assigned to this world.");
            root = effect;
            relativePath = curvePath.Substring(separator + 1);
            worldPath = string.Empty;
        }
        else
            throw new InvalidOperationException("Curve editing is supported on Atmosphere and Weather pages.");
        XElement current = root;
        foreach (var segment in relativePath.Split('/'))
        {
            var match = Regex.Match(segment, @"^(.*?) #(\d+)$");
            var name = match.Success ? match.Groups[1].Value : segment;
            var occurrence = match.Success && int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 1;
            current = current.Elements(name).ElementAtOrDefault(occurrence - 1) ?? throw new InvalidOperationException("Curve no longer exists: " + curvePath);
        }

        return current;
    }

    /// <summary>Validates the selected curve's draft values before applying them to XML.</summary>
    private void CommitCurveFields(IEnumerable<Field> fields, string curvePath)
    {
        var prefix = curvePath + "/Key #";
        var curveFields = fields.Where(field => field.Label.Replace(" / ", "/").StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        foreach (var field in curveFields)
            field.Validate();
        foreach (var field in curveFields)
            field.Apply();
    }

    /// <summary>Rebinds curve controls after points are added or removed so paths and indices stay consistent.</summary>
    private void RebuildCurveFields(List<Field> fields, XElement curve, string curvePath, string worldPath)
    {
        var normalizedPath = curvePath + "/Key #";
        fields.RemoveAll(field => field.Label.Replace(" / ", "/").StartsWith(normalizedPath, StringComparison.Ordinal));
        var parentLabelPath = curvePath.Substring(0, curvePath.LastIndexOf('/'));
        AddEditableTree(fields, curve, parentLabelPath);
        if (worldPath.Length == 0)
            return;
        var advancedCurvePath = "World/" + worldPath;
        var advancedPrefix = advancedCurvePath + "/Key #";
        AdvancedFields.RemoveAll(field => field.Label.StartsWith(advancedPrefix, StringComparison.Ordinal));
        foreach (var key in curve.Elements("Key"))
            AddAllFields(AdvancedFields, key, advancedCurvePath);
    }

    /// <summary>Chooses a free curve input, using the widest existing gap for bounded temperature and sunlight curves.</summary>
    private static string NextCurveInput(XElement curve, string worldPath)
    {
        var values = curve.Elements("Key").Select(key => double.TryParse((string? )key.Attribute("Time"), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && !double.IsNaN(value) && !double.IsInfinity(value) ? (double? )value : null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .OrderBy(value => value)
            .ToArray();
        if (worldPath.EndsWith("/Temperature", StringComparison.Ordinal) || worldPath.IndexOf("SolarRadiationTemperature", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var maximum = worldPath.EndsWith("/Temperature", StringComparison.Ordinal) ? 180d : 100d;
            var points = new[]
            {
                0d
            }.Concat(values.Where(value => value > 0 && value < maximum)).Concat(new[] { maximum })
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            var bestStart = 0d;
            var bestGap = -1d;
            for (var i = 1; i < points.Length; i++)
            {
                var gap = points[i] - points[i - 1];
                if (gap > bestGap)
                {
                    bestGap = gap;
                    bestStart = points[i - 1];
                }
            }

            return (bestStart + bestGap / 2).ToString("0.########", CultureInfo.InvariantCulture);
        }

        if (values.Length == 0)
            return "0";
        var span = values[values.Length - 1] - values[0];
        return (values[values.Length - 1] + Math.Max(1, span * 0.1)).ToString("0.########", CultureInfo.InvariantCulture);
    }

    /// <summary>Creates a base-temperature point with the interpolation attributes expected by the game.</summary>
    private static XElement TemperatureKey(string angle) => new XElement(
        "Key",
        new XAttribute("Time", angle),
        new XAttribute("Value", "273.15"),
        new XAttribute("InTangent", "0"),
        new XAttribute("OutTangent", "0"),
        new XAttribute("InWeight", "0"),
        new XAttribute("OutWeight", "0"),
        new XAttribute("WeightedMode", "None")
    );
}
