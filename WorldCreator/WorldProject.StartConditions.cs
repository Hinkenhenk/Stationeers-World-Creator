using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    /// <summary>Imports source-mod starting profiles and spawn definitions, giving them priority over native records with matching IDs.</summary>
    private void AddImportedStartConditionRecords(XElement root)
    {
        var data = root.Name.LocalName == "GameData" ? root : root.Element("GameData");
        foreach (var element in (data?.Elements("StartCondition") ?? Enumerable.Empty<XElement>()).Concat(data?.Elements("Spawn") ?? Enumerable.Empty<XElement>()))
        {
            var id = (string? )element.Attribute("Id");
            if (string.IsNullOrWhiteSpace(id))
                continue;
            var collection = element.Name.LocalName == "StartCondition" ? "StartCondition" : "Spawn";
            _startConditionLibrary.Elements(collection).Where(existing => (string? )existing.Attribute("Id") == id).Remove();
            _startConditionLibrary.Add(new XElement(element));
            if (collection == "StartCondition" && !_world.Elements("StartCondition").Any(reference => (string? )reference.Attribute("Id") == id))
                continue;
            var imported = collection == "StartCondition" ? _customStartConditions : _customSpawnDefinitions;
            imported.RemoveAll(existing => (string? )existing.Attribute("Id") == id);
            imported.Add(new XElement(element));
        }
    }

    /// <summary>Resolves a starting-profile ID against project copies before the native profile library.</summary>
    private XElement? FindStartCondition(string id) => _customStartConditions.FirstOrDefault(e => (string? )e.Attribute("Id") == id) ?? _startConditionLibrary.Elements("StartCondition").FirstOrDefault(e => (string? )e.Attribute("Id") == id);
    /// <summary>Copies an installed starting profile into the world and assigns it as a starting option.</summary>
    internal void AddStartConditionFrom(string sourceId)
    {
        var source = FindStartCondition(sourceId) ?? throw new InvalidOperationException("Starting profile not found: " + sourceId);
        var copy = CreateStartConditionCopy(source, sourceId + " custom");
        _customStartConditions.Add(copy);
        var reference = new XElement("StartCondition", new XAttribute("Id", (string)copy.Attribute("Id")!));
        if (!_world.Elements("StartCondition").Any(e => string.Equals((string? )e.Attribute("IsDefault"), "true", StringComparison.OrdinalIgnoreCase)))
            reference.SetAttributeValue("IsDefault", true);
        _world.Add(reference);
        _structureChanged = true;
    }

    /// <summary>Creates a world-specific starting profile with no inherited equipment references.</summary>
    internal void CreateBlankStartCondition()
    {
        var source = FindStartCondition("DefaultStart") ?? throw new InvalidOperationException("The installed DefaultStart profile was not found.");
        var copy = CreateStartConditionCopy(source, "New Start");
        copy.Elements("Spawn").Remove();
        _customStartConditions.Add(copy);
        _editableEquipmentProfiles.Add((string)copy.Attribute("Id")!);
        var reference = new XElement("StartCondition", new XAttribute("Id", (string)copy.Attribute("Id")!));
        if (!_world.Elements("StartCondition").Any(e => string.Equals((string? )e.Attribute("IsDefault"), "true", StringComparison.OrdinalIgnoreCase)))
            reference.SetAttributeValue("IsDefault", true);
        _world.Add(reference);
        _structureChanged = true;
    }

    /// <summary>Validates and changes the generated display name for a project-owned starting profile.</summary>
    internal void SetStartConditionName(string id, string name)
    {
        var definition = FindStartCondition(id);
        if (definition == null || !_customStartConditions.Contains(definition))
            throw new InvalidOperationException("Customize this starting condition before renaming it.");
        name = name.Trim();
        if (name.Length == 0)
            return;
        if (_startConditionNames.TryGetValue(id, out var existing) && existing == name)
            return;
        _startConditionNames[id] = name;
        _structureChanged = true;
    }

    /// <summary>Updates the selected profile's brutal-mode and terrain-edit flags.</summary>
    internal void SetStartConditionFlags(string id, bool isBrutal, bool isTerrainEdit)
    {
        var condition = FindEditableStartCondition(id);
        if (string.Equals((string? )condition.Attribute("IsBrutal"), isBrutal ? "true" : "false", StringComparison.OrdinalIgnoreCase) && string.Equals((string? )condition.Attribute("IsTerrainEdit"), isTerrainEdit ? "true" : "false", StringComparison.OrdinalIgnoreCase))
            return;
        condition.SetAttributeValue("IsBrutal", isBrutal ? "true" : "false");
        condition.SetAttributeValue("IsTerrainEdit", isTerrainEdit ? "true" : "false");
        _structureChanged = true;
    }

    /// <summary>Builds crate views for custom supply crates assigned to the world's creation event.</summary>
    private IReadOnlyList<StartCrateView> GetStartConditionCrates(XElement condition)
    {
        var crates = new List<StartCrateView>();
        foreach (var reference in condition.Elements("Spawn").Where(e => (string? )e.Attribute("Event") == "NewWorld"))
        {
            var id = (string? )reference.Attribute("Id") ?? string.Empty;
            if (id.Length == 0)
                continue;
            var spawn = _customSpawnDefinitions.FirstOrDefault(e => (string? )e.Attribute("Id") == id);
            var dynamicThing = spawn?.Element("DynamicThing");
            if (dynamicThing == null || (string? )dynamicThing.Attribute("Id") != "DynamicCrate")
                continue;
            var items = dynamicThing.Elements("Item").Select(item => ((string? )item.Attribute("Id") ?? string.Empty, (int? )item.Element("Quantity")?.Attribute("Value") ?? 1))
                .ToArray();
            crates.Add(new StartCrateView(id, _spawnNames.TryGetValue(id, out var name) ? name : id, items));
        }

        return crates;
    }

    /// <summary>Creates an empty project-owned supply crate and assigns it to the selected starting profile.</summary>
    internal string AddStartConditionCrate(string conditionId, string name)
    {
        var condition = FindEditableStartCondition(conditionId);
        var template = _startConditionLibrary.Elements("Spawn").FirstOrDefault(e => (string? )e.Attribute("Id") == "BrutalCrate") ?? throw new InvalidOperationException("The installed BrutalCrate example was not found.");
        var spawn = new XElement(template);
        var id = "WCSpawn_" + Regex.Replace(_world.Attribute("Id")?.Value ?? "World", "[^A-Za-z0-9_]", "_") + "_Crate" + (_customSpawnDefinitions.Count + 1).ToString(CultureInfo.InvariantCulture);
        while (_customSpawnDefinitions.Any(e => (string? )e.Attribute("Id") == id) || _startConditionLibrary.Elements("Spawn").Any(e => (string? )e.Attribute("Id") == id))
            id += "_";
        spawn.SetAttributeValue("Id", id);
        spawn.Elements("Item").Remove();
        var container = spawn.Element("DynamicThing") ?? throw new InvalidOperationException("The installed crate example has no DynamicThing container.");
        container.Elements().Where(e => e.Name.LocalName != "SpawnPosition" && e.Name.LocalName != "Color" && e.Name.LocalName != "Name").Remove();
        var nameKey = id + "Name";
        var nameElement = container.Element("Name");
        if (nameElement == null)
        {
            nameElement = new XElement("Name");
            container.Add(nameElement);
        }

        nameElement.SetAttributeValue("Key", nameKey);
        condition.Add(new XElement("Spawn", new XAttribute("Id", id), new XAttribute("Event", "NewWorld")));
        _customSpawnDefinitions.Add(spawn);
        _spawnNames[id] = string.IsNullOrWhiteSpace(name) ? "Supply crate" : name.Trim();
        _structureChanged = true;
        return id;
    }

    /// <summary>Updates the generated label for a project-owned supply crate.</summary>
    internal void SetStartConditionCrateName(string crateId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Enter a name for the supply crate.");
        FindCustomCrate(crateId);
        name = name.Trim();
        if (!_spawnNames.TryGetValue(crateId, out var current) || current == name)
            return;
        _spawnNames[crateId] = name;
        _structureChanged = true;
    }

    /// <summary>Adds an item and positive quantity to a project-owned supply crate.</summary>
    internal void AddStartConditionCrateItem(string crateId, string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            throw new InvalidOperationException("Enter an item prefab ID.");
        if (quantity < 1)
            throw new InvalidOperationException("Item quantity must be at least one.");
        var crate = FindCustomCrate(crateId);
        crate.Element("DynamicThing")!.Add(new XElement(
            "Item",
            new XAttribute("Id", itemId.Trim()),
            new XElement("Quantity", new XAttribute("Value", quantity.ToString(CultureInfo.InvariantCulture)))
        ));
        _structureChanged = true;
    }

    /// <summary>Updates the prefab ID and quantity of one existing custom-crate item.</summary>
    internal void SetStartConditionCrateItem(string crateId, int index, string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            throw new InvalidOperationException("Enter an item prefab ID.");
        if (quantity < 1)
            throw new InvalidOperationException("Item quantity must be at least one.");
        var item = FindCustomCrate(crateId).Element("DynamicThing")!.Elements("Item").ElementAtOrDefault(index) ?? throw new InvalidOperationException("Crate item no longer exists.");
        item.SetAttributeValue("Id", itemId.Trim());
        var value = item.Element("Quantity");
        if (value == null)
        {
            value = new XElement("Quantity");
            item.Add(value);
        }

        value.SetAttributeValue("Value", quantity.ToString(CultureInfo.InvariantCulture));
        _structureChanged = true;
    }

    /// <summary>Removes one item occurrence from a project-owned supply crate.</summary>
    internal void RemoveStartConditionCrateItem(string crateId, int index)
    {
        var item = FindCustomCrate(crateId).Element("DynamicThing")!.Elements("Item").ElementAtOrDefault(index) ?? throw new InvalidOperationException("Crate item no longer exists.");
        item.Remove();
        _structureChanged = true;
    }

    /// <summary>Detaches a custom crate from a profile and removes its definition when no other profile uses it.</summary>
    internal void RemoveStartConditionCrate(string conditionId, string crateId)
    {
        var condition = FindEditableStartCondition(conditionId);
        var reference = condition.Elements("Spawn").FirstOrDefault(e => (string? )e.Attribute("Id") == crateId && (string? )e.Attribute("Event") == "NewWorld") ?? throw new InvalidOperationException("Crate reference no longer exists.");
        reference.Remove();
        if (!_customStartConditions.SelectMany(e => e.Elements("Spawn")).Any(e => (string? )e.Attribute("Id") == crateId))
        {
            _customSpawnDefinitions.RemoveAll(e => (string? )e.Attribute("Id") == crateId);
            _spawnNames.Remove(crateId);
        }

        _structureChanged = true;
    }

    /// <summary>Rejects edits unless the selected starting profile belongs to this project.</summary>
    private XElement FindEditableStartCondition(string id)
    {
        var condition = FindStartCondition(id);
        if (condition == null || !_customStartConditions.Contains(condition))
            throw new InvalidOperationException("Customize this starting condition before editing its loadout.");
        return condition;
    }

    /// <summary>Finds a project-owned spawn definition that contains an editable dynamic container.</summary>
    private XElement FindCustomCrate(string id) => _customSpawnDefinitions.FirstOrDefault(e => (string? )e.Attribute("Id") == id && e.Element("DynamicThing") != null) ?? throw new InvalidOperationException("Custom crate no longer exists.");
    /// <summary>Replaces a native starting-profile reference with a world-owned copy before editing.</summary>
    internal void CustomizeStartCondition(string id)
    {
        if (_customStartConditions.Any(e => (string? )e.Attribute("Id") == id))
            return;
        var reference = _world.Elements("StartCondition").FirstOrDefault(e => (string? )e.Attribute("Id") == id) ?? throw new InvalidOperationException("Starting condition is not assigned to this world.");
        var source = FindStartCondition(id) ?? throw new InvalidOperationException("Starting profile not found: " + id);
        var copy = CreateStartConditionCopy(source, id + " custom");
        _customStartConditions.Add(copy);
        reference.SetAttributeValue("Id", (string)copy.Attribute("Id")!);
        _startConditionNames[(string)copy.Attribute("Id")!] = id + " custom";
        _structureChanged = true;
    }

    /// <summary>Creates a uniquely identified profile copy and its world-specific localization keys.</summary>
    private XElement CreateStartConditionCopy(XElement source, string name)
    {
        var id = "WCStart_" + Regex.Replace(_world.Attribute("Id")?.Value ?? "World", "[^A-Za-z0-9_]", "_") + "_" + (_customStartConditions.Count + 1).ToString(CultureInfo.InvariantCulture);
        while (FindStartCondition(id) != null)
            id += "_";
        var copy = new XElement(source);
        copy.SetAttributeValue("Id", id);
        copy.Element("Name")?.SetAttributeValue("Key", id + "Name");
        copy.Element("Description")?.SetAttributeValue("Key", id + "Description");
        copy.Element("WorldInject")?.Remove();
        _startConditionNames[id] = name;
        return copy;
    }

    /// <summary>Removes a starting option, repairs its default selection and releases unshared custom definitions.</summary>
    internal void RemoveStartCondition(string id)
    {
        var reference = _world.Elements("StartCondition").FirstOrDefault(e => (string? )e.Attribute("Id") == id) ?? throw new InvalidOperationException("Starting condition is not assigned to this world.");
        var removedDefinition = FindStartCondition(id);
        var removedSpawnIds = removedDefinition?.Elements("Spawn").Select(e => (string? )e.Attribute("Id")).Where(spawnId => spawnId != null).ToArray() ?? Array.Empty<string?>();
        var wasDefault = string.Equals((string? )reference.Attribute("IsDefault"), "true", StringComparison.OrdinalIgnoreCase);
        reference.Remove();
        if (wasDefault)
            _world.Elements("StartCondition").FirstOrDefault()?.SetAttributeValue("IsDefault", true);
        if (!_world.Elements("StartCondition").Any(e => (string? )e.Attribute("Id") == id))
        {
            _customStartConditions.RemoveAll(e => (string? )e.Attribute("Id") == id);
            _startConditionNames.Remove(id);
            foreach (var spawnId in removedSpawnIds)
            {
                if (_customStartConditions.SelectMany(e => e.Elements("Spawn")).Any(e => (string? )e.Attribute("Id") == spawnId))
                    continue;
                _customSpawnDefinitions.RemoveAll(e => (string? )e.Attribute("Id") == spawnId);
                _spawnNames.Remove(spawnId!);
            }
        }

        _structureChanged = true;
    }

    /// <summary>Marks one assigned starting profile as the default and clears the flag on the others.</summary>
    internal void SetDefaultStartCondition(string id)
    {
        var reference = _world.Elements("StartCondition").FirstOrDefault(e => (string? )e.Attribute("Id") == id) ?? throw new InvalidOperationException("Starting condition is not assigned to this world.");
        foreach (var item in _world.Elements("StartCondition"))
            item.SetAttributeValue("IsDefault", item == reference ? true : null);
        _structureChanged = true;
    }

    /// <summary>Updates one editable profile's spawn ID and lifecycle event after validating the event name.</summary>
    internal void SetStartConditionSpawn(string id, int index, string spawnId, string eventName)
    {
        if (!new[]
        {
            "NewWorld",
            "NewPlayer",
            "RespawnPlayer",
            "NewPlayerKit",
            "RespawnPlayerKit"
        }.Contains(eventName))
            throw new InvalidOperationException("Choose a supported start event.");
        var definition = FindStartCondition(id);
        if (definition == null || !_customStartConditions.Contains(definition))
            throw new InvalidOperationException("Customize this starting condition before editing its spawn references.");
        var spawn = definition!.Elements("Spawn").ElementAtOrDefault(index) ?? throw new InvalidOperationException("Starting spawn reference no longer exists.");
        spawn.SetAttributeValue("Id", spawnId.Trim());
        spawn.SetAttributeValue("Event", eventName);
        _structureChanged = true;
    }

    /// <summary>Assigns an existing game or mod spawn definition to a supported starting-profile event.</summary>
    internal void AddStartConditionSpawn(string id, string spawnId, string eventName)
    {
        if (string.IsNullOrWhiteSpace(spawnId))
            throw new InvalidOperationException("Enter a game Spawn ID.");
        if (!new[]
        {
            "NewWorld",
            "NewPlayer",
            "RespawnPlayer",
            "NewPlayerKit",
            "RespawnPlayerKit"
        }.Contains(eventName))
            throw new InvalidOperationException("Choose a supported start event.");
        var definition = FindStartCondition(id);
        if (definition == null || !_customStartConditions.Contains(definition))
            throw new InvalidOperationException("Customize this starting condition before editing its spawn references.");
        definition!.Add(new XElement("Spawn", new XAttribute("Id", spawnId.Trim()), new XAttribute("Event", eventName)));
        _structureChanged = true;
    }

    /// <summary>Removes one spawn-event reference from an editable starting profile.</summary>
    internal void RemoveStartConditionSpawn(string id, int index)
    {
        var definition = FindStartCondition(id);
        if (definition == null || !_customStartConditions.Contains(definition))
            throw new InvalidOperationException("Customize this starting condition before editing its spawn references.");
        (definition!.Elements("Spawn").ElementAtOrDefault(index) ?? throw new InvalidOperationException("Starting spawn reference no longer exists.")).Remove();
        _structureChanged = true;
    }
}
