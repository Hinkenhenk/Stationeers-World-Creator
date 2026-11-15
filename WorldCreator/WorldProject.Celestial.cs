using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    /// <summary>Resolves source-world asset paths against the source mod and installed game folders.</summary>
    private string? ResolveImportedAsset(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || string.IsNullOrEmpty(_sourceAssetRoot))
            return path;
        var fullPath = Path.Combine(_sourceAssetRoot, path.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath) || Directory.Exists(fullPath))
            return fullPath;
        if (!string.IsNullOrEmpty(_sourceGameDataRoot))
        {
            fullPath = Path.Combine(_sourceGameDataRoot, path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(fullPath) || Directory.Exists(fullPath))
                return fullPath;
        }

        return path;
    }

    /// <summary>Finds the primary body's configured texture for the orbit preview.</summary>
    internal string? GetPrimaryBodyTexturePath(string id)
    {
        var sourceDefinition = FindPrimaryBodyDefinition(_sourceXml, id);
        var texturePath = (string? )(sourceDefinition ?? FindPrimaryBodyDefinition(_celestialBodyData, id))?.Element("Texture")?.Attribute("Path");
        if (string.IsNullOrWhiteSpace(texturePath) || Path.IsPathRooted(texturePath))
            return texturePath;
        if (sourceDefinition != null)
            return ResolveImportedAsset(texturePath);
        var gameAsset = Path.Combine(_assets, texturePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(gameAsset) ? gameAsset : texturePath;
    }

    /// <summary>Reads the primary body's visual color when no texture is available.</summary>
    internal (float R, float G, float B)? GetPrimaryBodyColor(string id)
    {
        var color = FindPrimaryBodyDefinition(id)?.Element("Color");
        if (color == null || !float.TryParse((string? )color.Attribute("r"), NumberStyles.Float, CultureInfo.InvariantCulture, out var r) || !float.TryParse((string? )color.Attribute("g"), NumberStyles.Float, CultureInfo.InvariantCulture, out var g) || !float.TryParse((string? )color.Attribute("b"), NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
            return null;
        if (r > 1 || g > 1 || b > 1)
        {
            r /= 255f;
            g /= 255f;
            b /= 255f;
        }

        return (Math.Clamp(r, 0, 1), Math.Clamp(g, 0, 1), Math.Clamp(b, 0, 1));
    }

    /// <summary>Identifies black-hole primary bodies from their loaded celestial definition.</summary>
    internal bool IsBlackHolePrimary(string id)
    {
        if (id.IndexOf("blackhole", StringComparison.OrdinalIgnoreCase) >= 0 || id.IndexOf("black hole", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        var primaryBackdrop = _world.Descendants("PlanetPrefab").Elements("Name").Any(name => string.Equals(name.Value, id, StringComparison.OrdinalIgnoreCase));
        return primaryBackdrop && _world.Descendants("PreviewScene").Descendants("Prefab").Elements("Name").Any(name => name.Value.IndexOf("blackhole", StringComparison.OrdinalIgnoreCase) >= 0 || name.Value.IndexOf("black hole", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    /// <summary>Identifies star-like primary bodies from their loaded celestial definition.</summary>
    internal bool IsStarPrimary(string id)
    {
        return id.Equals("Sun", StringComparison.OrdinalIgnoreCase) || id.IndexOf("star", StringComparison.OrdinalIgnoreCase) >= 0 || FindPrimaryBodyDefinition(id)?.Name.LocalName.Equals("Star", StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>Looks up a celestial definition by ID in the imported world and native body library.</summary>
    private XElement? FindPrimaryBodyDefinition(string id)
    {
        return FindPrimaryBodyDefinition(_sourceXml, id) ?? FindPrimaryBodyDefinition(_celestialBodyData, id);
    }

    /// <summary>Looks up a celestial definition by ID in the imported world and native body library.</summary>
    private static XElement? FindPrimaryBodyDefinition(XElement root, string id)
    {
        return root.DescendantsAndSelf().FirstOrDefault(element => string.Equals((string? )element.Attribute("Id"), id, StringComparison.OrdinalIgnoreCase) && (element.Element("Texture") != null || element.Element("Color") != null));
    }

    /// <summary>Binds editable celestial display, longitude and sunrise-offset values to the orbit draft.</summary>
    private void AddCelestialBodySettings()
    {
        if (_orbitBody == null)
            return;
        var longitude = _orbitBody.Element("LongitudeAtEpoch");
        if (longitude == null)
        {
            longitude = new XElement("LongitudeAtEpoch", "0");
            _orbitBody.Add(longitude);
        }

        OrbitFields.Add(new Field(
            "Celestial body/Longitude at epoch",
            longitude.Value,
            value => longitude.Value = value,
            tooltip: "Reference longitude of this celestial body's position at the orbital epoch, in degrees. This is distinct from the orbit's argument of periapsis."
        ));
        var color = _orbitBody.Element("Color");
        if (color == null)
        {
            color = new XElement("Color", new XAttribute("r", "1"), new XAttribute("g", "1"), new XAttribute("b", "1"));
            _orbitBody.Add(color);
        }

        foreach (var channel in new[]
        {
            (Name: "r", Label: "red", Description: "Red"),
            (Name: "g", Label: "green", Description: "Green"),
            (Name: "b", Label: "blue", Description: "Blue")
        }

        )
        {
            var attribute = color.Attribute(channel.Name);
            if (attribute == null)
            {
                color.SetAttributeValue(channel.Name, "1");
                attribute = color.Attribute(channel.Name)!;
            }

            var current = attribute;
            OrbitFields.Add(new Field(
                "Celestial body/Display color/" + channel.Label,
                current.Value,
                value => current.Value = value,
                tooltip: channel.Description + " channel (0–1) for this body's color in celestial-body views. This is a visual color, not a terrain texture."
            ));
        }

        var sunrise = _orbitBody.Element("SunriseOffset");
        if (sunrise == null)
        {
            sunrise = new XElement("SunriseOffset", new XAttribute("Value", "0"));
            _orbitBody.Add(sunrise);
        }

        var offset = sunrise.Attribute("Value");
        if (offset == null)
        {
            sunrise.SetAttributeValue("Value", "0");
            offset = sunrise.Attribute("Value")!;
        }

        var sunriseValue = offset;
        OrbitFields.Add(new Field(
            "Celestial body/Sunrise offset",
            sunriseValue.Value,
            value => sunriseValue.Value = value,
            tooltip: "Offset, in degrees, applied to the body's sunrise and sunset timing. Zero keeps the default alignment."
        ));
    }
}
