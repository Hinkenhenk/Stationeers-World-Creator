using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    internal const string StarRuntimeId = "worldcreator.stationeers.runtime";
    private const string StarConfigName = "worldcreator-star.config";
    private const string StarTextureName = "worldcreator-star.png";
    private const int MaxStarTextureBytes = 32 * 1024 * 1024;
    private readonly Dictionary<string, StarAppearance> _customStars = new Dictionary<string, StarAppearance>(StringComparer.Ordinal);
    private string StarPrimaryId => string.IsNullOrWhiteSpace(PrimaryBodyId) ? "Sun" : PrimaryBodyId;
    internal StarAppearance? CustomStar => _customStars.TryGetValue(StarPrimaryId, out var star) ? star : null;

    internal sealed class StarAppearance
    {
        internal readonly string TexturePath;
        internal readonly byte[] Texture;
        internal readonly float Diameter;
        internal readonly float Glow;
        internal readonly string LightRgb;

        /// <summary>Retains a private copy of the selected texture and the parameters used to display it.</summary>
        internal StarAppearance(string texturePath, byte[] texture, float diameter, float glow, string rgb)
        {
            TexturePath = texturePath;
            Texture = texture;
            Diameter = diameter;
            Glow = glow;
            LightRgb = rgb;
        }
    }

    /// <summary>Applies a project-owned visual to the selected primary body.</summary>
    internal void SetCustomStar(string texturePath, float diameter, float glow, string rgb)
    {
        var primary = StarPrimaryId;
        _customStars[primary] = ReadStarAppearance(texturePath, diameter, glow, rgb, CustomStar);
        _structureChanged = true;
    }

    /// <summary>Restores the selected primary body's stock visuals and drops its export dependency.</summary>
    internal void ClearCustomStar()
    {
        if (_customStars.Remove(StarPrimaryId)) _structureChanged = true;
    }

    /// <summary>Returns the cached texture for the orbit preview without loading the library file again.</summary>
    internal byte[]? GetCustomStarTexture(string primaryId) => _customStars.TryGetValue(primaryId, out var star) ? star.Texture : null;

    /// <summary>Checks star parameters and snapshots a bounded PNG before changing the draft.</summary>
    private static StarAppearance ReadStarAppearance(string path, float diameter, float glow, string rgb, StarAppearance? previous = null)
    {
        if (!FiniteRotation(diameter) || diameter < .1f || diameter > 30f)
            throw new InvalidOperationException("Star diameter must be 0.1–30.");
        if (!FiniteRotation(glow) || glow < 0 || glow > 8)
            throw new InvalidOperationException("Star glow must be 0–8.");
        if (!TryParseCelestialColour(rgb, out _))
            throw new InvalidOperationException("Star light RGB must have three values from 0 to 1.");

        if (previous?.TexturePath == path)
            return new StarAppearance(path, previous.Texture, diameter, glow, rgb);
        var size = ReadTerrainPngSize(path);
        if (size.Width > 4096 || size.Height > 4096)
            throw new InvalidOperationException("Star textures must be at most 4096 × 4096 pixels.");

        using var file = File.OpenRead(path);
        if (file.Length > MaxStarTextureBytes)
            throw new InvalidOperationException("Star textures must be at most 32 MiB.");
        var bytes = new byte[(int)file.Length];
        var offset = 0;
        while (offset < bytes.Length)
        {
            var count = file.Read(bytes, offset, bytes.Length - offset);
            if (count == 0) throw new EndOfStreamException("The star texture changed while it was being read.");
            offset += count;
        }
        return new StarAppearance(path, bytes, diameter, glow, rgb);
    }

    /// <summary>Restores the project's custom star from an exported world belonging to this source XML.</summary>
    private void LoadCustomStar(string sourceFile)
    {
        var directory = Path.GetDirectoryName(sourceFile)!;
        var path = Path.Combine(directory, StarConfigName);
        if (!File.Exists(path)) return;
        try
        {
            using var reader = XmlReader.Create(path, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4096
            });
            var config = XElement.Load(reader);
            if (config.Name != "CustomStar" || (string?)config.Attribute("Version") != "1"
                || (string?)config.Attribute("WorldId") != (string?)_world.Attribute("Id")
                || (string?)config.Attribute("PrimaryId") != StarPrimaryId) return;

            var diameter = float.Parse((string?)config.Attribute("Diameter") ?? "", CultureInfo.InvariantCulture);
            var glow = float.Parse((string?)config.Attribute("Glow") ?? "", CultureInfo.InvariantCulture);
            var rgb = (string?)config.Attribute("LightRgb") ?? "";
            _customStars[StarPrimaryId] = ReadStarAppearance(Path.Combine(directory, StarTextureName), diameter, glow, rgb);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is XmlException
                                      || error is InvalidOperationException || error is FormatException || error is OverflowException)
        {
            throw new InvalidDataException("Could not load the exported custom star: " + error.Message, error);
        }
    }

    /// <summary>Writes the custom star and reconciles its dependency after any existing Workshop metadata has been restored.</summary>
    private void WriteCustomStar(string package, XElement world)
    {
        var primaryId = (string?)world.Element("PrimaryBody")?.Attribute("Id");
        var primary = string.IsNullOrWhiteSpace(primaryId) ? "Sun" : primaryId;
        var metadataPath = Path.Combine(package, "About", "About.xml");
        var metadata = XDocument.Load(metadataPath);
        var root = metadata.Root!;
        root.Elements("DependsOn").Where(element => (string?)element.Attribute("ModID") == StarRuntimeId).Remove();
        if (_customStars.TryGetValue(primary, out var star))
        {
            if (string.IsNullOrWhiteSpace((string?)world.Element("SunPrefabName")))
                world.SetElementValue("SunPrefabName", "MarsSun");
            var gameData = Path.Combine(package, "GameData");
            var config = new XElement("CustomStar",
                new XAttribute("Version", "1"),
                new XAttribute("WorldId", (string)world.Attribute("Id")!),
                new XAttribute("PrimaryId", primary),
                new XAttribute("Diameter", star.Diameter.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("Glow", star.Glow.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("LightRgb", star.LightRgb));
            config.Save(Path.Combine(gameData, StarConfigName));
            File.WriteAllBytes(Path.Combine(gameData, StarTextureName), star.Texture);
            root.Add(new XElement("DependsOn", new XAttribute("ModID", StarRuntimeId)));
        }
        metadata.Save(metadataPath);
    }
}
