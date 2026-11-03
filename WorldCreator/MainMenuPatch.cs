using System;
using System.IO;
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
            GameObject? buttonObject = null;
            try
            {
                var sourceField = AccessTools.Field(typeof(Assets.Scripts.UI.MainMenu), "_joinServerButton");
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

                buttonObject = UnityEngine.Object.Instantiate(sourceButton.gameObject, sourceButton.transform.parent);
                buttonObject.name = ButtonObjectName;
                buttonObject.transform.SetSiblingIndex(sourceButton.transform.GetSiblingIndex() + 1);
                buttonObject.SetActive(true);

                var artwork = buttonObject.AddComponent<MainMenuArtwork>();
                artwork.Load(buttonObject);

                foreach (var localized in buttonObject.GetComponentsInChildren<Assets.Scripts.UI.LocalizedText>(true))
                    localized.StringKey = string.Empty;

                var label = buttonObject.transform.Find("Title")?.GetComponent<TMP_Text>();
                if (label != null)
                    label.text = "Create World";
                var subtitle = buttonObject.transform.Find("SubTitle");
                if (subtitle != null)
                    subtitle.gameObject.SetActive(false);

                var button = buttonObject.GetComponent<Button>();
                button.onClick = new Button.ButtonClickedEvent();
                button.interactable = true;
                button.onClick.AddListener(() => EditorWindow.Toggle(canvas));

                var grid = sourceButton.transform.parent.GetComponent<GridLayoutGroup>();
                if (grid != null)
                {
                    var smallButtons = grid.transform.parent.Find("SmallButtonList") as RectTransform;
                    var list = (RectTransform)grid.transform;
                    if (smallButtons != null)
                    {
                        // Reserve the inactive Continue slot too, so it can appear without overlap.
                        var rows = grid.transform.childCount;
                        var available = list.anchoredPosition.y - smallButtons.anchoredPosition.y - 20f;
                        var height = (available - grid.padding.vertical - grid.spacing.y * (rows - 1)) / rows;
                        if (height > 0f)
                            grid.cellSize = new Vector2(grid.cellSize.x, Mathf.Min(grid.cellSize.y, height));
                    }
                    LayoutRebuilder.MarkLayoutForRebuild((RectTransform)grid.transform);
                }
            }
            catch (Exception exception)
            {
                if (buttonObject != null)
                    UnityEngine.Object.Destroy(buttonObject);
                Debug.LogError("[World Creator] Main-menu button setup failed: " + exception);
            }
        }
    }

    internal sealed class MainMenuArtwork : MonoBehaviour
    {
        private Texture2D? _texture;
        private Sprite? _sprite;

        internal void Load(GameObject buttonObject)
        {
            var frame = buttonObject.GetComponent<Image>();
            if (frame == null)
                throw new InvalidOperationException("The native banner has no frame image.");
            var path = Path.Combine(Application.streamingAssetsPath, "Interface", "preview_leo.png");
            _texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!_texture.LoadImage(File.ReadAllBytes(path)))
                throw new InvalidDataException("Cannot decode " + path);
            _sprite = Sprite.Create(_texture, new Rect(0, 0, _texture.width, _texture.height), new Vector2(0.5f, 0.5f));

            // Keep the game's rounded frame and use its alpha as the artwork mask.
            var mask = buttonObject.GetComponent<UnityEngine.UI.Mask>() ?? buttonObject.AddComponent<UnityEngine.UI.Mask>();
            mask.showMaskGraphic = true;
            var background = new GameObject("WorldCreatorArtwork", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            background.GetComponent<LayoutElement>().ignoreLayout = true;
            background.layer = buttonObject.layer;
            background.transform.SetParent(buttonObject.transform, false);
            background.transform.SetAsFirstSibling();
            var rect = (RectTransform)background.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 8f);
            rect.offsetMax = new Vector2(-8f, -8f);
            var image = background.GetComponent<Image>();
            image.sprite = _sprite;
            image.raycastTarget = false;
        }

        private void OnDestroy()
        {
            if (_sprite != null) Destroy(_sprite);
            if (_texture != null) Destroy(_texture);
        }
    }
}
