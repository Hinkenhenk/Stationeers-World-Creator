using UnityEngine;
using System;

namespace WorldCreator;

internal static class EditorWindow
{
    internal static Font? NativeFont { get; set; }

    private static WorldCreatorMenuPage? _page;
    /// <summary>Opens the creator through the game's page manager, hiding the previous menu normally.</summary>
    internal static void Toggle(Canvas canvas)
    {
        if (_page != null && _page.IsVisible)
            return;
        try
        {
            NativeMenuButtonAppearance.CaptureBack();
            if (_page == null)
                _page = WorldCreatorMenuPage.Create(NewWorldMenu.Instance);
            Assets.Scripts.UI.MainMenu.Instance.PageManager.EnableMainMenuPage(_page.WindowName);
        }
        catch (Exception exception)
        {
            Debug.LogError("[World Creator] Could not open the creator menu: " + exception);
        }
    }

    /// <summary>Returns to the previous native menu without discarding the editor's draft.</summary>
    internal static void Hide()
    {
        if (_page != null && _page.IsVisible)
            Assets.Scripts.UI.MainMenu.Instance.PageManager.DisableMainMenuPage(_page.WindowName);
    }

    /// <summary>Uses the native shell for entry screens and the larger workspace for editing.</summary>
    internal static Rect PreparePage(int screen) => _page!.Prepare(screen);
}
