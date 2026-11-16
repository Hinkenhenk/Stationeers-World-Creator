using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private readonly Dictionary<string, string> _mapRgbDrafts = new Dictionary<string, string>();

    /// <summary>Edits a complete RGB triplet and recolours this region without breaking painted pixels.</summary>
    private void DrawMapRgbField(WorldProject.MapLayer layer, WorldProject.MapLayer.Region region)
    {
        GUILayout.Label("RGB", _mutedStyle!);
        var key = layer.Id + "/" + region.Id;
        var text = _mapRgbDrafts.TryGetValue(key, out var draft) ? draft : region.R + "," + region.G + "," + region.B;
        var next = GUILayout.TextField(text, _inputStyle!, GUILayout.Height(32));
        if (next == text) return;
        if (MapRgbValue.TryParse(next, out var rgb))
        {
            try
            {
                ChangeMapColour(layer, region, rgb);
                _mapRgbDrafts.Remove(key);
            }
            catch (Exception error) { _mapRgbDrafts[key] = next; _status = error.Message; }
        }
        else
        {
            _mapRgbDrafts[key] = next;
            _status = "Enter RGB as three whole numbers from 0 to 255, for example 0,255,0.";
        }
    }

    /// <summary>Updates the definition and existing pixels together, with one undo snapshot for both.</summary>
    private void ChangeMapColour(WorldProject.MapLayer layer, WorldProject.MapLayer.Region region, MapRgbValue colour)
    {
        if (region.R == colour.R && region.G == colour.G && region.B == colour.B) return;
        if (layer.Regions.Any(other => other != region && other.R == colour.R && other.G == colour.G && other.B == colour.B))
            throw new InvalidOperationException("That RGB colour already belongs to another region in this mask.");
        LoadMapLayerPreview(layer);
        if (!_mapLayerPreviews.TryGetValue(layer.Id, out var texture) || !_mapLayerPixels.TryGetValue(layer.Id, out var pixels))
            throw new InvalidOperationException("Load this mask before changing its colours.");
        var old = new MapRgbValue((byte)region.R, (byte)region.G, (byte)region.B);
        CaptureMapUndo(layer.Id, (region.Id, old));
        _project!.SetRegionColour(layer, region, colour);
        var replacement = new Color32(colour.R, colour.G, colour.B, 255);
        for (var i = 0; i < pixels.Length; i++)
            if (pixels[i].a != 0 && pixels[i].r == old.R && pixels[i].g == old.G && pixels[i].b == old.B) pixels[i] = replacement;
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        _editedMapLayers.Add(layer.Id);
        CachePoiPositions(layer, texture);
    }
    /// <summary>Aligns a colour cube with the centre of a standard input row.</summary>
    private static void DrawMapColourCube(Color colour)
    {
        var rect = GUILayoutUtility.GetRect(24, 32, GUILayout.Width(24), GUILayout.Height(32));
        rect.y += (rect.height - 20) / 2;
        rect.height = 20;
        var previous = GUI.color;
        GUI.color = colour;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
