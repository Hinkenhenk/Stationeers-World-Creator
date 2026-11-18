using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    internal sealed class EquipmentKit
    {
        internal readonly string Id;
        internal readonly string Name;
        internal readonly string PrefabId;

        /// <summary>Identifies an installed supply kit and the physical object occupying its slot.</summary>
        internal EquipmentKit(string id, string name, string prefabId) { Id = id; Name = name; PrefabId = prefabId; }
    }

    /// <summary>Lists single-object installed kits whose wrapper has no actions or conditions that insertion would discard.</summary>
    internal IReadOnlyList<EquipmentKit> EquipmentKitOptions => _startConditionLibrary.Elements("Spawn")
        .Select(ReadEquipmentKit).Where(kit => kit != null).Cast<EquipmentKit>()
        .OrderBy(kit => kit.Name, StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>Rejects multi-object spawns and behavioural wrappers rather than silently losing their semantics.</summary>
    private EquipmentKit? ReadEquipmentKit(XElement definition)
    {
        var id = (string?)definition.Attribute("Id") ?? "";
        var roots = definition.Elements().Where(element => element.Name == "Item" || element.Name == "DynamicThing").ToArray();
        if (id.Length == 0 || id.StartsWith("WCEquipment_", StringComparison.Ordinal) || roots.Length != 1) return null;
        if (definition.Elements().Any(element => element != roots[0] && element.Name != "Name" && element.Name != "Description")) return null;
        if (definition.Attributes().Any(attribute => attribute.Name != "Id" && attribute.Name != "ShowInSpawnMenu" && attribute.Name != "HideInStartScreen" && attribute.Name != "StartScreenHeader")) return null;
        var prefab = (string?)roots[0].Attribute("Id");
        if (string.IsNullOrWhiteSpace(prefab)) return null;
        var name = _spawnNames.TryGetValue(id, out var display) ? display : id;
        return new EquipmentKit(id, name, prefab!);
    }

    /// <summary>Copies a kit into one verified empty slot, preserving its actions and isolating every nested spawn occurrence.</summary>
    internal XElement AddEquipmentKit(string conditionId, XElement container, string kitId, int slotIndex,
        Func<string, bool> validateSlot, IReadOnlyList<(XElement Item, int Slot)> assignments)
    {
        RequireEditableEquipment(conditionId, container);
        var definition = _startConditionLibrary.Elements("Spawn").FirstOrDefault(element => (string?)element.Attribute("Id") == kitId);
        var kit = definition == null ? null : ReadEquipmentKit(definition);
        if (kit == null || slotIndex < 0 || !validateSlot(kit.PrefabId))
            throw new InvalidOperationException("Choose a supply kit that fits an empty, unlocked slot.");
        var contents = GetEquipmentContents(container);
        if (assignments.Count != contents.Count || assignments.Any(assignment => assignment.Slot < 0 || assignment.Slot == slotIndex || !contents.Contains(assignment.Item))
            || assignments.Select(assignment => assignment.Item).Distinct().Count() != assignments.Count || assignments.Select(assignment => assignment.Slot).Distinct().Count() != assignments.Count)
            throw new InvalidOperationException("The container contents changed. Select an empty slot again.");
        var copy = new XElement(definition!.Elements().Single(element => element.Name == "Item" || element.Name == "DynamicThing"));
        var pending = new List<XElement>();
        try
        {
            foreach (var reference in copy.Descendants("Spawn").Where(element => !element.Ancestors("Spawn").Any()).ToArray())
                CopyEquipmentSpawn(reference, pending, new HashSet<string>(StringComparer.Ordinal), 0);
        }
        catch
        {
            foreach (var spawn in pending) _spawnNames.Remove((string)spawn.Attribute("Id")!);
            throw;
        }
        foreach (var assignment in assignments)
        {
            assignment.Item.SetAttributeValue("SlotIndex", assignment.Slot);
            assignment.Item.SetAttributeValue("SlotId", null);
            assignment.Item.SetAttributeValue("SlotClass", null);
        }
        copy.SetAttributeValue("SlotIndex", slotIndex);
        copy.SetAttributeValue("SlotId", null);
        copy.SetAttributeValue("SlotClass", null);
        var target = container.Name == "Spawn" ? FindEquipmentSpawn((string)container.Attribute("Id")!)! : container;
        target.Add(copy);
        _customSpawnDefinitions.AddRange(pending);
        SetEquipmentName(conditionId, copy, kit.Name);
        _structureChanged = true;
        return copy;
    }
}
