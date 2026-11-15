using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    /// <summary>Pairs each curve point's XML input and output fields for editing.</summary>
    private static List<(string Path, int Index, WorldProject.Field Time, WorldProject.Field Value)> CurveKeys(IEnumerable<WorldProject.Field> fields)
    {
        var result = new List<(string, int, WorldProject.Field, WorldProject.Field)>();
        foreach (var group in fields.Where(f => f.Label.Contains("/Key #") && (f.Label.EndsWith(" / Time", StringComparison.Ordinal) || f.Label.EndsWith(" / Value", StringComparison.Ordinal)))
            .GroupBy(f => f.Label.Substring(0, f.Label.LastIndexOf(" / ", StringComparison.Ordinal))))
        {
            var marker = group.Key.LastIndexOf("/Key #", StringComparison.Ordinal);
            if (marker < 0 || !int.TryParse(group.Key.Substring(marker + 6), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
                continue;
            var time = group.FirstOrDefault(f => f.Label.EndsWith(" / Time", StringComparison.Ordinal));
            var value = group.FirstOrDefault(f => f.Label.EndsWith(" / Value", StringComparison.Ordinal));
            if (time != null && value != null)
                result.Add((group.Key, index, time, value));
        }

        return result;
    }

    /// <summary>Returns the curve-point paths that generic field cards must exclude.</summary>
    private static List<string> GetCurveKeyParents(IEnumerable<WorldCreator.WorldProject.Field> fields) => CurveKeys(fields).Select(key => key.Path).ToList();
    /// <summary>Draws one editable card per curve and stops after an edit changes its structure.</summary>
    private void DrawCurveKeyEditor(List<WorldProject.Field> fields, string section)
    {
        var keys = CurveKeys(fields);
        foreach (var curve in keys.GroupBy(key => key.Path.Substring(0, key.Path.LastIndexOf("/Key #", StringComparison.Ordinal))))
        {
            if (DrawCurveCard(curve, fields, section))
                return;
        }
    }

    /// <summary>Edits a numeric curve coordinate and enforces any supplied bounds.</summary>
    private void DrawCurveNumber(WorldCreator.WorldProject.Field field, string label, float width, double? minimum = null, double? maximum = null)
    {
        GUILayout.BeginVertical(GUILayout.Width(width));
        HelpLabel(new GUIContent(label + "  ⓘ", field.Tooltip), _mutedStyle!);
        var rect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true), GUILayout.Height(28));
        RegisterHoverTooltip(rect, field.Tooltip);
        var value = MaskNumericInput(GUI.TextField(rect, field.Value, _inputStyle!));
        if (value == field.Value)
        {
            GUILayout.EndVertical();
            return;
        }
        if (minimum.HasValue && maximum.HasValue && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric) && !double.IsNaN(numeric) && !double.IsInfinity(numeric))
            field.Value = Math.Max(minimum.Value, Math.Min(maximum.Value, numeric)).ToString("0.##", CultureInfo.InvariantCulture);
        else
            field.Value = value;
        GUILayout.EndVertical();
    }

    /// <summary>Keeps an editable temperature draft synchronized with its stored kelvin value.</summary>
    private void DrawTemperatureValue(WorldProject.Field field, string label, float width)
    {
        GUILayout.BeginVertical(GUILayout.Width(width));
        HelpLabel(new GUIContent(label + "  ⓘ", field.Tooltip + " Stored in world XML as kelvin."), _mutedStyle!);
        var rect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true), GUILayout.Height(28));
        RegisterHoverTooltip(rect, field.Tooltip + " Stored in world XML as kelvin.");
        var kelvin = double.TryParse(field.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var stored) ? stored : 273.15;
        var value = ConvertTemperatureFromKelvin(kelvin, _temperatureUnit).ToString("0.##", CultureInfo.InvariantCulture);
        if (_temperatureValueDrafts.TryGetValue(field.Label, out var draft))
            value = draft;
        var edited = GUI.TextField(rect, value, _inputStyle!);
        if (edited != value)
        {
            var masked = MaskNumericInput(edited);
            if (double.TryParse(masked, NumberStyles.Float, CultureInfo.InvariantCulture, out var display) && !double.IsNaN(display) && !double.IsInfinity(display))
            {
                field.Value = Math.Max(0, ConvertTemperatureToKelvin(display, _temperatureUnit)).ToString("0.########", CultureInfo.InvariantCulture);
                _temperatureValueDrafts.Remove(field.Label);
            }
            else
            {
                _temperatureValueDrafts[field.Label] = masked;
                field.Value = masked;
            }
        }

        GUILayout.EndVertical();
    }

    /// <summary>Converts a stored absolute kelvin temperature into the selected display unit.</summary>
    private static double ConvertTemperatureFromKelvin(double kelvin, int unit) => unit == 1 ? kelvin - 273.15 : unit == 2 ? (kelvin - 273.15) * 9 / 5 + 32 : kelvin;
    /// <summary>Converts an edited display temperature back to the game's kelvin representation.</summary>
    private static double ConvertTemperatureToKelvin(double value, int unit) => unit == 1 ? value + 273.15 : unit == 2 ? (value - 32) * 5 / 9 + 273.15 : value;
    /// <summary>Returns the unit label used by temperature controls and axes.</summary>
    private static string TemperatureUnitName(int unit) => unit == 1 ? "°C" : unit == 2 ? "°F" : "K";
    /// <summary>Labels a temperature-offset curve as day or night from its XML path.</summary>
    private static string CurveSide(string path)
    {
        if (path.EndsWith("/Day", StringComparison.OrdinalIgnoreCase))
            return "Day";
        if (path.EndsWith("/Night", StringComparison.OrdinalIgnoreCase))
            return "Night";
        return "curve";
    }

    /// <summary>Derives a readable title for a curve without a specialized editor label.</summary>
    private static string CurveName(string path)
    {
        var start = path.LastIndexOf('/') + 1;
        return path.Substring(start).Replace("Offset", " ").Replace("Curve", " curve");
    }

    /// <summary>Edits one curve's points and reports when adding or removing a point changes the layout.</summary>
    private bool DrawCurveCard(
        IGrouping<string, (string Path, int Index, WorldProject.Field Time, WorldProject.Field Value)> curve,
        List<WorldProject.Field> fields,
        string section
    )
    {
        var curvePath = curve.Key;
        var isBaseTemperature = curvePath.EndsWith("/Temperature", StringComparison.Ordinal);
        var isSolar = curvePath.IndexOf("SolarRadiationTemperature", StringComparison.OrdinalIgnoreCase) >= 0;
        var isGreenhouse = curvePath.IndexOf("GHGTemperatureOffset", StringComparison.OrdinalIgnoreCase) >= 0;
        var isDensity = curvePath.IndexOf("DensityOffset", StringComparison.OrdinalIgnoreCase) >= 0;
        var timeLabel = isBaseTemperature ? "Solar angle (°)" : isSolar ? "Relative sunlight level (0–100)" : isGreenhouse ? "Greenhouse index" : isDensity ? "Gas density (mmol/L)" : "Curve input";
        var valueLabel = isBaseTemperature ? "Temperature (K)" : isSolar || isGreenhouse || isDensity ? "Temperature offset (K)" : "Curve value";
        var curveTitle = isBaseTemperature ? "Baseline temperature by sun angle" : isSolar ? "Solar-radiation offset · " + CurveSide(curvePath) : isGreenhouse ? "Greenhouse offset · " + CurveSide(curvePath) : isDensity ? "Density offset · " + CurveSide(curvePath) : CurveName(curvePath);
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.BeginHorizontal();
        HelpLabel(new GUIContent(curveTitle + "  ⓘ", curve.First().Time.Tooltip + " " + curve.First().Value.Tooltip), _sectionStyle!);
        GUILayout.FlexibleSpace();
        if (NativeButton("+ Add key", _buttonStyle!, GUILayout.Width(105)))
        {
            try
            {
                _project!.AddCurveKey(fields, section, curvePath);
                _status = "Added a key to " + curveTitle + ".";
            }
            catch (Exception e)
            {
                _status = e.Message;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            return true;
        }

        GUILayout.EndHorizontal();
        var curveDescription = isBaseTemperature ? "Sets the baseline surface temperature at each sun angle: 0° is overhead, 90° is the horizon, and 180° is night. The game blends between points; other temperature offsets are separate." : isSolar ? "Adds a day or night temperature offset based on the orbit's calculated sunlight level. The input runs across that orbit's irradiance range; it is not a clock time." : isGreenhouse ? "Adds a day or night temperature offset based on the atmosphere's greenhouse index. The game blends between the points." : isDensity ? "Adds a day or night temperature offset based on atmospheric gas density. The game blends between the points." : "Edit the curve input and output using the units shown. The game blends between points.";
        GUILayout.Label(curveDescription, _mutedStyle!);
        var keyCount = curve.Count();
        foreach (var key in curve.OrderBy(k => k.Index))
        {
            GUILayout.BeginHorizontal(_rowStyle!);
            if (isBaseTemperature)
                DrawCurveNumber(key.Time, timeLabel, 170, 0, 180);
            else
                DrawCurveNumber(key.Time, timeLabel, 170, isSolar ? 0 : (double?)null, isSolar ? 100 : (double?)null);
            GUILayout.Space(16);
            if (isBaseTemperature)
                DrawTemperatureValue(key.Value, valueLabel, 170);
            else
                DrawCurveNumber(key.Value, valueLabel, 170);
            GUILayout.Space(16);
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            var remove = keyCount > 1 && NativeButton("Remove", _buttonStyle!, GUILayout.Width(105), GUILayout.Height(32));
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (remove)
            {
                try
                {
                    _project!.RemoveCurveKey(fields, section, curvePath, key.Index);
                    _status = "Removed key " + key.Index.ToString(CultureInfo.InvariantCulture) + " from " + curveTitle + ".";
                }
                catch (Exception e)
                {
                    _status = e.Message;
                }

                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                return true;
            }

            GUILayout.EndHorizontal();
            GUILayout.Space(10);
        }

        GUILayout.EndVertical();
        GUILayout.Space(CardSpacing);
        return false;
    }
}
