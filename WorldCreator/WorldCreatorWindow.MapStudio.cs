using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private Texture2D? _mapPreview;
    private Texture2D? _mapReferencePreview;
    private int _mapReferenceSelection;
    private readonly Dictionary<string, Texture2D> _mapLayerPreviews = new Dictionary<string, Texture2D>();
    private readonly Dictionary<string, Vector2> _poiPositions = new Dictionary<string, Vector2>();
    private readonly HashSet<string> _selectedMapLayers = new HashSet<string>();
    private Vector2 _mapLayerScroll;
    private int _mapMaskAssetSelection;
    private readonly Dictionary<string, Vector2> _mapRegionScrolls = new Dictionary<string, Vector2>();
    /// <summary>Releases old map editing state and loads the current world minimap when available.</summary>
    private void LoadMapPreview()
    {
        if (_mapPreview != null)
            Destroy(_mapPreview);
        _mapPreview = null;
        foreach (var texture in _mapLayerPreviews.Values)
            if (texture != null)
                Destroy(texture);
        _mapLayerPreviews.Clear();
        _mapLayerPixels.Clear();
        _mapSourcePng.Clear();
        _editedMapLayers.Clear();
        _createdMapLayers.Clear();
        _poiPositions.Clear();
        _selectedMapLayers.Clear();
        _paintLayerId = null;
        _paintRegionId = null;
        _paintMode = false;
        _eraseMapMode = false;
        _brushRadius = 7f;
        _mapUndo.Clear();
        _editPlayableArea = false;
        _playableAreaShape = new PlayableAreaShape();
        _selectedBoundaryCorner = -1;
        _mapZoom = 1;
        _mapPan = Vector2.zero;
        _mapPanning = false;
        _mapRgbDrafts.Clear();
        _pixelMapBrush = true;
        _pipetteMapMode = false;
        _lastPaintedMapPixel = null;
        foreach (var boundary in _project!.MapLayers.Where(layer => _project.IsPlayableAreaLayer(layer.Id)))
        {
            _selectedMapLayers.Add(boundary.Id);
            LoadMapLayerPreview(boundary);
        }
        LoadPlayableAreaReference();
        var relative = _project?.MiniMapAssetPath;
        if (string.IsNullOrWhiteSpace(relative))
            return;
        var path = Path.Combine(Application.dataPath, "StreamingAssets", relative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
            return;
        var minimapTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!minimapTexture.LoadImage(File.ReadAllBytes(path)))
            {
                Destroy(minimapTexture);
                return;
            }

            minimapTexture.name = "World Creator minimap";
            _mapPreview = minimapTexture;
        }
        catch (Exception e)
        {
            Destroy(minimapTexture);
            Debug.LogWarning("[World Creator] Could not load minimap preview: " + e.Message);
        }
    }

    /// <summary>Arranges the square world map above its layer, mask and painting tools.</summary>
    private void DrawMapPreview()
    {
        GUILayout.BeginVertical(_rowStyle!);
        GUILayout.Label("WORLD OVERVIEW", _sectionStyle!);
        GUILayout.Label("World-size setting: " + _project!.TerrainWorldSize + "    ·    Points of interest: " + _project!.PointOfInterestCount, _mutedStyle!);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        var baseMapImage = _mapReferencePreview ?? _mapPreview;
        if (baseMapImage == null)
            GUILayout.Label("Blank geographic canvas. Add a PNG/JPG from the Asset library if you want a visual guide.", _mutedStyle!);
        {
            var size = Math.Max(220, Math.Min(PageContentWidth - 400, _window.height - 380));
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUI.Box(rect, "");
            var width = rect.width - 16;
            var height = width;
            var mapRect = BeginMapViewport(new Rect(rect.center.x - width / 2, rect.center.y - height / 2, width, height));
            if (baseMapImage != null)
                GUI.DrawTexture(mapRect, baseMapImage, ScaleMode.ScaleToFit);
            else
            {
                var previousColor = GUI.color;
                GUI.color = new Color(0.13f, 0.16f, 0.18f, 1f);
                GUI.DrawTexture(mapRect, Texture2D.whiteTexture);
                GUI.color = previousColor;
            }

            foreach (var layer in _project!.MapLayers.Where(layer => _selectedMapLayers.Contains(layer.Id)))
                if (_mapLayerPreviews.TryGetValue(layer.Id, out var overlay) && overlay != null)
                {
                    var previous = GUI.color;
                    GUI.color = new Color(1, 1, 1, 0.48f);
                    GUI.DrawTexture(mapRect, overlay, ScaleMode.StretchToFill);
                    GUI.color = previous;
                }

            foreach (var poi in _project!.PointsOfInterest)
            {
                var poiLayer = _project.MapLayers.FirstOrDefault(layer => layer.Regions.Any(region => region.Id == poi.RegionId));
                if (poiLayer == null || !_selectedMapLayers.Contains(poiLayer.Id) || !_poiPositions.TryGetValue(poi.RegionId, out var position)) continue;
                var point = new Vector2(mapRect.x + position.x * mapRect.width, mapRect.y + position.y * mapRect.height);
                var previous = GUI.color;
                GUI.color = new Color(1, .78f, .25f);
                GUI.DrawTexture(new Rect(point.x - 5, point.y - 5, 10, 10), Texture2D.whiteTexture);
                GUI.color = previous;
                GUI.Label(new Rect(point.x + 7, point.y - 9, 150, 18), _project.GetPoiText(poi, "Name"), _mutedStyle!);
            }
            DrawPlayableArea(mapRect);
            DrawMapPaint(mapRect);
            GUI.EndGroup();
        }
        GUILayout.Label("Edited masks are packaged with this world; source files stay unchanged.", _mutedStyle!);
        GUILayout.EndVertical();
        GUILayout.Space(CardSpacing);
        DrawMapLayerSelector();
        GUILayout.EndHorizontal();
        GUILayout.Space(CardSpacing);
        DrawMapToolsCard();
        GUILayout.EndVertical();
    }

    /// <summary>Creates and selects a blank geographic mask while preserving cleanup on failure.</summary>
    private void CreateGeographicLayer()
    {
        Texture2D? texture = null;
        try
        {
            texture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.name = "World Creator blank geographic map";
            var pixels = new Color32[512 * 512];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 255, 0, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            var initialPng = texture.EncodeToPNG();
            var id = _project!.AddGeographicLayer(initialPng);
            _mapLayerPreviews[id] = texture;
            _mapLayerPixels[id] = pixels;
            _mapSourcePng[id] = initialPng;
            _createdMapLayers.Add(id);
            _selectedMapLayers.Add(id);
            _editedMapLayers.Add(id);
            _paintLayerId = id;
            _paintRegionId = _project!.MapLayers.Last(layer => layer.Id == id).Regions[0].Id;
            CachePoiPositions(_project!.MapLayers.Last(layer => layer.Id == id), texture);
            _status = "Created an RGB24 geographic region layer. Add named regions and paint them before creating the world mod.";
            texture = null;
        }
        catch (Exception e)
        {
            _status = "Could not create geographic layer: " + e.Message;
            Debug.LogWarning("[World Creator] Could not create geographic layer: " + e);
        }
        finally
        {
            if (texture != null)
                Destroy(texture);
        }
    }

    /// <summary>Saves the selected edited mask as a PNG for reuse outside the world project.</summary>
    private void ExportMapMaskPng()
    {
        var layer = _project!.MapLayers.FirstOrDefault(item => item.Id == _paintLayerId && _selectedMapLayers.Contains(item.Id));
        if (layer == null)
        {
            _status = "Select a map layer before saving its PNG.";
            return;
        }

        try
        {
            LoadMapLayerPreview(layer);
            if (!_mapLayerPreviews.TryGetValue(layer.Id, out var texture) || texture == null)
                throw new FileNotFoundException("The selected map layer image could not be loaded.");
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Stationeers", "WorldCreator", "MapExports");
            Directory.CreateDirectory(folder);
            var stem = Regex.Replace((_id ?? "World") + "_" + layer.Id, "[^A-Za-z0-9_-]", "_");
            var path = Path.Combine(folder, stem + ".png");
            for (var suffix = 2; File.Exists(path); suffix++)
                path = Path.Combine(folder, stem + "_" + suffix.ToString(CultureInfo.InvariantCulture) + ".png");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            _status = "Saved the selected map mask to " + path;
        }
        catch (Exception e)
        {
            _status = "Could not save map mask: " + e.Message;
            Debug.LogWarning("[World Creator] Could not export map mask PNG: " + e);
        }
    }

    /// <summary>Loads reusable map assets once and retains a readable error if loading fails.</summary>
    private void EnsureAssetLibraryLoaded()
    {
        if (!_assetLibraryLoaded)
        {
            try
            {
                _assetLibraryItems.Clear();
                _assetLibraryItems.AddRange(AssetLibrary.Load());
                _assetLibraryError = null;
            }
            catch (Exception e)
            {
                _assetLibraryError = e.Message;
            }

            _assetLibraryLoaded = true;
        }
    }

    /// <summary>Lists reusable PNG assets suitable for exact-colour region masks.</summary>
    private LibraryAsset[] GetMapMaskAssets()
    {
        EnsureAssetLibraryLoaded();
        return _assetLibraryItems.Where(asset => string.Equals(Path.GetExtension(asset.FileName), ".png", StringComparison.OrdinalIgnoreCase)).OrderBy(asset => asset.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Lists reusable image assets suitable for an editor-only geographic reference.</summary>
    private LibraryAsset[] GetMapImageAssets()
    {
        EnsureAssetLibraryLoaded();
        return _assetLibraryItems.Where(asset => string.Equals(Path.GetExtension(asset.FileName), ".png", StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetExtension(asset.FileName), ".jpg", StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetExtension(asset.FileName), ".jpeg", StringComparison.OrdinalIgnoreCase))
            .OrderBy(asset => asset.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Loads a library image as a visual guide without treating it as exported region data.</summary>
    private void SetMapReferenceImage(LibraryAsset asset)
    {
        var preview = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!preview.LoadImage(File.ReadAllBytes(asset.FullPath)))
                throw new InvalidDataException("The library image could not be decoded.");
            preview.name = "World Creator map reference " + asset.Name;
            if (_mapReferencePreview != null)
                Destroy(_mapReferencePreview);
            _mapReferencePreview = preview;
            _status = "Using " + asset.Name + " as an editor-only map guide.";
        }
        catch (Exception e)
        {
            Destroy(preview);
            _status = "Could not load map reference image: " + e.Message;
        }
    }

    /// <summary>Validates and applies a library PNG to the selected layer while retaining undo data.</summary>
    private void ImportMapMask(LibraryAsset asset)
    {
        var layer = _project!.MapLayers.FirstOrDefault(item => item.Id == _paintLayerId);
        if (layer == null)
        {
            _status = "Select a map layer before applying a mask.";
            return;
        }

        var replacement = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!replacement.LoadImage(File.ReadAllBytes(asset.FullPath)))
                throw new InvalidDataException("The library image could not be decoded as a PNG.");
            replacement.filterMode = FilterMode.Point;
            var pixels = replacement.GetPixels32();
            var regionColors = new HashSet<uint>(layer.Regions.Select(region => ColorKey(region.R, region.G, region.B)));
            var invalidPixels = pixels.Count(pixel => pixel.a != 0 && !regionColors.Contains(ColorKey(pixel.r, pixel.g, pixel.b)));
            if (invalidPixels > 0)
                throw new InvalidDataException(invalidPixels + " visible pixels do not match this layer's exact region colors. Use a non-antialiased mask with the defined palette.");
            replacement.name = "World Creator layer " + layer.Id + " (library mask)";
            replacement.filterMode = FilterMode.Point;
            CaptureMapUndo(layer.Id);
            if (_mapLayerPreviews.TryGetValue(layer.Id, out var previous) && previous != null)
                Destroy(previous);
            _mapLayerPreviews[layer.Id] = replacement;
            _mapLayerPixels[layer.Id] = pixels;
            _selectedMapLayers.Add(layer.Id);
            _editedMapLayers.Add(layer.Id);
            CachePoiPositions(layer, replacement);
            _status = "Applied " + asset.Name + " to " + layer.Id + ". The original library PNG is unchanged.";
        }
        catch (Exception e)
        {
            Destroy(replacement);
            _status = "Could not use map mask: " + e.Message;
        }
    }

    /// <summary>Draws one selected layer and its region cards, with an optional named-region action.</summary>
    private void DrawMapLayerRegions(WorldProject.MapLayer layer)
    {
        GUILayout.Space(CardSpacing);
        GUILayout.BeginVertical(_rowStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label(HumanizeXmlName(layer.Id), _sectionStyle!);
        GUILayout.Label((_project!.IsGeographicLayer(layer.Id) ? "Geographic regions" : "Region mask") + " · " + layer.Regions.Count + " regions", _mutedStyle!);
        if (_project.IsGeographicLayer(layer.Id))
        {
            var field = _project.MapFields.FirstOrDefault(item => item.Label.Contains("/DefaultRegionName") && item.Label.EndsWith("/@Value"));
            if (field != null)
            {
                GUILayout.Label("Name outside named regions", _mutedStyle!);
                field.Value = GUILayout.TextField(field.Value, _inputStyle!, GUILayout.Width(220), GUILayout.Height(32));
            }
        }
        _mapRegionScrolls.TryGetValue(layer.Id, out var regionScroll);
        _mapRegionScrolls[layer.Id] = GUILayout.BeginScrollView(regionScroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar,
            GUILayout.Height(Math.Min(420, Math.Max(120, ((layer.Regions.Count + 3) / 4) * 110))));
        DrawCardColumns(layer.Regions, region =>
        {
            DrawMapRegionCard(layer, region);
            return false;
        }, inset: _cardStyle!.padding.horizontal + _rowStyle!.padding.horizontal + 35, minimumWidth: 420, maximumWidth: 620, maximumColumns: 4);

        GUILayout.EndScrollView();
        if (_project!.IsGeographicLayer(layer.Id) && NativeButton("+ Add named region", _buttonStyle!, GUILayout.Height(26)))
        {
            try
            {
                var regionId = _project!.AddGeographicRegion(layer.Id);
                _paintLayerId = layer.Id;
                _paintRegionId = regionId;
                _status = "Added " + regionId + ". Rename it, then paint it onto the map.";
            }
            catch (Exception e)
            {
                _status = "Could not add region: " + e.Message;
            }
        }

        GUILayout.EndVertical();
    }

    /// <summary>Draws one region colour and name, and selects that region for painting.</summary>
    private void DrawMapRegionCard(WorldProject.MapLayer layer, WorldProject.MapLayer.Region region)
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.BeginHorizontal();
        DrawMapColourCube(new Color(region.R / 255f, region.G / 255f, region.B / 255f));
        var regionName = _project!.GetMapRegionName(region);
        if (region.NameKey != null || _project.PointsOfInterest.Any(poi => poi.RegionId == region.Id))
            _project!.SetMapRegionName(region, GUILayout.TextField(regionName, _inputStyle!, GUILayout.MinWidth(70), GUILayout.MaxWidth(160), GUILayout.Height(32)));
        else
            GUILayout.Label(HumanizeXmlName(region.Id), _mutedStyle!, GUILayout.MinWidth(70), GUILayout.MaxWidth(160), GUILayout.Height(32));
        GUILayout.Space(10);
        var selectedRegion = _paintLayerId == layer.Id && _paintRegionId == region.Id;
        if (!_project!.IsPlayableAreaLayer(layer.Id) && NativeButton(selectedRegion ? "Selected colour" : "Use this region colour", _buttonStyle!, GUILayout.Width(200), GUILayout.Height(32)))
        {
            _editPlayableArea = false;
            _pipetteMapMode = false;
            _paintMode = true;
            _paintLayerId = layer.Id;
            _paintRegionId = region.Id;
            if (!_createdMapLayers.Contains(layer.Id))
                _eraseMapMode = false;
            if (_selectedMapLayers.Add(layer.Id))
                LoadMapLayerPreview(layer);
        }

        GUILayout.EndHorizontal();
        DrawMapRgbField(layer, region);
        foreach (var poi in _project!.PointsOfInterest.Where(item => item.RegionId == region.Id))
        {
            GUILayout.Label("Description", _mutedStyle!);
            _project.SetPoiText(poi, "Description", GUILayout.TextArea(_project.GetPoiText(poi, "Description"), _multilineStyle!, GUILayout.Height(64)));
        }
        GUILayout.EndVertical();
    }

    /// <summary>Selects or clears the editor-only reference image from the asset library.</summary>
    private void DrawMapReferencePicker()
    {
        var referenceAssets = GetMapImageAssets();
        GUILayout.Label("MAP REFERENCE IMAGE", _sectionStyle!);
        if (referenceAssets.Length == 0)
            GUILayout.Label("Import a PNG, JPG or JPEG into the Asset library to use it as a visual guide.", _mutedStyle!);
        else
        {
            _mapReferenceSelection = Mathf.Clamp(_mapReferenceSelection, 0, referenceAssets.Length - 1);
            GUILayout.BeginHorizontal();
            if (NativeButton("‹", _buttonStyle!, GUILayout.Width(32)))
                _mapReferenceSelection = (_mapReferenceSelection + referenceAssets.Length - 1) % referenceAssets.Length;
            GUILayout.Label(referenceAssets[_mapReferenceSelection].Name, _mutedStyle!, GUILayout.ExpandWidth(true));
            if (NativeButton("›", _buttonStyle!, GUILayout.Width(32)))
                _mapReferenceSelection = (_mapReferenceSelection + 1) % referenceAssets.Length;
            GUILayout.EndHorizontal();
            if (NativeButton("Use as visual guide", _buttonStyle!, GUILayout.Height(28)))
                SetMapReferenceImage(referenceAssets[_mapReferenceSelection]);
            if (_mapReferencePreview != null && NativeButton("Clear visual guide", _buttonStyle!, GUILayout.Height(26)))
            {
                Destroy(_mapReferencePreview);
                _mapReferencePreview = null;
                _status = "Cleared the editor-only map reference image.";
            }

            GUILayout.Label("This image is only an editor guide; it is not included in the world mod or used as a game region mask.", _mutedStyle!);
        }
    }

    /// <summary>Selects a PNG asset to use as the current region mask when a layer is available.</summary>
    private void DrawMapStartingMaskPicker()
    {
        var pngAssets = GetMapMaskAssets();
        GUILayout.Label("STARTING MASK", _sectionStyle!);
        if (!string.IsNullOrEmpty(_assetLibraryError))
            GUILayout.Label(_assetLibraryError, _mutedStyle!);
        if (pngAssets.Length == 0)
            GUILayout.Label("Import a PNG into the Asset library to use it as a map mask.", _mutedStyle!);
        else
        {
            _mapMaskAssetSelection = Mathf.Clamp(_mapMaskAssetSelection, 0, pngAssets.Length - 1);
            GUILayout.BeginHorizontal();
            if (NativeButton("‹", _buttonStyle!, GUILayout.Width(32)))
                _mapMaskAssetSelection = (_mapMaskAssetSelection + pngAssets.Length - 1) % pngAssets.Length;
            GUILayout.Label(pngAssets[_mapMaskAssetSelection].Name, _mutedStyle!, GUILayout.ExpandWidth(true));
            if (NativeButton("›", _buttonStyle!, GUILayout.Width(32)))
                _mapMaskAssetSelection = (_mapMaskAssetSelection + 1) % pngAssets.Length;
            GUILayout.EndHorizontal();
            var guiWasEnabled = GUI.enabled;
            GUI.enabled = guiWasEnabled && _paintLayerId != null && !_project!.IsPlayableAreaLayer(_paintLayerId) && _selectedMapLayers.Contains(_paintLayerId) && _project!.MapLayers.Any(layer => layer.Id == _paintLayerId);
            if (NativeButton("Use PNG as selected layer mask", _buttonStyle!, GUILayout.Height(28)))
                ImportMapMask(pngAssets[_mapMaskAssetSelection]);
            GUI.enabled = guiWasEnabled;
            GUILayout.Label("Select a layer and region above. Visible pixels must use its exact region colors; transparent pixels are allowed.", _mutedStyle!);
        }
    }

    /// <summary>Selects the exact region palette, pipette and source-pixel brush controls below the map.</summary>
    private void DrawMapBrushControls()
    {
        GUILayout.Label("MASK PAINTING", _sectionStyle!);
        var layers = _project!.MapLayers.Where(layer => !_project.IsPlayableAreaLayer(layer.Id)).ToArray();
        if (layers.Length == 0)
        {
            GUILayout.Label("Create a geographic layer to define colours and paint a new mask.", _mutedStyle!);
            return;
        }
        GUILayout.BeginHorizontal();
        var selected = Math.Max(0, Array.FindIndex(layers, layer => layer.Id == _paintLayerId));
        var next = NativeDropdown("paint-mask-layer", selected, layers.Select(layer => layer.Id).ToArray(), _buttonStyle!, GUILayout.Width(340), GUILayout.Height(32));
        var layer = layers[next];
        if (next != selected && _selectedMapLayers.Add(layer.Id)) LoadMapLayerPreview(layer);
        if (_paintLayerId != layer.Id)
        {
            _paintLayerId = layer.Id;
            _paintRegionId = null;
            _eraseMapMode = false;
        }
        if (NativeButton(_pipetteMapMode ? "Pipette active" : "Pick region (pipette)", _buttonStyle!, GUILayout.Width(190), GUILayout.Height(32)))
        {
            _pipetteMapMode = true;
            _editPlayableArea = false;
            _paintMode = false;
            if (_selectedMapLayers.Add(layer.Id)) LoadMapLayerPreview(layer);
        }
        GUILayout.EndHorizontal();
        var paint = GUILayout.Toggle(_paintMode, "Paint selected region on map");
        if (paint && !_paintMode) { _editPlayableArea = false; _pipetteMapMode = false; }
        _paintMode = paint && !_editPlayableArea && !_pipetteMapMode;
        _pixelMapBrush = GUILayout.Toggle(_pixelMapBrush, "1 pixel brush (exact mask pixel)");
        if (!_pixelMapBrush)
        {
            GUILayout.Label("Brush radius: " + Mathf.RoundToInt(_brushRadius) + " mask pixels", _mutedStyle!);
            _brushRadius = GUILayout.HorizontalSlider(_brushRadius, 1, 24, GUILayout.Width(340));
        }
        if (_createdMapLayers.Contains(layer.Id) && layer.Regions.Count > 0)
            _eraseMapMode = GUILayout.Toggle(_eraseMapMode, "Erase to base region (" + _project.GetMapRegionName(layer.Regions[0]) + ")");
        GUILayout.Label(_pipetteMapMode ? "Click a pixel on the map to pick its exact colour and linked region from " + layer.Id + "."
            : "Choose a region colour below, or use the pipette. Paint over a region to replace it, or extend it with the same colour. Ctrl+Z undoes recent strokes.", _mutedStyle!);
    }
    /// <summary>Arranges layer selection, references, mask actions, painting controls and points of interest.</summary>
    private void DrawMapLayerSelector()
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.Width(280));
        GUILayout.Label("MAP LAYERS", _sectionStyle!);
        if (_project!.CanAddGeographicLayer && NativeButton("+ Create geographic layer", _buttonStyle!, GUILayout.Height(28)))
            CreateGeographicLayer();
        _mapLayerScroll = GUILayout.BeginScrollView(_mapLayerScroll, GUILayout.Height(Math.Min(360, Math.Max(40, _project.MapLayers.Count * 32))));
        foreach (var layer in _project!.MapLayers)
        {
            DrawMapLayerVisibilityToggle(layer);
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    /// <summary>Keeps shape, palette and painting tools below the canvas while only layer visibility stays beside it.</summary>
    private void DrawMapToolsCard()
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        DrawMapZoomControls();
        GUILayout.Space(CardSpacing);
        DrawPlayableAreaControls();
        GUILayout.Space(CardSpacing);
        DrawMapBrushControls();
        DrawMapReferencePicker();
        foreach (var layer in _project!.MapLayers.Where(layer => layer.Id == _paintLayerId))
        {
            DrawMapLayerRegions(layer);
        }

        DrawMapStartingMaskPicker();
        var restoreWasEnabled = GUI.enabled;
        GUI.enabled = restoreWasEnabled && _paintLayerId != null && !_project!.IsPlayableAreaLayer(_paintLayerId) && _selectedMapLayers.Contains(_paintLayerId) && _mapSourcePng.ContainsKey(_paintLayerId);
        if (NativeButton("Restore source mask", _buttonStyle!, GUILayout.Height(28)))
            RestoreMapMask();
        GUI.enabled = restoreWasEnabled;
        GUILayout.Label("Restores the original game mask, or the blank base of a newly created geographic layer.", _mutedStyle!);
        var exportWasEnabled = GUI.enabled;
        GUI.enabled = exportWasEnabled && _paintLayerId != null && _selectedMapLayers.Contains(_paintLayerId);
        if (NativeButton("Save selected mask as PNG", _buttonStyle!, GUILayout.Height(28)))
            ExportMapMaskPng();
        GUI.enabled = exportWasEnabled;
        GUILayout.Label("PNG copies are saved under Documents/My Games/Stationeers/WorldCreator/MapExports.", _mutedStyle!);
        if (_mapUndo.Count > 0 && NativeButton("Undo last map change (Ctrl+Z)", _buttonStyle!, GUILayout.Width(270), GUILayout.Height(32)))
            UndoMapStroke();
        GUILayout.EndVertical();
    }

    /// <summary>Updates one layer visibility and clears paint selection when that layer is hidden.</summary>
    private void DrawMapLayerVisibilityToggle(WorldProject.MapLayer layer)
    {
        var selected = _selectedMapLayers.Contains(layer.Id);
        var next = GUILayout.Toggle(selected, layer.Id);
        if (next == selected)
            return;
        if (next)
        {
            _selectedMapLayers.Add(layer.Id);
            LoadMapLayerPreview(layer);
            if (!_project!.IsPlayableAreaLayer(layer.Id))
            {
                _paintLayerId = layer.Id;
                _paintRegionId = null;
            }
            if (!_createdMapLayers.Contains(layer.Id))
                _eraseMapMode = false;
            return;
        }

        _selectedMapLayers.Remove(layer.Id);
        if (_paintLayerId != layer.Id)
            return;
        _paintLayerId = null;
        _paintRegionId = null;
        _eraseMapMode = false;
    }
}
