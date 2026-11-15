using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    /// <summary>Groups XML-backed fields into named cards and renders them in responsive columns.</summary>
    private void DrawFieldCards(IReadOnlyList<WorldProject.Field> fields)
    {
        DrawCardColumns(fields.GroupBy(FieldGroupName).ToArray(), group =>
        {
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            GUILayout.Label(group.Key, _sectionStyle!);
            var groupedFields = group.ToArray();
            for (var i = 0; i < groupedFields.Length; i += 2)
            {
                GUILayout.BeginHorizontal();
                DrawGroupedField(groupedFields[i]);
                if (i + 1 < groupedFields.Length)
                {
                    GUILayout.Space(CardSpacing);
                    DrawGroupedField(groupedFields[i + 1]);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
            return false;
        });
    }

    /// <summary>Chooses the appropriate editor control for one grouped XML value.</summary>
    private void DrawGroupedField(WorldProject.Field field, bool? includeContext = null)
    {
        GUILayout.BeginVertical(GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
        var fieldLabel = new GUIContent(ReadableFieldLabel(field.Label, includeContext ?? (_page == 6 || _page == 7)) + "  ⓘ", field.Tooltip);
        GUILayout.Label(fieldLabel, _mutedStyle!);
        RegisterHoverTooltip(GUILayoutUtility.GetLastRect(), field.Tooltip);
        if (field.IsBoolean)
            field.Value = GUILayout.Toggle(bool.TryParse(field.Value, out var current) && current, "Enabled") ? "true" : "false";
        else
        {
            var value = GUILayout.TextField(field.Value, _inputStyle!, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
            field.Value = field.IsNumeric ? MaskNumericInput(value) : value;
        }

        GUILayout.Space(10);
        GUILayout.EndVertical();
    }

    /// <summary>Derives a readable card heading from an XML field path.</summary>
    private static string FieldGroupName(WorldProject.Field field)
    {
        var label = field.Label;
        var separator = Math.Max(label.LastIndexOf('/'), label.LastIndexOf(" / ", StringComparison.Ordinal));
        if (separator < 0)
            return "General settings";
        var parent = label.Substring(0, separator).TrimEnd(' ', '/');
        var parentSeparator = parent.LastIndexOf('/');
        if (parentSeparator >= 0)
            parent = parent.Substring(parentSeparator + 1);
        return string.IsNullOrWhiteSpace(parent) ? "General settings" : parent.Trim();
    }

    /// <summary>Shortens a field path for compact editor labels.</summary>
    private static string ShortLabel(string label)
    {
        var slash = label.LastIndexOf('/');
        if (slash > 0 && slash + 1 < label.Length && label[slash - 1] == 'm' && label[slash + 1] == 's')
            slash = -1;
        return slash >= 0 ? HumanizeXmlName(label.Substring(slash + 1).Replace("@", "")) + "   ·   " + HumanizeXmlPath(label.Substring(0, slash)) : HumanizeXmlName(label);
    }

    /// <summary>Replaces technical XML leaf names with the player-facing label for their context.</summary>
    private static string ReadableFieldLabel(string label, bool includeContext)
    {
        var divider = label.LastIndexOf(" / ", StringComparison.Ordinal);
        var pathSlash = label.LastIndexOf('/');
        if (pathSlash > 0 && pathSlash + 1 < label.Length && label[pathSlash - 1] == 'm' && label[pathSlash + 1] == 's')
            pathSlash = label.LastIndexOf('/', pathSlash - 1);
        var leaf = divider >= 0 ? label.Substring(divider + 3) : pathSlash >= 0 ? label.Substring(pathSlash + 1) : label;
        var prefix = divider >= 0 ? label.Substring(0, divider) : pathSlash >= 0 ? label.Substring(0, pathSlash) : string.Empty;
        var friendly = HumanizeXmlName(leaf.TrimStart('@'));
        switch (leaf.TrimStart('@'))
        {
            case "SemiMajorAxisAu":
                friendly = "Average orbital distance (AU)";
                break;
            case "SemiMajorAxisKm":
                friendly = "Average orbital distance (km)";
                break;
            case "Eccentricity":
                friendly = "Orbit shape (eccentricity)";
                break;
            case "Period":
                friendly = "Orbital period (game days)";
                break;
            case "Inclination":
                friendly = "Orbit inclination (°)";
                break;
            case "ArgumentOfPeriapsis":
                friendly = "Closest approach angle (°)";
                break;
            case "LongitudeOfAscendingNode":
                friendly = "Orbit plane angle (°)";
                break;
            case "LongitudeAtEpoch":
                friendly = "Position at orbital epoch (°)";
                break;
            case "CoolDown":
                friendly = "Weather cooldown";
                break;
            case "StartDelay":
                friendly = "Delay before weather starts";
                break;
            case "Duration":
                friendly = "Weather duration";
                break;
            case "OreDensity":
                friendly = "Ore abundance multiplier";
                break;
            case "WindStrength":
                friendly = "Wind strength";
                break;
        }

        if (prefix.Length == 0 || !includeContext)
            return friendly;
        var context = HumanizeXmlPath(prefix);
        return friendly + "   ·   " + context;
    }

    /// <summary>Converts a hierarchical XML path into readable section names.</summary>
    private static string HumanizeXmlPath(string path)
    {
        var rawParts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var parts = new List<string>();
        foreach (var rawPart in rawParts)
        {
            var part = HumanizeXmlName(rawPart);
            if (parts.Count == 0 || !string.Equals(parts[parts.Count - 1], part, StringComparison.OrdinalIgnoreCase))
                parts.Add(part);
        }

        if (parts.Count > 3)
            parts = parts.Skip(parts.Count - 3).ToList();
        return string.Join(" / ", parts);
    }

    /// <summary>Adds word boundaries to technical XML identifiers for display.</summary>
    private static string HumanizeXmlName(string name)
    {
        var value = Regex.Replace(name.Replace("_", " "), "([a-z0-9])([A-Z])", "$1 $2");
        value = Regex.Replace(value, "([A-Z])([A-Z][a-z])", "$1 $2");
        return value.Replace("Au", "AU").Replace("Km", "km").Replace("Mmol", "mmol").Replace("Poi", "POI");
    }
}
