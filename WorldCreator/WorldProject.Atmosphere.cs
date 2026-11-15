using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    /// <summary>Adds a previously absent gas type and binds its quantity to the atmosphere editor.</summary>
    internal void AddGas(string type, double quantity)
    {
        if (double.IsNaN(quantity) || double.IsInfinity(quantity) || quantity < 0 || quantity > float.MaxValue)
            throw new InvalidOperationException("Gas quantity must be non-negative and fit the game's 32-bit number range.");
        var atmosphere = _world.Element("GlobalAtmosphere") ?? throw new InvalidOperationException("The template has no global atmosphere definition.");
        var gas = atmosphere.Element("Gas");
        if (gas == null)
        {
            gas = new XElement("Gas");
            atmosphere.Add(gas);
        }

        if (gas.Elements("Gas").Any(e => (string? )e.Attribute("Type") == type))
            throw new InvalidOperationException(type + " is already in the atmosphere.");
        var entry = new XElement("Gas", new XAttribute("Type", type), new XAttribute("Quantity", quantity.ToString("0.########", CultureInfo.InvariantCulture)));
        gas.Add(entry);
        AddEditableTree(AtmosphereFields, entry, "Atmosphere/GlobalAtmosphere/Gas");
        _structureChanged = true;
    }

    /// <summary>Removes the chosen gas type and its matching editable fields from the world draft.</summary>
    internal void RemoveGas(string type)
    {
        var gas = _world.Element("GlobalAtmosphere")?.Element("Gas");
        var entry = gas?.Elements("Gas").FirstOrDefault(element => (string? )element.Attribute("Type") == type) ?? throw new InvalidOperationException(type + " is not in the atmosphere.");
        var entryPath = "World/World/GlobalAtmosphere/Gas/Gas" + (gas!.Elements("Gas").Count() > 1 ? " #" + (entry.ElementsBeforeSelf("Gas").Count() + 1).ToString(CultureInfo.InvariantCulture) : "");
        var remainingAdvanced = AdvancedFields.Where(field => field.Label.StartsWith("World/World/GlobalAtmosphere/Gas/", StringComparison.Ordinal) && !field.Label.StartsWith(entryPath + "/", StringComparison.Ordinal))
            .ToArray();
        var remaining = AtmosphereFields.Where(field => field.Label.StartsWith("Atmosphere/GlobalAtmosphere/Gas/", StringComparison.Ordinal) && !field.Label.Contains("[" + type + "]"))
            .ToArray();
        // Commit retained drafts before rebuilding the gas paths, whose numbered indices can change.
        foreach (var field in remainingAdvanced.Concat(remaining))
            field.Validate();
        foreach (var field in remainingAdvanced.Concat(remaining))
            field.Apply();
        entry.Remove();
        AdvancedFields.RemoveAll(field => field.Label.StartsWith("World/World/GlobalAtmosphere/Gas/", StringComparison.Ordinal));
        AtmosphereFields.RemoveAll(field => field.Label.StartsWith("Atmosphere/GlobalAtmosphere/Gas/", StringComparison.Ordinal));
        foreach (var retained in gas.Elements("Gas"))
        {
            AddAllFields(AdvancedFields, retained, "World/World/GlobalAtmosphere/Gas");
            AddEditableTree(AtmosphereFields, retained, "Atmosphere/GlobalAtmosphere/Gas");
        }

        _structureChanged = true;
    }

    /// <summary>Returns atmosphere XML with pending field values applied, leaving the live draft unchanged.</summary>
    internal XElement CreateAtmosphereSnapshot()
    {
        foreach (var field in AtmosphereFields)
            field.Validate();
        // Include reverted drafts so a previous preview cannot leave stale XML values behind.
        foreach (var field in AtmosphereFields)
            field.Apply(includeUnchanged: true);
        return new XElement(_world.Element("GlobalAtmosphere") ?? throw new InvalidOperationException("Add an atmosphere before previewing its temperature."));
    }

    /// <summary>Binds atmospheric curves and surface/deep resource quantities after the source world is loaded.</summary>
    private void InitializeAtmosphereAndResourceFields()
    {
        var atmosphere = _world.Element("GlobalAtmosphere");
        if (atmosphere != null)
            AddEditableTree(AtmosphereFields, atmosphere, "Atmosphere");
        foreach (var region in _world.Elements("Minables"))
            AddEditableAttributes(ResourceFields, region, "Surface resources [" + ((string? )region.Attribute("Id") ?? "unnamed") + "]");
        foreach (var region in _world.Elements("DeepMinables"))
            AddEditableAttributes(ResourceFields, region, "Deep resources [" + ((string? )region.Attribute("Id") ?? "unnamed") + "]");
    }
}
