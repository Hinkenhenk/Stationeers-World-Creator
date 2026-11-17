using System;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    /// <summary>Explains the replacement pixels and linked records before showing the native delete prompt.</summary>
    private void ConfirmRemoveMapRegion(WorldProject.MapLayer layer, WorldProject.MapLayer.Region region)
    {
        var replacement = layer.Regions.FirstOrDefault(other => other != region);
        var message = "Delete " + _project!.GetMapRegionName(region) + "?\n\n" +
            (replacement == null ? "This is the last region, so its mask will also be removed." :
            "Its painted area will use " + _project.GetMapRegionName(replacement) + ".") +
            " Linked POIs, resource placements and boundary rules will be removed from this draft. Source files stay unchanged.";
        ConfirmEditorAction("Delete region?", message, "Delete", () => RemoveMapRegion(layer, region, replacement));
    }

    /// <summary>Removes a region and remaps its mask pixels to a remaining region without leaving stale undo entries.</summary>
    private void RemoveMapRegion(WorldProject.MapLayer layer, WorldProject.MapLayer.Region region, WorldProject.MapLayer.Region? replacement)
    {
        LoadMapLayerPreview(layer);
        if (replacement != null && (!_mapLayerPreviews.ContainsKey(layer.Id) || !_mapLayerPixels.ContainsKey(layer.Id)))
            throw new InvalidOperationException("The mask must be available before its region can be deleted.");
        _project!.RemoveMapRegion(layer, region);
        _mapUndo.RemoveLayer(layer.Id);
        _mapRgbDrafts.Remove(layer.Id + "/" + region.Id);
        _poiPositions.Remove(region.Id);
        if (replacement != null)
        {
            var pixels = _mapLayerPixels[layer.Id];
            var colour = new Color32((byte)replacement.R, (byte)replacement.G, (byte)replacement.B, 255);
            for (var i = 0; i < pixels.Length; i++)
                if (pixels[i].a != 0 && pixels[i].r == region.R && pixels[i].g == region.G && pixels[i].b == region.B)
                    pixels[i] = colour;
            var texture = _mapLayerPreviews[layer.Id];
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            var png = texture.EncodeToPNG();
            _project.SetMapLayerTexture(layer.Id, png);
            _mapSourcePng[layer.Id] = png;
            _editedMapLayers.Add(layer.Id);
            CachePoiPositions(layer, texture);
        }
        else
        {
            if (_mapLayerPreviews.TryGetValue(layer.Id, out var texture)) Destroy(texture);
            _mapLayerPreviews.Remove(layer.Id);
            _mapLayerPixels.Remove(layer.Id);
            _mapSourcePng.Remove(layer.Id);
            _selectedMapLayers.Remove(layer.Id);
            _editedMapLayers.Remove(layer.Id);
            _createdMapLayers.Remove(layer.Id);
        }
        if (_paintLayerId == layer.Id)
        {
            _paintRegionId = replacement?.Id;
            if (replacement == null) _paintLayerId = _project.MapLayers.FirstOrDefault()?.Id;
        }
        _status = "Deleted the region and updated its mask and world bindings.";
    }
}
