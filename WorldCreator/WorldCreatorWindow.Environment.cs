using System.Xml.Serialization;
using System.Text.RegularExpressions;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private sealed class GasOption
    {
        internal readonly string Id;
        internal readonly string Name;
        internal readonly bool IsLiquid;
        internal readonly bool IsKnown;
        /// <summary>Builds the display name and liquid-state label for a selectable gas type.</summary>
        internal GasOption(string id)
        {
            Id = id;
            IsLiquid = id.StartsWith("Liquid", StringComparison.Ordinal);
            IsKnown = true;
            var name = IsLiquid ? id.Substring("Liquid".Length) : id;
            Name = Regex.Replace(name, "([a-z])([A-Z])", "$1 $2") + (IsLiquid ? " (Liquid)" : "");
        }

        /// <summary>Labels imported gas identifiers that are not in the installed gas catalogue.</summary>
        internal GasOption(string id, bool isKnown) : this(id)
        {
            IsKnown = isKnown;
            if (!isKnown)
                Name = "Unknown gas type (" + id + ")";
        }
    }

    private static readonly GasOption[] Gases = Assets.Scripts.Atmospherics.Chemistry.Gases.Where(mole => (uint)mole.Type != 0 && (((uint)mole.Type & ((uint)mole.Type - 1u)) == 0)).Select(mole =>
    {
        var type = mole.Type;
        var enumName = Enum.GetName(typeof(Assets.Scripts.Atmospherics.Chemistry.GasType), type);
        var member = enumName == null ? null : typeof(Assets.Scripts.Atmospherics.Chemistry.GasType).GetField(enumName);
        var id = member?.GetCustomAttribute<XmlEnumAttribute>()?.Name;
        return string.IsNullOrEmpty(id) ? null : new GasOption(id);
    })
        .Where(gas => gas != null && !string.Equals(gas.Id, "Undefined", StringComparison.Ordinal))
        .GroupBy(gas => gas!.Id, StringComparer.Ordinal)
        .Select(group => group.First()!)
        .OrderBy(gas => gas.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    private static readonly Color[] GasColors =
    {
        new Color(.25f, .72f, .9f),
        new Color(.72f, .78f, .82f),
        new Color(.9f, .45f, .28f),
        new Color(.8f, .68f, .3f),
        new Color(.7f, .4f, .65f),
        new Color(.35f, .8f, .58f),
        new Color(.55f, .68f, .95f),
        new Color(.84f, .38f, .35f),
        new Color(.55f, .78f, .38f),
        new Color(.3f, .78f, .72f)
    };
    private int _gasSelection;
    private string _newGasQuantity = "1";
    private string? _pendingGasRemoval;
    private string _referenceTemperature = "273.15";
    /// <summary>Arranges gas selection, quantity input and the reference pressure estimate.</summary>
    private void DrawGasControls()
    {
        if (!_project!.HasTemperatureCurve && NativeButton("Add a constant temperature curve (273.15 K)"))
        {
            try
            {
                _project.AddTemperatureCurve();
                _status = "Added a temperature curve. Edit its key values below.";
            }
            catch (Exception e)
            {
                _status = e.Message;
            }
        }

        HelpLabel(
            new GUIContent(
            "Add gas  ⓘ",
            "Add a gas type with a quantity in moles per 8,000-litre atmosphere grid cell. Every gas type shares the environment temperature; the temperature curve controls the world atmosphere, while this amount changes gas composition."
        ),
            _mutedStyle!
        );
        var availableGases = Gases.Where(g => !_project.AtmosphereFields.Any(field => field.Label.Contains("[" + g.Id + "]"))).ToArray();
        GUILayout.BeginHorizontal();
        if (availableGases.Length == 0)
            GUILayout.Label("All listed gases are already added.", _mutedStyle!);
        else
        {
            DrawGasAdditionControls(availableGases);
        }

        GUILayout.EndHorizontal();
        GUILayout.Space(12);
        GUILayout.BeginHorizontal();
        HelpLabel(
            new GUIContent(
            "Pressure estimate at temperature (K)  ⓘ",
            "This reference temperature is used only for the ideal-gas pressure estimate shown here; it does not change the world's temperature curve."
        ),
            _mutedStyle!, GUILayout.Width(352), GUILayout.Height(32)
        );
        _referenceTemperature = TooltipTextField(
            _referenceTemperature,
            "Reference temperature for the ideal-gas estimate only. This field does not change world settings.",
            95,
            32,
            numeric: true
        );
        var pressureLabel = "- kPa";
        if (double.TryParse(_referenceTemperature, NumberStyles.Float, CultureInfo.InvariantCulture, out var temperature) && temperature > 0 && temperature <= float.MaxValue && !double.IsNaN(temperature) && !double.IsInfinity(temperature))
        {
            var total = 0d;
            var valid = true;
            foreach (var field in _project!.AtmosphereFields.Where(f => f.Label.Contains("/Gas/Gas") && f.Label.EndsWith(" / Quantity")))
            {
                var gas = GasForField(field);
                if (gas == null || !gas.IsKnown)
                {
                    valid = false;
                    break;
                }

                if (!double.TryParse(field.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var quantity) || quantity < 0 || quantity > float.MaxValue || double.IsNaN(quantity) || double.IsInfinity(quantity))
                {
                    valid = false;
                    break;
                }

                if (!gas.IsLiquid)
                    total += quantity;
            }

            if (valid)
                pressureLabel = "~" + (total * 8.314462618 * temperature / 8000).ToString("0.##", CultureInfo.InvariantCulture) + " kPa (gas-phase estimate)";
        }

        GUILayout.Space(10);
        GUILayout.Label(pressureLabel, GUILayout.Height(32));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Label("The ideal-gas estimate includes gases and excludes liquid entries. Actual pressure also depends on temperature, phase changes, and the game's atmosphere simulation.");
        DrawGasComposition();
    }

    /// <summary>Draws the normalized composition bar and identifies each gas type with a color swatch.</summary>
    private void DrawGasComposition()
    {
        var mixture = new List<(string Id, string Name, double Quantity)>();
        var gasFields = _project!.AtmosphereFields.Where(f => f.Label.Contains("/Gas/Gas") && f.Label.EndsWith(" / Quantity")).ToArray();
        foreach (var field in gasFields)
        {
            var gas = GasForField(field);
            if (gas == null || !double.TryParse(field.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var quantity) || quantity < 0 || quantity > float.MaxValue || double.IsNaN(quantity) || double.IsInfinity(quantity))
                continue;
            mixture.Add((gas.Id, gas.Name, quantity));
        }

        var total = mixture.Sum(g => g.Quantity);
        GUILayout.Label("Gas composition by quantity");
        if (total <= 0)
            GUILayout.Label("No gas quantity to display.");
        GUILayout.BeginHorizontal();
        var compositionWidth = Mathf.Min(600f, Mathf.Max(280f, _window.width * .28f));
        GUILayout.BeginVertical(GUILayout.Width(compositionWidth));
        var bar = GUILayoutUtility.GetRect(100, 14, GUILayout.ExpandWidth(true), GUILayout.Height(14));
        GUI.Box(bar, "");
        var x = bar.x + 2;
        var width = Math.Max(1, bar.width - 4);
        var previous = GUI.color;
        for (var i = 0; i < mixture.Count; i++)
        {
            var segment = total > 0 ? (float)(width * mixture[i].Quantity / total) : 0f;
            GUI.color = GasColor(mixture[i].Id);
            GUI.DrawTexture(new Rect(x, bar.y + 2, segment, bar.height - 4), Texture2D.whiteTexture);
            x += segment;
        }

        GUI.color = previous;
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(8);
        string? removeGas = null;
        foreach (var field in gasFields)
        {
            var gas = GasForField(field);
            if (gas == null)
                continue;
            GUILayout.BeginHorizontal(GUILayout.Height(36));
            var swatch = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12), GUILayout.Height(12));
            GUI.color = GasColor(gas.Id);
            GUI.DrawTexture(swatch, Texture2D.whiteTexture);
            GUI.color = previous;
            GUILayout.Space(6);
            var quantity = mixture.FirstOrDefault(item => item.Id == gas.Id).Quantity;
            var share = total > 0 ? (100 * quantity / total).ToString("0.0", CultureInfo.InvariantCulture) + "%  ·  " : "";
            GUILayout.Label(gas.Name, _mutedStyle!, GUILayout.Width(210), GUILayout.Height(32));
            GUILayout.Space(12);
            field.Value = MaskNumericInput(GUILayout.TextField(field.Value, _inputStyle!, GUILayout.Width(130), GUILayout.Height(32)));
            GUILayout.Space(8);
            GUILayout.Label("mol", _mutedStyle!, GUILayout.Width(35), GUILayout.Height(32));
            GUILayout.Space(12);
            if (NativeButton("Remove gas", _buttonStyle!, GUILayout.Width(140), GUILayout.Height(32)))
                removeGas = gas.Id;
            GUILayout.Space(12);
            GUILayout.Label(share, _mutedStyle!, GUILayout.Width(90), GUILayout.Height(32));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
        }

        if (removeGas != null)
        {
            var type = removeGas;
            _pendingGasRemoval = type;
        }
    }

    /// <summary>Matches an atmosphere field to a known gas type while preserving unknown imported identifiers.</summary>
    private static GasOption? GasForField(WorldProject.Field field)
    {
        var known = Gases.FirstOrDefault(gas => field.Label.Contains("[" + gas.Id + "]"));
        if (known != null)
            return known;
        var start = field.Label.LastIndexOf('[', field.Label.Length - 1);
        var end = field.Label.LastIndexOf(']');
        return start >= 0 && end > start ? new GasOption(field.Label.Substring(start + 1, end - start - 1), false) : null;
    }

    /// <summary>Returns the stable composition color assigned to a gas type.</summary>
    private static Color GasColor(string gas)
    {
        var index = Array.FindIndex(Gases, option => option.Id == gas);
        return index >= 0 && index < GasColors.Length ? GasColors[index] : Color.HSVToRGB((Mathf.Max(0, index) * .618034f) % 1f, .62f, .92f);
    }

    /// <summary>Edits the selected gas type and quantity, then validates an addition to the atmosphere.</summary>
    private void DrawGasAdditionControls(GasOption[] availableGases)
    {
        _gasSelection = Mathf.Clamp(_gasSelection, 0, availableGases.Length - 1);
        var gasLabels = availableGases.Select(gas => gas.Name).ToArray();
        _gasSelection = NativeDropdown("gas-type", _gasSelection, gasLabels, _buttonStyle!, GUILayout.Width(250));
        GUILayout.Space(10);
        GUILayout.Label("Amount (mol)", _mutedStyle!, GUILayout.Width(88));
        _newGasQuantity = TooltipTextField(
            _newGasQuantity,
            "Initial moles of this gas per 8,000-litre atmosphere grid cell. The atmosphere's temperature is set by the world temperature curve, not per gas.",
            95,
            32,
            numeric: true
        );
        GUILayout.Space(10);
        if (NativeButton("Add gas", _buttonStyle!, GUILayout.Width(110), GUILayout.Height(32)))
        {
            if (!double.TryParse(_newGasQuantity, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) || amount < 0 || double.IsNaN(amount) || double.IsInfinity(amount))
                _status = "Enter a valid non-negative gas amount in moles.";
            else
            {
                try
                {
                    _project!.AddGas(availableGases[_gasSelection].Id, amount);
                    _status = "Added " + availableGases[_gasSelection].Name + " at " + amount.ToString("0.##", CultureInfo.InvariantCulture) + " mol per cell.";
                }
                catch (Exception e)
                {
                    _status = e.Message;
                }
            }
        }
    }
}
