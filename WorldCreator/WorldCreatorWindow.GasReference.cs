using System;
using System.Globalization;
using System.Linq;
using Assets.Scripts.Atmospherics;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private int _phaseGasSelection;
    private int _phaseComparison;
    private bool _phaseLogScale = true;
    private bool _phaseCelsius;
    private GUIStyle? _phasePropertyLabelStyle;
    private GUIStyle? _phasePropertyValueStyle;
    private GUIStyle? _phaseChartLabelStyle;
    private GUIStyle? _phaseChartHeadingStyle;
    private GUIStyle? _phaseToggleStyle;
    private PhaseBoundary? _phasePrimaryCurve;
    private PhaseBoundary? _phaseComparisonCurve;

    /// <summary>Stores a bounded set of samples from the installed game's liquid-vapour calculation.</summary>
    private sealed class PhaseBoundary
    {
        internal readonly Chemistry.GasType Type;
        internal readonly double[] Temperatures = new double[97];
        internal readonly double[] Pressures = new double[97];
        internal readonly double Freeze, MaximumTemperature, MaximumPressure;

        /// <summary>Samples once when a gas changes; no render targets or runtime scene objects are allocated.</summary>
        internal PhaseBoundary(Mole gas)
        {
            Type = gas.Type;
            Freeze = gas.FreezingTemperature().ToDouble();
            MaximumTemperature = gas.MaxLiquidTemperature().ToDouble();
            MaximumPressure = gas.MinimumLiquidPressureAtMaxTemperature().ToDouble();
            if (double.IsNaN(Freeze + MaximumTemperature + MaximumPressure) || double.IsInfinity(Freeze + MaximumTemperature + MaximumPressure) || Freeze <= 0 || MaximumTemperature <= Freeze || MaximumPressure <= .001) throw new InvalidOperationException("No liquid boundary is defined for this gas.");
            for (var i = 0; i < Temperatures.Length; i++)
            {
                var temperature = Freeze + (MaximumTemperature - Freeze) * i / (Temperatures.Length - 1);
                Temperatures[i] = temperature;
                Pressures[i] = Math.Max(gas.MinLiquidPressure().ToDouble(), Math.Min(MaximumPressure,
                    MoleHelper.EvaporationPressure(Type, new TemperatureKelvin(temperature)).ToDouble()));
                if (double.IsNaN(Pressures[i]) || double.IsInfinity(Pressures[i])) throw new InvalidOperationException("This gas returned an invalid boundary pressure.");
            }
        }
    }

    /// <summary>Shows the same gas properties as Stationpedia, plus a native-calculated boundary chart and optional comparison.</summary>
    private void DrawGasPhaseReference()
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("GAS PHASE REFERENCE", _sectionStyle!);
        GUILayout.Label("Stationpedia gas properties from the installed game. This reference does not change your atmosphere.", _mutedStyle!);
        var gases = Chemistry.Gases.Where(mole => Mole.MatterState(mole.Type) == AtmosphereHelper.MatterState.Gas).ToArray();
        if (gases.Length == 0) { GUILayout.Label("No gas definitions are available."); GUILayout.EndVertical(); return; }
        _phaseGasSelection = Mathf.Clamp(_phaseGasSelection, 0, gases.Length - 1);
        _phaseGasSelection = NativeDropdown("gas-phase-reference", _phaseGasSelection, gases.Select(gas => HumanizeXmlName(gas.Type.ToString())).ToArray(), _buttonStyle!, GUILayout.Width(300), GUILayout.Height(32));
        var gas = gases[_phaseGasSelection];
        try
        {
            GUILayout.Space(12);
            var condenses = MoleHelper.CanCondense(gas.Type);
            DrawPhasePropertyPair("Specific heat", gas.SpecificHeat().ToFloat().ToString("0.##", CultureInfo.InvariantCulture) + " J/K",
                "Latent heat", (gas.LatentHeatOfVaporization() / 1000).ToString("0.###", CultureInfo.InvariantCulture) + " kJ/mol");
            DrawPhasePropertyPair("Freezing temperature", gas.FreezingTemperature().ToDouble() > 0 ? PhaseTemperature(gas.FreezingTemperature().ToDouble()) : "Not defined",
                "Boiling point at 100 kPa", condenses && gas.BoilingPoint() >= gas.FreezingTemperature() ? PhaseTemperature(gas.BoilingPoint().ToDouble()) : "Not applicable");
            DrawPhasePropertyPair("Minimum condensation pressure", condenses ? gas.MinLiquidPressure().ToDouble().ToString("0.###", CultureInfo.InvariantCulture) + " kPa" : "Not applicable",
                "Maximum liquid temperature", condenses ? PhaseTemperature(gas.MaxLiquidTemperature().ToDouble()) + " at " + gas.MinimumLiquidPressureAtMaxTemperature().ToDouble().ToString("0.##", CultureInfo.InvariantCulture) + " kPa" : "Not applicable");
            if (condenses)
            {
                if (_phasePrimaryCurve?.Type != gas.Type) _phasePrimaryCurve = new PhaseBoundary(gas);
                DrawPhaseReferenceChart(gases.Where(item => MoleHelper.CanCondense(item.Type)).ToArray());
            }
            else GUILayout.Label("The installed game does not define a liquid boundary for this gas.", _mutedStyle!);
        }
        catch (Exception error) { GUILayout.Label("Reference unavailable: " + error.GetBaseException().Message, _mutedStyle!); }
        GUILayout.Label("The boundary uses the gas's own pressure. Mixtures depend on partial pressures, temperature and phase changes; this is not a time simulation or a condensation forecast.", _mutedStyle!);
        GUILayout.EndVertical();
    }

    /// <summary>Aligns two compact labelled property values in a Stationpedia-like table.</summary>
    private void DrawPhasePropertyPair(string leftLabel, string leftValue, string rightLabel, string rightValue)
    {
        _phasePropertyLabelStyle ??= new GUIStyle(_mutedStyle!) { fontStyle = FontStyle.Bold, wordWrap = true };
        _phasePropertyValueStyle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Normal, wordWrap = true };
        var width = Math.Max(1, (PageContentWidth - _cardStyle!.padding.horizontal - 16) * .5f);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(width));
        GUILayout.Label(leftLabel, _phasePropertyLabelStyle); GUILayout.Label(leftValue, _phasePropertyValueStyle);
        GUILayout.EndVertical();
        GUILayout.Space(16);
        GUILayout.BeginVertical(GUILayout.Width(width));
        GUILayout.Label(rightLabel, _phasePropertyLabelStyle); GUILayout.Label(rightValue, _phasePropertyValueStyle);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.Space(8);
    }

    /// <summary>Formats reference temperatures without changing any kelvin values saved in the world.</summary>
    private string PhaseTemperature(double kelvin) => (_phaseCelsius ? kelvin - 273.15 : kelvin).ToString("0.##", CultureInfo.InvariantCulture) + (_phaseCelsius ? " °C" : " K");

    /// <summary>Provides scale, temperature-unit and comparison controls above an uncompressed chart.</summary>
    private void DrawPhaseReferenceChart(Mole[] gases)
    {
        _phaseToggleStyle ??= new GUIStyle(GUI.skin.toggle) { fontStyle = FontStyle.Bold };
        GUILayout.BeginHorizontal();
        _phaseLogScale = GUILayout.Toggle(_phaseLogScale, "Log pressure scale", _phaseToggleStyle, GUILayout.Width(190));
        _phaseCelsius = GUILayout.Toggle(_phaseCelsius, "Use Celsius", _phaseToggleStyle, GUILayout.Width(150));
        GUILayout.Space(16);
        GUILayout.Label("Compare", _phasePropertyLabelStyle!, GUILayout.Width(75));
        _phaseComparison = NativeDropdown("gas-phase-comparison", Mathf.Clamp(_phaseComparison, 0, gases.Length),
            new[] { "None" }.Concat(gases.Select(gas => HumanizeXmlName(gas.Type.ToString()))).ToArray(), _buttonStyle!, GUILayout.Width(280), GUILayout.Height(32));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        if (_phaseComparison > 0 && _phaseComparison <= gases.Length && _phaseComparisonCurve?.Type != gases[_phaseComparison - 1].Type)
            _phaseComparisonCurve = new PhaseBoundary(gases[_phaseComparison - 1]);
        var comparison = _phaseComparison > 0 ? _phaseComparisonCurve : null;
        var chart = GUILayoutUtility.GetRect(100, 450, GUILayout.ExpandWidth(true), GUILayout.Height(450));
        DrawStationpediaPhaseDiagram(chart, _phasePrimaryCurve!, comparison);
        GUILayout.Label("Black: selected gas · Orange: comparison · Blue: freezing limit", _mutedStyle!);
    }

    /// <summary>Uses Stationpedia's light canvas, phase labels, boundary limits and cursor guides with native gas samples.</summary>
    private void DrawStationpediaPhaseDiagram(Rect chart, PhaseBoundary primary, PhaseBoundary? comparison)
    {
        if (_phaseChartLabelStyle == null)
        {
            _phaseChartLabelStyle = new GUIStyle(_mutedStyle!) { alignment = TextAnchor.MiddleCenter, wordWrap = false, fontStyle = FontStyle.Normal };
            _phaseChartLabelStyle.normal.textColor = Color.black;
            _phaseChartHeadingStyle = new GUIStyle(_phaseChartLabelStyle) { fontStyle = FontStyle.Bold };
        }
        var plot = new Rect(chart.x + 88, chart.y + 48, Math.Max(1, chart.width - 124), chart.height - 142);
        var maximumTemperature = Math.Max(700, Math.Max(primary.MaximumTemperature, comparison?.MaximumTemperature ?? 0) * 1.1);
        var maximumPressure = _phaseLogScale ? Math.Max(60000, Math.Max(primary.MaximumPressure, comparison?.MaximumPressure ?? 0) * 1.1)
            : Math.Max(primary.MaximumPressure, comparison?.MaximumPressure ?? 0) + 1000;
        var temperature = plot.Contains(Event.current.mousePosition) ? (Event.current.mousePosition.x - plot.x) / plot.width * maximumTemperature : double.NaN;
        var onBoundary = !double.IsNaN(temperature) && temperature >= primary.Freeze && temperature <= primary.MaximumTemperature;
        var pressure = onBoundary ? MoleHelper.EvaporationPressure(primary.Type, new TemperatureKelvin(temperature)).ToDouble() : double.NaN;
        if (Event.current.type == EventType.Repaint)
        {
            var colour = GUI.color;
            GUI.color = new Color(.72f, .72f, .72f);
            GUI.DrawTexture(chart, Texture2D.whiteTexture);
            GUI.color = colour;
            DrawOrbitLine(new Vector2(plot.x, plot.y), new Vector2(plot.x, plot.yMax), Color.gray, 2);
            DrawOrbitLine(new Vector2(plot.x, plot.yMax), new Vector2(plot.xMax, plot.yMax), Color.gray, 2);
            GUI.Label(new Rect(chart.x + 3, plot.y - 12, 80, 24), maximumPressure.ToString("0.#", CultureInfo.InvariantCulture), _phaseChartLabelStyle);
            GUI.Label(new Rect(chart.x + 3, plot.yMax - 12, 80, 24), "0", _phaseChartLabelStyle);
            GUI.Label(new Rect(plot.x - 55, plot.yMax + 5, 110, 24), PhaseTemperature(0), _phaseChartLabelStyle);
            GUI.Label(new Rect(chart.xMax - 118, plot.yMax + 5, 110, 24), PhaseTemperature(maximumTemperature), _phaseChartLabelStyle);
            GUI.Label(new Rect(chart.x + 8, chart.y + 3, 150, 24), "Pressure (kPa)", _phaseChartHeadingStyle!);
            GUI.Label(new Rect(plot.center.x - 100, chart.yMax - 28, 200, 24), _phaseCelsius ? "Temperature (°C)" : "Temperature (K)", _phaseChartHeadingStyle!);
            DrawPhaseBoundary(primary, plot, maximumTemperature, maximumPressure, Color.black, new Color(.12f, .2f, .9f));
            if (comparison != null) DrawPhaseBoundary(comparison, plot, maximumTemperature, maximumPressure, new Color(.85f, .3f, .05f), new Color(.12f, .2f, .9f, .5f));
            else DrawPhaseStateLabels(primary, plot, maximumTemperature, maximumPressure);
            if (onBoundary)
            {
                var point = PhasePlotPoint(temperature, pressure, plot, maximumTemperature, maximumPressure);
                var guide = new Color(.3f, .3f, .3f, .7f);
                DrawOrbitLine(new Vector2(point.x, plot.y), new Vector2(point.x, plot.yMax), guide, 1);
                DrawOrbitLine(new Vector2(plot.x, point.y), new Vector2(plot.xMax, point.y), guide, 1);
                var previous = GUI.color;
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(point.x - 3, point.y - 3, 6, 6), Texture2D.whiteTexture);
                GUI.color = previous;
            }
        }
        GUILayout.Label(onBoundary ? PhaseTemperature(temperature) + " · " + pressure.ToString("0.###", CultureInfo.InvariantCulture) + " kPa"
            : "Hover the liquid–gas boundary temperature range to read its pressure.", _mutedStyle!);
    }

    /// <summary>Maps native pressure samples to the same zero-based logarithmic convention as Stationpedia, with bounded coordinates.</summary>
    private Vector2 PhasePlotPoint(double temperature, double pressure, Rect plot, double maxTemperature, double maxPressure)
    {
        var x = Mathf.Clamp01((float)(temperature / maxTemperature));
        var y = Mathf.Clamp01((float)(_phaseLogScale ? Math.Log10(Math.Max(1, pressure)) / Math.Log10(maxPressure) : pressure / maxPressure));
        return new Vector2(plot.x + x * plot.width, plot.yMax - y * plot.height);
    }

    /// <summary>Places Solid, Liquid and Gas labels within the selected gas's reference regions, as in F1.</summary>
    private void DrawPhaseStateLabels(PhaseBoundary curve, Rect plot, double maxTemperature, double maxPressure)
    {
        var freeze = PhasePlotPoint(curve.Freeze, curve.Pressures[0], plot, maxTemperature, maxPressure);
        var critical = PhasePlotPoint(curve.MaximumTemperature, curve.MaximumPressure, plot, maxTemperature, maxPressure);
        var solidWidth = _phaseChartHeadingStyle!.CalcSize(new GUIContent("Solid")).x;
        var solidX = (plot.x + freeze.x) * .5f - 45;
        if (freeze.x - plot.x < solidWidth + 10) solidX = plot.x - 86;
        var solidY = Mathf.Clamp((plot.y + freeze.y) * .5f - 12, plot.y + 14, plot.yMax - 42);
        GUI.Label(new Rect(solidX, solidY, 90, 24), "Solid", _phaseChartHeadingStyle);
        GUI.Label(new Rect((freeze.x + critical.x) * .5f - 45, Mathf.Clamp(plot.y + (freeze.y - plot.y) * .25f - 12, plot.y + 14, plot.yMax - 42), 90, 24), "Liquid", _phaseChartHeadingStyle);
        GUI.Label(new Rect((critical.x + plot.xMax) * .5f - 45, Mathf.Clamp((freeze.y + plot.yMax) * .5f - 12, plot.y + 14, plot.yMax - 42), 90, 24), "Gas", _phaseChartHeadingStyle);
        // The freezing value gets its own lane, so near-zero temperatures cannot collide with the axis minimum.
        var tickX = Mathf.Clamp(freeze.x - 55, plot.x - 55, plot.xMax - 55);
        GUI.Label(new Rect(tickX, plot.yMax + 31, 110, 24), PhaseTemperature(curve.Freeze), _phaseChartLabelStyle!);
    }

    /// <summary>Draws the black phase boundary with its upper liquid limit and a blue freezing guide.</summary>
    private void DrawPhaseBoundary(PhaseBoundary curve, Rect plot, double maxTemperature, double maxPressure, Color boundaryColour, Color freezeColour)
    {
        for (var i = 1; i < curve.Temperatures.Length; i++)
            DrawOrbitLine(PhasePlotPoint(curve.Temperatures[i - 1], curve.Pressures[i - 1], plot, maxTemperature, maxPressure),
                PhasePlotPoint(curve.Temperatures[i], curve.Pressures[i], plot, maxTemperature, maxPressure), boundaryColour, 2);
        var freeze = PhasePlotPoint(curve.Freeze, curve.Pressures[0], plot, maxTemperature, maxPressure);
        var critical = PhasePlotPoint(curve.MaximumTemperature, curve.MaximumPressure, plot, maxTemperature, maxPressure);
        DrawOrbitLine(freeze, new Vector2(freeze.x, plot.y), freezeColour, 2);
        DrawOrbitLine(critical, new Vector2(critical.x, plot.y), boundaryColour, 2);
    }
}
