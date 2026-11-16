using System;
using System.Globalization;
using System.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    /// <summary>Changes a palette entry while preserving region IDs and synchronising the XML draft.</summary>
    internal void SetRegionColour(MapLayer layer, MapLayer.Region region, MapRgbValue colour)
    {
        if (!MapLayers.Contains(layer) || !layer.Regions.Contains(region)) throw new InvalidOperationException("Select an existing map region.");
        if (layer.Regions.Any(other => other != region && other.R == colour.R && other.G == colour.G && other.B == colour.B))
            throw new InvalidOperationException("That RGB colour is already used by another region in this mask.");
        var definition = _world.Descendants("RegionSet").First(element => (string?)element.Attribute("Id") == layer.Id);
        var xml = definition.Elements("Region").First(element => (string?)element.Attribute("Id") == region.Id);
        var setField = MapFields.First(field => field.Value == layer.Id && field.Label.EndsWith("/@Id"));
        var regionField = MapFields.First(field => field.Value == region.Id && field.Label.StartsWith(setField.Label.Substring(0, setField.Label.Length - 4)));
        var prefix = regionField.Label.Substring(0, regionField.Label.Length - 4);
        foreach (var channel in new[] { "R", "G", "B" })
            MapFields.First(field => field.Label == prefix + "/@" + channel).Value = (channel == "R" ? colour.R : channel == "G" ? colour.G : colour.B).ToString(CultureInfo.InvariantCulture);
        xml.SetAttributeValue("R", colour.R); xml.SetAttributeValue("G", colour.G); xml.SetAttributeValue("B", colour.B);
        region.R = colour.R; region.G = colour.G; region.B = colour.B;
        _structureChanged = true;
    }
}
