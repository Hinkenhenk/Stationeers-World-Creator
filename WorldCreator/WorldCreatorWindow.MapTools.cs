using System;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private bool _mapFilesOpen;

    /// <summary>Groups the active mask, tool and region palette below the map.</summary>
    private void DrawMapToolsCard()
    {
        GUILayout.BeginVertical(_cardStyle!);
        GUILayout.BeginHorizontal();
        DrawMapMaskSelector();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(10);
        DrawMapToolSelector();
        GUILayout.Space(8);
        DrawMapBrushSettings();
        if (_editPlayableArea)
        {
            GUILayout.Space(10);
            DrawPlayableAreaControls();
        }
        else if (_pipetteMapMode) GUILayout.Label("Click the map to pick a region colour.", _mutedStyle!);
        foreach (var layer in _project!.MapLayers.Where(layer => layer.Id == _paintLayerId))
            DrawMapLayerRegions(layer);
        GUILayout.EndVertical();
    }

    /// <summary>Places optional image and mask file actions above the map.</summary>
    private void DrawMapFilesCard()
    {
        GUILayout.BeginVertical(_cardStyle!);
        if (NativeButton(_mapFilesOpen ? "Mask files ▾" : "Mask files ▸", _buttonStyle!, GUILayout.Width(160), GUILayout.Height(32)))
            _mapFilesOpen = !_mapFilesOpen;
        if (_mapFilesOpen) DrawMapFileActions();
        GUILayout.EndVertical();
    }

    /// <summary>Uses one mode selector so painting, picking and boundary editing cannot compete for input.</summary>
    private void DrawMapToolSelector()
    {
        var playableMask = _paintLayerId != null && _project!.IsPlayableAreaLayer(_paintLayerId);
        _editPlayableArea &= playableMask;
        var current = _editPlayableArea ? 3 : _pipetteMapMode ? 2 : _paintMode ? 1 : 0;
        GUILayout.BeginHorizontal();
        GUILayout.Label("Tool", _mutedStyle!, GUILayout.Width(50));
        var tools = playableMask ? new[] { "Inspect", "Paint", "Pick colour", "Playable area" }
            : new[] { "Inspect", "Paint", "Pick colour" };
        var next = NativeDropdown("map-tool", current, tools,
            _buttonStyle!, GUILayout.Width(180), GUILayout.Height(32));
        if (next != current)
        {
            _editPlayableArea = playableMask && next == 3;
            _pipetteMapMode = next == 2;
            _paintMode = next == 1;
            _eraseMapMode = false;
            if ((next == 1 || next == 2) && _paintLayerId != null && _selectedMapLayers.Add(_paintLayerId))
                LoadMapLayerPreview(_project!.MapLayers.Single(layer => layer.Id == _paintLayerId));
        }
        GUILayout.Space(12);
        var enabled = GUI.enabled;
        GUI.enabled = enabled && _mapUndo.Count > 0;
        if (NativeButton("Undo", _buttonStyle!, GUILayout.Width(90), GUILayout.Height(32))) UndoMapStroke();
        GUI.enabled = enabled;
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    /// <summary>Allows inspecting and deleting regions in every mask, including playable boundaries.</summary>
    private void DrawMapMaskSelector()
    {
        var layers = _project!.MapLayers.ToArray();
        if (layers.Length == 0)
        {
            GUILayout.Label("Create a geographic layer in Map Layers to begin.", _mutedStyle!);
            return;
        }
        GUILayout.Label("Mask", _mutedStyle!, GUILayout.Width(50));
        var current = Math.Max(0, Array.FindIndex(layers, layer => layer.Id == _paintLayerId));
        var next = NativeDropdown("paint-mask-layer", current, layers.Select(layer => HumanizeXmlName(layer.Id)).ToArray(),
            _buttonStyle!, GUILayout.Width(300), GUILayout.Height(32));
        var layer = layers[next];
        if (_paintLayerId != layer.Id)
        {
            _paintLayerId = layer.Id;
            _paintRegionId = null;
            _eraseMapMode = false;
        }
        if (_paintMode && _project.IsPlayableAreaLayer(layer.Id)) _paintMode = false;
    }

    /// <summary>Keeps brush settings visible so a brush can be prepared before choosing Paint.</summary>
    private void DrawMapBrushSettings()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Brush", _mutedStyle!, GUILayout.Width(50));
        _pixelMapBrush = NativeDropdown("map-brush", _pixelMapBrush ? 0 : 1, new[] { "1 pixel", "Round brush" },
            _buttonStyle!, GUILayout.Width(180), GUILayout.Height(32)) == 0;
        if (!_pixelMapBrush)
        {
            _brushRadius = GUILayout.HorizontalSlider(_brushRadius, 1, 24, GUILayout.Width(180));
            GUILayout.Label(Mathf.RoundToInt(_brushRadius) + " px", _mutedStyle!, GUILayout.Width(60));
        }
        if (_paintLayerId != null && _createdMapLayers.Contains(_paintLayerId))
            _eraseMapMode = GUILayout.Toggle(_eraseMapMode, "Erase to base colour");
        GUILayout.EndHorizontal();
        if (_paintMode)
            GUILayout.Label("Choose a region colour below, then drag to paint. Ctrl+Z undoes a stroke.", _mutedStyle!);
    }

    /// <summary>Keeps import, source restoration and PNG export out of the main painting toolbar.</summary>
    private void DrawMapFileActions()
    {
        DrawMapReferencePicker();
        DrawMapStartingMaskPicker();
        GUILayout.BeginHorizontal();
        var enabled = GUI.enabled;
        GUI.enabled = enabled && _paintLayerId != null && !_project!.IsPlayableAreaLayer(_paintLayerId) &&
            _selectedMapLayers.Contains(_paintLayerId) && _mapSourcePng.ContainsKey(_paintLayerId);
        if (NativeButton("Restore mask", _buttonStyle!, GUILayout.Width(160), GUILayout.Height(32))) RestoreMapMask();
        GUI.enabled = enabled && _paintLayerId != null && _selectedMapLayers.Contains(_paintLayerId);
        if (NativeButton("Save mask PNG", _buttonStyle!, GUILayout.Width(160), GUILayout.Height(32))) ExportMapMaskPng();
        GUI.enabled = enabled;
        GUILayout.EndHorizontal();
    }
}
