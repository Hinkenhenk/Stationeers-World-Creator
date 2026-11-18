using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    /// <summary>Changes one owned item's quantity within its installed prefab's stack limit.</summary>
    internal void SetEquipmentQuantity(string conditionId, XElement item, int quantity, int maximum)
    {
        RequireEditableEquipment(conditionId, item);
        if (item.Name == "Spawn" || maximum < 1 || quantity < 1 || quantity > maximum)
            throw new InvalidOperationException("Quantity must be between 1 and " + maximum + " for this item.");
        var element = item.Element("Quantity");
        if (element == null)
        {
            element = new XElement("Quantity");
            item.Add(element);
        }
        element.SetAttributeValue("Value", quantity.ToString(CultureInfo.InvariantCulture));
        _structureChanged = true;
    }

    /// <summary>Returns a private container label or the readable catalog name without changing the XML.</summary>
    internal string GetEquipmentName(XElement item)
    {
        var literal = (string?)item.Element("Name")?.Attribute("Value");
        if (!string.IsNullOrWhiteSpace(literal)) return literal!;
        var key = (string?)item.Element("Name")?.Attribute("Key") ?? "";
        if (key.EndsWith("Name", StringComparison.Ordinal) && _spawnNames.TryGetValue(key.Substring(0, key.Length - 4), out var name))
            return name;
        var id = (string?)item.Attribute("Id") ?? "";
        return ItemPrefabCatalog.Entries.FirstOrDefault(entry => entry.Id == id)?.Name ?? id;
    }

    /// <summary>Renames one physical container using a private localization key, preserving its contents and metadata.</summary>
    internal void SetEquipmentName(string conditionId, XElement container, string name)
    {
        RequireEditableEquipment(conditionId, container);
        if (container.Name == "Spawn" || string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Enter a name for the container.");
        if (GetEquipmentName(container) == name.Trim())
            return;
        var label = container.Element("Name");
        if (label == null)
        {
            label = new XElement("Name");
            container.Add(label);
        }
        // Each occurrence gets its own key even when the source used a shared localized name.
        var key = "WCEquipmentLabel_" + Guid.NewGuid().ToString("N");
        label.SetAttributeValue("Key", key + "Name");
        _spawnNames[key] = name.Trim();
        _structureChanged = true;
    }
}
