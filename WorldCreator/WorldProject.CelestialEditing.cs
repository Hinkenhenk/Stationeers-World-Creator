using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    internal const string CelestialColourLabel = "Celestial body/RGB (0–1)";

    /// <summary>Lists bodies in this world's simulation, excluding the playable body and circular parent chains.</summary>
    internal string[] GetOrbitParentIds()
    {
        var current = GetRotationSettings();
        var primary = string.IsNullOrWhiteSpace(PrimaryBodyId) ? "Sun" : PrimaryBodyId;
        var ids = (_world.Element("CelestialBodies")?.Elements() ?? Enumerable.Empty<XElement>())
            .Select(body => RotationAttribute(body.Attribute("Id")) ?? string.Empty)
            .Concat(AvailableCelestialIds()).Append(primary).Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).Where(id => !OrbitParentCreatesCycle(id, current.BodyId));
        return ids.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>Detects self-parenting and ancestor loops, including pending Advanced edits.</summary>
    private bool OrbitParentCreatesCycle(string parentId, string bodyId)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal) { bodyId };
        while (!string.IsNullOrEmpty(parentId))
        {
            if (!visited.Add(parentId)) return true;
            parentId = CelestialParent(parentId) ?? string.Empty;
        }
        return false;
    }

    /// <summary>Changes the draft orbit parent and tidal-lock target together without modifying the source world.</summary>
    internal void SetOrbitParent(string parentId)
    {
        if (!CanEditRotation || !GetOrbitParentIds().Contains(parentId, StringComparer.Ordinal))
            throw new InvalidOperationException("Orbit & seasons → Parent body: choose an available body without creating a circular orbit.");
        ValidateCelestialImport(parentId);
        EnsureCelestialBody(parentId);
        var settings = GetRotationSettings();
        var bodies = _world.Element("CelestialBodies");
        if (bodies == null) { bodies = new XElement("CelestialBodies"); _world.Add(bodies); }
        var body = bodies.Elements().FirstOrDefault(element => RotationAttribute(element.Attribute("Id")) == settings.BodyId);
        if (body == null) { body = new XElement("Body", new XAttribute("Id", settings.BodyId)); bodies.Add(body); }
        SetCelestialAttribute(body, "Parent", parentId);
        var playable = _world.Element("PlayableBody");
        if (playable != null) SetCelestialAttribute(playable, "Parent", parentId);
        var attribute = body.Attribute("Parent");
        foreach (var field in AdvancedFields.Where(field => field.Target == attribute)) field.Value = parentId;
        _rotationChanged = _structureChanged = true;
        RefreshMapFields();
    }

    /// <summary>Parses a complete normalized celestial colour without rounding or accepting partial or invalid channels.</summary>
    internal static bool TryParseCelestialColour(string text, out (float R, float G, float B) colour)
    {
        colour = default;
        var parts = text.Split(',');
        if (parts.Length != 3) return false;
        var values = new float[3];
        for (var i = 0; i < values.Length; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                || double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 1
                || value != 0 && (float)value == 0) return false;
            values[i] = (float)value;
        }
        colour = (values[0], values[1], values[2]);
        return true;
    }

    /// <summary>Writes validated channel strings to the original XML attributes while preserving their precision.</summary>
    private static void WriteCelestialColour(XElement element, string text)
    {
        if (!TryParseCelestialColour(text, out _))
            throw new InvalidOperationException("Orbit & seasons → RGB: enter three values from 0 to 1 separated by commas, for example 0.9,0.5,0.3.");
        var parts = text.Split(',');
        var channels = new[] { "r", "g", "b" };
        for (var i = 0; i < channels.Length; i++) element.SetAttributeValue(channels[i], parts[i].Trim());
    }
}
