using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    private readonly Dictionary<string, ProjectAsset> _textureReplacements = new Dictionary<string, ProjectAsset>(StringComparer.Ordinal);

    /// <summary>Offers image generation only for a missing image reference in this draft, never a directory or terrain file.</summary>
    internal bool CanReplaceMissingTexture(Field? field)
    {
        if (field == null || !AdvancedFields.Contains(field) || !(field.Target is XAttribute attribute) || attribute.Name != "Path") return false;
        var extension = Path.GetExtension(field.Value).ToLowerInvariant();
        if (!new[] { ".png", ".jpg", ".jpeg", ".dds", ".tga", ".tif", ".tiff" }.Contains(extension)) return false;
        return !_textureReplacements.ContainsKey(field.Value) && !File.Exists(ResolveImportedAsset(field.Value)) && !File.Exists(Path.Combine(_assets, field.Value.Replace('\\', '/')));
    }

    /// <summary>Attaches an encoded PNG to the chosen missing reference without writing into the original mod.</summary>
    internal void ReplaceMissingTexture(Field field, byte[] png)
    {
        if (!CanReplaceMissingTexture(field)) throw new InvalidOperationException("Select a missing texture reference first.");
        var signature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        if (png.Length < 24 || !png.Take(signature.Length).SequenceEqual(signature)) throw new InvalidOperationException("The replacement must be a PNG image.");
        var asset = new ProjectAsset(Guid.NewGuid().ToString("N"), "Solid_" + Path.GetFileNameWithoutExtension(field.Value) + ".png", "Surface", "Texture / image", ".png", (byte[])png.Clone());
        field.Value = asset.RelativePath("Generated");
        ProjectAssets.Add(asset);
        _textureReplacements.Add(field.Value, asset);
        _structureChanged = true;
    }

    /// <summary>Resolves generated image references against the current export ID and an uncompressed PNG-compatible format.</summary>
    private void PrepareTextureReplacements(XElement data, string worldId)
    {
        foreach (var path in data.DescendantsAndSelf().Attributes("Path"))
        {
            if (!_textureReplacements.TryGetValue(path.Value, out var asset) || !ProjectAssets.Contains(asset)) continue;
            path.Value = asset.RelativePath(worldId);
            path.Parent!.SetAttributeValue("Format", "RGBA32");
        }
    }
}
