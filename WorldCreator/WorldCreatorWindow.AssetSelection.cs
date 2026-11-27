using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    /// <summary>Selects a compatible library copy while keeping all filesystem import controls on Asset Libraries.</summary>
    private string DrawLibraryAssetDropdown(string key, string currentPath, bool terrainData, string emptyLabel, string? retainedPath = null)
    {
        EnsureAssetLibraryLoaded();
        var items = _assetLibraryItems.Where(asset => terrainData
            ? new[] { ".raw", ".data" }.Contains(Path.GetExtension(asset.FullPath).ToLowerInvariant())
            : Path.GetExtension(asset.FullPath).Equals(".png", StringComparison.OrdinalIgnoreCase)).ToArray();
        var paths = new[] { "" }.Concat(items.Select(asset => asset.FullPath)).ToList();
        var labels = new[] { emptyLabel }.Concat(items.Select(asset => asset.Name + " · " + asset.Category)).ToList();
        if (!string.IsNullOrEmpty(retainedPath) && !paths.Contains(retainedPath!))
        {
            paths.Add(retainedPath!);
            labels.Add("Current project texture");
        }
        var selected = Math.Max(0, paths.IndexOf(currentPath));
        var next = NativeDropdown(key, selected, labels.ToArray(), _buttonStyle!, GUILayout.Height(32), GUILayout.ExpandWidth(true));
        if (_assetLibraryError != null) GUILayout.Label(_assetLibraryError, _mutedStyle!);
        if (items.Length == 0) GUILayout.Label("Import compatible files on Asset Libraries first.", _mutedStyle!);
        return next >= 0 && next < paths.Count ? paths[next] : currentPath;
    }
}
