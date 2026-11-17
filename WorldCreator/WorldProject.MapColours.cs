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
        foreach (var channel in new[] { "R", "G", "B" })
        {
            var value = (channel == "R" ? colour.R : channel == "G" ? colour.G : colour.B).ToString(CultureInfo.InvariantCulture);
            var field = MapFields.FirstOrDefault(item => item.Target == xml.Attribute(channel));
            if (field != null) field.Value = value;
        }
        xml.SetAttributeValue("R", colour.R); xml.SetAttributeValue("G", colour.G); xml.SetAttributeValue("B", colour.B);
        region.R = colour.R; region.G = colour.G; region.B = colour.B;
        _structureChanged = true;
    }
}
