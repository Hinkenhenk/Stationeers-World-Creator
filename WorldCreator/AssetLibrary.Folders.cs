using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace WorldCreator;

internal static partial class AssetLibrary
{
    internal static string ImportFolderPath => File.Exists(FolderSettingPath) ? File.ReadAllText(FolderSettingPath) : "";
    private static string FolderSettingPath => Path.Combine(DirectoryPath, "ImportFolder.txt");

    /// <summary>Remembers the user's drop folder without changing the original files or starting a filesystem watcher.</summary>
    internal static void SetImportFolder(string folder)
    {
        var path = Path.GetFullPath(folder.Trim());
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException("Choose an existing import folder.");
        var library = Path.GetFullPath(DirectoryPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if ((path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar).StartsWith(library, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Choose a source folder outside the managed library.");
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(FolderSettingPath, path);
    }

    /// <summary>Imports supported files in a folder, skips unchanged copies and reports individual failures without deleting sources.</summary>
    internal static (int Added, int Skipped, string[] Errors) ImportFolder(string category)
    {
        var folder = ImportFolderPath;
        SetImportFolder(folder);
        var existing = Load().ToList();
        var extensions = new[] { ".png", ".jpg", ".jpeg", ".dds", ".tif", ".tiff", ".raw", ".data", ".dat" };
        var errors = new List<string>();
        var added = 0;
        var skipped = 0;
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        var managed = Path.GetFullPath(DirectoryPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var file in Directory.EnumerateFiles(folder, "*", options))
        {
            if (Path.GetFullPath(file).StartsWith(managed, StringComparison.OrdinalIgnoreCase) || !extensions.Contains(Path.GetExtension(file).ToLowerInvariant())) continue;
            try
            {
                if (existing.Any(asset => asset.Name == Path.GetFileName(file) && SameFileContents(asset.FullPath, file))) { skipped++; continue; }
                existing.Add(Import(file, category));
                added++;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is InvalidOperationException)
            { errors.Add(Path.GetFileName(file) + ": " + error.Message); }
        }
        return (added, skipped, errors.ToArray());
    }

    /// <summary>Uses file length and content hashes so repeated refreshes do not create duplicate library copies.</summary>
    private static bool SameFileContents(string left, string right)
    {
        if (!File.Exists(left) || new FileInfo(left).Length != new FileInfo(right).Length) return false;
        using var hash = SHA256.Create();
        using var first = File.OpenRead(left);
        using var second = File.OpenRead(right);
        return hash.ComputeHash(first).SequenceEqual(hash.ComputeHash(second));
    }
}
