using System;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    /// <summary>Lists world definitions with the native loader's search, source tabs and per-row actions.</summary>
    private void DrawLoadPage()
    {
        BeginNativeMenuPage();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Search", _mutedStyle!, GUILayout.Width(65), GUILayout.Height(36));
        _loadSearch = GUILayout.TextField(_loadSearch, _inputStyle!, GUILayout.MinWidth(0), GUILayout.Height(36));
        if (NativeButton("Clear", _buttonStyle!, GUILayout.Width(120), GUILayout.Height(36)))
            _loadSearch = "";
        GUILayout.EndHorizontal();
        GUILayout.Space(20);
        GUILayout.BeginHorizontal();
        DrawWorldSourceTab("Local", 0);
        GUILayout.Space(20);
        DrawWorldSourceTab("Workshop", 1);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(20);
        var choices = _loadChoices.Where(choice => choice.Workshop == (_loadSource == 1)
            && choice.Name.IndexOf(_loadSearch.Trim(), StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        _scroll.x = 0;
        _scroll = GUILayout.BeginScrollView(_scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar,
            GUILayout.ExpandHeight(true));
        if (choices.Length == 0)
            GUILayout.Label("No " + (_loadSource == 0 ? "local" : "Workshop") + " world definitions match this search.", _mutedStyle!);
        foreach (var choice in choices)
        {
            DrawWorldChoiceRow(choice.Path, choice.Name);
            GUILayout.Space(18);
        }
        GUILayout.EndScrollView();
        EndNativeMenuPage();
    }

    /// <summary>Switches the loader between local mods and Steam Workshop without changing the project.</summary>
    private void DrawWorldSourceTab(string label, int source)
    {
        if (NativeButton(label, _buttonStyle!, GUILayout.Width(150), GUILayout.Height(44)))
        {
            _loadSource = source;
            _scroll = Vector2.zero;
        }
        if (_loadSource == source && Event.current.type == EventType.Repaint)
            GUI.DrawTexture(GUILayoutUtility.GetLastRect(), Texture2D.whiteTexture,
                ScaleMode.StretchToFill, true, 0, Accent, 2, 5);
    }

    /// <summary>Keeps a world's readable name and open action visible while hiding its technical path in details.</summary>
    private void DrawWorldChoiceRow(string path, string name)
    {
        GUILayout.BeginVertical(_worldChoiceStyle!);
        GUILayout.BeginHorizontal();
        var expanded = _loadDetailsPath == path;
        if (GUILayout.Toggle(expanded, name, _worldNameStyle!, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true), GUILayout.Height(60)) != expanded)
            _loadDetailsPath = expanded ? null : path;
        GUILayout.Space(20);
        if (NativeButton("Load", _loadWorldStyle!, GUILayout.Width(150), GUILayout.Height(50)))
            ConfirmWorldLoad(path, name);
        GUILayout.EndHorizontal();
        if (_loadDetailsPath == path)
        {
            GUILayout.Space(8);
            GUILayout.Label("World file location", _mutedStyle!);
            GUILayout.Label(path, _mutedStyle!);
        }
        GUILayout.EndVertical();
    }

    /// <summary>Uses the game's Delete Save prompt layout to confirm loading a world definition into the editor.</summary>
    private void ConfirmWorldLoad(string path, string name)
    {
        CloseNativePopup();
        var prompt = Assets.Scripts.UI.PromptPanel.Instance;
        prompt.CancelButton.gameObject.SetActive(true);
        var message = "Load the selected world?\n\n<color=#FF9D35>" + name + "</color>";
        if (HasUnsavedChanges())
            message += "\n\nUnsaved changes in the current project will be replaced.";
        prompt.ShowPrompt("Load selected world", message, "Load", () =>
        {
            prompt.DisablePromptPanel();
            _pendingLoadPath = path;
            _discardConfirmed = true;
        }, "Cancel", prompt.DisablePromptPanel);
    }
}
