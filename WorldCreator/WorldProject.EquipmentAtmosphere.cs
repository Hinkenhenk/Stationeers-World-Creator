using System;
using System.Globalization;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    /// <summary>Reads a native Gas action in moles and kelvin, including liquid litres and energy-based imports.</summary>
    internal static (double? Moles, double? Kelvin) ReadEquipmentGas(XElement gas, double molarVolume, double specificHeat)
    {
        var moles = EquipmentGasNumber(gas, "Moles");
        if (double.IsNaN(moles))
        {
            var litres = EquipmentGasNumber(gas, "Litres");
            moles = molarVolume > 0 ? litres / molarVolume : double.NaN;
        }
        var celsius = EquipmentGasNumber(gas, "Celsius");
        var kelvin = double.IsNaN(celsius) ? EquipmentGasNumber(gas, "Kelvin") : celsius + 273.15;
        var energy = EquipmentGasNumber(gas, "Energy");
        if (!double.IsNaN(energy)) kelvin = moles > 0 && specificHeat > 0 ? energy / (moles * specificHeat) : double.NaN;
        return (ValidEquipmentGasNumber(moles) ? moles : (double?)null, ValidEquipmentGasNumber(kelvin) ? kelvin : (double?)null);
    }

    /// <summary>Adds or updates one owned gas action after checking native type support and finite 32-bit input values.</summary>
    internal void SetEquipmentGas(string conditionId, XElement item, XElement? gas, string type, double moles, double kelvin, Func<bool> validateNative)
    {
        RequireEditableEquipment(conditionId, item);
        if ((item.Name != "Item" && item.Name != "DynamicThing") || (gas != null && (gas.Parent != item || gas.Name != "Gas")))
            throw new InvalidOperationException("Select a gas or liquid entry belonging to this equipment item.");
        if (string.IsNullOrWhiteSpace(type) || !validateNative()) throw new InvalidOperationException("Choose an installed gas or liquid type supported by this item.");
        if (!ValidEquipmentGasNumber(moles) || !ValidEquipmentGasNumber(kelvin))
            throw new InvalidOperationException("Amount and temperature must be finite numbers of zero or more within the game's 32-bit number range.");
        var action = gas ?? new XElement("Gas");
        action.SetAttributeValue("Type", type);
        action.SetAttributeValue("Moles", ((float)moles).ToString("R", CultureInfo.InvariantCulture));
        action.SetAttributeValue("Kelvin", ((float)kelvin).ToString("R", CultureInfo.InvariantCulture));
        action.SetAttributeValue("Litres", null);
        action.SetAttributeValue("Celsius", null);
        action.SetAttributeValue("Energy", null);
        if (gas == null) item.Add(action);
        _structureChanged = true;
    }

    /// <summary>Removes only the chosen gas action while retaining the item's other contents and metadata.</summary>
    internal void RemoveEquipmentGas(string conditionId, XElement item, XElement gas)
    {
        RequireEditableEquipment(conditionId, item);
        if (gas.Parent != item || gas.Name != "Gas") throw new InvalidOperationException("This contents entry no longer belongs to the selected item.");
        gas.Remove();
        _structureChanged = true;
    }

    /// <summary>Uses the native float attribute range without interpreting missing or invalid source text as zero.</summary>
    private static double EquipmentGasNumber(XElement gas, string attribute) => float.TryParse((string?)gas.Attribute(attribute), NumberStyles.Float,
        CultureInfo.InvariantCulture, out var value) ? value : double.NaN;

    /// <summary>Rejects negative, non-finite and unrepresentable quantities before writing a Gas action.</summary>
    private static bool ValidEquipmentGasNumber(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0 && value <= float.MaxValue;
}
