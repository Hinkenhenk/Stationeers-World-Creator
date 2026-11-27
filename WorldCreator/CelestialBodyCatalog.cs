using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace WorldCreator;

/// <summary>Collects native and installed-mod orbital definitions without registering anything in the running game.</summary>
internal sealed class CelestialBodyCatalog
{
    internal sealed class Entry
    {
        internal readonly string Id;
        internal XElement? Definition;
        internal XElement? Reference;
        internal XElement? Primary;
        internal string? SunPrefab;
        internal XElement? PrimaryPrefab;
        internal bool BlackHole;
        internal string Source = "";
        /// <summary>Identifies one serializer-compatible celestial body.</summary>
        internal Entry(string id) => Id = id;
    }

    internal readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
    internal readonly List<string> Warnings = new List<string>();
    private readonly HashSet<string> _files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly string _assets;

    /// <summary>Reads the game library and world-specific central bodies, including Janus.</summary>
    internal CelestialBodyCatalog(string assets, XElement source)
    {
        _assets = assets;
        AddFile(Path.Combine(assets, "Data", "celestialbodies.xml"));
        AddFolder(Path.Combine(assets, "Worlds"));
        AddDocument(source, null, true);
    }

    /// <summary>Scans a mod root or Workshop collection once, considering XML in GameData only.</summary>
    internal void AddModRoot(string root)
    {
        if (!Directory.Exists(root)) return;
        try
        {
            if (Path.GetFileName(root).Equals("GameData", StringComparison.OrdinalIgnoreCase)) AddFolder(root);
            foreach (var folder in Directory.GetDirectories(root, "GameData", SearchOption.AllDirectories)) AddFolder(folder);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        { Warnings.Add(root + ": " + error.Message); }
    }

    /// <summary>Reads each file once and leaves malformed third-party documents out of the picker.</summary>
    private void AddFolder(string folder)
    {
        if (!Directory.Exists(folder)) return;
        try
        {
            foreach (var file in Directory.GetFiles(folder, "*.xml", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal)) AddFile(file);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        { Warnings.Add(folder + ": " + error.Message); }
    }

    /// <summary>Disables DTD resolution when reading installed XML and reports unreadable entries.</summary>
    private void AddFile(string file)
    {
        if (!_files.Add(Path.GetFullPath(file))) return;
        try
        {
            using var reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var root = XElement.Load(reader);
            AddDocument(root, file, false);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is XmlException)
        { Warnings.Add(file + ": " + error.Message); }
    }

    /// <summary>Merges templates and visual references; the loaded world's own records take precedence.</summary>
    private void AddDocument(XElement root, string? file, bool overwrite)
    {
        foreach (var definition in root.DescendantsAndSelf("CelestialBody").Where(element => element.Element("Orbit") != null))
        {
            var entry = Get((string?)definition.Attribute("Id"));
            if (entry == null || entry.Definition != null && !overwrite) continue;
            entry.Definition = new XElement(definition);
            entry.Source = file ?? "Loaded world";
        }
        foreach (var world in root.DescendantsAndSelf("World"))
        {
            var primary = world.Element("PrimaryBody");
            var central = Get((string?)primary?.Attribute("Id"));
            if (central != null && (central.Primary == null || overwrite))
            {
                central.Primary = new XElement(primary!);
                central.SunPrefab = (string?)world.Element("SunPrefabName");
                var prefab = world.Element("Skybox")?.Elements("PlanetPrefab").FirstOrDefault(item => (string?)item.Element("Name") == central.Id);
                central.PrimaryPrefab = prefab == null ? null : new XElement(prefab);
                central.BlackHole = central.BlackHole || world.Element("PreviewScene")?.Descendants("Prefab").Elements("Name")
                    .Any(name => name.Value.IndexOf("blackhole", StringComparison.OrdinalIgnoreCase) >= 0 || name.Value.IndexOf("black hole", StringComparison.OrdinalIgnoreCase) >= 0) == true;
            }
            foreach (var reference in world.Element("CelestialBodies")?.Elements() ?? Enumerable.Empty<XElement>())
            {
                var entry = Get((string?)reference.Attribute("Id"));
                if (entry == null || entry.Reference != null && !overwrite && (entry.Reference.Name != "Sprite" || reference.Name == "Sprite")) continue;
                entry.Reference = new XElement(reference);
                if (entry.Reference.Attribute("Parent") == null && central != null) entry.Reference.SetAttributeValue("Parent", central.Id);
                ResolvePaths(entry.Reference, file);
            }
        }
    }

    /// <summary>Keeps native texture references native and resolves third-party visuals against their owning mod.</summary>
    private void ResolvePaths(XElement reference, string? file)
    {
        if (file == null) return;
        var directory = Path.GetDirectoryName(file)!;
        var gameData = new DirectoryInfo(directory);
        while (gameData.Parent != null && !gameData.Name.Equals("GameData", StringComparison.OrdinalIgnoreCase)) gameData = gameData.Parent;
        foreach (var attribute in reference.Descendants().Attributes("Path"))
        {
            var path = attribute.Value.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            if (File.Exists(Path.Combine(_assets, path))) continue;
            var candidates = new[] { Path.Combine(directory, path), Path.Combine(gameData.FullName, path), Path.Combine(gameData.Parent?.FullName ?? directory, path) };
            var resolved = candidates.FirstOrDefault(File.Exists);
            if (resolved != null) attribute.Value = resolved;
        }
    }

    /// <summary>Returns the shared entry for a nonempty ID.</summary>
    private Entry? Get(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (!Entries.TryGetValue(id!, out var entry)) Entries.Add(id!, entry = new Entry(id!));
        return entry;
    }
}
