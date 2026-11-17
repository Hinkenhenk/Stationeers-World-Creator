using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    /// <summary>Stores edited PNG bytes for a known region-mask layer.</summary>
    internal void SetMapLayerTexture(string layerId, byte[] png)
    {
        if (!MapLayers.Any(layer => layer.Id == layerId))
            throw new InvalidOperationException("Unknown map layer: " + layerId);
        ValidateMapPng(png);
        _mapLayerTextures[layerId] = (byte[])png.Clone();
        _structureChanged = true;
    }

    /// <summary>Rejects missing or incorrectly typed mask data before modifying the draft.</summary>
    private static void ValidateMapPng(byte[] png)
    {
        if (png == null || png.Length < 8 || png[0] != 137 || png[1] != 80 || png[2] != 78 || png[3] != 71 || png[4] != 13 || png[5] != 10 || png[6] != 26 || png[7] != 10)
            throw new InvalidOperationException("Map layer image must be a PNG.");
    }

    /// <summary>Discards an edited map-mask image so export reuses the source texture.</summary>
    internal void ClearMapLayerTexture(string layerId)
    {
        if (!MapLayers.Any(layer => layer.Id == layerId))
            throw new InvalidOperationException("Unknown map layer: " + layerId);
        if (_mapLayerTextures.Remove(layerId))
            _structureChanged = true;
    }

    /// <summary>Creates a project-owned geographic mask with a base region and editable XML fields.</summary>
    internal string AddGeographicLayer(byte[] initialPng)
    {
        if (_world.Element("GeographicRegionData") != null)
            throw new InvalidOperationException("This world already has a geographic layer. Add a region to that layer instead.");
        var suffix = 1;
        string id;
        do
        {
            id = "WCGeographicRegions" + (suffix++).ToString(CultureInfo.InvariantCulture);
        }
        while (_world.Descendants("RegionSet").Any(regionSet => (string? )regionSet.Attribute("Id") == id));
        var regionId = id + "World";
        var nameKey = regionId + "Name";
        var regionSet = new XElement(
            "RegionSet",
            new XAttribute("Id", id),
            new XElement(
            "Texture",
            new XAttribute("Path", "Worlds/WC_Blank/Textures/GeographicRegions.png"),
            new XAttribute("Format", "RGB24"),
            new XAttribute("LoadType", "OnRequest")
        ),
            new XElement(
            "Region",
            new XAttribute("Id", regionId),
            new XAttribute("R", "0"),
            new XAttribute("G", "255"),
            new XAttribute("B", "0"),
            new XElement("Name", new XAttribute("Key", nameKey))
        )
        );
        var terrainSettings = _world.Element("TerrainSettings");
        if (terrainSettings == null)
            _world.Add(regionSet);
        else
            terrainSettings.AddBeforeSelf(regionSet);
        var geographicData = new XElement(
            "GeographicRegionData",
            new XElement("DefaultRegionName", new XAttribute("Value", "World")),
            new XElement("RegionSet", new XAttribute("Id", id))
        );
        if (terrainSettings == null)
            _world.Add(geographicData);
        else
            terrainSettings.AddBeforeSelf(geographicData);
        var layer = new MapLayer(id, "Worlds/WC_Blank/Textures/GeographicRegions.png");
        layer.Regions.Add(new MapLayer.Region(regionId, 0, 255, 0, nameKey));
        MapLayers.Add(layer);
        _mapRegionNames.Add(nameKey, "World");
        SetMapLayerTexture(id, initialPng);
        RefreshMapFields();
        return id;
    }

    /// <summary>Adds a uniquely colored named region to an existing geographic mask.</summary>
    internal string AddGeographicRegion(string layerId)
    {
        var layer = MapLayers.FirstOrDefault(item => item.Id == layerId) ?? throw new InvalidOperationException("Select a map layer first.");
        if ((string? )_world.Element("GeographicRegionData")?.Element("RegionSet")?.Attribute("Id") != layerId)
            throw new InvalidOperationException("Only the world's geographic region layer supports named regions.");
        var suffix = 1;
        string regionId;
        do
        {
            regionId = "WCRegion" + (suffix++).ToString(CultureInfo.InvariantCulture);
        }
        while (layer.Regions.Any(region => region.Id == regionId));
        var colorIndex = 0;
        (int, int, int) color = (0, 0, 0);
        do
        {
            if (colorIndex > 0xFFFFFF)
                throw new InvalidOperationException("This geographic layer has used every available RGB color.");
            color = (colorIndex & 255, (colorIndex >> 8) & 255, (colorIndex >> 16) & 255);
            colorIndex++;
        }
        while (layer.Regions.Any(region => region.R == color.Item1 && region.G == color.Item2 && region.B == color.Item3));
        var nameKey = regionId + "Name";
        var definition = _world.Descendants("RegionSet").First(element => (string? )element.Attribute("Id") == layerId);
        definition.Add(new XElement(
            "Region",
            new XAttribute("Id", regionId),
            new XAttribute("R", color.Item1),
            new XAttribute("G", color.Item2),
            new XAttribute("B", color.Item3),
            new XElement("Name", new XAttribute("Key", nameKey))
        ));
        layer.Regions.Add(new MapLayer.Region(regionId, color.Item1, color.Item2, color.Item3, nameKey));
        _mapRegionNames.Add(nameKey, "Region " + (layer.Regions.Count - 1).ToString(CultureInfo.InvariantCulture));
        _structureChanged = true;
        RefreshMapFields();
        return regionId;
    }

    /// <summary>Returns the edited region name or its XML identifier when no custom name exists.</summary>
    internal string GetMapRegionName(MapLayer.Region region)
    {
        var poi = PointsOfInterest.FirstOrDefault(item => item.RegionId == region.Id);
        if (poi != null) return GetPoiText(poi, "Name");
        return region.NameKey != null && TryGetMapText(region.NameKey, out var name) ? name : region.Id;
    }
    /// <summary>Updates the exported localization text for a named map region.</summary>
    internal void SetMapRegionName(MapLayer.Region region, string name)
    {
        var poi = PointsOfInterest.FirstOrDefault(item => item.RegionId == region.Id);
        if (poi != null) { SetPoiText(poi, "Name", name); return; }
        if (region.NameKey == null || GetMapRegionName(region) == name) return;
        if (_mapRegionNames.ContainsKey(region.NameKey)) _mapRegionNames[region.NameKey] = name;
        else _mapTextEdits[region.NameKey] = name;
        _structureChanged = true;
    }

    /// <summary>Checks whether the layer is backed by this world's geographic-region XML.</summary>
    internal bool IsGeographicLayer(string layerId) => (string? )_world.Element("GeographicRegionData")?.Element("RegionSet")?.Attribute("Id") == layerId;
    /// <summary>Builds map-layer and point-of-interest views from the loaded world's terrain XML.</summary>
    private void InitializeMapSettings()
    {
        RefreshMapFields();
        var terrain = _world.Element("TerrainSettings");
        if (terrain == null)
            return;
        MiniMapAssetPath = ResolveImportedAsset((string? )terrain.Element("MiniMap")?.Attribute("Path"));
        TerrainWorldSize = (string? )terrain.Attribute("WorldSize") ?? "(game default)";
        PointOfInterestCount = _world.Descendants("PointOfInterest").Count();
        foreach (var poi in _world.Descendants("PointOfInterest"))
        {
            var id = (string? )poi.Attribute("Id");
            var regionId = (string? )poi.Element("Region")?.Attribute("Id");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(regionId))
                continue;
            PointsOfInterest.Add(new PointOfInterest(id, regionId, (string? )poi.Element("Name")?.Attribute("Key") ?? id));
        }

        foreach (var regionSet in _world.Descendants("RegionSet"))
        {
            var texturePath = (string? )regionSet.Element("Texture")?.Attribute("Path");
            if (string.IsNullOrWhiteSpace(texturePath))
                continue;
            var layer = new MapLayer((string? )regionSet.Attribute("Id") ?? "Map layer", ResolveImportedAsset(texturePath) ?? texturePath);
            foreach (var region in regionSet.Elements("Region"))
            {
                var regionId = (string? )region.Attribute("Id");
                if (string.IsNullOrWhiteSpace(regionId))
                    continue;
                layer.Regions.Add(new MapLayer.Region(
                    regionId,
                    (int? )region.Attribute("R") ?? 255,
                    (int? )region.Attribute("G") ?? 255,
                    (int? )region.Attribute("B") ?? 255,
                    (string? )region.Element("Name")?.Attribute("Key")
                ));
            }

            MapLayers.Add(layer);
        }

        TerrainSummary.Add("Path: " + ((string? )terrain.Attribute("Path") ?? "(not set)"));
        TerrainSummary.Add("World size: " + ((string? )terrain.Attribute("WorldSize") ?? "(game default)"));
        TerrainSummary.Add("Curvature: " + ((string? )terrain.Element("Curvature")?.Attribute("Value") ?? "(game default)"));
        TerrainSummary.Add("Mini-map: " + ((string? )terrain.Element("MiniMap")?.Attribute("Path") ?? "(not set)"));
        TerrainSummary.Add("Lava surface map: " + ((string? )terrain.Element("Lava")?.Element("HeightTexture")?.Attribute("Path") ?? "(not set)"));
        TerrainSummary.Add("Crust and crack maps are compiled into terrain using Terrain Tools.");
    }

    /// <summary>Rebuilds XML controls after replacing boundary rules while retaining unrelated pending edits.</summary>
    private void RefreshMapFields()
    {
        var pending = AdvancedFields.Where(field => field.Target != null && field.Value != field.Original)
            .ToDictionary(field => field.Target!, field => field.Value);
        AdvancedFields.Clear();
        MapFields.Clear();
        AddAllFields(AdvancedFields, _world, "World");
        foreach (var field in AdvancedFields)
            if (field.Target != null && pending.TryGetValue(field.Target, out var value))
                field.Value = value;
        MapFields.AddRange(AdvancedFields.Where(f => f.Label.IndexOf("/RegionSet", StringComparison.Ordinal) >= 0 || f.Label.IndexOf("/PointOfInterest", StringComparison.Ordinal) >= 0 || f.Label.IndexOf("/PlayableArea", StringComparison.Ordinal) >= 0 || f.Label.IndexOf("/GeographicRegionData", StringComparison.Ordinal) >= 0 || f.Label.IndexOf("/DeepMinablesRegionData", StringComparison.Ordinal) >= 0 || f.Label.IndexOf("/Minables/", StringComparison.Ordinal) >= 0 || f.Label.IndexOf("/DeepMinables/", StringComparison.Ordinal) >= 0));
    }
}
