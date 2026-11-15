using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WorldCreator;

/// <summary>Hosts the editor in Stationeers' menu stack using Select World's actual assets and anchors.</summary>
internal sealed class WorldCreatorMenuPage : UI.MainMenuPage
{
    private GameObject _frame = null!;
    private RectTransform _content = null!;
    private TMP_Text _title = null!;
    private WorldCreatorWindow _editor = null!;
    private readonly Vector3[] _corners = new Vector3[4];

    /// <summary>Builds an inactive native page before registration so failed initialization cannot obscure the main menu.</summary>
    internal static WorldCreatorMenuPage Create(NewWorldMenu source)
    {
        if (source == null)
            throw new InvalidOperationException("The Select World page is unavailable.");
        var root = new GameObject("WorldCreatorPage", typeof(RectTransform));
        root.SetActive(false);
        try
        {
            root.transform.SetParent(source.transform.parent, false);
            CopyRect((RectTransform)source.transform, (RectTransform)root.transform);
            var page = root.AddComponent<WorldCreatorMenuPage>();
            AccessTools.Field(typeof(UI.MainMenuPage), "windowName").SetValue(page, "WorldCreator");
            page.MenuSceneShouldBeVisible = true;
            page.BuildFrame(source);
            var editor = new GameObject("Editor", typeof(RectTransform));
            editor.transform.SetParent(root.transform, false);
            page._editor = editor.AddComponent<WorldCreatorWindow>();
            Assets.Scripts.UI.MainMenu.Instance.PageManager.RegisterPage(page);
            return page;
        }
        catch
        {
            Destroy(root);
            throw;
        }
    }

    /// <summary>Copies presentation alone, avoiding NewWorldMenu scripts that populate or select game worlds.</summary>
    private void BuildFrame(NewWorldMenu source)
    {
        var window = source.transform.Find("MainWindow");
        if (window == null)
            throw new InvalidOperationException("The Select World frame is unavailable.");
        _frame = CopyImage(window, transform).gameObject;
        CopyImage(window.Find("Header"), _frame.transform);
        _content = (RectTransform)CopyImage(window.Find("NewWorldPanel"), _frame.transform).transform;
        var titleObject = Instantiate(window.Find("PageTitle").gameObject, _frame.transform, false);
        foreach (var text in titleObject.GetComponentsInChildren<Assets.Scripts.UI.LocalizedText>(true))
            text.StringKey = string.Empty;
        _title = titleObject.GetComponent<TMP_Text>();
        var backObject = Instantiate(window.Find("BackButton").gameObject, _frame.transform, false);
        var back = backObject.GetComponent<Button>();
        back.onClick = new Button.ButtonClickedEvent();
        back.onClick.AddListener(() => _editor.BackFromMenu());
    }

    /// <summary>Preserves the native header and Back anchor while converting its content area to IMGUI coordinates.</summary>
    internal Rect Prepare(int screen)
    {
        _frame.SetActive(screen != 3);
        _title.text = screen == 2 ? "Load World" : "World Creator";
        _content.GetWorldCorners(_corners);
        var canvas = GetComponentInParent<Canvas>();
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, _corners[0]);
        var topRight = RectTransformUtility.WorldToScreenPoint(camera, _corners[2]);
        return new Rect(bottomLeft.x, Screen.height - topRight.y, topRight.x - bottomLeft.x, topRight.y - bottomLeft.y);
    }

    /// <summary>Copies a native image and layout without its game logic, lists or event handlers.</summary>
    private static Image CopyImage(Transform source, Transform parent)
    {
        if (source == null || source.GetComponent<Image>() == null)
            throw new InvalidOperationException("A Select World panel image is unavailable.");
        var original = source.GetComponent<Image>();
        var copy = new GameObject(source.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        copy.layer = source.gameObject.layer;
        copy.transform.SetParent(parent, false);
        CopyRect((RectTransform)source, (RectTransform)copy.transform);
        var image = copy.GetComponent<Image>();
        image.sprite = original.sprite;
        image.color = original.color;
        image.type = original.type;
        image.preserveAspect = original.preserveAspect;
        image.fillCenter = original.fillCenter;
        image.pixelsPerUnitMultiplier = original.pixelsPerUnitMultiplier;
        return image;
    }

    /// <summary>Keeps canvas scaling and resolution changes consistent with Select World.</summary>
    private static void CopyRect(RectTransform source, RectTransform target)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.sizeDelta = source.sizeDelta;
        target.anchoredPosition3D = source.anchoredPosition3D;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
    }
}
