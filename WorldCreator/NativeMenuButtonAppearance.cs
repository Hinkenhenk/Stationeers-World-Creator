using System;
using System.Collections.Generic;
using TMPro;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace WorldCreator;

/// <summary>Reads the installed game's small-button artwork for use by the editor's IMGUI controls.</summary>
internal sealed class NativeMenuButtonAppearance : IDisposable
{
    internal static NativeMenuButtonAppearance? Current { get; private set; }
    internal static NativeMenuButtonAppearance? Back { get; private set; }
    internal static NativeMenuButtonAppearance? Load { get; private set; }
    internal Vector2 Size { get; private set; } = new Vector2(64, 64);
    private readonly List<Texture2D> _textures = new List<Texture2D>();
    private readonly Texture2D _normal;
    private readonly Texture2D _hover;
    private readonly Texture2D _pressed;
    private readonly Texture2D _selected;
    private readonly RectOffset _border;
    private readonly Font? _font;

    /// <summary>Copies the native button's sprite and transition states without modifying its assets.</summary>
    private NativeMenuButtonAppearance(Button button, bool backButton = false)
    {
        try
        {
            var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (image == null || image.sprite == null)
                throw new InvalidOperationException("The main-menu small button has no background sprite.");
            var colors = button.colors;
            var states = button.spriteState;
            var swap = button.transition == Selectable.Transition.SpriteSwap;
            var tint = button.transition == Selectable.Transition.ColorTint;
            if (backButton)
            {
                var canvas = button.GetComponentInParent<Canvas>();
                Size = ((RectTransform)button.transform).rect.size * (canvas != null ? canvas.scaleFactor : 1f);
            }
            var height = backButton ? Mathf.Max(1, Mathf.RoundToInt(Size.y)) : 34;
            _normal = CopySprite(image.sprite, image.color * (tint ? colors.normalColor * colors.colorMultiplier : Color.white), height, backButton);
            _hover = CopySprite(swap && states.highlightedSprite != null ? states.highlightedSprite : image.sprite,
                image.color * (tint ? colors.highlightedColor * colors.colorMultiplier : Color.white), height, backButton);
            _pressed = CopySprite(swap && states.pressedSprite != null ? states.pressedSprite : image.sprite,
                image.color * (tint ? colors.pressedColor * colors.colorMultiplier : Color.white), height, backButton);
            _selected = CopySprite(swap && states.selectedSprite != null ? states.selectedSprite : image.sprite,
                image.color * (tint ? colors.selectedColor * colors.colorMultiplier : Color.white), height, backButton);
            var border = image.sprite.border;
            var scale = _normal.height / image.sprite.textureRect.height;
            _border = new RectOffset(Mathf.RoundToInt(border.x * scale), Mathf.RoundToInt(border.z * scale),
                Mathf.RoundToInt(border.w * scale), Mathf.RoundToInt(border.y * scale));
            _font = button.GetComponentInChildren<TMP_Text>(true)?.font?.sourceFontFile;
            Debug.Log("[World Creator] Using native " + (backButton ? "Back " + Size : "small-button") + " sprite '" + image.sprite.name + "'.");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Reads the New Game page's existing Back button, including its screen-pixel dimensions.</summary>
    internal static void CaptureBack()
    {
        if (Back != null)
            return;
        try
        {
            var menu = NewWorldMenu.Instance;
            var button = AccessTools.Field(typeof(UI.MainMenuPage), "_backButton")?.GetValue(menu) as Button;
            if (button == null)
                throw new InvalidOperationException("The New Game Back button is unavailable.");
            Back = new NativeMenuButtonAppearance(button, true);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[World Creator] Native Back button unavailable: " + exception.Message);
        }
    }

    /// <summary>Captures native styling separately so an unavailable skin cannot break the creator's entry point.</summary>
    internal static void Capture(Button? button)
    {
        try
        {
            if (button == null)
                throw new InvalidOperationException("The main-menu small button could not be found.");
            var appearance = new NativeMenuButtonAppearance(button);
            Current?.Dispose();
            Current = appearance;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[World Creator] Native small-button styling unavailable; using the GUI skin. " + exception.Message);
        }
    }

    /// <summary>Reads the native Load Game prefab's row background and green Load button without opening a save.</summary>
    internal static void ApplyLoadStyles(GUIStyle rowStyle, GUIStyle nameStyle, GUIStyle loadStyle)
    {
        try
        {
            var page = Assets.Scripts.UI.MainMenu.Instance.PageManager.GetComponentInChildren<UI.LoadGame.LoadGamePage>(true);
            var prefab = AccessTools.Field(typeof(UI.LoadGame.LoadGamePage), "_worldItemPrefab")
                .GetValue(page) as UI.LoadGame.WorldItem;
            var image = AccessTools.Field(typeof(UI.LoadGame.WorldItem), "_backgroundImage").GetValue(prefab) as Image;
            var button = AccessTools.Field(typeof(UI.LoadGame.WorldItem), "_loadLatestButton").GetValue(prefab) as Button;
            var title = AccessTools.Field(typeof(UI.LoadGame.WorldItem), "_titleText").GetValue(prefab) as TMP_Text;
            if (image == null || image.sprite == null || button == null)
                throw new InvalidOperationException("The Load Game row artwork is unavailable.");
            Load ??= new NativeMenuButtonAppearance(button);
            Load.Apply(loadStyle, false);
            rowStyle.normal.background = Load.CopySprite(image.sprite, image.color, 80, false);
            var scale = rowStyle.normal.background.height / image.sprite.textureRect.height;
            var border = image.sprite.border;
            rowStyle.border = new RectOffset(Mathf.RoundToInt(border.x * scale), Mathf.RoundToInt(border.z * scale),
                Mathf.RoundToInt(border.w * scale), Mathf.RoundToInt(border.y * scale));
            if (title?.font?.sourceFontFile != null)
                nameStyle.font = title.font.sourceFontFile;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[World Creator] Native Load Game styling unavailable: " + exception.Message);
        }
    }

    /// <summary>Applies the game's actual button backgrounds and font while preserving each control's layout.</summary>
    internal void Apply(GUIStyle style, bool emphasized)
    {
        if (_font != null)
            style.font = _font;
        style.border = new RectOffset(_border.left, _border.right, _border.top, _border.bottom);
        style.normal.background = emphasized ? _selected : _normal;
        style.hover.background = _hover;
        style.active.background = _pressed;
        style.focused.background = _selected;
        style.onNormal.background = _selected;
        style.onHover.background = _hover;
        style.onActive.background = _pressed;
        style.onFocused.background = _selected;
    }

    /// <summary>Reads a sprite's atlas rectangle through the GPU, including textures marked non-readable by the game.</summary>
    private Texture2D CopySprite(Sprite sprite, Color tint, int height, bool preservePadding)
    {
        var atlas = sprite.texture;
        var rect = preservePadding && !sprite.packed ? sprite.rect : sprite.textureRect;
        // Native sprites use much larger pixel borders than our compact controls; scale both together.
        var width = Mathf.Max(1, Mathf.RoundToInt(rect.width * height / rect.height));
        var previous = RenderTexture.active;
        var previousSrgbWrite = GL.sRGBWrite;
        var buffer = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        Texture2D? copy = null;
        try
        {
            // IMGUI disables sRGB writes; encode the copied artwork for its sRGB Texture2D regardless of the caller.
            GL.sRGBWrite = buffer.sRGB;
            Graphics.Blit(atlas, buffer, new Vector2(rect.width / atlas.width, rect.height / atlas.height),
                new Vector2(rect.x / atlas.width, rect.y / atlas.height));
            RenderTexture.active = buffer;
            copy = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "World Creator copy of " + sprite.name,
                wrapMode = TextureWrapMode.Clamp
            };
            copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            var pixels = copy.GetPixels();
            for (var index = 0; index < pixels.Length; index++)
                pixels[index] *= tint;
            copy.SetPixels(pixels);
            copy.Apply(false, true);
            _textures.Add(copy);
            return copy;
        }
        catch
        {
            if (copy != null)
                UnityEngine.Object.Destroy(copy);
            throw;
        }
        finally
        {
            GL.sRGBWrite = previousSrgbWrite;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(buffer);
        }
    }

    /// <summary>Releases only the mod's runtime texture copies, leaving game-owned sprites untouched.</summary>
    public void Dispose()
    {
        foreach (var texture in _textures)
            UnityEngine.Object.Destroy(texture);
        _textures.Clear();
        if (Current == this)
            Current = null;
        if (Back == this)
            Back = null;
        if (Load == this)
            Load = null;
    }
}
