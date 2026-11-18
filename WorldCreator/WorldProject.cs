using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    internal readonly List<Field> WorldFields = new List<Field>();
    internal readonly List<Field> AtmosphereFields = new List<Field>();
    internal readonly List<Field> WeatherFields = new List<Field>();
    internal readonly List<Field> OrbitFields = new List<Field>();
    internal readonly List<Field> ResourceFields = new List<Field>();
    internal readonly List<Field> AdvancedFields = new List<Field>();
    internal readonly List<string> TerrainSummary = new List<string>();
    internal readonly List<Field> MapFields = new List<Field>();
    internal readonly List<MapLayer> MapLayers = new List<MapLayer>();
    internal readonly List<PointOfInterest> PointsOfInterest = new List<PointOfInterest>();
    internal readonly List<ProjectAsset> ProjectAssets = new List<ProjectAsset>();
    private readonly List<OptionalSetting> _optionalSettings = new List<OptionalSetting>();
    private readonly XElement _startConditionLibrary;
    private readonly List<XElement> _customStartConditions = new List<XElement>();
    private readonly List<XElement> _customSpawnDefinitions = new List<XElement>();
    private readonly Dictionary<string, string> _startConditionNames = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _spawnNames = new Dictionary<string, string>(StringComparer.Ordinal);
    private bool _descriptionChanged;
    internal bool HasChanges => WorldFields.Concat(AtmosphereFields).Concat(WeatherFields).Concat(OrbitFields).Concat(ResourceFields).Concat(AdvancedFields).Any(f => f.Value != f.Original) || _structureChanged || _descriptionChanged || TerrainSourceFiles.Count > 0 || CompiledTerrainFiles.Count > 0 || ProjectAssets.Count > 0;
    internal string DisplayName => (string? )_world.Element("Name")?.Attribute("Value") ?? string.Empty;
    internal string WorldDescription => (string? )_world.Element("Description")?.Attribute("Value") ?? string.Empty;
    internal string? MiniMapAssetPath { get; private set; }
    internal string? SourceAssetRoot => _sourceAssetRoot;
    internal string? SourceGameDataRoot => _sourceGameDataRoot;
    internal string PrimaryBodyId => WorldFields.FirstOrDefault(entry => entry.Label.EndsWith(" / Primary body ID", StringComparison.Ordinal))?.Value ?? (string? )_world.Element("PrimaryBody")?.Attribute("Id") ?? "Sun";
    internal string TerrainWorldSize { get; private set; } = "(not set)";
    internal int PointOfInterestCount { get; private set; }

    internal readonly Dictionary<string, string> TerrainSourceFiles = new Dictionary<string, string>();
    private readonly Dictionary<string, byte[]> _mapLayerTextures = new Dictionary<string, byte[]>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _mapRegionNames = new Dictionary<string, string>(StringComparer.Ordinal);
    internal bool CanAddGeographicLayer => _world.Element("GeographicRegionData") == null;

    internal readonly List<string> CompiledTerrainFiles = new List<string>();
    private readonly XElement _world;
    private readonly string _assets;
    private readonly string _sourceWorldDirectory;
    private readonly string? _sourceAssetRoot;
    private readonly string? _sourceGameDataRoot;
    private readonly XElement _sourceXml;
    private readonly XElement _celestialBodyData;
    private readonly bool _isBlankProject;
    private readonly bool _hasImportedWeatherDefinitions;
    private readonly bool _hasImportedOrbitBody;
    private readonly XElement? _orbitBody;
    private readonly XElement _weatherLibrary;
    private readonly List<(XElement Reference, XElement Event)> _weather = new List<(XElement, XElement)>();
    private readonly HashSet<string> _userWeatherIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _weatherNames = new Dictionary<string, string>(StringComparer.Ordinal);
    private bool _structureChanged;
    internal IReadOnlyList<string> WeatherOptions => _weatherLibrary.Elements("WeatherEvent").Select(e => (string? )e.Attribute("Id")).Where(id => !string.IsNullOrEmpty(id)).Select(id => id!)
        .ToArray();
    internal IReadOnlyList<OptionalSetting> OptionalSettings => _optionalSettings;
    internal IReadOnlyList<WeatherTiming> WeatherTimings => _weather.Select(w => new WeatherTiming((string? )w.Event.Attribute("Id") ?? "Weather", w.Event, WeatherFields)).ToArray();
    internal IReadOnlyList<string> StartConditionOptions => _startConditionLibrary.Elements("StartCondition").Select(e => (string? )e.Attribute("Id") ?? string.Empty).Where(id => id.Length > 0)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    internal IReadOnlyList<string> StartConditionLabels => StartConditionOptions.Select(id => _startConditionNames.TryGetValue(id, out var name) && name != id ? name + "  ·  " + id : id).ToArray();
    internal IReadOnlyList<StartConditionView> StartConditions => _world.Elements("StartCondition").Select(reference =>
    {
        var id = (string? )reference.Attribute("Id") ?? string.Empty;
        var definition = FindStartCondition(id) ?? reference;
        return new StartConditionView(
            id,
            _startConditionNames.TryGetValue(id, out var name) ? name : id,
            string.Equals((string? )reference.Attribute("IsDefault"), "true", StringComparison.OrdinalIgnoreCase),
            _customStartConditions.Any(e => (string? )e.Attribute("Id") == id),
            string.Equals((string? )definition.Attribute("IsBrutal"), "true", StringComparison.OrdinalIgnoreCase),
            string.Equals((string? )definition.Attribute("IsTerrainEdit"), "true", StringComparison.OrdinalIgnoreCase),
            definition.Elements("Spawn").Select(e => ((string? )e.Attribute("Id") ?? string.Empty, (string? )e.Attribute("Event") ?? string.Empty))
            .ToArray(),
            GetStartConditionCrates(definition)
        );
    })
        .Where(view => view != null).Cast<StartConditionView>()
        .ToArray();

    /// <summary>Loads a native or custom world into an independent editable draft, including its referenced equipment, weather and orbit.</summary>
    internal WorldProject(string assets, string sourceFile, bool isAbsolute = false) : this(assets, sourceFile, isAbsolute, null)
    {
    }

    /// <summary>Creates an empty world draft with the minimum XML needed by the editor.</summary>
    internal static WorldProject CreateBlank(string assets) => new WorldProject(assets, string.Empty, false, CreateBlankWorld());
    /// <summary>Stores the player's description as literal XML text and marks it as changed.</summary>
    internal void SetWorldDescription(string value)
    {
        if (WorldDescription == value)
            return;
        var description = _world.Element("Description");
        if (description == null)
        {
            description = new XElement("Description");
            _world.AddFirst(description);
        }

        description.RemoveAttributes();
        description.SetAttributeValue("Value", value);
        _descriptionChanged = true;
    }

    /// <summary>Loads a native or custom world into an independent editable draft, including its referenced equipment, weather and orbit.</summary>
    private WorldProject(string assets, string sourceFile, bool isAbsolute, XElement? blankWorld)
    {
        _assets = assets;
        _sourceWorldDirectory = string.IsNullOrEmpty(sourceFile) ? string.Empty : Path.GetDirectoryName(sourceFile)?.Replace('\\', '/') ?? string.Empty;
        var file = isAbsolute ? sourceFile : Path.Combine(assets, "Worlds", sourceFile);
        var document = blankWorld == null ? XDocument.Load(file, LoadOptions.PreserveWhitespace) : null;
        var root = document?.Root ?? new XElement("GameData");
        _sourceXml = root;
        var sourceDirectory = isAbsolute ? Path.GetDirectoryName(file) : null;
        _sourceGameDataRoot = sourceDirectory;
        _sourceAssetRoot = isAbsolute && string.Equals(Path.GetFileName(sourceDirectory), "GameData", StringComparison.OrdinalIgnoreCase) ? Directory.GetParent(sourceDirectory!)?.FullName ?? sourceDirectory : sourceDirectory;
        var importedWorld = blankWorld ?? (root.Name.LocalName == "World" ? root : root.Element("WorldSettings")?.Element("World") ?? root.Element("GameData")?.Element("WorldSettings")?.Element("World"));
        if (importedWorld == null)
            throw new InvalidDataException("This file does not contain a Stationeers WorldSettings/World element.");
        _world = new XElement(importedWorld);
        RecognizeExportedWorld();
        _startConditionLibrary = new XElement(XDocument.Load(Path.Combine(assets, "Data", "startconditions.xml")).Root!);
        AddImportedStartConditionRecords(root);
        if (isAbsolute)
            ImportStartingEquipmentFiles(file);
        _isBlankProject = blankWorld != null;
        var sourceWeatherFile = isAbsolute ? Path.Combine(Path.GetDirectoryName(file) ?? string.Empty, "weather.xml") : string.Empty;
        _hasImportedWeatherDefinitions = root.Elements("WeatherEvent").Any() || root.Element("GameData")?.Elements("WeatherEvent").Any() == true || (!string.IsNullOrEmpty(sourceWeatherFile) && File.Exists(sourceWeatherFile));
        InitializeMapSettings();
        InitializeWorldFields();
        InitializeAtmosphereAndResourceFields();
        _weatherLibrary = new XElement(XDocument.Load(Path.Combine(assets, "Data", "weather.xml")).Root!);
        InitializeWeatherSettings(root, file, isAbsolute);
        var bodyId = (string? )_world.Element("PlayableBody")?.Attribute("Id");
        var bodyLibrary = XDocument.Load(Path.Combine(assets, "Data", "celestialbodies.xml"));
        _celestialBodyData = bodyLibrary.Root!;
        var body = root.Descendants("CelestialBody").FirstOrDefault(e => (string? )e.Attribute("Id") == bodyId) ?? bodyLibrary.Root!.Element("CelestialBodies")!.Elements("CelestialBody").FirstOrDefault(e => (string? )e.Attribute("Id") == bodyId);
        if (body != null)
        {
            _orbitBody = new XElement(body);
            _hasImportedOrbitBody = root.Descendants("CelestialBody").Any(e => (string? )e.Attribute("Id") == bodyId);
            AddEditableTree(OrbitFields, _orbitBody.Element("Orbit"), "Orbit");
            AddCelestialBodySettings();
        }

        RefreshOptionalSettings();
        InitializeSpaceMapSettings();
    }

    /// <summary>Builds the default XML for a blank world without copying a native planet's terrain or atmosphere.</summary>
    private static XElement CreateBlankWorld()
    {
        return new XElement(
            "World",
            new XAttribute("Id", "WC_Blank"),
            new XAttribute("Priority", "1"),
            new XAttribute("Hidden", "false"),
            new XAttribute("Deprecated", "false"),
            new XElement("Name", new XAttribute("Value", "My World")),
            new XElement("Description", new XAttribute("Value", "A new world for Stationeers.")),
            new XElement("StartCondition", new XAttribute("Id", "DefaultStart"), new XAttribute("IsDefault", "true")),
            new XElement("GlobalAtmosphere", new XElement("Temperature", TemperatureKey("0")), new XElement("Gas")),
            new XElement("SkyBoxMaterialName", "Starfield Skybox"),
            new XElement("SetSunInSkybox", "true"),
            new XElement("SunPrefabName", "MarsSun"),
            new XElement("Gravity", "-9.81"),
            new XElement(
            "CelestialConstants",
            new XAttribute("BodyScale", "1"),
            new XElement("TimeOffset", new XAttribute("Minutes", "0"), new XAttribute("Seconds", "0"))
        ),
            new XElement("PlayableBody", new XAttribute("Id", "Earth"), new XAttribute("Latitude", "0"), new XAttribute("Longitude", "0")),
            new XElement(
            "TerrainSettings",
            new XAttribute("Path", "Worlds/WC_Blank/Terrain"),
            new XAttribute("WorldSize", "4096"),
            new XElement("Curvature", new XAttribute("Value", "0"))
        )
        );
    }

    private static readonly HashSet<string> NonOptionalRecords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "WeatherEvent",
        "Minables",
        "DeepMinables",
        "RegionSet",
        "PointOfInterest",
        "PlayableArea",
        "StartLocation",
        "RandomStartLocation",
        "Spawn",
        "WorldObjective",
        "AchievementChain",
        "AchievementSurvival",
        "CelestialBodies"
    };
    internal bool HasTemperatureCurve => _world.Element("GlobalAtmosphere")?.Element("Temperature") != null;
    internal string TerrainDescription => CompiledTerrainFiles.Count > 0 ? CompiledTerrainFiles.Count.ToString(CultureInfo.InvariantCulture) + " compiled terrain chunk files selected. Export packages them under this world's own GameData/Worlds folder." : TerrainSourceFiles.Count == 0 ? "No custom terrain selected. The exported world will reuse the built-in template's compiled terrain and map assets." : "Selected source maps are included for compilation. Export still reuses the template's compiled terrain until compiled output is selected below.";
    internal IReadOnlyList<SavedWeatherEffect> SavedWeatherEffects => WeatherEffectLibrary.Load();
}
