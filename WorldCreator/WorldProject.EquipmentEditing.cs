using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    private readonly HashSet<string> _editableEquipmentProfiles = new HashSet<string>(StringComparer.Ordinal);
    internal sealed class EquipmentEntry
    {
        internal readonly XElement Element;
        internal readonly int Depth;
        internal readonly string Event;
        internal readonly string Label;
        /// <summary>Identifies one occurrence in the equipment hierarchy, rather than a shared prefab ID.</summary>
        internal EquipmentEntry(XElement element, int depth, string eventName, string label)
        {
            Element = element;
            Depth = depth;
            Event = eventName;
            Label = label;
        }
    }

    /// <summary>Reports whether this profile's equipment has been isolated from installed and shared definitions.</summary>
    internal bool CanEditEquipment(string conditionId) => _editableEquipmentProfiles.Contains(conditionId);
    /// <summary>Copies every referenced spawn occurrence before editing, preserving filters, actions and container settings.</summary>
    internal string EditStartingEquipment(string conditionId)
    {
        if (CanEditEquipment(conditionId))
            return conditionId;
        var reference = _world.Elements("StartCondition").FirstOrDefault(e => (string? )e.Attribute("Id") == conditionId) ?? throw new InvalidOperationException("Starting profile is no longer assigned to this world.");
        var source = FindStartCondition(conditionId) ?? throw new InvalidOperationException("Starting profile definition is missing.");
        var pending = new List<XElement>();
        var copy = new XElement(source);
        foreach (var spawn in copy.Elements("Spawn"))
            CopyEquipmentSpawn(spawn, pending, new HashSet<string>(StringComparer.Ordinal), 0);
        var name = _startConditionNames.TryGetValue(conditionId, out var display) ? display : conditionId;
        copy = CreateStartConditionCopy(copy, name);
        var id = (string)copy.Attribute("Id")!;
        _customStartConditions.Add(copy);
        _customSpawnDefinitions.AddRange(pending);
        reference.SetAttributeValue("Id", id);
        if (!_world.Elements("StartCondition").Any(e => (string? )e.Attribute("Id") == conditionId))
            _customStartConditions.RemoveAll(e => (string? )e.Attribute("Id") == conditionId);
        _editableEquipmentProfiles.Add(id);
        _structureChanged = true;
        return id;
    }

    /// <summary>Creates a distinct definition for each use of a spawn so editing one crate cannot change another.</summary>
    private void CopyEquipmentSpawn(XElement reference, List<XElement> pending, HashSet<string> branch, int depth)
    {
        if (depth > 48)
            throw new InvalidOperationException("Equipment nesting exceeds the safe editing limit.");
        var sourceId = (string? )reference.Attribute("Id") ?? "";
        if (!branch.Add(sourceId))
            throw new InvalidOperationException("Cannot edit a circular spawn reference: " + sourceId);
        var source = FindEquipmentSpawn(sourceId);
        // Inline spawn definitions already belong to this occurrence.
        if (reference.Elements().Any(e => e.Name == "Item" || e.Name == "DynamicThing" || e.Name == "Spawn"))
            source = reference;
        if (source == null)
            throw new InvalidOperationException("Install the mod defining spawn '" + sourceId + "' before editing this profile.");
        var definition = new XElement(source);
        var id = "WCEquipment_" + Guid.NewGuid().ToString("N");
        definition.SetAttributeValue("Id", id);
        foreach (var child in definition.Descendants("Spawn").Where(e => !e.Ancestors("Spawn").Any(a => a != definition)).ToArray())
            CopyEquipmentSpawn(child, pending, branch, depth + 1);
        if (source == reference)
            reference.Elements().Where(e => e.Name == "Item" || e.Name == "DynamicThing" || e.Name == "Spawn").Remove();
        reference.SetAttributeValue("Id", id);
        pending.Add(definition);
        _spawnNames[id] = _spawnNames.TryGetValue(sourceId, out var name) ? name : sourceId;
        branch.Remove(sourceId);
    }

    /// <summary>Resolves a spawn against the current project before the installed library.</summary>
    private XElement? FindEquipmentSpawn(string id) => _customSpawnDefinitions.FirstOrDefault(e => (string? )e.Attribute("Id") == id) ?? _startConditionLibrary.Elements("Spawn").FirstOrDefault(e => (string? )e.Attribute("Id") == id);
    /// <summary>Builds occurrence-specific rows for editable equipment without exposing mutable game prefabs.</summary>
    internal IReadOnlyList<EquipmentEntry> GetEquipmentEntries(string conditionId)
    {
        var result = new List<EquipmentEntry>();
        var condition = FindStartCondition(conditionId);
        if (condition == null)
            return result;
        foreach (var spawn in condition.Elements("Spawn"))
            AppendEquipmentEntries(spawn, 0, (string? )spawn.Attribute("Event") ?? "", result, new HashSet<string>());
        return result;
    }

    /// <summary>Visits actual item occurrences and their private spawn references with cycle protection.</summary>
    private void AppendEquipmentEntries(XElement element, int depth, string eventName, List<EquipmentEntry> result, HashSet<string> branch)
    {
        if (depth > 48)
            return;
        var id = (string? )element.Attribute("Id") ?? "";
        if (element.Name == "Spawn")
        {
            var name = _spawnNames.TryGetValue(id, out var display) ? display : id;
            var filters = string.Join(", ", element.Elements("Species").Select(e => (string? )e.Attribute("Id")));
            result.Add(new EquipmentEntry(element, depth, eventName, name + (filters.Length > 0 ? " · for " + filters : "")));
            if (!branch.Add(id))
                return;
            var definition = FindEquipmentSpawn(id);
            if (definition != null)
                foreach (var child in definition.Elements().Where(IsEquipmentElement))
                    AppendEquipmentEntries(child, depth + 1, eventName, result, branch);
            branch.Remove(id);
            return;
        }

        var itemName = GetEquipmentName(element);
        var slot = (string? )element.Attribute("SlotId") ?? (string? )element.Attribute("SlotIndex");
        var quantity = (string? )element.Element("Quantity")?.Attribute("Value") ?? "1";
        result.Add(new EquipmentEntry(element, depth, eventName, itemName + " × " + quantity + (slot != null ? " · slot " + slot : "")));
        foreach (var child in element.Elements().Where(IsEquipmentElement))
            AppendEquipmentEntries(child, depth + 1, eventName, result, branch);
    }

    /// <summary>Recognizes XML elements which spawn objects or reference a loadout.</summary>
    private static bool IsEquipmentElement(XElement element) => element.Name == "Item" || element.Name == "DynamicThing" || element.Name == "Spawn";
    /// <summary>Returns the immediate contents in the same item/dynamic/spawn order used by the game.</summary>
    internal IReadOnlyList<XElement> GetEquipmentContents(XElement container)
    {
        var result = new List<XElement>();
        AppendEquipmentContents(container, result, new HashSet<string>());
        return result;
    }

    /// <summary>Expands references at this container level without descending into its items.</summary>
    private void AppendEquipmentContents(XElement container, List<XElement> result, HashSet<string> branch)
    {
        if (container.Name == "Spawn")
        {
            var id = (string? )container.Attribute("Id") ?? "";
            if (!branch.Add(id))
                throw new InvalidOperationException("Circular equipment reference.");
            var definition = FindEquipmentSpawn(id) ?? throw new InvalidOperationException("Equipment definition is missing: " + id);
            result.AddRange(definition.Elements("Item"));
            result.AddRange(definition.Elements("DynamicThing"));
            foreach (var spawn in definition.Elements("Spawn"))
                AppendEquipmentContents(spawn, result, branch);
            branch.Remove(id);
            return;
        }

        result.AddRange(container.Elements("Item"));
        result.AddRange(container.Elements("DynamicThing"));
        foreach (var spawn in container.Elements("Spawn"))
            AppendEquipmentContents(spawn, result, branch);
    }

    /// <summary>Checks that a control still points to equipment owned by this editable profile.</summary>
    private void RequireEditableEquipment(string conditionId, XElement element)
    {
        if (!CanEditEquipment(conditionId) || !GetEquipmentEntries(conditionId).Any(row => row.Element == element))
            throw new InvalidOperationException("Equipment selection changed. Select the container again.");
    }

    /// <summary>Removes exactly one occurrence, including its nested contents, from the project copy.</summary>
    internal void RemoveEquipment(string conditionId, XElement element)
    {
        RequireEditableEquipment(conditionId, element);
        element.Remove();
        _structureChanged = true;
    }

    /// <summary>Adds a validated item into an explicit slot; the caller supplies the game's current compatibility check.</summary>
    internal void AddEquipmentItem(
        string conditionId,
        XElement container,
        string itemId,
        int quantity,
        int slotIndex,
        Func<bool> validateSlot,
        IReadOnlyList<(XElement Item, int Slot)> assignments,
        bool dynamicThing = false
    )
    {
        RequireEditableEquipment(conditionId, container);
        if (string.IsNullOrWhiteSpace(itemId) || quantity < 1 || slotIndex < 0 || !validateSlot())
            throw new InvalidOperationException("Choose a valid item, quantity and empty compatible slot.");
        // Pin existing auto-selected slots before adding an explicit item earlier in XML execution order.
        foreach (var assignment in assignments)
        {
            assignment.Item.SetAttributeValue("SlotIndex", assignment.Slot);
            assignment.Item.SetAttributeValue("SlotId", null);
            assignment.Item.SetAttributeValue("SlotClass", null);
        }

        var target = container.Name == "Spawn" ? FindEquipmentSpawn((string)container.Attribute("Id")!)! : container;
        target.Add(new XElement(
            dynamicThing ? "DynamicThing" : "Item",
            new XAttribute("Id", itemId),
            new XAttribute("SlotIndex", slotIndex),
            new XElement("Quantity", new XAttribute("Value", quantity.ToString(CultureInfo.InvariantCulture)))
        ));
        _structureChanged = true;
    }

    /// <summary>Adds another independent lander to the world-created event after native prefab validation.</summary>
    internal void AddEquipmentLander(string conditionId, string prefabId, Func<bool> validatePrefab)
    {
        var condition = FindEditableStartCondition(conditionId);
        if (!CanEditEquipment(conditionId) || string.IsNullOrWhiteSpace(prefabId) || !validatePrefab())
            throw new InvalidOperationException("Choose a valid lander prefab.");
        var id = "WCEquipment_" + Guid.NewGuid().ToString("N");
        _customSpawnDefinitions.Add(new XElement(
            "Spawn",
            new XAttribute("Id", id),
            new XElement(
            "DynamicThing",
            new XAttribute("Id", prefabId),
            new XAttribute("ExpandInStartScreen", true),
            new XElement("SpawnPosition", new XAttribute("Rule", "Radial"))
        )
        ));
        _spawnNames[id] = "Additional lander";
        condition.Add(new XElement("Spawn", new XAttribute("Id", id), new XAttribute("Event", "NewWorld")));
        _structureChanged = true;
    }
}
