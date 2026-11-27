using System;
using System.Globalization;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private readonly StarAppearanceDraft _currentStarAppearance = new StarAppearanceDraft();
    private WorldProject? _starAppearanceProject;
    private string? _starAppearancePrimary;
    private string? _starAppearanceTexture;
    private bool _starAppearancePending;

    private sealed class StarAppearanceDraft
    {
        internal string Texture = "";
        internal string Diameter = "4";
        internal string Glow = "1";
        internal string LightRgb = "1,1,1";
    }

    /// <summary>Draws the creation tools independently of their world assignment controls.</summary>
    private void DrawAssetCreator()
    {
        DrawCelestialLibrary();
        DrawCurrentStarAppearance();
        DrawSpaceMapCreator();
    }

    /// <summary>Edits the active star appearance and keeps the already packaged texture selectable.</summary>
    private void DrawCurrentStarAppearance()
    {
        var star = _project!.CustomStar;
        if (_starAppearanceProject != _project || _starAppearancePrimary != _project.PrimaryBodyId
            || _starAppearanceTexture != star?.TexturePath)
        {
            _starAppearanceProject = _project;
            _starAppearancePrimary = _project.PrimaryBodyId;
            _starAppearanceTexture = star?.TexturePath;
            _currentStarAppearance.Texture = star?.TexturePath ?? "";
            _currentStarAppearance.Diameter = (star?.Diameter ?? 4f).ToString(CultureInfo.InvariantCulture);
            _currentStarAppearance.Glow = (star?.Glow ?? 1f).ToString(CultureInfo.InvariantCulture);
            _currentStarAppearance.LightRgb = star?.LightRgb ?? "1,1,1";
        }

        GUILayout.Space(CardSpacing);
        GUILayout.BeginVertical(_cardStyle!);
        GUILayout.Label("PRIMARY STAR APPEARANCE", _sectionStyle!);
        GUILayout.Label("Current primary body: " + _project.PrimaryBodyId, _mutedStyle!);
        DrawStarAppearanceInputs(_currentStarAppearance, "current-star-texture", star?.TexturePath);
        if (NativeButton("Apply star appearance", _primaryButtonStyle!, GUILayout.Height(32)))
            _starAppearancePending = true;
        GUILayout.EndVertical();
    }

    /// <summary>Uses compatible library textures and displays the settings used by the runtime.</summary>
    private void DrawStarAppearanceInputs(StarAppearanceDraft draft, string dropdownId, string? retainedPath = null)
    {
        GUILayout.Space(8);
        GUILayout.Label("Surface texture", _mutedStyle!);
        draft.Texture = DrawLibraryAssetDropdown(dropdownId, draft.Texture, false, "Use stock sun", retainedPath);
        if (string.IsNullOrEmpty(draft.Texture)) return;

        GUILayout.Label("Worlds using a custom texture require World Creator Runtime.", _mutedStyle!);
        draft.Diameter = DrawRotationNumber("Visual diameter", draft.Diameter, "0.1–30 Unity units at the standard sky distance.");
        draft.Glow = DrawRotationNumber("Glow", draft.Glow, "Emission multiplier from 0 to 8.");
        GUILayout.Label("Light RGB (0–1)", _mutedStyle!);
        draft.LightRgb = GUILayout.TextField(draft.LightRgb, _inputStyle!, GUILayout.Height(30));
    }

    /// <summary>Creates the star with its optional appearance in one model operation.</summary>
    private string CreateStarFromDraft()
    {
        var name = _newCelestialName;
        var irradiance = ParseCelestialNumber(_newCelestialIrradiance);
        var appearance = _newStarAppearance;
        if (string.IsNullOrEmpty(appearance.Texture)) return _project!.CreateCentralCelestial(name, irradiance);
        return _project!.CreateCentralCelestial(name, irradiance, appearance.Texture,
            (float)ParseCelestialNumber(appearance.Diameter), (float)ParseCelestialNumber(appearance.Glow), appearance.LightRgb);
    }

    /// <summary>Applies the edit at Layout and restores the stock appearance if requested.</summary>
    private void ApplyStarAppearance()
    {
        if (!_starAppearancePending) return;
        _starAppearancePending = false;
        if (_project == null || _project != _starAppearanceProject || _project.PrimaryBodyId != _starAppearancePrimary) return;
        try
        {
            var draft = _currentStarAppearance;
            if (string.IsNullOrEmpty(draft.Texture))
                _project.ClearCustomStar();
            else
                _project.SetCustomStar(draft.Texture, (float)ParseCelestialNumber(draft.Diameter),
                    (float)ParseCelestialNumber(draft.Glow), draft.LightRgb);
            _orbitSphereTexturesDirty = true;
            _status = "Applied star appearance.";
        }
        catch (Exception error) when (error is InvalidOperationException || error is ArgumentException
            || error is FormatException || error is OverflowException || error is System.IO.IOException || error is UnauthorizedAccessException)
        {
            _status = error.Message;
        }
    }
}
