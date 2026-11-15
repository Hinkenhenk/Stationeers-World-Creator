using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    /// <summary>Arranges the editor shell, scrollable page content, footer and fixed navigation sidebar.</summary>
    private void DrawEditorPage()
    {
        DrawEditorHeader();
        GUILayout.BeginHorizontal();
        GUILayout.Space(SidebarWidth + CardSpacing);
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        var contentRightInset = 10f + ScrollContentRightPadding + GUI.skin.verticalScrollbar.fixedWidth;
        GUILayout.BeginHorizontal();
        GUILayout.Space(10f);
        DrawTopbar();
        GUILayout.Space(contentRightInset);
        GUILayout.EndHorizontal();
        GUILayout.Space(CardSpacing);
        _scroll.x = 0;
        _scroll = GUILayout.BeginScrollView(_scroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar,
            GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        GUILayout.BeginHorizontal();
        GUILayout.Space(10f);
        GUILayout.BeginVertical(GUILayout.Width(PageContentWidth), GUILayout.ExpandHeight(true));
        DrawSelectedPageContent();
        GUILayout.EndVertical();
        GUILayout.Space(10f);
        GUILayout.Space(ScrollContentRightPadding);
        GUILayout.EndHorizontal();
        GUILayout.EndScrollView();
        GUILayout.Space(CardSpacing);
        DrawEditorFooter(contentRightInset);
        GUILayout.EndVertical();
        GUILayout.Space(30f);
        GUILayout.EndHorizontal();
        DrawSidebar();
    }

    /// <summary>Draws the fixed page navigation and queues page changes for the next layout pass.</summary>
    private void DrawSidebar()
    {
        var top = 82f + Mathf.Max(0, (NativeMenuButtonAppearance.Back?.Size.y ?? 64f) - 34f);
        var height = Mathf.Max(160f, _window.height - top - 62f);
        GUI.BeginGroup(new Rect(12f, top, SidebarWidth, height));
        GUI.Box(new Rect(0f, 0f, SidebarWidth, height), GUIContent.none, _sidebarStyle!);
        const float left = 14f;
        const float width = SidebarWidth - 28f;
        var y = 16f;
        GUI.Label(new Rect(left, y, width, 22f), "STATIONEERS", _sectionStyle!);
        y += 24f;
        GUI.Label(new Rect(left, y, width, 22f), "WORLD CREATOR", _mutedStyle!);
        y += 42f;
        GUI.Label(new Rect(left, y, width, 22f), "NAVIGATION", _mutedStyle!);
        y += 22f;
        foreach (var i in Enumerable.Range(0, Pages.Length).Where(page => page != 6).Concat(new[] { 6 }))
        {
            var buttonRect = new Rect(left, y, width, 34f);
            if (NativeButtonAt(buttonRect, Pages[i], _navStyle!))
                _pendingPage = i;
            if (i == _page && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(buttonRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, Accent, 2, 5);
            y += 44f;
        }

        GUI.EndGroup();
    }

    /// <summary>Edits the shared world name and identifier above the current page.</summary>
    private void DrawTopbar()
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(160));
        GUILayout.Label("WORLD PROJECT", _sectionStyle!);
        GUILayout.Label(_projectOrigin, _mutedStyle!);
        GUILayout.EndVertical();
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        HelpLabel(new GUIContent("Display name  ⓘ", "Player-facing name shown in the Stationeers world selection menu."), _mutedStyle!);
        _name = TooltipTextField(_name, "Player-facing name shown in the Stationeers world selection menu.", 0, 30);
        GUILayout.EndVertical();
        GUILayout.BeginVertical(GUILayout.Width(240));
        HelpLabel(
            new GUIContent(
            "World ID  ⓘ",
            "Unique XML identifier for the world mod: 1–47 letters, numbers or underscores. No prefix is required; WC_ is optional. Avoid IDs used by other installed worlds."
        ),
            _mutedStyle!
        );
        _id = TooltipTextField(
            _id,
            "Unique XML identifier for the world mod: 1–47 letters, numbers or underscores. No prefix is required; WC_ is optional. Avoid IDs used by other installed worlds.",
            0,
            30,
            47
        );
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    /// <summary>Selects the editable XML field collection belonging to the current page.</summary>
    private List<WorldProject.Field> Fields()
    {
        switch (_page)
        {
            case 1:
                return _project!.AtmosphereFields;
            case 2:
                return _project!.WeatherFields;
            case 3:
                return _project!.OrbitFields;
            case 4:
                return _project!.ResourceFields;
            case 6:
                return _project!.AdvancedFields;
            case 7:
                return _project!.MapFields;
            default:
                return _project!.WorldFields;
        }
    }

    /// <summary>Supplies a player-facing explanation of the current editing page.</summary>
    private string PageDescription()
    {
        switch (_page)
        {
            case 1:
                return "Gas quantities: moles per 8,000 L. Temperature values: kelvin; curve Time is the input coordinate.";
            case 2:
                return "Add or remove weather events, then adjust their timing and effects.";
            case 3:
                return "Distance uses AU or km as labelled. Changes create a private celestial body; verify orbit in-game.";
            case 4:
                return "OreDensity multipliers come from this template's world-region entries.";
            case 5:
                return "Source maps can be compiled with the game's Terrain Tools; compiled chunks can be packaged with this world.";
            case 6:
                return "Advanced XML-backed fields include strings and booleans as well as numbers. Unchanged values and unrecognized nodes are preserved.";
            case 7:
                return "Preview and paint region masks. Painting edits this new world copy and includes a custom mask texture in the created mod.";
            case 8:
                return "Choose an installed starting profile, copy it into this world, and adjust its spawn roles without changing the installed game data.";
            case 9:
                return "Store reusable terrain and texture files locally, then add copies to this world mod for XML settings to reference.";
            default:
                return "Review the blank world's identity and presentation settings, or apply an original world preset above.";
        }
    }

    /// <summary>Draws the title bar with Back and Help actions.</summary>
    private void DrawEditorHeader()
    {
        GUILayout.BeginHorizontal();
        GUILayout.BeginHorizontal(_cardStyle!);
        if (NativeBackButton())
            ConfirmLeaveEditor();
        GUILayout.FlexibleSpace();
        GUILayout.Label("WORLD CREATOR", _titleStyle!, GUILayout.ExpandWidth(true));
        GUILayout.FlexibleSpace();
        if (NativeButton("Help", _buttonStyle!, GUILayout.Width(120), GUILayout.Height(34)))
            _pendingHelpTopic = _page;
        GUILayout.EndHorizontal();
        GUILayout.Space(30f);
        GUILayout.EndHorizontal();
        GUILayout.Space(CardSpacing);
    }

    /// <summary>Arranges the preset, summary, identity and presentation cards on Overview.</summary>
    private void DrawOverviewPageCards()
    {
        DrawOriginalWorldCard();
        GUILayout.Space(CardSpacing);
        DrawOverviewSummary();
        GUILayout.Space(CardSpacing);
        DrawCardColumns(new Action[] { DrawWorldIdentityCard, DrawOverviewShortcuts }, draw =>
        {
            draw();
            return false;
        });
        DrawWorldPresentationCard();
        GUILayout.Space(CardSpacing);
    }

    /// <summary>Shows the XML field count and captures the Advanced search filter.</summary>
    private void DrawAdvancedFilterCard()
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("ADVANCED WORLD DATA", _sectionStyle!);
        GUILayout.Label(_project!.AdvancedFields.Count + " editable values from the selected world's complete XML tree. Unknown elements are retained during export; edits to IDs or references can still break their links.");
        GUILayout.BeginHorizontal();
        GUILayout.Label("Filter path or value", GUILayout.Width(130));
        _advancedFilter = GUILayout.TextField(_advancedFilter);
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        GUILayout.Space(CardSpacing);
    }

    /// <summary>Shows the map-linked field count and captures the map field filter.</summary>
    private void DrawMapFilterCard()
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("MAP DATA", _sectionStyle!);
        GUILayout.Label(
            _project!.MapFields.Count + " map-linked XML values. Changes are shared with Advanced and included in the created world mod.",
            _mutedStyle!
        );
        GUILayout.BeginHorizontal();
        GUILayout.Label("Filter map fields", GUILayout.Width(130));
        _advancedFilter = GUILayout.TextField(_advancedFilter);
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    /// <summary>Arranges atmosphere, gas reference, temperature, climate and environment controls.</summary>
    private void DrawEnvironmentPageCards()
    {
        var atmosphereAvailableWidth = PageContentWidth;
        var atmospherePair = atmosphereAvailableWidth >= 1220f;
        var atmosphereCardWidth = atmospherePair ? (atmosphereAvailableWidth - CardSpacing) * 0.5f : atmosphereAvailableWidth;
        if (atmospherePair)
            GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(_cardStyle!, GUILayout.Width(atmosphereCardWidth));
        GUILayout.Label("ATMOSPHERE", _sectionStyle!);
        DrawGasControls();
        GUILayout.EndVertical();
        GUILayout.Space(CardSpacing);
        DrawGasPhaseReference();
        if (atmospherePair)
            GUILayout.EndHorizontal();
        GUILayout.Space(CardSpacing);
        DrawTemperatureChart();
        GUILayout.Space(CardSpacing);
        DrawClimatePreview();
        GUILayout.Space(CardSpacing);
        DrawCurveKeyEditor(_project!.AtmosphereFields, "Atmosphere");
        GUILayout.Space(CardSpacing);
        DrawWorldEnvironmentCard();
    }

    /// <summary>Arranges weather selection, event timing and weather curve controls.</summary>
    private void DrawWeatherPageCards()
    {
        DrawWeatherControls();
        GUILayout.Space(CardSpacing);
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("WEATHER EFFECT SETTINGS", _sectionStyle!);
        GUILayout.Label(
            "Each event added to this world has its own editable settings card below. Use the Installed Weather or My Saved Weather cards above to add or remove events.",
            _mutedStyle!
        );
        GUILayout.Space(10);
        DrawWeatherTimingDescription();
        GUILayout.EndVertical();
        GUILayout.Space(CardSpacing);
        DrawCurveKeyEditor(_project!.WeatherFields, "Weather");
    }

    /// <summary>Renders fields not already represented by page-specific controls, applying the current filter.</summary>
    private void DrawRemainingPageFields()
    {
        var fields = _page >= 8 || _page == 3 || _page == 5 ? new List<WorldProject.Field>() : Fields();
        var weatherFieldsRendered = false;
        if (_page == 1 || _page == 2)
        {
            var curveParents = GetCurveKeyParents(fields);
            fields = fields.Where(f => !curveParents.Any(parent => f.Label.StartsWith(parent + " / ", StringComparison.Ordinal))).ToList();
        }

        if (_page == 2 && fields.Count > 0)
        {
            DrawWeatherFieldGroups(fields);
            weatherFieldsRendered = true;
        }

        if ((_page == 6 || _page == 7) && _advancedFilter.Length > 0)
            fields = fields.Where(f => f.Label.IndexOf(_advancedFilter, StringComparison.OrdinalIgnoreCase) >= 0 || f.Value.IndexOf(_advancedFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        if (_page == 0)
            fields = fields.Where(f => !f.Label.StartsWith("Overview/", StringComparison.Ordinal) && !f.Label.StartsWith("Environment/", StringComparison.Ordinal) && !f.Label.StartsWith("Gravity", StringComparison.Ordinal) && !f.Label.EndsWith(" / Latitude", StringComparison.Ordinal) && !f.Label.EndsWith(" / Longitude", StringComparison.Ordinal))
                .ToList();
        if (_page == 6 || _page == 7)
            GUILayout.Label("Showing " + fields.Count + " fields");
        if (fields.Count == 0 && _page < 8 && _page != 3 && _page != 5 && !weatherFieldsRendered)
            GUILayout.Label("No values are defined for this template in this section.");
        if (!weatherFieldsRendered && fields.Count > 0)
        {
            if (_page == 0 || _page == 4 || _page == 6 || _page == 7)
                GUILayout.Space(CardSpacing);
            if (_page == 7)
                DrawMapFieldCards(fields);
            else
                DrawFieldCards(fields);
        }
    }

    /// <summary>Dispatches the selected page controls before rendering its remaining XML fields.</summary>
    private void DrawSelectedPageContent()
    {
        if (_page != 1)
        {
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            GUILayout.Label(Pages[_page], _titleStyle!);
            GUILayout.Label(PageDescription(), _mutedStyle!);
            GUILayout.EndVertical();
            GUILayout.Space(CardSpacing);
        }

        if (_page == 0)
        {
            DrawOverviewPageCards();
        }

        if (_page == 4 && _project != null)
            DrawResourceControls();
        if (_page == 5 && _project != null)
            DrawTerrainInfo();
        if (_page == 6 && _project != null)
        {
            DrawAdvancedFilterCard();
        }

        if (_page == 7 && _project != null)
        {
            DrawMapFilterCard();
        }

        if (_page == 1 && _project != null)
        {
            DrawEnvironmentPageCards();
        }

        if (_page == 2 && _project != null)
        {
            DrawWeatherPageCards();
        }

        if (_page == 3 && _project != null)
            DrawOrbitPreview();
        if (_page == 7 && _project != null)
        {
            GUILayout.Space(CardSpacing);
            DrawSpaceMapSettings();
            DrawMapPreview();
        }

        if (_page == 8 && _project != null)
            DrawStartConditions();
        if (_page == 9 && _project != null)
            DrawAssetLibrary();
        if (_project != null)
        {
            DrawRemainingPageFields();
        }
    }

    /// <summary>Draws status and world creation controls aligned with the scroll content.</summary>
    private void DrawEditorFooter(float contentRightInset)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(10f);
        GUILayout.BeginHorizontal(_cardStyle!);
        GUILayout.Label(_status, _mutedStyle!, GUILayout.ExpandWidth(true));
        if (NativeButton("Create world mod", _primaryButtonStyle!, GUILayout.Width(170), GUILayout.Height(34)) && _project != null)
            Export();
        GUILayout.EndHorizontal();
        GUILayout.Space(contentRightInset);
        GUILayout.EndHorizontal();
    }
}
