using System;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    private byte[]? _runtimeLavaMap;
    private int _runtimeLavaMin;
    private int _runtimeLavaMax;

    internal int LavaMinimumHeight => _runtimeLavaMap != null ? _runtimeLavaMin : (int?)_world.Element("TerrainSettings")?.Element("Lava")?.Attribute("MinHeight") ?? 2;
    internal int LavaMaximumHeight => _runtimeLavaMap != null ? _runtimeLavaMax : (int?)_world.Element("TerrainSettings")?.Element("Lava")?.Attribute("MaxHeight") ?? 128;

    /// <summary>Captures a lava PNG and heights that are safe for the game's 256-square lava sampler.</summary>
    internal void SetRuntimeLavaMap(string path, int minHeight, int maxHeight)
    {
        if (minHeight < 0 || maxHeight > 1023 || minHeight >= maxHeight)
            throw new InvalidOperationException("Lava heights must be from 0 to 1023, with minimum below maximum.");
        if (_world.Element("TerrainSettings") == null)
            throw new InvalidOperationException("This world has no TerrainSettings entry.");
        var size = ReadTerrainPngSize(path);
        // The native sampler steps by width / 256; smaller or non-square images can hang or overrun its array.
        if (size.Width < 256 || size.Width != size.Height || (size.Width & (size.Width - 1)) != 0)
            throw new InvalidOperationException("Lava PNG must be square, power-of-two sized and at least 256 × 256.");
        var bytes = File.ReadAllBytes(path);
        _runtimeLavaMap = bytes;
        _runtimeLavaMin = minHeight;
        _runtimeLavaMax = maxHeight;
        _structureChanged = true;
    }

    /// <summary>Checks PNG identity and positive image dimensions before allocating preview or lava textures.</summary>
    internal static (int Width, int Height) ReadTerrainPngSize(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Choose a PNG image.");
        using var reader = new BinaryReader(File.OpenRead(path));
        var header = reader.ReadBytes(24);
        var signature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82 };
        if (header.Length != 24)
            throw new InvalidOperationException("PNG header is incomplete.");
        for (var i = 0; i < signature.Length; i++)
            if (header[i] != signature[i])
                throw new InvalidOperationException("The selected file is not a valid PNG image.");
        var width = ReadPngDimension(header, 16);
        var height = ReadPngDimension(header, 20);
        return (width, height);
    }

    /// <summary>Reads one positive PNG dimension while rejecting unsupported integer sizes.</summary>
    private static int ReadPngDimension(byte[] header, int offset)
    {
        var value = ((uint)header[offset] << 24) | ((uint)header[offset + 1] << 16) | ((uint)header[offset + 2] << 8) | header[offset + 3];
        if (value == 0 || value > int.MaxValue)
            throw new InvalidOperationException("PNG dimensions are outside the supported range.");
        return (int)value;
    }

    /// <summary>Writes the chosen lava image and its exact runtime XML reference into the exported world.</summary>
    private void WriteRuntimeLavaMap(XElement world, string temporary, string id)
    {
        if (_runtimeLavaMap == null)
            return;
        var relative = "Worlds/" + id + "/Textures/LavaHeight.png";
        var destination = Path.Combine(temporary, "GameData", relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllBytes(destination, _runtimeLavaMap);
        var terrain = world.Element("TerrainSettings")!;
        var lava = terrain.Element("Lava");
        if (lava == null)
        {
            lava = new XElement("Lava");
            terrain.Add(lava);
        }
        lava.SetAttributeValue("MinHeight", _runtimeLavaMin.ToString(CultureInfo.InvariantCulture));
        lava.SetAttributeValue("MaxHeight", _runtimeLavaMax.ToString(CultureInfo.InvariantCulture));
        lava.Element("HeightTexture")?.Remove();
        lava.Add(new XElement("HeightTexture", new XAttribute("Path", relative), new XAttribute("Format", "RGB24"), new XAttribute("Linear", "true")));
        world.SetElementValue("HasLava", true);
    }
}
