using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    /// <summary>Checks world IDs against the supported filename characters, length and Windows reserved names.</summary>
    internal static bool IsValidWorldId(string id) => Regex.IsMatch(id, @"\A[A-Za-z0-9_]{1,47}\z") && !Regex.IsMatch(id, @"\A(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])\z", RegexOptions.IgnoreCase);
    /// <summary>Collects draft errors and terrain warnings before any package files are written.</summary>
    internal List<(bool IsError, string Text)> ValidateForExport(string modsDirectory, string id, string displayName, bool updateExisting = false)
    {
        var issues = new List<(bool IsError, string Text)>();
        if (!IsValidWorldId(id))
            issues.Add((true, "World ID must contain 1–47 letters, digits or underscores and cannot be a reserved filename such as CON or NUL. No prefix is required."));
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 80)
            issues.Add((true, "Enter a world name of up to 80 characters."));
        if (IsValidWorldId(id) && !string.IsNullOrWhiteSpace(displayName) && displayName.Length <= 80 && Directory.Exists(Path.Combine(modsDirectory, "WorldCreator_" + id)) && !(updateExisting && CanUpdateExport(modsDirectory, id)))
            issues.Add((true, "World Project → World ID: a mod with this ID already exists. Use Update world mod for the loaded local mod, or choose a new ID to create a copy."));
        var fields = AdvancedFields.Concat(WorldFields).Concat(AtmosphereFields).Concat(WeatherFields).Concat(OrbitFields).Concat(ResourceFields).Concat(MapFields).ToArray();
        foreach (var field in fields)
        {
            try
            {
                field.Validate();
            }
            catch (InvalidOperationException e)
            {
                issues.Add((true, e.Message));
            }
        }

        issues.AddRange(ValidateNumericRanges(fields));

        if (_isBlankProject && CompiledTerrainFiles.Count == 0)
            issues.Add((false, "This blank world has no compiled terrain. Add compiled terrain before trying to load it in game."));
        else if (CompiledTerrainFiles.Count == 0 && TerrainSourceFiles.Count > 0)
            issues.Add((false, "Terrain source maps are included, but they are not compiled terrain. Compile them with Terrain Tools before loading this world."));
        return issues;
    }

    /// <summary>Checks generated metadata, world references and packaged asset paths before publishing the temporary mod.</summary>
    private void ValidateExport(
        string folder,
        string id,
        string displayName,
        bool changedWeather,
        bool changedOrbit,
        IReadOnlyCollection<(string Key, string Name)> weatherNames
    )
    {
        var metadata = XDocument.Load(Path.Combine(folder, "About", "About.xml")).Root;
        if (metadata?.Name != "ModMetadata" || (string? )metadata.Element("ModID") != "worldcreator.world." + id || (string? )metadata.Element("Name") != displayName)
            throw new InvalidDataException("The generated About/About.xml metadata is incomplete or does not match this world.");
        var data = XDocument.Load(Path.Combine(folder, "GameData", "world.xml")).Root;
        var world = data?.Element("WorldSettings")?.Elements("World").SingleOrDefault(e => (string? )e.Attribute("Id") == id);
        if (data?.Name != "GameData" || world == null || (string? )world.Element("Name")?.Attribute("Value") != displayName || world.Element("TerrainSettings") == null || world.Element("PlayableBody") == null)
            throw new InvalidDataException("The generated GameData/world.xml is missing the expected world settings.");
        if (changedWeather)
        {
            foreach (var reference in world.Elements("WeatherEvent"))
            {
                var eventId = (string? )reference.Attribute("Id");
                if (eventId != null && eventId.StartsWith(id + "_Weather", StringComparison.Ordinal) && data.Elements("WeatherEvent").Count(e => (string? )e.Attribute("Id") == eventId) != 1)
                    throw new InvalidDataException("A custom weather reference has no unique matching definition in GameData/world.xml.");
            }
        }

        ValidateStartConditionExport(folder, world);
        ValidateMapTextExport(folder);
        var geographic = world.Element("GeographicRegionData");
        if (_mapRegionNames.Count > 0)
        {
            if (geographic == null)
                throw new InvalidDataException("Custom geographic regions have no GeographicRegionData block.");
            var regionSetId = (string? )geographic.Element("RegionSet")?.Attribute("Id");
            var regionSet = data.Descendants("RegionSet").SingleOrDefault(element => (string? )element.Attribute("Id") == regionSetId && element.Element("Texture") != null);
            if (string.IsNullOrWhiteSpace(regionSetId) || regionSet == null || regionSet.Elements("Region").Count() == 0)
                throw new InvalidDataException("GeographicRegionData must reference a defined RegionSet with at least one Region.");
            var regionIds = regionSet.Elements("Region").Select(element => (string? )element.Attribute("Id")).ToArray();
            if (regionIds.Any(string.IsNullOrWhiteSpace) || regionIds.Distinct(StringComparer.Ordinal).Count() != regionIds.Length)
                throw new InvalidDataException("The geographic region layer contains a missing or duplicate region ID.");
            var regionColors = regionSet.Elements("Region").Select(element => ((int? )element.Attribute("R") ?? -1, (int? )element.Attribute("G") ?? -1, (int? )element.Attribute("B") ?? -1))
                .ToArray();
            if (regionColors.Any(color => color.Item1 < 0 || color.Item1 > 255 || color.Item2 < 0 || color.Item2 > 255 || color.Item3 < 0 || color.Item3 > 255) || regionColors.Distinct().Count() != regionColors.Length)
                throw new InvalidDataException("The geographic region layer contains an invalid or duplicate RGB color.");
            foreach (var pair in _mapRegionNames)
            {
                if (string.IsNullOrWhiteSpace(pair.Value))
                    throw new InvalidDataException("A custom geographic region needs a display name: " + pair.Key);
                var keyCount = regionSet.Elements("Region").Count(element => (string? )element.Element("Name")?.Attribute("Key") == pair.Key);
                var languageFile = Path.Combine(folder, "GameData", "Language", "english.xml");
                var language = File.Exists(languageFile) ? XDocument.Load(languageFile).Root?.Element("Interface") : null;
                if (keyCount != 1 || language?.Elements("Record").Count(record => (string? )record.Element("Key") == pair.Key && (string? )record.Element("Value") == pair.Value) != 1)
                    throw new InvalidDataException("A custom geographic region is missing its unique English name record: " + pair.Key);
            }
        }

        if (weatherNames.Count > 0)
        {
            var languagePath = Path.Combine(folder, "GameData", "Language", "english.xml");
            var language = File.Exists(languagePath) ? XDocument.Load(languagePath).Root : null;
            if (language?.Name != "English" || language.Element("Interface") == null || weatherNames.Any(pair => language.Element("Interface")!.Elements("Record").Count(record => (string? )record.Element("Key") == pair.Key && (string? )record.Element("Value") == pair.Name) != 1))
                throw new InvalidDataException("Custom weather names are missing from the generated GameData/Language/english.xml file.");
        }

        if (changedOrbit)
        {
            var bodyId = (string? )world.Element("PlayableBody")?.Attribute("Id");
            if (bodyId != id + "_Body" || data.Elements("CelestialBodies").Elements("CelestialBody").Count(e => (string? )e.Attribute("Id") == bodyId) != 1)
                throw new InvalidDataException("The custom playable body has no unique matching celestial body definition.");
        }

        var gameDataFolder = Path.Combine(folder, "GameData");
        foreach (var attribute in world.DescendantsAndSelf().Attributes("Path"))
        {
            var path = attribute.Value.Replace('\\', '/');
            var packagedPath = Path.Combine(gameDataFolder, path.Replace('/', Path.DirectorySeparatorChar));
            var resolvedPath = File.Exists(packagedPath) || Directory.Exists(packagedPath) ? packagedPath : Path.Combine(_assets, path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(resolvedPath) && !Directory.Exists(resolvedPath))
                throw new ExportAssetException(FindAssetField(attribute, path), path);
        }
    }

    /// <summary>Checks generated profile and spawn IDs, item quantities, defaults and lifecycle references.</summary>
    private void ValidateStartConditionExport(string folder, XElement world)
    {
        if (_customStartConditions.Count == 0 && _customSpawnDefinitions.Count == 0)
            return;
        var path = Path.Combine(folder, "GameData", "startconditions.xml");
        if (!File.Exists(path))
            throw new InvalidDataException("Custom starting conditions were authored but GameData/startconditions.xml was not generated.");
        var data = XDocument.Load(path).Root;
        if (data?.Name != "GameData")
            throw new InvalidDataException("Generated GameData/startconditions.xml must have a GameData root.");
        var exportedConditions = data.Elements("StartCondition").ToArray();
        var exportedSpawns = data.Elements("Spawn").ToArray();
        if (exportedConditions.Length != _customStartConditions.Count || exportedSpawns.Length != _customSpawnDefinitions.Count)
            throw new InvalidDataException("Generated startconditions.xml does not contain the expected number of custom definitions.");
        var worldReferences = world.Elements("StartCondition").ToArray();
        var defaultCount = worldReferences.Count(reference => string.Equals((string? )reference.Attribute("IsDefault"), "true", StringComparison.OrdinalIgnoreCase));
        if (defaultCount > 1)
            throw new InvalidDataException("The exported world marks more than one start condition as the default.");
        foreach (var condition in _customStartConditions)
        {
            var id = (string? )condition.Attribute("Id");
            if (string.IsNullOrWhiteSpace(id) || exportedConditions.Count(item => (string? )item.Attribute("Id") == id) != 1)
                throw new InvalidDataException("A custom start condition has a missing or duplicate definition in startconditions.xml.");
            if (worldReferences.Count(reference => (string? )reference.Attribute("Id") == id) != 1)
                throw new InvalidDataException("A custom start condition must have exactly one matching reference in world.xml: " + id);
            foreach (var spawn in condition.Elements("Spawn"))
            {
                if (string.IsNullOrWhiteSpace((string? )spawn.Attribute("Id")) || string.IsNullOrWhiteSpace((string? )spawn.Attribute("Event")))
                    throw new InvalidDataException("A start condition contains a Spawn reference without both Id and Event attributes: " + id);
            }
        }

        var spawnIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var spawn in _customSpawnDefinitions)
        {
            var id = (string? )spawn.Attribute("Id");
            if (string.IsNullOrWhiteSpace(id) || !spawnIds.Add(id) || exportedSpawns.Count(item => (string? )item.Attribute("Id") == id) != 1)
                throw new InvalidDataException("A custom spawn definition has a missing or duplicate Id in startconditions.xml.");
            var dynamicThing = spawn.Element("DynamicThing");
            if ((string? )dynamicThing?.Attribute("Id") != "DynamicCrate")
                continue;
            foreach (var item in dynamicThing.Elements("Item"))
            {
                var itemId = (string? )item.Attribute("Id");
                var quantity = item.Element("Quantity") is XElement quantityElement ? (string?)quantityElement.Attribute("Value") : "1";
                if (string.IsNullOrWhiteSpace(itemId) || !float.TryParse(quantity, NumberStyles.Float, CultureInfo.InvariantCulture, out var count) || float.IsNaN(count) || float.IsInfinity(count) || count < 0)
                    throw new ExportEquipmentException(item, _spawnNames.TryGetValue(id!, out var crateName) ? crateName : id!);
            }
        }

        var expectedLanguage = _customStartConditions.SelectMany(condition =>
        {
            var id = (string? )condition.Attribute("Id") ?? string.Empty;
            var name = _startConditionNames.TryGetValue(id, out var value) ? value : id;
            var entries = new List<(string Key, string Value)>();
            var nameKey = (string? )condition.Element("Name")?.Attribute("Key");
            var descriptionKey = (string? )condition.Element("Description")?.Attribute("Key");
            if (!string.IsNullOrWhiteSpace(nameKey))
                entries.Add((nameKey!, name));
            if (!string.IsNullOrWhiteSpace(descriptionKey))
                entries.Add((descriptionKey!, "Starting option for " + name + "."));
            return entries;
        }).Concat(_spawnNames.Select(pair => (Key: pair.Key + "Name", Value: pair.Value)))
            .ToArray();
        if (expectedLanguage.Length == 0)
            return;
        var languagePath = Path.Combine(folder, "GameData", "Language", "english.xml");
        var language = File.Exists(languagePath) ? XDocument.Load(languagePath).Root : null;
        if (language?.Name != "English" || language.Element("Interface") == null || expectedLanguage.Any(entry => language.Element("Interface")!.Elements("Record").Count(record => (string? )record.Element("Key") == entry.Key && (string? )record.Element("Value") == entry.Value) != 1))
            throw new InvalidDataException("Custom starting-condition and crate names are missing or duplicated in GameData/Language/english.xml.");
    }
}
