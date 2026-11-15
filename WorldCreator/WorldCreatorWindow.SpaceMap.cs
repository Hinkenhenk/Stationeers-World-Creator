using System;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    /// <summary>Selects a space-map definition while preserving custom identifiers from loaded worlds.</summary>
    private void DrawSpaceMapSettings()
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("SPACE MAP", _sectionStyle!);
        GUILayout.Label("Choose the existing rocket destination map for this world. This includes its travel connections and Low Orbit hub.", _mutedStyle!);
        var ids = _project!.SpaceMapIds;
        if (!ids.Contains(_project.SpaceMapReference.Value))
            ids = ids.Concat(new[] { _project.SpaceMapReference.Value }).ToArray();
        var selected = Array.IndexOf(ids, _project.SpaceMapReference.Value);
        var labels = ids.Select(_project.SpaceMapLabel).ToArray();
        HelpLabel(
            new GUIContent(
            "Rocket destination map  ⓘ",
            "References a SpaceMap definition from rocketlocations.xml or the loaded world's mod data. Choosing a map does not copy or edit its nodes. External mod maps require that mod to remain enabled."
        ),
            _mutedStyle!
        );
        var next = NativeDropdown("space-map-reference", Math.Max(0, selected), labels, _buttonStyle!, GUILayout.Width(Mathf.Min(420, _window.width - 240)));
        if (next >= 0 && next < ids.Length)
            _project.SpaceMapReference.Value = ids[next];
        GUILayout.Space(10);
        GUILayout.Label(_project.SpaceMapSummary, _mutedStyle!);
        GUILayout.EndVertical();
        GUILayout.Space(CardSpacing);
    }
}
