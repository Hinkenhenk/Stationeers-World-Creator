using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private int _temperatureUnit;
    private string _temperatureAxisMaximum = "";
    private string _temperatureScaleTopDraft = "";
    private bool _temperatureScaleTopEditing;
    private readonly Dictionary<string, string> _temperatureValueDrafts = new Dictionary<string, string>(StringComparer.Ordinal);
    private string? _temperatureDragPath;
    private float _temperatureDragMin;
    private float _temperatureDragMax;
    /// <summary>Builds the editable base-temperature graph and its unit, axis and scale cards.</summary>
    private void DrawTemperatureChart()
    {
        var keys = _project!.AtmosphereFields.Where(f => f.Label.Contains("/Temperature/Key") && (f.Label.EndsWith(" / Time") || f.Label.EndsWith(" / Value")))
            .ToList();
        var points = new List<(WorldProject.Field Time, WorldProject.Field Value, Vector2 Position)>();
        foreach (var group in keys.GroupBy(f => f.Label.Substring(0, f.Label.LastIndexOf(" / ", System.StringComparison.Ordinal))))
        {
            var time = group.FirstOrDefault(f => f.Label.EndsWith(" / Time"));
            var value = group.FirstOrDefault(f => f.Label.EndsWith(" / Value"));
            if (time != null && value != null && double.TryParse(time.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) && double.TryParse(value.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y) && !double.IsNaN(x) && !double.IsInfinity(x) && !double.IsNaN(y) && !double.IsInfinity(y) && x >= 0 && x <= 180)
                points.Add((time, value, new Vector2((float)x, (float)ConvertTemperatureFromKelvin(y, _temperatureUnit))));
        }

        if (points.Count == 0)
            return;
        points.Sort((a, b) => a.Position.x.CompareTo(b.Position.x));
        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            var kelvin = double.TryParse(point.Value.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 273.15;
            points[i] = (point.Time, point.Value, new Vector2(point.Position.x, (float)ConvertTemperatureFromKelvin(kelvin, _temperatureUnit)));
        }

        var dataMaxKelvin = points.Max(p => ConvertTemperatureToKelvin(p.Position.y, _temperatureUnit));
        if (!_temperatureScaleTopEditing && double.TryParse(_temperatureAxisMaximum, NumberStyles.Float, CultureInfo.InvariantCulture, out var savedTop) && savedTop < dataMaxKelvin)
            _temperatureAxisMaximum = dataMaxKelvin.ToString("0.##", CultureInfo.InvariantCulture);
        var min = (float)ConvertTemperatureFromKelvin(0, _temperatureUnit);
        var autoMaxKelvin = (Math.Floor(dataMaxKelvin / 20) + 1) * 20;
        var topKelvin = autoMaxKelvin;
        var topEntry = _temperatureScaleTopEditing ? _temperatureScaleTopDraft : _temperatureAxisMaximum;
        if (double.TryParse(topEntry, NumberStyles.Float, CultureInfo.InvariantCulture, out var requestedTop) && requestedTop > 0 && !double.IsNaN(requestedTop) && !double.IsInfinity(requestedTop))
            topKelvin = Math.Max(requestedTop, dataMaxKelvin);
        var max = (float)ConvertTemperatureFromKelvin(topKelvin, _temperatureUnit);
        if (_temperatureDragPath != null)
        {
            min = _temperatureDragMin;
            max = _temperatureDragMax;
        }

        var displayPoints = points;
        var horizontalRange = 180f;
        var controlsOnRight = Screen.width >= 1400;
        if (controlsOnRight)
            GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        HelpLabel(
            new GUIContent(
            "BASE TEMPERATURE CURVE  ⓘ",
            "Absolute atmosphere temperature in kelvin, sampled against Stationeers' solar-angle input from 0° to 180°. Each solar angle has one curve point."
        ),
            _sectionStyle!
        );
        GUILayout.Label("Temperature (" + TemperatureUnitName(_temperatureUnit) + ")", _mutedStyle!);
        var chart = GUILayoutUtility.GetRect(100, 520, GUILayout.ExpandWidth(true));
        GUI.Box(chart, "");
        var plot = new Rect(chart.x + 72, chart.y + 20, chart.width - 94, chart.height - 88);
        DrawTemperatureAxes(chart, plot, min, max);
        HandleTemperatureChartInput(chart, plot, points, displayPoints, horizontalRange, min, max);
        DrawTemperatureCurvePoints(plot, displayPoints, horizontalRange, min, max);
        GUILayout.Label(
            "Click an empty point to add a key or drag an existing point. The horizontal axis uses the game's 0°–180° solar-angle input. Temperature values are saved as kelvin.",
            _mutedStyle!
        );
        GUILayout.EndVertical();
        if (controlsOnRight)
            GUILayout.Space(CardSpacing);
        if (controlsOnRight)
            GUILayout.BeginVertical(GUILayout.Width(300));
        DrawTemperatureUnitCard();
        GUILayout.Space(CardSpacing);
        DrawTemperatureAxisCard();
        GUILayout.Space(CardSpacing);
        DrawTemperatureScaleCard(dataMaxKelvin);
        if (controlsOnRight)
        {
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
    }

    /// <summary>Changes displayed temperature units while preserving stored kelvin values.</summary>
    private void DrawTemperatureUnitCard()
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("TEMPERATURE UNITS", _sectionStyle!);
        GUILayout.Label("Display temperature values as", _mutedStyle!);
        var selected = NativeSelectionGrid(_temperatureUnit, new[] { "Kelvin (K)", "Celsius (°C)", "Fahrenheit (°F)" }, 1, _buttonStyle!);
        if (selected != _temperatureUnit)
        {
            _temperatureUnit = selected;
            _temperatureValueDrafts.Clear();
        }

        GUILayout.Label("World values remain stored in kelvin.", _mutedStyle!);
        GUILayout.EndVertical();
    }

    /// <summary>Explains the solar-angle input represented by the graph horizontal axis.</summary>
    private void DrawTemperatureAxisCard()
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("HORIZONTAL AXIS", _sectionStyle!);
        GUILayout.Label("Solar angle (°)", _mutedStyle!);
        GUILayout.Label("Stationeers input: 0° overhead to 180° night. Each angle appears once.", _mutedStyle!);
        GUILayout.EndVertical();
    }

    /// <summary>Edits and commits a graph ceiling that cannot fall below the highest curve value.</summary>
    private void DrawTemperatureScaleCard(double maximumCurveKelvin)
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("VERTICAL SCALE", _sectionStyle!);
        GUILayout.Label("Scale top (K)", _mutedStyle!);
        var rect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true), GUILayout.Height(28));
        GUI.SetNextControlName("WorldCreatorTemperatureScaleTop");
        var shown = _temperatureScaleTopEditing ? _temperatureScaleTopDraft : _temperatureAxisMaximum;
        var edited = GUI.TextField(rect, shown, _inputStyle!);
        if (edited != shown)
        {
            _temperatureScaleTopDraft = edited;
            _temperatureScaleTopEditing = true;
        }

        var evt = Event.current;
        var enterPressed = evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter);
        var commit = _temperatureScaleTopEditing && (enterPressed || (evt.type == EventType.MouseDown && !rect.Contains(evt.mousePosition)) || (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Tab));
        if (commit)
        {
            if (string.IsNullOrWhiteSpace(_temperatureScaleTopDraft))
                _temperatureAxisMaximum = "";
            else if (double.TryParse(_temperatureScaleTopDraft, NumberStyles.Float, CultureInfo.InvariantCulture, out var entered) && entered > 0 && !double.IsNaN(entered) && !double.IsInfinity(entered))
                _temperatureAxisMaximum = Math.Max(entered, maximumCurveKelvin).ToString("0.##", CultureInfo.InvariantCulture);
            else
                _status = "Enter a valid Kelvin scale top above zero.";
            _temperatureScaleTopDraft = "";
            _temperatureScaleTopEditing = false;
            if (enterPressed)
            {
                GUI.FocusControl(null);
                evt.Use();
            }
        }

        GUILayout.Label(
            "The top is in kelvin. It cannot go below the highest curve point. Leave blank to scale automatically. Ticks are 20 K apart.",
            _mutedStyle!
        );
        GUILayout.Label("Highest curve value: " + maximumCurveKelvin.ToString("0.##", CultureInfo.InvariantCulture) + " K", _mutedStyle!);
        GUILayout.EndVertical();
    }

    /// <summary>Draws temperature grid lines and solar-angle tick labels using the current graph limits.</summary>
    private void DrawTemperatureAxes(Rect chart, Rect plot, float min, float max)
    {
        var axisLabelStyle = new GUIStyle(_mutedStyle!)
        {
            alignment = TextAnchor.MiddleRight
        };
        var tickTopKelvin = ConvertTemperatureToKelvin(max, _temperatureUnit);
        var labelIntervalKelvin = tickTopKelvin > 240 ? 100d : 20d;
        for (var tickKelvin = 0d; tickKelvin <= tickTopKelvin + 0.001; tickKelvin += 20)
        {
            var tickValue = (float)ConvertTemperatureFromKelvin(tickKelvin, _temperatureUnit);
            var y = plot.y + (max - tickValue) / (max - min) * plot.height;
            if (y < plot.y - 0.5f || y > plot.yMax + 0.5f)
                continue;
            var majorTick = tickKelvin % labelIntervalKelvin < 0.001;
            GUI.color = new Color(1f, 1f, 1f, majorTick ? 0.22f : 0.07f);
            GUI.DrawTexture(new Rect(plot.x, y, plot.width, 1), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (majorTick || tickKelvin == 0)
                GUI.Label(new Rect(chart.x + 3, y - 9, 62, 18), tickValue.ToString("0.#", CultureInfo.InvariantCulture), axisLabelStyle);
        }

        GUI.color = new Color(1f, 1f, 1f, 0.7f);
        GUI.DrawTexture(new Rect(plot.x, plot.y, 1, plot.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(plot.x, plot.yMax, plot.width, 1), Texture2D.whiteTexture);
        GUI.color = Color.white;
        var xAxis = new[]
        {
            (0f, "0°"),
            (0.5f, "90°"),
            (1f, "180°")
        };
        foreach (var tick in xAxis)
            GUI.Label(new Rect(plot.x + tick.Item1 * plot.width - 30, plot.yMax + 2, 60, 18), tick.Item2, _mutedStyle!);
        GUI.Label(new Rect(plot.x, plot.yMax + 20, plot.width, 20), "Solar angle · sun overhead (0°) through horizon (90°) to night (180°)", _mutedStyle!);
    }

    /// <summary>Handles point selection, dragging, release and insertion in their required event order.</summary>
    private void HandleTemperatureChartInput(
        Rect chart,
        Rect plot,
        List<(WorldProject.Field Time, WorldProject.Field Value, Vector2 Position)> points,
        List<(WorldProject.Field Time, WorldProject.Field Value, Vector2 Position)> displayPoints,
        float horizontalRange,
        float min,
        float max
    )
    {
        var control = GUIUtility.GetControlID(FocusType.Passive, chart);
        var evt = Event.current;
        if (evt.type == EventType.MouseDown && evt.button == 0 && chart.Contains(evt.mousePosition))
        {
            var nearest = displayPoints.OrderBy(p => Vector2.Distance(
                p.Position,
                new Vector2((evt.mousePosition.x - plot.x) / plot.width * horizontalRange, max - (evt.mousePosition.y - plot.y) / plot.height * (max - min))
            )).First();
            var pointX = plot.x + nearest.Position.x / horizontalRange * plot.width;
            var pointY = plot.y + (max - nearest.Position.y) / (max - min) * plot.height;
            if (Vector2.Distance(evt.mousePosition, new Vector2(pointX, pointY)) <= 12)
            {
                _temperatureDragPath = nearest.Time.Label.Substring(0, nearest.Time.Label.LastIndexOf(" / ", System.StringComparison.Ordinal));
                _temperatureDragMin = min;
                _temperatureDragMax = max;
                GUIUtility.hotControl = control;
                evt.Use();
            }
        }

        if (evt.type == EventType.MouseDrag && GUIUtility.hotControl == control && _temperatureDragPath != null)
        {
            var point = points.FirstOrDefault(p => string.Equals(
                p.Time.Label.Substring(0, p.Time.Label.LastIndexOf(" / ", System.StringComparison.Ordinal)),
                _temperatureDragPath,
                System.StringComparison.Ordinal
            ));
            if (point.Time != null && point.Value != null)
            {
                var horizontalValue = Mathf.Clamp((evt.mousePosition.x - plot.x) / plot.width * horizontalRange, 0, horizontalRange);
                var x = horizontalValue;
                var y = _temperatureDragMax - Mathf.Clamp((evt.mousePosition.y - plot.y) / plot.height, 0, 1) * (_temperatureDragMax - _temperatureDragMin);
                point.Time.Value = x.ToString("0.##", CultureInfo.InvariantCulture);
                point.Value.Value = ConvertTemperatureToKelvin(y, _temperatureUnit).ToString("0.########", CultureInfo.InvariantCulture);
                _temperatureValueDrafts.Remove(point.Value.Label);
            }

            evt.Use();
        }

        if (evt.type == EventType.MouseUp && GUIUtility.hotControl == control)
        {
            GUIUtility.hotControl = 0;
            _temperatureDragPath = null;
            evt.Use();
        }

        if (evt.type == EventType.MouseDown && evt.button == 0 && plot.Contains(evt.mousePosition) && GUIUtility.hotControl != control)
        {
            var clickedExisting = displayPoints.Any(p => Vector2.Distance(
                evt.mousePosition,
                new Vector2(plot.x + p.Position.x / horizontalRange * plot.width, plot.y + (max - p.Position.y) / (max - min) * plot.height)
            ) <= 12);
            if (!clickedExisting)
            {
                var horizontalValue = Mathf.Clamp((evt.mousePosition.x - plot.x) / plot.width * horizontalRange, 0, horizontalRange);
                var x = horizontalValue;
                var y = max - Mathf.Clamp((evt.mousePosition.y - plot.y) / plot.height, 0, 1) * (max - min);
                try
                {
                    _project!.AddTemperatureKey(_project!.AtmosphereFields, "Atmosphere/GlobalAtmosphere/Temperature", x, ConvertTemperatureToKelvin(y, _temperatureUnit));
                    _status = "Added a base temperature point.";
                }
                catch (Exception e)
                {
                    _status = e.Message;
                }

                evt.Use();
            }
        }
    }

    /// <summary>Draws the connecting curve and handles for the currently displayed temperature points.</summary>
    private void DrawTemperatureCurvePoints(
        Rect plot,
        List<(WorldProject.Field Time, WorldProject.Field Value, Vector2 Position)> displayPoints,
        float horizontalRange,
        float min,
        float max
    )
    {
        var oldColor = GUI.color;
        GUI.color = new Color(TemperatureOrange.r, TemperatureOrange.g, TemperatureOrange.b, 0.8f);
        for (var i = 1; i < displayPoints.Count; i++)
            DrawChartLine(plot, displayPoints[i - 1].Position, displayPoints[i].Position, horizontalRange, min, max);
        GUI.color = oldColor;
        foreach (var point in displayPoints)
        {
            var x = plot.x + point.Position.x / horizontalRange * plot.width;
            var y = plot.y + (max - point.Position.y) / (max - min) * plot.height;
            GUI.DrawTexture(new Rect(x - 4, y - 4, 8, 8), Texture2D.whiteTexture);
        }
    }
}
