using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    private string? _playableAreaLayerId;

    /// <summary>Identifies masks used by playable-area rules so other painting tools cannot distort the boundary.</summary>
    internal bool IsPlayableAreaLayer(string layerId)
    {
        var layer = MapLayers.FirstOrDefault(item => item.Id == layerId);
        return layer != null && (layer.TexturePath.Replace('\\', '/').EndsWith("/4KMissionArea.png", StringComparison.OrdinalIgnoreCase)
            || _world.Elements("PlayableArea").Descendants("Region").Any(reference => layer.Regions.Any(region => region.Id == (string?)reference.Attribute("Id"))));
    }

    /// <summary>Replaces this draft's playable-area rules with a private, three-colour area mask.</summary>
    internal string SetPlayableAreaMask(byte[] png)
    {
        ValidateMapPng(png);
        if (_playableAreaLayerId == null)
            _playableAreaLayerId = MapLayers.FirstOrDefault(layer => layer.Id.EndsWith("_WCPlayableBounds", StringComparison.Ordinal)
                && layer.Regions.Count == 3 && layer.Regions.Any(region => region.Id == layer.Id + "Valid" && region.G == 255)
                && IsPlayableAreaLayer(layer.Id))?.Id;
        if (_playableAreaLayerId == null)
            CreatePlayableAreaLayer();
        var id = _playableAreaLayerId!;
        _world.Elements("PlayableArea").Remove();
        _world.Add(new XElement("PlayableArea", new XAttribute("Id", id + "WarningArea"), new XAttribute("Rule", "Warning"),
            new XElement("Region", new XAttribute("Id", id + "Warning"))));
        _world.Add(new XElement("PlayableArea", new XAttribute("Id", id + "InvalidArea"), new XAttribute("Rule", "Invalid"),
            new XElement("Region", new XAttribute("Id", id + "Invalid"))));
        SetMapLayerTexture(id, png);
        RefreshMapFields();
        return id;
    }

    /// <summary>Adds a new mask without repurposing source masks that may also locate resources or points of interest.</summary>
    private void CreatePlayableAreaLayer()
    {
        var suffix = 1;
        string id;
        do { id = "WCPlayableBounds" + (suffix++).ToString(CultureInfo.InvariantCulture); }
        while (_world.Descendants().Attributes("Id").Any(attribute => attribute.Value.StartsWith(id, StringComparison.Ordinal)));
        var path = "Worlds/WC_Blank/Textures/PlayableArea.png";
        var definition = new XElement("RegionSet", new XAttribute("Id", id),
            new XElement("Texture", new XAttribute("Path", path), new XAttribute("Format", "RGB24"), new XAttribute("LoadType", "OnRequest")));
        var layer = new MapLayer(id, path);
        AddPlayableRegion(definition, layer, "Valid", 0, 255, 0);
        AddPlayableRegion(definition, layer, "Warning", 0, 0, 255);
        AddPlayableRegion(definition, layer, "Invalid", 255, 0, 0);
        _world.Add(definition);
        MapLayers.Add(layer);
        _playableAreaLayerId = id;
    }

    /// <summary>Keeps each region's XML definition and preview colour in agreement.</summary>
    private static void AddPlayableRegion(XElement definition, MapLayer layer, string suffix, int r, int g, int b)
    {
        var id = layer.Id + suffix;
        definition.Add(new XElement("Region", new XAttribute("Id", id), new XAttribute("R", r), new XAttribute("G", g), new XAttribute("B", b)));
        layer.Regions.Add(new MapLayer.Region(id, r, g, b));
    }

    /// <summary>Scopes generated boundary IDs to the exported world so installed custom worlds do not share definitions.</summary>
    private void PreparePlayableAreaIdentifiers(XElement worldOutput, string worldId)
    {
        if (_playableAreaLayerId == null)
            return;
        var previous = _playableAreaLayerId;
        var next = worldId + "_WCPlayableBounds";
        var owned = worldOutput.Elements("RegionSet").Where(element => (string?)element.Attribute("Id") == previous)
            .Concat(worldOutput.Elements("PlayableArea"));
        foreach (var attribute in owned.SelectMany(element => element.DescendantsAndSelf()).Attributes("Id"))
        {
            if (attribute.Value.StartsWith(previous, StringComparison.Ordinal))
                attribute.Value = next + attribute.Value.Substring(previous.Length);
        }
    }
}
