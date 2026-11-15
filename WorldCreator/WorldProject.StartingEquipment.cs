using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    /// <summary>Loads starting profiles and spawn definitions from the source mod's GameData XML files.</summary>
    private void ImportStartingEquipmentFiles(string worldFile)
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(worldFile)!);
        for (var parent = directory; parent != null; parent = parent.Parent)
            if (parent.Name.Equals("GameData", StringComparison.OrdinalIgnoreCase))
            {
                directory = parent;
                break;
            }

        foreach (var file in Directory.GetFiles(directory.FullName, "*.xml", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var root = XDocument.Load(file).Root;
                if (root != null)
                    AddImportedStartConditionRecords(root);
            }
            catch (Exception error)when (error is XmlException || error is IOException || error is UnauthorizedAccessException)
            { /* An unrelated or malformed optional XML file must not hide the world's equipment. */
            }
        }
    }

    /// <summary>Resolves all spawn roles into a read-only hierarchy without flattening or editing the source XML.</summary>
    internal IReadOnlyList<(string Text, int Depth)> GetStartingEquipment(string conditionId)
    {
        var lines = new List<(string Text, int Depth)>();
        var condition = FindStartCondition(conditionId);
        if (condition == null)
        {
            lines.Add(("Starting profile definition not found: " + conditionId, 0));
            return lines;
        }

        var names = ItemPrefabCatalog.Entries.GroupBy(item => item.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Name, StringComparer.Ordinal);
        foreach (var reference in condition.Elements("Spawn"))
        {
            lines.Add(("When: " + ((string? )reference.Attribute("Event") ?? "Unspecified event"), 0));
            AppendStartingEquipment(reference, 1, new HashSet<string>(StringComparer.Ordinal), names, lines);
        }

        if (lines.Count == 0)
            lines.Add(("No spawn references are defined for this profile.", 0));
        return lines;
    }

    /// <summary>Expands nested spawn references and container contents while reporting cycles and missing definitions.</summary>
    private void AppendStartingEquipment(XElement element, int depth, HashSet<string> branch, IReadOnlyDictionary<string, string> names, List<(string Text, int Depth)> lines)
    {
        if (depth > 48)
        {
            lines.Add(("Further nesting omitted (depth limit).", depth));
            return;
        }

        var kind = element.Name.LocalName;
        var id = (string? )element.Attribute("Id") ?? "";
        if (kind == "Spawn")
        {
            var filters = element.Elements("Species").Select(filter => (string? )filter.Attribute("Id")).Where(value => !string.IsNullOrEmpty(value));
            var filterText = string.Join(", ", filters);
            lines.Add((id + (filterText.Length > 0 ? " · for " + filterText : ""), depth));
            if (!branch.Add(id))
            {
                lines.Add(("Circular spawn reference: " + id, depth + 1));
                return;
            }

            var definition = _customSpawnDefinitions.FirstOrDefault(spawn => (string? )spawn.Attribute("Id") == id) ?? _startConditionLibrary.Elements("Spawn").FirstOrDefault(spawn => (string? )spawn.Attribute("Id") == id);
            if (definition == null)
                lines.Add(("Spawn definition not found: " + id + " (may require another mod)", depth + 1));
            else
                foreach (var child in definition.Elements())
                    AppendStartingEquipment(child, depth + 1, branch, names, lines);
            branch.Remove(id);
            return;
        }

        var visible = kind == "Item" || kind == "DynamicThing" || kind == "Slot";
        if (visible)
        {
            var name = names.TryGetValue(id, out var display) ? display : id;
            var quantity = (string? )element.Element("Quantity")?.Attribute("Value") ?? (string? )element.Attribute("Quantity") ?? "1";
            lines.Add(((kind == "Slot" ? "Slot: " : "") + name + (kind == "Slot" ? "" : " × " + quantity) + (name != id ? "  [" + id + "]" : ""), depth));
        }

        foreach (var child in element.Elements())
            if (child.Name.LocalName != "Quantity")
                AppendStartingEquipment(child, depth + (visible ? 1 : 0), branch, names, lines);
    }
}
