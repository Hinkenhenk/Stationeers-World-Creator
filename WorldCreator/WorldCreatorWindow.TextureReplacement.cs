using System;
using System.Globalization;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private string _replacementRgb = "128,128,128";
    private string _replacementSize = "512";

    /// <summary>Shows a live colour swatch and bounded image-size input beside the exact missing-texture error.</summary>
    private void DrawMissingTextureReplacement()
    {
        GUILayout.Space(12);
        GUILayout.Label("Create a solid-colour replacement", _sectionStyle!);
        GUILayout.Label("Every pixel will use this colour. Roughness uses brightness; a flat normal is usually 128,128,255. Masks will fill the whole image with one value. Match the size of paired textures such as albedo and emissive.", _mutedStyle!);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(180));
        GUILayout.Label("RGB (0–255)", _mutedStyle!);
        _replacementRgb = GUILayout.TextField(_replacementRgb, _inputStyle!, GUILayout.Height(32));
        GUILayout.EndVertical();
        GUILayout.Space(12);
        var validColour = MapRgbValue.TryParse(_replacementRgb, out var rgb);
        GUILayout.BeginVertical(GUILayout.Width(32));
        GUILayout.Space(22);
        var swatch = GUILayoutUtility.GetRect(32, 32, GUILayout.Width(32), GUILayout.Height(32));
        if (validColour && Event.current.type == EventType.Repaint)
        {
            var colour = GUI.color;
            GUI.color = new Color32(rgb.R, rgb.G, rgb.B, 255);
            GUI.DrawTexture(new Rect(swatch.x, swatch.yMax - 32, 32, 32), Texture2D.whiteTexture);
            GUI.color = colour;
        }
        GUILayout.EndVertical();
        GUILayout.Space(12);
        GUILayout.BeginVertical(GUILayout.Width(140));
        GUILayout.Label("Size (px, square)", _mutedStyle!);
        _replacementSize = GUILayout.TextField(_replacementSize, _inputStyle!, GUILayout.Height(32));
        GUILayout.EndVertical();
        GUILayout.Space(16);
        GUILayout.BeginVertical();
        GUILayout.Space(22);
        var validSize = int.TryParse(_replacementSize, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size) && size >= 16 && size <= 4096 && (size & (size - 1)) == 0;
        var enabled = GUI.enabled;
        GUI.enabled = enabled && validColour && validSize;
        if (NativeButton("Generate and use texture", _buttonStyle!, GUILayout.Width(230), GUILayout.Height(32))) GenerateMissingTexture(rgb, size);
        GUI.enabled = enabled;
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        if (!validColour || !validSize) GUILayout.Label("Use three RGB numbers from 0 to 255 and a power-of-two size from 16 to 4096 (for example 512).", _mutedStyle!);
    }

    /// <summary>Encodes the chosen opaque colour, assigns it to the missing field and releases the temporary Unity texture.</summary>
    private void GenerateMissingTexture(MapRgbValue rgb, int size)
    {
        Texture2D? texture = null;
        try
        {
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            var colour = new Color32(rgb.R, rgb.G, rgb.B, 255);
            for (var i = 0; i < pixels.Length; i++) pixels[i] = colour;
            texture.SetPixels32(pixels);
            texture.Apply();
            _project!.ReplaceMissingTexture(_exportIssueField!, texture.EncodeToPNG());
            _status = "Generated a solid-colour PNG and assigned it to " + _exportIssueName + ". Export again to check the remaining references.";
            ClearExportIssue();
        }
        catch (Exception error) { _status = "Could not generate texture: " + error.Message; }
        finally { if (texture != null) Destroy(texture); }
    }
}
