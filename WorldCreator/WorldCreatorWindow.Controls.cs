using System;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private string? _hoverTooltip;
    private string? _nativePopupOpen;
    private string? _queuedNativePopupSelection;
    private string[] _nativePopupOptions = Array.Empty<string>();
    private Rect _nativePopupAnchor;
    private float _nativePopupScrollOffset;
    private Rect _nativePopupRect;
    private int _nativePopupSelectedIndex;
    private int _nativePopupCurrentSelected;
    /// <summary>Draws the installed game's Back arrow at the same size as the New Game page.</summary>
    private bool NativeBackButton()
    {
        var size = NativeMenuButtonAppearance.Back?.Size ?? new Vector2(64, 64);
        return GUILayout.Button(new GUIContent("", "Back"), _backButtonStyle!, GUILayout.Width(size.x), GUILayout.Height(size.y));
    }
    /// <summary>Draws an editor action using the selected native-style button layout.</summary>
    private bool NativeButton(string label, GUIStyle layoutStyle, params GUILayoutOption[] options) => GUILayout.Button(new GUIContent(label), layoutStyle, options);
    /// <summary>Draws an editor action using the selected native-style button layout.</summary>
    private bool NativeButton(string label, params GUILayoutOption[] options) => NativeButton(label, GUI.skin.button, options);
    /// <summary>Draws a button at an explicit rectangle without adding a GUILayout control.</summary>
    private bool NativeButtonAt(Rect guiRect, string label, GUIStyle layoutStyle) => GUI.Button(guiRect, new GUIContent(label), layoutStyle);
    /// <summary>Arranges native-style options in rows and returns the selected index.</summary>
    private int NativeSelectionGrid(int selected, string[] labels, int columns, GUIStyle style, params GUILayoutOption[] options)
    {
        columns = Mathf.Max(1, columns);
        GUILayout.BeginVertical(options);
        for (var start = 0; start < labels.Length; start += columns)
        {
            GUILayout.BeginHorizontal();
            var end = Math.Min(labels.Length, start + columns);
            for (var i = start; i < end; i++)
            {
                if (NativeButton(labels[i], style, GUILayout.ExpandWidth(true), GUILayout.Height(32)))
                    selected = i;
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
        return selected;
    }

    /// <summary>Arranges native-style options in rows and returns the selected index.</summary>
    private int NativeSelectionGrid(int selected, string[] labels, int columns, params GUILayoutOption[] options) => NativeSelectionGrid(selected, labels, columns, GUI.skin.button, options);
    /// <summary>Anchors a floating option list to its trigger and applies selections during Layout.</summary>
    private int NativeDropdown(string id, int selected, string[] labels, GUIStyle layoutStyle, params GUILayoutOption[] options)
    {
        if (labels == null || labels.Length == 0)
            return -1;
        selected = selected < 0 ? -1 : Mathf.Clamp(selected, 0, labels.Length - 1);
        var key = "dropdown:" + _screen + ":" + _page + ":" + id;
        var label = (selected < 0 ? "Choose a vanilla world…" : labels[selected]) + "  ▾";
        if (NativeButton(label, layoutStyle, options))
        {
            if (_nativePopupOpen == key)
                CloseNativePopup();
            else
            {
                _nativePopupOpen = key;
                _nativePopupOptions = labels;
                _nativePopupCurrentSelected = selected;
                _nativePopupScrollOffset = selected * 32f;
                var buttonRect = GUILayoutUtility.GetLastRect();
                var mouse = Input.mousePosition;
                var width = Mathf.Max(buttonRect.width, 190f);
                _nativePopupAnchor = new Rect(mouse.x - width * 0.5f, Screen.height - mouse.y + buttonRect.height * 0.5f, width, buttonRect.height);
            }
        }
        else if (_nativePopupOpen == key)
        {
            _nativePopupOptions = labels;
            _nativePopupCurrentSelected = selected;
        }

        if (_queuedNativePopupSelection == key && Event.current.type == EventType.Layout)
        {
            selected = _nativePopupSelectedIndex;
            _queuedNativePopupSelection = null;
            CloseNativePopup();
        }

        return selected;
    }

    /// <summary>Draws the floating dropdown above the editor and handles scrolling and selection.</summary>
    private void DrawNativePopup(string key, string[] labels, int selected)
    {
        if (_nativePopupOpen != key || labels.Length == 0)
            return;
        const float rowHeight = 32f;
        var visibleRows = Mathf.Max(1, Mathf.FloorToInt((Screen.height * .55f - 16f) / rowHeight));
        var count = Math.Min(labels.Length, visibleRows);
        var height = count * rowHeight + 16f;
        var widestLabel = labels.Max(option => _templateStyle!.CalcSize(new GUIContent(option)).x);
        var width = Mathf.Clamp(Mathf.Max(_nativePopupAnchor.width, widestLabel + 52f), 190f, Mathf.Max(190f, Screen.width - 16f));
        var x = Mathf.Clamp(_nativePopupAnchor.center.x - width * 0.5f, 8f, Screen.width - width - 8f);
        var y = _nativePopupAnchor.yMax + 4f;
        if (y + height > Screen.height - 8f)
            y = _nativePopupAnchor.yMin - height - 4f;
        y = Mathf.Clamp(y, 8f, Mathf.Max(8f, Screen.height - height - 8f));
        _nativePopupRect = new Rect(x, y, width, height);
        GUI.Window(904523, _nativePopupRect, _ =>
        {
            var scroll = GUILayout.BeginScrollView(new Vector2(0f, _nativePopupScrollOffset), false, true, GUILayout.Height(height - 12f));
            _nativePopupScrollOffset = scroll.y;
            for (var optionIndex = 0; optionIndex < labels.Length; optionIndex++)
            {
                if (GUILayout.Button(labels[optionIndex], optionIndex == selected ? _templateActiveStyle! : _templateStyle!, GUILayout.Height(rowHeight)))
                {
                    _nativePopupSelectedIndex = optionIndex;
                    _queuedNativePopupSelection = key;
                    CloseNativePopup();
                }
            }

            GUILayout.EndScrollView();
        }, GUIContent.none, _cardStyle!);
    }

    /// <summary>Closes the floating dropdown and clears its active option state.</summary>
    private void CloseNativePopup()
    {
        _nativePopupOpen = null;
    }

    /// <summary>Asks for confirmation before replacing the current draft with an original-world preset.</summary>
    private void DrawDiscardDialog(int windowId)
    {
        GUILayout.Label("Replace the current settings with this selection? Any edits in this project will be discarded.");
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        if (NativeButton(
            "Replace settings",
            _primaryButtonStyle!,
            GUILayout.Height(32)
        ))
            _discardConfirmed = true;
        if (NativeButton("Keep editing", _buttonStyle!, GUILayout.Height(32)))
        {
            _pendingTemplate = -1;
            _pendingLoadPath = null;
        }

        GUILayout.EndHorizontal();
    }

    /// <summary>Draws a text input and registers its help text without changing the field value.</summary>
    private string TooltipTextField(string value, string tooltip, float width, float height, int maxLength = 0, bool numeric = false)
    {
        var rect = width > 0 ? GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height)) : GUILayoutUtility.GetRect(0, height, GUILayout.ExpandWidth(true), GUILayout.Height(height));
        RegisterHoverTooltip(rect, tooltip);
        var edited = maxLength > 0 ? GUI.TextField(rect, value, maxLength, _inputStyle!) : GUI.TextField(rect, value, _inputStyle!);
        return numeric ? MaskNumericInput(edited) : edited;
    }

    /// <summary>Records the help text when the pointer is over the supplied control rectangle.</summary>
    private void RegisterHoverTooltip(Rect rect, string tooltip)
    {
        // Last-rect coordinates and the current event share the active
        // IMGUI group's origin, including nested cards and scroll views.
        if (!rect.Contains(Event.current.mousePosition))
            return;
        _hoverTooltip = tooltip;
    }

    /// <summary>Draws a label and makes its description available to the hover-help overlay.</summary>
    private void HelpLabel(GUIContent content, GUIStyle style, params GUILayoutOption[] options)
    {
        GUILayout.Label(content, style, options);
        if (Event.current.type == EventType.Repaint)
            RegisterHoverTooltip(GUILayoutUtility.GetLastRect(), content.tooltip);
    }

    /// <summary>Positions the current help text inside the visible editor bounds.</summary>
    private void DrawHoverTooltip(string tooltip)
    {
        if (string.IsNullOrWhiteSpace(tooltip) || _tooltipStyle == null || Event.current.type != EventType.Repaint || GUIUtility.hotControl != 0)
            return;
        var content = new GUIContent(tooltip);
        var width = Mathf.Min(420, _window.width - 32);
        var height = Mathf.Min(_window.height - 32, _tooltipStyle.CalcHeight(content, width) + 8);
        var mouse = Event.current.mousePosition;
        var x = Mathf.Clamp(mouse.x + 18, 8, _window.width - width - 8);
        var y = mouse.y + 22;
        if (y + height > _window.height - 8)
            y = mouse.y - height - 12;
        y = Mathf.Clamp(y, 8, _window.height - height - 8);
        GUI.Box(new Rect(x, y, width, height), content, _tooltipStyle);
    }

    /// <summary>Filters numeric drafts while preserving intermediate values the player is still typing.</summary>
    private static string MaskNumericInput(string value)
    {
        var chars = value.Where(c => char.IsDigit(c) || c == '.' || c == '-' || c == '+' || c == 'e' || c == 'E').ToArray();
        return new string (chars);
    }

    /// <summary>Draws a colored line between two points in a chart.</summary>
    private static void DrawChartLine(Rect chart, Vector2 from, Vector2 to, float horizontalRange, float min, float max)
    {
        var a = new Vector2(chart.x + from.x / horizontalRange * chart.width, chart.y + (max - from.y) / (max - min) * chart.height);
        var b = new Vector2(chart.x + to.x / horizontalRange * chart.width, chart.y + (max - to.y) / (max - min) * chart.height);
        var delta = b - a;
        var previous = GUI.matrix;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, a);
        GUI.DrawTexture(new Rect(a.x, a.y - 1, delta.magnitude, 2), Texture2D.whiteTexture);
        GUI.matrix = previous;
    }
}
