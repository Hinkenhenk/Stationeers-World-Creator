using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using System.Xml.Serialization;
using Assets.Scripts.Atmospherics;
using Assets.Scripts.Objects.Items;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private sealed class EquipmentGasDraft
    {
        internal string Moles;
        internal string Kelvin;
        /// <summary>Keeps an imported action's editable values without rewriting its source attributes while browsing.</summary>
        internal EquipmentGasDraft(double? moles, double? kelvin)
        {
            Moles = moles?.ToString("G7", CultureInfo.InvariantCulture) ?? "";
            Kelvin = kelvin?.ToString("G7", CultureInfo.InvariantCulture) ?? "";
        }
    }

    private readonly Dictionary<XElement, EquipmentGasDraft> _equipmentGasDrafts = new Dictionary<XElement, EquipmentGasDraft>();
    private XElement? _equipmentAtmosphereItem;
    private int _equipmentGasSelection;
    private string _equipmentNewMoles = "1";
    private string _equipmentNewKelvin = "293.15";
    private static readonly Dictionary<string, Chemistry.GasType> EquipmentGasTypes = Enum.GetValues(typeof(Chemistry.GasType)).Cast<Chemistry.GasType>()
        .Where(type => type != Chemistry.GasType.Undefined).ToDictionary(type => typeof(Chemistry.GasType).GetField(type.ToString())?.GetCustomAttribute<XmlEnumAttribute>()?.Name ?? type.ToString(), type => type, StringComparer.Ordinal);

    /// <summary>Recognizes atmospheric equipment from native interfaces or existing imported gas actions, including empty tanks.</summary>
    private static bool EquipmentHasAtmosphere(XElement item) => item.Name != "Spawn" && (item.Elements("Gas").Any()
        || EquipmentPrefabRules.Find((string?)item.Attribute("Id") ?? "") is IInternalAtmosphere);

    /// <summary>Shows the selected tank's starting gas or fluid amounts and temperatures without needing a running world.</summary>
    private void DrawEquipmentAtmosphere(string conditionId, XElement selected)
    {
        if (!EquipmentHasAtmosphere(selected)) return;
        if (_equipmentAtmosphereItem != selected)
        {
            _equipmentAtmosphereItem = selected;
            _equipmentGasDrafts.Clear();
            _equipmentNewMoles = "1";
            _equipmentNewKelvin = "293.15";
        }
        GUILayout.Space(12);
        GUILayout.BeginVertical(_cardStyle!);
        GUILayout.Label("GAS / FLUID CONTENTS", _sectionStyle!);
        GUILayout.Label("Starting amounts and temperatures. Different entries mix when the equipment spawns; pressure, temperature and phase may then change.", _mutedStyle!);
        var enabled = GUI.enabled;
        GUI.enabled = enabled && _project!.CanEditEquipment(conditionId);
        foreach (var gas in selected.Elements("Gas").ToArray()) DrawEquipmentGasRow(conditionId, selected, gas);
        if (!selected.Elements("Gas").Any()) GUILayout.Label("No starting gas or liquid contents.", _mutedStyle!);
        DrawEquipmentGasAddition(conditionId, selected);
        GUI.enabled = enabled;
        GUILayout.EndVertical();
        GUILayout.Space(12);
    }

    /// <summary>Edits one occurrence independently, retaining litres, Celsius or energy XML until the player explicitly applies it.</summary>
    private void DrawEquipmentGasRow(string conditionId, XElement item, XElement gas)
    {
        var typeId = (string?)gas.Attribute("Type") ?? "Unknown";
        if (!_equipmentGasDrafts.TryGetValue(gas, out var draft))
        {
            var known = EquipmentGasTypes.TryGetValue(typeId, out var type);
            var values = WorldProject.ReadEquipmentGas(gas, known ? Mole.MolarVolume(type).ToDouble() : 0, known ? Mole.SpecificHeat(type).ToDouble() : 0);
            _equipmentGasDrafts.Add(gas, draft = new EquipmentGasDraft(values.Moles, values.Kelvin));
        }
        GUILayout.Space(10);
        GUILayout.Label(Gases.FirstOrDefault(option => option.Id == typeId)?.Name ?? HumanizeXmlName(typeId), _mutedStyle!);
        GUILayout.BeginHorizontal();
        DrawEquipmentGasInput("Amount (mol)", ref draft.Moles);
        GUILayout.Space(12);
        DrawEquipmentGasInput("Temperature (K)", ref draft.Kelvin);
        GUILayout.Space(12);
        GUILayout.BeginVertical();
        GUILayout.Space(22);
        if (NativeButton("Apply", _buttonStyle!, GUILayout.Width(90), GUILayout.Height(32))) QueueEquipmentGasChange(conditionId, item, gas, typeId, draft.Moles, draft.Kelvin);
        GUILayout.EndVertical();
        GUILayout.Space(12);
        GUILayout.BeginVertical();
        GUILayout.Space(22);
        if (NativeButton("Remove", _buttonStyle!, GUILayout.Width(100), GUILayout.Height(32)))
            _pendingEquipmentChange = () => { _project!.RemoveEquipmentGas(conditionId, item, gas); _equipmentGasDrafts.Remove(gas); };
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        if (draft.Moles.Length == 0 || draft.Kelvin.Length == 0)
            GUILayout.Label("The imported action has no directly calculable amount or temperature. It is preserved until Apply; enter both values to replace it.", _mutedStyle!);
    }

    /// <summary>Provides the same installed gas-type dropdown as Environment, with explicit quantity and temperature.</summary>
    private void DrawEquipmentGasAddition(string conditionId, XElement item)
    {
        if (Gases.Length == 0) return;
        GUILayout.Space(16);
        GUILayout.Label("Add gas / fluid", _mutedStyle!);
        _equipmentGasSelection = NativeDropdown("equipment-gas-type", Mathf.Clamp(_equipmentGasSelection, 0, Gases.Length - 1), Gases.Select(gas => gas.Name).ToArray(),
            _buttonStyle!, GUILayout.Width(290), GUILayout.Height(32));
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        DrawEquipmentGasInput("Amount (mol)", ref _equipmentNewMoles);
        GUILayout.Space(12);
        DrawEquipmentGasInput("Temperature (K)", ref _equipmentNewKelvin);
        GUILayout.Space(12);
        GUILayout.BeginVertical();
        GUILayout.Space(22);
        if (NativeButton("Add", _buttonStyle!, GUILayout.Width(90), GUILayout.Height(32)))
            QueueEquipmentGasChange(conditionId, item, null, Gases[_equipmentGasSelection].Id, _equipmentNewMoles, _equipmentNewKelvin);
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    /// <summary>Keeps a labeled amount or temperature field compact and aligned with its Apply action.</summary>
    private void DrawEquipmentGasInput(string label, ref string value)
    {
        GUILayout.BeginVertical(GUILayout.Width(150));
        GUILayout.Label(label, _mutedStyle!);
        value = GUILayout.TextField(value, _inputStyle!, GUILayout.Height(32));
        GUILayout.EndVertical();
    }

    /// <summary>Parses complete values and defers validated XML edits until the next layout pass.</summary>
    private void QueueEquipmentGasChange(string conditionId, XElement item, XElement? gas, string typeId, string molesText, string kelvinText)
    {
        if (!double.TryParse(molesText, NumberStyles.Float, CultureInfo.InvariantCulture, out var moles)
            || !double.TryParse(kelvinText, NumberStyles.Float, CultureInfo.InvariantCulture, out var kelvin))
        {
            _status = "Enter an amount in moles and a temperature in kelvin using decimal points.";
            return;
        }
        _pendingEquipmentChange = () =>
        {
            _project!.SetEquipmentGas(conditionId, item, gas, typeId, moles, kelvin, () => EquipmentGasTypes.ContainsKey(typeId) && EquipmentHasAtmosphere(item));
            if (gas != null) _equipmentGasDrafts.Remove(gas);
            _status = "Updated starting gas / fluid contents. Export the world to save these settings.";
        };
    }
}
