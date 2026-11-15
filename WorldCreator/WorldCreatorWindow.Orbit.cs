using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private int _orbitView;
    private float _orbitPhase = 45f;
    private float _orbitZoom = 1f;
    private Quaternion _orbitGlobeRotation = DefaultOrbitRotation();
    private float _orbitSpinPhase;
    private float _orbitSimulationSpeed = 1f;
    private bool _orbitSimulationPlaying;
    private bool _orbitDragging;
    private Vector2 _orbitDragPosition;
    /// <summary>Reads the orbit period from the draft fields with a one-day fallback for invalid drafts.</summary>
    private double OrbitPeriodDays()
    {
        var field = _project?.OrbitFields.FirstOrDefault(f => f.Label.EndsWith(" / Period"));
        return field != null && double.TryParse(field.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : 1d;
    }

    /// <summary>Reads the signed body rotation period, retaining retrograde direction.</summary>
    private double RotationPeriodDays()
    {
        var field = _project?.OrbitFields.FirstOrDefault(f => f.Label.IndexOf("RotationPeriod", StringComparison.OrdinalIgnoreCase) >= 0);
        return field != null && double.TryParse(field.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0d;
    }

    /// <summary>Arranges the orbital preview beside its editable orbital and landing settings.</summary>
    private void DrawOrbitPreview()
    {
        var distance = _project!.OrbitFields.FirstOrDefault(f => f.Label.EndsWith(" / SemiMajorAxisAu") || f.Label.EndsWith(" / SemiMajorAxisKm"));
        var eccentricity = _project!.OrbitFields.FirstOrDefault(f => f.Label.EndsWith(" / Eccentricity"));
        if (distance == null || eccentricity == null || !double.TryParse(distance.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var a) || !double.TryParse(eccentricity.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var e) || double.IsNaN(a) || double.IsInfinity(a) || double.IsNaN(e) || double.IsInfinity(e) || a <= 0 || e < 0 || e >= 1)
            return;
        var sideBySide = UnityEngine.Screen.width >= 1400;
        if (sideBySide)
            GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        var unit = distance.Label.EndsWith("Au") ? "AU" : "km";
        DrawOrbitDistanceSummary(distance, a, e);
        DrawOrbitCameraControls();
        // Keep the orbit map large while placing editable values alongside it on wide displays.
        var chartHeight = Mathf.Clamp(UnityEngine.Screen.height * 0.38f, 240f, 560f);
        var chart = GUILayoutUtility.GetRect(100, chartHeight, GUILayout.ExpandWidth(true));
        GUI.Box(chart, "");
        GUI.BeginGroup(chart);
        var localChart = new Rect(0, 0, chart.width, chart.height);
        if (_orbitView == 3)
        {
            var landingPointHidden = DrawLandingSiteSurface(localChart, WorldAngle("Latitude"), WorldAngle("Longitude"));
            GUI.EndGroup();
            if (landingPointHidden)
                GUILayout.Label("The selected landing point is on the far side; drag the globe to bring it into view.", _mutedStyle!);
            GUILayout.Label(
                "The red marker is the configured landing site. This globe is a coordinate preview; terrain shape and the game's body-axis conventions are not simulated.",
                _mutedStyle!
            );
        }
        else
        {
            var radius = DrawOrbitalSystemChart(chart, a, e);
            GUI.EndGroup();
            GUILayout.Label("Primary body  ·  playable body  ·  Current distance " + radius.ToString("0.###", CultureInfo.InvariantCulture) + " " + unit, _mutedStyle!);
            GUILayout.Label(
                "Illustrative projection from the configured orbital elements; phase and camera controls do not change world XML. Confirm runtime timing and coordinate conventions in game.",
                _mutedStyle!
            );
        }

        HandleOrbitPreviewDrag(chart);
        GUILayout.EndVertical();
        GUILayout.Space(CardSpacing);
        if (sideBySide)
            GUILayout.BeginVertical(_cardStyle!, GUILayout.Width(Mathf.Clamp(UnityEngine.Screen.width * 0.27f, 320f, 520f)), GUILayout.ExpandHeight(true));
        else
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        DrawOrbitSettings();
        GUILayout.EndVertical();
        if (sideBySide)
            GUILayout.EndHorizontal();
    }

    /// <summary>Places a rendered primary or playable sphere texture on the orbital chart.</summary>
    private void DrawOrbitSphere(Vector2 center, float size, bool sun)
    {
        EnsureOrbitSpherePreviews();
        var texture = sun ? _orbitSunRenderTexture : _orbitPlanetRenderTexture;
        if (texture != null)
            GUI.DrawTexture(new Rect(center.x - size / 2f, center.y - size / 2f, size, size), texture, ScaleMode.ScaleToFit, true);
    }

    /// <summary>Groups orbital elements, landing coordinates and body-rotation controls.</summary>
    private void DrawOrbitSettings()
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("ORBITAL ELEMENTS", _sectionStyle!);
        foreach (var field in _project!.OrbitFields.Where(field => !field.Label.StartsWith("Celestial body/", StringComparison.Ordinal)))
            DrawOrbitField(field);
        GUILayout.EndVertical();
        GUILayout.Space(CardSpacing);
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("CELESTIAL BODY", _sectionStyle!);
        GUILayout.Label("Reference position, appearance, and sunrise timing for the playable body.", _mutedStyle!);
        foreach (var field in _project!.OrbitFields.Where(field => field.Label.StartsWith("Celestial body/", StringComparison.Ordinal)))
            DrawOrbitField(field);
        GUILayout.EndVertical();
        GUILayout.Space(8);
        GUILayout.Label("LANDING SITE", _sectionStyle!);
        GUILayout.Label("Coordinates on the playable body's surface.", _mutedStyle!);
        DrawLandingCoordinate("Latitude", "Latitude", "−90° to 90°");
        DrawLandingCoordinate("Longitude", "Longitude", "−180° to 180°");
    }

    /// <summary>Draws an orbital value with its player-facing label and numeric input rules.</summary>
    private void DrawOrbitField(WorldProject.Field field)
    {
        GUILayout.BeginVertical(_rowStyle!);
        var content = new GUIContent(ReadableFieldLabel(field.Label, false) + "  ⓘ", field.Tooltip);
        GUILayout.Label(content, _mutedStyle!);
        RegisterHoverTooltip(GUILayoutUtility.GetLastRect(), field.Tooltip);
        if (field.IsBoolean)
            field.Value = GUILayout.Toggle(bool.TryParse(field.Value, out var current) && current, new GUIContent("Enabled", field.Tooltip)) ? "true" : "false";
        else
        {
            var value = GUILayout.TextField(field.Value, _inputStyle!, GUILayout.ExpandWidth(true));
            field.Value = field.IsNumeric ? MaskNumericInput(value) : value;
        }

        GUILayout.EndVertical();
    }

    /// <summary>Draws a landing latitude or longitude field with its allowed range.</summary>
    private void DrawLandingCoordinate(string suffix, string label, string range)
    {
        var field = _project!.WorldFields.FirstOrDefault(f => f.Label.EndsWith(" / " + suffix, StringComparison.Ordinal));
        if (field == null)
            return;
        GUILayout.BeginVertical(_rowStyle!);
        GUILayout.Label(label + " (°)   " + range, _mutedStyle!);
        var value = GUILayout.TextField(field.Value, _inputStyle!, GUILayout.ExpandWidth(true));
        field.Value = MaskNumericInput(value);
        GUILayout.EndVertical();
    }

    /// <summary>Draws the shared XYZ reference axes in the current orbital view.</summary>
    private void DrawOrbitAxisTriad(Vector2 center, float scale)
    {
        var length = Mathf.Min(0.7f * scale, 84f);
        /// <summary>Projects a reference-axis direction through the preview rotation into chart pixels.</summary>
        Vector2 ProjectAxis(double x, double y, double z)
        {
            var view = _orbitGlobeRotation * new Vector3((float)x, (float)y, (float)z);
            return new Vector2(view.x * length, -view.y * length);
        }

        var origin = center;
        /// <summary>Draws one labelled reference axis with its coloured line and endpoint marker.</summary>
        void Axis(double x, double y, double z, string label, Color color)
        {
            var delta = ProjectAxis(x, y, z);
            var end = origin + delta;
            DrawOrbitLine(origin, end, color, 2f);
            DrawOrbitMarker(end, 5f, color);
            GUI.Label(new Rect(end.x + 3, end.y - 9, 20, 18), label, _mutedStyle!);
        }

        Axis(1, 0, 0, "X", new Color(.95f, .38f, .38f));
        Axis(0, 1, 0, "Y", new Color(.35f, .58f, 1f));
        Axis(0, 0, 1, "Z", new Color(.38f, .83f, .51f));
    }

    /// <summary>Reads a draft landing coordinate without changing the stored XML.</summary>
    private double WorldAngle(string suffix)
    {
        var field = _project!.WorldFields.FirstOrDefault(f => f.Label.EndsWith(" / " + suffix, StringComparison.Ordinal));
        return field != null && double.TryParse(field.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && !double.IsNaN(value) && !double.IsInfinity(value) ? value : 0d;
    }

    /// <summary>Draws the textured landing globe, coordinate grid and landing marker, reporting a hidden marker.</summary>
    private bool DrawLandingSiteSurface(Rect chart, double latitude, double longitude)
    {
        EnsureOrbitSpherePreviews();
        var radius = Mathf.Min(Mathf.Max(18f, Mathf.Min(chart.width, chart.height) * .39f * _orbitZoom), Mathf.Min(chart.width, chart.height) * .47f);
        var center = new Vector2(chart.center.x, chart.center.y);
        if (_orbitPlanetRenderTexture != null)
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2), _orbitPlanetRenderTexture, ScaleMode.StretchToFill, true);
        var surfaceRadius = radius * (0.9f / 1.08f);
        var lightScreenDirection = new Vector2(_orbitLightDirectionView.x, -_orbitLightDirectionView.y);
        var lightMarker = center + lightScreenDirection * (surfaceRadius + 15f);
        DrawOrbitLine(center + lightScreenDirection * (surfaceRadius * .88f), lightMarker, new Color(1f, .76f, .28f, .8f), 2f);
        DrawOrbitMarker(lightMarker, 9f, new Color(1f, .76f, .28f));
        var dim = new Color(.72f, .84f, .87f, .48f);
        for (var meridian = -150; meridian <= 180; meridian += 30)
            DrawGlobeMeridian(center, surfaceRadius, meridian, dim, 1f);
        for (var latitudeLine = -60; latitudeLine <= 60; latitudeLine += 30)
            DrawGlobeLatitude(center, surfaceRadius, latitudeLine, dim, 1f);
        DrawGlobeMeridian(center, surfaceRadius, longitude, Accent, 2f);
        DrawGlobeLatitude(center, surfaceRadius, latitude, TemperatureOrange, 2f);
        var marker = ProjectGlobePoint(latitude, longitude, center, surfaceRadius);
        if (marker.Depth <= 0)
        {
            DrawOrbitMarker(marker.Point, 9f, new Color(1f, .2f, .24f));
            return false;
        }

        return true;
    }

    /// <summary>Draws the visible part of one longitude line on the rotated globe.</summary>
    private void DrawGlobeMeridian(Vector2 center, float radius, double longitude, Color color, float thickness)
    {
        var hasPrevious = false;
        var previous = Vector2.zero;
        for (var latitude = -90; latitude <= 90; latitude += 3)
        {
            var projected = ProjectGlobePoint(latitude, longitude, center, radius);
            if (projected.Depth > 0)
            {
                hasPrevious = false;
                continue;
            }

            if (hasPrevious)
                DrawOrbitLine(previous, projected.Point, color, thickness);
            previous = projected.Point;
            hasPrevious = true;
        }
    }

    /// <summary>Draws the visible part of one latitude line on the rotated globe.</summary>
    private void DrawGlobeLatitude(Vector2 center, float radius, double latitude, Color color, float thickness)
    {
        var hasPrevious = false;
        var previous = Vector2.zero;
        for (var longitude = 0; longitude <= 360; longitude += 3)
        {
            var projected = ProjectGlobePoint(latitude, longitude, center, radius);
            if (projected.Depth > 0)
            {
                hasPrevious = false;
                continue;
            }

            if (hasPrevious)
                DrawOrbitLine(previous, projected.Point, color, thickness);
            previous = projected.Point;
            hasPrevious = true;
        }
    }

    /// <summary>Projects a surface coordinate into chart pixels and retains depth for visibility checks.</summary>
    private (Vector2 Point, double Depth) ProjectGlobePoint(double latitude, double longitude, Vector2 center, float radius)
    {
        var lat = latitude * Math.PI / 180d;
        var lon = longitude * Math.PI / 180d;
        var x = Math.Cos(lat) * Math.Sin(lon);
        var y = Math.Sin(lat);
        var z = Math.Cos(lat) * Math.Cos(lon);
        var rotated = _orbitGlobeRotation * new Vector3((float)x, (float)y, (float)z);
        return (new Vector2(center.x + rotated.x * radius, center.y - rotated.y * radius), rotated.z);
    }

    /// <summary>Defines the initial viewing orientation shared by the orbital and landing previews.</summary>
    private static Quaternion DefaultOrbitRotation()
    {
        return Quaternion.AngleAxis(28f, Vector3.right) * Quaternion.AngleAxis(35f, Vector3.up);
    }

    /// <summary>Applies mouse dragging to the shared globe orientation while retaining control capture.</summary>
    private void HandleOrbitPreviewDrag(Rect chart)
    {
        var current = Event.current;
        var control = GUIUtility.GetControlID(FocusType.Passive, chart);
        if (_orbitView >= 2 && current.type == EventType.MouseDown && current.button == 0 && chart.Contains(current.mousePosition))
        {
            _orbitDragging = true;
            _orbitDragPosition = current.mousePosition;
            GUIUtility.hotControl = control;
            current.Use();
        }
        else if (_orbitDragging && GUIUtility.hotControl == control && current.type == EventType.MouseDrag)
        {
            var delta = current.mousePosition - _orbitDragPosition;
            _orbitDragPosition = current.mousePosition;
            // Rotate around screen axes, so front-facing surface points follow the pointer at every globe orientation.
            var dragAxis = new Vector3(-delta.y, -delta.x, 0f);
            if (dragAxis.sqrMagnitude > 0.000001f)
                _orbitGlobeRotation = (Quaternion.AngleAxis(delta.magnitude * .6f, dragAxis.normalized) * _orbitGlobeRotation).normalized;
            current.Use();
        }
        else if (_orbitDragging && GUIUtility.hotControl == control && (current.type == EventType.MouseUp || current.type == EventType.MouseLeaveWindow))
        {
            _orbitDragging = false;
            GUIUtility.hotControl = 0;
            current.Use();
        }
    }

    /// <summary>Reads a draft orbital angle with a zero fallback for invalid input.</summary>
    private double OrbitAngle(string suffix)
    {
        var field = _project!.OrbitFields.FirstOrDefault(f => f.Label.EndsWith(" / " + suffix, StringComparison.Ordinal));
        return field != null && double.TryParse(field.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && !double.IsNaN(value) && !double.IsInfinity(value) ? value * Math.PI / 180d : 0d;
    }

    /// <summary>Calculates the direction from the playable body toward the primary body at the preview phase.</summary>
    private Vector3 CurrentPrimaryLightDirection()
    {
        var eccentricityField = _project!.OrbitFields.FirstOrDefault(f => f.Label.EndsWith(" / Eccentricity", StringComparison.Ordinal));
        if (eccentricityField == null || !double.TryParse(eccentricityField.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var eccentricity) || double.IsNaN(eccentricity) || double.IsInfinity(eccentricity) || eccentricity < 0 || eccentricity >= 1)
            return Vector3.back;
        var position = CalculateOrbitPosition(
            SolveEccentricAnomaly(_orbitPhase * Math.PI / 180d, eccentricity),
            eccentricity,
            OrbitAngle("Inclination"),
            OrbitAngle("LongitudeOfAscendingNode"),
            OrbitAngle("ArgumentOfPeriapsis")
        );
        return new Vector3((float)-position.x, (float)-position.z, (float)-position.y).normalized;
    }

    /// <summary>Solves the eccentric anomaly used to place the body along its elliptical orbit.</summary>
    private static double SolveEccentricAnomaly(double meanAnomaly, double eccentricity)
    {
        var eccentricAnomaly = meanAnomaly;
        for (var i = 0; i < 8; i++)
            eccentricAnomaly -= (eccentricAnomaly - eccentricity * Math.Sin(eccentricAnomaly) - meanAnomaly) / (1 - eccentricity * Math.Cos(eccentricAnomaly));
        return eccentricAnomaly;
    }

    /// <summary>Transforms an elliptical orbit position by inclination, ascending node and periapsis.</summary>
    private static Vector3 CalculateOrbitPosition(double eccentricAnomaly, double eccentricity, double inclination, double node, double periapsis)
    {
        var x = Math.Cos(eccentricAnomaly) - eccentricity;
        var y = Math.Sqrt(1 - eccentricity * eccentricity) * Math.Sin(eccentricAnomaly);
        var cosPeriapsis = Math.Cos(periapsis);
        var sinPeriapsis = Math.Sin(periapsis);
        var inPlaneX = cosPeriapsis * x - sinPeriapsis * y;
        var inPlaneY = sinPeriapsis * x + cosPeriapsis * y;
        var cosNode = Math.Cos(node);
        var sinNode = Math.Sin(node);
        var cosInclination = Math.Cos(inclination);
        var sinInclination = Math.Sin(inclination);
        var orbitX = cosNode * inPlaneX - sinNode * cosInclination * inPlaneY;
        var orbitY = sinNode * inPlaneX + cosNode * cosInclination * inPlaneY;
        var orbitZ = sinInclination * inPlaneY;
        return new Vector3((float)orbitX, (float)orbitY, (float)orbitZ);
    }

    /// <summary>Projects an orbital position into the chosen overhead, side or rotated view.</summary>
    private static Vector2 ProjectOrbitPoint(double eccentricAnomaly, double eccentricity, double inclination, double node, double periapsis, int view, Quaternion viewRotation)
    {
        var orbit = CalculateOrbitPosition(eccentricAnomaly, eccentricity, inclination, node, periapsis);
        // Stationeers' preview uses Unity axes: X/Z is the orbital plane and
        // Y is its normal. Above therefore shows the full orbit; Side shows tilt.
        if (view == 0)
            return new Vector2(orbit.x, orbit.y);
        if (view == 1)
            return new Vector2(orbit.x, orbit.z);
        var rotated = viewRotation * new Vector3(orbit.x, orbit.z, orbit.y);
        return new Vector2(rotated.x, rotated.y);
    }

    /// <summary>Draws a coloured square marker centred on an orbital chart position.</summary>
    private static void DrawOrbitMarker(Vector2 point, float size, Color color)
    {
        var previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(point.x - size / 2f, point.y - size / 2f, size, size), Texture2D.whiteTexture);
        GUI.color = previous;
    }

    /// <summary>Draws a thick line segment without leaking GUI rotation or colour state.</summary>
    private static void DrawOrbitLine(Vector2 from, Vector2 to, Color color, float thickness)
    {
        var delta = to - from;
        if (delta.sqrMagnitude < 0.01f)
            return;
        var previousMatrix = GUI.matrix;
        var previousColor = GUI.color;
        try
        {
            GUI.color = color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
            GUI.DrawTexture(new Rect(from.x, from.y - thickness / 2f, delta.magnitude, thickness), Texture2D.whiteTexture);
        }
        finally
        {
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }
    }

    /// <summary>Shows the periapsis, apoapsis, orbital period and applicable sunlight estimate.</summary>
    private void DrawOrbitDistanceSummary(WorldProject.Field distance, double a, double e)
    {
        var unit = distance.Label.EndsWith("Au") ? "AU" : "km";
        var periapsis = a * (1 - e);
        var apoapsis = a * (1 + e);
        GUILayout.Label("Periapsis: " + periapsis.ToString("0.###", CultureInfo.InvariantCulture) + " " + unit + "    Apoapsis: " + apoapsis.ToString("0.###", CultureInfo.InvariantCulture) + " " + unit);
        var period = _project!.OrbitFields.FirstOrDefault(f => f.Label.EndsWith(" / Period"));
        if (period != null && double.TryParse(period.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var days) && !double.IsNaN(days) && !double.IsInfinity(days))
            GUILayout.Label("Orbital period: " + days.ToString("0.###", CultureInfo.InvariantCulture) + " simulated days");
        if (unit == "AU")
        {
            var farSunlight = 1367 / Math.Pow(apoapsis, 2);
            var nearSunlight = 1367 / Math.Pow(periapsis, 2);
            if (!double.IsInfinity(farSunlight) && !double.IsInfinity(nearSunlight))
                GUILayout.Label("Estimated sunlight: " + farSunlight.ToString("0.#", CultureInfo.InvariantCulture) + "–" + nearSunlight.ToString("0.#", CultureInfo.InvariantCulture) + " W/m² (1367 W/m² at 1 AU)");
        }
    }

    /// <summary>Edits view mode, zoom, simulation speed and phase without modifying world XML.</summary>
    private void DrawOrbitCameraControls()
    {
        GUILayout.BeginHorizontal();
        var selectedOrbitView = NativeSelectionGrid(_orbitView, new[] { "Above (XZ)", "Side (XY)", "3D", "Landing site" }, 4, _buttonStyle!, GUILayout.MaxWidth(540));
        _orbitView = selectedOrbitView;
        GUILayout.FlexibleSpace();
        if (NativeButton("−", _buttonStyle!, GUILayout.Width(34)))
            _orbitZoom = Mathf.Clamp(_orbitZoom - 0.2f, 0.6f, 2.4f);
        GUILayout.Label(Mathf.RoundToInt(_orbitZoom * 100f) + "%", _mutedStyle!, GUILayout.Width(44));
        if (NativeButton("+", _buttonStyle!, GUILayout.Width(34)))
            _orbitZoom = Mathf.Clamp(_orbitZoom + 0.2f, 0.6f, 2.4f);
        if (NativeButton("Reset view", _buttonStyle!, GUILayout.Width(100)))
        {
            _orbitZoom = 1f;
            _orbitGlobeRotation = DefaultOrbitRotation();
        }

        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (NativeButton(_orbitSimulationPlaying ? "Pause preview" : "▶ Simulate", _primaryButtonStyle!, GUILayout.Width(150)))
            _orbitSimulationPlaying = !_orbitSimulationPlaying;
        GUILayout.Label("Simulation speed · 1× = 1 orbit/min", _mutedStyle!, GUILayout.Width(230));
        var speedOptions = new[]
        {
            "1×",
            "10×"
        };
        var speedIndex = Mathf.Clamp(Array.IndexOf(new[] { 1f, 10f }, _orbitSimulationSpeed), 0, 1);
        var selectedSpeed = NativeSelectionGrid(speedIndex, speedOptions, 2, _buttonStyle!, GUILayout.Width(125));
        _orbitSimulationSpeed = new[]
        {
            1f,
            10f
        }[Mathf.Clamp(selectedSpeed, 0, 1)];
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        if (_orbitView == 3)
            GUILayout.Label("Landing site · drag to rotate the globe and light reference; simulation moves the light around the stationary globe", _mutedStyle!);
        else
        {
            GUILayout.Label(
                "Position along orbit: " + Mathf.RoundToInt(_orbitPhase) + "°" + (_orbitView == 2 ? "   ·   Drag the preview to rotate" : "") + (_orbitSimulationPlaying ? "   ·   Simulating at " + _orbitSimulationSpeed.ToString("0", CultureInfo.InvariantCulture) + "×" : ""),
                _mutedStyle!
            );
            _orbitPhase = GUILayout.HorizontalSlider(_orbitPhase, 0f, 360f);
        }
    }

    /// <summary>Draws the orbit path, reference axes and bodies, returning the current orbital distance.</summary>
    private double DrawOrbitalSystemChart(Rect chart, double a, double e)
    {
        var inclination = OrbitAngle("Inclination");
        var node = OrbitAngle("LongitudeOfAscendingNode");
        var periapsisAngle = OrbitAngle("ArgumentOfPeriapsis");
        var points = new Vector2[121];
        var maxExtent = 1d;
        for (var i = 0; i < points.Length; i++)
        {
            var eccentricAnomaly = i * Math.PI * 2 / (points.Length - 1);
            points[i] = ProjectOrbitPoint(eccentricAnomaly, e, inclination, node, periapsisAngle, _orbitView, _orbitGlobeRotation);
            maxExtent = Math.Max(maxExtent, Math.Max(Math.Abs(points[i].x), Math.Abs(points[i].y)));
        }

        var margin = 22f;
        var scale = Mathf.Min((chart.width - margin * 2) / (float)(maxExtent * 2), (chart.height - margin * 2) / (float)(maxExtent * 2)) * _orbitZoom;
        var center = new Vector2(chart.width / 2f, chart.height / 2f);
        /// <summary>Converts projected orbit coordinates into pixels inside the chart.</summary>
        Vector2 ToChartPosition(Vector2 p) => new Vector2(center.x + p.x * scale, center.y - p.y * scale);
        for (var i = 1; i < points.Length; i++)
            DrawOrbitLine(ToChartPosition(points[i - 1]), ToChartPosition(points[i]), MenuBlue, 2f);
        var origin = ToChartPosition(Vector2.zero);
        if (_orbitView == 2)
            DrawOrbitAxisTriad(center, scale);
        else
        {
            DrawOrbitLine(new Vector2(margin, center.y), new Vector2(chart.width - margin, center.y), new Color(.42f, .42f, .46f, .5f), 1f);
            DrawOrbitLine(new Vector2(center.x, margin), new Vector2(center.x, chart.height - margin), new Color(.42f, .42f, .46f, .5f), 1f);
        }

        DrawOrbitSphere(origin, 30f * _orbitZoom, sun: true);
        var meanAnomaly = _orbitPhase * Math.PI / 180d;
        var eccentricAnomalyAtPhase = SolveEccentricAnomaly(meanAnomaly, e);
        var playable = ToChartPosition(ProjectOrbitPoint(eccentricAnomalyAtPhase, e, inclination, node, periapsisAngle, _orbitView, _orbitGlobeRotation));
        DrawOrbitSphere(playable, 19f * _orbitZoom, sun: false);
        var radius = a * (1 - e * Math.Cos(eccentricAnomalyAtPhase));
        return radius;
    }
}
