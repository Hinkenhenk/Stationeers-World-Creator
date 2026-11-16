using System;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private float _mapZoom = 1;
    private Vector2 _mapPan;
    private bool _mapPanning;
    private Vector2 _mapPanPointer;
    private int _mapPanControl;
    private Rect _mapInputViewport;
    private bool _pixelMapBrush = true;
    private bool _pipetteMapMode;
    private (int X, int Y)? _lastPaintedMapPixel;

    /// <summary>Shows the loaded playable mask and seeds shape handles only when its valid region is an exact rectangle.</summary>
    private void LoadPlayableAreaReference()
    {
        var layer = _project?.MapLayers.FirstOrDefault(item => _project.IsPlayableAreaLayer(item.Id));
        if (layer == null) return;
        LoadMapLayerPreview(layer);
        if (!_mapLayerPreviews.TryGetValue(layer.Id, out var texture) || !_mapLayerPixels.TryGetValue(layer.Id, out var pixels)) return;
        _selectedMapLayers.Add(layer.Id);
        var green = layer.Regions.FirstOrDefault(region => region.R == 0 && region.G == 255 && region.B == 0);
        if (green == null) return;
        var minX = texture.width;
        var minY = texture.height;
        var maxX = -1;
        var maxY = -1;
        var count = 0;
        for (var i = 0; i < pixels.Length; i++)
        {
            var pixel = pixels[i];
            if (pixel.r != 0 || pixel.g != 255 || pixel.b != 0 || pixel.a == 0) continue;
            var x = i % texture.width;
            var y = i / texture.width;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
            count++;
        }
        if (count == 0 || count != (maxX - minX + 1) * (maxY - minY + 1)) return;
        _playableAreaShape.SetReferenceRectangle(new PlayableAreaShape.Point((double)minX / texture.width, 1 - (double)(maxY + 1) / texture.height),
            new PlayableAreaShape.Point((double)(maxX + 1) / texture.width, 1 - (double)minY / texture.height));
    }

    /// <summary>Provides zoom and reset actions below the canvas without changing world data.</summary>
    private void DrawMapZoomControls()
    {
        GUILayout.BeginHorizontal();
        if (NativeButton("−", _buttonStyle!, GUILayout.Width(38), GUILayout.Height(32))) SetMapZoom(_mapZoom / 2, Vector2.zero);
        GUILayout.Label((_mapZoom * 100).ToString("0") + "%", _mutedStyle!, GUILayout.Width(70));
        if (NativeButton("+", _buttonStyle!, GUILayout.Width(38), GUILayout.Height(32))) SetMapZoom(_mapZoom * 2, Vector2.zero);
        if (NativeButton("Reset map view", _buttonStyle!, GUILayout.Width(170), GUILayout.Height(32))) { _mapZoom = 1; _mapPan = Vector2.zero; }
        GUILayout.EndHorizontal();
        GUILayout.Label("Mouse wheel over the map: zoom. Hold the wheel button and drag to pan. Masks stay sharp so you can paint individual pixels.", _mutedStyle!);
    }

    /// <summary>Clips the zoomed map to a fixed square viewport and captures only zoom and pan gestures.</summary>
    private Rect BeginMapViewport(Rect viewport)
    {
        GUI.BeginGroup(viewport);
        var local = new Rect(0, 0, viewport.width, viewport.height);
        _mapInputViewport = local;
        var current = Event.current;
        var control = GUIUtility.GetControlID("WorldCreatorMapPan".GetHashCode(), FocusType.Passive);
        var raw = current.rawType;
        if (current.type == EventType.ScrollWheel && local.Contains(current.mousePosition) && _nativePopupOpen == null && !_helpOpen)
        {
            SetMapZoom(_mapZoom * Mathf.Pow(1.2f, -current.delta.y), current.mousePosition - local.center);
            current.Use();
        }
        else if (raw == EventType.MouseDown && current.button == 2 && local.Contains(current.mousePosition) && _nativePopupOpen == null && !_helpOpen)
        {
            _mapPanning = true;
            _mapPanPointer = current.mousePosition;
            _mapPanControl = control;
            GUIUtility.hotControl = control;
            current.Use();
        }
        else if (_mapPanning && raw == EventType.MouseDrag)
        {
            _mapPan += current.mousePosition - _mapPanPointer;
            _mapPanPointer = current.mousePosition;
            current.Use();
        }
        else if (_mapPanning && ((raw == EventType.MouseUp && current.button == 2) || raw == EventType.MouseLeaveWindow ||
            (raw == EventType.Repaint && !Input.GetMouseButton(2))))
        {
            _mapPanning = false;
            if (GUIUtility.hotControl == _mapPanControl) GUIUtility.hotControl = 0;
            if (current.type != EventType.Repaint) current.Use();
        }
        var limit = local.width * (_mapZoom - 1) / 2;
        _mapPan.x = Mathf.Clamp(_mapPan.x, -limit, limit);
        _mapPan.y = Mathf.Clamp(_mapPan.y, -limit, limit);
        var size = local.width * _mapZoom;
        return new Rect(local.center.x + _mapPan.x - size / 2, local.center.y + _mapPan.y - size / 2, size, size);
    }

    /// <summary>Keeps the point under the pointer fixed while bounding zoom to a usable range.</summary>
    private void SetMapZoom(float value, Vector2 anchor)
    {
        var next = Mathf.Clamp(value, 1, 32);
        _mapPan = anchor - (anchor - _mapPan) * (next / _mapZoom);
        _mapZoom = next;
    }

    /// <summary>Samples the active layer's original RGB pixel, never the blended colours displayed by overlays.</summary>
    private bool HandleMapPipette(Rect mapRect)
    {
        if (!_pipetteMapMode) return false;
        var current = Event.current;
        if (current.type != EventType.MouseDown || current.button != 0 || !_mapInputViewport.Contains(current.mousePosition) || !mapRect.Contains(current.mousePosition)) return true;
        var layer = _project!.MapLayers.Find(item => item.Id == _paintLayerId);
        if (layer == null || !_mapLayerPreviews.TryGetValue(layer.Id, out var texture) || !_mapLayerPixels.TryGetValue(layer.Id, out var pixels)) return true;
        var pixel = pixels[MapMaskEditing.PixelIndex((current.mousePosition.x - mapRect.x) / mapRect.width,
            (current.mousePosition.y - mapRect.y) / mapRect.height, texture.width, texture.height)];
        var region = layer.Regions.Find(item => item.R == pixel.r && item.G == pixel.g && item.B == pixel.b);
        if (pixel.a == 0 || region == null) _status = "That pixel does not belong to a defined region in the selected layer.";
        else
        {
            _paintRegionId = region.Id;
            _eraseMapMode = false;
            _pipetteMapMode = false;
            _paintMode = true;
            _status = "Selected " + _project.GetMapRegionName(region) + " from the mask. Drag to paint; Ctrl+Z undoes a stroke.";
        }
        GUIUtility.keyboardControl = 0;
        current.Use();
        return true;
    }

    /// <summary>Draws a gap-free stroke of exact pixels, including a single pixel on a click.</summary>
    private void PaintMapPixelLine(WorldProject.MapLayer layer, WorldProject.MapLayer.Region region, Rect mapRect, Vector2 mouse)
    {
        var texture = _mapLayerPreviews[layer.Id];
        var pixels = _mapLayerPixels[layer.Id];
        var index = MapMaskEditing.PixelIndex((mouse.x - mapRect.x) / mapRect.width, (mouse.y - mapRect.y) / mapRect.height, texture.width, texture.height);
        var end = (X: index % texture.width, Y: index / texture.width);
        var start = _lastPaintedMapPixel ?? end;
        var color = new Color32((byte)region.R, (byte)region.G, (byte)region.B, 255);
        foreach (var point in MapMaskEditing.Line(start.X, start.Y, end.X, end.Y))
        {
            pixels[point.Y * texture.width + point.X] = color;
            texture.SetPixel(point.X, point.Y, color);
        }
        texture.Apply(false, false);
        _lastPaintedMapPixel = end;
        _editedMapLayers.Add(layer.Id);
    }
}
