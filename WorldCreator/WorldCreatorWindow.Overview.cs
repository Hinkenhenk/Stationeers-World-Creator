using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private string[]? _sunPrefabNames;

    /// <summary>Offers original-world presets and requests confirmation before replacing project settings.</summary>
    private void DrawOriginalWorldCard()
    {
        GUILayout.BeginVertical(_cardStyle!);
        GUILayout.Label("START FROM AN ORIGINAL WORLD", _sectionStyle!);
        GUILayout.Label(
            "Use an installed vanilla world as a starting point for gravity, orbit, landing site and atmosphere. Weather and default starting gear are applied too.",
            _mutedStyle!
        );
        GUILayout.BeginHorizontal();
        var selectedPreset = NativeDropdown("original-world", _presetSelection - 1, Names.Skip(1).ToArray(), _templateActiveStyle!, GUILayout.Width(270), GUILayout.Height(36));
        if (selectedPreset >= 0)
            _presetSelection = selectedPreset + 1;
        if (NativeButton("Apply preset", _primaryButtonStyle!, GUILayout.Width(150), GUILayout.Height(36)))
        {
            if (_presetSelection > 0)
                _pendingTemplate = _presetSelection;
        }

        GUILayout.EndHorizontal();
        GUILayout.Label("Only the world settings and matching weather/start references are applied; maps and terrain assets are not copied.", _mutedStyle!);
        GUILayout.EndVertical();
    }

    /// <summary>Groups world-list, skybox, primary-body and clock settings into a presentation card.</summary>
    private void DrawWorldPresentationCard()
    {
        var fields = _project!.WorldFields.Where(field => field.Label.StartsWith("Overview/", StringComparison.Ordinal)).ToArray();
        if (fields.Length == 0)
            return;
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("WORLD PRESENTATION & CLOCK", _sectionStyle!);
        GUILayout.Label("Edit the world-list, lighting, skybox scale and starting celestial clock fields.", _mutedStyle!);
        DrawWorldFieldGroup(
            "World list",
            fields.Where(field => field.Label.EndsWith("World list priority", StringComparison.Ordinal) || field.Label.EndsWith("World visibility", StringComparison.Ordinal))
        );
        DrawWorldFieldGroup(
            "Primary body & presentation",
            fields.Where(field => field.Label.EndsWith("Primary body ID", StringComparison.Ordinal) || field.Label.EndsWith("Primary star irradiance (W/m²)", StringComparison.Ordinal) || field.Label.EndsWith("Ambience sound ID", StringComparison.Ordinal) || field.Label.EndsWith("Skybox material ID", StringComparison.Ordinal) || field.Label.EndsWith("Sun prefab ID", StringComparison.Ordinal))
        );
        DrawWorldFieldGroup(
            "Visual scale & celestial clock",
            fields.Where(field => field.Label.EndsWith("Celestial body scale", StringComparison.Ordinal) || field.Label.EndsWith("Skybox scale", StringComparison.Ordinal) || field.Label.Contains("Clock offset", StringComparison.Ordinal))
        );
        GUILayout.EndVertical();
    }

    /// <summary>Shows world-level environment settings separately from individual gas entries.</summary>
    private void DrawWorldEnvironmentCard()
    {
        var fields = _project!.WorldFields.Where(field => field.Label.StartsWith("Environment/", StringComparison.Ordinal) || field.Label.StartsWith("Gravity", StringComparison.Ordinal))
            .ToArray();
        if (fields.Length == 0)
            return;
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("WORLD CONDITIONS", _sectionStyle!);
        DrawWorldFieldRows(fields, "Environment/");
        GUILayout.EndVertical();
        GUILayout.Space(CardSpacing);
    }

    /// <summary>Finds and renders the world fields belonging to one presentation group.</summary>
    private void DrawWorldFieldGroup(string title, IEnumerable<WorldProject.Field> source)
    {
        var fields = source.ToArray();
        if (fields.Length == 0)
            return;
        GUILayout.Space(12);
        GUILayout.Label(title, _mutedStyle!);
        GUILayout.Space(6);
        DrawWorldFieldRows(fields, "Overview/");
    }

    /// <summary>Arranges related world fields into readable rows.</summary>
    private void DrawWorldFieldRows(IReadOnlyList<WorldProject.Field> fields, string labelPrefix)
    {
        for (var i = 0; i < fields.Count; i += 2)
        {
            GUILayout.BeginHorizontal();
            DrawWorldField(fields[i], labelPrefix);
            if (i + 1 < fields.Count)
            {
                GUILayout.Space(20);
                DrawWorldField(fields[i + 1], labelPrefix);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
        }
    }

    /// <summary>Draws the boolean or text control appropriate to one world setting.</summary>
    private void DrawWorldField(WorldProject.Field field, string labelPrefix)
    {
        GUILayout.BeginVertical(GUILayout.MinWidth(0), GUILayout.MaxWidth(340), GUILayout.ExpandWidth(true));
        var title = field.Label.StartsWith(labelPrefix, StringComparison.Ordinal) ? field.Label.Substring(labelPrefix.Length) : ReadableFieldLabel(field.Label, false);
        HelpLabel(new GUIContent(title + "  ⓘ", field.Tooltip), _mutedStyle!);
        GUILayout.Space(5);
        if (title == "Sun prefab ID")
        {
            DrawSunPrefabDropdown(field);
            GUILayout.EndVertical();
            return;
        }
        var rect = GUILayoutUtility.GetRect(80, 28, GUILayout.ExpandWidth(true), GUILayout.Height(28));
        RegisterHoverTooltip(rect, field.Tooltip);
        if (field.IsBoolean)
            field.Value = GUI.Toggle(rect, bool.TryParse(field.Value, out var current) && current, new GUIContent("", field.Tooltip)) ? "true" : "false";
        else
        {
            var value = GUI.TextField(rect, field.Value, _inputStyle!);
            field.Value = field.IsNumeric ? MaskNumericInput(value) : value;
        }

        GUILayout.EndVertical();
    }

    /// <summary>Lists installed sun resources with the root light component required by the world loader.</summary>
    private void DrawSunPrefabDropdown(WorldProject.Field field)
    {
        _sunPrefabNames ??= Resources.LoadAll<GameObject>("WorldEnvironment/Suns")
            .Where(prefab => prefab.GetComponent<Light>() != null)
            .Select(prefab => prefab.name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var names = _sunPrefabNames;
        var selected = Array.IndexOf(names, field.Value);
        if (selected < 0)
        {
            names = new[] { field.Value }.Concat(names).ToArray();
            selected = 0;
        }
        var labels = names.Select(name => string.IsNullOrWhiteSpace(name) ? "Not set" : name).ToArray();
        var choice = NativeDropdown("sun-prefab", selected, labels, _templateStyle!, GUILayout.Height(32), GUILayout.ExpandWidth(true));
        if (choice >= 0)
            field.Value = names[choice];
    }

    /// <summary>Shows the current project's orbit, atmosphere, start-profile and asset counts.</summary>
    private void DrawOverviewSummary()
    {
        GUILayout.BeginHorizontal();
        DrawOverviewTile(
            "ORBIT PERIOD",
            _project!.OrbitFields.FirstOrDefault(f => f.Label.EndsWith(" / Period", StringComparison.Ordinal))?.Value ?? "—",
            "Configured around the primary body"
        );
        GUILayout.Space(CardSpacing);
        DrawOverviewTile(
            "ATMOSPHERE GASES",
            _project.AtmosphereFields.Count(f => f.Label.Contains("/Gas/Gas") && f.Label.EndsWith(" / Quantity", StringComparison.Ordinal) && f.Value != "0").ToString(CultureInfo.InvariantCulture),
            "Gas types with non-zero amount"
        );
        GUILayout.Space(CardSpacing);
        DrawOverviewTile("START OPTIONS", _project.StartConditions.Count.ToString(CultureInfo.InvariantCulture), "Starting conditions");
        GUILayout.Space(CardSpacing);
        DrawOverviewTile("LIBRARY ASSETS", _project.ProjectAssets.Count.ToString(CultureInfo.InvariantCulture), "Texture and terrain references");
        GUILayout.EndHorizontal();
    }

    /// <summary>Draws one compact project-summary value and its explanation.</summary>
    private void DrawOverviewTile(string title, string value, string note)
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true), GUILayout.MinHeight(76));
        GUILayout.Label(title, _mutedStyle!);
        GUILayout.Label(value, _titleStyle!);
        GUILayout.Label(note, _mutedStyle!);
        GUILayout.EndVertical();
    }

    /// <summary>Provides navigation from the overview to the relevant editing pages.</summary>
    private void DrawOverviewShortcuts()
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("CONTINUE BUILDING", _sectionStyle!);
        GUILayout.Label("Open a section to edit its settings.", _mutedStyle!);
        for (var page = 1; page < Pages.Length; page++)
        {
            if (page == 6)
                continue;
            if (NativeButton(Pages[page], _navStyle!, GUILayout.Height(34)))
                _pendingPage = page;
            if (page + 1 < Pages.Length)
                GUILayout.Space(10);
        }

        GUILayout.EndVertical();
    }

    /// <summary>Edits the world's display name, identifier, description and gravity in one card.</summary>
    private void DrawWorldIdentityCard()
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("WORLD IDENTITY", _sectionStyle!);
        GUILayout.Label("Basic information used by the world menu and generated mod.", _mutedStyle!);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.MinWidth(0), GUILayout.MaxWidth(300), GUILayout.ExpandWidth(true));
        HelpLabel(new GUIContent("Display name  ⓘ", "Player-facing name shown in the Stationeers world selection menu."), _mutedStyle!);
        GUILayout.Space(5);
        _name = TooltipTextField(_name, "Player-facing name shown in the Stationeers world selection menu.", 0, 30);
        GUILayout.EndVertical();
        GUILayout.Space(20);
        GUILayout.BeginVertical(GUILayout.MinWidth(0), GUILayout.MaxWidth(300), GUILayout.ExpandWidth(true));
        HelpLabel(
            new GUIContent(
            "World ID  ⓘ",
            "Unique XML identifier for the world mod: 1–47 letters, numbers or underscores. No prefix is required; WC_ is optional. Avoid IDs used by other installed worlds."
        ),
            _mutedStyle!
        );
        GUILayout.Space(5);
        _id = TooltipTextField(
            _id,
            "Unique XML identifier for the world mod: 1–47 letters, numbers or underscores. No prefix is required; WC_ is optional. Avoid IDs used by other installed worlds.",
            0,
            30,
            47
        );
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(14);
        var descriptionHelp = "Description shown for this world. It is saved into the generated world.xml.";
        HelpLabel(new GUIContent("Description  ⓘ", descriptionHelp), _mutedStyle!);
        RegisterHoverTooltip(GUILayoutUtility.GetLastRect(), descriptionHelp);
        GUILayout.Space(5);
        _project!.SetWorldDescription(GUILayout.TextArea(_project.WorldDescription, _multilineStyle!, GUILayout.Height(76), GUILayout.ExpandWidth(true)));
        var gravity = _project!.WorldFields.FirstOrDefault(field => field.Label.StartsWith("Gravity", StringComparison.Ordinal));
        if (gravity != null)
        {
            GUILayout.Space(14);
            DrawWorldFieldRows(new[] { gravity }, "");
        }

        GUILayout.EndVertical();
    }
}
