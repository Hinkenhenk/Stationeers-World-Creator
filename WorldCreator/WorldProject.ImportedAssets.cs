using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    /// <summary>Gives referenced source-mod assets private paths, including mods that keep textures outside GameData.</summary>
    private List<(string Source, string RelativePath)> PrepareImportedAssetPaths(XElement data, string worldId)
    {
        var copies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(_sourceAssetRoot)) return new List<(string, string)>();
        var root = Path.GetFullPath(_sourceAssetRoot!).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var attribute in data.DescendantsAndSelf().Attributes("Path"))
        {
            var path = attribute.Value.Replace('\\', '/');
            if (Path.IsPathRooted(path) && !(path.StartsWith("/", StringComparison.Ordinal) && !path.StartsWith("//", StringComparison.Ordinal))) continue;
            var resolved = ResolveImportedAsset(path)!;
            if (!File.Exists(resolved) && !Directory.Exists(resolved)) continue;
            var source = Path.GetFullPath(resolved);
            if (!source.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
            var sourceRelative = source.Substring(root.Length).Replace('\\', '/');
            var previousImport = sourceRelative.IndexOf("/Imported/", StringComparison.OrdinalIgnoreCase);
            if (sourceRelative.StartsWith("GameData/Worlds/", StringComparison.OrdinalIgnoreCase) && previousImport >= 0)
                sourceRelative = sourceRelative.Substring(previousImport + "/Imported/".Length);
            var relative = "Worlds/" + worldId + "/Imported/" + sourceRelative;
            copies[source] = relative;
            attribute.Value = relative;
        }
        return copies.Select(pair => (pair.Key, pair.Value)).ToList();
    }

    /// <summary>Copies only resolved assets inside the source mod, leaving installed game assets as native references.</summary>
    private void CopyImportedAssets(IEnumerable<(string Source, string RelativePath)> copies, string temporary)
    {
        foreach (var copy in copies)
        {
            var destination = Path.Combine(temporary, "GameData", copy.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(copy.Source))
                CopyImportedDirectory(copy.Source, destination);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (!File.Exists(destination))
                {
                    if (_celestialSurfaceData.TryGetValue(copy.Source, out var data)) File.WriteAllBytes(destination, data);
                    else File.Copy(copy.Source, destination);
                }
            }
        }
    }
}
