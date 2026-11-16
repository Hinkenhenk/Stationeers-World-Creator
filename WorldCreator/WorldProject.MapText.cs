using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    private readonly Dictionary<string, string> _sourceMapText = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _mapTextEdits = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Reads player-facing text while preserving imported keys and literal values.</summary>
    internal string GetPoiText(PointOfInterest poi, string elementName)
    {
        var element = FindPoi(poi).Element(elementName);
        var literal = (string?)element?.Attribute("Value");
        if (literal != null) return literal;
        var key = (string?)element?.Attribute("Key");
        return key != null && TryGetMapText(key, out var text) ? text : elementName == "Name" ? poi.Id : "";
    }

    /// <summary>Updates readable POI text and creates a missing key from the region without changing imported IDs or keys.</summary>
    internal void SetPoiText(PointOfInterest poi, string elementName, string text)
    {
        if (elementName != "Name" && elementName != "Description") throw new ArgumentException("Select a POI name or description.", nameof(elementName));
        if (GetPoiText(poi, elementName) == text) return;
        if (GetPoiText(poi, elementName) == text) return;
        var definition = FindPoi(poi);
        var element = definition.Element(elementName);
        if (element == null) { element = new XElement(elementName); definition.Add(element); }
        if (element.Attribute("Value") != null)
            element.SetAttributeValue("Value", text);
        else
        {
            var key = (string?)element.Attribute("Key");
            if (string.IsNullOrWhiteSpace(key)) { key = poi.RegionId + elementName; element.SetAttributeValue("Key", key); }
            _mapTextEdits[key!] = text;
        }
        _structureChanged = true;
    }

    /// <summary>Finds the backing POI without duplicating the player's imported reference records.</summary>
    private XElement FindPoi(PointOfInterest poi) => _world.Descendants("PointOfInterest").First(element => (string?)element.Attribute("Id") == poi.Id);

    /// <summary>Resolves text from edits, custom geographic names or the imported language records.</summary>
    private bool TryGetMapText(string key, out string text) => _mapTextEdits.TryGetValue(key, out text!) || _mapRegionNames.TryGetValue(key, out text!) || _sourceMapText.TryGetValue(key, out text!);

    /// <summary>Checks that automatically managed POI keys have their corresponding exported English text.</summary>
    private void ValidateMapTextExport(string folder)
    {
        if (_mapTextEdits.Count == 0) return;
        var records = XDocument.Load(Path.Combine(folder, "GameData", "Language", "english.xml")).Root!.Element("Interface")!.Elements("Record");
        foreach (var pair in _mapTextEdits)
            if (records.Count(record => (string?)record.Element("Key") == pair.Key && (string?)record.Element("Value") == pair.Value) != 1)
                throw new InvalidDataException("The exported region text is missing or duplicated: " + pair.Key);
    }
}
