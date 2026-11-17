using System;
using System.Collections.Generic;
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
    private const float PopupRowHeight = 32f;
    private const float PopupRowSpacing = 10f;
    private readonly Dictionary<string, Rect> _dropdownButtonRects = new Dictionary<string, Rect>();
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
                _nativePopupScrollOffset = Mathf.Max(0, selected) * (PopupRowHeight + PopupRowSpacing);
                if (_dropdownButtonRects.TryGetValue(key, out var buttonRect))
                    _nativePopupAnchor = buttonRect;
            }
        }
        else if (_nativePopupOpen == key)
        {
            _nativePopupOptions = labels;
            _nativePopupCurrentSelected = selected;
        }

        // GetLastRect is reliable during Repaint; cache the trigger after scroll/group transforms.
        if (Event.current.type == EventType.Repaint)
        {
            var rect = GUILayoutUtility.GetLastRect();
            var screenRect = new Rect(GUIUtility.GUIToScreenPoint(rect.position), rect.size);
            _dropdownButtonRects[key] = screenRect;
            if (_nativePopupOpen == key) _nativePopupAnchor = screenRect;
        }

        if (_queuedNativePopupSelection == key && Event.current.type == EventType.Layout)
        {
            selected = Mathf.Clamp(_nativePopupSelectedIndex, 0, labels.Length - 1);
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
        var contentHeight = labels.Length * (PopupRowHeight + PopupRowSpacing) - PopupRowSpacing;
        var height = Mathf.Min(contentHeight + 24f, Screen.height * .55f);
        var widestLabel = labels.Max(option => _templateStyle!.CalcSize(new GUIContent(option)).x);
        var width = Mathf.Clamp(Mathf.Max(_nativePopupAnchor.width, widestLabel + 52f), 190f, Mathf.Max(190f, Screen.width - 16f));
        var x = Mathf.Clamp(_nativePopupAnchor.x, 8f, Screen.width - width - 8f);
        var y = _nativePopupAnchor.yMax + 4f;
        if (y + height > Screen.height - 8f)
            y = _nativePopupAnchor.yMin - height - 4f;
        y = Mathf.Clamp(y, 8f, Mathf.Max(8f, Screen.height - height - 8f));
        _nativePopupRect = new Rect(x, y, width, height);
        GUI.Window(904523, _nativePopupRect, _ =>
        {
            var viewport = new Rect(12f, 12f, width - 24f, height - 24f);
            var contentWidth = viewport.width - (contentHeight > viewport.height ? 22f : 0f);
            var scroll = GUI.BeginScrollView(viewport, new Vector2(0f, _nativePopupScrollOffset),
                new Rect(0f, 0f, contentWidth, contentHeight), false, false);
            _nativePopupScrollOffset = scroll.y;
            for (var optionIndex = 0; optionIndex < labels.Length; optionIndex++)
            {
                var row = new Rect(0f, optionIndex * (PopupRowHeight + PopupRowSpacing), contentWidth, PopupRowHeight);
                if (NativeButtonAt(row, labels[optionIndex], optionIndex == selected ? _templateActiveStyle! : _templateStyle!))
                {
                    _nativePopupSelectedIndex = optionIndex;
                    _queuedNativePopupSelection = key;
                    CloseNativePopup();
                }
            }

            GUI.EndScrollView();
        }, GUIContent.none, _cardStyle!);
    }

    /// <summary>Closes the floating dropdown and clears its active option state.</summary>
    private void CloseNativePopup()
    {
        _nativePopupOpen = null;
    }

    /// <summary>Closes the frontmost editor overlay without hiding the underlying draft.</summary>
    internal bool TryCloseOverlay()
    {
        if (_nativePopupOpen != null)
        {
            CloseNativePopup();
            return true;
        }
        if (!_helpOpen) return false;
        _pendingHelpClose = true;
        return true;
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
