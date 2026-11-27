using System;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    internal sealed class RotationLighting
    {
        internal readonly (double X, double Y, double Z) BodySunDirection;
        internal readonly (double X, double Y, double Z) SiteNormal;
        internal readonly double SolarAngle;

        /// <summary>Shares one native-coordinate lighting result between the landing globe and climate calculation.</summary>
        internal RotationLighting((double X, double Y, double Z) direction, (double X, double Y, double Z) normal)
        {
            BodySunDirection = direction;
            SiteNormal = normal;
            var dot = direction.X * normal.X + direction.Y * normal.Y + direction.Z * normal.Z;
            SolarAngle = Math.Acos(Math.Clamp(dot, -1, 1)) * 180 / Math.PI;
        }
    }

    /// <summary>Matches native WorldSunVector and scene-zenith geometry from a physical planet-to-star direction.</summary>
    internal static RotationLighting CalculateRotationLighting(RotationSettings rotation, double meanPhase, double latitude, double longitude, double starX, double starY, double starZ)
    {
        var normal = RotationSiteNormal(rotation, latitude, longitude);
        var axis = rotation.NormalizedAxis;
        var spin = rotation.RotationAngleAtMeanAnomaly(meanPhase);
        var source = NormalizeRotationAxis(starX, starY, starZ);
        // Native Celestial.GetSceneLocalPosition uses planet-minus-star, opposite the physical input direction.
        var direction = RotateRotationVector((-source.X, -source.Y, -source.Z), axis, spin);
        return new RotationLighting(direction, normal);
    }

    /// <summary>Maps native latitude/longitude to the same body-frame surface normal used by the scene transform.</summary>
    internal static (double X, double Y, double Z) RotationSiteNormal(RotationSettings rotation, double latitude, double longitude)
    {
        if (!FiniteRotation(latitude) || latitude < -90 || latitude > 90 || !FiniteRotation(longitude) || Math.Abs(longitude) > float.MaxValue)
            throw new InvalidOperationException("Latitude must be −90–90° and longitude must be finite and within the game's numeric range.");
        var radians = latitude * Math.PI / 180;
        return RotateRotationVector((0, Math.Sin(radians), Math.Cos(radians)), rotation.NormalizedAxis, -(longitude % 360));
    }

    /// <summary>Builds an undistorted geographic grid in the globe's local frame before one shared axis/view rotation.</summary>
    internal static (double X, double Y, double Z) GeographicSurfaceNormal(double latitude, double longitude)
    {
        if (!FiniteRotation(latitude) || latitude < -90 || latitude > 90 || !FiniteRotation(longitude) || Math.Abs(longitude) > float.MaxValue)
            throw new InvalidOperationException("Latitude must be −90–90° and longitude must be finite and within the game's numeric range.");
        var lat = latitude * Math.PI / 180;
        var lon = (longitude % 360) * Math.PI / 180;
        return (-Math.Cos(lat) * Math.Sin(lon), Math.Sin(lat), Math.Cos(lat) * Math.Cos(lon));
    }

    /// <summary>Applies the native right-handed axis rotation without depending on Unity's native quaternion calls.</summary>
    private static (double X, double Y, double Z) RotateRotationVector((double X, double Y, double Z) value, (double X, double Y, double Z) axis, double degrees)
    {
        var angle = degrees * Math.PI / 180;
        var cosine = Math.Cos(angle);
        var sine = Math.Sin(angle);
        var dot = axis.X * value.X + axis.Y * value.Y + axis.Z * value.Z;
        return (value.X * cosine + (axis.Y * value.Z - axis.Z * value.Y) * sine + axis.X * dot * (1 - cosine),
            value.Y * cosine + (axis.Z * value.X - axis.X * value.Z) * sine + axis.Y * dot * (1 - cosine),
            value.Z * cosine + (axis.X * value.Y - axis.Y * value.X) * sine + axis.Z * dot * (1 - cosine));
    }
}
