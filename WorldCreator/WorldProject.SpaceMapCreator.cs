using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    private const string DraftMapId = "WorldCreatorDraftMap";
    private const string MapSuffix = "_CreatorSpaceMap";
    private XElement? _authoredSpaceMap;

    /// <summary>Restores a map previously authored by this editor.</summary>
    private void RestoreAuthoredSpaceMap()
    {
        var id = (string?)_sourceXml.Element("WorldSettings")?.Element("World")?.Attribute("Id");
        var map = _sourceXml.Elements("SpaceMap").FirstOrDefault(element =>
            id != null && (string?)element.Attribute("Id") == id + MapSuffix && element.Element("Entry") != null);
        if (map != null) _authoredSpaceMap = new XElement(map);
    }

    /// <summary>Exposes a copy of the current authored map for editing.</summary>
    internal XElement? ReadAuthoredSpaceMap() => _authoredSpaceMap == null ? null : new XElement(_authoredSpaceMap);

    /// <summary>Saves a validated destination tree and selects it for this world.</summary>
    internal void SaveAuthoredSpaceMap(string entry, float orbitDistance,
        IReadOnlyList<(string Name, float X, float Y, int Parent, int Difficulty)> destinations)
    {
        if (string.IsNullOrWhiteSpace(entry) || entry.Trim().Length > 80 ||
            float.IsNaN(orbitDistance) || float.IsInfinity(orbitDistance) || orbitDistance <= 0 ||
            destinations.Count < 1 || destinations.Count > 64)
            throw new InvalidOperationException("Enter a short entry name, a positive orbit distance and 1–64 destinations.");

        var map = new XElement("SpaceMap", new XAttribute("Id", DraftMapId), new XAttribute("OrbitDistance", orbitDistance));
        var nodes = new List<XElement> { MakeMapNode("Entry", "0", entry, 0, 0) };
        nodes[0].SetAttributeValue("Code", "10");
        nodes[0].SetAttributeValue("IsCharted", true);
        nodes[0].Add(new XElement("Deploy", new XAttribute("Type", "DeliveryPayload")));
        map.Add(nodes[0]);
        foreach (var destination in destinations)
        {
            if (string.IsNullOrWhiteSpace(destination.Name) || destination.Name.Trim().Length > 80 ||
                !(Math.Abs(destination.X) <= 100000 && Math.Abs(destination.Y) <= 100000) ||
                destination.Parent < 0 || destination.Parent >= nodes.Count || destination.Difficulty < 0)
                throw new InvalidOperationException("Each destination needs a short name, valid coordinates, an earlier parent and a nonnegative difficulty.");
            var id = nodes.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var node = MakeMapNode("Node", id, destination.Name, destination.X, destination.Y);
            node.Add(new XElement("Survey"));
            nodes[destination.Parent].Add(new XElement("Connection", new XAttribute("Id", id), new XAttribute("Difficulty", destination.Difficulty)));
            nodes.Add(node);
            map.Add(node);
        }
        _authoredSpaceMap = map;
        _spaceMapDefinitions[DraftMapId] = map;
        SpaceMapIds = SpaceMapIds.Append(DraftMapId).Distinct(StringComparer.Ordinal).ToArray();
        SpaceMapReference.Value = DraftMapId;
        _structureChanged = true;
    }

    /// <summary>Builds a native map location with a built-in icon.</summary>
    private static XElement MakeMapNode(string type, string id, string name, float x, float y) =>
        new XElement(type, new XAttribute("Id", id), new XElement("Name", new XAttribute("Value", name.Trim())),
            new XElement("MapDisplay", new XAttribute("X", x), new XAttribute("Y", y),
                new XElement("Icon", new XAttribute("Id", "StandardNode01"), new XAttribute("Size", "3"))));

    /// <summary>Packages only the selected authored map and gives its globally registered records private IDs.</summary>
    private void AddExportSpaceMap(XElement data, string worldId)
    {
        if (_authoredSpaceMap == null || SpaceMapReference.Value != (string?)_authoredSpaceMap.Attribute("Id"))
            return;
        var map = new XElement(_authoredSpaceMap);
        var mapId = worldId + MapSuffix;
        map.SetAttributeValue("Id", mapId);
        var ids = map.Elements().Select((node, index) => ((string)node.Attribute("Id")!, NewId: mapId + "_" + index))
            .ToDictionary(pair => pair.Item1, pair => pair.NewId, StringComparer.Ordinal);
        foreach (var id in map.Descendants().Where(element => element.Name == "Entry" || element.Name == "Node" ||
            element.Name == "Connection").Attributes("Id"))
            id.Value = ids[id.Value];
        var world = data.Element("WorldSettings")!.Element("World")!;
        world.Element("SpaceMap")?.Remove();
        world.Add(new XElement("SpaceMap", new XAttribute("Id", mapId)));
        data.Add(map);
    }
}
