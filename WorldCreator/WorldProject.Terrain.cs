using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    /// <summary>Validates a terrain input file and records its intended import role.</summary>
    internal void SetTerrainSource(string kind, string path)
    {
        if (kind != "Heightmap" && kind != "Crust" && kind != "Cracks" && kind != "Lava")
            throw new InvalidOperationException("Unknown terrain source type.");
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Terrain source file not found.", fullPath);
        if (kind == "Heightmap")
        {
            var settings = _world.Element("TerrainSettings") ?? throw new InvalidOperationException("The template has no TerrainSettings.");
            var size = (int? )settings.Attribute("WorldSize") ?? 4096;
            if (new FileInfo(fullPath).Length != (long)size * size * 4)
                throw new InvalidOperationException("Heightmap must be a headerless " + size + "x" + size + " little-endian float32 file (" + ((long)size * size * 4).ToString("N0", CultureInfo.InvariantCulture) + " bytes). The game terrain tool is the source of truth for its terrain size.");
            ValidateHeightmapSamples(fullPath);
        }
        else
            ReadTerrainPngSize(fullPath);
        TerrainSourceFiles[kind] = fullPath;
        _structureChanged = true;
    }

    /// <summary>Rejects heightmaps with unsupported sample sizes or non-finite height values.</summary>
    private static void ValidateHeightmapSamples(string path)
    {
        using var stream = File.OpenRead(path);
        var buffer = new byte[64 * 1024];
        var remainder = 0;
        long sample = 0;
        while (true)
        {
            var read = stream.Read(buffer, remainder, buffer.Length - remainder);
            if (read == 0)
                break;
            var bytes = remainder + read;
            var completeBytes = bytes - bytes % 4;
            for (var offset = 0; offset < completeBytes; offset += 4, sample++)
            {
                var value = BitConverter.ToSingle(buffer, offset);
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new InvalidOperationException("Heightmap contains a non-finite float at sample " + sample.ToString(CultureInfo.InvariantCulture) + ".");
            }

            remainder = bytes - completeBytes;
            if (remainder > 0)
                Array.Copy(buffer, completeBytes, buffer, 0, remainder);
        }
    }

    /// <summary>Validates and records the compiled terrain chunks to package with the world.</summary>
    internal void SetCompiledTerrainFolder(string folder)
    {
        var fullPath = Path.GetFullPath(folder);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException("Compiled terrain folder not found.");
        var files = Directory.GetFiles(fullPath, "Terrain*.dat", SearchOption.TopDirectoryOnly).Where(f => Regex.IsMatch(Path.GetFileName(f), @"^Terrain\d+\.dat$", RegexOptions.IgnoreCase))
            .OrderBy(f => int.Parse(Regex.Match(Path.GetFileName(f), @"\d+").Value, CultureInfo.InvariantCulture))
            .ToList();
        if (files.Count == 0)
            throw new InvalidOperationException("Folder must contain at least one indexed Terrain<number>.dat file.");
        if (files.Any(f => new FileInfo(f).Length == 0))
            throw new InvalidOperationException("Compiled terrain files cannot be empty.");
        CompiledTerrainFiles.Clear();
        CompiledTerrainFiles.AddRange(files);
        _structureChanged = true;
    }
}
