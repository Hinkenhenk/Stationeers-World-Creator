using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    internal sealed class OptionalSetting
    {
        internal readonly string Path;
        internal readonly string SourceWorld;
        internal readonly XElement Element;
        /// <summary>Retains a missing XML setting with its source-world provenance for optional import.</summary>
        internal OptionalSetting(string path, string sourceWorld, XElement element)
        {
            Path = path;
            SourceWorld = sourceWorld;
            Element = new XElement(element);
        }
    }

    internal sealed class MapLayer
    {
        internal sealed class Region
        {
            internal readonly string Id;
            internal readonly int R;
            internal readonly int G;
            internal readonly int B;
            internal readonly string? NameKey;
            /// <summary>Stores the identifier, mask color and optional localization key for one map region.</summary>
            internal Region(string id, int r, int g, int b, string? nameKey = null)
            {
                Id = id;
                R = r;
                G = g;
                B = b;
                NameKey = nameKey;
            }
        }

        internal readonly string Id;
        internal readonly string TexturePath;
        internal readonly List<Region> Regions = new List<Region>();
        /// <summary>Creates a map-mask view with its XML identifier and source texture path.</summary>
        internal MapLayer(string id, string texturePath)
        {
            Id = id;
            TexturePath = texturePath;
        }
    }

    internal sealed class PointOfInterest
    {
        internal readonly string Id;
        internal readonly string RegionId;
        internal readonly string NameKey;
        /// <summary>Retains a point-of-interest reference and the region that locates it on the map.</summary>
        internal PointOfInterest(string id, string regionId, string nameKey)
        {
            Id = id;
            RegionId = regionId;
            NameKey = nameKey;
        }
    }

    internal sealed class StartConditionView
    {
        internal readonly string Id;
        internal readonly string Name;
        internal readonly bool IsDefault;
        internal readonly bool IsCustom;
        internal readonly bool IsBrutal;
        internal readonly bool IsTerrainEdit;
        internal readonly IReadOnlyList<(string Id, string Event)> Spawns;
        internal readonly IReadOnlyList<StartCrateView> Crates;
        /// <summary>Collects the profile flags, event references and crates needed by the starting-conditions page.</summary>
        internal StartConditionView(
            string id,
            string name,
            bool isDefault,
            bool isCustom,
            bool isBrutal,
            bool isTerrainEdit,
            IReadOnlyList<(string Id, string Event)> spawns,
            IReadOnlyList<StartCrateView> crates
        )
        {
            Id = id;
            Name = name;
            IsDefault = isDefault;
            IsCustom = isCustom;
            IsBrutal = isBrutal;
            IsTerrainEdit = isTerrainEdit;
            Spawns = spawns;
            Crates = crates;
        }
    }

    internal sealed class StartCrateView
    {
        internal readonly string Id;
        internal readonly string Name;
        internal readonly IReadOnlyList<(string Id, int Quantity)> Items;
        /// <summary>Collects a custom crate's friendly name and item quantities for display.</summary>
        internal StartCrateView(string id, string name, IReadOnlyList<(string Id, int Quantity)> items)
        {
            Id = id;
            Name = name;
            Items = items;
        }
    }

    internal sealed class ProjectAsset
    {
        internal readonly string Id;
        internal readonly string Name;
        internal readonly string Category;
        internal readonly string Kind;
        internal readonly string Extension;
        internal readonly byte[] Data;
        /// <summary>Retains one packaged asset's bytes, category and file metadata independently of its library source.</summary>
        internal ProjectAsset(string id, string name, string category, string kind, string extension, byte[] data)
        {
            Id = id;
            Name = name;
            Category = category;
            Kind = kind;
            Extension = extension;
            Data = data;
        }

        /// <summary>Builds a filesystem-safe, world-specific output path for the packaged asset.</summary>
        internal string RelativePath(string worldId)
        {
            var safeName = Regex.Replace(Path.GetFileNameWithoutExtension(Name), "[^A-Za-z0-9_-]", "_");
            return "Worlds/" + worldId + "/Assets/" + safeName + "_" + Id.Substring(0, 8) + Extension;
        }
    }

    internal sealed class WeatherTiming
    {
        internal readonly string Id;
        internal readonly double CooldownMin;
        internal readonly double CooldownMax;
        internal readonly double DelayMinSeconds;
        internal readonly double DelayMaxSeconds;
        internal readonly double DurationMinSeconds;
        internal readonly double DurationMaxSeconds;
        /// <summary>Reads one weather event's current cooldown, start delay and duration ranges.</summary>
        internal WeatherTiming(string id, XElement element, IEnumerable<Field> fields)
        {
            Id = id;
            var ownFields = fields.Where(f => f.Label.StartsWith(id + "/", StringComparison.Ordinal)).ToList();
            CooldownMin = Read(ownFields, "CoolDown / Min", element.Element("CoolDown"), "Min");
            CooldownMax = Read(ownFields, "CoolDown / Max", element.Element("CoolDown"), "Max");
            DelayMinSeconds = Read(ownFields, "StartDelay / Min", element.Element("StartDelay"), "Min");
            DelayMaxSeconds = Read(ownFields, "StartDelay / Max", element.Element("StartDelay"), "Max");
            DurationMinSeconds = Read(ownFields, "Duration / Min", element.Element("Duration"), "Min");
            DurationMaxSeconds = Read(ownFields, "Duration / Max", element.Element("Duration"), "Max");
        }

        /// <summary>Reads the current weather-field draft, falling back to its original XML attribute when necessary.</summary>
        private static double Read(IEnumerable<Field> fields, string suffix, XElement? element, string attribute)
        {
            var field = fields.FirstOrDefault(f => f.Label.EndsWith(suffix, StringComparison.Ordinal));
            if (double.TryParse(field?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var current))
                return current;
            return double.TryParse((string? )element?.Attribute(attribute), NumberStyles.Float, CultureInfo.InvariantCulture, out var original) ? original : 0;
        }
    }

    internal sealed class Field
    {
        internal readonly string Label;
        internal readonly string Tooltip;
        internal readonly string Original;
        internal string Value;
        internal readonly bool IsBoolean;
        internal readonly bool IsNumeric;
        private readonly bool _boolean;
        private readonly Action<string> _write;
        /// <summary>Binds a labeled draft value, validation rules and writer to its original XML location.</summary>
        internal Field(string label, string value, Action<string> write, bool numeric = true, bool boolean = false, string? tooltip = null)
        {
            Label = label;
            Tooltip = tooltip ?? DescribeField(label);
            Original = value;
            Value = value;
            _write = write;
            IsNumeric = numeric;
            _boolean = boolean;
            IsBoolean = boolean;
        }

        /// <summary>Rejects invalid numeric, boolean, gas-quantity and bounded-curve edits before they reach XML.</summary>
        internal void Validate()
        {
            if (Value != Original && IsNumeric)
                ValidateNumericField(Label, Value);

            if (Value != Original && _boolean && !bool.TryParse(Value, out _))
                throw new InvalidOperationException("Enter true or false for " + Label + ".");
            if (Label.Contains("/Gas") && Label.EndsWith(" / Quantity", StringComparison.Ordinal) && double.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var quantity) && quantity < 0)
                throw new InvalidOperationException("Gas quantities cannot be negative: " + Label);
        }

        /// <summary>Writes the field draft back to XML when it changed or the caller explicitly requests an unchanged value.</summary>
        internal void Apply(bool includeUnchanged = false)
        {
            if (includeUnchanged || Value != Original)
                _write(Value);
        }
    }
}
