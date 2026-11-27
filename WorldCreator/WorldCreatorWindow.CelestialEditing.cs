using System;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private WorldProject? _orbitParentDraftProject;
    private string? _orbitParentPending;

    /// <summary>Selects the shared orbit parent and tidal-lock target from bodies available in this world.</summary>
    private void DrawOrbitParentSelector(bool tidalTarget = false)
    {
        EnsureInstalledCelestialLibrary();
        var current = _project!.GetRotationSettings().ParentId;
        var ids = _project.GetOrbitParentIds();
        var selected = Array.IndexOf(ids, current);
        if (selected < 0) { ids = new[] { current }.Concat(ids).ToArray(); selected = 0; }
        GUILayout.BeginVertical(_rowStyle!);
        HelpLabel(new GUIContent(tidalTarget ? "Tidal-lock target (also orbit parent)  ⓘ" : "Parent body / tidal-lock target  ⓘ",
            "Select any available game or installed-mod body. Missing ancestors are imported. Choosing a central body such as Janus replaces the system root. Tidal locking uses the same parent; review orbital distance, period and sunlight."), _mutedStyle!);
        var choice = NativeDropdown(tidalTarget ? "tidal-lock-target" : "orbit-parent", selected, ids.Select(_project.CelestialBodyLabel).ToArray(), _buttonStyle!, GUILayout.Height(32), GUILayout.ExpandWidth(true));
        if (choice >= 0 && choice < ids.Length && ids[choice] != current)
        {
            _orbitParentDraftProject = _project;
            _orbitParentPending = ids[choice];
        }
        GUILayout.Label("Changing parent also changes the orbit. Review distance and period.", _mutedStyle!);
        GUILayout.EndVertical();
    }

    /// <summary>Changes the parent at a Layout boundary so rebuilding Advanced fields cannot interrupt drawing.</summary>
    private void ApplyOrbitParentDraft()
    {
        if (_orbitParentPending == null) return;
        var parent = _orbitParentPending;
        _orbitParentPending = null;
        if (_project != _orbitParentDraftProject) return;
        try
        {
            _project!.SetOrbitParent(parent);
            // Keep unapplied rotation inputs when choosing their target.
            if (_rotationDraftProject == _project) _rotationDraftKey = _project.GetRotationSettings().InputKey;
            _orbitSphereTexturesDirty = true;
            _status = "Orbit parent set to " + parent + ". Review orbital distance and period. The source world is unchanged.";
        }
        catch (InvalidOperationException error) { _status = error.Message; }
    }

    /// <summary>Edits one comma-separated normalized colour with an aligned swatch and an inline validation hint.</summary>
    private void DrawCelestialColourInput(WorldProject.Field field)
    {
        GUILayout.BeginHorizontal();
        field.Value = GUILayout.TextField(field.Value, _inputStyle!, GUILayout.Height(32), GUILayout.ExpandWidth(true));
        GUILayout.Space(10);
        var valid = WorldProject.TryParseCelestialColour(field.Value, out var colour);
        DrawMapColourCube(valid ? new Color(colour.R, colour.G, colour.B) : Color.gray);
        GUILayout.EndHorizontal();
        if (!valid) GUILayout.Label("Enter three values from 0 to 1: red,green,blue.", _mutedStyle!);
    }
}
