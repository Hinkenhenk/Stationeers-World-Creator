using System.Collections.Generic;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private bool _stylesReady;
    private Texture2D? _backgroundTexture;
    private Texture2D? _sidebarTexture;
    private Texture2D? _panelTexture;
    private Texture2D? _raisedTexture;
    private GUIStyle? _sidebarStyle;
    private GUIStyle? _cardStyle;
    private GUIStyle? _titleStyle;
    private GUIStyle? _sectionStyle;
    private GUIStyle? _mutedStyle;
    private GUIStyle? _navStyle;
    private GUIStyle? _templateStyle;
    private GUIStyle? _templateActiveStyle;
    private GUIStyle? _rowStyle;
    private GUIStyle? _inputStyle;
    private GUIStyle? _multilineStyle;
    private GUIStyle? _buttonStyle;
    private GUIStyle? _backButtonStyle;
    private GUIStyle? _primaryButtonStyle;
    private GUIStyle? _windowStyle;
    private GUIStyle? _entryWindowStyle;
    private GUIStyle? _worldChoiceStyle;
    private GUIStyle? _worldNameStyle;
    private GUIStyle? _loadWorldStyle;
    private GUIStyle? _dialogStyle;
    private GUIStyle? _tooltipStyle;
    private readonly List<Texture2D> _styleTextures = new List<Texture2D>();
    /// <summary>Initializes the editor's shared fonts, colors, controls and card styles once.</summary>
    private void EnsureStyles()
    {
        if (_stylesReady)
            return;
        _backgroundTexture = CreateTexture(new Color(0, 0, 0, 0));
        _sidebarTexture = CreateRoundedTexture(Sidebar, new Color(0.31f, 0.30f, 0.34f));
        _panelTexture = CreateRoundedTexture(Panel, new Color(0.34f, 0.33f, 0.37f));
        _raisedTexture = CreateRoundedTexture(PanelRaised, new Color(0.30f, 0.29f, 0.33f));
        var fieldTexture = CreateRoundedTexture(PanelRaised, new Color(0.34f, 0.33f, 0.37f), 5);
        var rowTexture = CreateRoundedTexture(PanelRaised, new Color(0.34f, 0.33f, 0.37f), 6);
        var uiScale = Mathf.Clamp(Screen.height / 1440f, 0.75f, 1.35f);
        _windowStyle = new GUIStyle(GUI.skin.window)
        {
            normal =
            {
                background = _panelTexture,
                textColor = Color.white
            },
            onNormal =
            {
                background = _panelTexture,
                textColor = Color.white
            },
            border = new RectOffset(10, 10, 10, 10),
            padding = new RectOffset(12, 12, 18, 12)
        };
        _entryWindowStyle = new GUIStyle(GUIStyle.none) { padding = new RectOffset(16, 16, 16, 16) };
        _dialogStyle = new GUIStyle(_windowStyle)
        {
            normal =
            {
                background = _panelTexture,
                textColor = Color.white
            },
            onNormal =
            {
                background = _panelTexture,
                textColor = Color.white
            },
            border = new RectOffset(12, 12, 12, 12),
            padding = new RectOffset(18, 18, 18, 14),
            fontSize = Mathf.RoundToInt(18 * uiScale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter
        };
        _sidebarStyle = new GUIStyle(GUI.skin.box)
        {
            normal =
            {
                background = _sidebarTexture
            },
            border = new RectOffset(10, 10, 10, 10),
            padding = new RectOffset(14, 14, 16, 14),
            margin = new RectOffset(0, 8, 0, 0),
            alignment = TextAnchor.UpperLeft
        };
        var insetCardTexture = CreateInsetCardTexture(PanelRaised, new Color(0.34f, 0.33f, 0.37f));
        _cardStyle = new GUIStyle(GUI.skin.box)
        {
            normal =
            {
                background = insetCardTexture
            },
            border = new RectOffset(9, 9, 9, 9),
            padding = new RectOffset(15, 15, 12, 12),
            margin = new RectOffset(0, 0, 0, 0)
        };
        _rowStyle = new GUIStyle(GUI.skin.box)
        {
            normal =
            {
                background = rowTexture
            },
            border = new RectOffset(6, 6, 6, 6),
            padding = new RectOffset(10, 10, 7, 7),
            margin = new RectOffset(0, 0, 3, 3)
        };
        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(27 * uiScale),
            fontStyle = FontStyle.Bold,
            normal =
            {
                textColor = Color.white
            },
            margin = new RectOffset(0, 0, 0, 4)
        };
        _sectionStyle = new GUIStyle(GUI.skin.label)
        {
            wordWrap = true,
            fontSize = Mathf.RoundToInt(15 * uiScale),
            fontStyle = FontStyle.Bold,
            normal =
            {
                textColor = Accent
            },
            margin = new RectOffset(0, 0, 0, 3)
        };
        _mutedStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(14 * uiScale),
            wordWrap = true,
            normal =
            {
                textColor = Muted
            }
        };
        _navStyle = MakeButtonStyle(Color.white, PanelRaised, TextAnchor.MiddleLeft);
        _templateStyle = MakeButtonStyle(new Color(0.82f, 0.87f, 0.88f), Panel, TextAnchor.MiddleLeft);
        _templateActiveStyle = MakeButtonStyle(Color.white, MenuBlue, TextAnchor.MiddleLeft);
        _buttonStyle = MakeButtonStyle(Color.white, PanelRaised, TextAnchor.MiddleCenter);
        _backButtonStyle = new GUIStyle(_buttonStyle) { padding = new RectOffset(), margin = new RectOffset() };
        NativeMenuButtonAppearance.Back?.Apply(_backButtonStyle, false);
        _primaryButtonStyle = MakeButtonStyle(Color.white, MenuBlue, TextAnchor.MiddleCenter);
        _worldChoiceStyle = new GUIStyle(_rowStyle) { padding = new RectOffset(20, 20, 10, 10) };
        _worldNameStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(24 * uiScale), alignment = TextAnchor.MiddleLeft, wordWrap = true,
            normal = { textColor = Color.white }, onNormal = { textColor = Color.white }
        };
        _loadWorldStyle = new GUIStyle(_buttonStyle) { fontSize = Mathf.RoundToInt(23 * uiScale) };
        NativeMenuButtonAppearance.ApplyLoadStyles(_worldChoiceStyle, _worldNameStyle, _loadWorldStyle);
        _inputStyle = new GUIStyle(GUI.skin.textField)
        {
            normal =
            {
                textColor = Color.white,
                background = fieldTexture
            },
            focused =
            {
                textColor = Color.white,
                background = fieldTexture
            },
            border = new RectOffset(5, 5, 5, 5),
            padding = new RectOffset(9, 9, 6, 6),
            fontSize = Mathf.RoundToInt(15 * uiScale)
        };
        _multilineStyle = new GUIStyle(_inputStyle)
        {
            wordWrap = true,
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(9, 9, 8, 8)
        };
        _tooltipStyle = new GUIStyle(GUI.skin.box)
        {
            normal =
            {
                background = _raisedTexture,
                textColor = Color.white
            },
            border = new RectOffset(8, 8, 8, 8),
            wordWrap = true,
            alignment = TextAnchor.UpperLeft,
            fontSize = Mathf.RoundToInt(13 * uiScale),
            padding = new RectOffset(10, 10, 8, 8)
        };
        _stylesReady = true;
    }

    /// <summary>Creates the shared button states used by editor actions and navigation.</summary>
    private GUIStyle MakeButtonStyle(Color text, Color fill, TextAnchor alignment)
    {
        var uiScale = Mathf.Clamp(Screen.height / 1440f, 0.75f, 1.35f);
        var style = new GUIStyle(GUI.skin.button)
        {
            alignment = alignment,
            fontSize = Mathf.RoundToInt(17 * uiScale),
            fontStyle = FontStyle.Normal,
            border = new RectOffset(8, 8, 8, 8),
            padding = new RectOffset(10, 10, 6, 6),
            margin = new RectOffset(2, 2, 2, 2)
        };
        NativeMenuButtonAppearance.Current?.Apply(style, fill == MenuBlue);
        style.normal.textColor = text;
        style.hover.textColor = Color.white;
        style.active.textColor = Color.white;
        style.focused.textColor = Color.white;
        return style;
    }

    /// <summary>Creates a solid-color runtime texture owned by the editor.</summary>
    private Texture2D CreateTexture(Color color)
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        _styleTextures.Add(texture);
        return texture;
    }

    /// <summary>Creates a rounded panel texture with the requested fill and border colors.</summary>
    private Texture2D CreateRoundedTexture(Color fill, Color outline, int radius = 12)
    {
        const int size = 32;
        const float outlineWidth = 1.5f;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var cx = Mathf.Clamp(x, radius, size - radius - 1);
                var cy = Mathf.Clamp(y, radius, size - radius - 1);
                var distance = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                var edge = radius - distance;
                pixels[y * size + x] = edge < 0 ? Color.clear : edge <= outlineWidth ? outline : fill;
            }

        texture.SetPixels(pixels);
        texture.Apply();
        _styleTextures.Add(texture);
        return texture;
    }

    /// <summary>Builds the recessed card background used to group settings.</summary>
    private Texture2D CreateInsetCardTexture(Color fill, Color outline)
    {
        const int size = 32;
        const int radius = 7;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "World Creator recessed card",
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color[size * size];
        var inner = Color.Lerp(fill, Sidebar, .25f);
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var cx = Mathf.Clamp(x, radius, size - radius - 1);
                var cy = Mathf.Clamp(y, radius, size - radius - 1);
                var edge = radius - Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                if (edge < 0)
                {
                    pixels[y * size + x] = Color.clear;
                    continue;
                }

                var color = inner;
                if (edge <= 1.5f)
                    color = outline;
                else if (y >= size - 4 || x <= 2)
                    color = Color.Lerp(inner, Color.white, .08f);
                else if (y <= 2 || x >= size - 3)
                    color = Color.Lerp(inner, Color.black, .22f);
                pixels[y * size + x] = color;
            }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        _styleTextures.Add(texture);
        return texture;
    }
}
