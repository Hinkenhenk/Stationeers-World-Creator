using System;
using System.Globalization;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private WorldProject? _rotationDraftProject;
    private string? _rotationDraftKey;
    private int _rotationMode;
    private string _rotationValue = "1";
    private string _rotationTilt = "0";
    private string _rotationDirection = "0";
    private string? _rotationError;
    private bool _applyRotationPending;
    private WorldProject.RotationSettings? _orbitFrameRotation;

    /// <summary>Edits the native rotation model and axis in degrees without changing a loaded world until Apply.</summary>
    private void DrawRotationSettings()
    {
        GUILayout.Space(CardSpacing);
        GUILayout.BeginVertical(_cardStyle!);
        GUILayout.Label("ROTATION & AXIS", _sectionStyle!);
        if (!_project!.CanEditRotation)
            GUILayout.Label("This world has no editable celestial-body definition. Choose a world with orbital data first.", _mutedStyle!);
        else
            DrawRotationInputs(_project.GetRotationSettings());
        GUILayout.EndVertical();
    }

    /// <summary>Keeps a stable draft while typing and reloads it when the project or Advanced rotation values change.</summary>
    private void RefreshRotationDraft(WorldProject.RotationSettings settings)
    {
        if (_rotationDraftProject == _project && _rotationDraftKey == settings.InputKey) return;
        _rotationDraftProject = _project;
        _rotationDraftKey = settings.InputKey;
        _rotationMode = (int)settings.Mode;
        _rotationValue = settings.Value.ToString("G9", CultureInfo.InvariantCulture);
        _rotationError = null;
        try
        {
            var angles = WorldProject.RotationAnglesFromAxis(settings.X, settings.Y, settings.Z);
            _rotationTilt = angles.Tilt.ToString("G9", CultureInfo.InvariantCulture);
            _rotationDirection = angles.Direction.ToString("G9", CultureInfo.InvariantCulture);
        }
        catch (InvalidOperationException error)
        {
            _rotationError = error.Message;
            _rotationTilt = _rotationDirection = "0";
        }
    }

    /// <summary>Displays rotation units, parent locking and an axis direction distinct from orbital inclination.</summary>
    private void DrawRotationInputs(WorldProject.RotationSettings settings)
    {
        RefreshRotationDraft(settings);
        GUILayout.Label("Rotation mode", _mutedStyle!);
        var selected = NativeDropdown("rotation-mode", _rotationMode,
            new[] { "Rotation period", "Rotation speed", "Tidally locked to parent" }, _buttonStyle!, GUILayout.Height(32), GUILayout.ExpandWidth(true));
        if (selected >= 0 && selected != _rotationMode) ChangeRotationMode(selected, settings);
        GUILayout.Space(10);
        var tidal = _rotationMode == (int)WorldProject.RotationMode.TidalLocking;
        if (tidal)
        {
            DrawOrbitParentSelector(tidalTarget: true);
            GUILayout.Space(10);
        }
        var previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && !tidal;
        var value = DrawRotationNumber(tidal ? "Orbit around parent (simulated days)" : _rotationMode == 0 ? "Rotation period (simulated days)" : "Rotation speed (° / simulated day)",
            tidal ? settings.OrbitPeriod.ToString("G9", CultureInfo.InvariantCulture) : _rotationValue,
            "A nonzero signed value. Negative values rotate in the opposite direction. This is separate from the orbital period.");
        if (!tidal) _rotationValue = value;
        GUI.enabled = previousEnabled;
        GUILayout.Label(tidal ? "Locked to: " + settings.ParentId + ". A moon locked to its planet can still have day and night."
            : "Negative periods or speeds reverse the rotation.", _mutedStyle!);
        GUILayout.Space(10);
        _rotationTilt = DrawRotationNumber("Axis tilt from reference Y (°)", _rotationTilt,
            "0–180°. Direction of the spin axis relative to the reference plane's upward Y axis. The angle to this body's own orbital plane is shown below.");
        GUILayout.Space(8);
        _rotationDirection = DrawRotationNumber("Axis direction (°)", _rotationDirection,
            "−180–180°. Turns the tilted axis around reference Y: 0 points towards +X and 90 towards +Z. At zero tilt this direction has no effect.");
        GUILayout.Space(10);
        DrawRotationAxisSummary(settings);
        GUILayout.Space(10);
        if (NativeButton("Apply rotation", _primaryButtonStyle!, GUILayout.Width(180), GUILayout.Height(32))) _applyRotationPending = true;
        GUILayout.Label("Apply updates this draft. Saving over a local world still requires the Update confirmation.", _mutedStyle!);
        if (_rotationError != null) GUILayout.Label(_rotationError, _mutedStyle!);
        if (tidal && _project!.IsStarPrimary(settings.ParentId))
            GUILayout.Label("Direct star locking needs a test world: the game's day counter and day-based weather may behave differently.", _mutedStyle!);
    }

    /// <summary>Converts the current spin rate when switching native duration and speed inputs.</summary>
    private void ChangeRotationMode(int selected, WorldProject.RotationSettings settings)
    {
        var value = double.TryParse(_rotationValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : 1;
        var speed = _rotationMode == 2 ? 360 / settings.OrbitPeriod : _rotationMode == 0 ? 360 / value : value;
        _rotationMode = selected;
        _rotationValue = (selected == 2 ? settings.OrbitPeriod : selected == 0 ? 360 / speed : speed).ToString("G9", CultureInfo.InvariantCulture);
    }

    /// <summary>Renders one labelled numeric input using the existing native appearance and hover help.</summary>
    private string DrawRotationNumber(string label, string value, string tooltip)
    {
        HelpLabel(new GUIContent(label + "  ⓘ", tooltip), _mutedStyle!);
        return MaskNumericInput(GUILayout.TextField(value, _inputStyle!, GUILayout.Height(30), GUILayout.ExpandWidth(true)));
    }

    /// <summary>Reports the applied native vector and its actual angle to the orbital normal.</summary>
    private void DrawRotationAxisSummary(WorldProject.RotationSettings settings)
    {
        GUILayout.Label("Applied axis XYZ: " + settings.X.ToString("0.####", CultureInfo.InvariantCulture) + ", "
            + settings.Y.ToString("0.####", CultureInfo.InvariantCulture) + ", " + settings.Z.ToString("0.####", CultureInfo.InvariantCulture), _mutedStyle!);
        try
        {
            var angle = WorldProject.RotationObliquity(settings.X, settings.Y, settings.Z,
                OrbitAngle("Inclination") * 180 / Math.PI, OrbitAngle("LongitudeOfAscendingNode") * 180 / Math.PI);
            GUILayout.Label("Tilt to orbital plane normal: " + angle.ToString("0.##", CultureInfo.InvariantCulture) + "°", _mutedStyle!);
        }
        catch (InvalidOperationException) { GUILayout.Label("The applied axis is invalid; enter a valid tilt and Apply to repair it.", _mutedStyle!); }
    }

    /// <summary>Applies validated rotation only at the next Layout boundary to avoid rebuilding controls mid-frame.</summary>
    private void ApplyRotationDraft()
    {
        ApplyStarAppearance();
        ApplyNewCelestialBody();
        ApplyOrbitParentDraft();
        if (!_applyRotationPending) return;
        _applyRotationPending = false;
        try
        {
            var tilt = double.Parse(_rotationTilt, NumberStyles.Float, CultureInfo.InvariantCulture);
            var direction = double.Parse(_rotationDirection, NumberStyles.Float, CultureInfo.InvariantCulture);
            var value = _rotationMode == 2 ? 0 : double.Parse(_rotationValue, NumberStyles.Float, CultureInfo.InvariantCulture);
            var axis = WorldProject.RotationAxisFromAngles(tilt, direction);
            _project!.SetRotationSettings((WorldProject.RotationMode)_rotationMode, axis.X, axis.Y, axis.Z, value);
            _rotationDraftKey = null;
            _rotationError = null;
            _orbitSphereTexturesDirty = true;
            _status = "Rotation applied to this draft. The source world is unchanged.";
        }
        catch (Exception error) when (error is FormatException || error is OverflowException || error is InvalidOperationException)
        {
            _rotationError = _status = error is InvalidOperationException ? error.Message : "Enter valid numbers for rotation, axis tilt and axis direction.";
        }
    }

    /// <summary>Orients the preview's north pole and applies the native mean-phase spin convention in inertial space.</summary>
    private Quaternion BodyRotationAtPhase(float phase)
    {
        var settings = _orbitFrameRotation ?? _project!.GetRotationSettings();
        var spin = settings.RotationAngleAtMeanAnomaly(phase);
        var normalized = settings.NormalizedAxis;
        var axis = new Vector3((float)normalized.X, (float)normalized.Y, (float)normalized.Z);
        return Quaternion.AngleAxis((float)-spin, axis) * Quaternion.FromToRotation(Vector3.up, axis);
    }

    /// <summary>Evaluates the native landing-site sun angle without registering a simulation or loading a running world.</summary>
    private float LandingSiteSunAngle(float phase, WorldProject.RotationSettings settings)
    {
        var light = PrimaryLightDirectionAtPhase(phase);
        return (float)WorldProject.CalculateRotationLighting(settings, phase, WorldAngle("Latitude"), WorldAngle("Longitude"), light.x, light.y, light.z).SolarAngle;
    }
}
