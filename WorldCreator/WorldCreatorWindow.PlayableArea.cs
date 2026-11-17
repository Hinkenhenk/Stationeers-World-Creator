using System;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private bool _editPlayableArea;
    private PlayableAreaShape _playableAreaShape = new PlayableAreaShape();
    private int _selectedBoundaryCorner = -1;
    private int _boundaryDragHandle;
    private PlayableAreaShape.Point _boundaryDragPrevious;

    /// <summary>Edits the active shape without changing the world until Apply is pressed.</summary>
    private void DrawPlayableAreaControls()
    {
        _paintMode = false;
        GUILayout.BeginHorizontal();
        GUILayout.Label("Shape", _mutedStyle!, GUILayout.Width(50));
        _playableAreaShape.Kind = (PlayableAreaShape.ShapeKind)NativeDropdown("playable-shape", (int)_playableAreaShape.Kind,
            new[] { "Polygon", "Square", "Circle" }, _buttonStyle!, GUILayout.Width(180), GUILayout.Height(32));
        GUILayout.Space(10);
        if (NativeButton("Centre area", _buttonStyle!, GUILayout.Width(150), GUILayout.Height(32)))
            _playableAreaShape.MoveBy(.5 - _playableAreaShape.Centre.X, .5 - _playableAreaShape.Centre.Y);
        GUILayout.Space(10);
        if (_playableAreaShape.Kind == PlayableAreaShape.ShapeKind.Polygon)
            DrawPolygonCornerControls();
        GUILayout.Space(10);
        if (NativeButton("Apply area", _buttonStyle!, GUILayout.Width(140), GUILayout.Height(32)))
            ApplyPlayableArea();
        GUILayout.EndHorizontal();
        GUILayout.Label(_playableAreaShape.Kind == PlayableAreaShape.ShapeKind.Polygon
            ? "Drag corners to reshape; click an edge to add a corner. Drag inside to move. Apply area commits the boundary."
            : "Drag inside to move; drag the edge to resize. Apply area commits the boundary.", _mutedStyle!);
    }

    /// <summary>Adds a handle after the selected corner and allows correction without dropping below three vertices.</summary>
    private void DrawPolygonCornerControls()
    {
        var points = _playableAreaShape.Corners;
        var enabled = GUI.enabled;
        GUI.enabled = enabled && points.Count < PlayableAreaShape.MaximumCorners;
        if (NativeButton("Add corner", _buttonStyle!, GUILayout.Width(140), GUILayout.Height(32)))
        {
            var edge = _selectedBoundaryCorner >= 0 && _selectedBoundaryCorner < points.Count ? _selectedBoundaryCorner : 0;
            var a = points[edge];
            var b = points[(edge + 1) % points.Count];
            if (_playableAreaShape.TryAddCorner(edge, new PlayableAreaShape.Point((a.X + b.X) / 2, (a.Y + b.Y) / 2)))
                _selectedBoundaryCorner = edge + 1;
        }
        GUILayout.Space(10);
        GUI.enabled = enabled && points.Count > 3 && _selectedBoundaryCorner >= 0;
        if (NativeButton("Remove corner", _buttonStyle!, GUILayout.Width(150), GUILayout.Height(32)) && _playableAreaShape.TryRemoveCorner(_selectedBoundaryCorner))
            _selectedBoundaryCorner = -1;
        GUI.enabled = enabled;
        GUILayout.Label(points.Count + " / " + PlayableAreaShape.MaximumCorners + " corners", _mutedStyle!, GUILayout.Width(140));
    }

    /// <summary>Draws editable handles in the same normalised coordinates used to generate the mask.</summary>
    private void DrawPlayableArea(Rect mapRect)
    {
        if (!_editPlayableArea) return;
        var circle = _playableAreaShape.Kind == PlayableAreaShape.ShapeKind.Circle;
        var centre = _playableAreaShape.Centre;
        var points = circle ? Enumerable.Range(0, 96).Select(i => new PlayableAreaShape.Point(
            centre.X + Math.Cos(i * Math.PI * 2 / 96) * _playableAreaShape.Radius,
            centre.Y + Math.Sin(i * Math.PI * 2 / 96) * _playableAreaShape.Radius)).ToArray() : _playableAreaShape.Outline.ToArray();
        for (var i = 0; i < points.Length; i++)
        {
            DrawPlayableAreaLine(MapAreaPoint(mapRect, points[i]), MapAreaPoint(mapRect, points[(i + 1) % points.Length]));
            if (!circle || i % 24 == 0)
                DrawPlayableHandle(MapAreaPoint(mapRect, points[i]), !circle && _selectedBoundaryCorner == i);
        }
        var marker = MapAreaPoint(mapRect, centre);
        GUI.Label(new Rect(marker.x - 5, marker.y - 10, 20, 20), "+", _mutedStyle!);
        HandlePlayableAreaDrag(mapRect);
    }

    /// <summary>Keeps outline rendering inside the map and parent scroll clip without transforming the GUI matrix.</summary>
    private void DrawPlayableAreaLine(Vector2 from, Vector2 to)
    {
        if (Event.current.type != EventType.Repaint) return;
        var previous = GUI.color;
        GUI.color = new Color(.3f, 1, .3f);
        foreach (var run in MapOutlineDrawing.Runs(from.x, from.y, to.x, to.y,
            Mathf.CeilToInt(_mapInputViewport.width), Mathf.CeilToInt(_mapInputViewport.height)))
            GUI.DrawTexture(new Rect(run.X, run.Y, run.Width + 1, run.Height + 1), Texture2D.whiteTexture);
        GUI.color = previous;
    }

    /// <summary>Draws a clear corner or resize handle without leaking GUI colour changes.</summary>
    private static void DrawPlayableHandle(Vector2 point, bool selected)
    {
        var previous = GUI.color;
        GUI.color = selected ? new Color(1, .5f, 0) : Color.white;
        GUI.DrawTexture(new Rect(point.x - 5, point.y - 5, 10, 10), Texture2D.whiteTexture);
        GUI.color = previous;
    }

    /// <summary>Converts a geometry point into the square map's local GUI coordinates.</summary>
    private static Vector2 MapAreaPoint(Rect mapRect, PlayableAreaShape.Point point) => new Vector2(mapRect.x + (float)point.X * mapRect.width, mapRect.y + (float)point.Y * mapRect.height);

    /// <summary>Captures one drag for a handle or the shape body and releases it even outside the map.</summary>
    private void HandlePlayableAreaDrag(Rect mapRect)
    {
        var control = GUIUtility.GetControlID(FocusType.Passive);
        var current = Event.current;
        var mouse = new PlayableAreaShape.Point((current.mousePosition.x - mapRect.x) / mapRect.width, (current.mousePosition.y - mapRect.y) / mapRect.height);
        if (current.type == EventType.MouseDown && current.button == 0 && _mapInputViewport.Contains(current.mousePosition) && mapRect.Contains(current.mousePosition))
        {
            var handle = HitPlayableArea(mouse, 12 / mapRect.width);
            if (handle == int.MinValue) return;
            _boundaryDragHandle = handle;
            _boundaryDragPrevious = mouse;
            GUIUtility.hotControl = control;
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && GUIUtility.hotControl == control)
        {
            DragPlayableArea(mouse);
            _boundaryDragPrevious = mouse;
            current.Use();
        }
        else if (current.type == EventType.MouseUp && GUIUtility.hotControl == control)
        {
            GUIUtility.hotControl = 0;
            current.Use();
        }
    }

    /// <summary>Prioritises resize handles, then polygon edge insertion, then whole-area movement.</summary>
    private int HitPlayableArea(PlayableAreaShape.Point mouse, double tolerance)
    {
        var centre = _playableAreaShape.Centre;
        if (_playableAreaShape.Kind == PlayableAreaShape.ShapeKind.Circle)
        {
            var distance = Math.Sqrt((mouse.X - centre.X) * (mouse.X - centre.X) + (mouse.Y - centre.Y) * (mouse.Y - centre.Y));
            if (Math.Abs(distance - _playableAreaShape.Radius) <= tolerance) return -2;
        }
        else
        {
            var points = _playableAreaShape.Outline;
            for (var i = 0; i < points.Count; i++)
            {
                if (Math.Abs(points[i].X - mouse.X) > tolerance || Math.Abs(points[i].Y - mouse.Y) > tolerance) continue;
                _selectedBoundaryCorner = i;
                return i;
            }
        }
        if (_playableAreaShape.Kind == PlayableAreaShape.ShapeKind.Polygon && _playableAreaShape.DistanceToBoundary(mouse) <= tolerance)
        {
            var edge = _playableAreaShape.NearestEdge(mouse);
            if (_playableAreaShape.TryAddCorner(edge, mouse)) return _selectedBoundaryCorner = edge + 1;
        }
        return _playableAreaShape.Contains(mouse) ? -1 : int.MinValue;
    }

    /// <summary>Moves independent polygon vertices, resizes regular shapes, or translates the entire area.</summary>
    private void DragPlayableArea(PlayableAreaShape.Point mouse)
    {
        if (_boundaryDragHandle == -1)
            _playableAreaShape.MoveBy(mouse.X - _boundaryDragPrevious.X, mouse.Y - _boundaryDragPrevious.Y);
        else if (_playableAreaShape.Kind == PlayableAreaShape.ShapeKind.Polygon)
        {
            if (!_playableAreaShape.TryMoveCorner(_boundaryDragHandle, mouse))
                _status = "Keep corners separated and edges uncrossed. The last valid shape is preserved.";
        }
        else
        {
            var dx = Math.Abs(mouse.X - _playableAreaShape.Centre.X);
            var dy = Math.Abs(mouse.Y - _playableAreaShape.Centre.Y);
            _playableAreaShape.SetRadius(_playableAreaShape.Kind == PlayableAreaShape.ShapeKind.Circle ? Math.Sqrt(dx * dx + dy * dy) : Math.Max(dx, dy));
        }
    }

    /// <summary>Encodes the active geometry and updates only the editable world draft and its preview cache.</summary>
    private void ApplyPlayableArea()
    {
        Texture2D? texture = null;
        try
        {
            texture = new Texture2D(PlayableAreaShape.Resolution, PlayableAreaShape.Resolution, TextureFormat.RGB24, false);
            texture.filterMode = FilterMode.Point;
            texture.LoadRawTextureData(_playableAreaShape.CreateRgb());
            texture.Apply();
            var oldBoundaries = _project!.MapLayers.Where(layer => _project.IsPlayableAreaLayer(layer.Id)).Select(layer => layer.Id).ToArray();
            var id = _project.SetPlayableAreaMask(texture.EncodeToPNG());
            foreach (var previousBoundary in oldBoundaries)
                _selectedMapLayers.Remove(previousBoundary);
            if (_mapLayerPreviews.TryGetValue(id, out var previous)) Destroy(previous);
            _mapLayerPreviews[id] = texture;
            _mapLayerPixels[id] = texture.GetPixels32();
            _selectedMapLayers.Add(id);
            _editedMapLayers.Add(id);
            _paintLayerId = id;
            _paintRegionId = null;
            _status = "Applied the playable area. Create the world mod to export it; source game files are unchanged.";
            texture = null;
        }
        catch (Exception error)
        {
            _status = "Could not apply playable area: " + error.Message;
            Debug.LogWarning("[World Creator] " + _status);
        }
        finally
        {
            if (texture != null) Destroy(texture);
        }
    }
}
