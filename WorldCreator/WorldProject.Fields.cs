using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    /// <summary>Provides a player-facing explanation and units for a recognized XML field path.</summary>
    private static string DescribeField(string label)
    {
        var lower = label.ToLowerInvariant();
        var divider = label.LastIndexOf(" / ", StringComparison.Ordinal);
        var slash = label.LastIndexOf('/');
        var leaf = label.Substring(Math.Max(divider >= 0 ? divider + 3 : 0, slash >= 0 ? slash + 1 : 0)).Trim().TrimStart('@');
        if (leaf.StartsWith("Gravity", StringComparison.OrdinalIgnoreCase))
            return "Surface gravity in m/s². Stationeers world data uses negative values; a larger absolute value means stronger pull toward the surface.";
        if (leaf.Equals("Latitude", StringComparison.OrdinalIgnoreCase))
            return "Playable landing-site latitude in degrees: −90° is the south pole, 0° the equator and +90° the north pole.";
        if (leaf.Equals("Longitude", StringComparison.OrdinalIgnoreCase))
            return "Playable landing-site longitude in degrees around the selected body. This does not change the orbit or spin axis.";
        if (leaf.Equals("SemiMajorAxisAu", StringComparison.OrdinalIgnoreCase))
            return "Average orbital distance from the parent body, in astronomical units (AU). This changes the orbit and sunlight distance.";
        if (leaf.Equals("SemiMajorAxisKm", StringComparison.OrdinalIgnoreCase))
            return "Average orbital distance from the parent body, in kilometres. This is an alternate unit to AU for the same orbit size.";
        if (leaf.Equals("Period", StringComparison.OrdinalIgnoreCase))
            return "Time for one full orbit, in simulated game days.";
        if (leaf.Equals("Eccentricity", StringComparison.OrdinalIgnoreCase))
            return "Orbit shape: 0 is circular; values closer to 1 are more elongated. Keep an elliptical orbit below 1.";
        if (leaf.Equals("Inclination", StringComparison.OrdinalIgnoreCase))
            return "Angle between this orbit's plane and its reference plane, in degrees.";
        if (leaf.Equals("ArgumentOfPeriapsis", StringComparison.OrdinalIgnoreCase))
            return "Orbit orientation angle, in degrees. It sets the closest orbital point within the orbit plane.";
        if (leaf.Equals("LongitudeOfAscendingNode", StringComparison.OrdinalIgnoreCase))
            return "Orbit orientation angle, in degrees. It rotates the orbit plane around the reference axis.";
        if (leaf.Equals("LongitudeAtEpoch", StringComparison.OrdinalIgnoreCase))
            return "Celestial body's reference position at its orbital epoch, in degrees.";
        if (leaf.Equals("Priority", StringComparison.OrdinalIgnoreCase))
            return "World-list priority. It influences this world's placement in the world selection list.";
        if (leaf.Equals("Hidden", StringComparison.OrdinalIgnoreCase))
            return "When true, hides this world definition from the world selection menu.";
        if (leaf.Equals("Deprecated", StringComparison.OrdinalIgnoreCase))
            return "Marks this world definition as deprecated. Keep the source value unless you intend to flag the world as obsolete.";
        if (leaf.Equals("Id", StringComparison.OrdinalIgnoreCase))
            return "Identifier used by the game to connect this record to other world, body or resource data. Changing it can break those references.";
        if (leaf.Equals("Path", StringComparison.OrdinalIgnoreCase))
            return "GameData-relative file or folder path used by this setting. The referenced asset must be included in the mod or available in the installed game.";
        if (leaf.Equals("Format", StringComparison.OrdinalIgnoreCase))
            return "Texture or asset format identifier expected by the game loader. Keep a format supported by the referenced asset.";
        if (leaf.Equals("LoadType", StringComparison.OrdinalIgnoreCase))
            return "Asset loading strategy used by Stationeers. Preserve the source value unless you know the loader behavior.";
        if (leaf.Equals("WorldSize", StringComparison.OrdinalIgnoreCase))
            return "Terrain world-size value from the installed world XML. The game does not document this as a direct kilometre measurement.";
        if (leaf.Equals("RadiusKm", StringComparison.OrdinalIgnoreCase))
            return "Radius of this celestial body, in kilometres.";
        if (leaf.Equals("CanOccult", StringComparison.OrdinalIgnoreCase))
            return "Whether this celestial body may visually occult (cover) another body when they line up in the sky.";
        if (leaf.Equals("WeightedMode", StringComparison.OrdinalIgnoreCase))
            return "Unity animation-curve weighting mode for this point. The guided curve editor hides this technical value and preserves the source value.";
        if (leaf.Equals("Days", StringComparison.OrdinalIgnoreCase) && lower.Contains("synodicrotation"))
            return "Length of one synodic rotation, in game days. It controls the body's day/night rotation relative to its orbit.";
        if ((leaf.Equals("x", StringComparison.OrdinalIgnoreCase) || leaf.Equals("y", StringComparison.OrdinalIgnoreCase) || leaf.Equals("z", StringComparison.OrdinalIgnoreCase)) && lower.Contains("rotation"))
            return "One component of the celestial rotation axis vector. These three values together set the axis direction.";
        if ((leaf.Equals("R", StringComparison.OrdinalIgnoreCase) || leaf.Equals("G", StringComparison.OrdinalIgnoreCase) || leaf.Equals("B", StringComparison.OrdinalIgnoreCase)) && lower.Contains("region"))
            return "One color channel (red, green or blue) that identifies this region in its map mask. Changing it can disconnect the region from the texture.";
        if (leaf.Equals("Time", StringComparison.OrdinalIgnoreCase))
        {
            if (lower.Contains("/temperature/key"))
                return "Solar angle in degrees for the base temperature curve: 0° is sun overhead, 90° is the horizon and 180° is below the opposite side. This is not clock time.";
            if (lower.Contains("solarradiationtemperature"))
                return "Relative sunlight input used by this orbit's solar-radiation temperature curve, scaled from 0–100. It is not clock time, sun angle, or a location on the planet.";
            if (lower.Contains("ghgtemperatureoffset"))
                return "Greenhouse-index input for this offset curve. It is not a clock time; preserve the template's domain unless you know the runtime range.";
            if (lower.Contains("densityoffset"))
                return "Atmospheric gas-density input in mmol/L for this offset curve. It is not a clock time.";
            return "Curve input coordinate. Its meaning depends on the curve named in the setting path; it is not necessarily clock time.";
        }

        if (leaf.Equals("Value", StringComparison.OrdinalIgnoreCase))
        {
            if (lower.Contains("/temperature/key"))
                return "Absolute baseline atmosphere temperature at this solar angle, in kelvin (K).";
            if (lower.Contains("temperatureoffset") || lower.Contains("solarradiationtemperature") || lower.Contains("ghgtemperatureoffset") || lower.Contains("densityoffset"))
                return "Temperature adjustment contributed by this curve, in kelvin (K). This is a difference, so do not add 273.15.";
            if (lower.Contains("curvature"))
                return "Terrain curvature setting from the selected world definition. The game data does not document a physical unit.";
        }

        if (leaf.Equals("Quantity", StringComparison.OrdinalIgnoreCase) && lower.Contains("/gas"))
            return "Authored gas amount in moles per 8,000-litre atmosphere grid cell. It is an absolute quantity, not a percentage.";
        if (lower.Contains("/volume") && (leaf.Equals("Volume", StringComparison.OrdinalIgnoreCase) || leaf.Equals("Value", StringComparison.OrdinalIgnoreCase)))
            return "Effective global atmosphere simulation volume, in litres. This is separate from the 8,000-litre atmosphere grid cell.";
        if (lower.Contains("cooldown"))
            return leaf.Equals("Min", StringComparison.OrdinalIgnoreCase) || leaf.Equals("Max", StringComparison.OrdinalIgnoreCase) ? "Minimum/maximum interval in game days between scheduled weather events. A value of 0 does not disable the event; remove the event to exclude it." : "Weather cooldown interval, measured in game days.";
        if (lower.Contains("startdelay"))
            return "Minimum/maximum delay before this weather event starts, in seconds.";
        if (lower.Contains("duration"))
            return "Minimum/maximum duration of this weather event, in seconds.";
        if (leaf.Equals("OreDensity", StringComparison.OrdinalIgnoreCase))
            return "Relative ore-density multiplier for this resource region. It scales the selected template's baseline density.";
        if (leaf.Equals("WindStrength", StringComparison.OrdinalIgnoreCase))
            return "Wind-strength parameter used by this weather event. The XML exposes a number, but does not document its unit; compare with installed event examples.";
        if (leaf.Equals("DamageMultiplier", StringComparison.OrdinalIgnoreCase))
            return "Multiplier applied to weather damage. 1 is the event's authored baseline; 0 removes this contribution.";
        if (leaf.Equals("MovementSpeedMultiplier", StringComparison.OrdinalIgnoreCase))
            return "Multiplier applied to movement speed during this weather event. 1 leaves the authored baseline unchanged.";
        if (leaf.Equals("SolarRatio", StringComparison.OrdinalIgnoreCase))
            return "Solar-radiation ratio used by this weather event. Its exact runtime scaling is not documented; compare with installed event values.";
        if (leaf.Equals("BodyScale", StringComparison.OrdinalIgnoreCase))
            return "Celestial body visual scale. This is a rendering setting, not orbital distance.";
        if (leaf.Equals("SkyboxScale", StringComparison.OrdinalIgnoreCase))
            return "Visual skybox scale. This does not change the physical orbit.";
        if (leaf.Equals("InTangent", StringComparison.OrdinalIgnoreCase) || leaf.Equals("OutTangent", StringComparison.OrdinalIgnoreCase))
            return "Animation-curve slope at this key. It controls interpolation into or out of the point; World Creator hides this technical value in the guided curve editor and preserves it.";
        if (leaf.Equals("InWeight", StringComparison.OrdinalIgnoreCase) || leaf.Equals("OutWeight", StringComparison.OrdinalIgnoreCase))
            return "Animation-curve interpolation weight at this key. It shapes interpolation; World Creator hides this technical value in the guided curve editor and preserves it.";
        if (leaf.Equals("Min", StringComparison.OrdinalIgnoreCase) || leaf.Equals("Max", StringComparison.OrdinalIgnoreCase))
            return "Lower/upper bound for the setting identified by its path. Check the displayed unit and preserve the range order (minimum ≤ maximum).";
        if (lower.Contains("orbit"))
            return "Orbit setting from the installed celestial-body data. Hovered value: " + leaf + ". Use the displayed unit and preserve the selected body's parent relationship.";
        if (lower.Contains("weather"))
            return "Weather setting from the installed event definition. Hovered value: " + leaf + ". Its unit or runtime effect is not described by the XML field name; compare against the installed event before changing it.";
        if (lower.Contains("globalatmosphere"))
            return "Atmosphere setting at " + label + ". The XML stores this value in the global-atmosphere definition; its unit is not identified by the field name, so compare with the selected world before changing it.";
        if (lower.Contains("minables") || lower.Contains("resources"))
            return "Resource setting for the region identified in its path. The field is preserved from this world definition; compare with the selected template before changing it.";
        if (lower.Contains("terrain"))
            return "Terrain setting at " + label + ". It controls a terrain asset or rendering parameter; keep referenced assets available in the exported mod.";
        return "Advanced world XML setting: " + label + ". The installed XML does not identify this property's unit or full runtime effect. Its current source value is preserved; compare with the selected world before changing it.";
    }

    /// <summary>Binds an existing XML text element to one editable field.</summary>
    private static void AddText(List<Field> fields, XElement? element, string label)
    {
        if (element != null && double.TryParse(element.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            fields.Add(new Field(label, element.Value, value => element.Value = value));
    }

    /// <summary>Binds a known world property while preserving its original XML until the player changes it.</summary>
    private static void AddWorldField(
        List<Field> fields,
        XElement world,
        string elementPath,
        string? attribute,
        string label,
        string tooltip,
        bool numeric = false,
        bool boolean = false,
        string group = "Overview"
    )
    {
        var segments = string.IsNullOrWhiteSpace(elementPath) ? Array.Empty<string>() : elementPath.Split('/');
        /// <summary>Locates the requested nested world element without creating missing XML nodes.</summary>
        XElement? FindExisting()
        {
            XElement? current = world;
            foreach (var segment in segments)
                current = current?.Element(segment);
            return current;
        }

        var existing = FindExisting();
        var original = attribute == null ? existing?.Value ?? "" : (string? )existing?.Attribute(attribute) ?? "";
        fields.Add(new Field(group + "/" + label, original, value => WriteWorldFieldValue(world, segments, attribute, value), numeric, boolean, tooltip));
    }

    /// <summary>Creates a known property's missing XML ancestors only when its edited value is committed.</summary>
    private static void WriteWorldFieldValue(XElement world, IEnumerable<string> segments, string? attribute, string value)
    {
        var current = world;
        foreach (var segment in segments)
        {
            var child = current.Element(segment);
            if (child == null)
            {
                child = new XElement(segment);
                current.Add(child);
            }

            current = child;
        }

        if (attribute == null)
            current.Value = value;
        else
            current.SetAttributeValue(attribute, value);
    }

    /// <summary>Creates editable fields for the attributes of a recognized XML element.</summary>
    private static void AddEditableAttributes(List<Field> fields, XElement? element, string label)
    {
        if (element == null)
            return;
        foreach (var attribute in element.Attributes())
        {
            var current = attribute;
            if (current.Name.LocalName == "Id")
                continue;
            var numeric = double.TryParse(current.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
            var boolean = bool.TryParse(current.Value, out _);
            fields.Add(new Field(label + " / " + current.Name.LocalName, current.Value, value => current.Value = value, numeric, boolean));
        }
    }

    /// <summary>Walks a supported XML subtree and binds its leaf values to the editor.</summary>
    private static void AddEditableTree(List<Field> fields, XElement? element, string path)
    {
        if (element == null)
            return;
        var hint = (string? )element.Attribute("Type") ?? (string? )element.Attribute("Id");
        var siblings = element.Parent?.Elements(element.Name).Count() ?? 1;
        var index = siblings > 1 ? " #" + (element.ElementsBeforeSelf(element.Name).Count() + 1).ToString(CultureInfo.InvariantCulture) : "";
        var label = path + "/" + element.Name.LocalName + index + (hint == null ? "" : " [" + hint + "]");
        AddEditableAttributes(fields, element, label);
        if (!element.HasElements && !string.IsNullOrWhiteSpace(element.Value))
        {
            var current = element;
            var numeric = double.TryParse(current.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
            var boolean = bool.TryParse(current.Value, out _);
            fields.Add(new Field(label, current.Value, value => current.Value = value, numeric, boolean));
        }

        foreach (var child in element.Elements())
            AddEditableTree(fields, child, label);
    }

    /// <summary>Builds the complete advanced-field view while retaining XML element and attribute locations.</summary>
    private static void AddAllFields(List<Field> fields, XElement element, string path)
    {
        var siblings = element.Parent?.Elements(element.Name).Count() ?? 1;
        var index = siblings > 1 ? " #" + (element.ElementsBeforeSelf(element.Name).Count() + 1).ToString(CultureInfo.InvariantCulture) : "";
        var label = path + "/" + element.Name.LocalName + index;
        foreach (var attribute in element.Attributes())
        {
            var current = attribute;
            if (current.Name.LocalName == "nil" && current.Name.NamespaceName == "http://www.w3.org/2001/XMLSchema-instance")
                continue;
            var numeric = double.TryParse(current.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
            var boolean = bool.TryParse(current.Value, out _);
            fields.Add(new Field(label + "/@" + current.Name.LocalName, current.Value, value => current.Value = value, numeric, boolean, target: current));
        }

        var nil = element.Attributes().FirstOrDefault(a => a.Name.LocalName == "nil" && a.Name.NamespaceName == "http://www.w3.org/2001/XMLSchema-instance");
        if (nil != null && nil.Value == "true")
        {
            var current = element;
            fields.Add(new Field(label, "", value =>
            {
                current.Value = value;
                current.SetAttributeValue(nil.Name, null);
            }, numeric: false, boolean: true, target: current));
        }

        if (!element.HasElements && !string.IsNullOrWhiteSpace(element.Value))
        {
            var current = element;
            var numeric = double.TryParse(current.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
            var boolean = bool.TryParse(current.Value, out _);
            fields.Add(new Field(label, current.Value, value => current.Value = value, numeric, boolean, target: current));
        }

        foreach (var child in element.Elements())
            AddAllFields(fields, child, label);
    }

    /// <summary>Binds the overview and presentation controls to their corresponding world XML values.</summary>
    private void InitializeWorldFields()
    {
        AddText(WorldFields, _world.Element("Gravity"), "Gravity (negative m/s²)");
        AddEditableAttributes(WorldFields, _world.Element("PlayableBody"), "Location");
        AddWorldField(WorldFields, _world, "", "Priority", "World list priority", "Controls this world's position in the world list.", true);
        AddWorldField(WorldFields, _world, "", "Hidden", "World visibility", "When enabled, this world is hidden from the world selection list.", false, true);
        AddWorldField(
            WorldFields,
            _world,
            "PrimaryBody",
            "Id",
            "Primary body ID",
            "ID of the primary celestial body used for the world's star and lighting reference."
        );
        AddWorldField(
            WorldFields,
            _world,
            "PrimaryBody",
            "SolarConstant",
            "Primary star irradiance (W/m²)",
            "Reference stellar irradiance. The installed orbital calculation uses this value divided by distance in AU squared.",
            true
        );
        AddWorldField(WorldFields, _world, "AmbienceSound", null, "Ambience sound ID", "Installed or mod-provided ambience sound resource used by this world.");
        AddWorldField(WorldFields, _world, "SkyBoxMaterialName", null, "Skybox material ID", "Installed or mod-provided skybox material used by this world.");
        AddWorldField(WorldFields, _world, "SunPrefabName", null, "Sun prefab ID", "Installed or mod-provided sun and light prefab used by this world.");
        AddWorldField(
            WorldFields,
            _world,
            "CelestialConstants",
            "BodyScale",
            "Celestial body scale",
            "Visual scale for celestial bodies; this does not change orbital distance.",
            true
        );
        AddWorldField(
            WorldFields,
            _world,
            "CelestialConstants",
            "SkyboxScale",
            "Skybox scale",
            "Visual scale for the skybox; this does not change orbital distance.",
            true
        );
        AddWorldField(
            WorldFields,
            _world,
            "CelestialConstants/TimeOffset",
            "Days",
            "Clock offset · days",
            "Day component of the initial celestial clock offset.",
            true
        );
        AddWorldField(
            WorldFields,
            _world,
            "CelestialConstants/TimeOffset",
            "Hours",
            "Clock offset · hours",
            "Hour component of the initial celestial clock offset.",
            true
        );
        AddWorldField(
            WorldFields,
            _world,
            "CelestialConstants/TimeOffset",
            "Minutes",
            "Clock offset · minutes",
            "Minute component of the initial celestial clock offset.",
            true
        );
        AddWorldField(
            WorldFields,
            _world,
            "CelestialConstants/TimeOffset",
            "Seconds",
            "Clock offset · seconds",
            "Second component of the initial celestial clock offset.",
            true
        );
        AddWorldField(
            WorldFields,
            _world,
            "MaxSunIntensity",
            null,
            "Maximum sun intensity",
            "Upper bound for apparent sunlight and lighting intensity; solar-panel output is controlled separately.",
            true,
            false,
            "Environment"
        );
    }
}
