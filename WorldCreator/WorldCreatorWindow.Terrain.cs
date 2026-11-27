using System;
using System.IO;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private string _terrainPath = "";
    private string _compiledTerrainPath = "";
    private string _terrainKind = "Heightmap";
    private Texture2D? _terrainPreview;
    private string _lavaMinHeight = "2";
    private string _lavaMaxHeight = "128";
    private WorldProject? _lavaDraftProject;
    /// <summary>Arranges the source-terrain summary and the terrain-file controls.</summary>
    private void DrawTerrainInfo()
    {
        DrawCardColumns(new Func<bool>[] { DrawTerrainSourceCard, DrawTerrainFilesCard }, draw => draw(), minimumWidth: 700);
    }

    /// <summary>Shows the loaded world's terrain size, maps and source references.</summary>
    private bool DrawTerrainSourceCard()
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("TERRAIN SOURCE", _sectionStyle!);
        GUILayout.Label("Terrain settings copied from the selected world:");
        if (_project!.TerrainSummary.Count == 0)
        {
            GUILayout.Label("No TerrainSettings entry is defined.");
            GUILayout.EndVertical();
            return true;
        }

        foreach (var item in _project.TerrainSummary)
            GUILayout.Label(item);
        GUILayout.Space(8);
        GUILayout.Label(_project.TerrainDescription);
        GUILayout.EndVertical();
        return false;
    }

    /// <summary>Collects terrain-source files or compiled terrain and provides access to the native terrain tools.</summary>
    private bool DrawTerrainFilesCard()
    {
        var project = _project!;
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("TERRAIN FILES & TOOLS", _sectionStyle!);
        GUILayout.BeginHorizontal();
        var kinds = new[] { "Heightmap", "Crust", "Cracks", "Lava" };
        GUILayout.Label("Source type", GUILayout.Width(90));
        _terrainKind = kinds[NativeDropdown("terrain-kind", Array.IndexOf(kinds, _terrainKind), kinds, _buttonStyle!, GUILayout.Width(180))];
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(10);
        GUILayout.Label("Source from Asset Libraries", _mutedStyle!);
        _terrainPath = DrawLibraryAssetDropdown("terrain-source-asset", _terrainPath, _terrainKind == "Heightmap", "Select a library source");
        GUILayout.Space(10);
        GUILayout.Label("Type: Heightmap (.raw/.data), Crust (.png), Cracks (.png), or Lava (.png). Heightmap must match this template's terrain size.");
        GUILayout.Space(10);
        GUILayout.Label("Compiled terrain folder", _mutedStyle!);
        GUILayout.BeginHorizontal();
        _compiledTerrainPath = GUILayout.TextField(_compiledTerrainPath);
        GUILayout.Space(10);
        if (NativeButton("Browse…", _buttonStyle!, GUILayout.Width(110), GUILayout.Height(32)))
            BrowseTerrainPath(true);
        GUILayout.EndHorizontal();
        GUILayout.Space(10);
        if (NativeButton("Use compiled terrain", _buttonStyle!, GUILayout.Width(210), GUILayout.Height(32)))
        {
            try
            {
                project.SetCompiledTerrainFolder(_compiledTerrainPath);
                _status = "Validated " + project.CompiledTerrainFiles.Count + " indexed terrain files for world-specific packaging.";
            }
            catch (Exception e)
            {
                _status = "Compiled terrain rejected: " + e.Message;
            }
        }

        GUILayout.Label("Choose the folder saved by Terrain Tools. The export copies indexed Terrain<number>.dat files into this mod.");
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        if (NativeButton("Validate and add source", _buttonStyle!, GUILayout.Width(240), GUILayout.Height(32)))
        {
            try
            {
                var kind = new[]
                {
                    "heightmap",
                    "crust",
                    "cracks",
                    "lava"
                }.FirstOrDefault(k => string.Equals(k, _terrainKind, StringComparison.OrdinalIgnoreCase));
                if (kind == null)
                    throw new InvalidOperationException("Use Heightmap, Crust, Cracks, or Lava as source type.");
                var name = char.ToUpperInvariant(kind[0]) + kind.Substring(1);
                LoadTerrainPreview(name, _terrainPath);
                project.SetTerrainSource(name, _terrainPath);
                _status = name + " source validated and will be copied into TerrainSource in the created world mod.";
            }
            catch (Exception e)
            {
                _status = "Terrain source rejected: " + e.Message;
            }
        }

        GUILayout.Space(10);
        if (NativeButton("Open Terrain Tools", _buttonStyle!, GUILayout.Width(210), GUILayout.Height(32)))
            OpenTerrainTools();
        GUILayout.EndHorizontal();
        GUILayout.Space(CardSpacing);
        GUILayout.Label(
            "Terrain Tools is the game's own terrain utility. World Creator hides while it is open; reopen World Creator from the main menu when you're ready to continue this project.",
            _mutedStyle!
        );
        if (_terrainPreview != null)
            GUILayout.Label(_terrainPreview, GUILayout.Width(240), GUILayout.Height(160));
        foreach (var pair in project.TerrainSourceFiles)
            GUILayout.Label(pair.Key + ": " + Path.GetFileName(pair.Value));
        GUILayout.Space(12);
        GUILayout.Label("Crust and crack maps are compilation inputs: select the PNG here, then import it in Terrain Tools and select the saved compiled terrain above. They are not separate runtime map fields.", _mutedStyle!);
        GUILayout.Label("Backsplash map is not a supported terrain setting in this game build.", _mutedStyle!);
        if (_terrainKind == "Lava")
            DrawRuntimeLavaControls(project);
        GUILayout.EndVertical();
        return false;
    }

    /// <summary>Assigns the chosen lava PNG to the runtime lava surface, independently of terrain compilation.</summary>
    private void DrawRuntimeLavaControls(WorldProject project)
    {
        if (_lavaDraftProject != project)
        {
            _lavaDraftProject = project;
            _lavaMinHeight = project.LavaMinimumHeight.ToString(CultureInfo.InvariantCulture);
            _lavaMaxHeight = project.LavaMaximumHeight.ToString(CultureInfo.InvariantCulture);
        }
        GUILayout.Space(12);
        GUILayout.Label("LAVA SURFACE", _sectionStyle!);
        GUILayout.Label("Black pixels use the minimum height; white pixels use the maximum. Use a square power-of-two PNG, at least 256 × 256. Use the same map and heights in Terrain Tools so the ground and lava agree.", _mutedStyle!);
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(180));
        GUILayout.Label("Minimum height (0–1023)", _mutedStyle!);
        _lavaMinHeight = MaskNumericInput(GUILayout.TextField(_lavaMinHeight, _inputStyle!));
        GUILayout.EndVertical();
        GUILayout.Space(20);
        GUILayout.BeginVertical(GUILayout.Width(180));
        GUILayout.Label("Maximum height (0–1023)", _mutedStyle!);
        _lavaMaxHeight = MaskNumericInput(GUILayout.TextField(_lavaMaxHeight, _inputStyle!));
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.Space(10);
        if (!NativeButton("Use PNG as lava surface", _buttonStyle!, GUILayout.Width(250), GUILayout.Height(32)))
            return;
        try
        {
            if (!int.TryParse(_lavaMinHeight, NumberStyles.Integer, CultureInfo.InvariantCulture, out var min)
                || !int.TryParse(_lavaMaxHeight, NumberStyles.Integer, CultureInfo.InvariantCulture, out var max))
                throw new InvalidOperationException("Lava heights must be whole numbers from 0 to 1023, with minimum below maximum.");
            LoadTerrainPreview("Lava", _terrainPath);
            project.SetRuntimeLavaMap(_terrainPath, min, max);
            _status = "Lava surface selected. The PNG, heights and lava-enabled setting will be included in the exported world.";
        }
        catch (Exception exception)
        {
            _status = "Lava surface rejected: " + exception.Message;
        }
    }

    /// <summary>Opens the native client editor without requiring its client-only types as build references.</summary>
    private void OpenTerrainTools()
    {
        var gameAssembly = typeof(WorldManager).Assembly;
        var windowType = gameAssembly.GetType("UI.ImGuiUi.ImGuiTerrainUtilityWindow");
        var managerType = gameAssembly.GetType("UI.ImGuiUi.ImGuiWindows.ImGuiWindowManager");
        var terrainWindow = windowType?.GetField("Window")?.GetValue(null);
        var showingField = windowType?.GetField("IsShowing");
        var openMethod = managerType?.GetMethod("Open");
        if (terrainWindow == null || showingField == null || openMethod == null)
        {
            _status = "The native Terrain Editor is unavailable in this game build.";
            return;
        }

        EditorWindow.Hide();
        if (!(bool)showingField.GetValue(terrainWindow)!)
            openMethod.Invoke(null, new[] { terrainWindow });
        _status = "Opened the game's Terrain Tools. Reopen World Creator from the main menu to continue this project.";
    }

    /// <summary>Loads the selected terrain image for preview and reports unsupported or unreadable files.</summary>
    private void LoadTerrainPreview(string kind, string path)
    {
        if (_terrainPreview != null)
            Destroy(_terrainPreview);
        _terrainPreview = null;
        if (kind == "Heightmap")
            return;
        var size = WorldProject.ReadTerrainPngSize(path);
        if (size.Width > SystemInfo.maxTextureSize || size.Height > SystemInfo.maxTextureSize)
            throw new InvalidOperationException("PNG dimensions exceed this graphics device's texture limit of " + SystemInfo.maxTextureSize + ".");
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!texture.LoadImage(File.ReadAllBytes(path)))
        {
            Destroy(texture);
            throw new InvalidOperationException("Could not decode PNG image.");
        }

        texture.name = "World Creator " + kind + " preview";
        _terrainPreview = texture;
    }
}
