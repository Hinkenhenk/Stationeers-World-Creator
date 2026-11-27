using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private sealed class SpaceMapDestinationDraft
    {
        public string Name = "";
        public string X = "100";
        public string Y = "0";
        public int Parent;
        public string Difficulty = "100";
    }

    private WorldProject? _spaceMapEditorProject;
    private string _spaceMapEntry = "Low Orbit";
    private string _spaceMapDistance = "400";
    private readonly List<SpaceMapDestinationDraft> _spaceMapDestinations = new List<SpaceMapDestinationDraft>();

    /// <summary>Draws the destination tree editor with safe parent choices for each row.</summary>
    private void DrawSpaceMapCreator()
    {
        if (_project == null) return;
        if (_spaceMapEditorProject != _project)
        {
            _spaceMapEditorProject = _project;
            LoadSpaceMapCreator(_project.ReadAuthoredSpaceMap());
        }

        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("CREATE SPACE MAP", _sectionStyle!);
        GUILayout.Label("Add rocket destinations. Each route starts at the entry or an earlier destination.", _mutedStyle!);
        GUILayout.Label("Entry name");
        _spaceMapEntry = GUILayout.TextField(_spaceMapEntry, 80, _inputStyle!);
        GUILayout.Label("Orbit distance");
        _spaceMapDistance = GUILayout.TextField(_spaceMapDistance, _inputStyle!);
        for (var i = 0; i < _spaceMapDestinations.Count; i++)
            DrawSpaceMapDestination(i);
        if (_spaceMapDestinations.Count < 64 && NativeButton("Add destination", _buttonStyle!, GUILayout.Width(190)))
            AddSpaceMapDestination();
        if (_spaceMapDestinations.Count > 1 && NativeButton("Remove last destination", _buttonStyle!, GUILayout.Width(225)))
            _spaceMapDestinations.RemoveAt(_spaceMapDestinations.Count - 1);
        if (NativeButton("Apply map to this world", _primaryButtonStyle!, GUILayout.Width(235)))
            ApplySpaceMapCreator();
        GUILayout.EndVertical();
    }

    /// <summary>Draws editable destination name, connection, position and difficulty.</summary>
    private void DrawSpaceMapDestination(int index)
    {
        var draft = _spaceMapDestinations[index];
        GUILayout.Space(8);
        GUILayout.Label("Destination " + (index + 1), _sectionStyle!);
        draft.Name = GUILayout.TextField(draft.Name, 80, _inputStyle!);
        var parents = new[] { "Entry: " + _spaceMapEntry }
            .Concat(_spaceMapDestinations.Take(index).Select((node, i) => (i + 1) + " · " + node.Name)).ToArray();
        GUILayout.Label("Route from");
        draft.Parent = NativeDropdown("space-map-parent-" + index, draft.Parent, parents, _buttonStyle!);
        GUILayout.BeginHorizontal();
        draft.X = DrawSpaceMapNumber("X", draft.X);
        draft.Y = DrawSpaceMapNumber("Y", draft.Y);
        draft.Difficulty = DrawSpaceMapNumber("Travel difficulty", draft.Difficulty);
        GUILayout.EndHorizontal();
    }

    /// <summary>Renders one compact map number field.</summary>
    private string DrawSpaceMapNumber(string title, string value)
    {
        GUILayout.BeginVertical();
        GUILayout.Label(title);
        value = GUILayout.TextField(value, _inputStyle!);
        GUILayout.EndVertical();
        return value;
    }

    /// <summary>Adds a default destination with positions staggered across the map.</summary>
    private void AddSpaceMapDestination()
    {
        var index = _spaceMapDestinations.Count + 1;
        _spaceMapDestinations.Add(new SpaceMapDestinationDraft
        {
            Name = "Destination " + index,
            X = (index * 100).ToString(CultureInfo.InvariantCulture)
        });
    }

    /// <summary>Reopens the saved tree for editing, or starts a blank map.</summary>
    private void LoadSpaceMapCreator(XElement? map)
    {
        _spaceMapDestinations.Clear();
        var entry = map?.Element("Entry");
        _spaceMapEntry = (string?)entry?.Element("Name")?.Attribute("Value") ?? "Low Orbit";
        _spaceMapDistance = (string?)map?.Attribute("OrbitDistance") ?? "400";
        if (map == null || entry == null)
        {
            AddSpaceMapDestination();
            return;
        }

        var nodes = new[] { entry }.Concat(map.Elements("Node")).ToArray();
        foreach (var node in nodes.Skip(1))
        {
            var id = (string?)node.Attribute("Id");
            var parent = Array.FindIndex(nodes, candidate => candidate.Elements("Connection").Any(route => (string?)route.Attribute("Id") == id));
            var route = parent >= 0 ? nodes[parent].Elements("Connection").First(link => (string?)link.Attribute("Id") == id) : null;
            _spaceMapDestinations.Add(new SpaceMapDestinationDraft
            {
                Name = (string?)node.Element("Name")?.Attribute("Value") ?? "Destination",
                X = (string?)node.Element("MapDisplay")?.Attribute("X") ?? "0",
                Y = (string?)node.Element("MapDisplay")?.Attribute("Y") ?? "0",
                Parent = Math.Max(0, parent),
                Difficulty = (string?)route?.Attribute("Difficulty") ?? "100"
            });
        }
    }

    /// <summary>Checks numbers and applies the current draft without changing any installed map.</summary>
    private void ApplySpaceMapCreator()
    {
        try
        {
            var culture = CultureInfo.InvariantCulture;
            var destinations = _spaceMapDestinations.Select(node => (node.Name,
                float.Parse(node.X, NumberStyles.Float, culture), float.Parse(node.Y, NumberStyles.Float, culture),
                node.Parent, int.Parse(node.Difficulty, culture))).ToArray();
            _project!.SaveAuthoredSpaceMap(_spaceMapEntry, float.Parse(_spaceMapDistance, NumberStyles.Float, culture), destinations);
            _status = "Custom space map selected for this world.";
        }
        catch (Exception error) when (error is FormatException || error is OverflowException || error is InvalidOperationException)
        {
            _status = error.Message;
        }
    }
}
