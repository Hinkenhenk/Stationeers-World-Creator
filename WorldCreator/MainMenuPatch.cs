using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using HarmonyLib;

namespace WorldCreator
{
    [HarmonyPatch(typeof(Assets.Scripts.UI.MainMenu), "Start")]
    internal static class MainMenuPatch
    {
        private const string ButtonObjectName = "WorldCreatorMenuButton";

        private static void Postfix(Assets.Scripts.UI.MainMenu __instance)
        {
            try
            {
                var sourceField = AccessTools.Field(typeof(Assets.Scripts.UI.MainMenu), "_scenarioButton");
                var sourceButton = sourceField?.GetValue(__instance) as Button;
                if (sourceButton == null)
                {
                    Debug.LogError("[World Creator] Could not find the main-menu button template.");
                    return;
                }
                EditorWindow.NativeFont = sourceButton.GetComponentInChildren<TMP_Text>(true)?.font?.sourceFontFile;
                if (sourceButton.transform.parent.Find(ButtonObjectName) != null)
                    return;

                var canvas = sourceButton.GetComponentInParent<Canvas>();
                if (canvas == null)
                {
                    Debug.LogError("[World Creator] Could not find the main-menu canvas.");
                    return;
                }

                var buttonObject = UnityEngine.Object.Instantiate(sourceButton.gameObject, sourceButton.transform.parent);
                buttonObject.name = ButtonObjectName;
                buttonObject.transform.SetSiblingIndex(sourceButton.transform.GetSiblingIndex() + 1);
                buttonObject.SetActive(true);

                var label = buttonObject.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.text = "World Creator";

                var button = buttonObject.GetComponent<Button>();
                button.onClick = new Button.ButtonClickedEvent();
                button.interactable = true;
                button.onClick.AddListener(() => EditorWindow.Toggle(canvas));

                var grid = sourceButton.transform.parent.GetComponent<GridLayoutGroup>();
                if (grid != null)
                {
                    var size = grid.cellSize;
                    size.y *= 0.65f;
                    grid.cellSize = size;
                    LayoutRebuilder.MarkLayoutForRebuild((RectTransform)grid.transform);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("[World Creator] Main-menu button setup failed: " + exception);
            }
        }
    }
}
