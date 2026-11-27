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
        DrawExportIssue();
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

    private Vector2 _sidebarScroll;

    /// <summary>Draws the page navigation and queues page changes for the next layout pass.</summary>
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
        var rowHeight = Mathf.Clamp((height - y - 8f) / NavigationOrder.Length, 34f, 44f);
        var scroll = NavigationOrder.Length * rowHeight > height - y - 8f;
        if (scroll)
        {
            _sidebarScroll = GUI.BeginScrollView(new Rect(0f, y, SidebarWidth, height - y),
                _sidebarScroll, new Rect(0f, 0f, SidebarWidth - 18f, NavigationOrder.Length * rowHeight));
            y = 0f;
        }
        foreach (var i in NavigationOrder)
        {
            var buttonRect = new Rect(left, y, scroll ? width - 18f : width, rowHeight - 4f);
            if (NativeButtonAt(buttonRect, Pages[i], _navStyle!))
                _pendingPage = i;
            if (i == _page && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(buttonRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, Accent, 2, 5);
            y += rowHeight;
        }

        if (scroll) GUI.EndScrollView();
        GUI.EndGroup();
    }

    /// <summary>Edits the shared world name and identifier above the current page.</summary>
    private void DrawTopbar()
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(160));
        GUILayout.Label("WORLD PROJECT", _sectionStyle!);
        GUILayout.EndVertical();
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        GUILayout.BeginHorizontal();
        HelpLabel(new GUIContent("Display name  ⓘ", "Player-facing name shown in the Stationeers world selection menu."), _mutedStyle!, GUILayout.Height(24));
        GUILayout.Space(16);
        HelpLabel(
            new GUIContent(
            "World ID  ⓘ",
            "Unique XML identifier for the world mod: 1–47 letters, numbers or underscores. No prefix is required; WC_ is optional. Avoid IDs used by other installed worlds."
        ),
            _mutedStyle!, GUILayout.Width(240), GUILayout.Height(24)
        );
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        _name = TooltipTextField(_name, "Player-facing name shown in the Stationeers world selection menu.", 0, 30);
        GUILayout.Space(16);
        _id = TooltipTextField(
            _id,
            "Unique XML identifier for the world mod: 1–47 letters, numbers or underscores. No prefix is required; WC_ is optional. Avoid IDs used by other installed worlds.",
            240,
            30,
            47
        );
        GUILayout.EndHorizontal();
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
            case 10:
                return "Create celestial bodies and rocket travel maps for this world.";
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

    /// <summary>Filters Advanced cards immediately as the player types a path, field name or current value.</summary>
    private void DrawAdvancedFilterCard()
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("ADVANCED WORLD DATA", _sectionStyle!);
        GUILayout.Label(_project!.AdvancedFields.Count + " editable values from the selected world's complete XML tree. Unknown elements are retained during export; edits to IDs or references can still break their links.");
        GUILayout.BeginHorizontal();
        GUILayout.Label("Search", _mutedStyle!, GUILayout.Width(80));
        var search = GUILayout.TextField(_advancedFilter, _inputStyle!, GUILayout.Height(32));
        if (search != _advancedFilter)
        {
            _advancedFilter = search;
            _scroll = Vector2.zero;
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(8);
        GUILayout.Label("Filters cards by their field names, XML paths or current values as you type.", _mutedStyle!);
        GUILayout.EndVertical();
        GUILayout.Space(CardSpacing);
    }

    /// <summary>Arranges atmosphere, gas reference, temperature, climate and environment controls.</summary>
    private void DrawEnvironmentPageCards()
    {
        DrawWorldEnvironmentCard();
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("ATMOSPHERE", _sectionStyle!);
        DrawGasControls();
        GUILayout.EndVertical();
        GUILayout.Space(CardSpacing);
        DrawGasPhaseReference();
        GUILayout.Space(CardSpacing);
        DrawTemperatureChart();
        GUILayout.Space(CardSpacing);
        DrawClimatePreview();
        GUILayout.Space(CardSpacing);
        DrawCurveKeyEditor(_project!.AtmosphereFields, "Atmosphere");
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
        if (_page == 7) return;
        var fields = _page >= 8 || _page == 3 || _page == 5 ? new List<WorldProject.Field>() : Fields();
        var weatherFieldsRendered = false;
        if (_page == 1 || _page == 2)
        {
            var curveParents = GetCurveKeyParents(fields);
            fields = fields.Where(f => !curveParents.Any(parent => f.Label.StartsWith(parent + " / ", StringComparison.Ordinal))).ToList();
        }

        if (_page == 1) fields = fields.Where(field => !field.Label.Contains("/Gas/Gas")).ToList();
        if (_page == 2 && fields.Count > 0)
        {
            DrawWeatherFieldGroups(fields);
            weatherFieldsRendered = true;
        }

        var search = _advancedFilter.Trim();
        if (_page == 6 && search.Length > 0)
            fields = fields.GroupBy(FieldGroupName).Where(card => card.Any(field =>
                field.Label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || field.Value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || ReadableFieldLabel(field.Label, true).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).SelectMany(card => card).ToList();
        if (_page == 0)
            fields = fields.Where(f => !f.Label.StartsWith("Overview/", StringComparison.Ordinal) && !f.Label.StartsWith("Environment/", StringComparison.Ordinal) && !f.Label.StartsWith("Gravity", StringComparison.Ordinal) && !f.Label.EndsWith(" / Latitude", StringComparison.Ordinal) && !f.Label.EndsWith(" / Longitude", StringComparison.Ordinal))
                .ToList();
        if (_page == 6)
            GUILayout.Label("Showing " + fields.GroupBy(FieldGroupName).Count() + " cards · " + fields.Count + " fields", _mutedStyle!);
        if (fields.Count == 0 && _page < 8 && _page != 1 && _page != 3 && _page != 5 && !weatherFieldsRendered)
            GUILayout.Label(_page == 6 && search.Length > 0 ? "No cards match this search. Clear or change the search phrase." : "No values are defined for this template in this section.", _mutedStyle!);
        if (!weatherFieldsRendered && fields.Count > 0)
        {
            if (_page == 0 || _page == 4 || _page == 6 || _page == 7)
                GUILayout.Space(CardSpacing);
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
        if (_page == 10 && _project != null)
            DrawAssetCreator();
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
        var mods = ModSearchPaths.LocalMods(Assets.Scripts.Serialization.Settings.CurrentData?.SavePath);
        if (_project?.CanUpdateExport(mods, _id) == true)
        {
            if (NativeButton("Update world mod", _primaryButtonStyle!, GUILayout.Width(180), GUILayout.Height(34))) ConfirmUpdateWorld();
            GUILayout.Space(10);
        }
        if (NativeButton("Create world mod", _primaryButtonStyle!, GUILayout.Width(170), GUILayout.Height(34)) && _project != null)
            Export();
        GUILayout.EndHorizontal();
        GUILayout.Space(contentRightInset);
        GUILayout.EndHorizontal();
    }
}
