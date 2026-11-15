using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    private readonly Dictionary<string, XElement> _spaceMapDefinitions = new Dictionary<string, XElement>(StringComparer.Ordinal);
    internal Field SpaceMapReference = null!;
    internal string[] SpaceMapIds { get; private set; } = Array.Empty<string>();

    /// <summary>Collects native and imported rocket-destination maps and binds the world's selected map reference.</summary>
    private void InitializeSpaceMapSettings()
    {
        AddSpaceMapDefinitions(XDocument.Load(Path.Combine(_assets, "Data", "rocketlocations.xml")).Root);
        AddSpaceMapDefinitions(_sourceXml);
        if (!string.IsNullOrEmpty(_sourceGameDataRoot) && Directory.Exists(_sourceGameDataRoot))
        {
            foreach (var file in Directory.GetFiles(_sourceGameDataRoot, "*.xml", SearchOption.AllDirectories))
            {
                try
                {
                    AddSpaceMapDefinitions(XDocument.Load(file).Root);
                }
                catch (Exception error)when (error is System.Xml.XmlException || error is IOException || error is UnauthorizedAccessException)
                { /* Optional, unrelated mod data must not prevent loading the world. */
                }
            }
        }

        SpaceMapReference = AdvancedFields.FirstOrDefault(field => field.Label.EndsWith("/SpaceMap/@Id", StringComparison.Ordinal))!;
        if (SpaceMapReference == null)
        {
            SpaceMapReference = new Field("World/World/SpaceMap/@Id", "", value =>
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _world.Element("SpaceMap")?.Remove();
                    return;
                }

                var reference = _world.Element("SpaceMap");
                if (reference == null)
                {
                    reference = new XElement("SpaceMap");
                    _world.Add(reference);
                }

                reference.SetAttributeValue("Id", value);
            }, numeric: false, tooltip: "Existing rocket destination map used by this world. Custom IDs require the mod defining that map to remain enabled.");
            AdvancedFields.Add(SpaceMapReference);
        }

        SpaceMapIds = _spaceMapDefinitions.Keys.Concat(new[] { SpaceMapReference.Value }).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Registers complete space-map definitions while ignoring simple reference-only XML records.</summary>
    private void AddSpaceMapDefinitions(XElement? root)
    {
        if (root == null)
            return;
        foreach (var map in root.DescendantsAndSelf("SpaceMap").Where(map => map.Element("Entry") != null))
        {
            var id = (string? )map.Attribute("Id");
            if (!string.IsNullOrWhiteSpace(id))
                _spaceMapDefinitions[id!] = new XElement(map);
        }
    }

    /// <summary>Builds the destination-map label shown to the player, retaining unknown custom IDs.</summary>
    internal string SpaceMapLabel(string id)
    {
        if (string.IsNullOrEmpty(id))
            return "Game default (FallBack)";
        var name = _spaceMapDefinitions.TryGetValue(id, out var map) ? (string? )map.Element("Entry")?.Element("Name")?.Attribute("Value") : null;
        return string.IsNullOrWhiteSpace(name) ? id : name + " · " + id;
    }

    internal string SpaceMapSummary
    {
        get
        {
            var id = string.IsNullOrWhiteSpace(SpaceMapReference.Value) ? "FallBack" : SpaceMapReference.Value;
            if (!_spaceMapDefinitions.TryGetValue(id, out var map))
                return "External map reference. Its definition was not found in the installed game or this world's mod data. Keep the mod that provides it enabled.";
            var entry = map.Element("Entry");
            var entryName = (string? )entry?.Element("Name")?.Attribute("Value") ?? (string? )entry?.Attribute("Id") ?? "Unnamed entry";
            return "Entry: " + entryName + " · Destinations: " + map.Elements("Node").Count() + " · Low Orbit hub: " + (map.Element("LowOrbitHub") != null ? "present" : "not defined") + " · Orbit distance: " + ((string? )map.Attribute("OrbitDistance") ?? "game default (400)") + ". This is the rocket travel map, separate from terrain and region masks.";
        }
    }
}
