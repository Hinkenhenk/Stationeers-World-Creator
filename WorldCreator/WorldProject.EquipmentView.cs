using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    internal sealed class EquipmentViewNode
    {
        internal readonly XElement Element;
        internal string Name;
        internal readonly string Event;
        internal readonly bool IsPlayerKit;
        internal EquipmentViewNode? Parent;
        internal readonly List<EquipmentViewNode> Children;

        /// <summary>Connects one visible object to its original editable occurrence and visible children.</summary>
        internal EquipmentViewNode(EquipmentEntry row, string name, bool playerKit, List<EquipmentViewNode> children)
        {
            Element = row.Element;
            Name = name;
            Event = row.Event;
            IsPlayerKit = playerKit;
            Children = children;
            foreach (var child in children) child.Parent = this;
        }
    }

    /// <summary>Builds the player's equipment hierarchy without exposing reference wrappers or modifying their XML.</summary>
    internal IReadOnlyList<EquipmentViewNode> GetEquipmentView(string conditionId)
    {
        var rows = GetEquipmentEntries(conditionId);
        var cursor = 0;
        var roots = ReadEquipmentViewLevel(rows, ref cursor, 0);
        foreach (var root in roots) root.Parent = null;
        return roots.OrderBy(node => node.IsPlayerKit ? 0 : 1).ToArray();
    }

    /// <summary>Visits every visible object once, retaining occurrence identity for selection and breadcrumbs.</summary>
    internal static IEnumerable<EquipmentViewNode> FlattenEquipmentView(IEnumerable<EquipmentViewNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in FlattenEquipmentView(node.Children)) yield return child;
        }
    }

    /// <summary>Collapses a reference and its single container into one named object while retaining species kit roots.</summary>
    private List<EquipmentViewNode> ReadEquipmentViewLevel(IReadOnlyList<EquipmentEntry> rows, ref int cursor, int depth)
    {
        var result = new List<EquipmentViewNode>();
        while (cursor < rows.Count && rows[cursor].Depth == depth)
        {
            var row = rows[cursor++];
            var children = ReadEquipmentViewLevel(rows, ref cursor, depth + 1);
            if (row.Element.Name != "Spawn")
            {
                result.Add(new EquipmentViewNode(row, GetEquipmentName(row.Element), false, children));
                continue;
            }
            var species = string.Join(", ", row.Element.Elements("Species").Select(item => (string?)item.Attribute("Id")).Where(id => !string.IsNullOrWhiteSpace(id)));
            if (species.Length > 0 && (row.Event == "NewPlayerKit" || row.Event == "RespawnPlayerKit"))
            {
                result.Add(new EquipmentViewNode(row, species + (row.Event == "RespawnPlayerKit" ? " (respawn)" : ""), true, children));
                continue;
            }
            if (children.Count == 1 && !children[0].IsPlayerKit && children[0].Element.Name != "Spawn")
                ApplyEquipmentViewAlias(row, children[0]);
            if (children.Count == 0 && FindEquipmentSpawn((string?)row.Element.Attribute("Id") ?? "") == null)
                result.Add(new EquipmentViewNode(row, "Missing equipment: " + row.Label, false, children));
            else result.AddRange(children);
        }
        return result;
    }

    /// <summary>Uses a supply kit's name for its physical container, preserving custom object labels and lander names.</summary>
    private void ApplyEquipmentViewAlias(EquipmentEntry wrapper, EquipmentViewNode item)
    {
        var id = (string?)item.Element.Attribute("Id") ?? "";
        var key = (string?)item.Element.Element("Name")?.Attribute("Key") ?? "";
        if (id.IndexOf("Lander", StringComparison.OrdinalIgnoreCase) >= 0 || key.StartsWith("WCEquipmentLabel_", StringComparison.Ordinal)
            || !string.IsNullOrWhiteSpace((string?)item.Element.Element("Name")?.Attribute("Value"))) return;
        var spawnId = (string?)wrapper.Element.Attribute("Id") ?? "";
        item.Name = _spawnNames.TryGetValue(spawnId, out var name) ? name : spawnId;
    }
}
