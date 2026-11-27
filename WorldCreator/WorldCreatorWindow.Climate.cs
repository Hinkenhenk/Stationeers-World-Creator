using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private ClimatePreview? _climatePreview;
    private WorldProject? _climateProject;
    private string? _climateInputKey;
    private string? _climateError;
    private float[][]? _climateSamples;
    private float[]? _pressureDay;
    private float[][]? _pressureOrbit;
    private string? _pressureInputKey;
    private string? _pressureError;
    private string? _pressureOrbitError;
    private string? _pressureSiteNote;
    private double _pressureOrbitPeriod;
    private double _pressureOrbitEccentricity;
    private double _pressureOrbitDistance;
    private double _pressureOrbitSolarConstant;
    private float _climateSunlight = 50;
    private float _climateAngle = 90;
    private int _greenhouseSelection;
    private readonly bool[] _climateVisible =
    {
        true,
        true,
        true,
        true,
        true
    };
    private static readonly string[] ClimateLayers =
    {
        "Combined",
        "Baseline",
        "Sunlight offset",
        "Greenhouse offset",
        "Density offset"
    };
    private static readonly Color[] ClimateColors =
    {
        new Color(1, .35f, .85f),
        new Color(1, .48f, .22f),
        new Color(1, .85f, .2f),
        new Color(.45f, 1, .3f),
        new Color(.25f, .8f, 1)
    };
    /// <summary>Arranges the calculated temperature graph, pressure estimates and greenhouse reference controls.</summary>
    private void DrawClimatePreview()
    {
        // Change the calculated data on Layout so invalid drafts cannot change the control tree during repaint.
        if (Event.current.type == EventType.Layout)
        {
            RefreshClimatePreview();
        }

        if (_climateSamples == null || _climatePreview == null)
        {
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            GUILayout.Label("CLIMATE PREVIEW", _sectionStyle!);
            GUILayout.Label(_climateError ?? "Preparing the atmosphere preview…", _mutedStyle!);
            GUILayout.EndVertical();
            return;
        }

        var wide = Screen.width >= 1400;
        if (wide)
            GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("COMBINED TEMPERATURE PREVIEW", _sectionStyle!);
        GUILayout.Label(
            "See how the game's temperature curves combine for this atmosphere. Weather, phase-change energy and player heat inputs are excluded from this initial-world preview.",
            _mutedStyle!
        );
        GUILayout.BeginHorizontal();
        for (var layer = 0; layer < ClimateLayers.Length; layer++)
        {
            if (NativeButton(ClimateLayers[layer], _climateVisible[layer] ? _primaryButtonStyle! : _buttonStyle!, GUILayout.ExpandWidth(true)))
                _climateVisible[layer] = !_climateVisible[layer];
        }

        GUILayout.EndHorizontal();
        HelpLabel(
            new GUIContent(
            "Relative sunlight level: " + _climateSunlight.ToString("0.#", CultureInfo.InvariantCulture) + " / 100  ⓘ",
            "Position across the orbit's calculated irradiance range: 0 is the lower end and 100 the upper end. This preview input selects where the solar-radiation offset curves are evaluated; it does not edit world settings."
        ),
            _mutedStyle!
        );
        _climateSunlight = GUILayout.HorizontalSlider(_climateSunlight, 0, 100);
        GUILayout.Label("Inspect sun angle: " + _climateAngle.ToString("0", CultureInfo.InvariantCulture) + "°", _mutedStyle!);
        _climateAngle = GUILayout.HorizontalSlider(_climateAngle, 0, 180);
        var displayed = _climateSamples.Select((series, layer) => series.Select(value => DisplayClimateValue(layer, value)).ToArray()).ToArray();
        DrawClimatePlot(
            displayed,
            ClimateColors,
            _climateVisible,
            180,
            "Sun angle (°) · 0 overhead / 90 horizon / 180 night",
            "Temperature / change (" + TemperatureUnitName(_temperatureUnit) + ")",
            _climateAngle
        );
        GUILayout.BeginHorizontal();
        for (var layer = 0; layer < ClimateLayers.Length; layer++)
        {
            var previous = GUI.color;
            GUI.color = ClimateColors[layer];
            GUILayout.Label("■ " + ClimateLayers[layer], _mutedStyle!);
            GUI.color = previous;
        }

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        if (wide)
        {
            GUILayout.Space(CardSpacing);
            GUILayout.BeginVertical(GUILayout.Width(300));
        }
        else
            GUILayout.Space(CardSpacing);
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("TEMPERATURE BREAKDOWN", _sectionStyle!);
        var index = Mathf.Clamp(Mathf.RoundToInt(_climateAngle), 0, 180);
        GUILayout.Label("At sun angle " + index + "°", _mutedStyle!);
        for (var layer = 0; layer < ClimateLayers.Length; layer++)
        {
            GUILayout.Label(ClimateLayers[layer], _mutedStyle!);
            GUILayout.Label(DisplayClimateValue(layer, _climateSamples[layer][index]).ToString("0.##", CultureInfo.InvariantCulture) + " " + TemperatureUnitName(_temperatureUnit) + (layer >= 2 ? " change" : ""));
        }

        GUILayout.Space(10);
        GUILayout.Label("Greenhouse index: " + _climatePreview.GreenhouseIndex.ToString("0.###", CultureInfo.InvariantCulture), _mutedStyle!);
        GUILayout.Label("Gas density: " + _climatePreview.Density.ToString("0.###", CultureInfo.InvariantCulture) + " mmol/L", _mutedStyle!);
        GUILayout.EndVertical();
        if (wide)
        {
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(CardSpacing);
        DrawPressurePreview();
        GUILayout.Space(CardSpacing);
        DrawGreenhousePreview(_climatePreview);
    }

    /// <summary>Rebuilds daily pressure samples and refreshes the seasonal envelope when its inputs change.</summary>
    private void UpdatePressurePreview(string atmosphereKey)
    {
        _pressureDay = null;
        _pressureError = null;
        try
        {
            // Density is mmol/L; convert to moles in the same 8,000 L cell used by world gas quantities.
            var moles = _climatePreview!.Density * 8d;
            _pressureDay = _climateSamples![0].Select(kelvin => PressureAtTemperature(moles, kelvin)).ToArray();
            RefreshSeasonalPressurePreview(atmosphereKey, moles);
        }
        catch (Exception error)
        {
            _pressureDay = null;
            _pressureOrbit = null;
            _pressureError = "Cannot estimate pressure: " + error.GetBaseException().Message + " Check gas quantities and temperatures (0 K or higher).";
        }
    }

    /// <summary>Converts a temperature sample to pressure and rejects values the graph cannot display.</summary>
    private static float PressureAtTemperature(double moles, float kelvin)
    {
        var pressure = (float)PressureEstimate.KPa(moles, kelvin);
        if (float.IsInfinity(pressure))
            throw new InvalidOperationException("Pressure exceeds the display range.");
        return pressure;
    }

    /// <summary>Caches the seasonal pressure envelope separately so changing the inspection slider does not rebuild it.</summary>
    private void RefreshSeasonalPressurePreview(string atmosphereKey, double moles)
    {
        if (_pressureInputKey == atmosphereKey && _pressureOrbit != null)
            return;
        _pressureOrbitError = null;
        _pressureOrbit = null;
        try
        {
            ReadPressureOrbitSettings();
            _pressureOrbit = CalculateSeasonalPressureEnvelope(moles);
        }
        catch (Exception error)
        {
            _pressureOrbitError = error.GetBaseException().Message;
        }

        _pressureInputKey = atmosphereKey;
    }

    /// <summary>Reads the orbit used by pressure estimates and rejects unsupported parent-star distance assumptions.</summary>
    private void ReadPressureOrbitSettings()
    {
        var rotation = _project!.GetRotationSettings();
        var primary = string.IsNullOrWhiteSpace(_project.PrimaryBodyId) ? "Sun" : _project.PrimaryBodyId;
        if (rotation.ParentId != primary)
            throw new InvalidOperationException("Solar seasons for a body orbiting another planet need parent-orbit modelling; its local orbit is not its distance from the star.");
        _pressureOrbitDistance = ReadPressureOrbitValue("SemiMajorAxisAu");
        if (double.IsNaN(_pressureOrbitDistance))
        {
            var km = ReadPressureOrbitValue("SemiMajorAxisKm");
            if (_project!.IsStarPrimary(_project.PrimaryBodyId))
                throw new InvalidOperationException("A moon's parent-body orbit cannot be treated as its distance from the star. Solar seasons for nested systems need parent-orbit modelling.");
            _pressureOrbitDistance = km / 149597870.7;
        }

        _pressureOrbitEccentricity = ReadPressureOrbitValue("Eccentricity");
        _pressureOrbitPeriod = ReadPressureOrbitValue("Period");
        var solarField = _project!.WorldFields.FirstOrDefault(field => field.Label.EndsWith("/Primary star irradiance (W/m²)", StringComparison.Ordinal));
        _pressureOrbitSolarConstant = string.IsNullOrWhiteSpace(solarField?.Value) ? 1367 : double.Parse(solarField!.Value, CultureInfo.InvariantCulture);
    }

    /// <summary>Parses one orbital input while leaving missing or invalid drafts distinguishable as NaN.</summary>
    private double ReadPressureOrbitValue(string name) => double.TryParse(
        _project!.OrbitFields.FirstOrDefault(field => field.Label.EndsWith(" / " + name, StringComparison.Ordinal))?.Value,
        NumberStyles.Float,
        CultureInfo.InvariantCulture,
        out var value
    ) ? value : double.NaN;
    /// <summary>Samples each orbital degree and finds the lowest and highest daily pressure at that position.</summary>
    private float[][] CalculateSeasonalPressureEnvelope(double moles)
    {
        var minimum = new float[361];
        var maximum = new float[361];
        var rotation = _project!.GetRotationSettings();
        var primary = string.IsNullOrWhiteSpace(_project.PrimaryBodyId) ? "Sun" : _project.PrimaryBodyId;
        var directStar = rotation.ParentId == primary && _project.IsStarPrimary(primary);
        var site = directStar ? new float[361] : null;
        _pressureSiteNote = directStar ? null : "Landing-site curve unavailable: lighting from the parent star in a nested system needs parent-orbit modelling.";
        for (var phase = 0; phase <= 360; phase++)
        {
            var orbit = PressureEstimate.AtMeanAnomaly(_pressureOrbitDistance, _pressureOrbitEccentricity, _pressureOrbitSolarConstant, phase);
            minimum[phase] = float.MaxValue;
            for (var angle = 0; angle <= 180; angle++)
            {
                var pressure = PressureAtTemperature(moles, _climatePreview!.Evaluate(angle, orbit.Sunlight)[0]);
                minimum[phase] = Mathf.Min(minimum[phase], pressure);
                maximum[phase] = Mathf.Max(maximum[phase], pressure);
            }
            if (site != null)
                site[phase] = PressureAtTemperature(moles, _climatePreview!.Evaluate(LandingSiteSunAngle(phase, rotation), orbit.Sunlight)[0]);
        }
        return site == null ? new[] { minimum, maximum } : new[] { minimum, maximum, site };
    }

    /// <summary>Shows daily pressure and the seasonal pressure envelope with their calculation limits.</summary>
    private void DrawPressurePreview()
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("EXPECTED PRESSURE · IDEAL-GAS ESTIMATE", _sectionStyle!);
        HelpLabel(
            new GUIContent(
            "How this estimate works  ⓘ",
            "P = nRT/V. Uses the current gas-phase molar density and the combined temperature curves in a fixed 8,000 L cell. Liquid entries do not contribute gas pressure. No gas is removed by condensation in this estimate."
        ),
            _mutedStyle!
        );
        GUILayout.Label(
            "Fixed gas amount and volume. Weather, condensation/evaporation, thermal lag and player activity are excluded; actual pressure can differ.",
            _mutedStyle!
        );
        if (_pressureDay == null)
            GUILayout.Label(_pressureError ?? "Preparing pressure estimate…", _mutedStyle!);
        else
        {
            var index = Mathf.Clamp(Mathf.RoundToInt(_climateAngle), 0, 180);
            GUILayout.Label("Day/night sun-angle range: " + PressureRange(_pressureDay) + " · At " + index + "°: " + _pressureDay[index].ToString("0.##", CultureInfo.InvariantCulture) + " kPa");
            DrawClimatePlot(
                new[] { _pressureDay },
                new[] { ClimateColors[0] },
                new[] { true },
                180,
                "Sun angle (°) · 0 overhead / 90 horizon / 180 night",
                "Pressure (kPa)",
                _climateAngle
            );
            GUILayout.Space(CardSpacing);
            GUILayout.Label("SEASONAL / ORBITAL PRESSURE ENVELOPE", _sectionStyle!);
            HelpLabel(
                new GUIContent(
                "Pressure over one eccentric orbit  ⓘ",
                "Solves the eccentric orbit and inverse-square irradiance, mapped to the game's 0–100 sunlight input. Blue/yellow bound all sun angles. Pink, when available, uses this landing site's latitude/longitude and applied rotation/axis. Pink samples once per orbital degree and may miss short daily extrema. It is illustrative; weather, thermal lag and parent-star lighting in nested systems are excluded."
            ),
                _mutedStyle!
            );
            if (_pressureOrbit == null)
                GUILayout.Label(_pressureOrbitError ?? "No orbital estimate available.", _mutedStyle!);
            else
            {
                var periodAvailable = !double.IsNaN(_pressureOrbitPeriod) && !double.IsInfinity(_pressureOrbitPeriod) && _pressureOrbitPeriod > 0 && _pressureOrbitPeriod <= float.MaxValue && (float)_pressureOrbitPeriod > 0;
                var xMaximum = periodAvailable ? (float)_pressureOrbitPeriod : 360f;
                GUILayout.Label("One orbit from closest approach: constant time spacing, with a lower/upper pressure across sampled day/night sun angles.", _mutedStyle!);
                DrawClimatePlot(
                    _pressureOrbit,
                    new[] { ClimateColors[4], ClimateColors[2], ClimateColors[0] },
                    new[] { true, true, true },
                    xMaximum,
                    periodAvailable ? "Elapsed game days from periapsis" : "Mean anomaly (°) · 0 / 360 periapsis, 180 apoapsis",
                    "Pressure envelope (kPa)",
                    _orbitPhase / 360 * xMaximum
                );
                if (_pressureOrbit.Length > 2)
                    GUILayout.Label("Pink: sampled landing-site pressure using the applied rotation and axis.", _mutedStyle!);
                if (_pressureSiteNote != null) GUILayout.Label(_pressureSiteNote, _mutedStyle!);
                GUILayout.Label(
                    "Blue: lower sun-angle pressure · Yellow: upper sun-angle pressure · Sampled orbit range: " + PressureRange(_pressureOrbit.SelectMany(series => series).ToArray()),
                    _mutedStyle!
                );
                var near = PressureEstimate.AtMeanAnomaly(_pressureOrbitDistance, _pressureOrbitEccentricity, _pressureOrbitSolarConstant, 0);
                var far = PressureEstimate.AtMeanAnomaly(_pressureOrbitDistance, _pressureOrbitEccentricity, _pressureOrbitSolarConstant, 180);
                GUILayout.Label(
                    "Periapsis: " + near.Irradiance.ToString("0.##", CultureInfo.InvariantCulture) + " W/m² · Apoapsis: " + far.Irradiance.ToString("0.##", CultureInfo.InvariantCulture) + " W/m²",
                    _mutedStyle!
                );
                if (_pressureOrbitEccentricity == 0)
                    GUILayout.Label("Circular orbit: constant irradiance and pressure envelope. A zero-width irradiance range uses sunlight input 0.", _mutedStyle!);
                GUILayout.Label(
                    "Blue/yellow include sun angles the site may never reach. No thermal lag, condensation, weather or gas exchange; parent-body orbital seasons are excluded.",
                    _mutedStyle!
                );
            }
        }

        GUILayout.EndVertical();
    }

    /// <summary>Formats the lowest and highest pressure in a calculated series.</summary>
    private static string PressureRange(float[] values) => values.Min().ToString("0.##", CultureInfo.InvariantCulture) + "–" + values.Max().ToString("0.##", CultureInfo.InvariantCulture) + " kPa (change " + (values.Max() - values.Min()).ToString("0.##", CultureInfo.InvariantCulture) + " kPa)";
    /// <summary>Converts absolute temperatures and temperature differences using their respective unit rules.</summary>
    private float DisplayClimateValue(int layer, float kelvin) => layer < 2 ? (float)ConvertTemperatureFromKelvin(kelvin, _temperatureUnit) : _temperatureUnit == 2 ? kelvin * 1.8f : kelvin;
    /// <summary>Shows how each gas type contributes to the atmosphere's greenhouse index.</summary>
    private void DrawGreenhousePreview(ClimatePreview preview)
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("GAS GREENHOUSE RESPONSE", _sectionStyle!);
        GUILayout.Label(
            "Each gas contributes to the greenhouse index according to its concentration. The world’s day/night greenhouse curves convert the combined index into a temperature change.",
            _mutedStyle!
        );
        GUILayout.Label(preview.ResponseSource, _mutedStyle!);
        var labels = preview.Responses.Select(response => Gases.FirstOrDefault(gas => gas.Id == response.Id)?.Name ?? response.Id).ToArray();
        _greenhouseSelection = Mathf.Clamp(_greenhouseSelection, 0, labels.Length - 1);
        _greenhouseSelection = NativeDropdown("greenhouse-gas", _greenhouseSelection, labels, _buttonStyle!, GUILayout.Width(300));
        var response = preview.Responses[Mathf.Clamp(_greenhouseSelection, 0, labels.Length - 1)];
        var concentration = preview.Concentration(response.Type);
        var xMax = Mathf.Max(100, concentration);
        var values = Enumerable.Range(0, 101).Select(i => response.Curve.Evaluate(xMax * (i / 100f))).ToArray();
        DrawClimatePlot(
            new[] { values },
            new[] { GasColor(response.Id) },
            new[] { true },
            xMax,
            "Gas concentration (mmol/L)",
            "Greenhouse index contribution",
            concentration
        );
        GUILayout.Label(
            labels[_greenhouseSelection] + " in this atmosphere: " + concentration.ToString("0.###", CultureInfo.InvariantCulture) + " mmol/L → " + preview.Contribution(response).ToString("0.###", CultureInfo.InvariantCulture) + " greenhouse index",
            _mutedStyle!
        );
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        GUILayout.Label("GAS", _sectionStyle!, GUILayout.MinWidth(180));
        GUILayout.Label("CONCENTRATION (mmol/L)", _sectionStyle!, GUILayout.MinWidth(190));
        GUILayout.Label("GREENHOUSE CONTRIBUTION", _sectionStyle!, GUILayout.MinWidth(190));
        GUILayout.EndHorizontal();
        foreach (var gas in preview.Gases)
        {
            var gasResponse = preview.Responses.FirstOrDefault(item => item.Type == gas.Type);
            var gasName = gasResponse == null ? gas.Type.ToString() : Gases.FirstOrDefault(item => item.Id == gasResponse.Id)?.Name ?? gasResponse.Id;
            GUILayout.BeginHorizontal();
            GUILayout.Label(gasName, _mutedStyle!, GUILayout.MinWidth(180));
            GUILayout.Label(preview.Concentration(gas.Type).ToString("0.###", CultureInfo.InvariantCulture), GUILayout.MinWidth(190));
            GUILayout.Label(
                gasResponse == null ? "0 (no response curve)" : preview.Contribution(gasResponse).ToString("0.###", CultureInfo.InvariantCulture),
                GUILayout.MinWidth(190)
            );
            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
    }

    /// <summary>Draws the axes, visible series and inspection marker for a calculated climate graph.</summary>
    private void DrawClimatePlot(float[][] series, Color[] colors, bool[] visible, float xMaximum, string xTitle, string yTitle, float marker)
    {
        GUILayout.Label(yTitle, _mutedStyle!);
        var chartRect = GUILayoutUtility.GetRect(100, 350, GUILayout.ExpandWidth(true));
        GUI.Box(chartRect, "");
        GUI.BeginGroup(chartRect);
        try
        {
            var chart = new Rect(0, 0, chartRect.width, chartRect.height);
            var plot = new Rect(chart.x + 72, chart.y + 16, Mathf.Max(1, chart.width - 94), chart.height - 72);
            var finite = series.Where((_, index) => visible[index]).SelectMany(values => values).Where(value => !float.IsNaN(value) && !float.IsInfinity(value))
                .ToArray();
            var min = finite.Length == 0 ? 0 : Mathf.Min(0, finite.Min());
            var max = finite.Length == 0 ? 1 : Mathf.Max(min + 1, finite.Max());
            if (float.IsInfinity(max - min))
            {
                GUI.Label(chart, "The selected curve range is too large to display.", _mutedStyle!);
                return;
            }

            var step = Math.Pow(10, Math.Floor(Math.Log10((max - min) / 5)));
            step *= (max - min) / (step * 5) > 5 ? 10 : (max - min) / (step * 5) > 2 ? 5 : 2;
            min = (float)(Math.Floor(min / step) * step);
            max = (float)(Math.Ceiling(max / step) * step);
            if (float.IsInfinity(min) || float.IsInfinity(max))
                return;
            var style = new GUIStyle(_mutedStyle!)
            {
                alignment = TextAnchor.MiddleRight
            };
            for (var tick = 0; tick <= 5; tick++)
            {
                var fraction = tick / 5f;
                var y = plot.yMax - fraction * plot.height;
                GUI.Label(new Rect(chart.x + 2, y - 9, 62, 18), Mathf.Lerp(min, max, fraction).ToString("0.##", CultureInfo.InvariantCulture), style);
                DrawOrbitLine(new Vector2(plot.x, y), new Vector2(plot.xMax, y), new Color(1, 1, 1, .13f), 1);
            }

            for (var tick = 0; tick <= 4; tick++)
            {
                var x = plot.x + tick / 4f * plot.width;
                GUI.Label(new Rect(x - 26, plot.yMax + 2, 60, 18), (tick / 4f * xMaximum).ToString("0.#", CultureInfo.InvariantCulture), _mutedStyle!);
            }

            GUI.Label(new Rect(plot.x, plot.yMax + 22, plot.width, 24), xTitle, _mutedStyle!);
            if (Event.current.type != EventType.Repaint)
                return;
            DrawOrbitLine(new Vector2(plot.x, plot.y), new Vector2(plot.x, plot.yMax), Muted, 1);
            DrawOrbitLine(new Vector2(plot.x, plot.yMax), new Vector2(plot.xMax, plot.yMax), Muted, 1);
            var markerX = plot.x + Mathf.Clamp01(marker / xMaximum) * plot.width;
            DrawOrbitLine(new Vector2(markerX, plot.y), new Vector2(markerX, plot.yMax), new Color(1, 1, 1, .4f), 1);
            for (var layer = 0; layer < series.Length; layer++)
            {
                if (!visible[layer])
                    continue;
                var samples = series[layer];
                DrawClimateSampleSeries(samples, colors[layer], plot, min, max, layer == 0 ? 2 : 1);
            }
        }
        finally
        {
            GUI.EndGroup();
        }
    }

    /// <summary>Recalculates atmosphere samples only when the project or preview inputs have changed.</summary>
    private void RefreshClimatePreview()
    {
        var atmosphereKey = string.Join("\n", _project!.AtmosphereFields.Select(field => field.Label + "=" + field.Value));
        var orbitKey = string.Join("\n", _project.OrbitFields.Concat(_project.WorldFields).Select(field => field.Label + "=" + field.Value));
        var pressureKey = atmosphereKey + "\nOrbit=" + orbitKey + "\nRotation=" + _project.GetRotationSettings().CacheKey;
        var key = pressureKey + "\nSunlight=" + _climateSunlight.ToString("R", CultureInfo.InvariantCulture);
        if (_climateProject != _project || key != _climateInputKey)
        {
            _climateInputKey = key;
            _climateSamples = null;
            _climateError = null;
            try
            {
                if (_climateProject != _project || _climatePreview == null)
                {
                    _climatePreview = new ClimatePreview(Application.streamingAssetsPath);
                    _climateProject = _project;
                }

                _climatePreview.UpdateAtmosphere(_project.CreateAtmosphereSnapshot());
                var samples = Enumerable.Range(0, 5).Select(_ => new float[181]).ToArray();
                SampleClimateTemperatureLayers(samples);
                _climateSamples = samples;
                UpdatePressurePreview(pressureKey);
            }
            catch (Exception error)
            {
                _climateProject = _project;
                _climatePreview = null;
                _climateError = error.GetBaseException().Message;
            }
        }
    }

    /// <summary>Draws one finite climate series inside the shared plot bounds.</summary>
    private void DrawClimateSampleSeries(float[] samples, Color color, Rect plot, float min, float max, float width)
    {
        for (var sample = 1; sample < samples.Length; sample++)
        {
            if (float.IsNaN(samples[sample - 1]) || float.IsInfinity(samples[sample - 1]) || float.IsNaN(samples[sample]) || float.IsInfinity(samples[sample]))
                continue;
            var from = new Vector2(plot.x + (sample - 1f) / (samples.Length - 1) * plot.width, plot.yMax - (samples[sample - 1] - min) / (max - min) * plot.height);
            var to = new Vector2(plot.x + sample / (float)(samples.Length - 1) * plot.width, plot.yMax - (samples[sample] - min) / (max - min) * plot.height);
            DrawOrbitLine(from, to, color, width);
        }
    }

    /// <summary>Samples each temperature component at every supported sun angle.</summary>
    private void SampleClimateTemperatureLayers(float[][] samples)
    {
        for (var angle = 0; angle <= 180; angle++)
        {
            var values = _climatePreview!.Evaluate(angle, _climateSunlight);
            for (var layer = 0; layer < values.Length; layer++)
                samples[layer][angle] = values[layer];
        }
    }
}
