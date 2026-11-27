using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    /// <summary>Creates a project from a blank world or the selected installed template.</summary>
    private void LoadTemplate()
    {
        ClearExportIssue();
        try
        {
            _project = _template == 0 ? WorldProject.CreateBlank(Path.Combine(Application.dataPath, "StreamingAssets")) : new WorldProject(Path.Combine(Application.dataPath, "StreamingAssets"), Files[_template]);
            LoadMapPreview();
            _scroll = Vector2.zero;
            _status = _template == 0 ? "Blank settings created. Add compiled terrain before loading this world in game." : "Template loaded. Create a world mod to keep these settings.";
        }
        catch (Exception e)
        {
            _project = null;
            _status = "Could not load template: " + e.Message;
            Debug.LogError("[World Creator] " + e);
        }
    }

    /// <summary>Offers entry points for creating a blank world or loading an installed custom world.</summary>
    private void DrawHome()
    {
        BeginNativeMenuPage();
        GUILayout.Label("Create a custom world or continue editing one of your installed world mods.", _mutedStyle!);
        GUILayout.Space(24);
        if (NativeButton("Create a New World\nStart with a blank world and edit its settings", _primaryButtonStyle!, GUILayout.Height(86)))
            StartBlankProject();
        GUILayout.Space(12);
        if (NativeButton("Load a World\nContinue from an installed custom world mod", _buttonStyle!, GUILayout.Height(86)))
        {
            ScanWorlds();
            _pendingScreen = 2;
        }

        EndNativeMenuPage();
    }

    /// <summary>Resets project identity and opens the editor with blank world settings.</summary>
    private void StartBlankProject()
    {
        _template = 0;
        _presetSelection = -1;
        _id = "WC_MyWorld";
        _name = "My World";
        _pendingPage = 0;
        LoadTemplate();
        _pendingScreen = 3;
    }

    /// <summary>Applies the confirmed original-world preset to the current project.</summary>
    private void ApplyOriginalWorld(int template)
    {
        _template = template;
        _id = "WC_MyWorld";
        _name = "My World";
        LoadTemplate();
        _pendingPage = 0;
    }

    /// <summary>Starts the shared panel layout for the home and world-loading screens.</summary>
    private void BeginNativeMenuPage()
    {
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
    }

    /// <summary>Closes the shared home or world-loading panel layout.</summary>
    private static void EndNativeMenuPage()
    {
        GUILayout.EndVertical();
    }

    /// <summary>Returns from the loader to the entry page, or from the entry page to the native main menu.</summary>
    internal void BackFromMenu()
    {
        if (_screen == 0)
            EditorWindow.Hide();
        else
            _pendingScreen = 0;
    }

    /// <summary>Refreshes the available worlds from local mods and discovered Workshop directories.</summary>
    private void ScanWorlds()
    {
        _loadChoices.Clear();
        foreach (var root in DiscoverModRoots())
            AddWorldChoices(root.Path, root.Workshop);
        _loadChoices.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        _loadSource = _loadChoices.Any(choice => !choice.Workshop) ? 0 : 1;
    }

    /// <summary>Shares the game-resolved mod locations between world and celestial-library discovery.</summary>
    private static IEnumerable<(string Path, bool Workshop)> DiscoverModRoots()
    {
        var installed = (WorkshopMenu.ModsConfig?.Mods.AsEnumerable() ?? Enumerable.Empty<ModData>()).Where(mod => !(mod is CoreModData)).Select(mod => (Path: (string)mod.DirectoryPath, Workshop: mod is WorkshopModData));
        return ModSearchPaths.Discover(Application.dataPath, Assets.Scripts.Serialization.Settings.CurrentData?.SavePath, installed);
    }

    /// <summary>Scans a mod root's GameData folders while tolerating unreadable directories.</summary>
    private void AddWorldChoices(string root, bool workshop)
    {
        if (!Directory.Exists(root))
            return;
        try
        {
            var gameDataFolders = new List<string>();
            if (string.Equals(
                Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                "GameData",
                StringComparison.OrdinalIgnoreCase
            ))
                gameDataFolders.Add(root);
            gameDataFolders.AddRange(Directory.GetDirectories(root, "GameData", SearchOption.AllDirectories));
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var gameData in gameDataFolders)
            {
                var modRoot = Directory.GetParent(gameData)?.FullName ?? gameData;
                var aboutName = ReadModName(modRoot);
                foreach (var path in Directory.GetFiles(gameData, "*.xml", SearchOption.AllDirectories))
                {
                    TryAddWorldChoice(path, workshop, aboutName, seen);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[World Creator] Could not scan world folder " + root + ": " + e.Message);
        }
    }

    /// <summary>Finds the supported World element inside a standalone or wrapped XML document.</summary>
    private static XElement? FindWorldDefinition(XElement? root)
    {
        if (root == null)
            return null;
        if (root.Name.LocalName == "World")
            return root;
        return root.Element("WorldSettings")?.Element("World") ?? root.Element("GameData")?.Element("WorldSettings")?.Element("World");
    }

    /// <summary>Reads a mod's display name from its About metadata when available.</summary>
    private static string ReadModName(string modRoot)
    {
        try
        {
            var aboutPath = Path.Combine(modRoot, "About", "About.xml");
            return File.Exists(aboutPath) ? XDocument.Load(aboutPath).Root?.Element("Name")?.Value ?? string.Empty : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Chooses the loaded world's meaningful name rather than a placeholder or folder label.</summary>
    private static string ResolveLoadedWorldDisplayName(string worldFile, string? xmlName)
    {
        if (!IsPlaceholderWorldName(xmlName))
            return xmlName!;
        var directory = new DirectoryInfo(Path.GetDirectoryName(worldFile) ?? string.Empty);
        while (directory != null)
        {
            var modName = ReadModName(directory.FullName);
            if (!string.IsNullOrWhiteSpace(modName) && !IsPlaceholderWorldName(modName))
                return modName;
            directory = directory.Parent!;
        }

        return Path.GetFileName(Path.GetDirectoryName(worldFile));
    }

    /// <summary>Recognizes generic imported names that should fall back to source-world metadata.</summary>
    private static bool IsPlaceholderWorldName(string? name)
    {
        return string.IsNullOrWhiteSpace(name) || name.Equals("GameData", StringComparison.OrdinalIgnoreCase) || name.Equals("WorldSettings", StringComparison.OrdinalIgnoreCase) || name.Equals("World", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Loads the selected world into a new project and refreshes its map previews.</summary>
    private void LoadWorld(string path)
    {
        ClearExportIssue();
        try
        {
            _project = new WorldProject(Path.Combine(Application.dataPath, "StreamingAssets"), path, true);
            _template = -1;
            _presetSelection = -1;
            var mods = ModSearchPaths.LocalMods(Assets.Scripts.Serialization.Settings.CurrentData?.SavePath);
            _id = _project.ExportedWorldId != null && _project.CanUpdateExport(mods, _project.ExportedWorldId) ? _project.ExportedWorldId : "WC_MyWorld";
            _name = ResolveLoadedWorldDisplayName(path, _project.DisplayName);
            _pendingPage = 0;
            LoadMapPreview();
            _pendingScreen = 3;
            _status = _project.CanUpdateExport(mods, _id) ? "Loaded local world. Update this mod, or change World ID to create a separate copy." : "Loaded world settings. Review and export as a new mod.";
        }
        catch (Exception e)
        {
            _status = "Could not load world: " + e.Message;
            Debug.LogError("[World Creator] " + e);
        }
    }

    /// <summary>Adds one valid world XML file to the load list and ignores unrelated or malformed files.</summary>
    private void TryAddWorldChoice(string path, bool workshop, string? aboutName, HashSet<string> seen)
    {
        if (!seen.Add(path) || _loadChoices.Any(choice => string.Equals(choice.Path, path, StringComparison.OrdinalIgnoreCase)))
            return;
        try
        {
            var document = XDocument.Load(path);
            var world = FindWorldDefinition(document.Root);
            if (world == null || (world.Element("Name") == null && string.IsNullOrWhiteSpace((string? )world.Attribute("Id"))))
                return;
            var name = (string? )world.Element("Name")?.Attribute("Value");
            if (IsPlaceholderWorldName(name))
                name = aboutName;
            if (string.IsNullOrWhiteSpace(name))
                name = (string? )world.Attribute("Id");
            if (string.IsNullOrWhiteSpace(name))
                name = Path.GetFileNameWithoutExtension(path);
            _loadChoices.Add((path, name, workshop));
        }
        catch
        { /* Ignore unrelated or malformed XML files in mod data folders. */
        }
    }
}
