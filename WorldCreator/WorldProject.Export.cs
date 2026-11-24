using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    /// <summary>Validates the draft, writes a complete temporary mod and publishes it only after its packaged files pass validation.</summary>
    internal string Export(string modsDirectory, string id, string displayName, bool updateExisting = false)
    {
        if (updateExisting && !CanUpdateExport(modsDirectory, id))
            throw new IOException("Only the loaded World Creator local mod can be updated. Load it first, or use a different World ID to create a copy.");
        if (!updateExisting && IsValidWorldId(id) && Directory.Exists(Path.Combine(modsDirectory, "WorldCreator_" + id)))
            throw new IOException("World Project → World ID: a mod with this ID already exists. Use Update world mod for the loaded local mod, or choose a new ID to create a copy.");
        ExportWarnings.Clear();
        var issue = ValidateForExport(modsDirectory, id, displayName, updateExisting).FirstOrDefault(item => item.IsError);
        if (issue.IsError)
            throw new InvalidOperationException(issue.Text);
        var target = Path.Combine(modsDirectory, "WorldCreator_" + id);
        ApplyDraftFields();
        var worldOutput = CreateExportWorld(id, displayName);
        var changedMapLayers = PrepareEditedMapLayers(worldOutput, id);
        PreparePlayableAreaIdentifiers(worldOutput, id);
        var changedWeather = _hasImportedWeatherDefinitions || WeatherFields.Any(f => f.Value != f.Original) || _weather.Any(w => _userWeatherIds.Contains((string? )w.Event.Attribute("Id") ?? string.Empty));
        var weatherCopies = PrepareWeatherCopies(worldOutput, id, changedWeather, out var weatherNames);
        var changedOrbit = _orbitBody != null && (_hasImportedOrbitBody || _rotationChanged || OrbitFields.Any(f => f.Value != f.Original));
        RewriteExportOrbitReferences(worldOutput, id, changedOrbit);
        var data = CreateExportData(worldOutput, id, changedWeather, changedOrbit, weatherCopies);
        PrepareTextureReplacements(data, id);
        RepairMissingTemplateAssetPaths(worldOutput, id);
        var importedAssets = PrepareImportedAssetPaths(data, id);
        var temporary = Path.Combine(modsDirectory, ".WorldCreator_" + Guid.NewGuid().ToString("N"));
        try
        {
            WriteModMetadata(temporary, id, displayName);
            if (updateExisting) PreserveExportMetadata(target, temporary, displayName);
            WriteRuntimeLavaMap(worldOutput, temporary, id);
            new XDocument(data).Save(Path.Combine(temporary, "GameData", "world.xml"));
            WriteExportLanguage(temporary, weatherNames);
            WriteStartingEquipment(temporary);
            CopyImportedAssets(importedAssets, temporary);
            WriteEditedMapLayers(temporary, changedMapLayers);
            WriteProjectAssets(temporary, id);
            WriteCompiledTerrain(temporary, id);
            WriteTerrainSources(temporary);
            ValidateExport(temporary, id, displayName.Trim(), changedWeather, changedOrbit, weatherNames);
            WriteExportReadme(temporary, id, displayName.Trim(), weatherCopies.Count, changedOrbit, changedMapLayers.Count);
            PublishExport(temporary, target, updateExisting);
        }
        catch
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, true);
            throw;
        }

        _exportDirectory = Path.GetFullPath(target);
        ExportedWorldId = id;
        return target;
    }

    /// <summary>Commits all pending control values to their backing XML before an export copy is created.</summary>
    private void ApplyDraftFields()
    {
        var allFields = AdvancedFields.Concat(WorldFields).Concat(AtmosphereFields).Concat(WeatherFields).Concat(OrbitFields).Concat(ResourceFields).ToList();
        foreach (var field in allFields)
            field.Apply();
    }

    /// <summary>Creates the public world XML with the chosen identity and world-specific terrain path.</summary>
    private XElement CreateExportWorld(string id, string displayName)
    {
        var worldOutput = new XElement(_world);
        worldOutput.SetAttributeValue("Id", id);
        worldOutput.SetAttributeValue("Hidden", false);
        worldOutput.SetAttributeValue("Deprecated", false);
        var name = worldOutput.Element("Name") ?? new XElement("Name");
        if (name.Parent == null)
            worldOutput.AddFirst(name);
        name.RemoveAttributes();
        name.SetAttributeValue("Value", displayName.Trim());
        if (CompiledTerrainFiles.Count > 0 || _isBlankProject)
            worldOutput.Element("TerrainSettings")?.SetAttributeValue("Path", "Worlds/" + id + "/Terrain");
        return worldOutput;
    }

    /// <summary>Assigns world-specific texture paths to edited region masks and collects the bytes to package.</summary>
    private List<(string Id, string RelativePath, byte[] Data)> PrepareEditedMapLayers(XElement worldOutput, string id)
    {
        var changedMapLayers = new List<(string Id, string RelativePath, byte[] Data)>();
        for (var i = 0; i < MapLayers.Count; i++)
        {
            var layer = MapLayers[i];
            if (!_mapLayerTextures.TryGetValue(layer.Id, out var textureBytes))
                continue;
            var relativePath = "Worlds/" + id + "/Textures/MapLayer_" + i.ToString(CultureInfo.InvariantCulture) + ".png";
            var regionSet = worldOutput.Descendants("RegionSet").FirstOrDefault(e => (string? )e.Attribute("Id") == layer.Id);
            var texture = regionSet?.Element("Texture") ?? throw new InvalidOperationException("Map layer has no Texture element: " + layer.Id);
            texture.SetAttributeValue("Path", relativePath);
            texture.SetAttributeValue("Format", "RGB24");
            changedMapLayers.Add((layer.Id, relativePath, textureBytes));
        }

        return changedMapLayers;
    }

    /// <summary>Creates independent weather definitions and rewrites their world references without editing the source definitions.</summary>
    private List<XElement> PrepareWeatherCopies(XElement worldOutput, string id, bool changedWeather, out List<(string Key, string Name)> weatherNames)
    {
        var weatherCopies = new List<XElement>();
        weatherNames = new List<(string Key, string Name)>();
        if (!changedWeather)
            return weatherCopies;
        for (var i = 0; i < _weather.Count; i++)
        {
            var sourceWeatherId = (string? )_weather[i].Event.Attribute("Id") ?? "";
            var weatherId = id + "_Weather" + i.ToString(CultureInfo.InvariantCulture);
            var referenceIndex = _world.Elements("WeatherEvent").ToList().IndexOf(_weather[i].Reference);
            worldOutput.Elements("WeatherEvent").ElementAt(referenceIndex).SetAttributeValue("Id", weatherId);
            var copy = new XElement(_weather[i].Event);
            copy.SetAttributeValue("Id", weatherId);
            if (_weatherNames.TryGetValue(sourceWeatherId, out var weatherName))
            {
                var nameKey = (string? )copy.Element("Name")?.Attribute("Key") ?? sourceWeatherId;
                if (!weatherNames.Any(pair => pair.Key == nameKey))
                    weatherNames.Add((nameKey, weatherName));
            }

            weatherCopies.Add(copy);
        }

        return weatherCopies;
    }

    /// <summary>Updates the playable body and its child references to the generated celestial-body ID.</summary>
    private void RewriteExportOrbitReferences(XElement worldOutput, string id, bool changedOrbit)
    {
        if (!changedOrbit)
            return;
        var playable = worldOutput.Element("PlayableBody")!;
        var originalId = (string)playable.Attribute("Id")!;
        var bodyId = id + "_Body";
        playable.SetAttributeValue("Id", bodyId);
        var bodies = worldOutput.Element("CelestialBodies");
        if (bodies == null)
            return;
        foreach (var entry in bodies.Elements())
        {
            if ((string? )entry.Attribute("Id") == originalId)
                entry.SetAttributeValue("Id", bodyId);
            if ((string? )entry.Attribute("Parent") == originalId)
                entry.SetAttributeValue("Parent", bodyId);
        }
    }

    /// <summary>Assembles world, weather and celestial records into the exported GameData document.</summary>
    private XElement CreateExportData(XElement worldOutput, string id, bool changedWeather, bool changedOrbit, IReadOnlyList<XElement> weatherCopies)
    {
        var data = new XElement("GameData", new XElement("WorldSettings", worldOutput));
        if (changedWeather)
            foreach (var weather in weatherCopies)
                data.Add(weather);
        if (changedOrbit)
        {
            var bodyCopy = new XElement(_orbitBody!);
            bodyCopy.SetAttributeValue("Id", id + "_Body");
            data.Add(new XElement("CelestialBodies", bodyCopy));
        }

        return data;
    }

    /// <summary>Creates the package directories and writes the user-facing mod metadata.</summary>
    private static void WriteModMetadata(string temporary, string id, string displayName)
    {
        Directory.CreateDirectory(Path.Combine(temporary, "About"));
        Directory.CreateDirectory(Path.Combine(temporary, "GameData"));
        new XDocument(new XElement(
            "ModMetadata",
            new XElement("Name", displayName.Trim()),
            new XElement("ModID", "worldcreator.world." + id),
            new XElement("Author", "World Creator"),
            new XElement("Version", "1.0"),
            new XElement("Description", "Custom world created with World Creator.")
        )).Save(Path.Combine(temporary, "About", "About.xml"));
    }

    /// <summary>Packages translated weather, starting-equipment and region labels when the project defines any.</summary>
    private void WriteExportLanguage(string temporary, IReadOnlyList<(string Key, string Name)> weatherNames)
    {
        var languageRecords = weatherNames.Select(pair => new XElement("Record", new XElement("Key", pair.Key), new XElement("Value", pair.Name))).Concat(_customStartConditions.Select(condition =>
        {
            var id = (string? )condition.Attribute("Id") ?? string.Empty;
            var name = _startConditionNames.TryGetValue(id, out var value) ? value : id;
            var nameKey = (string? )condition.Element("Name")?.Attribute("Key");
            var descriptionKey = (string? )condition.Element("Description")?.Attribute("Key");
            var records = new List<XElement>();
            if (nameKey != null)
                records.Add(new XElement("Record", new XElement("Key", nameKey), new XElement("Value", name)));
            if (descriptionKey != null)
                records.Add(new XElement("Record", new XElement("Key", descriptionKey), new XElement("Value", "Starting option for " + name + ".")));
            return records;
        })
            .SelectMany(records => records)).Concat(_spawnNames.Select(pair => new XElement("Record", new XElement("Key", pair.Key + "Name"), new XElement("Value", pair.Value)))).Concat(_mapRegionNames.Select(pair => new XElement("Record", new XElement("Key", pair.Key), new XElement("Value", pair.Value))))
            .Concat(_sourceMapText.Where(pair => _world.Elements("RegionSet").Concat(_world.Elements("PointOfInterest")).SelectMany(element => element.Descendants()).Any(element => (string?)element.Attribute("Key") == pair.Key)).Select(pair => new XElement("Record", new XElement("Key", pair.Key), new XElement("Value", pair.Value))))
            .Concat(_mapTextEdits.Select(pair => new XElement("Record", new XElement("Key", pair.Key), new XElement("Value", pair.Value))))
            .GroupBy(record => (string?)record.Element("Key"), StringComparer.Ordinal).Select(group => group.Last()).ToList();
        if (languageRecords.Count > 0)
        {
            var language = new XElement(
                "English",
                new XElement("Name", "English"),
                new XElement("Code", "EN"),
                new XElement("Font", "font_english"),
                new XElement("Interface", languageRecords)
            );
            var languagePath = Path.Combine(temporary, "GameData", "Language");
            Directory.CreateDirectory(languagePath);
            new XDocument(language).Save(Path.Combine(languagePath, "english.xml"));
        }
    }

    /// <summary>Writes private starting-profile and spawn definitions alongside the world XML.</summary>
    private void WriteStartingEquipment(string temporary)
    {
        if (_customStartConditions.Count > 0 || _customSpawnDefinitions.Count > 0)
        {
            var startData = new XElement(
                "GameData",
                _customStartConditions.Select(condition => new XElement(condition)).Concat(_customSpawnDefinitions.Select(spawn => new XElement(spawn)))
            );
            new XDocument(startData).Save(Path.Combine(temporary, "GameData", "startconditions.xml"));
        }
    }

    /// <summary>Writes changed map-mask images to the paths already assigned in the exported XML.</summary>
    private static void WriteEditedMapLayers(string temporary, IReadOnlyList<(string Id, string RelativePath, byte[] Data)> changedMapLayers)
    {
        if (changedMapLayers.Count > 0)
        {
            foreach (var mapLayer in changedMapLayers)
            {
                var destination = Path.Combine(temporary, "GameData", mapLayer.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.WriteAllBytes(destination, mapLayer.Data);
            }
        }
    }

    /// <summary>Copies reusable project assets into the world-specific GameData asset folder.</summary>
    private void WriteProjectAssets(string temporary, string id)
    {
        foreach (var asset in ProjectAssets)
        {
            var relativePath = asset.RelativePath(id);
            var destination = Path.Combine(temporary, "GameData", relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllBytes(destination, asset.Data);
        }
    }

    /// <summary>Packages compiled terrain chunks or creates the empty terrain directory required by a blank world.</summary>
    private void WriteCompiledTerrain(string temporary, string id)
    {
        if (CompiledTerrainFiles.Count > 0)
        {
            var destination = Path.Combine(temporary, "GameData", "Worlds", id, "Terrain");
            Directory.CreateDirectory(destination);
            foreach (var file in CompiledTerrainFiles)
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            File.WriteAllLines(
                Path.Combine(destination, "README.txt"),
                new[] { "Compiled terrain copied from Stationeers Terrain Tools.", "The world XML references this directory as Worlds/" + id + "/Terrain.", "If the world fails to load, check the game's log for terrain asset resolver errors." }
            );
        }
        else if (_isBlankProject)
        {
            Directory.CreateDirectory(Path.Combine(temporary, "GameData", "Worlds", id, "Terrain"));
        }
    }

    /// <summary>Packages uncompiled terrain inputs with a manifest that identifies their source roles.</summary>
    private void WriteTerrainSources(string temporary)
    {
        if (TerrainSourceFiles.Count > 0)
        {
            var sourceDirectory = Path.Combine(temporary, "TerrainSource");
            Directory.CreateDirectory(sourceDirectory);
            var manifest = new List<string>
            {
                "Stationeers terrain sources selected in World Creator.",
                "The game Terrain Tools utility must import these files and save compiled terrain before the world can use them.",
                "Terrain XML still points at the selected built-in template's terrain assets.",
                "Do not save compiled data over the installed game's built-in worlds."
            };
            foreach (var pair in TerrainSourceFiles)
            {
                var destination = Path.Combine(sourceDirectory, pair.Key + Path.GetExtension(pair.Value).ToLowerInvariant());
                File.Copy(pair.Value, destination);
                manifest.Add(pair.Key + "=" + Path.GetFileName(destination));
            }

            File.WriteAllLines(Path.Combine(sourceDirectory, "README.txt"), manifest);
        }
    }

    /// <summary>Copies a referenced source directory recursively without replacing files already generated for this package.</summary>
    private static void CopyImportedDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, directory.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var copied = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(copied)!);
            if (!File.Exists(copied))
                File.Copy(file, copied);
        }
    }

    /// <summary>Repairs unresolved native-world texture paths using the source world's texture directory.</summary>
    private void RepairMissingTemplateAssetPaths(XElement world, string id)
    {
        RepairMissingPreview(world);
        foreach (var attribute in world.DescendantsAndSelf().Attributes("Path"))
        {
            var path = attribute.Value.Replace('\\', '/');
            var asset = Path.Combine(_assets, path.Replace('/', Path.DirectorySeparatorChar));
            if (!string.IsNullOrEmpty(_sourceAssetRoot))
            {
                var importedAsset = ResolveImportedAsset(path)!;
                if (File.Exists(importedAsset) || Directory.Exists(importedAsset))
                    continue;
            }

            if (File.Exists(asset) || Directory.Exists(asset) || !path.StartsWith("Worlds/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("Worlds/" + id + "/", StringComparison.Ordinal))
                continue;
            var textureMarker = path.IndexOf("/Textures/", StringComparison.OrdinalIgnoreCase);
            if (textureMarker < 0)
                continue;
            var replacement = "Worlds/" + _sourceWorldDirectory + path.Substring(textureMarker);
            var replacementPath = Path.Combine(_assets, replacement.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(replacementPath))
                attribute.Value = replacement;
        }
    }

    /// <summary>Writes a package report listing generated files, custom records and remaining gameplay checks.</summary>
    private void WriteExportReadme(string folder, string id, string displayName, int customWeatherCount, bool customOrbit, int editedMapCount)
    {
        var lines = new List<string>
        {
            "Stationeers World Creator export report",
            "World: " + displayName + " (" + id + ")",
            "Editor build: " + BuildInfo.Version,
            "Status: generated XML and packaged asset checks passed.",
            "",
            "Generated files:"
        };
        foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Select(file => file.Substring(folder.TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase))
            lines.Add("  " + file);
        lines.Add("  README.txt (this report)");
        lines.Add("");
        lines.Add("Export summary:");
        lines.Add("  Custom weather definitions: " + customWeatherCount.ToString(CultureInfo.InvariantCulture));
        lines.Add("  Custom start conditions: " + _customStartConditions.Count.ToString(CultureInfo.InvariantCulture));
        lines.Add("  Custom spawn definitions: " + _customSpawnDefinitions.Count.ToString(CultureInfo.InvariantCulture));
        lines.Add("  Edited map masks: " + editedMapCount.ToString(CultureInfo.InvariantCulture));
        lines.Add("  Custom orbit body: " + (customOrbit ? "yes" : "no"));
        lines.Add("  Reusable project assets: " + ProjectAssets.Count.ToString(CultureInfo.InvariantCulture));
        var warnings = new List<string>();
        warnings.AddRange(ExportWarnings);
        var knownSpawnIds = new HashSet<string>(
            _startConditionLibrary.Elements("Spawn").Concat(_customSpawnDefinitions).Select(spawn => (string? )spawn.Attribute("Id") ?? ""),
            StringComparer.Ordinal
        );
        var externalSpawnIds = _customStartConditions.SelectMany(condition => condition.Elements("Spawn")).Select(spawn => (string? )spawn.Attribute("Id") ?? "").Where(spawnId => spawnId.Length > 0 && !knownSpawnIds.Contains(spawnId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(spawnId => spawnId, StringComparer.Ordinal)
            .ToArray();
        if (externalSpawnIds.Length > 0)
            warnings.Add("Spawn IDs not found in installed game definitions or this export; confirm an enabled mod provides them: " + string.Join(", ", externalSpawnIds));
        if (CompiledTerrainFiles.Count == 0 && TerrainSourceFiles.Count > 0)
            warnings.Add("Terrain source maps are included, but they are not compiled terrain. Import them with the game's Terrain Tools before using this world as a playable custom terrain world.");
        else if (CompiledTerrainFiles.Count == 0 && _isBlankProject)
            warnings.Add("No compiled terrain files are included. A blank world needs terrain compiled with the game's Terrain Tools before it can load as a playable world.");
        if (_weather.Any(weather => weather.Event.Elements("Fog").Skip(1).Any()))
            warnings.Add("At least one weather definition contains repeated Fog elements. The installed game serializes one Fog field per event, so extra Fog cards may not render as separate layers.");
        if (customOrbit)
            warnings.Add("The custom orbit XML passed structural checks; orbital progression and coordinate conventions have not been verified in game.");
        warnings.Add("This report confirms generated file structure and available asset references only; test gameplay behavior in Stationeers.");
        lines.Add("");
        lines.Add("Warnings and follow-up:");
        foreach (var warning in warnings)
            lines.Add("  - " + warning);
        lines.Add("");
        lines.Add("This report is informational. It does not modify player saves or install the generated mod.");
        File.WriteAllLines(Path.Combine(folder, "README.txt"), lines);
    }
}
