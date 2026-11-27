using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    private CelestialBodyCatalog? _celestialCatalog;
    private readonly Dictionary<string, byte[]> _celestialSurfaceData = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, XElement> _extraCelestialDefinitions = new Dictionary<string, XElement>(StringComparer.Ordinal);

    /// <summary>Loads native bodies lazily so other editor pages do not scan the celestial library.</summary>
    private CelestialBodyCatalog CelestialCatalog => _celestialCatalog ??= new CelestialBodyCatalog(_assets, _sourceXml);

    /// <summary>Adds discovered local and Workshop definitions without changing the draft.</summary>
    internal void LoadCelestialModRoots(IEnumerable<string> roots)
    {
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase)) CelestialCatalog.AddModRoot(root);
    }

    /// <summary>Lists every usable native or installed body, including the playable body for creating its moons.</summary>
    internal string[] AvailableCelestialIds() => CelestialCatalog.Entries.Values
        .Where(entry => entry.Definition != null || entry.Primary != null).Select(entry => entry.Id)
        .Append(PrimaryBodyId).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>Identifies central references, which cannot be added as orbiting bodies without orbital data.</summary>
    internal bool IsCentralCelestial(string id) => CelestialCatalog.Entries.TryGetValue(id, out var entry) && entry.Primary != null && entry.Definition == null;

    /// <summary>Shows the authored name instead of an export's private internal ID.</summary>
    internal string CelestialBodyLabel(string id)
    {
        var definition = _extraCelestialDefinitions.TryGetValue(id, out var owned) ? owned
            : CelestialCatalog.Entries.TryGetValue(id, out var entry) ? entry.Definition : null;
        var name = (string?)definition?.Element("Name")?.Attribute("Value") ?? id;
        return IsCentralCelestial(id) ? name + " (central body)" : name;
    }

    /// <summary>Checks the entire dependency chain before importing anything into the draft.</summary>
    private void ValidateCelestialImport(string id)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (id != PrimaryBodyId && id != GetRotationSettings().BodyId)
        {
            if (!seen.Add(id)) throw new InvalidOperationException("Circular celestial parent chain: " + id);
            if (_world.Element("CelestialBodies")?.Elements().Any(body => (string?)body.Attribute("Id") == id) == true) return;
            if (!CelestialCatalog.Entries.TryGetValue(id, out var entry) || entry.Definition == null && entry.Primary == null)
                throw new InvalidOperationException("Missing orbital ancestor: " + id);
            if (entry.Primary != null && entry.Definition == null) return;
            id = (string?)entry.Reference?.Attribute("Parent") ?? PrimaryBodyId;
        }
    }

    /// <summary>Lists orbital templates separately from primary star/light references.</summary>
    internal string[] CelestialTemplateIds() => CelestialCatalog.Entries.Values.Where(entry => entry.Definition != null)
        .Select(entry => entry.Id).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>Retains all loaded private templates when a previously exported world is edited again.</summary>
    private void InitializeExtraCelestialDefinitions()
    {
        var playable = (string?)_world.Element("PlayableBody")?.Attribute("Id");
        foreach (var body in _sourceXml.Elements("CelestialBodies").Elements("CelestialBody"))
        {
            var id = (string?)body.Attribute("Id");
            if (!string.IsNullOrEmpty(id) && id != playable) _extraCelestialDefinitions[id!] = new XElement(body);
        }
    }

    /// <summary>Reads a parent from the draft first, then the installed template's world reference.</summary>
    private string? CelestialParent(string id)
    {
        var reference = _world.Element("CelestialBodies")?.Elements().FirstOrDefault(body => (string?)body.Attribute("Id") == id);
        if (reference != null) return RotationAttribute(reference.Attribute("Parent"));
        return CelestialCatalog.Entries.TryGetValue(id, out var entry) ? (string?)entry.Reference?.Attribute("Parent") : null;
    }

    /// <summary>Imports an absent parent and its ancestors after the complete chain has passed cycle validation.</summary>
    private void EnsureCelestialBody(string id)
    {
        if (id == PrimaryBodyId || id == GetRotationSettings().BodyId) return;
        if (_world.Element("CelestialBodies")?.Elements().Any(body => (string?)body.Attribute("Id") == id) == true) return;
        if (!CelestialCatalog.Entries.TryGetValue(id, out var entry)) throw new InvalidOperationException("Celestial body is unavailable: " + id);
        if (IsCentralCelestial(id)) { SetCentralCelestial(id, entry.Primary!); return; }
        if (entry.Definition == null) throw new InvalidOperationException("No orbital definition was found for " + id + ".");
        var parent = (string?)entry.Reference?.Attribute("Parent") ?? PrimaryBodyId;
        EnsureCelestialBody(parent);
        var reference = entry.Reference == null ? new XElement("Body", new XAttribute("Id", id)) : new XElement(entry.Reference);
        reference.SetAttributeValue("Parent", parent);
        CelestialReferences().Add(reference);
        _extraCelestialDefinitions[id] = new XElement(entry.Definition);
    }

    /// <summary>Returns the world's reference collection, creating only the missing container.</summary>
    private XElement CelestialReferences()
    {
        var references = _world.Element("CelestialBodies");
        if (references != null) return references;
        references = new XElement("CelestialBodies");
        _world.Add(references);
        return references;
    }

    /// <summary>Changes the system root and redirects references to the former root while keeping field drafts synchronized.</summary>
    private void SetCentralCelestial(string id, XElement source)
    {
        var old = PrimaryBodyId;
        var primary = _world.Element("PrimaryBody");
        if (primary == null) { primary = new XElement("PrimaryBody"); _world.Add(primary); }
        SetCelestialAttribute(primary, "Id", id);
        var solar = (string?)source.Attribute("SolarConstant");
        if (solar != null) SetCelestialAttribute(primary, "SolarConstant", solar);
        primary.Element("Name")?.Remove();
        primary.Add(new XElement("Name", new XAttribute("Value", id)));
        ApplyCentralCelestialAppearance(id, old);
        foreach (var parent in _world.Descendants().Attributes("Parent").Where(attribute => RotationAttribute(attribute) == old).ToArray())
            SetCelestialAttribute(parent.Parent!, "Parent", id);
    }

    /// <summary>Reuses the chosen native central body's sun and matching skybox prefab without replacing unrelated sky settings.</summary>
    private void ApplyCentralCelestialAppearance(string id, string old)
    {
        if (!CelestialCatalog.Entries.TryGetValue(id, out var entry)) return;
        if (!string.IsNullOrWhiteSpace(entry.SunPrefab))
        {
            _world.SetElementValue("SunPrefabName", entry.SunPrefab);
            foreach (var field in WorldFields.Where(field => field.Label.EndsWith(" / Sun prefab ID", StringComparison.Ordinal))) field.Value = entry.SunPrefab!;
        }
        var skybox = _world.Element("Skybox");
        skybox?.Elements("PlanetPrefab").Where(prefab => (string?)prefab.Element("Name") == old).Remove();
        if (entry.PrimaryPrefab == null) return;
        if (skybox == null) { skybox = new XElement("Skybox"); _world.Add(skybox); }
        skybox.Add(new XElement(entry.PrimaryPrefab));
    }

    /// <summary>Updates an XML value and existing Overview or Advanced controls that point at it.</summary>
    private void SetCelestialAttribute(XElement element, string name, string value)
    {
        element.SetAttributeValue(name, value);
        var attribute = element.Attribute(name);
        foreach (var field in AdvancedFields.Concat(WorldFields).Where(field => field.Target == attribute)) field.Value = value;
        if (name == "Id" && element.Name == "PrimaryBody")
            foreach (var field in WorldFields.Where(field => field.Label.EndsWith(" / Primary body ID", StringComparison.Ordinal))) field.Value = value;
        if (name == "SolarConstant")
            foreach (var field in WorldFields.Where(field => field.Label.EndsWith(" / Primary star irradiance (W/m²)", StringComparison.Ordinal))) field.Value = value;
    }

    /// <summary>Creates a named planet or moon using an installed appearance and a validated, explicitly authored orbit.</summary>
    internal string CreateCelestialBody(string name, string templateId, string parentId, double distance, bool kilometres, double period, double eccentricity, double inclination, double radius, string rgb, string? surfaceTexture = null)
    {
        var id = NewCelestialId(name);
        if (!CelestialCatalog.Entries.TryGetValue(templateId, out var template) || template.Definition == null) throw new InvalidOperationException("Choose an available orbital template.");
        if (!AvailableCelestialIds().Contains(parentId) || OrbitParentCreatesCycle(parentId, id)) throw new InvalidOperationException("Choose an available parent without a circular orbit.");
        if (!FiniteRotation(distance) || distance <= 0 || !FiniteRotation(distance * (1 + eccentricity)) || !kilometres && !FiniteRotation(distance * 149597870.7) || kilometres && distance / 149597870.7 == 0 || !FiniteRotation(period) || period <= 0 || !FiniteRotation(360 / period) || 360 / period == 0
            || !FiniteRotation(eccentricity) || eccentricity < 0 || (float)eccentricity >= 1 || !FiniteRotation(inclination) || Math.Abs(inclination) > 180
            || !FiniteRotation(radius) || (float)radius <= 0 || radius > float.MaxValue || !TryParseCelestialColour(rgb, out _))
            throw new InvalidOperationException("Use positive finite distance, period and radius; eccentricity from 0 to below 1; inclination from -180 to 180; and RGB values from 0 to 1.");
        byte[]? surfaceData = null;
        if (!string.IsNullOrWhiteSpace(surfaceTexture))
        {
            surfaceTexture = Path.GetFullPath(surfaceTexture!.Trim());
            ReadTerrainPngSize(surfaceTexture);
            if (new FileInfo(surfaceTexture).Length > 268435456) throw new InvalidOperationException("Surface textures must be 256 MB or smaller.");
            surfaceData = File.ReadAllBytes(surfaceTexture);
        }
        ValidateCelestialImport(parentId);
        var definition = new XElement(template.Definition);
        definition.SetAttributeValue("Id", id);
        definition.Element("Name")?.Remove();
        definition.AddFirst(new XElement("Name", new XAttribute("Value", name.Trim())));
        definition.Element("Orbit")!.ReplaceWith(new XElement("Orbit",
            new XAttribute(kilometres ? "SemiMajorAxisKm" : "SemiMajorAxisAu", distance.ToString("R", CultureInfo.InvariantCulture)),
            new XAttribute("Period", period.ToString("R", CultureInfo.InvariantCulture)), new XAttribute("Eccentricity", eccentricity.ToString("R", CultureInfo.InvariantCulture)),
            new XAttribute("Inclination", inclination.ToString("R", CultureInfo.InvariantCulture)), new XAttribute("LongitudeOfAscendingNode", "0"), new XAttribute("ArgumentOfPeriapsis", "0")));
        var color = definition.Element("Color");
        if (color == null) { color = new XElement("Color"); definition.Add(color); }
        WriteCelestialColour(color, rgb);
        var reference = template.Reference != null && template.Reference.Element("Texture") != null
            ? new XElement(template.Reference) : new XElement("RockyBody", new XElement("Texture", new XAttribute("Path", "Textures/mercury_diffuse.png"), new XAttribute("Format", "DXT1")));
        reference.SetAttributeValue("Id", id);
        reference.SetAttributeValue("Parent", parentId);
        reference.SetAttributeValue("RadiusKm", radius.ToString("R", CultureInfo.InvariantCulture));
        reference.Element("Name")?.Remove();
        reference.AddFirst(new XElement("Name", new XAttribute("Value", name.Trim())));
        if (!string.IsNullOrWhiteSpace(surfaceTexture))
        {
            reference.Element("Texture")?.Remove();
            reference.Add(new XElement("Texture", new XAttribute("Path", surfaceTexture!), new XAttribute("Format", "RGB24")));
        }
        EnsureCelestialBody(parentId);
        CelestialReferences().Add(reference);
        if (surfaceData != null) _celestialSurfaceData[surfaceTexture!] = surfaceData;
        _extraCelestialDefinitions.Add(id, definition);
        CelestialCatalog.Entries.Add(id, new CelestialBodyCatalog.Entry(id) { Definition = definition, Reference = reference, Source = "This project" });
        _structureChanged = true;
        RefreshMapFields();
        return id;
    }

    /// <summary>Creates a central light reference with optional custom visuals for the exported runtime.</summary>
    internal string CreateCentralCelestial(string name, double irradiance, string? texturePath = null, float diameter = 4, float glow = 1, string rgb = "1,1,1")
    {
        var id = NewCelestialId(name);
        if (!FiniteRotation(irradiance) || irradiance < 0 || irradiance > float.MaxValue || irradiance > 0 && (float)irradiance == 0) throw new InvalidOperationException("Irradiance must be a finite nonnegative value supported by the game.");
        if (!CanEditRotation) throw new InvalidOperationException("This world has no editable orbit.");
        var appearance = string.IsNullOrWhiteSpace(texturePath) ? null : ReadStarAppearance(texturePath!, diameter, glow, rgb);
        var primary = new XElement("PrimaryBody", new XAttribute("Id", id), new XAttribute("SolarConstant", irradiance.ToString("R", CultureInfo.InvariantCulture)));
        CelestialCatalog.Entries.Add(id, new CelestialBodyCatalog.Entry(id) { Primary = primary, Source = "This project" });
        SetOrbitParent(id);
        if (appearance != null) _customStars.Add(id, appearance);
        return id;
    }

    /// <summary>Builds a readable unique ID without exposing GUIDs or allowing a built-in body to be overwritten.</summary>
    private string NewCelestialId(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 40) throw new InvalidOperationException("Enter a unique body name of up to 40 characters.");
        var id = string.Concat(name.Trim().Select(character => character == ' ' ? '_' : character));
        if (!IsValidWorldId(id) || AvailableCelestialIds().Any(existing => existing.Equals(id, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Use a unique name with letters, digits, spaces or underscores; installed body names cannot be overwritten.");
        return id;
    }

    /// <summary>Copies third-party celestial visuals to private package paths while retaining game textures as native references.</summary>
    private List<(string Source, string RelativePath)> PrepareCelestialAssetPaths(XElement data, string worldId)
    {
        var copies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var attribute in data.Descendants().Attributes("Path").Where(attribute => Path.IsPathRooted(attribute.Value) && attribute.Parent!.AncestorsAndSelf().Any(element => element.Name == "CelestialBodies")))
        {
            var source = Path.GetFullPath(attribute.Value);
            if (!File.Exists(source) && !_celestialSurfaceData.ContainsKey(source)) throw new IOException("Celestial body texture is missing: " + source);
            if (!copies.TryGetValue(source, out var relative))
            {
                relative = "Worlds/" + worldId + "/Celestial/" + copies.Count + "_" + Path.GetFileName(source);
                copies.Add(source, relative);
            }
            attribute.Value = relative;
        }
        return copies.Select(pair => (pair.Key, pair.Value)).ToList();
    }

    /// <summary>Exports imported and created templates with private IDs and rewrites all associated parent links.</summary>
    private void AddExportCelestialDefinitions(XElement data, string worldId)
    {
        var definitions = data.Element("CelestialBodies");
        if (definitions == null && _extraCelestialDefinitions.Count > 0) { definitions = new XElement("CelestialBodies"); data.Add(definitions); }
        var index = 0;
        var ids = _extraCelestialDefinitions.Keys.ToDictionary(key => key, key => worldId + "_Celestial" + index++, StringComparer.Ordinal);
        foreach (var pair in _extraCelestialDefinitions)
        {
            var copy = new XElement(pair.Value);
            copy.SetAttributeValue("Id", ids[pair.Key]);
            var name = copy.Element("Name");
            if (name == null) { name = new XElement("Name"); copy.AddFirst(name); }
            if (string.IsNullOrWhiteSpace((string?)name.Attribute("Value"))) name.SetAttributeValue("Value", CelestialBodyLabel(pair.Key));
            definitions!.Add(copy);
        }
        var world = data.Element("WorldSettings")!.Element("World")!;
        var attributes = world.Element("CelestialBodies")?.Elements().Attributes().Where(attribute => attribute.Name == "Id" || attribute.Name == "Parent") ?? Enumerable.Empty<XAttribute>();
        foreach (var attribute in attributes.Concat(world.Elements("PlayableBody").Attributes("Parent")))
            if (ids.TryGetValue(attribute.Value, out var replacement)) attribute.Value = replacement;
    }
}
