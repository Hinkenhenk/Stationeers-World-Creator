using System;
using System.IO;
using System.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    /// <summary>Copies a reusable library asset into this project's packaged asset list.</summary>
    internal void AddLibraryAsset(LibraryAsset asset)
    {
        if (ProjectAssets.Any(existing => existing.Id == asset.Id))
            throw new InvalidOperationException("This asset is already in the project.");
        if (!File.Exists(asset.FullPath))
            throw new FileNotFoundException("The library copy is missing.", asset.FullPath);
        var data = File.ReadAllBytes(asset.FullPath);
        ProjectAssets.Add(new ProjectAsset(asset.Id, asset.Name, asset.Category, asset.Kind, Path.GetExtension(asset.FileName).ToLowerInvariant(), data));
        _structureChanged = true;
    }

    /// <summary>Removes the selected packaged asset without changing its reusable library copy.</summary>
    internal void RemoveProjectAsset(string id)
    {
        var asset = ProjectAssets.FirstOrDefault(existing => existing.Id == id) ?? throw new InvalidOperationException("This asset is not in the project.");
        ProjectAssets.Remove(asset);
        _structureChanged = true;
    }
}
