using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private WorldProject? _celestialLibraryProject;
    private bool _createCelestialOpen;
    private readonly StarAppearanceDraft _newStarAppearance = new StarAppearanceDraft();
    private int _newCelestialKind;
    private string _celestialSearch = "";
    private string _newCelestialName = "";
    private string _newCelestialTemplate = "Earth";
    private string _newCelestialParent = "Sun";
    private string _newCelestialDistance = "1";
    private string _newCelestialPeriod = "365.25";
    private string _newCelestialEccentricity = "0";
    private string _newCelestialInclination = "0";
    private string _newCelestialRadius = "6371";
    private string _newCelestialRgb = "0.5,0.6,0.8";
    private string _newCelestialTexture = "";
    private string _newCelestialIrradiance = "1367";
    private bool _newCelestialKilometres;
    private bool _createCelestialPending;
    private string? _celestialLibraryError;

    /// <summary>Scans installed mod roots once per loaded project rather than during every GUI pass.</summary>
    private void EnsureInstalledCelestialLibrary()
    {
        if (_celestialLibraryProject == _project) return;
        _project!.LoadCelestialModRoots(DiscoverModRoots().Select(root => root.Path));
        _celestialLibraryProject = _project;
        _newCelestialParent = _project.PrimaryBodyId;
        _celestialLibraryError = null;
    }

    /// <summary>Offers one compact entry point for making a new orbital body or central light reference.</summary>
    private void DrawCelestialLibrary()
    {
        EnsureInstalledCelestialLibrary();
        GUILayout.Space(CardSpacing);
        GUILayout.BeginVertical(_cardStyle!);
        GUILayout.Label("CREATE CELESTIAL BODY", _sectionStyle!);
        GUILayout.Label("Parent and tidal-lock selectors include installed game and mod bodies. Selecting a central body changes the system root and reference sunlight; review the orbit and sun prefab.", _mutedStyle!);
        if (NativeButton(_createCelestialOpen ? "Close creation form" : "+ Create celestial body", _buttonStyle!, GUILayout.Height(32), GUILayout.Width(225)))
            _createCelestialOpen = !_createCelestialOpen;
        if (_createCelestialOpen) DrawNewCelestialInputs();
        if (_celestialLibraryError != null) GUILayout.Label(_celestialLibraryError, _mutedStyle!);
        GUILayout.EndVertical();
    }

    /// <summary>Collects a name and native orbital values without exposing XML or prefab-building requirements.</summary>
    private void DrawNewCelestialInputs()
    {
        GUILayout.Space(10);
        _newCelestialKind = NativeDropdown("new-celestial-kind", _newCelestialKind, new[] { "Planet / moon", "Primary star / central body" }, _buttonStyle!, GUILayout.Height(32), GUILayout.ExpandWidth(true));
        GUILayout.Space(10);
        HelpLabel(new GUIContent("Name  ⓘ", "A unique name using letters, digits, spaces or underscores. Installed bodies are never overwritten."), _mutedStyle!);
        _newCelestialName = GUILayout.TextField(_newCelestialName, _inputStyle!, GUILayout.Height(30));
        GUILayout.Space(10);
        if (_newCelestialKind == 0) DrawNewOrbitingBodyInputs();
        else
        {
            _newCelestialIrradiance = DrawRotationNumber("Reference sunlight at 1 AU (W/m²)", _newCelestialIrradiance, "Finite nonnegative irradiance. Appearance uses the world's Sun prefab selected on Overview.");
            DrawStarAppearanceInputs(_newStarAppearance, "new-star-texture");
        }
        GUILayout.Space(12);
        if (NativeButton("Create body", _primaryButtonStyle!, GUILayout.Width(180), GUILayout.Height(32))) _createCelestialPending = true;
    }

    /// <summary>Filters appearance templates while keeping the new body's orbit and parent explicit.</summary>
    private void DrawNewOrbitingBodyInputs()
    {
        GUILayout.Label("Find an appearance template", _mutedStyle!);
        _celestialSearch = GUILayout.TextField(_celestialSearch, _inputStyle!, GUILayout.Height(30));
        GUILayout.Space(8);
        var ids = _project!.CelestialTemplateIds().Where(id => id.IndexOf(_celestialSearch, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        if (ids.Length == 0) GUILayout.Label("No matching templates.", _mutedStyle!);
        else
        {
            var choice = NativeDropdown("new-celestial-template", Math.Max(0, Array.IndexOf(ids, _newCelestialTemplate)), ids.Select(_project.CelestialBodyLabel).ToArray(), _buttonStyle!, GUILayout.Height(32), GUILayout.ExpandWidth(true));
            if (choice >= 0 && choice < ids.Length) _newCelestialTemplate = ids[choice];
        }
        GUILayout.Space(10);
        GUILayout.Label("Orbits around", _mutedStyle!);
        var parents = _project.AvailableCelestialIds();
        var parent = NativeDropdown("new-celestial-parent", Math.Max(0, Array.IndexOf(parents, _newCelestialParent)), parents.Select(_project.CelestialBodyLabel).ToArray(), _buttonStyle!, GUILayout.Height(32), GUILayout.ExpandWidth(true));
        if (parent >= 0 && parent < parents.Length) _newCelestialParent = parents[parent];
        GUILayout.Space(10);
        var unit = NativeDropdown("new-celestial-unit", _newCelestialKilometres ? 1 : 0, new[] { "Distance in AU", "Distance in km" }, _buttonStyle!, GUILayout.Height(32), GUILayout.ExpandWidth(true));
        _newCelestialKilometres = unit == 1;
        _newCelestialDistance = DrawRotationNumber("Average orbital distance", _newCelestialDistance, "Positive distance in the selected unit. Changing units does not convert the typed number.");
        GUILayout.Space(8);
        _newCelestialPeriod = DrawRotationNumber("Orbital period (simulated days)", _newCelestialPeriod, "Positive finite time for one complete orbit.");
        GUILayout.Space(8);
        _newCelestialEccentricity = DrawRotationNumber("Eccentricity", _newCelestialEccentricity, "0 for a circle; less than 1 for an ellipse.");
        GUILayout.Space(8);
        _newCelestialInclination = DrawRotationNumber("Orbital inclination (°)", _newCelestialInclination, "-180 to 180 degrees.");
        GUILayout.Space(8);
        _newCelestialRadius = DrawRotationNumber("Body radius (km)", _newCelestialRadius, "Positive finite radius; the selected appearance is reused.");
        GUILayout.Space(8);
        GUILayout.Label("Preview RGB (0–1)", _mutedStyle!);
        _newCelestialRgb = GUILayout.TextField(_newCelestialRgb, _inputStyle!, GUILayout.Height(30));
        GUILayout.Space(8);
        GUILayout.Label("Surface texture from Asset Libraries", _mutedStyle!);
        _newCelestialTexture = DrawLibraryAssetDropdown("celestial-surface-asset", _newCelestialTexture, false, "Use template surface");
        GUILayout.Label("Import an original PNG on Asset Libraries. A 2:1 image wraps around a globe; selected textures are packaged on export.", _mutedStyle!);
        GUILayout.Label("Creates an additional body. Choose it as the playable world's parent when ready. Appearance uses existing textures; RGB colours the orbit preview.", _mutedStyle!);
    }

    /// <summary>Creates the body at Layout so its new fields and dropdown choices cannot interrupt the current UI pass.</summary>
    private void ApplyNewCelestialBody()
    {
        if (!_createCelestialPending) return;
        _createCelestialPending = false;
        if (_project == null) return;
        try
        {
            var id = _newCelestialKind == 1
                ? CreateStarFromDraft()
                : _project.CreateCelestialBody(_newCelestialName, _newCelestialTemplate, _newCelestialParent, ParseCelestialNumber(_newCelestialDistance), _newCelestialKilometres,
                    ParseCelestialNumber(_newCelestialPeriod), ParseCelestialNumber(_newCelestialEccentricity), ParseCelestialNumber(_newCelestialInclination), ParseCelestialNumber(_newCelestialRadius), _newCelestialRgb, _newCelestialTexture);
            _celestialLibraryError = null;
            if (_rotationDraftProject == _project) _rotationDraftKey = _project.GetRotationSettings().InputKey;
            _orbitSphereTexturesDirty = true;
            _status = "Created " + id + " in this draft. Installed definitions are unchanged.";
        }
        catch (Exception error) when (error is InvalidOperationException || error is FormatException || error is OverflowException || error is System.IO.IOException || error is UnauthorizedAccessException || error is ArgumentException)
        { _celestialLibraryError = _status = error.Message; }
    }

    /// <summary>Uses invariant XML number syntax for body creation inputs.</summary>
    private static double ParseCelestialNumber(string value) => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
}
