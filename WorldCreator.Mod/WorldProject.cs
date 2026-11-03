using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WorldCreator.Mod
{
    internal sealed class WorldProject
    {
        internal sealed class OptionalSetting
        {
            internal readonly string Path;
            internal readonly string SourceWorld;
            internal readonly XElement Element;

            internal OptionalSetting(string path, string sourceWorld, XElement element)
            {
                Path = path;
                SourceWorld = sourceWorld;
                Element = new XElement(element);
            }
        }

        internal sealed class MapLayer
        {
            internal sealed class Region
            {
                internal readonly string Id;
                internal readonly int R;
                internal readonly int G;
                internal readonly int B;
                internal readonly string? NameKey;

                internal Region(string id, int r, int g, int b, string? nameKey = null) { Id = id; R = r; G = g; B = b; NameKey = nameKey; }
            }

            internal readonly string Id;
            internal readonly string TexturePath;
            internal readonly List<Region> Regions = new List<Region>();

            internal MapLayer(string id, string texturePath) { Id = id; TexturePath = texturePath; }
        }

        internal sealed class PointOfInterest
        {
            internal readonly string Id;
            internal readonly string RegionId;
            internal readonly string NameKey;

            internal PointOfInterest(string id, string regionId, string nameKey)
            {
                Id = id;
                RegionId = regionId;
                NameKey = nameKey;
            }
        }

        internal sealed class StartConditionView
        {
            internal readonly string Id;
            internal readonly string Name;
            internal readonly bool IsDefault;
            internal readonly bool IsCustom;
            internal readonly bool IsBrutal;
            internal readonly bool IsTerrainEdit;
            internal readonly IReadOnlyList<(string Id, string Event)> Spawns;
            internal readonly IReadOnlyList<StartCrateView> Crates;

            internal StartConditionView(string id, string name, bool isDefault, bool isCustom, bool isBrutal, bool isTerrainEdit, IReadOnlyList<(string Id, string Event)> spawns, IReadOnlyList<StartCrateView> crates)
            {
                Id = id;
                Name = name;
                IsDefault = isDefault;
                IsCustom = isCustom;
                IsBrutal = isBrutal;
                IsTerrainEdit = isTerrainEdit;
                Spawns = spawns;
                Crates = crates;
            }
        }

        internal sealed class StartCrateView
        {
            internal readonly string Id;
            internal readonly string Name;
            internal readonly IReadOnlyList<(string Id, int Quantity)> Items;
            internal StartCrateView(string id, string name, IReadOnlyList<(string Id, int Quantity)> items) { Id = id; Name = name; Items = items; }
        }

        internal sealed class ProjectAsset
        {
            internal readonly string Id;
            internal readonly string Name;
            internal readonly string Category;
            internal readonly string Kind;
            internal readonly string Extension;
            internal readonly byte[] Data;

            internal ProjectAsset(string id, string name, string category, string kind, string extension, byte[] data)
            {
                Id = id;
                Name = name;
                Category = category;
                Kind = kind;
                Extension = extension;
                Data = data;
            }

            internal string RelativePath(string worldId)
            {
                var safeName = Regex.Replace(Path.GetFileNameWithoutExtension(Name), "[^A-Za-z0-9_-]", "_");
                return "Worlds/" + worldId + "/Assets/" + safeName + "_" + Id.Substring(0, 8) + Extension;
            }
        }

        internal sealed class WeatherTiming
        {
            internal readonly string Id;
            internal readonly double CooldownMin;
            internal readonly double CooldownMax;
            internal readonly double DelayMinSeconds;
            internal readonly double DelayMaxSeconds;
            internal readonly double DurationMinSeconds;
            internal readonly double DurationMaxSeconds;

            internal WeatherTiming(string id, XElement element, IEnumerable<Field> fields)
            {
                Id = id;
                var ownFields = fields.Where(f => f.Label.StartsWith(id + "/", StringComparison.Ordinal)).ToList();
                CooldownMin = Read(ownFields, "CoolDown / Min", element.Element("CoolDown"), "Min");
                CooldownMax = Read(ownFields, "CoolDown / Max", element.Element("CoolDown"), "Max");
                DelayMinSeconds = Read(ownFields, "StartDelay / Min", element.Element("StartDelay"), "Min");
                DelayMaxSeconds = Read(ownFields, "StartDelay / Max", element.Element("StartDelay"), "Max");
                DurationMinSeconds = Read(ownFields, "Duration / Min", element.Element("Duration"), "Min");
                DurationMaxSeconds = Read(ownFields, "Duration / Max", element.Element("Duration"), "Max");
            }

            private static double Read(IEnumerable<Field> fields, string suffix, XElement? element, string attribute)
            {
                var field = fields.FirstOrDefault(f => f.Label.EndsWith(suffix, StringComparison.Ordinal));
                if (double.TryParse(field?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var current)) return current;
                return double.TryParse((string?)element?.Attribute(attribute), NumberStyles.Float, CultureInfo.InvariantCulture, out var original) ? original : 0;
            }
        }

        internal sealed class Field
        {
            internal readonly string Label;
            internal readonly string Tooltip;
            internal readonly string Original;
            internal string Value;
            internal readonly bool IsBoolean;
            internal readonly bool IsNumeric;
            private readonly bool _boolean;
            private readonly Action<string> _write;

            internal Field(string label, string value, Action<string> write, bool numeric = true, bool boolean = false, string? tooltip = null)
            {
                Label = label;
                Tooltip = tooltip ?? DescribeField(label);
                Original = value;
                Value = value;
                _write = write;
                IsNumeric = numeric;
                _boolean = boolean;
                IsBoolean = boolean;
            }

            internal void Validate()
            {
                if (Value != Original && IsNumeric)
                {
                    if (!double.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || double.IsNaN(number) || double.IsInfinity(number))
                        throw new InvalidOperationException("Invalid number in " + Label + ". Use a decimal point.");
                    var lower = Label.ToLowerInvariant();
                    if (lower.Contains("/temperature/key") && lower.EndsWith(" / time") && (number < 0 || number > 180))
                        throw new InvalidOperationException("Temperature curve solar angle must be between 0° and 180°: " + Label);
                    if (lower.Contains("/globalatmosphere/temperature/key") && lower.EndsWith(" / value") && number < 0)
                        throw new InvalidOperationException("Absolute base temperature cannot be below 0 K: " + Label);
                    if (lower.Contains("solarradiationtemperature") && lower.Contains("/key") && lower.EndsWith(" / time") && (number < 0 || number > 100))
                        throw new InvalidOperationException("Solar-radiation curve input must be between 0 and 100: " + Label);
                }
                if (Value != Original && _boolean && !bool.TryParse(Value, out _))
                    throw new InvalidOperationException("Enter true or false for " + Label + ".");
                if (Label.Contains("/Gas") && Label.EndsWith(" / Quantity", StringComparison.Ordinal) &&
                    double.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var quantity) && quantity < 0)
                    throw new InvalidOperationException("Gas quantities cannot be negative: " + Label);
            }

            internal void Apply()
            {
                if (Value != Original) _write(Value);
            }
        }

        private static string DescribeField(string label)
        {
            var lower = label.ToLowerInvariant();
            var divider = label.LastIndexOf(" / ", StringComparison.Ordinal);
            var slash = label.LastIndexOf('/');
            var leaf = label.Substring(Math.Max(divider >= 0 ? divider + 3 : 0, slash >= 0 ? slash + 1 : 0)).Trim().TrimStart('@');
            if (leaf.StartsWith("Gravity", StringComparison.OrdinalIgnoreCase)) return "Surface gravity in m/s². Stationeers world data uses negative values; a larger absolute value means stronger pull toward the surface.";
            if (leaf.Equals("Latitude", StringComparison.OrdinalIgnoreCase)) return "Playable landing-site latitude in degrees: −90° is the south pole, 0° the equator and +90° the north pole.";
            if (leaf.Equals("Longitude", StringComparison.OrdinalIgnoreCase)) return "Playable landing-site longitude in degrees around the selected body. This does not change the orbit or spin axis.";
            if (leaf.Equals("SemiMajorAxisAu", StringComparison.OrdinalIgnoreCase)) return "Average orbital distance from the parent body, in astronomical units (AU). This changes the orbit and sunlight distance.";
            if (leaf.Equals("SemiMajorAxisKm", StringComparison.OrdinalIgnoreCase)) return "Average orbital distance from the parent body, in kilometres. This is an alternate unit to AU for the same orbit size.";
            if (leaf.Equals("Period", StringComparison.OrdinalIgnoreCase)) return "Time for one full orbit, in simulated game days.";
            if (leaf.Equals("Eccentricity", StringComparison.OrdinalIgnoreCase)) return "Orbit shape: 0 is circular; values closer to 1 are more elongated. Keep an elliptical orbit below 1.";
            if (leaf.Equals("Inclination", StringComparison.OrdinalIgnoreCase)) return "Angle between this orbit's plane and its reference plane, in degrees.";
            if (leaf.Equals("ArgumentOfPeriapsis", StringComparison.OrdinalIgnoreCase)) return "Orbit orientation angle, in degrees. It sets the closest orbital point within the orbit plane.";
            if (leaf.Equals("LongitudeOfAscendingNode", StringComparison.OrdinalIgnoreCase)) return "Orbit orientation angle, in degrees. It rotates the orbit plane around the reference axis.";
            if (leaf.Equals("LongitudeAtEpoch", StringComparison.OrdinalIgnoreCase)) return "Celestial body's reference position at its orbital epoch, in degrees.";
            if (leaf.Equals("Priority", StringComparison.OrdinalIgnoreCase)) return "World-list priority. It influences this world's placement in the world selection list.";
            if (leaf.Equals("Hidden", StringComparison.OrdinalIgnoreCase)) return "When true, hides this world definition from the world selection menu.";
            if (leaf.Equals("Deprecated", StringComparison.OrdinalIgnoreCase)) return "Marks this world definition as deprecated. Keep the source value unless you intend to flag the world as obsolete.";
            if (leaf.Equals("Id", StringComparison.OrdinalIgnoreCase)) return "Identifier used by the game to connect this record to other world, body or resource data. Changing it can break those references.";
            if (leaf.Equals("Path", StringComparison.OrdinalIgnoreCase)) return "GameData-relative file or folder path used by this setting. The referenced asset must be included in the mod or available in the installed game.";
            if (leaf.Equals("Format", StringComparison.OrdinalIgnoreCase)) return "Texture or asset format identifier expected by the game loader. Keep a format supported by the referenced asset.";
            if (leaf.Equals("LoadType", StringComparison.OrdinalIgnoreCase)) return "Asset loading strategy used by Stationeers. Preserve the source value unless you know the loader behavior.";
            if (leaf.Equals("WorldSize", StringComparison.OrdinalIgnoreCase)) return "Terrain world-size value from the installed world XML. The game does not document this as a direct kilometre measurement.";
            if (leaf.Equals("RadiusKm", StringComparison.OrdinalIgnoreCase)) return "Radius of this celestial body, in kilometres.";
            if (leaf.Equals("CanOccult", StringComparison.OrdinalIgnoreCase)) return "Whether this celestial body may visually occult (cover) another body when they line up in the sky.";
            if (leaf.Equals("WeightedMode", StringComparison.OrdinalIgnoreCase)) return "Unity animation-curve weighting mode for this point. The guided curve editor hides this technical value and preserves the source value.";
            if (leaf.Equals("Days", StringComparison.OrdinalIgnoreCase) && lower.Contains("synodicrotation")) return "Length of one synodic rotation, in game days. It controls the body's day/night rotation relative to its orbit.";
            if ((leaf.Equals("x", StringComparison.OrdinalIgnoreCase) || leaf.Equals("y", StringComparison.OrdinalIgnoreCase) || leaf.Equals("z", StringComparison.OrdinalIgnoreCase)) && lower.Contains("rotation")) return "One component of the celestial rotation axis vector. These three values together set the axis direction.";
            if ((leaf.Equals("R", StringComparison.OrdinalIgnoreCase) || leaf.Equals("G", StringComparison.OrdinalIgnoreCase) || leaf.Equals("B", StringComparison.OrdinalIgnoreCase)) && lower.Contains("region")) return "One color channel (red, green or blue) that identifies this region in its map mask. Changing it can disconnect the region from the texture.";
            if (leaf.Equals("Time", StringComparison.OrdinalIgnoreCase))
            {
                if (lower.Contains("/temperature/key")) return "Solar angle in degrees for the base temperature curve: 0° is sun overhead, 90° is the horizon and 180° is below the opposite side. This is not clock time.";
                if (lower.Contains("solarradiationtemperature")) return "Input position from 0–100 across this orbit's calculated solar-irradiance range. It is not a clock time or percent of Earth's sunlight.";
                if (lower.Contains("ghgtemperatureoffset")) return "Greenhouse-index input for this offset curve. It is not a clock time; preserve the template's domain unless you know the runtime range.";
                if (lower.Contains("densityoffset")) return "Atmospheric gas-density input in mmol/L for this offset curve. It is not a clock time.";
                return "Curve input coordinate. Its meaning depends on the curve named in the setting path; it is not necessarily clock time.";
            }
            if (leaf.Equals("Value", StringComparison.OrdinalIgnoreCase))
            {
                if (lower.Contains("/temperature/key")) return "Absolute baseline atmosphere temperature at this solar angle, in kelvin (K).";
                if (lower.Contains("temperatureoffset") || lower.Contains("solarradiationtemperature") || lower.Contains("ghgtemperatureoffset") || lower.Contains("densityoffset")) return "Temperature adjustment contributed by this curve, in kelvin (K). This is a difference, so do not add 273.15.";
                if (lower.Contains("curvature")) return "Terrain curvature setting from the selected world definition. The game data does not document a physical unit.";
            }
            if (leaf.Equals("Quantity", StringComparison.OrdinalIgnoreCase) && lower.Contains("/gas")) return "Authored gas amount in moles per 8,000-litre atmosphere grid cell. It is an absolute quantity, not a percentage.";
            if (lower.Contains("/volume") && (leaf.Equals("Volume", StringComparison.OrdinalIgnoreCase) || leaf.Equals("Value", StringComparison.OrdinalIgnoreCase))) return "Effective global atmosphere simulation volume, in litres. This is separate from the 8,000-litre atmosphere grid cell.";
            if (lower.Contains("cooldown")) return leaf.Equals("Min", StringComparison.OrdinalIgnoreCase) || leaf.Equals("Max", StringComparison.OrdinalIgnoreCase)
                ? "Minimum/maximum interval in game days between scheduled weather events. A value of 0 does not disable the event; remove the event to exclude it."
                : "Weather cooldown interval, measured in game days.";
            if (lower.Contains("startdelay")) return "Minimum/maximum delay before this weather event starts, in seconds.";
            if (lower.Contains("duration")) return "Minimum/maximum duration of this weather event, in seconds.";
            if (leaf.Equals("OreDensity", StringComparison.OrdinalIgnoreCase)) return "Relative ore-density multiplier for this resource region. It scales the selected template's baseline density.";
            if (leaf.Equals("WindStrength", StringComparison.OrdinalIgnoreCase)) return "Wind-strength parameter used by this weather event. The XML exposes a number, but does not document its unit; compare with installed event examples.";
            if (leaf.Equals("DamageMultiplier", StringComparison.OrdinalIgnoreCase)) return "Multiplier applied to weather damage. 1 is the event's authored baseline; 0 removes this contribution.";
            if (leaf.Equals("MovementSpeedMultiplier", StringComparison.OrdinalIgnoreCase)) return "Multiplier applied to movement speed during this weather event. 1 leaves the authored baseline unchanged.";
            if (leaf.Equals("SolarRatio", StringComparison.OrdinalIgnoreCase)) return "Solar-radiation ratio used by this weather event. Its exact runtime scaling is not documented; compare with installed event values.";
            if (leaf.Equals("BodyScale", StringComparison.OrdinalIgnoreCase)) return "Celestial body visual scale. This is a rendering setting, not orbital distance.";
            if (leaf.Equals("SkyboxScale", StringComparison.OrdinalIgnoreCase)) return "Visual skybox scale. This does not change the physical orbit.";
            if (leaf.Equals("InTangent", StringComparison.OrdinalIgnoreCase) || leaf.Equals("OutTangent", StringComparison.OrdinalIgnoreCase)) return "Animation-curve slope at this key. It controls interpolation into or out of the point; World Creator hides this technical value in the guided curve editor and preserves it.";
            if (leaf.Equals("InWeight", StringComparison.OrdinalIgnoreCase) || leaf.Equals("OutWeight", StringComparison.OrdinalIgnoreCase)) return "Animation-curve interpolation weight at this key. It shapes interpolation; World Creator hides this technical value in the guided curve editor and preserves it.";
            if (leaf.Equals("Min", StringComparison.OrdinalIgnoreCase) || leaf.Equals("Max", StringComparison.OrdinalIgnoreCase)) return "Lower/upper bound for the setting identified by its path. Check the displayed unit and preserve the range order (minimum ≤ maximum).";
            if (lower.Contains("orbit")) return "Orbit setting from the installed celestial-body data. Hovered value: " + leaf + ". Use the displayed unit and preserve the selected body's parent relationship.";
            if (lower.Contains("weather")) return "Weather setting from the installed event definition. Hovered value: " + leaf + ". Its unit or runtime effect is not described by the XML field name; compare against the installed event before changing it.";
            if (lower.Contains("globalatmosphere")) return "Atmosphere setting at " + label + ". The XML stores this value in the global-atmosphere definition; its unit is not identified by the field name, so compare with the selected world before changing it.";
            if (lower.Contains("minables") || lower.Contains("resources")) return "Resource setting for the region identified in its path. The field is preserved from this world definition; compare with the selected template before changing it.";
            if (lower.Contains("terrain")) return "Terrain setting at " + label + ". It controls a terrain asset or rendering parameter; keep referenced assets available in the exported mod.";
            return "Advanced world XML setting: " + label + ". The installed XML does not identify this property's unit or full runtime effect. Its current source value is preserved; compare with the selected world before changing it.";
        }

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
        internal bool HasChanges => WorldFields.Concat(AtmosphereFields).Concat(WeatherFields).Concat(OrbitFields).Concat(ResourceFields).Concat(AdvancedFields)
            .Any(f => f.Value != f.Original) || _structureChanged || _descriptionChanged || TerrainSourceFiles.Count > 0 || CompiledTerrainFiles.Count > 0 || ProjectAssets.Count > 0;
        internal string DisplayName => (string?)_world.Element("Name")?.Attribute("Value") ?? string.Empty;
        internal string WorldDescription => (string?)_world.Element("Description")?.Attribute("Value") ?? string.Empty;
        internal string? MiniMapAssetPath { get; private set; }
        internal string? SourceAssetRoot => _sourceAssetRoot;
        internal string? SourceGameDataRoot => _sourceGameDataRoot;
        internal string PrimaryBodyId => WorldFields.FirstOrDefault(entry => entry.Label.EndsWith(" / Primary body ID", StringComparison.Ordinal))?.Value
            ?? (string?)_world.Element("PrimaryBody")?.Attribute("Id") ?? "Sun";
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
        internal IReadOnlyList<string> WeatherOptions => _weatherLibrary.Elements("WeatherEvent")
            .Select(e => (string?)e.Attribute("Id")).Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToArray();
        internal IReadOnlyList<OptionalSetting> OptionalSettings => _optionalSettings;
        internal IReadOnlyList<WeatherTiming> WeatherTimings => _weather.Select(w => new WeatherTiming((string?)w.Event.Attribute("Id") ?? "Weather", w.Event, WeatherFields)).ToArray();
        internal IReadOnlyList<string> StartConditionOptions => _startConditionLibrary.Elements("StartCondition")
            .Select(e => (string?)e.Attribute("Id") ?? string.Empty).Where(id => id.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        internal IReadOnlyList<string> StartConditionLabels => StartConditionOptions
            .Select(id => _startConditionNames.TryGetValue(id, out var name) && name != id ? name + "  ·  " + id : id).ToArray();
        internal IReadOnlyList<StartConditionView> StartConditions => _world.Elements("StartCondition")
            .Select(reference =>
            {
                var id = (string?)reference.Attribute("Id") ?? string.Empty;
                var definition = FindStartCondition(id);
                return definition == null ? null : new StartConditionView(id,
                    _startConditionNames.TryGetValue(id, out var name) ? name : id,
                    string.Equals((string?)reference.Attribute("IsDefault"), "true", StringComparison.OrdinalIgnoreCase),
                    _customStartConditions.Any(e => (string?)e.Attribute("Id") == id),
                    string.Equals((string?)definition.Attribute("IsBrutal"), "true", StringComparison.OrdinalIgnoreCase),
                    string.Equals((string?)definition.Attribute("IsTerrainEdit"), "true", StringComparison.OrdinalIgnoreCase),
                    definition.Elements("Spawn").Select(e => ((string?)e.Attribute("Id") ?? string.Empty, (string?)e.Attribute("Event") ?? string.Empty)).ToArray(),
                    GetStartConditionCrates(definition));
            }).Where(view => view != null).Cast<StartConditionView>().ToArray();

        internal WorldProject(string assets, string sourceFile, bool isAbsolute = false)
            : this(assets, sourceFile, isAbsolute, null)
        {
        }

        internal static WorldProject CreateBlank(string assets) => new WorldProject(assets, string.Empty, false, CreateBlankWorld());

        internal void SetWorldDescription(string value)
        {
            if (WorldDescription == value) return;
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

        private WorldProject(string assets, string sourceFile, bool isAbsolute, XElement? blankWorld)
        {
            _assets = assets;
            _sourceWorldDirectory = string.IsNullOrEmpty(sourceFile)
                ? string.Empty
                : Path.GetDirectoryName(sourceFile)?.Replace('\\', '/') ?? string.Empty;
            var file = isAbsolute ? sourceFile : Path.Combine(assets, "Worlds", sourceFile);
            var document = blankWorld == null ? XDocument.Load(file, LoadOptions.PreserveWhitespace) : null;
            var root = document?.Root ?? new XElement("GameData");
            _sourceXml = root;
            var sourceDirectory = isAbsolute ? Path.GetDirectoryName(file) : null;
            _sourceGameDataRoot = sourceDirectory;
            _sourceAssetRoot = isAbsolute && string.Equals(Path.GetFileName(sourceDirectory), "GameData", StringComparison.OrdinalIgnoreCase)
                ? Directory.GetParent(sourceDirectory!)?.FullName ?? sourceDirectory
                : sourceDirectory;
            var importedWorld = blankWorld ?? (root.Name.LocalName == "World"
                ? root
                : root.Element("WorldSettings")?.Element("World") ?? root.Element("GameData")?.Element("WorldSettings")?.Element("World"));
            if (importedWorld == null) throw new InvalidDataException("This file does not contain a Stationeers WorldSettings/World element.");
            _world = new XElement(importedWorld);
            _startConditionLibrary = new XElement(XDocument.Load(Path.Combine(assets, "Data", "startconditions.xml")).Root!);
            AddImportedStartConditionRecords(root);
            var importedStartFile = Path.Combine(Path.GetDirectoryName(file) ?? string.Empty, "startconditions.xml");
            if (isAbsolute && File.Exists(importedStartFile))
            {
                try { AddImportedStartConditionRecords(XDocument.Load(importedStartFile).Root!); }
                catch { /* Keep loading the world if an optional startconditions.xml is malformed. */ }
            }
            _isBlankProject = blankWorld != null;
            var sourceWeatherFile = isAbsolute ? Path.Combine(Path.GetDirectoryName(file) ?? string.Empty, "weather.xml") : string.Empty;
            _hasImportedWeatherDefinitions = root.Elements("WeatherEvent").Any() || root.Element("GameData")?.Elements("WeatherEvent").Any() == true ||
                (!string.IsNullOrEmpty(sourceWeatherFile) && File.Exists(sourceWeatherFile));
            AddAllFields(AdvancedFields, _world, "World");
            MapFields.AddRange(AdvancedFields.Where(f =>
                f.Label.IndexOf("/RegionSet", StringComparison.Ordinal) >= 0 ||
                f.Label.IndexOf("/PointOfInterest", StringComparison.Ordinal) >= 0 ||
                f.Label.IndexOf("/PlayableArea", StringComparison.Ordinal) >= 0 ||
                f.Label.IndexOf("/GeographicRegionData", StringComparison.Ordinal) >= 0 ||
                f.Label.IndexOf("/DeepMinablesRegionData", StringComparison.Ordinal) >= 0 ||
                f.Label.IndexOf("/Minables/", StringComparison.Ordinal) >= 0 ||
                f.Label.IndexOf("/DeepMinables/", StringComparison.Ordinal) >= 0));
            var terrain = _world.Element("TerrainSettings");
            if (terrain != null)
            {
                MiniMapAssetPath = ResolveImportedAsset((string?)terrain.Element("MiniMap")?.Attribute("Path"));
                TerrainWorldSize = (string?)terrain.Attribute("WorldSize") ?? "(game default)";
                PointOfInterestCount = _world.Descendants("PointOfInterest").Count();
                foreach (var poi in _world.Descendants("PointOfInterest"))
                {
                    var id = (string?)poi.Attribute("Id");
                    var regionId = (string?)poi.Element("Region")?.Attribute("Id");
                    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(regionId)) continue;
                    PointsOfInterest.Add(new PointOfInterest(id, regionId, (string?)poi.Element("Name")?.Attribute("Key") ?? id));
                }
                foreach (var regionSet in _world.Descendants("RegionSet"))
                {
                    var texturePath = (string?)regionSet.Element("Texture")?.Attribute("Path");
                    if (string.IsNullOrWhiteSpace(texturePath)) continue;
                    var layer = new MapLayer((string?)regionSet.Attribute("Id") ?? "Map layer", ResolveImportedAsset(texturePath) ?? texturePath);
                    foreach (var region in regionSet.Elements("Region"))
                    {
                        var regionId = (string?)region.Attribute("Id");
                        if (string.IsNullOrWhiteSpace(regionId)) continue;
                        layer.Regions.Add(new MapLayer.Region(regionId,
                            (int?)region.Attribute("R") ?? 255,
                            (int?)region.Attribute("G") ?? 255,
                            (int?)region.Attribute("B") ?? 255,
                            (string?)region.Element("Name")?.Attribute("Key")));
                    }
                    MapLayers.Add(layer);
                }
                TerrainSummary.Add("Path: " + ((string?)terrain.Attribute("Path") ?? "(not set)"));
                TerrainSummary.Add("World size: " + ((string?)terrain.Attribute("WorldSize") ?? "(game default)"));
                TerrainSummary.Add("Curvature: " + ((string?)terrain.Element("Curvature")?.Attribute("Value") ?? "(game default)"));
                TerrainSummary.Add("Mini-map: " + ((string?)terrain.Element("MiniMap")?.Attribute("Path") ?? "(not set)"));
                TerrainSummary.Add("Crack map: " + ((string?)terrain.Element("CrackMap")?.Attribute("Path") ?? "(not set)"));
                TerrainSummary.Add("Lava map: " + ((string?)terrain.Element("LavaMap")?.Attribute("Path") ?? "(not set)"));
                TerrainSummary.Add("Backsplash map: " + ((string?)terrain.Element("BacksplashMap")?.Attribute("Path") ?? "(not set)"));
            }
            AddText(WorldFields, _world.Element("Gravity"), "Gravity (negative m/s²)");
            AddEditableAttributes(WorldFields, _world.Element("PlayableBody"), "Location");
            AddWorldField(WorldFields, _world, "", "Priority", "World list priority", "Controls this world's position in the world list.", true);
            AddWorldField(WorldFields, _world, "", "Hidden", "World visibility", "When enabled, this world is hidden from the world selection list.", false, true);
            AddWorldField(WorldFields, _world, "PrimaryBody", "Id", "Primary body ID", "ID of the primary celestial body used for the world's star and lighting reference.");
            AddWorldField(WorldFields, _world, "PrimaryBody", "SolarConstant", "Primary star irradiance (W/m²)", "Reference stellar irradiance. The installed orbital calculation uses this value divided by distance in AU squared.", true);
            AddWorldField(WorldFields, _world, "AmbienceSound", null, "Ambience sound ID", "Installed or mod-provided ambience sound resource used by this world.");
            AddWorldField(WorldFields, _world, "SkyBoxMaterialName", null, "Skybox material ID", "Installed or mod-provided skybox material used by this world.");
            AddWorldField(WorldFields, _world, "SunPrefabName", null, "Sun prefab ID", "Installed or mod-provided sun and light prefab used by this world.");
            AddWorldField(WorldFields, _world, "CelestialConstants", "BodyScale", "Celestial body scale", "Visual scale for celestial bodies; this does not change orbital distance.", true);
            AddWorldField(WorldFields, _world, "CelestialConstants", "SkyboxScale", "Skybox scale", "Visual scale for the skybox; this does not change orbital distance.", true);
            AddWorldField(WorldFields, _world, "CelestialConstants/TimeOffset", "Days", "Clock offset · days", "Day component of the initial celestial clock offset.", true);
            AddWorldField(WorldFields, _world, "CelestialConstants/TimeOffset", "Hours", "Clock offset · hours", "Hour component of the initial celestial clock offset.", true);
            AddWorldField(WorldFields, _world, "CelestialConstants/TimeOffset", "Minutes", "Clock offset · minutes", "Minute component of the initial celestial clock offset.", true);
            AddWorldField(WorldFields, _world, "CelestialConstants/TimeOffset", "Seconds", "Clock offset · seconds", "Second component of the initial celestial clock offset.", true);
            AddWorldField(WorldFields, _world, "MaxSunIntensity", null, "Maximum sun intensity", "Upper bound for apparent sunlight and lighting intensity; solar-panel output is controlled separately.", true, false, "Environment");
            var atmosphere = _world.Element("GlobalAtmosphere");
            if (atmosphere != null)
                AddEditableTree(AtmosphereFields, atmosphere, "Atmosphere");
            foreach (var region in _world.Elements("Minables"))
                AddEditableAttributes(ResourceFields, region, "Surface resources [" + ((string?)region.Attribute("Id") ?? "unnamed") + "]");
            foreach (var region in _world.Elements("DeepMinables"))
                AddEditableAttributes(ResourceFields, region, "Deep resources [" + ((string?)region.Attribute("Id") ?? "unnamed") + "]");

            _weatherLibrary = new XElement(XDocument.Load(Path.Combine(assets, "Data", "weather.xml")).Root!);
            foreach (var embeddedWeather in root.Elements("WeatherEvent"))
                _weatherLibrary.Add(new XElement(embeddedWeather));
            foreach (var embeddedWeather in root.Element("GameData")?.Elements("WeatherEvent") ?? Enumerable.Empty<XElement>())
                _weatherLibrary.Add(new XElement(embeddedWeather));
            var importedWeatherFile = Path.Combine(Path.GetDirectoryName(file) ?? string.Empty, "weather.xml");
            if (isAbsolute && File.Exists(importedWeatherFile))
            {
                try
                {
                    foreach (var eventElement in XDocument.Load(importedWeatherFile).Root?.Elements("WeatherEvent") ?? Enumerable.Empty<XElement>())
                        if (!_weatherLibrary.Elements("WeatherEvent").Any(e => (string?)e.Attribute("Id") == (string?)eventElement.Attribute("Id")))
                            _weatherLibrary.Add(new XElement(eventElement));
                }
                catch { /* An invalid optional weather.xml must not prevent importing the world itself. */ }
            }
            var importedEnglishFile = Path.Combine(Path.GetDirectoryName(file) ?? string.Empty, "Language", "english.xml");
            if (isAbsolute && File.Exists(importedEnglishFile))
            {
                try
                {
                    var interfaceRecords = XDocument.Load(importedEnglishFile).Root?.Element("Interface")?.Elements("Record") ?? Enumerable.Empty<XElement>();
                    var translations = interfaceRecords.Where(r => !string.IsNullOrWhiteSpace((string?)r.Element("Key")))
                        .GroupBy(r => (string)r.Element("Key")!, StringComparer.Ordinal)
                        .ToDictionary(g => g.Key, g => (string?)g.First().Element("Value") ?? g.Key, StringComparer.Ordinal);
                    foreach (var eventElement in _weatherLibrary.Elements("WeatherEvent"))
                    {
                        var weatherId = (string?)eventElement.Attribute("Id");
                        var nameKey = (string?)eventElement.Element("Name")?.Attribute("Key");
                        if (weatherId != null && nameKey != null && translations.TryGetValue(nameKey, out var translatedName))
                            _weatherNames[weatherId] = translatedName;
                    }
                    foreach (var condition in _customStartConditions)
                    {
                        var id = (string?)condition.Attribute("Id");
                        var nameKey = (string?)condition.Element("Name")?.Attribute("Key");
                        if (id != null && nameKey != null && translations.TryGetValue(nameKey, out var translatedName))
                            _startConditionNames[id] = translatedName;
                    }
                    foreach (var spawn in _customSpawnDefinitions)
                    {
                        var id = (string?)spawn.Attribute("Id");
                        var nameKey = (string?)spawn.Element("DynamicThing")?.Element("Name")?.Attribute("Key");
                        if (id != null && nameKey != null && translations.TryGetValue(nameKey, out var translatedName))
                            _spawnNames[id] = translatedName;
                    }
                }
                catch { /* Preserve the world even when a mod's optional localization file is malformed. */ }
            }
            foreach (var saved in WeatherEffectLibrary.Load())
            {
                if (!_weatherLibrary.Elements("WeatherEvent").Any(e => (string?)e.Attribute("Id") == saved.Id))
                    _weatherLibrary.Add(new XElement(saved.Definition));
                _weatherNames[saved.Id] = saved.Name;
                _userWeatherIds.Add(saved.Id);
            }
            foreach (var reference in _world.Elements("WeatherEvent"))
            {
                var id = (string?)reference.Attribute("Id");
                var definition = _weatherLibrary.Elements("WeatherEvent").FirstOrDefault(e => (string?)e.Attribute("Id") == id);
                if (definition == null) continue;
                var copy = new XElement(definition);
                _weather.Add((reference, copy));
                AddEditableTree(WeatherFields, copy, id ?? "Weather");
            }

            var bodyId = (string?)_world.Element("PlayableBody")?.Attribute("Id");
            var bodyLibrary = XDocument.Load(Path.Combine(assets, "Data", "celestialbodies.xml"));
            _celestialBodyData = bodyLibrary.Root!;
            var body = root.Descendants("CelestialBody").FirstOrDefault(e => (string?)e.Attribute("Id") == bodyId)
                ?? bodyLibrary.Root!.Element("CelestialBodies")!.Elements("CelestialBody").FirstOrDefault(e => (string?)e.Attribute("Id") == bodyId);
            if (body != null)
            {
                _orbitBody = new XElement(body);
                _hasImportedOrbitBody = root.Descendants("CelestialBody").Any(e => (string?)e.Attribute("Id") == bodyId);
                AddEditableTree(OrbitFields, _orbitBody.Element("Orbit"), "Orbit");
                AddCelestialBodySettings();
            }
            RefreshOptionalSettings();
        }

        private static XElement CreateBlankWorld()
        {
            return new XElement("World",
                new XAttribute("Id", "WC_Blank"),
                new XAttribute("Priority", "1"),
                new XAttribute("Hidden", "false"),
                new XAttribute("Deprecated", "false"),
                new XElement("Name", new XAttribute("Value", "My World")),
                new XElement("Description", new XAttribute("Value", "A new world for Stationeers.")),
                new XElement("StartCondition", new XAttribute("Id", "DefaultStart"), new XAttribute("IsDefault", "true")),
                new XElement("GlobalAtmosphere",
                    new XElement("Temperature", new XElement("Key",
                        new XAttribute("Time", "0"), new XAttribute("Value", "273.15"),
                        new XAttribute("InTangent", "0"), new XAttribute("OutTangent", "0"),
                        new XAttribute("InWeight", "0"), new XAttribute("OutWeight", "0"),
                        new XAttribute("WeightedMode", "None"))),
                    new XElement("Gas")),
                new XElement("SkyBoxMaterialName", "Starfield Skybox"),
                new XElement("SetSunInSkybox", "true"),
                new XElement("SunPrefabName", "MarsSun"),
                new XElement("Gravity", "-9.81"),
                new XElement("CelestialConstants", new XAttribute("BodyScale", "1"),
                    new XElement("TimeOffset", new XAttribute("Minutes", "0"), new XAttribute("Seconds", "0"))),
                new XElement("PlayableBody", new XAttribute("Id", "Earth"), new XAttribute("Latitude", "0"), new XAttribute("Longitude", "0")),
                new XElement("TerrainSettings", new XAttribute("Path", "Worlds/WC_Blank/Terrain"), new XAttribute("WorldSize", "4096"),
                    new XElement("Curvature", new XAttribute("Value", "0"))));
        }

        private static readonly HashSet<string> NonOptionalRecords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "WeatherEvent", "Minables", "DeepMinables", "RegionSet", "PointOfInterest", "PlayableArea",
            "StartLocation", "RandomStartLocation", "Spawn", "WorldObjective", "AchievementChain",
            "AchievementSurvival", "CelestialBodies"
        };

        private string? ResolveImportedAsset(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || string.IsNullOrEmpty(_sourceAssetRoot)) return path;
            var fullPath = Path.Combine(_sourceAssetRoot, path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath)) return fullPath;
            if (!string.IsNullOrEmpty(_sourceGameDataRoot))
            {
                fullPath = Path.Combine(_sourceGameDataRoot, path.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(fullPath)) return fullPath;
            }
            return path;
        }

        internal string? GetPrimaryBodyTexturePath(string id)
        {
            var sourceDefinition = FindPrimaryBodyDefinition(_sourceXml, id);
            var texturePath = (string?)(sourceDefinition ?? FindPrimaryBodyDefinition(_celestialBodyData, id))?.Element("Texture")?.Attribute("Path");
            if (string.IsNullOrWhiteSpace(texturePath) || Path.IsPathRooted(texturePath)) return texturePath;
            if (sourceDefinition != null) return ResolveImportedAsset(texturePath);
            var gameAsset = Path.Combine(_assets, texturePath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(gameAsset) ? gameAsset : texturePath;
        }

        internal (float R, float G, float B)? GetPrimaryBodyColor(string id)
        {
            var color = FindPrimaryBodyDefinition(id)?.Element("Color");
            if (color == null || !float.TryParse((string?)color.Attribute("r"), NumberStyles.Float, CultureInfo.InvariantCulture, out var r)
                || !float.TryParse((string?)color.Attribute("g"), NumberStyles.Float, CultureInfo.InvariantCulture, out var g)
                || !float.TryParse((string?)color.Attribute("b"), NumberStyles.Float, CultureInfo.InvariantCulture, out var b)) return null;
            if (r > 1 || g > 1 || b > 1) { r /= 255f; g /= 255f; b /= 255f; }
            return (Math.Clamp(r, 0, 1), Math.Clamp(g, 0, 1), Math.Clamp(b, 0, 1));
        }

        internal bool IsBlackHolePrimary(string id)
        {
            if (id.IndexOf("blackhole", StringComparison.OrdinalIgnoreCase) >= 0 || id.IndexOf("black hole", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            var primaryBackdrop = _world.Descendants("PlanetPrefab").Elements("Name").Any(name => string.Equals(name.Value, id, StringComparison.OrdinalIgnoreCase));
            return primaryBackdrop && _world.Descendants("PreviewScene").Descendants("Prefab").Elements("Name")
                .Any(name => name.Value.IndexOf("blackhole", StringComparison.OrdinalIgnoreCase) >= 0 || name.Value.IndexOf("black hole", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        internal bool IsStarPrimary(string id)
        {
            return id.Equals("Sun", StringComparison.OrdinalIgnoreCase) || id.IndexOf("star", StringComparison.OrdinalIgnoreCase) >= 0
                || FindPrimaryBodyDefinition(id)?.Name.LocalName.Equals("Star", StringComparison.OrdinalIgnoreCase) == true;
        }

        private XElement? FindPrimaryBodyDefinition(string id)
        {
            return FindPrimaryBodyDefinition(_sourceXml, id) ?? FindPrimaryBodyDefinition(_celestialBodyData, id);
        }

        private static XElement? FindPrimaryBodyDefinition(XElement root, string id)
        {
            return root.DescendantsAndSelf().FirstOrDefault(element => string.Equals((string?)element.Attribute("Id"), id, StringComparison.OrdinalIgnoreCase)
                    && (element.Element("Texture") != null || element.Element("Color") != null));
        }

        private void RefreshOptionalSettings()
        {
            _optionalSettings.Clear();
            var found = new HashSet<string>(StringComparer.Ordinal);
            var worldDirectory = Path.Combine(_assets, "Worlds");
            foreach (var sourceFile in Directory.GetFiles(worldDirectory, "*.xml", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var sourceWorld = XDocument.Load(sourceFile).Root?.Element("WorldSettings")?.Element("World");
                    if (sourceWorld == null) continue;
                    CollectOptionalSettings(sourceWorld, _world, "World", Path.GetFileNameWithoutExtension(sourceFile), found);
                }
                catch { /* Ignore unrelated or malformed XML in the world's asset tree. */ }
            }
            var atmosphere = _world.Element("GlobalAtmosphere");
            if (atmosphere != null && atmosphere.Element("Volume") == null && found.Add("World/GlobalAtmosphere/Volume"))
                _optionalSettings.Insert(0, new OptionalSetting("World/GlobalAtmosphere/Volume", "serializer default", new XElement("Volume", new XAttribute("Value", "40000000000"))));
        }

        private void CollectOptionalSettings(XElement sourceParent, XElement targetParent, string parentPath, string sourceWorld, HashSet<string> found)
        {
            foreach (var sourceChild in sourceParent.Elements())
            {
                if (sourceParent.Elements(sourceChild.Name).Count() != 1) continue;
                var path = parentPath + "/" + sourceChild.Name.LocalName;
                if (NonOptionalRecords.Contains(sourceChild.Name.LocalName) || sourceChild.DescendantsAndSelf().Attributes().Any(a => a.Name.LocalName.Equals("Id", StringComparison.OrdinalIgnoreCase))) continue;

                var targetChildren = targetParent.Elements(sourceChild.Name).ToList();
                if (targetChildren.Count == 0 && found.Add(path))
                    _optionalSettings.Add(new OptionalSetting(path, sourceWorld, sourceChild));
                else if (targetChildren.Count == 1)
                    CollectOptionalSettings(sourceChild, targetChildren[0], path, sourceWorld, found);
            }
        }

        internal void AddOptionalSetting(string path)
        {
            var setting = _optionalSettings.FirstOrDefault(s => s.Path == path)
                ?? throw new InvalidOperationException("That optional setting is no longer available.");
            var segments = path.Split('/');
            XElement parent = _world;
            for (var i = 1; i < segments.Length - 1; i++)
            {
                var matches = parent.Elements(segments[i]).ToList();
                if (matches.Count != 1) throw new InvalidOperationException("The setting's parent is not unique: " + string.Join("/", segments.Take(i + 1)));
                parent = matches[0];
            }
            if (parent.Elements(segments[segments.Length - 1]).Any()) throw new InvalidOperationException("The setting already exists in this world.");
            var added = new XElement(setting.Element);
            parent.Add(added);
            AddAllFields(AdvancedFields, added, string.Join("/", segments.Take(segments.Length - 1)));
            if (path.Contains("/GlobalAtmosphere/")) AddEditableTree(AtmosphereFields, added, "Atmosphere/GlobalAtmosphere");
            MapFields.AddRange(AdvancedFields.Where(f => !MapFields.Contains(f) &&
                (f.Label.IndexOf("/RegionSet", StringComparison.Ordinal) >= 0 || f.Label.IndexOf("/PointOfInterest", StringComparison.Ordinal) >= 0 ||
                 f.Label.IndexOf("/PlayableArea", StringComparison.Ordinal) >= 0 || f.Label.IndexOf("/GeographicRegionData", StringComparison.Ordinal) >= 0 ||
                 f.Label.IndexOf("/DeepMinablesRegionData", StringComparison.Ordinal) >= 0 || f.Label.IndexOf("/Minables/", StringComparison.Ordinal) >= 0 ||
                 f.Label.IndexOf("/DeepMinables/", StringComparison.Ordinal) >= 0)));
            _structureChanged = true;
            RefreshOptionalSettings();
        }

        internal void AddGas(string type, double quantity)
        {
            if (double.IsNaN(quantity) || double.IsInfinity(quantity) || quantity < 0)
                throw new InvalidOperationException("Gas quantity must be a finite, non-negative number of moles.");
            var atmosphere = _world.Element("GlobalAtmosphere") ?? throw new InvalidOperationException("The template has no global atmosphere definition.");
            var gas = atmosphere.Element("Gas");
            if (gas == null) { gas = new XElement("Gas"); atmosphere.Add(gas); }
            if (gas.Elements("Gas").Any(e => (string?)e.Attribute("Type") == type))
                throw new InvalidOperationException(type + " is already in the atmosphere.");
            var entry = new XElement("Gas", new XAttribute("Type", type), new XAttribute("Quantity", quantity.ToString("0.########", CultureInfo.InvariantCulture)));
            gas.Add(entry);
            AddEditableTree(AtmosphereFields, entry, "Atmosphere/GlobalAtmosphere/Gas");
            _structureChanged = true;
        }

        internal bool HasTemperatureCurve => _world.Element("GlobalAtmosphere")?.Element("Temperature") != null;

        internal void SetTerrainSource(string kind, string path)
        {
            if (kind != "Heightmap" && kind != "Crust" && kind != "Cracks" && kind != "Lava")
                throw new InvalidOperationException("Unknown terrain source type.");
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) throw new FileNotFoundException("Terrain source file not found.", fullPath);
            if (kind == "Heightmap")
            {
                var settings = _world.Element("TerrainSettings") ?? throw new InvalidOperationException("The template has no TerrainSettings.");
                var size = (int?)settings.Attribute("WorldSize") ?? 4096;
                if (new FileInfo(fullPath).Length != (long)size * size * 4)
                    throw new InvalidOperationException("Heightmap must be a headerless " + size + "x" + size + " little-endian float32 file (" + ((long)size * size * 4).ToString("N0", CultureInfo.InvariantCulture) + " bytes). The game terrain tool is the source of truth for its terrain size.");
                ValidateHeightmapSamples(fullPath);
            }
            else if (!string.Equals(Path.GetExtension(fullPath), ".png", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(kind + " source must be a PNG image.");
            TerrainSourceFiles[kind] = fullPath;
            _structureChanged = true;
        }

        private static void ValidateHeightmapSamples(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                var buffer = new byte[64 * 1024];
                var remainder = 0;
                long sample = 0;
                while (true)
                {
                    var read = stream.Read(buffer, remainder, buffer.Length - remainder);
                    if (read == 0) break;
                    var bytes = remainder + read;
                    var completeBytes = bytes - bytes % 4;
                    for (var offset = 0; offset < completeBytes; offset += 4, sample++)
                    {
                        var value = BitConverter.ToSingle(buffer, offset);
                        if (float.IsNaN(value) || float.IsInfinity(value))
                            throw new InvalidOperationException("Heightmap contains a non-finite float at sample " + sample.ToString(CultureInfo.InvariantCulture) + ".");
                    }
                    remainder = bytes - completeBytes;
                    if (remainder > 0) Array.Copy(buffer, completeBytes, buffer, 0, remainder);
                }
            }
        }

        internal void SetCompiledTerrainFolder(string folder)
        {
            var fullPath = Path.GetFullPath(folder);
            if (!Directory.Exists(fullPath)) throw new DirectoryNotFoundException("Compiled terrain folder not found.");
            var files = Directory.GetFiles(fullPath, "Terrain*.dat", SearchOption.TopDirectoryOnly)
                .Where(f => Regex.IsMatch(Path.GetFileName(f), @"^Terrain\d+\.dat$", RegexOptions.IgnoreCase))
                .OrderBy(f => int.Parse(Regex.Match(Path.GetFileName(f), @"\d+").Value, CultureInfo.InvariantCulture)).ToList();
            if (files.Count == 0) throw new InvalidOperationException("Folder must contain at least one indexed Terrain<number>.dat file.");
            if (files.Any(f => new FileInfo(f).Length == 0)) throw new InvalidOperationException("Compiled terrain files cannot be empty.");
            CompiledTerrainFiles.Clear();
            CompiledTerrainFiles.AddRange(files);
            _structureChanged = true;
        }

        internal void SetMapLayerTexture(string layerId, byte[] png)
        {
            if (!MapLayers.Any(layer => layer.Id == layerId)) throw new InvalidOperationException("Unknown map layer: " + layerId);
            if (png == null || png.Length < 8 || png[0] != 137 || png[1] != 80 || png[2] != 78 || png[3] != 71 || png[4] != 13 || png[5] != 10 || png[6] != 26 || png[7] != 10)
                throw new InvalidOperationException("Map layer image must be a PNG.");
            _mapLayerTextures[layerId] = (byte[])png.Clone();
            _structureChanged = true;
        }

        internal void ClearMapLayerTexture(string layerId)
        {
            if (!MapLayers.Any(layer => layer.Id == layerId)) throw new InvalidOperationException("Unknown map layer: " + layerId);
            if (_mapLayerTextures.Remove(layerId)) _structureChanged = true;
        }

        internal string AddGeographicLayer(byte[] initialPng)
        {
            if (_world.Element("GeographicRegionData") != null)
                throw new InvalidOperationException("This world already has a geographic layer. Add a region to that layer instead.");
            var suffix = 1;
            string id;
            do { id = "WCGeographicRegions" + (suffix++).ToString(CultureInfo.InvariantCulture); }
            while (_world.Descendants("RegionSet").Any(regionSet => (string?)regionSet.Attribute("Id") == id));
            var regionId = id + "World";
            var nameKey = regionId + "Name";
            var regionSet = new XElement("RegionSet", new XAttribute("Id", id),
                new XElement("Texture", new XAttribute("Path", "Worlds/WC_Blank/Textures/GeographicRegions.png"), new XAttribute("Format", "RGB24"), new XAttribute("LoadType", "OnRequest")),
                new XElement("Region", new XAttribute("Id", regionId), new XAttribute("R", "0"), new XAttribute("G", "255"), new XAttribute("B", "0"),
                    new XElement("Name", new XAttribute("Key", nameKey))));
            var terrainSettings = _world.Element("TerrainSettings");
            if (terrainSettings == null) _world.Add(regionSet);
            else terrainSettings.AddBeforeSelf(regionSet);
            var geographicData = new XElement("GeographicRegionData", new XElement("DefaultRegionName", new XAttribute("Value", "World")), new XElement("RegionSet", new XAttribute("Id", id)));
            if (terrainSettings == null) _world.Add(geographicData);
            else terrainSettings.AddBeforeSelf(geographicData);
            var layer = new MapLayer(id, "Worlds/WC_Blank/Textures/GeographicRegions.png");
            layer.Regions.Add(new MapLayer.Region(regionId, 0, 255, 0, nameKey));
            MapLayers.Add(layer);
            _mapRegionNames.Add(nameKey, "World");
            SetMapLayerTexture(id, initialPng);
            return id;
        }

        internal string AddGeographicRegion(string layerId)
        {
            var layer = MapLayers.FirstOrDefault(item => item.Id == layerId)
                ?? throw new InvalidOperationException("Select a map layer first.");
            if ((string?)_world.Element("GeographicRegionData")?.Element("RegionSet")?.Attribute("Id") != layerId)
                throw new InvalidOperationException("Only the world's geographic region layer supports named regions.");
            var suffix = 1;
            string regionId;
            do { regionId = "WCRegion" + (suffix++).ToString(CultureInfo.InvariantCulture); }
            while (layer.Regions.Any(region => region.Id == regionId));
            var colorIndex = 0;
            (int, int, int) color = (0, 0, 0);
            do
            {
                if (colorIndex > 0xFFFFFF) throw new InvalidOperationException("This geographic layer has used every available RGB color.");
                color = (colorIndex & 255, (colorIndex >> 8) & 255, (colorIndex >> 16) & 255);
                colorIndex++;
            } while (layer.Regions.Any(region => region.R == color.Item1 && region.G == color.Item2 && region.B == color.Item3));
            var nameKey = regionId + "Name";
            var definition = _world.Descendants("RegionSet").First(element => (string?)element.Attribute("Id") == layerId);
            definition.Add(new XElement("Region", new XAttribute("Id", regionId), new XAttribute("R", color.Item1), new XAttribute("G", color.Item2), new XAttribute("B", color.Item3),
                new XElement("Name", new XAttribute("Key", nameKey))));
            layer.Regions.Add(new MapLayer.Region(regionId, color.Item1, color.Item2, color.Item3, nameKey));
            _mapRegionNames.Add(nameKey, "Region " + (layer.Regions.Count - 1).ToString(CultureInfo.InvariantCulture));
            _structureChanged = true;
            return regionId;
        }

        internal string GetMapRegionName(MapLayer.Region region) => region.NameKey != null && _mapRegionNames.TryGetValue(region.NameKey, out var name) ? name : region.Id;

        internal void SetMapRegionName(MapLayer.Region region, string name)
        {
            if (region.NameKey == null || !_mapRegionNames.ContainsKey(region.NameKey)) return;
            if (_mapRegionNames[region.NameKey] == name) return;
            _mapRegionNames[region.NameKey] = name;
            _structureChanged = true;
        }

        internal bool IsGeographicLayer(string layerId) =>
            (string?)_world.Element("GeographicRegionData")?.Element("RegionSet")?.Attribute("Id") == layerId;

        internal void AddLibraryAsset(LibraryAsset asset)
        {
            if (ProjectAssets.Any(existing => existing.Id == asset.Id)) throw new InvalidOperationException("This asset is already in the project.");
            if (!File.Exists(asset.FullPath)) throw new FileNotFoundException("The library copy is missing.", asset.FullPath);
            var data = File.ReadAllBytes(asset.FullPath);
            ProjectAssets.Add(new ProjectAsset(asset.Id, asset.Name, asset.Category, asset.Kind, Path.GetExtension(asset.FileName).ToLowerInvariant(), data));
            _structureChanged = true;
        }

        internal void RemoveProjectAsset(string id)
        {
            var asset = ProjectAssets.FirstOrDefault(existing => existing.Id == id)
                ?? throw new InvalidOperationException("This asset is not in the project.");
            ProjectAssets.Remove(asset);
            _structureChanged = true;
        }

        internal string TerrainDescription => CompiledTerrainFiles.Count > 0
            ? CompiledTerrainFiles.Count.ToString(CultureInfo.InvariantCulture) + " compiled terrain chunk files selected. Export packages them under this world's own GameData/Worlds folder."
            : TerrainSourceFiles.Count == 0
                ? "No custom terrain selected. The exported world will reuse the built-in template's compiled terrain and map assets."
                : "Selected source maps are included for compilation. Export still reuses the template's compiled terrain until compiled output is selected below.";

        internal void AddTemperatureCurve()
        {
            var atmosphere = _world.Element("GlobalAtmosphere") ?? throw new InvalidOperationException("The template has no global atmosphere definition.");
            if (HasTemperatureCurve) throw new InvalidOperationException("This world already has a temperature curve.");
            var curve = new XElement("Temperature",
                TemperatureKey("0"), TemperatureKey("180"));
            atmosphere.AddFirst(curve);
            AddEditableTree(AtmosphereFields, curve, "Atmosphere/GlobalAtmosphere");
            _structureChanged = true;
        }

        internal void AddCurveKey(List<Field> fields, string section, string curvePath)
        {
            var curve = FindEditableCurve(section, curvePath, out var worldPath);
            CommitCurveFields(fields, curvePath);
            var keys = curve.Elements("Key").ToArray();
            var input = NextCurveInput(curve, worldPath);
            var key = keys.Length == 0
                ? new XElement("Key", new XAttribute("Time", input), new XAttribute("Value", "0"),
                    new XAttribute("InTangent", "0"), new XAttribute("OutTangent", "0"),
                    new XAttribute("InWeight", "0"), new XAttribute("OutWeight", "0"), new XAttribute("WeightedMode", "None"))
                : new XElement(keys[keys.Length - 1]);
            key.SetAttributeValue("Time", input);
            var next = curve.Elements("Key").FirstOrDefault(existing =>
                double.TryParse((string?)existing.Attribute("Time"), NumberStyles.Float, CultureInfo.InvariantCulture, out var time) &&
                double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var newTime) && time > newTime);
            if (next == null) curve.Add(key); else next.AddBeforeSelf(key);
            RebuildCurveFields(fields, curve, curvePath, worldPath);
            _structureChanged = true;
        }

        internal void AddTemperatureKey(List<Field> fields, string curvePath, double solarAngle, double temperatureKelvin)
        {
            if (double.IsNaN(solarAngle) || double.IsInfinity(solarAngle) || solarAngle < 0 || solarAngle > 180)
                throw new InvalidOperationException("Temperature curve time must be between 0° and 180° solar angle.");
            if (double.IsNaN(temperatureKelvin) || double.IsInfinity(temperatureKelvin) || temperatureKelvin < 0)
                throw new InvalidOperationException("Temperature must be zero kelvin or higher.");
            var curve = FindEditableCurve("Atmosphere", curvePath, out var worldPath);
            if (!worldPath.EndsWith("/Temperature", StringComparison.Ordinal)) throw new InvalidOperationException("Only the base temperature curve accepts chart points.");
            CommitCurveFields(fields, curvePath);
            if (curve.Elements("Key").Any(key => double.TryParse((string?)key.Attribute("Time"), NumberStyles.Float, CultureInfo.InvariantCulture, out var time) && Math.Abs(time - solarAngle) < 0.001))
                throw new InvalidOperationException("A temperature key already exists at that solar angle.");
            var template = curve.Elements("Key").OrderBy(key => Math.Abs(((double?)key.Attribute("Time") ?? 0) - solarAngle)).FirstOrDefault();
            var added = template == null
                ? new XElement("Key", new XAttribute("InTangent", "0"), new XAttribute("OutTangent", "0"), new XAttribute("InWeight", "0"), new XAttribute("OutWeight", "0"), new XAttribute("WeightedMode", "None"))
                : new XElement(template);
            added.SetAttributeValue("Time", solarAngle.ToString("0.########", CultureInfo.InvariantCulture));
            added.SetAttributeValue("Value", temperatureKelvin.ToString("0.########", CultureInfo.InvariantCulture));
            var next = curve.Elements("Key").FirstOrDefault(key => (double?)key.Attribute("Time") > solarAngle);
            if (next == null) curve.Add(added); else next.AddBeforeSelf(added);
            RebuildCurveFields(fields, curve, curvePath, worldPath);
            _structureChanged = true;
        }

        internal void RemoveCurveKey(List<Field> fields, string section, string curvePath, int index)
        {
            var curve = FindEditableCurve(section, curvePath, out var worldPath);
            var keys = curve.Elements("Key").ToArray();
            if (keys.Length <= 1) throw new InvalidOperationException("A curve must keep at least one key.");
            if (index < 1 || index > keys.Length) throw new InvalidOperationException("This curve key no longer exists.");
            CommitCurveFields(fields, curvePath);
            keys[index - 1].Remove();
            RebuildCurveFields(fields, curve, curvePath, worldPath);
            _structureChanged = true;
        }

        private XElement FindEditableCurve(string section, string curvePath, out string worldPath)
        {
            XElement root;
            string relativePath;
            if (section == "Atmosphere")
            {
                const string prefix = "Atmosphere/";
                if (!curvePath.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException("Atmosphere curve path is invalid.");
                relativePath = curvePath.Substring(prefix.Length);
                worldPath = relativePath;
                root = _world;
            }
            else if (section == "Weather")
            {
                var separator = curvePath.IndexOf('/');
                if (separator <= 0) throw new InvalidOperationException("Weather curve path is invalid.");
                var weatherId = curvePath.Substring(0, separator);
                var effect = _weather.FirstOrDefault(w => (string?)w.Event.Attribute("Id") == weatherId).Event
                    ?? throw new InvalidOperationException("Weather effect is not assigned to this world.");
                root = effect;
                relativePath = curvePath.Substring(separator + 1);
                worldPath = string.Empty;
            }
            else throw new InvalidOperationException("Curve editing is supported on Atmosphere and Weather pages.");

            XElement current = root;
            foreach (var segment in relativePath.Split('/'))
            {
                var match = Regex.Match(segment, @"^(.*?) #(\d+)$");
                var name = match.Success ? match.Groups[1].Value : segment;
                var occurrence = match.Success && int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : 1;
                current = current.Elements(name).ElementAtOrDefault(occurrence - 1)
                    ?? throw new InvalidOperationException("Curve no longer exists: " + curvePath);
            }
            return current;
        }

        private void CommitCurveFields(IEnumerable<Field> fields, string curvePath)
        {
            var prefix = curvePath + "/Key #";
            var curveFields = fields.Where(field => field.Label.Replace(" / ", "/").StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            foreach (var field in curveFields) field.Validate();
            foreach (var field in curveFields) field.Apply();
        }

        private void RebuildCurveFields(List<Field> fields, XElement curve, string curvePath, string worldPath)
        {
            var normalizedPath = curvePath + "/Key #";
            fields.RemoveAll(field => field.Label.Replace(" / ", "/").StartsWith(normalizedPath, StringComparison.Ordinal));
            var parentLabelPath = curvePath.Substring(0, curvePath.LastIndexOf('/'));
            AddEditableTree(fields, curve, parentLabelPath);
            if (worldPath.Length == 0) return;
            var advancedCurvePath = "World/" + worldPath;
            var advancedPrefix = advancedCurvePath + "/Key #";
            AdvancedFields.RemoveAll(field => field.Label.StartsWith(advancedPrefix, StringComparison.Ordinal));
            foreach (var key in curve.Elements("Key")) AddAllFields(AdvancedFields, key, advancedCurvePath);
        }

        private static string NextCurveInput(XElement curve, string worldPath)
        {
            var values = curve.Elements("Key").Select(key => double.TryParse((string?)key.Attribute("Time"), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
                !double.IsNaN(value) && !double.IsInfinity(value) ? (double?)value : null).Where(value => value.HasValue).Select(value => value!.Value).OrderBy(value => value).ToArray();
            if (worldPath.EndsWith("/Temperature", StringComparison.Ordinal) || worldPath.IndexOf("SolarRadiationTemperature", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var maximum = worldPath.EndsWith("/Temperature", StringComparison.Ordinal) ? 180d : 100d;
                var points = new[] { 0d }.Concat(values.Where(value => value > 0 && value < maximum)).Concat(new[] { maximum }).Distinct().OrderBy(value => value).ToArray();
                var bestStart = 0d;
                var bestGap = -1d;
                for (var i = 1; i < points.Length; i++)
                {
                    var gap = points[i] - points[i - 1];
                    if (gap > bestGap) { bestGap = gap; bestStart = points[i - 1]; }
                }
                return (bestStart + bestGap / 2).ToString("0.########", CultureInfo.InvariantCulture);
            }
            if (values.Length == 0) return "0";
            var span = values[values.Length - 1] - values[0];
            return (values[values.Length - 1] + Math.Max(1, span * 0.1)).ToString("0.########", CultureInfo.InvariantCulture);
        }

        private static XElement TemperatureKey(string angle) => new XElement("Key",
            new XAttribute("Time", angle), new XAttribute("Value", "273.15"),
            new XAttribute("InTangent", "0"), new XAttribute("OutTangent", "0"),
            new XAttribute("InWeight", "0"), new XAttribute("OutWeight", "0"),
            new XAttribute("WeightedMode", "None"));

        internal bool HasWeather(string id) => _weather.Any(w => (string?)w.Event.Attribute("Id") == id);

        private void AddImportedStartConditionRecords(XElement root)
        {
            var data = root.Name.LocalName == "GameData" ? root : root.Element("GameData");
            foreach (var element in (data?.Elements("StartCondition") ?? Enumerable.Empty<XElement>()).Concat(data?.Elements("Spawn") ?? Enumerable.Empty<XElement>()))
            {
                var id = (string?)element.Attribute("Id");
                if (string.IsNullOrWhiteSpace(id)) continue;
                var collection = element.Name.LocalName == "StartCondition" ? "StartCondition" : "Spawn";
                if (!_startConditionLibrary.Elements(collection).Any(existing => (string?)existing.Attribute("Id") == id))
                    _startConditionLibrary.Add(new XElement(element));
                if (collection == "StartCondition" && !_customStartConditions.Any(existing => (string?)existing.Attribute("Id") == id))
                    _customStartConditions.Add(new XElement(element));
                if (collection == "Spawn" && !_customSpawnDefinitions.Any(existing => (string?)existing.Attribute("Id") == id))
                    _customSpawnDefinitions.Add(new XElement(element));
            }
        }

        private XElement? FindStartCondition(string id) => _customStartConditions.FirstOrDefault(e => (string?)e.Attribute("Id") == id)
            ?? _startConditionLibrary.Elements("StartCondition").FirstOrDefault(e => (string?)e.Attribute("Id") == id);

        internal void AddStartConditionFrom(string sourceId)
        {
            var source = FindStartCondition(sourceId) ?? throw new InvalidOperationException("Starting profile not found: " + sourceId);
            var copy = CreateStartConditionCopy(source, sourceId + " custom");
            _customStartConditions.Add(copy);
            var reference = new XElement("StartCondition", new XAttribute("Id", (string)copy.Attribute("Id")!));
            if (!_world.Elements("StartCondition").Any(e => string.Equals((string?)e.Attribute("IsDefault"), "true", StringComparison.OrdinalIgnoreCase)))
                reference.SetAttributeValue("IsDefault", true);
            _world.Add(reference);
            _structureChanged = true;
        }

        internal void CreateBlankStartCondition()
        {
            var source = FindStartCondition("DefaultStart") ?? throw new InvalidOperationException("The installed DefaultStart profile was not found.");
            var copy = CreateStartConditionCopy(source, "New Start");
            _customStartConditions.Add(copy);
            var reference = new XElement("StartCondition", new XAttribute("Id", (string)copy.Attribute("Id")!));
            if (!_world.Elements("StartCondition").Any(e => string.Equals((string?)e.Attribute("IsDefault"), "true", StringComparison.OrdinalIgnoreCase)))
                reference.SetAttributeValue("IsDefault", true);
            _world.Add(reference);
            _structureChanged = true;
        }

        internal void SetStartConditionName(string id, string name)
        {
            var definition = FindStartCondition(id);
            if (definition == null || !_customStartConditions.Contains(definition)) throw new InvalidOperationException("Customize this starting condition before renaming it.");
            name = name.Trim();
            if (name.Length == 0) return;
            if (_startConditionNames.TryGetValue(id, out var existing) && existing == name) return;
            _startConditionNames[id] = name;
            _structureChanged = true;
        }

        internal void SetStartConditionFlags(string id, bool isBrutal, bool isTerrainEdit)
        {
            var condition = FindEditableStartCondition(id);
            if (string.Equals((string?)condition.Attribute("IsBrutal"), isBrutal ? "true" : "false", StringComparison.OrdinalIgnoreCase) &&
                string.Equals((string?)condition.Attribute("IsTerrainEdit"), isTerrainEdit ? "true" : "false", StringComparison.OrdinalIgnoreCase)) return;
            condition.SetAttributeValue("IsBrutal", isBrutal ? "true" : "false");
            condition.SetAttributeValue("IsTerrainEdit", isTerrainEdit ? "true" : "false");
            _structureChanged = true;
        }

        private void AddCelestialBodySettings()
        {
            if (_orbitBody == null) return;
            var longitude = _orbitBody.Element("LongitudeAtEpoch");
            if (longitude == null) { longitude = new XElement("LongitudeAtEpoch", "0"); _orbitBody.Add(longitude); }
            OrbitFields.Add(new Field("Celestial body/Longitude at epoch", longitude.Value, value => longitude.Value = value,
                tooltip: "Reference longitude of this celestial body's position at the orbital epoch, in degrees. This is distinct from the orbit's argument of periapsis."));

            var color = _orbitBody.Element("Color");
            if (color == null) { color = new XElement("Color", new XAttribute("r", "1"), new XAttribute("g", "1"), new XAttribute("b", "1")); _orbitBody.Add(color); }
            foreach (var channel in new[] { (Name: "r", Label: "red", Description: "Red"), (Name: "g", Label: "green", Description: "Green"), (Name: "b", Label: "blue", Description: "Blue") })
            {
                var attribute = color.Attribute(channel.Name);
                if (attribute == null) { color.SetAttributeValue(channel.Name, "1"); attribute = color.Attribute(channel.Name)!; }
                var current = attribute;
                OrbitFields.Add(new Field("Celestial body/Display color/" + channel.Label, current.Value, value => current.Value = value,
                    tooltip: channel.Description + " channel (0–1) for this body's color in celestial-body views. This is a visual color, not a terrain texture."));
            }

            var sunrise = _orbitBody.Element("SunriseOffset");
            if (sunrise == null) { sunrise = new XElement("SunriseOffset", new XAttribute("Value", "0")); _orbitBody.Add(sunrise); }
            var offset = sunrise.Attribute("Value");
            if (offset == null) { sunrise.SetAttributeValue("Value", "0"); offset = sunrise.Attribute("Value")!; }
            var sunriseValue = offset;
            OrbitFields.Add(new Field("Celestial body/Sunrise offset", sunriseValue.Value, value => sunriseValue.Value = value,
                tooltip: "Offset, in degrees, applied to the body's sunrise and sunset timing. Zero keeps the default alignment."));
        }

        private IReadOnlyList<StartCrateView> GetStartConditionCrates(XElement condition)
        {
            var crates = new List<StartCrateView>();
            foreach (var reference in condition.Elements("Spawn").Where(e => (string?)e.Attribute("Event") == "NewWorld"))
            {
                var id = (string?)reference.Attribute("Id") ?? string.Empty;
                if (id.Length == 0) continue;
                var spawn = _customSpawnDefinitions.FirstOrDefault(e => (string?)e.Attribute("Id") == id);
                var dynamicThing = spawn?.Element("DynamicThing");
                if (dynamicThing == null || (string?)dynamicThing.Attribute("Id") != "DynamicCrate") continue;
                var items = dynamicThing.Elements("Item").Select(item =>
                    ((string?)item.Attribute("Id") ?? string.Empty, (int?)item.Element("Quantity")?.Attribute("Value") ?? 1)).ToArray();
                crates.Add(new StartCrateView(id, _spawnNames.TryGetValue(id, out var name) ? name : id, items));
            }
            return crates;
        }

        internal string AddStartConditionCrate(string conditionId, string name)
        {
            var condition = FindEditableStartCondition(conditionId);
            var template = _startConditionLibrary.Elements("Spawn").FirstOrDefault(e => (string?)e.Attribute("Id") == "BrutalCrate")
                ?? throw new InvalidOperationException("The installed BrutalCrate example was not found.");
            var spawn = new XElement(template);
            var id = "WCSpawn_" + Regex.Replace(_world.Attribute("Id")?.Value ?? "World", "[^A-Za-z0-9_]", "_") + "_Crate" + (_customSpawnDefinitions.Count + 1).ToString(CultureInfo.InvariantCulture);
            while (_customSpawnDefinitions.Any(e => (string?)e.Attribute("Id") == id) || _startConditionLibrary.Elements("Spawn").Any(e => (string?)e.Attribute("Id") == id)) id += "_";
            spawn.SetAttributeValue("Id", id);
            spawn.Elements("Item").Remove();
            var container = spawn.Element("DynamicThing") ?? throw new InvalidOperationException("The installed crate example has no DynamicThing container.");
            container.Elements().Where(e => e.Name.LocalName != "SpawnPosition" && e.Name.LocalName != "Color" && e.Name.LocalName != "Name").Remove();
            var nameKey = id + "Name";
            var nameElement = container.Element("Name");
            if (nameElement == null) { nameElement = new XElement("Name"); container.Add(nameElement); }
            nameElement.SetAttributeValue("Key", nameKey);
            condition.Add(new XElement("Spawn", new XAttribute("Id", id), new XAttribute("Event", "NewWorld")));
            _customSpawnDefinitions.Add(spawn);
            _spawnNames[id] = string.IsNullOrWhiteSpace(name) ? "Supply crate" : name.Trim();
            _structureChanged = true;
            return id;
        }

        internal void SetStartConditionCrateName(string crateId, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Enter a name for the supply crate.");
            FindCustomCrate(crateId);
            name = name.Trim();
            if (!_spawnNames.TryGetValue(crateId, out var current) || current == name) return;
            _spawnNames[crateId] = name;
            _structureChanged = true;
        }

        internal void AddStartConditionCrateItem(string crateId, string itemId, int quantity)
        {
            if (string.IsNullOrWhiteSpace(itemId)) throw new InvalidOperationException("Enter an item prefab ID.");
            if (quantity < 1) throw new InvalidOperationException("Item quantity must be at least one.");
            var crate = FindCustomCrate(crateId);
            crate.Element("DynamicThing")!.Add(new XElement("Item", new XAttribute("Id", itemId.Trim()),
                new XElement("Quantity", new XAttribute("Value", quantity.ToString(CultureInfo.InvariantCulture)))));
            _structureChanged = true;
        }

        internal void SetStartConditionCrateItem(string crateId, int index, string itemId, int quantity)
        {
            if (string.IsNullOrWhiteSpace(itemId)) throw new InvalidOperationException("Enter an item prefab ID.");
            if (quantity < 1) throw new InvalidOperationException("Item quantity must be at least one.");
            var item = FindCustomCrate(crateId).Element("DynamicThing")!.Elements("Item").ElementAtOrDefault(index)
                ?? throw new InvalidOperationException("Crate item no longer exists.");
            item.SetAttributeValue("Id", itemId.Trim());
            var value = item.Element("Quantity");
            if (value == null) { value = new XElement("Quantity"); item.Add(value); }
            value.SetAttributeValue("Value", quantity.ToString(CultureInfo.InvariantCulture));
            _structureChanged = true;
        }

        internal void RemoveStartConditionCrateItem(string crateId, int index)
        {
            var item = FindCustomCrate(crateId).Element("DynamicThing")!.Elements("Item").ElementAtOrDefault(index)
                ?? throw new InvalidOperationException("Crate item no longer exists.");
            item.Remove();
            _structureChanged = true;
        }

        internal void RemoveStartConditionCrate(string conditionId, string crateId)
        {
            var condition = FindEditableStartCondition(conditionId);
            var reference = condition.Elements("Spawn").FirstOrDefault(e => (string?)e.Attribute("Id") == crateId && (string?)e.Attribute("Event") == "NewWorld")
                ?? throw new InvalidOperationException("Crate reference no longer exists.");
            reference.Remove();
            if (!_customStartConditions.SelectMany(e => e.Elements("Spawn")).Any(e => (string?)e.Attribute("Id") == crateId))
            {
                _customSpawnDefinitions.RemoveAll(e => (string?)e.Attribute("Id") == crateId);
                _spawnNames.Remove(crateId);
            }
            _structureChanged = true;
        }

        private XElement FindEditableStartCondition(string id)
        {
            var condition = FindStartCondition(id);
            if (condition == null || !_customStartConditions.Contains(condition)) throw new InvalidOperationException("Customize this starting condition before editing its loadout.");
            return condition;
        }

        private XElement FindCustomCrate(string id) => _customSpawnDefinitions.FirstOrDefault(e => (string?)e.Attribute("Id") == id && e.Element("DynamicThing") != null)
            ?? throw new InvalidOperationException("Custom crate no longer exists.");

        internal void CustomizeStartCondition(string id)
        {
            if (_customStartConditions.Any(e => (string?)e.Attribute("Id") == id)) return;
            var reference = _world.Elements("StartCondition").FirstOrDefault(e => (string?)e.Attribute("Id") == id)
                ?? throw new InvalidOperationException("Starting condition is not assigned to this world.");
            var source = FindStartCondition(id) ?? throw new InvalidOperationException("Starting profile not found: " + id);
            var copy = CreateStartConditionCopy(source, id + " custom");
            _customStartConditions.Add(copy);
            reference.SetAttributeValue("Id", (string)copy.Attribute("Id")!);
            _startConditionNames[(string)copy.Attribute("Id")!] = id + " custom";
            _structureChanged = true;
        }

        private XElement CreateStartConditionCopy(XElement source, string name)
        {
            var id = "WCStart_" + Regex.Replace(_world.Attribute("Id")?.Value ?? "World", "[^A-Za-z0-9_]", "_") + "_" + (_customStartConditions.Count + 1).ToString(CultureInfo.InvariantCulture);
            while (FindStartCondition(id) != null) id += "_";
            var copy = new XElement(source);
            copy.SetAttributeValue("Id", id);
            copy.Element("Name")?.SetAttributeValue("Key", id + "Name");
            copy.Element("Description")?.SetAttributeValue("Key", id + "Description");
            copy.Element("WorldInject")?.Remove();
            _startConditionNames[id] = name;
            return copy;
        }

        internal void RemoveStartCondition(string id)
        {
            var reference = _world.Elements("StartCondition").FirstOrDefault(e => (string?)e.Attribute("Id") == id)
                ?? throw new InvalidOperationException("Starting condition is not assigned to this world.");
            var removedDefinition = FindStartCondition(id);
            var removedSpawnIds = removedDefinition?.Elements("Spawn").Select(e => (string?)e.Attribute("Id")).Where(spawnId => spawnId != null).ToArray() ?? Array.Empty<string?>();
            var wasDefault = string.Equals((string?)reference.Attribute("IsDefault"), "true", StringComparison.OrdinalIgnoreCase);
            reference.Remove();
            if (wasDefault) _world.Elements("StartCondition").FirstOrDefault()?.SetAttributeValue("IsDefault", true);
            if (!_world.Elements("StartCondition").Any(e => (string?)e.Attribute("Id") == id))
            {
                _customStartConditions.RemoveAll(e => (string?)e.Attribute("Id") == id);
                _startConditionNames.Remove(id);
                foreach (var spawnId in removedSpawnIds)
                {
                    if (_customStartConditions.SelectMany(e => e.Elements("Spawn")).Any(e => (string?)e.Attribute("Id") == spawnId)) continue;
                    _customSpawnDefinitions.RemoveAll(e => (string?)e.Attribute("Id") == spawnId);
                    _spawnNames.Remove(spawnId!);
                }
            }
            _structureChanged = true;
        }

        internal void SetDefaultStartCondition(string id)
        {
            var reference = _world.Elements("StartCondition").FirstOrDefault(e => (string?)e.Attribute("Id") == id)
                ?? throw new InvalidOperationException("Starting condition is not assigned to this world.");
            foreach (var item in _world.Elements("StartCondition")) item.SetAttributeValue("IsDefault", item == reference ? true : null);
            _structureChanged = true;
        }

        internal void SetStartConditionSpawn(string id, int index, string spawnId, string eventName)
        {
            if (!new[] { "NewWorld", "NewPlayer", "RespawnPlayer", "NewPlayerKit", "RespawnPlayerKit" }.Contains(eventName))
                throw new InvalidOperationException("Choose a supported start event.");
            var definition = FindStartCondition(id);
            if (definition == null || !_customStartConditions.Contains(definition)) throw new InvalidOperationException("Customize this starting condition before editing its spawn references.");
            var spawn = definition!.Elements("Spawn").ElementAtOrDefault(index) ?? throw new InvalidOperationException("Starting spawn reference no longer exists.");
            spawn.SetAttributeValue("Id", spawnId.Trim());
            spawn.SetAttributeValue("Event", eventName);
            _structureChanged = true;
        }

        internal void AddStartConditionSpawn(string id, string spawnId, string eventName)
        {
            if (string.IsNullOrWhiteSpace(spawnId)) throw new InvalidOperationException("Enter a game Spawn ID.");
            if (!new[] { "NewWorld", "NewPlayer", "RespawnPlayer", "NewPlayerKit", "RespawnPlayerKit" }.Contains(eventName))
                throw new InvalidOperationException("Choose a supported start event.");
            var definition = FindStartCondition(id);
            if (definition == null || !_customStartConditions.Contains(definition)) throw new InvalidOperationException("Customize this starting condition before editing its spawn references.");
            definition!.Add(new XElement("Spawn", new XAttribute("Id", spawnId.Trim()), new XAttribute("Event", eventName)));
            _structureChanged = true;
        }

        internal void RemoveStartConditionSpawn(string id, int index)
        {
            var definition = FindStartCondition(id);
            if (definition == null || !_customStartConditions.Contains(definition)) throw new InvalidOperationException("Customize this starting condition before editing its spawn references.");
            (definition!.Elements("Spawn").ElementAtOrDefault(index) ?? throw new InvalidOperationException("Starting spawn reference no longer exists.")).Remove();
            _structureChanged = true;
        }

        internal void AddWeather(string id)
        {
            if (HasWeather(id)) throw new InvalidOperationException(id + " is already assigned to this world.");
            var definition = _weatherLibrary.Elements("WeatherEvent").FirstOrDefault(e => (string?)e.Attribute("Id") == id)
                ?? throw new InvalidOperationException("Weather event not found: " + id);
            var reference = new XElement("WeatherEvent", new XAttribute("Id", id));
            _world.Add(reference);
            var copy = new XElement(definition);
            _weather.Add((reference, copy));
            AddEditableTree(WeatherFields, copy, id);
            _structureChanged = true;
        }

        internal IReadOnlyList<SavedWeatherEffect> SavedWeatherEffects => WeatherEffectLibrary.Load();
        internal bool IsUserWeather(string id) => _userWeatherIds.Contains(id);
        internal string WeatherName(string id) => _weatherNames.TryGetValue(id, out var name) ? name : id;

        internal string CreateWeather(string id, string name, string? baseId = null)
        {
            if (!WeatherEffectLibrary.IsValidId(id)) throw new InvalidOperationException("Weather ID must begin with WCWeather_ and use letters, numbers, and underscores.");
            if (_weatherLibrary.Elements("WeatherEvent").Any(e => (string?)e.Attribute("Id") == id)) throw new InvalidOperationException("That weather ID is already in use.");
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 80) throw new InvalidOperationException("Enter an effect name of up to 80 characters.");
            XElement definition;
            if (!string.IsNullOrEmpty(baseId))
            {
                definition = new XElement(_weatherLibrary.Elements("WeatherEvent").FirstOrDefault(e => (string?)e.Attribute("Id") == baseId)
                    ?? throw new InvalidOperationException("Weather preset not found."));
            }
            else
            {
                definition = new XElement("WeatherEvent", new XAttribute("Id", id), new XAttribute("WindSound", "false"), new XAttribute("ActiveInOrbit", "false"),
                    new XElement("Name", new XAttribute("Key", id)), new XElement("CoolDown", new XAttribute("Min", "3"), new XAttribute("Max", "12")),
                    new XElement("StartDelay", new XAttribute("Min", "30"), new XAttribute("Max", "60")), new XElement("Duration", new XAttribute("Min", "120"), new XAttribute("Max", "240")),
                    new XElement("Fog", new XElement("Color", new XAttribute("r", "0.7"), new XAttribute("g", "0.75"), new XAttribute("b", "0.8"), new XAttribute("a", "1"))),
                    new XElement("TemperatureOffset", new XElement("Day", new XAttribute("Value", "0")), new XElement("Night", new XAttribute("Value", "0"))),
                    new XElement("SolarRatio", new XAttribute("Value", "1")), new XElement("WindStrength", new XAttribute("Value", "0")),
                    new XElement("MovementSpeedMultiplier", new XAttribute("Value", "1")), new XElement("DamageMultiplier", new XAttribute("Value", "0")), new XElement("Direction", new XAttribute("Value", "None")));
            }
            definition.SetAttributeValue("Id", id);
            var nameElement = definition.Element("Name") ?? new XElement("Name");
            if (nameElement.Parent == null) definition.AddFirst(nameElement);
            nameElement.SetAttributeValue("Key", id);
            _weatherLibrary.Add(new XElement(definition));
            _weatherNames[id] = name.Trim();
            _userWeatherIds.Add(id);
            AddWeather(id);
            return id;
        }

        internal void SaveWeatherToLibrary(string id)
        {
            foreach (var field in WeatherFields) { field.Validate(); field.Apply(); }
            var effect = _weather.FirstOrDefault(w => (string?)w.Event.Attribute("Id") == id).Event;
            if (effect == null) throw new InvalidOperationException("Add the weather effect to this world before saving it.");
            WeatherEffectLibrary.Save(id, WeatherName(id), effect);
        }

        internal void AddWeatherBlock(string id, string block)
        {
            var effect = _weather.FirstOrDefault(w => (string?)w.Event.Attribute("Id") == id).Event;
            if (effect == null) throw new InvalidOperationException("Weather effect is not assigned to this world.");
            var installed = _weatherLibrary.Descendants(block).FirstOrDefault();
            XElement node;
            switch (block)
            {
                case "Fog": node = installed != null ? new XElement(installed) : new XElement("Fog", new XElement("Color", new XAttribute("r", "0.7"), new XAttribute("g", "0.75"), new XAttribute("b", "0.8"), new XAttribute("a", "1"))); break;
                case "StormEffect": node = installed != null ? new XElement(installed) : new XElement("StormEffect", new XAttribute("Speed", "2"), new XAttribute("Size", "12"), new XElement("TextureId", new XAttribute("Value", "0")), new XElement("RayMarch", new XAttribute("Size", "0.2"), new XAttribute("Growth", "0.3"), new XAttribute("Density", "1.4")), new XElement("Layer1", new XAttribute("Power", "0.8"), new XAttribute("Multiplier", "1")), new XElement("Layer2", new XAttribute("Threshold", "0.22"), new XAttribute("Power", "0.84"), new XAttribute("Multiplier", "1"), new XAttribute("MaskMultiplier", "1")), new XElement("Layer3", new XAttribute("Threshold", "0.12"), new XAttribute("Power", "1.53"), new XAttribute("Multiplier", "0.5")), new XElement("Color1", new XAttribute("r", "91"), new XAttribute("g", "69"), new XAttribute("b", "61"), new XAttribute("a", "255")), new XElement("Color2", new XAttribute("r", "212"), new XAttribute("g", "153"), new XAttribute("b", "113"), new XAttribute("a", "255")), new XElement("MaskColor", new XAttribute("r", "0"), new XAttribute("g", "0"), new XAttribute("b", "0"), new XAttribute("a", "255"))); break;
                case "DirectionalLight": node = installed != null ? new XElement(installed) : new XElement("DirectionalLight", new XElement("Intensity", new XAttribute("Min", "8"), new XAttribute("Max", "12")), new XElement("Frequency", new XAttribute("Value", "0.3")), new XElement("Color32", new XAttribute("r", "84"), new XAttribute("g", "64"), new XAttribute("b", "44"), new XAttribute("a", "255"))); break;
                default: throw new InvalidOperationException("Unknown weather block.");
            }
            effect.Add(node);
            AddEditableTree(WeatherFields, node, id);
            _structureChanged = true;
        }

        internal void RemoveWeatherBlock(string id, string block, int instanceIndex)
        {
            if (block != "Fog" && block != "StormEffect" && block != "DirectionalLight")
                throw new InvalidOperationException("Unknown weather block.");
            var effect = _weather.FirstOrDefault(w => (string?)w.Event.Attribute("Id") == id).Event;
            if (effect == null) throw new InvalidOperationException("Weather effect is not assigned to this world.");
            var node = effect.Elements(block).ElementAtOrDefault(instanceIndex);
            if (node == null) throw new InvalidOperationException("This effect block no longer exists.");
            node.Remove();
            WeatherFields.RemoveAll(field => field.Label.StartsWith(id + "/", StringComparison.Ordinal) &&
                new[] { "Fog", "StormEffect", "DirectionalLight" }.Any(type => WeatherFieldStartsWithBlock(field, id, type)));
            foreach (var child in effect.Elements().Where(e => e.Name.LocalName == "Fog" || e.Name.LocalName == "StormEffect" || e.Name.LocalName == "DirectionalLight"))
                AddEditableTree(WeatherFields, child, id);
            _structureChanged = true;
        }

        private static bool WeatherFieldStartsWithBlock(Field field, string id, string block)
        {
            var path = field.Label.Substring(id.Length + 1).Replace(" / ", "/");
            return path.StartsWith(block + "/", StringComparison.Ordinal) || path.StartsWith(block + " #", StringComparison.Ordinal);
        }

        internal void AddWeatherSound(string id)
        {
            var effect = _weather.FirstOrDefault(w => (string?)w.Event.Attribute("Id") == id).Event;
            if (effect == null) throw new InvalidOperationException("Weather effect is not assigned to this world.");
            var index = Enumerable.Range(1, 4).FirstOrDefault(i => effect.Element("Shell" + i + "Sound") == null);
            if (index == 0) throw new InvalidOperationException("All four shell sound slots are already present.");
            var sound = new XElement("Shell" + index + "Sound", new XAttribute("Value", (string?)_weatherLibrary.Descendants("Shell" + index + "Sound").FirstOrDefault()?.Attribute("Value") ?? ""));
            effect.Add(sound);
            AddEditableTree(WeatherFields, sound, id);
            _structureChanged = true;
        }

        internal void RemoveWeatherSound(string id, int slot)
        {
            if (slot < 1 || slot > 4) throw new InvalidOperationException("Choose a shell sound slot from 1 to 4.");
            var effect = _weather.FirstOrDefault(w => (string?)w.Event.Attribute("Id") == id).Event;
            if (effect == null) throw new InvalidOperationException("Weather effect is not assigned to this world.");
            var elementName = "Shell" + slot.ToString(CultureInfo.InvariantCulture) + "Sound";
            var sound = effect.Element(elementName) ?? throw new InvalidOperationException("This shell sound slot no longer exists.");
            sound.Remove();
            WeatherFields.RemoveAll(field => field.Label.Replace(" / ", "/").StartsWith(id + "/" + elementName + "/", StringComparison.Ordinal));
            _structureChanged = true;
        }

        internal void RemoveWeather(string id)
        {
            var index = _weather.FindIndex(w => (string?)w.Event.Attribute("Id") == id);
            if (index < 0) throw new InvalidOperationException(id + " is not assigned to this world.");
            _weather[index].Reference.Remove();
            _weather.RemoveAt(index);
            WeatherFields.RemoveAll(f => f.Label.StartsWith(id + "/", StringComparison.Ordinal));
            _structureChanged = true;
        }

        private static void AddText(List<Field> fields, XElement? element, string label)
        {
            if (element != null && double.TryParse(element.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                fields.Add(new Field(label, element.Value, value => element.Value = value));
        }

        private static void AddWorldField(List<Field> fields, XElement world, string elementPath, string? attribute, string label, string tooltip, bool numeric = false, bool boolean = false, string group = "Overview")
        {
            var segments = string.IsNullOrWhiteSpace(elementPath) ? Array.Empty<string>() : elementPath.Split('/');
            XElement? FindExisting()
            {
                XElement? current = world;
                foreach (var segment in segments) current = current?.Element(segment);
                return current;
            }

            var existing = FindExisting();
            var original = attribute == null ? existing?.Value ?? "" : (string?)existing?.Attribute(attribute) ?? "";
            fields.Add(new Field(group + "/" + label, original, value =>
            {
                var current = world;
                foreach (var segment in segments)
                {
                    var child = current.Element(segment);
                    if (child == null) { child = new XElement(segment); current.Add(child); }
                    current = child;
                }
                if (attribute == null) current.Value = value;
                else current.SetAttributeValue(attribute, value);
            }, numeric, boolean, tooltip));
        }

        private static void AddEditableAttributes(List<Field> fields, XElement? element, string label)
        {
            if (element == null) return;
            foreach (var attribute in element.Attributes())
            {
                var current = attribute;
                if (current.Name.LocalName == "Id") continue;
                var numeric = double.TryParse(current.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                var boolean = bool.TryParse(current.Value, out _);
                fields.Add(new Field(label + " / " + current.Name.LocalName, current.Value, value => current.Value = value, numeric, boolean));
            }
        }

        private static void AddEditableTree(List<Field> fields, XElement? element, string path)
        {
            if (element == null) return;
            var hint = (string?)element.Attribute("Type") ?? (string?)element.Attribute("Id");
            var siblings = element.Parent?.Elements(element.Name).Count() ?? 1;
            var index = siblings > 1 ? " #" + (element.ElementsBeforeSelf(element.Name).Count() + 1).ToString(CultureInfo.InvariantCulture) : "";
            var label = path + "/" + element.Name.LocalName + index + (hint == null ? "" : " [" + hint + "]");
            AddEditableAttributes(fields, element, label);
            if (!element.HasElements && !string.IsNullOrWhiteSpace(element.Value))
            {
                var current = element;
                var numeric = double.TryParse(current.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                var boolean = bool.TryParse(current.Value, out _);
                fields.Add(new Field(label, current.Value, value => current.Value = value, numeric, boolean));
            }
            foreach (var child in element.Elements()) AddEditableTree(fields, child, label);
        }

        private static void AddAllFields(List<Field> fields, XElement element, string path)
        {
            var siblings = element.Parent?.Elements(element.Name).Count() ?? 1;
            var index = siblings > 1 ? " #" + (element.ElementsBeforeSelf(element.Name).Count() + 1).ToString(CultureInfo.InvariantCulture) : "";
            var label = path + "/" + element.Name.LocalName + index;
            foreach (var attribute in element.Attributes())
            {
                var current = attribute;
                if (current.Name.LocalName == "nil" && current.Name.NamespaceName == "http://www.w3.org/2001/XMLSchema-instance") continue;
                var numeric = double.TryParse(current.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                var boolean = bool.TryParse(current.Value, out _);
                fields.Add(new Field(label + "/@" + current.Name.LocalName, current.Value, value => current.Value = value, numeric, boolean));
            }
            var nil = element.Attributes().FirstOrDefault(a => a.Name.LocalName == "nil" && a.Name.NamespaceName == "http://www.w3.org/2001/XMLSchema-instance");
            if (nil != null && nil.Value == "true")
            {
                var current = element;
                fields.Add(new Field(label, "", value =>
                {
                    current.Value = value;
                    current.SetAttributeValue(nil.Name, null);
                }, numeric: false, boolean: true));
            }
            if (!element.HasElements && !string.IsNullOrWhiteSpace(element.Value))
            {
                var current = element;
                var numeric = double.TryParse(current.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                var boolean = bool.TryParse(current.Value, out _);
                fields.Add(new Field(label, current.Value, value => current.Value = value, numeric, boolean));
            }
            foreach (var child in element.Elements()) AddAllFields(fields, child, label);
        }

        internal List<(bool IsError, string Text)> ValidateForExport(string modsDirectory, string id, string displayName)
        {
            var issues = new List<(bool IsError, string Text)>();
            if (!Regex.IsMatch(id, @"^WC_[A-Za-z0-9_]{1,44}$"))
                issues.Add((true, "World ID must start with WC_ and contain only letters, digits and underscores (max 47 characters)."));
            if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 80)
                issues.Add((true, "Enter a world name of up to 80 characters."));
            if (Regex.IsMatch(id, @"^WC_[A-Za-z0-9_]{1,44}$") && !string.IsNullOrWhiteSpace(displayName) && displayName.Length <= 80 &&
                Directory.Exists(Path.Combine(modsDirectory, "WorldCreator_" + id)))
                issues.Add((true, "A mod with this ID already exists. Choose a new ID."));
            var fields = AdvancedFields.Concat(WorldFields).Concat(AtmosphereFields).Concat(WeatherFields).Concat(OrbitFields).Concat(ResourceFields);
            foreach (var field in fields)
            {
                try { field.Validate(); }
                catch (InvalidOperationException e) { issues.Add((true, e.Message)); }
            }
            if (_isBlankProject && CompiledTerrainFiles.Count == 0)
                issues.Add((false, "This blank world has no compiled terrain. Add compiled terrain before trying to load it in game."));
            else if (CompiledTerrainFiles.Count == 0 && TerrainSourceFiles.Count > 0)
                issues.Add((false, "Terrain source maps are included, but they are not compiled terrain. Compile them with Terrain Tools before loading this world."));
            return issues;
        }

        internal string Export(string modsDirectory, string id, string displayName)
        {
            if (Regex.IsMatch(id, @"^WC_[A-Za-z0-9_]{1,44}$") && Directory.Exists(Path.Combine(modsDirectory, "WorldCreator_" + id)))
                throw new IOException("A mod with this ID already exists. Choose a new ID.");
            var issue = ValidateForExport(modsDirectory, id, displayName).FirstOrDefault(item => item.IsError);
            if (issue.IsError) throw new InvalidOperationException(issue.Text);
            var target = Path.Combine(modsDirectory, "WorldCreator_" + id);

            var allFields = AdvancedFields.Concat(WorldFields).Concat(AtmosphereFields).Concat(WeatherFields).Concat(OrbitFields).Concat(ResourceFields).ToList();
            foreach (var field in allFields) field.Apply();
            var worldOutput = new XElement(_world);
            worldOutput.SetAttributeValue("Id", id);
            worldOutput.SetAttributeValue("Hidden", false);
            worldOutput.SetAttributeValue("Deprecated", false);
            var name = worldOutput.Element("Name") ?? new XElement("Name");
            if (name.Parent == null) worldOutput.AddFirst(name);
            name.RemoveAttributes();
            name.SetAttributeValue("Value", displayName.Trim());
            if (CompiledTerrainFiles.Count > 0 || _isBlankProject)
                worldOutput.Element("TerrainSettings")?.SetAttributeValue("Path", "Worlds/" + id + "/Terrain");
            var changedMapLayers = new List<(string Id, string RelativePath, byte[] Data)>();
            for (var i = 0; i < MapLayers.Count; i++)
            {
                var layer = MapLayers[i];
                if (!_mapLayerTextures.TryGetValue(layer.Id, out var textureBytes)) continue;
                var relativePath = "Worlds/" + id + "/Textures/MapLayer_" + i.ToString(CultureInfo.InvariantCulture) + ".png";
                var regionSet = worldOutput.Descendants("RegionSet").FirstOrDefault(e => (string?)e.Attribute("Id") == layer.Id);
                var texture = regionSet?.Element("Texture") ?? throw new InvalidOperationException("Map layer has no Texture element: " + layer.Id);
                texture.SetAttributeValue("Path", relativePath);
                changedMapLayers.Add((layer.Id, relativePath, textureBytes));
            }

            var changedWeather = _hasImportedWeatherDefinitions || WeatherFields.Any(f => f.Value != f.Original) ||
                _weather.Any(w => _userWeatherIds.Contains((string?)w.Event.Attribute("Id") ?? string.Empty));
            var weatherCopies = new List<XElement>();
            var weatherNames = new List<(string Key, string Name)>();
            if (changedWeather)
            {
                for (var i = 0; i < _weather.Count; i++)
                {
                    var sourceWeatherId = (string?)_weather[i].Event.Attribute("Id") ?? "";
                    var weatherId = id + "_Weather" + i.ToString(CultureInfo.InvariantCulture);
                    var referenceIndex = _world.Elements("WeatherEvent").ToList().IndexOf(_weather[i].Reference);
                    worldOutput.Elements("WeatherEvent").ElementAt(referenceIndex).SetAttributeValue("Id", weatherId);
                    var copy = new XElement(_weather[i].Event);
                    copy.SetAttributeValue("Id", weatherId);
                    if (_weatherNames.TryGetValue(sourceWeatherId, out var weatherName))
                    {
                        var nameKey = (string?)copy.Element("Name")?.Attribute("Key") ?? sourceWeatherId;
                        if (!weatherNames.Any(pair => pair.Key == nameKey)) weatherNames.Add((nameKey, weatherName));
                    }
                    weatherCopies.Add(copy);
                }
            }
            var changedOrbit = _orbitBody != null && (_hasImportedOrbitBody || OrbitFields.Any(f => f.Value != f.Original));
            if (changedOrbit)
            {
                var playable = worldOutput.Element("PlayableBody")!;
                var originalId = (string)playable.Attribute("Id")!;
                var bodyId = id + "_Body";
                playable.SetAttributeValue("Id", bodyId);
                var bodies = worldOutput.Element("CelestialBodies");
                if (bodies != null)
                {
                    foreach (var entry in bodies.Elements())
                    {
                        if ((string?)entry.Attribute("Id") == originalId) entry.SetAttributeValue("Id", bodyId);
                        if ((string?)entry.Attribute("Parent") == originalId) entry.SetAttributeValue("Parent", bodyId);
                    }
                }
            }

            var data = new XElement("GameData", new XElement("WorldSettings", worldOutput));
            if (changedWeather)
                foreach (var weather in weatherCopies) data.Add(weather);
            if (changedOrbit)
            {
                var bodyCopy = new XElement(_orbitBody!);
                bodyCopy.SetAttributeValue("Id", id + "_Body");
                data.Add(new XElement("CelestialBodies", bodyCopy));
            }
            RepairMissingTemplateAssetPaths(worldOutput, id);

            var temporary = Path.Combine(modsDirectory, ".WorldCreator_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(temporary, "About"));
                Directory.CreateDirectory(Path.Combine(temporary, "GameData"));
                new XDocument(new XElement("ModMetadata", new XElement("Name", displayName.Trim()),
                    new XElement("ModID", "worldcreator.world." + id), new XElement("Author", "World Creator"),
                    new XElement("Version", "1.0"), new XElement("Description", "Custom world created with World Creator.")))
                    .Save(Path.Combine(temporary, "About", "About.xml"));
                new XDocument(data).Save(Path.Combine(temporary, "GameData", "world.xml"));
                var languageRecords = weatherNames
                    .Select(pair => new XElement("Record", new XElement("Key", pair.Key), new XElement("Value", pair.Name)))
                    .Concat(_customStartConditions.Select(condition =>
                    {
                        var id = (string?)condition.Attribute("Id") ?? string.Empty;
                        var name = _startConditionNames.TryGetValue(id, out var value) ? value : id;
                        var nameKey = (string?)condition.Element("Name")?.Attribute("Key");
                        var descriptionKey = (string?)condition.Element("Description")?.Attribute("Key");
                        var records = new List<XElement>();
                        if (nameKey != null) records.Add(new XElement("Record", new XElement("Key", nameKey), new XElement("Value", name)));
                        if (descriptionKey != null) records.Add(new XElement("Record", new XElement("Key", descriptionKey), new XElement("Value", "Starting option for " + name + ".")));
                        return records;
                    }).SelectMany(records => records))
                    .Concat(_spawnNames.Select(pair => new XElement("Record", new XElement("Key", pair.Key + "Name"), new XElement("Value", pair.Value))))
                    .Concat(_mapRegionNames.Select(pair => new XElement("Record", new XElement("Key", pair.Key), new XElement("Value", pair.Value))))
                    .ToList();
                if (languageRecords.Count > 0)
                {
                    var language = new XElement("English", new XElement("Name", "English"), new XElement("Code", "EN"), new XElement("Font", "font_english"),
                        new XElement("Interface", languageRecords));
                    var languagePath = Path.Combine(temporary, "GameData", "Language");
                    Directory.CreateDirectory(languagePath);
                    new XDocument(language).Save(Path.Combine(languagePath, "english.xml"));
                }
                if (_customStartConditions.Count > 0 || _customSpawnDefinitions.Count > 0)
                {
                    var startData = new XElement("GameData",
                        _customStartConditions.Select(condition => new XElement(condition))
                            .Concat(_customSpawnDefinitions.Select(spawn => new XElement(spawn))));
                    new XDocument(startData).Save(Path.Combine(temporary, "GameData", "startconditions.xml"));
                }
                CopyImportedAssets(worldOutput, temporary);
                if (changedMapLayers.Count > 0)
                {
                    foreach (var mapLayer in changedMapLayers)
                    {
                        var destination = Path.Combine(temporary, "GameData", mapLayer.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                        File.WriteAllBytes(destination, mapLayer.Data);
                    }
                }
                foreach (var asset in ProjectAssets)
                {
                    var relativePath = asset.RelativePath(id);
                    var destination = Path.Combine(temporary, "GameData", relativePath.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.WriteAllBytes(destination, asset.Data);
                }
                if (CompiledTerrainFiles.Count > 0)
                {
                    var destination = Path.Combine(temporary, "GameData", "Worlds", id, "Terrain");
                    Directory.CreateDirectory(destination);
                    foreach (var file in CompiledTerrainFiles) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
                    File.WriteAllLines(Path.Combine(destination, "README.txt"), new[]
                    {
                        "Compiled terrain copied from Stationeers Terrain Tools.",
                        "The world XML references this directory as Worlds/" + id + "/Terrain.",
                        "If the world fails to load, check the game's log for terrain asset resolver errors."
                    });
                }
                else if (_isBlankProject)
                {
                    Directory.CreateDirectory(Path.Combine(temporary, "GameData", "Worlds", id, "Terrain"));
                }
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
                ValidateExport(temporary, id, displayName.Trim(), changedWeather, changedOrbit, weatherNames);
                WriteExportReadme(temporary, id, displayName.Trim(), weatherCopies.Count, changedOrbit, changedMapLayers.Count);
                Directory.Move(temporary, target);
            }
            catch
            {
                if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
                throw;
            }
            return target;
        }

        private void CopyImportedAssets(XElement world, string temporary)
        {
            if (string.IsNullOrEmpty(_sourceAssetRoot)) return;
            foreach (var path in world.DescendantsAndSelf().Attributes("Path").Select(a => a.Value.Replace('\\', '/')).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!path.StartsWith("Worlds/", StringComparison.OrdinalIgnoreCase)) continue;
                var source = Path.Combine(_sourceAssetRoot, path.Replace('/', Path.DirectorySeparatorChar));
                var destination = Path.Combine(temporary, "GameData", path.Replace('/', Path.DirectorySeparatorChar));
                if (Directory.Exists(source))
                {
                    foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                    {
                        var relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        var copied = Path.Combine(destination, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(copied)!);
                        if (!File.Exists(copied)) File.Copy(file, copied);
                    }
                    continue;
                }
                if (!File.Exists(source)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (!File.Exists(destination)) File.Copy(source, destination);
            }
        }

        private void RepairMissingTemplateAssetPaths(XElement world, string id)
        {
            foreach (var attribute in world.DescendantsAndSelf().Attributes("Path"))
            {
                var path = attribute.Value.Replace('\\', '/');
                var asset = Path.Combine(_assets, path.Replace('/', Path.DirectorySeparatorChar));
                if (!string.IsNullOrEmpty(_sourceAssetRoot))
                {
                    var importedAsset = Path.Combine(_sourceAssetRoot, path.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(importedAsset) || Directory.Exists(importedAsset)) continue;
                }
                if (File.Exists(asset) || Directory.Exists(asset) || !path.StartsWith("Worlds/", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("Worlds/" + id + "/", StringComparison.Ordinal)) continue;
                var textureMarker = path.IndexOf("/Textures/", StringComparison.OrdinalIgnoreCase);
                if (textureMarker < 0) continue;
                var replacement = "Worlds/" + _sourceWorldDirectory + path.Substring(textureMarker);
                var replacementPath = Path.Combine(_assets, replacement.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(replacementPath)) attribute.Value = replacement;
            }
        }

        private void ValidateExport(string folder, string id, string displayName, bool changedWeather, bool changedOrbit, IReadOnlyCollection<(string Key, string Name)> weatherNames)
        {
            var metadata = XDocument.Load(Path.Combine(folder, "About", "About.xml")).Root;
            if (metadata?.Name != "ModMetadata" || (string?)metadata.Element("ModID") != "worldcreator.world." + id ||
                (string?)metadata.Element("Name") != displayName)
                throw new InvalidDataException("The generated About/About.xml metadata is incomplete or does not match this world.");

            var data = XDocument.Load(Path.Combine(folder, "GameData", "world.xml")).Root;
            var world = data?.Element("WorldSettings")?.Elements("World").SingleOrDefault(e => (string?)e.Attribute("Id") == id);
            if (data?.Name != "GameData" || world == null || (string?)world.Element("Name")?.Attribute("Value") != displayName ||
                world.Element("TerrainSettings") == null || world.Element("PlayableBody") == null)
                throw new InvalidDataException("The generated GameData/world.xml is missing the expected world settings.");

            if (changedWeather)
            {
                foreach (var reference in world.Elements("WeatherEvent"))
                {
                    var eventId = (string?)reference.Attribute("Id");
                    if (eventId != null && eventId.StartsWith(id + "_Weather", StringComparison.Ordinal) &&
                        data.Elements("WeatherEvent").Count(e => (string?)e.Attribute("Id") == eventId) != 1)
                        throw new InvalidDataException("A custom weather reference has no unique matching definition in GameData/world.xml.");
                }
            }

            ValidateStartConditionExport(folder, world);

            var geographic = world.Element("GeographicRegionData");
            if (_mapRegionNames.Count > 0)
            {
                if (geographic == null) throw new InvalidDataException("Custom geographic regions have no GeographicRegionData block.");
                var regionSetId = (string?)geographic.Element("RegionSet")?.Attribute("Id");
                var regionSet = data.Descendants("RegionSet").SingleOrDefault(element => (string?)element.Attribute("Id") == regionSetId && element.Element("Texture") != null);
                if (string.IsNullOrWhiteSpace(regionSetId) || regionSet == null || regionSet.Elements("Region").Count() == 0)
                    throw new InvalidDataException("GeographicRegionData must reference a defined RegionSet with at least one Region.");
                var regionIds = regionSet.Elements("Region").Select(element => (string?)element.Attribute("Id")).ToArray();
                if (regionIds.Any(string.IsNullOrWhiteSpace) || regionIds.Distinct(StringComparer.Ordinal).Count() != regionIds.Length)
                    throw new InvalidDataException("The geographic region layer contains a missing or duplicate region ID.");
                var regionColors = regionSet.Elements("Region").Select(element => (
                    (int?)element.Attribute("R") ?? -1,
                    (int?)element.Attribute("G") ?? -1,
                    (int?)element.Attribute("B") ?? -1)).ToArray();
                if (regionColors.Any(color => color.Item1 < 0 || color.Item1 > 255 || color.Item2 < 0 || color.Item2 > 255 || color.Item3 < 0 || color.Item3 > 255) ||
                    regionColors.Distinct().Count() != regionColors.Length)
                    throw new InvalidDataException("The geographic region layer contains an invalid or duplicate RGB color.");
                foreach (var pair in _mapRegionNames)
                {
                    if (string.IsNullOrWhiteSpace(pair.Value)) throw new InvalidDataException("A custom geographic region needs a display name: " + pair.Key);
                    var keyCount = regionSet.Elements("Region").Count(element => (string?)element.Element("Name")?.Attribute("Key") == pair.Key);
                    var languageFile = Path.Combine(folder, "GameData", "Language", "english.xml");
                    var language = File.Exists(languageFile) ? XDocument.Load(languageFile).Root?.Element("Interface") : null;
                    if (keyCount != 1 || language?.Elements("Record").Count(record => (string?)record.Element("Key") == pair.Key && (string?)record.Element("Value") == pair.Value) != 1)
                        throw new InvalidDataException("A custom geographic region is missing its unique English name record: " + pair.Key);
                }
            }

            if (weatherNames.Count > 0)
            {
                var languagePath = Path.Combine(folder, "GameData", "Language", "english.xml");
                var language = File.Exists(languagePath) ? XDocument.Load(languagePath).Root : null;
                if (language?.Name != "English" || language.Element("Interface") == null ||
                    weatherNames.Any(pair => language.Element("Interface")!.Elements("Record").Count(record => (string?)record.Element("Key") == pair.Key && (string?)record.Element("Value") == pair.Name) != 1))
                    throw new InvalidDataException("Custom weather names are missing from the generated GameData/Language/english.xml file.");
            }

            if (changedOrbit)
            {
                var bodyId = (string?)world.Element("PlayableBody")?.Attribute("Id");
                if (bodyId != id + "_Body" || data.Elements("CelestialBodies").Elements("CelestialBody").Count(e => (string?)e.Attribute("Id") == bodyId) != 1)
                    throw new InvalidDataException("The custom playable body has no unique matching celestial body definition.");
            }

            var gameDataFolder = Path.Combine(folder, "GameData");
            foreach (var attribute in world.DescendantsAndSelf().Attributes("Path"))
            {
                var path = attribute.Value.Replace('\\', '/');
                var packagedPath = Path.Combine(gameDataFolder, path.Replace('/', Path.DirectorySeparatorChar));
                var resolvedPath = File.Exists(packagedPath) || Directory.Exists(packagedPath)
                    ? packagedPath
                    : Path.Combine(_assets, path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(resolvedPath) && !Directory.Exists(resolvedPath))
                    throw new InvalidDataException("An asset referenced by the exported world is missing: " + path);
            }
        }

        private void ValidateStartConditionExport(string folder, XElement world)
        {
            if (_customStartConditions.Count == 0 && _customSpawnDefinitions.Count == 0) return;
            var path = Path.Combine(folder, "GameData", "startconditions.xml");
            if (!File.Exists(path)) throw new InvalidDataException("Custom starting conditions were authored but GameData/startconditions.xml was not generated.");
            var data = XDocument.Load(path).Root;
            if (data?.Name != "GameData") throw new InvalidDataException("Generated GameData/startconditions.xml must have a GameData root.");

            var exportedConditions = data.Elements("StartCondition").ToArray();
            var exportedSpawns = data.Elements("Spawn").ToArray();
            if (exportedConditions.Length != _customStartConditions.Count || exportedSpawns.Length != _customSpawnDefinitions.Count)
                throw new InvalidDataException("Generated startconditions.xml does not contain the expected number of custom definitions.");

            var worldReferences = world.Elements("StartCondition").ToArray();
            var defaultCount = worldReferences.Count(reference => string.Equals((string?)reference.Attribute("IsDefault"), "true", StringComparison.OrdinalIgnoreCase));
            if (defaultCount > 1) throw new InvalidDataException("The exported world marks more than one start condition as the default.");

            foreach (var condition in _customStartConditions)
            {
                var id = (string?)condition.Attribute("Id");
                if (string.IsNullOrWhiteSpace(id) || exportedConditions.Count(item => (string?)item.Attribute("Id") == id) != 1)
                    throw new InvalidDataException("A custom start condition has a missing or duplicate definition in startconditions.xml.");
                if (worldReferences.Count(reference => (string?)reference.Attribute("Id") == id) != 1)
                    throw new InvalidDataException("A custom start condition must have exactly one matching reference in world.xml: " + id);
                foreach (var spawn in condition.Elements("Spawn"))
                {
                    if (string.IsNullOrWhiteSpace((string?)spawn.Attribute("Id")) || string.IsNullOrWhiteSpace((string?)spawn.Attribute("Event")))
                        throw new InvalidDataException("A start condition contains a Spawn reference without both Id and Event attributes: " + id);
                }
            }

            var spawnIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var spawn in _customSpawnDefinitions)
            {
                var id = (string?)spawn.Attribute("Id");
                if (string.IsNullOrWhiteSpace(id) || !spawnIds.Add(id) || exportedSpawns.Count(item => (string?)item.Attribute("Id") == id) != 1)
                    throw new InvalidDataException("A custom spawn definition has a missing or duplicate Id in startconditions.xml.");
                var dynamicThing = spawn.Element("DynamicThing");
                if ((string?)dynamicThing?.Attribute("Id") != "DynamicCrate") continue;
                foreach (var item in dynamicThing.Elements("Item"))
                {
                    var itemId = (string?)item.Attribute("Id");
                    var quantity = (string?)item.Element("Quantity")?.Attribute("Value");
                    if (string.IsNullOrWhiteSpace(itemId) || !int.TryParse(quantity, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) || count < 1)
                        throw new InvalidDataException("A custom supply crate contains an item without a valid prefab Id and positive integer quantity: " + id);
                }
            }

            var expectedLanguage = _customStartConditions.SelectMany(condition =>
            {
                var id = (string?)condition.Attribute("Id") ?? string.Empty;
                var name = _startConditionNames.TryGetValue(id, out var value) ? value : id;
                var entries = new List<(string Key, string Value)>();
                var nameKey = (string?)condition.Element("Name")?.Attribute("Key");
                var descriptionKey = (string?)condition.Element("Description")?.Attribute("Key");
                if (!string.IsNullOrWhiteSpace(nameKey)) entries.Add((nameKey!, name));
                if (!string.IsNullOrWhiteSpace(descriptionKey)) entries.Add((descriptionKey!, "Starting option for " + name + "."));
                return entries;
            }).Concat(_spawnNames.Select(pair => (Key: pair.Key + "Name", Value: pair.Value))).ToArray();
            if (expectedLanguage.Length == 0) return;

            var languagePath = Path.Combine(folder, "GameData", "Language", "english.xml");
            var language = File.Exists(languagePath) ? XDocument.Load(languagePath).Root : null;
            if (language?.Name != "English" || language.Element("Interface") == null ||
                expectedLanguage.Any(entry => language.Element("Interface")!.Elements("Record").Count(record =>
                    (string?)record.Element("Key") == entry.Key && (string?)record.Element("Value") == entry.Value) != 1))
                throw new InvalidDataException("Custom starting-condition and crate names are missing or duplicated in GameData/Language/english.xml.");
        }

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
            foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                         .Select(file => file.Substring(folder.TrimEnd(Path.DirectorySeparatorChar).Length + 1).Replace(Path.DirectorySeparatorChar, '/'))
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
            var knownSpawnIds = new HashSet<string>(_startConditionLibrary.Elements("Spawn")
                .Concat(_customSpawnDefinitions).Select(spawn => (string?)spawn.Attribute("Id") ?? ""), StringComparer.Ordinal);
            var externalSpawnIds = _customStartConditions.SelectMany(condition => condition.Elements("Spawn"))
                .Select(spawn => (string?)spawn.Attribute("Id") ?? "").Where(spawnId => spawnId.Length > 0 && !knownSpawnIds.Contains(spawnId))
                .Distinct(StringComparer.Ordinal).OrderBy(spawnId => spawnId, StringComparer.Ordinal).ToArray();
            if (externalSpawnIds.Length > 0)
                warnings.Add("Spawn IDs not found in installed game definitions or this export; confirm an enabled mod provides them: " + string.Join(", ", externalSpawnIds));
            if (CompiledTerrainFiles.Count == 0 && TerrainSourceFiles.Count > 0)
                warnings.Add("Terrain source maps are included, but they are not compiled terrain. Import them with the game's Terrain Tools before using this world as a playable custom terrain world.");
            else if (CompiledTerrainFiles.Count == 0 && _isBlankProject)
                warnings.Add("No compiled terrain files are included. A blank world needs terrain compiled with the game's Terrain Tools before it can load as a playable world.");
            if (_weather.Any(weather => weather.Event.Elements("Fog").Skip(1).Any()))
                warnings.Add("At least one weather definition contains repeated Fog elements. The installed game serializes one Fog field per event, so extra Fog cards may not render as separate layers.");
            if (customOrbit) warnings.Add("The custom orbit XML passed structural checks; orbital progression and coordinate conventions have not been verified in game.");
            warnings.Add("This report confirms generated file structure and available asset references only; test gameplay behavior in Stationeers.");

            lines.Add("");
            lines.Add("Warnings and follow-up:");
            foreach (var warning in warnings) lines.Add("  - " + warning);
            lines.Add("");
            lines.Add("This report is informational. It does not modify player saves or install the generated mod.");
            File.WriteAllLines(Path.Combine(folder, "README.txt"), lines);
        }

    }
}
