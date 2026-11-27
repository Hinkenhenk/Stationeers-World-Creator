using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private bool _fileBrowserPending;

    /// <summary>Opens the game's bundled Windows file picker after the current GUI frame has finished.</summary>
    private void BrowseTerrainPath(bool folder)
    {
        BrowseEditorPath(folder, folder ? "Choose compiled terrain folder" : "Choose " + _terrainKind + " source",
            folder ? _compiledTerrainPath : _terrainPath, _terrainKind == "Heightmap" ? "" : "png",
            path => { if (folder) _compiledTerrainPath = path; else _terrainPath = path; });
    }

    /// <summary>Opens the shared game file picker for an editor-owned path after the GUI frame ends.</summary>
    private void BrowseEditorPath(bool folder, string title, string current, string extension, Action<string> selected)
    {
        if (_fileBrowserPending)
            return;
        CloseNativePopup();
        GUI.FocusControl(null);
        _fileBrowserPending = true;
        StartCoroutine(ChooseEditorPath(folder, title, current, extension, selected));
    }

    /// <summary>Uses the existing game file-browser library and preserves the current path when cancelled.</summary>
    private IEnumerator ChooseEditorPath(bool folder, string title, string current, string extension, Action<string> selected)
    {
        yield return new WaitForEndOfFrame();
        try
        {
            var browser = Type.GetType("SFB.StandaloneFileBrowser, Assembly-CSharp-firstpass")
                ?? throw new InvalidOperationException("The game's file browser is unavailable in this build.");
            var directory = string.IsNullOrWhiteSpace(current) ? null : Directory.Exists(current) ? current : Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                directory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var method = browser.GetMethod(folder ? "OpenFolderPanel" : "OpenFilePanel",
                folder ? new[] { typeof(string), typeof(string), typeof(bool) }
                       : new[] { typeof(string), typeof(string), typeof(string), typeof(bool) });
            var arguments = folder ? new object[] { title, directory, false }
                : new object[] { title, directory, extension, false };
            var paths = method?.Invoke(null, arguments) as string[];
            if (paths != null && paths.Length > 0 && !string.IsNullOrWhiteSpace(paths[0]))
            {
                selected(paths[0]);
            }
        }
        catch (Exception exception)
        {
            _status = "Could not open file browser: " + (exception.InnerException ?? exception).Message;
        }
        finally
        {
            _fileBrowserPending = false;
        }
    }
}
