using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    /// <summary>Finds optional native-world XML groups not yet present in this draft.</summary>
    private void RefreshOptionalSettings()
    {
        _optionalSettings.Clear();
        var found = new HashSet<string>(StringComparer.Ordinal);
        var worldDirectory = Path.Combine(_assets, "Worlds");
        foreach (var sourceFile in Directory.GetFiles(worldDirectory, "*.xml", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var sourceWorld = XDocument.Load(sourceFile).Root?.Element("WorldSettings")?.Element("World");
                if (sourceWorld == null)
                    continue;
                CollectOptionalSettings(sourceWorld, _world, "World", Path.GetFileNameWithoutExtension(sourceFile), found);
            }
            catch
            { /* Ignore unrelated or malformed XML in the world's asset tree. */
            }
        }

        var atmosphere = _world.Element("GlobalAtmosphere");
        if (atmosphere != null && atmosphere.Element("Volume") == null && found.Add("World/GlobalAtmosphere/Volume"))
            _optionalSettings.Insert(
                0,
                new OptionalSetting("World/GlobalAtmosphere/Volume", "serializer default", new XElement("Volume", new XAttribute("Value", "40000000000")))
            );
    }

    /// <summary>Recursively records missing optional groups while excluding records managed by dedicated editors.</summary>
    private void CollectOptionalSettings(XElement sourceParent, XElement targetParent, string parentPath, string sourceWorld, HashSet<string> found)
    {
        foreach (var sourceChild in sourceParent.Elements())
        {
            if (sourceParent.Elements(sourceChild.Name).Count() != 1)
                continue;
            var path = parentPath + "/" + sourceChild.Name.LocalName;
            if (NonOptionalRecords.Contains(sourceChild.Name.LocalName) || sourceChild.DescendantsAndSelf().Attributes().Any(a => a.Name.LocalName.Equals("Id", StringComparison.OrdinalIgnoreCase)))
                continue;
            var targetChildren = targetParent.Elements(sourceChild.Name).ToList();
            if (targetChildren.Count == 0 && found.Add(path))
                _optionalSettings.Add(new OptionalSetting(path, sourceWorld, sourceChild));
            else if (targetChildren.Count == 1)
                CollectOptionalSettings(sourceChild, targetChildren[0], path, sourceWorld, found);
        }
    }

    /// <summary>Copies a selected optional XML group into the draft and rebuilds its editable fields.</summary>
    internal void AddOptionalSetting(string path)
    {
        var setting = _optionalSettings.FirstOrDefault(s => s.Path == path) ?? throw new InvalidOperationException("That optional setting is no longer available.");
        var segments = path.Split('/');
        XElement parent = _world;
        for (var i = 1; i < segments.Length - 1; i++)
        {
            var matches = parent.Elements(segments[i]).ToList();
            if (matches.Count != 1)
                throw new InvalidOperationException("The setting's parent is not unique: " + string.Join("/", segments.Take(i + 1)));
            parent = matches[0];
        }

        if (parent.Elements(segments[segments.Length - 1]).Any())
            throw new InvalidOperationException("The setting already exists in this world.");
        var added = new XElement(setting.Element);
        parent.Add(added);
        AddAllFields(AdvancedFields, added, string.Join("/", segments.Take(segments.Length - 1)));
        if (path.Contains("/GlobalAtmosphere/"))
            AddEditableTree(AtmosphereFields, added, "Atmosphere/GlobalAtmosphere");
        MapFields.AddRange(AdvancedFields.Where(f => !MapFields.Contains(f) && (f.Label.IndexOf("/RegionSet", StringComparison.Ordinal) >= 0 || f.Label.IndexOf("/PointOfInterest", StringComparison.Ordinal) >= 0 || f.Label.IndexOf("/PlayableArea", StringComparison.Ordinal) >= 0 || f.Label.IndexOf("/GeographicRegionData", StringComparison.Ordinal) >= 0 || f.Label.IndexOf("/DeepMinablesRegionData", StringComparison.Ordinal) >= 0 || f.Label.IndexOf("/Minables/", StringComparison.Ordinal) >= 0 || f.Label.IndexOf("/DeepMinables/", StringComparison.Ordinal) >= 0)));
        _structureChanged = true;
        RefreshOptionalSettings();
    }
}
