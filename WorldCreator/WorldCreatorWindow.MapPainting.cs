using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private readonly Dictionary<string, Color32[]> _mapLayerPixels = new Dictionary<string, Color32[]>();
    private readonly Dictionary<string, byte[]> _mapSourcePng = new Dictionary<string, byte[]>(StringComparer.Ordinal);
    private readonly HashSet<string> _editedMapLayers = new HashSet<string>();
    private readonly HashSet<string> _createdMapLayers = new HashSet<string>();
    private string? _paintLayerId;
    private string? _paintRegionId;
    private bool _paintMode;
    private bool _eraseMapMode;
    private float _brushRadius = 7f;
    private readonly MapMaskHistory _mapUndo = new MapMaskHistory();
    /// <summary>Loads a region mask and caches its image and pixels for drawing and painting.</summary>
    private void LoadMapLayerPreview(WorldProject.MapLayer layer)
    {
        if (_mapLayerPreviews.ContainsKey(layer.Id))
            return;
        var path = Path.Combine(Application.dataPath, "StreamingAssets", layer.TexturePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
            return;
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            var imageBytes = File.ReadAllBytes(path);
            if (!texture.LoadImage(imageBytes))
            {
                Destroy(texture);
                return;
            }

            texture.name = "World Creator layer " + layer.Id;
            texture.filterMode = FilterMode.Point;
            _mapLayerPreviews.Add(layer.Id, texture);
            _mapSourcePng[layer.Id] = imageBytes;
            _mapLayerPixels[layer.Id] = texture.GetPixels32();
            CachePoiPositions(layer, texture);
        }
        catch (Exception e)
        {
            Destroy(texture);
            Debug.LogWarning("[World Creator] Could not load map layer " + layer.Id + ": " + e.Message);
        }
    }

    /// <summary>Locates representative region pixels used to place point-of-interest markers.</summary>
    private void CachePoiPositions(WorldProject.MapLayer layer, Texture2D texture)
    {
        foreach (var region in layer.Regions)
            _poiPositions.Remove(region.Id);
        var regionIndexes = new Dictionary<uint, int>();
        for (var i = 0; i < layer.Regions.Count; i++)
        {
            var region = layer.Regions[i];
            regionIndexes[ColorKey(region.R, region.G, region.B)] = i;
        }

        if (!_mapLayerPixels.TryGetValue(layer.Id, out var pixels))
        {
            pixels = texture.GetPixels32();
            _mapLayerPixels[layer.Id] = pixels;
        }

        var counts = new int[layer.Regions.Count];
        var xTotals = new long[layer.Regions.Count];
        var yTotals = new long[layer.Regions.Count];
        for (var i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];
            if (!regionIndexes.TryGetValue(ColorKey(pixel.r, pixel.g, pixel.b), out var regionIndex))
                continue;
            xTotals[regionIndex] += i % texture.width;
            yTotals[regionIndex] += i / texture.width;
            counts[regionIndex]++;
        }

        for (var i = 0; i < layer.Regions.Count; i++)
            if (counts[i] > 0)
                _poiPositions[layer.Regions[i].Id] = new Vector2((float)xTotals[i] / counts[i] / Math.Max(1, texture.width - 1), 1f - (float)yTotals[i] / counts[i] / Math.Max(1, texture.height - 1));
    }

    /// <summary>Encodes a mask's RGB color as a lookup key for region matching.</summary>
    private static uint ColorKey(int r, int g, int b) => ((uint)r << 16) | ((uint)g << 8) | (uint)b;
    /// <summary>Handles pointer input inside the map preview and starts or finishes a paint stroke.</summary>
    private void DrawMapPaint(Rect mapRect)
    {
        var input = Event.current;
        if (input.type == EventType.KeyDown && input.control && !input.shift && input.keyCode == KeyCode.Z && GUIUtility.keyboardControl == 0 && _mapUndo.Count > 0)
        {
            UndoMapStroke();
            input.Use();
        }
        if (HandleMapPipette(mapRect)) return;
        if (!_paintMode || _editPlayableArea || _paintLayerId == null || _project!.IsPlayableAreaLayer(_paintLayerId) || (_paintRegionId == null && !_eraseMapMode))
            return;
        var layer = _project!.MapLayers.FirstOrDefault(item => item.Id == _paintLayerId);
        var region = layer == null ? null : _eraseMapMode && _createdMapLayers.Contains(layer.Id) ? layer.Regions.FirstOrDefault() : layer.Regions.FirstOrDefault(item => item.Id == _paintRegionId);
        if (layer == null || region == null || !_selectedMapLayers.Contains(layer.Id) || !_mapLayerPreviews.ContainsKey(layer.Id))
            return;
        var controlId = GUIUtility.GetControlID(FocusType.Passive);
        var current = Event.current;
        if (current.type == EventType.MouseDown && current.button == 0 && _mapInputViewport.Contains(current.mousePosition) && mapRect.Contains(current.mousePosition))
        {
            CaptureMapUndo(layer.Id);
            GUIUtility.keyboardControl = 0;
            _lastPaintedMapPixel = null;
            GUIUtility.hotControl = controlId;
            PaintMapAt(layer, region, mapRect, current.mousePosition);
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && GUIUtility.hotControl == controlId)
        {
            if (_mapInputViewport.Contains(current.mousePosition) && mapRect.Contains(current.mousePosition))
                PaintMapAt(layer, region, mapRect, current.mousePosition);
            else
                _lastPaintedMapPixel = null;
            current.Use();
        }
        else if (current.type == EventType.MouseUp && GUIUtility.hotControl == controlId)
        {
            GUIUtility.hotControl = 0;
            _lastPaintedMapPixel = null;
            CachePoiPositions(layer, _mapLayerPreviews[layer.Id]);
            current.Use();
        }
    }

    /// <summary>Paints the selected region color inside the brush while retaining undo information.</summary>
    private void PaintMapAt(WorldProject.MapLayer layer, WorldProject.MapLayer.Region region, Rect mapRect, Vector2 mouse)
    {
        if (_pixelMapBrush)
        {
            PaintMapPixelLine(layer, region, mapRect, mouse);
            return;
        }
        if (!_mapLayerPreviews.TryGetValue(layer.Id, out var texture) || !_mapLayerPixels.TryGetValue(layer.Id, out var pixels))
            return;
        var centerX = Mathf.Clamp(Mathf.RoundToInt((mouse.x - mapRect.x) / mapRect.width * (texture.width - 1)), 0, texture.width - 1);
        var centerY = Mathf.Clamp(Mathf.RoundToInt((1f - (mouse.y - mapRect.y) / mapRect.height) * (texture.height - 1)), 0, texture.height - 1);
        var radiusX = Mathf.Max(1, Mathf.RoundToInt(_brushRadius));
        var radiusY = radiusX;
        var minX = Mathf.Max(0, centerX - radiusX);
        var maxX = Mathf.Min(texture.width - 1, centerX + radiusX);
        var minY = Mathf.Max(0, centerY - radiusY);
        var maxY = Mathf.Min(texture.height - 1, centerY + radiusY);
        var blockWidth = maxX - minX + 1;
        var blockHeight = maxY - minY + 1;
        var block = new Color32[blockWidth * blockHeight];
        var color = new Color32((byte)region.R, (byte)region.G, (byte)region.B, 255);
        var normalizedRadiusX = Math.Max(1f, radiusX);
        var normalizedRadiusY = Math.Max(1f, radiusY);
        for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX; x++)
            {
                var dx = (x - centerX) / normalizedRadiusX;
                var dy = (y - centerY) / normalizedRadiusY;
                var index = (y - minY) * blockWidth + x - minX;
                var pixelIndex = y * texture.width + x;
                block[index] = dx * dx + dy * dy <= 1f ? color : pixels[pixelIndex];
            }

        texture.SetPixels32(minX, minY, blockWidth, blockHeight, block);
        texture.Apply(false, false);
        for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX; x++)
                pixels[y * texture.width + x] = block[(y - minY) * blockWidth + x - minX];
        _editedMapLayers.Add(layer.Id);
    }

    /// <summary>Restores the previous mask image and its edited-state flag.</summary>
    private void UndoMapStroke()
    {
        if (_mapUndo.Count == 0)
            return;
        var previous = _mapUndo.Peek();
        if (!_mapLayerPreviews.TryGetValue(previous.LayerId, out var texture)) return;
        if (!texture.LoadImage(previous.Png))
        {
            _status = "Could not restore the previous map mask.";
            return;
        }

        _mapLayerPixels[previous.LayerId] = texture.GetPixels32();
        if (previous.HadEdits)
        {
            _editedMapLayers.Add(previous.LayerId);
            _project!.SetMapLayerTexture(previous.LayerId, previous.Png);
        }
        else
        {
            _editedMapLayers.Remove(previous.LayerId);
            _project!.ClearMapLayerTexture(previous.LayerId);
        }
        var layer = _project!.MapLayers.First(item => item.Id == previous.LayerId);
        if (previous.ColourChange is var change && change.HasValue)
        {
            _project.SetRegionColour(layer, layer.Regions.First(region => region.Id == change.Value.RegionId), change.Value.Previous);
            _mapRgbDrafts.Remove(layer.Id + "/" + change.Value.RegionId);
        }
        CachePoiPositions(layer, texture);
        _mapUndo.Pop();
        _status = "Restored the map mask before the last change.";
    }

    /// <summary>Snapshots the selected mask before a change replaces its pixels.</summary>
    private void CaptureMapUndo(string layerId, (string RegionId, MapRgbValue Previous)? colourChange = null)
    {
        if (!_mapLayerPreviews.TryGetValue(layerId, out var texture) || texture == null)
            return;
        _mapUndo.Push(layerId, texture.EncodeToPNG(), _editedMapLayers.Contains(layerId), colourChange);
    }

    /// <summary>Restores the selected layer's original mask or its newly created blank base.</summary>
    private void RestoreMapMask()
    {
        if (_paintLayerId == null || !_mapSourcePng.TryGetValue(_paintLayerId, out var sourcePng))
            return;
        var layer = _project!.MapLayers.FirstOrDefault(item => item.Id == _paintLayerId);
        if (layer == null)
        {
            _status = "Select a map layer before restoring its mask.";
            return;
        }

        var replacement = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!replacement.LoadImage(sourcePng))
                throw new InvalidDataException("The source map mask could not be decoded as a PNG.");
            CaptureMapUndo(layer.Id);
            replacement.name = "World Creator layer " + layer.Id + " (source mask)";
            if (_mapLayerPreviews.TryGetValue(layer.Id, out var previous) && previous != null)
                Destroy(previous);
            _mapLayerPreviews[layer.Id] = replacement;
            _mapLayerPixels[layer.Id] = replacement.GetPixels32();
            if (_createdMapLayers.Contains(layer.Id))
            {
                _project.SetMapLayerTexture(layer.Id, sourcePng);
                _editedMapLayers.Add(layer.Id);
            }
            else
            {
                _project.ClearMapLayerTexture(layer.Id);
                _editedMapLayers.Remove(layer.Id);
            }

            CachePoiPositions(layer, replacement);
            _status = "Restored the source mask for " + layer.Id + ". Undo is available.";
            replacement = null!;
        }
        catch (Exception e)
        {
            _status = "Could not restore source mask: " + e.Message;
            Debug.LogWarning("[World Creator] Could not restore source map mask: " + e);
        }
        finally
        {
            if (replacement != null)
                Destroy(replacement);
        }
    }
}
