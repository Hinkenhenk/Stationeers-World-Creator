using System;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    /// <summary>Removes one mask region and its world-specific bindings, leaving other regions and IDs intact.</summary>
    internal void RemoveMapRegion(MapLayer layer, MapLayer.Region region)
    {
        if (!MapLayers.Contains(layer) || !layer.Regions.Contains(region))
            throw new InvalidOperationException("Select an existing region.");
        var definition = _world.Elements("RegionSet").Single(element => (string?)element.Attribute("Id") == layer.Id);
        var xml = definition.Elements("Region").Single(element => (string?)element.Attribute("Id") == region.Id);
        var bindings = _world.Descendants("Region").Where(element => element != xml &&
            element.Parent?.Name != "RegionSet" && (string?)element.Attribute("Id") == region.Id)
            .Select(element => element.Ancestors().First(parent => parent.Parent == _world)).Distinct().ToArray();
        if (bindings.Any(element => !new[] { "PointOfInterest", "PlayableArea", "Minables", "DeepMinables" }.Contains(element.Name.LocalName)))
            throw new InvalidOperationException("This region has an unsupported world binding. Update that binding in Advanced before deleting it.");
        var keys = xml.DescendantsAndSelf().Concat(bindings.SelectMany(element => element.DescendantsAndSelf()))
            .Attributes("Key").Select(attribute => attribute.Value).ToArray();
        foreach (var binding in bindings)
        {
            var id = (string?)binding.Attribute("Id");
            ResourceFields.RemoveAll(field => field.Label.StartsWith("Surface resources [" + id + "]") ||
                field.Label.StartsWith("Deep resources [" + id + "]"));
            binding.Remove();
        }
        xml.Remove();
        layer.Regions.Remove(region);
        PointsOfInterest.RemoveAll(poi => poi.RegionId == region.Id);
        PointOfInterestCount = PointsOfInterest.Count;
        foreach (var key in keys.Distinct())
        {
            if (_world.Descendants().Attributes("Key").Any(attribute => attribute.Value == key)) continue;
            _mapRegionNames.Remove(key);
            _mapTextEdits.Remove(key);
        }
        if (layer.Regions.Count == 0)
        {
            definition.Remove();
            _world.Elements().Where(element => element.Elements("RegionSet").Any(reference =>
                (string?)reference.Attribute("Id") == layer.Id)).Remove();
            MapLayers.Remove(layer);
            _mapLayerTextures.Remove(layer.Id);
            if (_playableAreaLayerId == layer.Id) _playableAreaLayerId = null;
        }
        _structureChanged = true;
        RefreshMapFields();
    }
}
