using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    /// <summary>Returns the shared page width after navigation, padding and the vertical scrollbar.</summary>
    private float PageContentWidth => Mathf.Max(1f,
        _window.width - _windowStyle!.padding.horizontal - SidebarWidth - CardSpacing - 20f - 30f
        - ScrollContentRightPadding - GUI.skin.verticalScrollbar.fixedWidth);

    /// <summary>Stacks cards independently in bounded columns so short cards do not wait for neighboring tall cards.</summary>
    private bool DrawCardColumns<T>(IReadOnlyList<T> cards, Func<T, bool> draw, float inset = 0,
        float minimumWidth = 440, float maximumWidth = 720)
    {
        var available = Mathf.Max(1f, PageContentWidth - inset);
        var columns = Mathf.Clamp(Mathf.FloorToInt((available + CardSpacing) / (minimumWidth + CardSpacing)), 1, 3);
        var width = Mathf.Min(Mathf.Max(minimumWidth, maximumWidth), (available - CardSpacing * (columns - 1)) / columns);
        GUILayout.BeginHorizontal();
        for (var column = 0; column < columns && column < cards.Count; column++)
        {
            if (column > 0)
                GUILayout.Space(CardSpacing);
            GUILayout.BeginVertical(GUILayout.Width(width));
            for (var index = column; index < cards.Count; index += columns)
            {
                if (draw(cards[index]))
                {
                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                    return true;
                }
                GUILayout.Space(CardSpacing);
            }
            GUILayout.EndVertical();
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        return false;
    }
}
