using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WorldCreator
{
    internal sealed class LibraryAsset
    {
        internal readonly string Id;
        internal readonly string Name;
        internal readonly string Category;
        internal readonly string Kind;
        internal readonly string FileName;
        internal readonly string FullPath;

        internal LibraryAsset(string id, string name, string category, string kind, string fileName, string fullPath)
        {
            Id = id;
            Name = name;
            Category = category;
            Kind = kind;
            FileName = fileName;
            FullPath = fullPath;
        }
    }

    internal static class AssetLibrary
    {
        internal static string DirectoryPath => Environment.GetEnvironmentVariable("WORLD_CREATOR_ASSET_LIBRARY") ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Stationeers", "WorldCreator", "AssetLibrary");
        private static string MetadataPath => Path.Combine(DirectoryPath, "Library.xml");
        private static readonly string[] Categories = { "Surface", "Underground", "Atmosphere / Sky", "Map / UI", "Other" };

        internal static IReadOnlyList<LibraryAsset> Load()
        {
            var result = new List<LibraryAsset>();
            if (!File.Exists(MetadataPath)) return result;
            try
            {
                foreach (var element in XDocument.Load(MetadataPath).Root?.Elements("Asset") ?? Enumerable.Empty<XElement>())
                {
                    var id = (string?)element.Attribute("Id");
                    var name = (string?)element.Attribute("Name");
                    var category = (string?)element.Attribute("Category");
                    var kind = (string?)element.Attribute("Kind");
                    var fileName = (string?)element.Attribute("File");
                    if (string.IsNullOrWhiteSpace(id) || !Regex.IsMatch(id, "^[a-fA-F0-9]{32}$") || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(fileName) ||
                        !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal)) continue;
                    var path = Path.Combine(DirectoryPath, "Assets", fileName);
                    if (File.Exists(path)) result.Add(new LibraryAsset(id, name, Categories.Contains(category) ? category! : "Other", kind ?? "Other", fileName, path));
                }
            }
            catch (Exception e) { throw new InvalidDataException("The asset library index could not be read; its existing entries were left unchanged.", e); }
            return result.OrderBy(asset => asset.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        internal static LibraryAsset Import(string source, string category)
        {
            var path = Path.GetFullPath(source.Trim());
            if (!File.Exists(path)) throw new FileNotFoundException("Asset file was not found.", path);
            if (!Categories.Contains(category)) throw new InvalidOperationException("Choose an asset category.");
            var existing = Load();
            var extension = Path.GetExtension(path).ToLowerInvariant();
            var kind = extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".dds" || extension == ".tif" || extension == ".tiff"
                ? "Texture / image"
                : extension == ".raw" || extension == ".data" || extension == ".dat" ? "Terrain / data" : null;
            if (kind == null) throw new InvalidOperationException("Supported asset files are PNG, JPG, JPEG, DDS, TIF, TIFF, RAW, DATA, and DAT.");
            if (new FileInfo(path).Length > 268435456) throw new InvalidOperationException("Asset files must be 256 MB or smaller.");

            var id = Guid.NewGuid().ToString("N");
            var name = Path.GetFileName(path);
            var safeName = Regex.Replace(name, @"[^A-Za-z0-9._-]", "_");
            var fileName = id + "_" + safeName;
            Directory.CreateDirectory(Path.Combine(DirectoryPath, "Assets"));
            File.Copy(path, Path.Combine(DirectoryPath, "Assets", fileName));
            var all = existing.Select(asset => new XElement("Asset", new XAttribute("Id", asset.Id), new XAttribute("Name", asset.Name),
                new XAttribute("Category", asset.Category), new XAttribute("Kind", asset.Kind), new XAttribute("File", asset.FileName))).ToList();
            all.Add(new XElement("Asset", new XAttribute("Id", id), new XAttribute("Name", name), new XAttribute("Category", category),
                new XAttribute("Kind", kind), new XAttribute("File", fileName)));
            Save(all);
            return new LibraryAsset(id, name, category, kind, fileName, Path.Combine(DirectoryPath, "Assets", fileName));
        }

        internal static void Remove(string id)
        {
            var current = Load();
            var asset = current.FirstOrDefault(item => item.Id == id) ?? throw new FileNotFoundException("Asset is not in the reusable library.");
            var all = current.Where(item => item.Id != id).Select(item => new XElement("Asset", new XAttribute("Id", item.Id), new XAttribute("Name", item.Name),
                new XAttribute("Category", item.Category), new XAttribute("Kind", item.Kind), new XAttribute("File", item.FileName))).ToList();
            Save(all);
            File.Delete(asset.FullPath);
        }

        private static void Save(IEnumerable<XElement> items)
        {
            Directory.CreateDirectory(DirectoryPath);
            var temporary = MetadataPath + ".tmp";
            new XDocument(new XElement("AssetLibrary", items)).Save(temporary);
            if (File.Exists(MetadataPath)) File.Replace(temporary, MetadataPath, null);
            else File.Move(temporary, MetadataPath);
        }
    }
}
