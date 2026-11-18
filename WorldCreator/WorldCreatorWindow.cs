using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow : MonoBehaviour
{
    private const float CardSpacing = 20f;
    private const float SidebarWidth = 220f;
    private const float ScrollContentRightPadding = 36f;
    private static readonly string[] Names =
    {
        "Blank world (no template)",
        "Mars",
        "Moon",
        "Europa",
        "Mimas",
        "Venus",
        "Vulcan"
    };
    private static readonly string[] Files =
    {
        "",
        "Mars2/Mars2.xml",
        "Lunar/Lunar.xml",
        "Europa/Europa.xml",
        "Mimas/MimasHerschel.xml",
        "Venus/Venus.xml",
        "Vulcan/VulcanV2.xml"
    };
    private static readonly string[] Pages = EditorHelp.Titles;
    private static readonly string[] AssetCategories =
    {
        "Surface",
        "Underground",
        "Atmosphere / Sky",
        "Map / UI",
        "Other"
    };
    private static readonly Color Sidebar = new Color(0.102f, 0.098f, 0.114f);
    private static readonly Color Panel = new Color(0.192f, 0.184f, 0.208f);
    private static readonly Color PanelRaised = new Color(0.145f, 0.137f, 0.157f);
    private static readonly Color Accent = new Color(1f, 0.39f, 0.13f);
    private static readonly Color MenuBlue = new Color(0.075f, 0.29f, 0.48f);
    private static readonly Color Muted = new Color(0.72f, 0.70f, 0.73f);
    private static readonly Color TemperatureOrange = new Color(1f, 0.48f, 0.22f);
    private Rect _window;
    private Vector2 _scroll;
    private int _template;
    private int _page;
    private static readonly string[] AssetTypeFilters =
    {
        "Textures",
        "Terrain data",
        "All"
    };
    private static readonly string[] StartEvents =
    {
        "NewWorld",
        "NewPlayer",
        "RespawnPlayer",
        "NewPlayerKit",
        "RespawnPlayerKit"
    };
    private static readonly string[] StartEventLabels =
    {
        "World",
        "Player",
        "Respawn",
        "New-player kit",
        "Respawn kit"
    };
    private int _pendingTemplate = -1;
    private string? _pendingLoadPath;
    private readonly List<(string Path, string Name, bool Workshop)> _loadChoices = new List<(string, string, bool)>();
    private int _loadSource;
    private string _loadSearch = "";
    private string? _loadDetailsPath;
    private int _presetSelection = -1;
    private int _screen;
    private int _pendingScreen = -1;
    private int _pendingPage = -1;
    private bool _discardConfirmed;
    private string _id = "WC_MyWorld";
    private string _name = "My World";
    private string _advancedFilter = "";
    private string _status = "Start from a blank world, apply a preset if desired, then create a new mod.";
    private WorldProject? _project;
    /// <summary>Processes editor shortcuts and advances the orbit preview independently of game time.</summary>
    private void Update()
    {
        if (_confirmationBackdrop != null && !Assets.Scripts.UI.PromptPanel.Instance.IsActive)
            ClearConfirmationBackdrop();
        if (!_orbitSimulationPlaying || _screen != 3 || _page != 3 || _project == null)
            return;
        var delta = Time.unscaledDeltaTime;
        if (delta <= 0)
            return;
        var orbitalPeriod = OrbitPeriodDays();
        if (orbitalPeriod > 0)
            _orbitPhase = (_orbitPhase + 360f * _orbitSimulationSpeed * delta / 60f) % 360f;
        var rotationPeriod = RotationPeriodDays();
        if (_orbitView != 3 && rotationPeriod != 0)
            _orbitSpinPhase = (_orbitSpinPhase + 360f * (float)orbitalPeriod * _orbitSimulationSpeed * delta / (60f * (float)Math.Abs(rotationPeriod)) * (rotationPeriod < 0 ? -1f : 1f)) % 360f;
    }

    /// <summary>Recalculates the creator's screen bounds so resolution changes take effect immediately.</summary>
    private void UpdateWindowRect()
    {
        var nativeContent = EditorWindow.PreparePage(_screen);
        if (_screen != 3)
        {
            _window = nativeContent;
            return;
        }
        var margin = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) * 0.025f, 8f, 32f);
        var width = Mathf.Max(1f, Screen.width - margin * 2f);
        var height = Mathf.Max(1f, Screen.height - margin * 2f);
        _window = new Rect(margin, margin, width, height);
    }

    /// <summary>Coordinates one editor UI pass, pending actions, overlays and restoration of the game's GUI skin.</summary>
    private void OnGUI()
    {
        if (_confirmationBackdrop != null)
            return;
        if (Event.current.type == EventType.Layout && _pendingScreen >= 0)
        {
            _screen = _pendingScreen;
            _pendingScreen = -1;
        }
        // Recalculate on every GUI pass so a live resolution change is reflected immediately.
        UpdateWindowRect();
        var skin = GUI.skin;
        var previousSkinFont = skin.font;
        var previousLabelFont = skin.label.font;
        var previousButtonFont = skin.button.font;
        var previousBoxFont = skin.box.font;
        var previousTextFieldFont = skin.textField.font;
        var previousToggleFont = skin.toggle.font;
        var previousWindowFont = skin.window.font;
        if (EditorWindow.NativeFont != null)
        {
            skin.font = EditorWindow.NativeFont;
            skin.label.font = EditorWindow.NativeFont;
            skin.button.font = EditorWindow.NativeFont;
            skin.box.font = EditorWindow.NativeFont;
            skin.textField.font = EditorWindow.NativeFont;
            skin.toggle.font = EditorWindow.NativeFont;
            skin.window.font = EditorWindow.NativeFont;
        }

        EnsureStyles();
        var previousBackground = GUI.backgroundColor;
        GUI.backgroundColor = Color.white;
        _hoverTooltip = null;
        if (Event.current.type == EventType.Layout)
        {
            if (_pendingPage >= 0)
            {
                _page = _pendingPage;
                _pendingPage = -1;
                _scroll = Vector2.zero;
                CloseNativePopup();
            }

            ApplyHelpNavigation();
            ApplyEquipmentChange();
            if (_pendingGasRemoval != null)
            {
                var type = _pendingGasRemoval;
                _pendingGasRemoval = null;
                try
                {
                    _project!.RemoveGas(type);
                    _status = "Removed " + type + " from this world's atmosphere.";
                }
                catch (Exception e)
                {
                    _status = e.Message;
                }
            }
        }

        GUI.Window(904521, _window, DrawWindow, GUIContent.none, _screen == 3 ? _windowStyle! : _entryWindowStyle!);
        if (_helpOpen)
            DrawHelpOverlay();
        if (_pendingTemplate >= 0)
        {
            CloseNativePopup();
            var margin = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) * 0.025f, 8f, 32f);
            var dialogWidth = Mathf.Min(460f, Mathf.Max(1f, Screen.width - margin * 2f));
            var dialogHeight = Mathf.Min(170f, Mathf.Max(1f, Screen.height - margin * 2f));
            GUI.ModalWindow(
                904522,
                new Rect((Screen.width - dialogWidth) / 2f, (Screen.height - dialogHeight) / 2f, dialogWidth, dialogHeight),
                DrawDiscardDialog,
                "Replace current settings?",
                _dialogStyle!
            );
        }

        if (_nativePopupOpen != null)
            DrawNativePopup(_nativePopupOpen, _nativePopupOptions, _nativePopupCurrentSelected);
        GUI.backgroundColor = previousBackground;
        skin.font = previousSkinFont;
        skin.label.font = previousLabelFont;
        skin.button.font = previousButtonFont;
        skin.box.font = previousBoxFont;
        skin.textField.font = previousTextFieldFont;
        skin.toggle.font = previousToggleFont;
        skin.window.font = previousWindowFont;
        if (_discardConfirmed)
        {
            _discardConfirmed = false;
            var nextTemplate = _pendingTemplate;
            var nextLoadPath = _pendingLoadPath;
            _pendingTemplate = -1;
            _pendingLoadPath = null;
            if (nextTemplate >= 0)
                ApplyOriginalWorld(nextTemplate);
            else if (nextLoadPath != null)
                LoadWorld(nextLoadPath);
        }
    }

    /// <summary>Releases textures, previews and runtime rendering objects owned by the editor.</summary>
    private void OnDestroy()
    {
        ClearConfirmationBackdrop();
        if (_terrainPreview != null)
            Destroy(_terrainPreview);
        if (_mapPreview != null)
            Destroy(_mapPreview);
        if (_mapReferencePreview != null)
            Destroy(_mapReferencePreview);
        if (_orbitPlanetSurfaceTexture != null)
            Destroy(_orbitPlanetSurfaceTexture);
        if (_orbitPrimarySurfaceTexture != null)
            Destroy(_orbitPrimarySurfaceTexture);
        if (_orbitSphereMaterial != null)
            Destroy(_orbitSphereMaterial);
        if (_orbitSunRenderTexture != null)
        {
            _orbitSunRenderTexture.Release();
            Destroy(_orbitSunRenderTexture);
        }

        if (_orbitPlanetRenderTexture != null)
        {
            _orbitPlanetRenderTexture.Release();
            Destroy(_orbitPlanetRenderTexture);
        }

        if (_orbitSphereCamera != null)
            Destroy(_orbitSphereCamera.gameObject);
        if (_orbitSphereObject != null)
            Destroy(_orbitSphereObject);
        if (_orbitSphereLight != null)
            Destroy(_orbitSphereLight.gameObject);
        foreach (var layerTexture in _mapLayerPreviews.Values)
            if (layerTexture != null)
                Destroy(layerTexture);
        foreach (var texture in _styleTextures)
            if (texture != null)
                Destroy(texture);
    }

    /// <summary>Routes the current screen to the home, load or editing page.</summary>
    private void DrawWindow(int windowId)
    {
        var editorWasEnabled = GUI.enabled;
        GUI.enabled = editorWasEnabled && !_helpOpen && !(Assets.Scripts.UI.PromptPanel.Instance?.IsActive ?? false);
        if (_screen == 0)
            DrawHome();
        else if (_screen == 2)
            DrawLoadPage();
        else
            DrawEditorPage();
        GUI.enabled = editorWasEnabled;
        // GUI.tooltip is resolved by IMGUI after controls have been drawn.
        // Capture it inside the window callback before its GUI context ends.
        if (!string.IsNullOrWhiteSpace(GUI.tooltip))
            _hoverTooltip = GUI.tooltip;
        if (!_helpOpen && _nativePopupOpen == null && _pendingTemplate < 0 && _pendingLoadPath == null && !_confirmationPending)
            DrawHoverTooltip(_hoverTooltip ?? "");
    }

    /// <summary>Checks project edits, painted masks and identity changes before navigation can discard them.</summary>
    private bool HasUnsavedChanges() => _project != null && (_project.HasChanges || _editedMapLayers.Count > 0 || _mapRgbDrafts.Count > 0 || _id != "WC_MyWorld" || _name != "My World");
}
