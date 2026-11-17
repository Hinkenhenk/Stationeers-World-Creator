using System.Collections.Generic;
using System;
using System.IO;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private int _assetCategorySelection;
    private int _assetCategoryFilter;
    private int _assetTypeFilter = 2;
    private string _assetPath = "";
    private string _assetSearch = "";
    private string? _pendingAssetRemoveId;
    private bool _assetLibraryLoaded;
    private string? _assetLibraryError;
    private readonly List<LibraryAsset> _assetLibraryItems = new List<LibraryAsset>();
    /// <summary>Arranges import, browse and project-asset cards on the asset-library page.</summary>
    private void DrawAssetLibrary()
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

        DrawCardColumns(new Func<bool>[] { DrawAssetImportCard, DrawAssetBrowseCard, DrawProjectAssetsCard }, draw => draw(), minimumWidth: 850);
    }

    /// <summary>Collects a source file and category before importing an asset into the reusable library.</summary>
    private bool DrawAssetImportCard()
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("Reusable asset library", _sectionStyle!);
        GUILayout.Label("Library folder: " + AssetLibrary.DirectoryPath, _mutedStyle!);
        GUILayout.Label("Original files are copied into the library and left unchanged.", _mutedStyle!);
        GUILayout.Space(5);
        _assetPath = GUILayout.TextField(_assetPath, _inputStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("Enter a full file path. Supported: PNG, JPG, JPEG, DDS, TIF, TIFF, RAW, DATA, DAT.", _mutedStyle!);
        _assetCategorySelection = Mathf.Clamp(_assetCategorySelection, 0, AssetCategories.Length - 1);
        GUILayout.Space(12);
        _assetCategorySelection = DrawAssetFilterButtons(_assetCategorySelection, AssetCategories);
        GUILayout.Space(12);
        if (NativeButton("+ Import file to library", _primaryButtonStyle!, GUILayout.Width(210)))
        {
            try
            {
                var item = AssetLibrary.Import(_assetPath, AssetCategories[_assetCategorySelection]);
                _assetPath = "";
                _assetLibraryLoaded = false;
                _status = "Copied " + item.Name + " into the reusable asset library.";
            }
            catch (Exception e)
            {
                _status = e.Message;
            }
        }

        GUILayout.EndVertical();
        return false;
    }

    /// <summary>Filters library entries and offers actions for adding or removing the selected asset.</summary>
    private bool DrawAssetBrowseCard()
    {
        if (!string.IsNullOrEmpty(_assetLibraryError))
        {
            GUILayout.Label(_assetLibraryError, _mutedStyle!);
            return true;
        }

        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("Browse library", _sectionStyle!);
        GUILayout.Space(10);
        _assetTypeFilter = DrawAssetFilterButtons(_assetTypeFilter, AssetTypeFilters);
        var categoryOptions = new[]
        {
            "All categories"
        }.Concat(AssetCategories).ToArray();
        _assetCategoryFilter = Mathf.Clamp(_assetCategoryFilter, 0, categoryOptions.Length - 1);
        GUILayout.Space(10);
        _assetCategoryFilter = DrawAssetFilterButtons(_assetCategoryFilter, categoryOptions);
        GUILayout.Space(16);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Search", GUILayout.Width(58));
        _assetSearch = GUILayout.TextField(_assetSearch, _inputStyle!, GUILayout.Width(350), GUILayout.Height(32));
        GUILayout.Space(10);
        GUILayout.Label(_assetLibraryItems.Count + " in library", _mutedStyle!);
        GUILayout.EndHorizontal();
        GUILayout.Space(12);
        var assets = _assetLibraryItems.Where(asset => (_assetTypeFilter == 2 || (_assetTypeFilter == 0) == asset.Kind.StartsWith("Texture", StringComparison.Ordinal)) && (_assetCategoryFilter == 0 || asset.Category == categoryOptions[_assetCategoryFilter]) && (string.IsNullOrWhiteSpace(_assetSearch) || asset.Name.IndexOf(_assetSearch, StringComparison.OrdinalIgnoreCase) >= 0 || asset.Category.IndexOf(_assetSearch, StringComparison.OrdinalIgnoreCase) >= 0))
            .ToArray();
        var pendingAsset = _assetLibraryItems.FirstOrDefault(asset => asset.Id == _pendingAssetRemoveId);
        if (pendingAsset != null)
        {
            GUILayout.BeginHorizontal(_rowStyle!);
            GUILayout.Label(
                "Remove the library copy of " + pendingAsset.Name + "? The original and any project copy will remain unchanged.",
                _mutedStyle!,
                GUILayout.ExpandWidth(true)
            );
            if (NativeButton("Keep", _buttonStyle!, GUILayout.Width(90)))
                _pendingAssetRemoveId = null;
            if (NativeButton("Remove copy", _buttonStyle!, GUILayout.Width(120)))
            {
                try
                {
                    AssetLibrary.Remove(pendingAsset.Id);
                    _pendingAssetRemoveId = null;
                    _assetLibraryLoaded = false;
                    _status = "Removed the library copy of " + pendingAsset.Name + ". Existing project copies are unchanged.";
                }
                catch (Exception e)
                {
                    _status = e.Message;
                }
            }

            GUILayout.EndHorizontal();
        }

        if (assets.Length == 0)
            GUILayout.Label("No files match this view yet. Import an original file above.", _mutedStyle!);
        foreach (var asset in assets)
        {
            GUILayout.BeginHorizontal(_rowStyle!);
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label(asset.Name, _mutedStyle!);
            GUILayout.Label(
                asset.Category + "  ·  " + asset.Kind + "  ·  " + new FileInfo(asset.FullPath).Length.ToString("N0", CultureInfo.InvariantCulture) + " bytes",
                _mutedStyle!
            );
            GUILayout.EndVertical();
            var inProject = _project!.ProjectAssets.Any(item => item.Id == asset.Id);
            if (NativeButton(inProject ? "Added" : "Add to project", _buttonStyle!, GUILayout.Width(190), GUILayout.Height(32)) && !inProject)
            {
                try
                {
                    _project.AddLibraryAsset(asset);
                    _status = "Copied " + asset.Name + " into this world project.";
                }
                catch (Exception e)
                {
                    _status = e.Message;
                }
            }

            GUILayout.Space(10);
            if (NativeButton("Remove from library", _buttonStyle!, GUILayout.Width(190), GUILayout.Height(32)))
                _pendingAssetRemoveId = asset.Id;
            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
        return false;
    }

    /// <summary>Uses equal-sized filter buttons with room between rows and columns.</summary>
    private int DrawAssetFilterButtons(int selected, string[] labels)
    {
        for (var start = 0; start < labels.Length; start += 3)
        {
            if (start > 0) GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            for (var i = start; i < Math.Min(start + 3, labels.Length); i++)
            {
                if (i > start) GUILayout.Space(10);
                if (NativeButton(labels[i], i == selected ? _primaryButtonStyle! : _buttonStyle!, GUILayout.Width(190), GUILayout.Height(32)))
                    selected = i;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        return selected;
    }

    /// <summary>Lists the assets attached to this world and lets the player remove those references.</summary>
    private bool DrawProjectAssetsCard()
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("Assets in this world", _sectionStyle!);
        if (_project!.ProjectAssets.Count == 0)
            GUILayout.Label("Add a library file to include it in this world mod.", _mutedStyle!);
        foreach (var asset in _project.ProjectAssets.ToArray())
        {
            GUILayout.BeginHorizontal(_rowStyle!);
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label(asset.Name + "  ·  " + asset.Category, _mutedStyle!);
            GUILayout.Label(asset.RelativePath(_id), _mutedStyle!);
            GUILayout.EndVertical();
            if (NativeButton("Remove from project", _buttonStyle!, GUILayout.Width(180)))
                try
                {
                    _project.RemoveProjectAsset(asset.Id);
                    _status = "Removed " + asset.Name + " from this world project.";
                }
                catch (Exception e)
                {
                    _status = e.Message;
                }

            GUILayout.EndHorizontal();
        }

        GUILayout.Label(
            "Project copies export under GameData/Worlds/<world ID>/Assets. Use the displayed path in the relevant XML asset field; categorizing a file does not assign it to a game setting.",
            _mutedStyle!
        );
        GUILayout.EndVertical();
        return false;
    }
}
