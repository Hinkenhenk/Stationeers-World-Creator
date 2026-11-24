using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    internal enum RotationMode { SynodicRotation, FixedRotation, TidalLocking }

    internal sealed class RotationSettings
    {
        internal readonly string BodyId;
        internal readonly string ParentId;
        internal readonly RotationMode Mode;
        internal readonly double X, Y, Z, Value, OrbitPeriod;
        internal readonly bool HasExplicitRotation;
        internal string InputKey => BodyId + "|" + ParentId + "|" + Mode + "|" + X.ToString("R", CultureInfo.InvariantCulture) + "|" + Y.ToString("R", CultureInfo.InvariantCulture) + "|" + Z.ToString("R", CultureInfo.InvariantCulture) + "|" + Value.ToString("R", CultureInfo.InvariantCulture);
        internal string CacheKey => InputKey + "|" + OrbitPeriod.ToString("R", CultureInfo.InvariantCulture);
        internal double SpeedDegreesPerDay => Mode == RotationMode.TidalLocking ? 360 / OrbitPeriod : Mode == RotationMode.SynodicRotation ? 360 / Value : Value;
        internal (double X, double Y, double Z) NormalizedAxis => NormalizeRotationAxis(X, Y, Z);

        /// <summary>Describes the draft's native rotation without rewriting its serialized values.</summary>
        internal RotationSettings(string bodyId, string parentId, RotationMode mode, double x, double y, double z, double value, double orbitPeriod, bool explicitRotation)
        {
            BodyId = bodyId;
            ParentId = parentId;
            Mode = mode;
            X = x; Y = y; Z = z;
            Value = value;
            OrbitPeriod = orbitPeriod;
            HasExplicitRotation = explicitRotation;
        }

        /// <summary>Matches the native CurrentAngle calculation, including its signed-speed and remainder conventions.</summary>
        internal double RotationAngleAtMeanAnomaly(double phase)
        {
            ValidateRotationValues(Mode, X, Y, Z, Value, OrbitPeriod);
            if (!FiniteRotation(phase)) throw new InvalidOperationException("Orbital phase must be finite.");
            var solarDegreesPerDay = 360 / (OrbitPeriod * SpeedDegreesPerDay / 360);
            var angle = (phase % solarDegreesPerDay) / solarDegreesPerDay * 360;
            return SpeedDegreesPerDay < -float.Epsilon ? (360 - angle % 360) % 360 : angle % 360;
        }
    }

    private bool _rotationChanged;
    internal bool CanEditRotation => _orbitBody != null;

    /// <summary>Reads pending Advanced values and uses the game's fallback axis/speed when no rotation was authored.</summary>
    internal RotationSettings GetRotationSettings()
    {
        var bodyId = RotationAttribute(_world.Element("PlayableBody")?.Attribute("Id")) ?? "Earth";
        var body = _world.Element("CelestialBodies")?.Elements().FirstOrDefault(element => RotationAttribute(element.Attribute("Id")) == bodyId);
        var rotation = body?.Elements().FirstOrDefault(IsRotationElement);
        var mode = rotation == null ? RotationMode.FixedRotation : (RotationMode)Enum.Parse(typeof(RotationMode), rotation.Name.LocalName);
        var parent = RotationAttribute(body?.Attribute("Parent"));
        var period = OrbitFields.FirstOrDefault(field => field.Label.EndsWith(" / Period", StringComparison.Ordinal))?.Value ?? (string?)_orbitBody?.Element("Orbit")?.Attribute("Period");
        var primary = string.IsNullOrWhiteSpace(PrimaryBodyId) ? "Sun" : PrimaryBodyId;
        return new RotationSettings(bodyId, string.IsNullOrWhiteSpace(parent) ? primary : parent!, mode,
            rotation == null ? 0 : RotationNumber(rotation.Attribute("x"), 0),
            rotation == null ? 1 : RotationNumber(rotation.Attribute("y"), 0),
            rotation == null ? 0 : RotationNumber(rotation.Attribute("z"), 0),
            mode == RotationMode.TidalLocking ? 0 : rotation == null ? 15 : RotationNumber(rotation.Attribute(mode == RotationMode.SynodicRotation ? "Days" : "Speed"), double.NaN),
            double.TryParse(period, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : double.NaN, rotation != null);
    }

    /// <summary>Writes one validated native rotation while retaining the body's identity, parent, appearance and unrelated XML.</summary>
    internal void SetRotationSettings(RotationMode mode, double x, double y, double z, double value)
    {
        if (!CanEditRotation) throw new InvalidOperationException("Orbit & seasons → Rotation: this world has no editable celestial-body definition.");
        var current = GetRotationSettings();
        ValidateRotationValues(mode, x, y, z, value, current.OrbitPeriod);
        var axis = NormalizeRotationAxis(x, y, z);
        var bodies = _world.Element("CelestialBodies");
        if (bodies == null) { bodies = new XElement("CelestialBodies"); _world.Add(bodies); }
        var body = bodies.Elements().FirstOrDefault(element => RotationAttribute(element.Attribute("Id")) == current.BodyId);
        if (body == null) { body = new XElement("Body", new XAttribute("Id", current.BodyId)); bodies.Add(body); }
        var existing = body.Elements().FirstOrDefault(IsRotationElement);
        var replacement = existing == null ? new XElement(mode.ToString()) : new XElement(existing);
        replacement.Name = mode.ToString();
        replacement.SetAttributeValue("x", axis.X.ToString("G17", CultureInfo.InvariantCulture));
        replacement.SetAttributeValue("y", axis.Y.ToString("G17", CultureInfo.InvariantCulture));
        replacement.SetAttributeValue("z", axis.Z.ToString("G17", CultureInfo.InvariantCulture));
        replacement.SetAttributeValue("Days", mode == RotationMode.SynodicRotation ? value.ToString("G17", CultureInfo.InvariantCulture) : null);
        replacement.SetAttributeValue("Speed", mode == RotationMode.FixedRotation ? value.ToString("G17", CultureInfo.InvariantCulture) : null);
        if (existing == null) body.Add(replacement);
        else existing.ReplaceWith(replacement);
        body.Elements().Where(element => element != replacement && IsRotationElement(element)).Remove();
        _rotationChanged = _structureChanged = true;
        RefreshMapFields();
    }

    /// <summary>Converts reference-plane tilt and axis direction into a normalized axis; both angles are in degrees.</summary>
    internal static (double X, double Y, double Z) RotationAxisFromAngles(double tilt, double direction)
    {
        if (!FiniteRotation(tilt) || tilt < 0 || tilt > 180 || !FiniteRotation(direction) || direction < -180 || direction > 180)
            throw new InvalidOperationException("Axis tilt must be 0–180° and axis direction must be −180–180°.");
        var radians = Math.PI / 180;
        return (Math.Sin(tilt * radians) * Math.Cos(direction * radians), Math.Cos(tilt * radians), Math.Sin(tilt * radians) * Math.Sin(direction * radians));
    }

    /// <summary>Returns tilt from the reference plane's positive Y axis and direction from positive X towards positive Z.</summary>
    internal static (double Tilt, double Direction) RotationAnglesFromAxis(double x, double y, double z)
    {
        var axis = NormalizeRotationAxis(x, y, z);
        return (Math.Acos(Math.Clamp(axis.Y, -1, 1)) * 180 / Math.PI, Math.Atan2(axis.Z, axis.X) * 180 / Math.PI);
    }

    /// <summary>Measures actual obliquity against the native orbit normal rather than confusing it with orbital inclination.</summary>
    internal static double RotationObliquity(double x, double y, double z, double inclination, double ascendingNode)
    {
        if (!FiniteRotation(inclination) || !FiniteRotation(ascendingNode)) throw new InvalidOperationException("Orbital plane angles must be finite.");
        var axis = NormalizeRotationAxis(x, y, z);
        var radians = Math.PI / 180;
        var normalX = Math.Sin(ascendingNode * radians) * Math.Sin(inclination * radians);
        var normalY = Math.Cos(inclination * radians);
        var normalZ = -Math.Cos(ascendingNode * radians) * Math.Sin(inclination * radians);
        return Math.Acos(Math.Clamp(axis.X * normalX + axis.Y * normalY + axis.Z * normalZ, -1, 1)) * 180 / Math.PI;
    }

    /// <summary>Checks editable rotation and Advanced XML together before any export is written.</summary>
    private void ValidateRotationSettings()
    {
        var settings = GetRotationSettings();
        var body = _world.Element("CelestialBodies")?.Elements().FirstOrDefault(element => RotationAttribute(element.Attribute("Id")) == settings.BodyId);
        if (body?.Elements().Count(IsRotationElement) > 1) throw new InvalidOperationException("Orbit & seasons → Rotation: the playable body must have only one rotation mode.");
        if (settings.HasExplicitRotation) ValidateRotationValues(settings.Mode, settings.X, settings.Y, settings.Z, settings.Value, settings.OrbitPeriod);
    }

    /// <summary>Rejects values that produce an invalid native axis, rotation speed or orbital progression.</summary>
    private static void ValidateRotationValues(RotationMode mode, double x, double y, double z, double value, double period)
    {
        if (!Enum.IsDefined(typeof(RotationMode), mode)) throw new InvalidOperationException("Choose a supported rotation mode.");
        NormalizeRotationAxis(x, y, z);
        if (!FiniteRotation(period) || period <= 0) throw new InvalidOperationException("Orbit & seasons → Rotation: orbital period must be a positive finite number.");
        var speed = mode == RotationMode.TidalLocking ? 360 / period : mode == RotationMode.SynodicRotation ? 360 / value : value;
        var solarDegreesPerDay = 360 / (period * speed / 360);
        if ((mode != RotationMode.TidalLocking && (!FiniteRotation(value) || value == 0)) || !FiniteRotation(speed) || speed == 0 || !FiniteRotation(solarDegreesPerDay) || solarDegreesPerDay == 0)
            throw new InvalidOperationException("Orbit & seasons → Rotation: use a finite nonzero rotation period or speed within the game's numeric range.");
    }

    /// <summary>Normalizes safely without overflow and rejects zero/nonfinite vectors and unsupported serializer values.</summary>
    private static (double X, double Y, double Z) NormalizeRotationAxis(double x, double y, double z)
    {
        var maximum = Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z)));
        if (!FiniteRotation(maximum) || maximum == 0 || maximum > float.MaxValue || maximum < float.Epsilon)
            throw new InvalidOperationException("Orbit & seasons → Rotation axis: enter a finite nonzero x, y, z vector within the game's numeric range.");
        x /= maximum; y /= maximum; z /= maximum;
        var length = Math.Sqrt(x * x + y * y + z * z);
        return (x / length, y / length, z / length);
    }

    /// <summary>Recognizes the three native serializer alternatives without treating unrelated body children as rotation.</summary>
    private static bool IsRotationElement(XElement element) => element.Name.LocalName == nameof(RotationMode.SynodicRotation) || element.Name.LocalName == nameof(RotationMode.FixedRotation) || element.Name.LocalName == nameof(RotationMode.TidalLocking);

    /// <summary>Reads a pending Advanced attribute without applying unrelated draft edits.</summary>
    private string? RotationAttribute(XAttribute? attribute) => attribute == null ? null : AdvancedFields.FirstOrDefault(field => field.Target == attribute)?.Value ?? attribute.Value;

    /// <summary>Returns malformed authored numbers as NaN so validation can report them rather than replacing their values.</summary>
    private double RotationNumber(XAttribute? attribute, double fallback) => attribute == null ? fallback : double.TryParse(RotationAttribute(attribute), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN;

    /// <summary>Identifies numbers that the native arithmetic can safely use.</summary>
    private static bool FiniteRotation(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
