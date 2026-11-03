using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.Serialization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WorldCreator.Mod
{
    public sealed class WorldCreatorWindow : MonoBehaviour
    {
        private const float CardSpacing = 20f;
        private const float ScrollContentRightPadding = 36f;
        private static readonly string[] Names = { "Blank world (no template)", "Mars", "Moon", "Europa", "Mimas", "Venus", "Vulcan" };
        private static readonly string[] Files = { "", "Mars2/Mars2.xml", "Lunar/Lunar.xml", "Europa/Europa.xml", "Mimas/MimasHerschel.xml", "Venus/Venus.xml", "Vulcan/VulcanV2.xml" };
        private static readonly string[] Pages = { "Overview", "Environment", "Weather", "Orbit & seasons", "Resources", "Terrain", "Advanced", "Map studio", "Starting conditions", "Asset libraries" };
        private static readonly string[] AssetCategories = { "Surface", "Underground", "Atmosphere / Sky", "Map / UI", "Other" };
        private sealed class GasOption
        {
            internal readonly string Id;
            internal readonly string Name;
            internal readonly bool IsLiquid;
            internal readonly bool IsKnown;
            internal GasOption(string id)
            {
                Id = id;
                IsLiquid = id.StartsWith("Liquid", StringComparison.Ordinal);
                IsKnown = true;
                var name = IsLiquid ? id.Substring("Liquid".Length) : id;
                Name = Regex.Replace(name, "([a-z])([A-Z])", "$1 $2") + (IsLiquid ? " (Liquid)" : "");
            }
            internal GasOption(string id, bool isKnown) : this(id)
            {
                IsKnown = isKnown;
                if (!isKnown) Name = "Unknown species (" + id + ")";
            }
        }
        private static readonly GasOption[] Gases = Assets.Scripts.Atmospherics.Chemistry.Gases
            .Where(mole => (uint)mole.Type != 0 && (((uint)mole.Type & ((uint)mole.Type - 1u)) == 0))
            .Select(mole =>
            {
                var type = mole.Type;
                var enumName = Enum.GetName(typeof(Assets.Scripts.Atmospherics.Chemistry.GasType), type);
                var member = enumName == null ? null : typeof(Assets.Scripts.Atmospherics.Chemistry.GasType).GetField(enumName);
                var id = member?.GetCustomAttribute<XmlEnumAttribute>()?.Name;
                return string.IsNullOrEmpty(id) ? null : new GasOption(id);
            })
            .Where(gas => gas != null && !string.Equals(gas.Id, "Undefined", StringComparison.Ordinal))
            .GroupBy(gas => gas!.Id, StringComparer.Ordinal).Select(group => group.First()!)
            .OrderBy(gas => gas.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        private static readonly Color[] GasColors = { new Color(.25f,.72f,.9f), new Color(.72f,.78f,.82f), new Color(.9f,.45f,.28f), new Color(.8f,.68f,.3f), new Color(.7f,.4f,.65f), new Color(.35f,.8f,.58f), new Color(.55f,.68f,.95f), new Color(.84f,.38f,.35f), new Color(.55f,.78f,.38f), new Color(.3f,.78f,.72f) };
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
        private int _gasSelection;
        private string _newGasQuantity = "1";
        private int _temperatureUnit;
        private string _temperatureAxisMaximum = "";
        private string _temperatureScaleTopDraft = "";
        private bool _temperatureScaleTopEditing;
        private readonly Dictionary<string, string> _temperatureValueDrafts = new Dictionary<string, string>(StringComparer.Ordinal);
        private int _weatherSelection;
        private int _startConditionSelection;
        private int _newStartEventSelection;
        private string _newStartSpawnId = "DefaultNewPlayer";
        private string _newStartCrateName = "Supply crate";
        private string _newStartItemId = "ItemIronSheets";
        private string _newStartItemQuantity = "1";
        private string _startItemSearch = "";
        private int _assetCategorySelection;
        private int _assetCategoryFilter;
        private int _assetTypeFilter = 2;
        private string _assetPath = "";
        private string _assetSearch = "";
        private string? _pendingAssetRemoveId;
        private bool _assetLibraryLoaded;
        private string? _assetLibraryError;
        private readonly List<LibraryAsset> _assetLibraryItems = new List<LibraryAsset>();
        private static readonly string[] AssetTypeFilters = { "Textures", "Terrain data", "All" };
        private static readonly string[] StartEvents = { "NewWorld", "NewPlayer", "RespawnPlayer", "NewPlayerKit", "RespawnPlayerKit" };
        private static readonly string[] StartEventLabels = { "World", "Player", "Respawn", "New-player kit", "Respawn kit" };
        private string _newWeatherId = "WCWeather_MyEffect";
        private string _newWeatherName = "My Weather Effect";
        private int _pendingTemplate = -1;
        private string? _pendingLoadPath;
        private string? _pendingWeatherArchiveId;
        private readonly List<(string Path, string Name)> _loadChoices = new List<(string, string)>();
        private int _loadSelection;
        private int _presetSelection = -1;
        private int _screen;
        private int _pendingScreen = -1;
        private int _pendingPage = -1;
        private bool _pendingClose;
        private bool _discardConfirmed;
        private string? _hoverTooltip;
        private Vector2 _pointerPosition;
        private string? _temperatureDragPath;
        private float _temperatureDragMin;
        private float _temperatureDragMax;
        private int _orbitView;
        private float _orbitPhase = 45f;
        private float _orbitZoom = 1f;
        private Quaternion _orbitGlobeRotation = DefaultOrbitRotation();
        private float _orbitSpinPhase;
        private float _orbitSimulationSpeed = 1f;
        private bool _orbitSimulationPlaying;
        private Vector3 _orbitLightDirectionView = Vector3.forward;
        private Vector3 _orbitRenderedLightDirectionView = Vector3.forward;
        private bool _orbitDragging;
        private Vector2 _orbitDragPosition;
        private Texture2D? _orbitPlanetSurfaceTexture;
        private Texture2D? _orbitPrimarySurfaceTexture;
        private string? _orbitPrimaryTexturePath;
        private RenderTexture? _orbitSunRenderTexture;
        private RenderTexture? _orbitPlanetRenderTexture;
        private Camera? _orbitSphereCamera;
        private GameObject? _orbitSphereObject;
        private MeshRenderer? _orbitSphereRenderer;
        private Light? _orbitSphereLight;
        private Material? _orbitSphereMaterial;
        private float _orbitSphereRenderedSpin = float.NaN;
        private Quaternion _orbitSphereRenderedRotation = Quaternion.identity;
        private bool _orbitSphereTexturesDirty = true;
        private byte[]? _orbitSourceSurfaceData;
        private string? _orbitPrimaryBodyIdRendered;
        private bool _orbitPrimaryIsBlackHole;
        private bool _orbitPrimaryIsStar;
        private string? _orbitGroundTexturePath;
        private byte[]? _orbitGroundTextureData;
        private string _id = "WC_MyWorld";
        private string _name = "My World";
        private string _projectOrigin = "";
        private string _referenceTemperature = "273.15";
        private string _terrainPath = "";
        private string _compiledTerrainPath = "";
        private string _advancedFilter = "";
        private string _optionalSettingFilter = "";
        private string _terrainKind = "Heightmap";
        private Texture2D? _terrainPreview;
        private Texture2D? _mapPreview;
        private Texture2D? _mapReferencePreview;
        private int _mapReferenceSelection;
        private string _status = "Start from a blank world, apply a preset if desired, then create a new mod.";
        private WorldProject? _project;
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
        private GUIStyle? _navActiveStyle;
        private GUIStyle? _templateStyle;
        private GUIStyle? _templateActiveStyle;
        private GUIStyle? _rowStyle;
        private GUIStyle? _inputStyle;
        private GUIStyle? _multilineStyle;
        private GUIStyle? _buttonStyle;
        private GUIStyle? _primaryButtonStyle;
        private GUIStyle? _windowStyle;
        private GUIStyle? _dialogStyle;
        private GUIStyle? _tooltipStyle;
        private readonly List<Texture2D> _styleTextures = new List<Texture2D>();
        private string? _nativePopupOpen;
        private string? _queuedNativePopupSelection;
        private string[] _nativePopupOptions = Array.Empty<string>();
        private Rect _nativePopupAnchor;
        private float _nativePopupScrollOffset;
        private readonly Dictionary<string, Texture2D> _mapLayerPreviews = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, Vector2> _poiPositions = new Dictionary<string, Vector2>();
        private readonly Dictionary<string, Color32[]> _mapLayerPixels = new Dictionary<string, Color32[]>();
        private readonly Dictionary<string, byte[]> _mapSourcePng = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private readonly HashSet<string> _editedMapLayers = new HashSet<string>();
        private readonly HashSet<string> _createdMapLayers = new HashSet<string>();
        private readonly HashSet<string> _selectedMapLayers = new HashSet<string>();
        private string? _paintLayerId;
        private string? _paintRegionId;
        private bool _paintMode;
        private bool _eraseMapMode;
        private float _brushRadius = 7f;
        private byte[]? _mapUndoPng;
        private string? _mapUndoLayerId;
        private bool _mapUndoLayerHadEdits;
        private Vector2 _mapLayerScroll;
        private Vector2 _mapRegionScroll;
        private int _mapMaskAssetSelection;

        private void Awake()
        {
            UpdateWindowRect();
        }

        private void Update()
        {
            if (_nativePopupOpen != null && Input.GetKeyDown(KeyCode.Escape)) CloseNativePopup();
            if (!_orbitSimulationPlaying || _screen != 1 || _page != 3 || _project == null) return;
            var delta = Time.unscaledDeltaTime;
            if (delta <= 0) return;
            var orbitalPeriod = OrbitPeriodDays();
            if (orbitalPeriod > 0) _orbitPhase = (_orbitPhase + 360f * _orbitSimulationSpeed * delta / 60f) % 360f;
            var rotationPeriod = RotationPeriodDays();
            if (_orbitView != 3 && rotationPeriod != 0)
                _orbitSpinPhase = (_orbitSpinPhase + 360f * (float)orbitalPeriod * _orbitSimulationSpeed * delta / (60f * (float)Math.Abs(rotationPeriod)) * (rotationPeriod < 0 ? -1f : 1f)) % 360f;
        }
        private Rect _nativePopupRect;

        private double OrbitPeriodDays()
        {
            var field = _project?.OrbitFields.FirstOrDefault(f => f.Label.EndsWith(" / Period"));
            return field != null && double.TryParse(field.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : 1d;
        }

        private double RotationPeriodDays()
        {
            var field = _project?.OrbitFields.FirstOrDefault(f => f.Label.IndexOf("RotationPeriod", StringComparison.OrdinalIgnoreCase) >= 0);
            return field != null && double.TryParse(field.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0d;
        }

        private void UpdateWindowRect()
        {
            var margin = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) * 0.025f, 8f, 32f);
            var width = Mathf.Max(1f, Screen.width - margin * 2f);
            var height = Mathf.Max(1f, Screen.height - margin * 2f);
            _window = new Rect(margin, margin, width, height);
        }

        private void LoadTemplate()
        {
            try
            {
                _project = _template == 0
                    ? WorldProject.CreateBlank(Path.Combine(Application.dataPath, "StreamingAssets"))
                    : new WorldProject(Path.Combine(Application.dataPath, "StreamingAssets"), Files[_template]);
                _projectOrigin = _template == 0 ? "Blank world" : Names[_template] + " template";
                LoadMapPreview();
                _scroll = Vector2.zero;
                _status = _template == 0
                    ? "Blank settings created. Add compiled terrain before loading this world in game."
                    : "Template loaded. Create a world mod to keep these settings.";
            }
            catch (Exception e)
            {
                _project = null;
                _status = "Could not load template: " + e.Message;
                Debug.LogError("[World Creator] " + e);
            }
        }

        private void OnGUI()
        {
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
            _pointerPosition = Input.mousePosition;
            _hoverTooltip = null;
            if (Event.current.type == EventType.Layout)
            {
                if (_pendingScreen >= 0) { _screen = _pendingScreen; _pendingScreen = -1; }
                if (_pendingPage >= 0) { _page = _pendingPage; _pendingPage = -1; }
            }
            GUI.Window(904521, _window, DrawWindow, GUIContent.none, _windowStyle!);
            if (_pendingTemplate >= 0 || _pendingLoadPath != null || _pendingClose || _pendingWeatherArchiveId != null)
            {
                CloseNativePopup();
                var margin = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) * 0.025f, 8f, 32f);
                var dialogWidth = Mathf.Min(460f, Mathf.Max(1f, Screen.width - margin * 2f));
                var dialogHeight = Mathf.Min(170f, Mathf.Max(1f, Screen.height - margin * 2f));
                GUI.ModalWindow(904522, new Rect((Screen.width - dialogWidth) / 2f, (Screen.height - dialogHeight) / 2f, dialogWidth, dialogHeight), DrawDiscardDialog, _pendingWeatherArchiveId != null ? "Remove saved weather effect?" : _pendingClose ? "Leave editor?" : "Replace current settings?", _dialogStyle!);
            }
            if (_nativePopupOpen != null) DrawNativePopup(_nativePopupOpen, _nativePopupOptions, _nativePopupCurrentSelected);
            DrawHoverTooltip(string.IsNullOrWhiteSpace(_hoverTooltip) ? GUI.tooltip : _hoverTooltip);
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
                var archiveWeatherId = _pendingWeatherArchiveId;
                var close = _pendingClose;
                _pendingTemplate = -1;
                _pendingLoadPath = null;
                _pendingWeatherArchiveId = null;
                _pendingClose = false;
                if (close) { _pendingScreen = 0; }
                else if (nextTemplate >= 0) ApplyOriginalWorld(nextTemplate);
                else if (nextLoadPath != null) LoadWorld(nextLoadPath);
                else if (archiveWeatherId != null) { try { WeatherEffectLibrary.Archive(archiveWeatherId); _status = "Removed the saved copy. Effects already added to the current project are unchanged."; } catch (Exception e) { _status = e.Message; } }
            }
        }

        private bool NativeButton(string label, GUIStyle layoutStyle, params GUILayoutOption[] options) =>
            GUILayout.Button(new GUIContent(label, GUI.tooltip), layoutStyle, options);

        private bool NativeButton(string label, params GUILayoutOption[] options) => NativeButton(label, GUI.skin.button, options);

        private bool NativeButtonAt(Rect guiRect, string label, GUIStyle layoutStyle) => GUI.Button(guiRect, new GUIContent(label, GUI.tooltip), layoutStyle);
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
                    if (NativeButton(labels[i], style, GUILayout.ExpandWidth(true), GUILayout.Height(32))) selected = i;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            return selected;
        }

        private int NativeSelectionGrid(int selected, string[] labels, int columns, params GUILayoutOption[] options) =>
            NativeSelectionGrid(selected, labels, columns, GUI.skin.button, options);

        private int NativeDropdown(string id, int selected, string[] labels, GUIStyle layoutStyle, params GUILayoutOption[] options)
        {
            if (labels == null || labels.Length == 0) return -1;
            selected = selected < 0 ? -1 : Mathf.Clamp(selected, 0, labels.Length - 1);
            var key = "dropdown:" + _screen + ":" + _page + ":" + id;
            var label = (selected < 0 ? "Choose a vanilla world…" : labels[selected]) + "  ▾";
            if (NativeButton(label, layoutStyle, options))
            {
                if (_nativePopupOpen == key) CloseNativePopup();
                else
                {
                    _nativePopupOpen = key;
                    _nativePopupOptions = labels;
                    _nativePopupCurrentSelected = selected;
                    _nativePopupScrollOffset = selected * 32f;
                    var buttonRect = GUILayoutUtility.GetLastRect();
                    var mouse = Input.mousePosition;
                    var width = Mathf.Max(buttonRect.width, 190f);
                    _nativePopupAnchor = new Rect(mouse.x - width * 0.5f,
                        Screen.height - mouse.y + buttonRect.height * 0.5f, width, buttonRect.height);
                }
            }
            else if (_nativePopupOpen == key)
            {
                _nativePopupOptions = labels;
                _nativePopupCurrentSelected = selected;
            }
            if (_queuedNativePopupSelection == key && Event.current.type == EventType.Layout)
            {
                selected = _nativePopupSelectedIndex;
                _queuedNativePopupSelection = null;
                CloseNativePopup();
            }
            return selected;
        }

        private int _nativePopupSelectedIndex;
        private int _nativePopupCurrentSelected;

        private void DrawNativePopup(string key, string[] labels, int selected)
        {
            if (_nativePopupOpen != key || labels.Length == 0) return;
            const float rowHeight = 32f;
            var visibleRows = Mathf.Max(1, Mathf.FloorToInt((Screen.height * .55f - 16f) / rowHeight));
            var count = Math.Min(labels.Length, visibleRows);
            var height = count * rowHeight + 16f;
            var widestLabel = labels.Max(option => _templateStyle!.CalcSize(new GUIContent(option)).x);
            var width = Mathf.Clamp(Mathf.Max(_nativePopupAnchor.width, widestLabel + 52f), 190f, Mathf.Max(190f, Screen.width - 16f));
            var x = Mathf.Clamp(_nativePopupAnchor.center.x - width * 0.5f, 8f, Screen.width - width - 8f);
            var y = _nativePopupAnchor.yMax + 4f;
            if (y + height > Screen.height - 8f) y = _nativePopupAnchor.yMin - height - 4f;
            y = Mathf.Clamp(y, 8f, Mathf.Max(8f, Screen.height - height - 8f));
            _nativePopupRect = new Rect(x, y, width, height);
            GUI.Window(904523, _nativePopupRect, _ =>
            {
                var scroll = GUILayout.BeginScrollView(new Vector2(0f, _nativePopupScrollOffset), false, true, GUILayout.Height(height - 12f));
                _nativePopupScrollOffset = scroll.y;
                for (var optionIndex = 0; optionIndex < labels.Length; optionIndex++)
                {
                    if (GUILayout.Button(labels[optionIndex], optionIndex == selected ? _templateActiveStyle! : _templateStyle!, GUILayout.Height(rowHeight)))
                    {
                        _nativePopupSelectedIndex = optionIndex;
                        _queuedNativePopupSelection = key;
                        CloseNativePopup();
                    }
                }
                GUILayout.EndScrollView();
            }, GUIContent.none, _cardStyle!);
        }

        private void CloseNativePopup()
        {
            _nativePopupOpen = null;
        }

        private void DrawDiscardDialog(int windowId)
        {
            GUILayout.Label(_pendingWeatherArchiveId != null
                ? "Move this effect out of your reusable weather library? A copy already added to this project will remain."
                : _pendingClose
                    ? "Return to the World Creator start page? Any edits in this project will be discarded."
                    : "Replace the current settings with this selection? Any edits in this project will be discarded.");
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (NativeButton(_pendingWeatherArchiveId != null ? "Remove saved effect" : _pendingClose ? "Back to start page" : "Replace settings", _primaryButtonStyle!, GUILayout.Height(32))) _discardConfirmed = true;
            if (NativeButton("Keep editing", _buttonStyle!, GUILayout.Height(32)))
            {
                _pendingTemplate = -1;
                _pendingLoadPath = null;
                _pendingWeatherArchiveId = null;
                _pendingClose = false;
            }
            GUILayout.EndHorizontal();
        }

        private void OnDestroy()
        {
            if (_terrainPreview != null) Destroy(_terrainPreview);
            if (_mapPreview != null) Destroy(_mapPreview);
            if (_mapReferencePreview != null) Destroy(_mapReferencePreview);
            if (_orbitPlanetSurfaceTexture != null) Destroy(_orbitPlanetSurfaceTexture);
            if (_orbitPrimarySurfaceTexture != null) Destroy(_orbitPrimarySurfaceTexture);
            if (_orbitSphereMaterial != null) Destroy(_orbitSphereMaterial);
            if (_orbitSunRenderTexture != null) { _orbitSunRenderTexture.Release(); Destroy(_orbitSunRenderTexture); }
            if (_orbitPlanetRenderTexture != null) { _orbitPlanetRenderTexture.Release(); Destroy(_orbitPlanetRenderTexture); }
            if (_orbitSphereCamera != null) Destroy(_orbitSphereCamera.gameObject);
            if (_orbitSphereObject != null) Destroy(_orbitSphereObject);
            if (_orbitSphereLight != null) Destroy(_orbitSphereLight.gameObject);
            foreach (var layerTexture in _mapLayerPreviews.Values) if (layerTexture != null) Destroy(layerTexture);
            foreach (var texture in _styleTextures) if (texture != null) Destroy(texture);
        }

        private void DrawWindow(int windowId)
        {
            if (_screen == 0) DrawHome();
            else if (_screen == 2) DrawLoadPage();
            else DrawEditorPage();

            // GUI.tooltip is resolved by IMGUI after controls have been drawn.
            // Capture it inside the window callback before its GUI context ends.
            if (!string.IsNullOrWhiteSpace(GUI.tooltip)) _hoverTooltip = GUI.tooltip;
        }

        private void DrawHome()
        {
            BeginNativeMenuPage();
            if (NativeButton("‹  Back to Stationeers", _buttonStyle!, GUILayout.Width(220), GUILayout.Height(38))) EditorWindow.Hide();
            GUILayout.Space(24);
            GUILayout.Label("WORLD CREATOR", _titleStyle!);
            GUILayout.Label("Create a custom world or continue editing one of your installed world mods.", _mutedStyle!);
            GUILayout.Space(24);
            if (NativeButton("Create a New World\nStart with a blank world and edit its settings", _primaryButtonStyle!, GUILayout.Height(86))) StartBlankProject();
            GUILayout.Space(12);
            if (NativeButton("Load a World\nContinue from an installed custom world mod", _buttonStyle!, GUILayout.Height(86))) { ScanWorlds(); _pendingScreen = 2; }
            EndNativeMenuPage();
        }

        private void StartBlankProject()
        {
            _template = 0;
            _presetSelection = -1;
            _id = "WC_MyWorld";
            _name = "My World";
            _pendingPage = 0;
            LoadTemplate();
            _pendingScreen = 3;
        }

        private void DrawOriginalWorldCard()
        {
            GUILayout.BeginVertical(_cardStyle!);
            GUILayout.Label("START FROM AN ORIGINAL WORLD", _sectionStyle!);
            GUILayout.Label("Use an installed vanilla world as a starting point for gravity, orbit, landing site and atmosphere. Weather and default starting gear are applied too.", _mutedStyle!);
            GUILayout.BeginHorizontal();
            var selectedPreset = NativeDropdown("original-world", _presetSelection - 1, Names.Skip(1).ToArray(), _templateActiveStyle!, GUILayout.Width(270), GUILayout.Height(36));
            if (selectedPreset >= 0) _presetSelection = selectedPreset + 1;
            if (NativeButton("Apply preset", _primaryButtonStyle!, GUILayout.Width(150), GUILayout.Height(36)))
            {
                if (_presetSelection > 0) _pendingTemplate = _presetSelection;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Only the world settings and matching weather/start references are applied; maps and terrain assets are not copied.", _mutedStyle!);
            GUILayout.EndVertical();
        }

        private void DrawWorldPresentationCard()
        {
            var fields = _project!.WorldFields.Where(field => field.Label.StartsWith("Overview/", StringComparison.Ordinal)).ToArray();
            if (fields.Length == 0) return;
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            GUILayout.Label("WORLD PRESENTATION & CLOCK", _sectionStyle!);
            GUILayout.Label("Edit the world-list, lighting, skybox scale and starting celestial clock fields.", _mutedStyle!);
            DrawWorldFieldGroup("World list", fields.Where(field => field.Label.EndsWith("World list priority", StringComparison.Ordinal) || field.Label.EndsWith("World visibility", StringComparison.Ordinal)));
            DrawWorldFieldGroup("Primary body & presentation", fields.Where(field => field.Label.EndsWith("Primary body ID", StringComparison.Ordinal) ||
                field.Label.EndsWith("Primary star irradiance (W/m²)", StringComparison.Ordinal) || field.Label.EndsWith("Ambience sound ID", StringComparison.Ordinal) ||
                field.Label.EndsWith("Skybox material ID", StringComparison.Ordinal) || field.Label.EndsWith("Sun prefab ID", StringComparison.Ordinal)));
            DrawWorldFieldGroup("Visual scale & celestial clock", fields.Where(field => field.Label.EndsWith("Celestial body scale", StringComparison.Ordinal) ||
                field.Label.EndsWith("Skybox scale", StringComparison.Ordinal) || field.Label.Contains("Clock offset", StringComparison.Ordinal)));
            GUILayout.EndVertical();
        }

        private void DrawWorldEnvironmentCard()
        {
            var fields = _project!.WorldFields.Where(field => field.Label.StartsWith("Environment/", StringComparison.Ordinal) || field.Label.StartsWith("Gravity", StringComparison.Ordinal)).ToArray();
            if (fields.Length == 0) return;
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            GUILayout.Label("WORLD CONDITIONS", _sectionStyle!);
            DrawWorldFieldRows(fields, "Environment/");
            GUILayout.EndVertical();
            GUILayout.Space(CardSpacing);
        }

        private void DrawWorldFieldGroup(string title, IEnumerable<WorldProject.Field> source)
        {
            var fields = source.ToArray();
            if (fields.Length == 0) return;
            GUILayout.Space(5);
            GUILayout.Label(title, _mutedStyle!);
            DrawWorldFieldRows(fields, "Overview/");
        }

        private void DrawWorldFieldRows(IReadOnlyList<WorldProject.Field> fields, string labelPrefix)
        {
            for (var i = 0; i < fields.Count; i += 2)
            {
                GUILayout.BeginHorizontal();
                DrawWorldField(fields[i], labelPrefix);
                if (i + 1 < fields.Count) DrawWorldField(fields[i + 1], labelPrefix);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawWorldField(WorldProject.Field field, string labelPrefix)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            var title = field.Label.StartsWith(labelPrefix, StringComparison.Ordinal) ? field.Label.Substring(labelPrefix.Length) : ReadableFieldLabel(field.Label, false);
            GUILayout.Label(new GUIContent(title + "  ⓘ", field.Tooltip), _mutedStyle!);
            var rect = GUILayoutUtility.GetRect(80, 28, GUILayout.ExpandWidth(true), GUILayout.Height(28));
            RegisterHoverTooltip(rect, field.Tooltip);
            if (field.IsBoolean)
                field.Value = GUI.Toggle(rect, bool.TryParse(field.Value, out var current) && current, new GUIContent("", field.Tooltip)) ? "true" : "false";
            else
            {
                var value = GUI.TextField(rect, field.Value, _inputStyle!);
                field.Value = field.IsNumeric ? MaskNumericInput(value) : value;
            }
            GUILayout.EndVertical();
        }

        private void ApplyOriginalWorld(int template)
        {
            _template = template;
            _id = "WC_MyWorld";
            _name = "My World";
            LoadTemplate();
            _pendingPage = 0;
        }

        private void DrawLoadPage()
        {
            BeginNativeMenuPage();
            if (NativeButton("‹  Back to World Creator", _buttonStyle!, GUILayout.Width(240), GUILayout.Height(38))) _pendingScreen = 0;
            GUILayout.Space(20);
            GUILayout.Label("LOAD A WORLD", _titleStyle!);
            GUILayout.Label("Choose an installed world mod from your local mods folder or Steam Workshop. The current project changes only after confirmation.", _mutedStyle!);
            GUILayout.Space(12);
            if (_loadChoices.Count == 0) GUILayout.Label("No custom world mods with GameData\\world.xml were found.", _mutedStyle!);
            else
            {
                _loadSelection = Mathf.Clamp(_loadSelection, 0, _loadChoices.Count - 1);
                _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
                for (var i = 0; i < _loadChoices.Count; i++)
                    if (NativeButton(_loadChoices[i].Name + "\n" + Path.GetDirectoryName(_loadChoices[i].Path), i == _loadSelection ? _templateActiveStyle! : _templateStyle!, GUILayout.Height(54))) _loadSelection = i;
                GUILayout.EndScrollView();
            }
            GUILayout.BeginHorizontal();
            var loadButtonWasEnabled = GUI.enabled;
            GUI.enabled = loadButtonWasEnabled && _loadChoices.Count > 0;
            if (NativeButton("Load selected world", _primaryButtonStyle!, GUILayout.Width(220), GUILayout.Height(36))) _pendingLoadPath = _loadChoices[_loadSelection].Path;
            GUI.enabled = loadButtonWasEnabled;
            GUILayout.EndHorizontal();
            EndNativeMenuPage();
        }

        private void BeginNativeMenuPage()
        {
            GUILayout.Space(12);
            GUILayout.BeginHorizontal();
            GUILayout.Space(28);
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        }

        private static void EndNativeMenuPage()
        {
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void ScanWorlds()
        {
            _loadChoices.Clear();
            var mods = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Stationeers", "mods");
            AddWorldChoices(mods, false);
            var gameData = new DirectoryInfo(Application.dataPath);
            var steamApps = gameData.Parent?.Parent?.Parent;
            if (steamApps != null) AddWorldChoices(Path.Combine(steamApps.FullName, "workshop", "content", "544550"), true);
            _loadChoices.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            _loadSelection = 0;
        }

        private void AddWorldChoices(string root, bool workshop)
        {
            if (!Directory.Exists(root)) return;
            try
            {
                var gameDataFolders = new List<string>();
                if (string.Equals(Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), "GameData", StringComparison.OrdinalIgnoreCase))
                    gameDataFolders.Add(root);
                gameDataFolders.AddRange(Directory.GetDirectories(root, "GameData", SearchOption.AllDirectories));
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var gameData in gameDataFolders)
                {
                    var modRoot = Directory.GetParent(gameData)?.FullName ?? gameData;
                    var aboutName = ReadModName(modRoot);
                    foreach (var path in Directory.GetFiles(gameData, "*.xml", SearchOption.AllDirectories))
                    {
                        if (!seen.Add(path)) continue;
                        try
                        {
                            var document = XDocument.Load(path);
                            var world = FindWorldDefinition(document.Root);
                            if (world == null || (world.Element("Name") == null && string.IsNullOrWhiteSpace((string?)world.Attribute("Id")))) continue;
                            var name = (string?)world.Element("Name")?.Attribute("Value");
                            if (IsPlaceholderWorldName(name)) name = aboutName;
                            if (string.IsNullOrWhiteSpace(name)) name = (string?)world.Attribute("Id");
                            if (string.IsNullOrWhiteSpace(name)) name = Path.GetFileNameWithoutExtension(path);
                            if (workshop) name = "Workshop · " + name;
                            _loadChoices.Add((path, name));
                        }
                        catch { /* Ignore unrelated or malformed XML files in mod data folders. */ }
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[World Creator] Could not scan world folder " + root + ": " + e.Message); }
        }

        private static XElement? FindWorldDefinition(XElement? root)
        {
            if (root == null) return null;
            if (root.Name.LocalName == "World") return root;
            return root.Element("WorldSettings")?.Element("World")
                ?? root.Element("GameData")?.Element("WorldSettings")?.Element("World");
        }

        private static string ReadModName(string modRoot)
        {
            try
            {
                var aboutPath = Path.Combine(modRoot, "About", "About.xml");
                return File.Exists(aboutPath) ? XDocument.Load(aboutPath).Root?.Element("Name")?.Value ?? string.Empty : string.Empty;
            }
            catch { return string.Empty; }
        }

        private static string ResolveLoadedWorldDisplayName(string worldFile, string? xmlName)
        {
            if (!IsPlaceholderWorldName(xmlName)) return xmlName!;
            var directory = new DirectoryInfo(Path.GetDirectoryName(worldFile) ?? string.Empty);
            while (directory != null)
            {
                var modName = ReadModName(directory.FullName);
                if (!string.IsNullOrWhiteSpace(modName) && !IsPlaceholderWorldName(modName)) return modName;
                directory = directory.Parent!;
            }
            return Path.GetFileName(Path.GetDirectoryName(worldFile));
        }

        private static bool IsPlaceholderWorldName(string? name)
        {
            return string.IsNullOrWhiteSpace(name)
                || name.Equals("GameData", StringComparison.OrdinalIgnoreCase)
                || name.Equals("WorldSettings", StringComparison.OrdinalIgnoreCase)
                || name.Equals("World", StringComparison.OrdinalIgnoreCase);
        }

        private void LoadWorld(string path)
        {
            try
            {
                _project = new WorldProject(Path.Combine(Application.dataPath, "StreamingAssets"), path, true);
                _template = -1;
                _presetSelection = -1;
                _id = "WC_MyWorld";
                _name = ResolveLoadedWorldDisplayName(path, _project.DisplayName);
                _projectOrigin = "Loaded custom world";
                _pendingPage = 0;
                LoadMapPreview();
                _pendingScreen = 3;
                _status = "Loaded world settings. Review and export as a new mod.";
            }
            catch (Exception e)
            {
                _status = "Could not load world: " + e.Message;
                Debug.LogError("[World Creator] " + e);
            }
        }

        private void DrawEditorPage()
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginHorizontal(_cardStyle!);
            if (NativeButton("‹  Back", _buttonStyle!, GUILayout.Width(120), GUILayout.Height(34))) _pendingClose = true;
            GUILayout.FlexibleSpace();
            GUILayout.Label("WORLD CREATOR", _titleStyle!, GUILayout.ExpandWidth(true));
            GUILayout.FlexibleSpace();
            GUILayout.Space(120f);
            GUILayout.EndHorizontal();
            GUILayout.Space(30f);
            GUILayout.EndHorizontal();
            GUILayout.Space(CardSpacing);
            GUILayout.BeginHorizontal();
            GUILayout.Space(190 + CardSpacing);
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var contentRightInset = 10f + ScrollContentRightPadding + GUI.skin.verticalScrollbar.fixedWidth;
            GUILayout.BeginHorizontal();
            GUILayout.Space(10f);
            DrawTopbar();
            GUILayout.Space(contentRightInset);
            GUILayout.EndHorizontal();
            GUILayout.Space(CardSpacing);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.BeginHorizontal();
            GUILayout.Space(10f);
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (_page != 1)
            {
                GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
                GUILayout.Label(Pages[_page], _titleStyle!);
                GUILayout.Label(PageDescription(), _mutedStyle!);
                GUILayout.EndVertical();
                GUILayout.Space(CardSpacing);
            }
            if (_page == 0)
            {
                DrawOriginalWorldCard();
                GUILayout.Space(CardSpacing);
                DrawOverviewSummary();
                GUILayout.Space(CardSpacing);
                GUILayout.BeginHorizontal();
                DrawWorldIdentityCard();
                GUILayout.Space(CardSpacing);
                DrawOverviewShortcuts();
                GUILayout.EndHorizontal();
                GUILayout.Space(CardSpacing);
                DrawWorldPresentationCard();
                GUILayout.Space(CardSpacing);
            }
            if (_page == 4 && _project != null) DrawResourceControls();
            if (_page == 5 && _project != null) DrawTerrainInfo();
            if (_page == 6 && _project != null)
            {
                GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
                GUILayout.Label("ADVANCED WORLD DATA", _sectionStyle!);
                GUILayout.Label(_project.AdvancedFields.Count + " editable values from the selected world's complete XML tree. Unknown elements are retained during export; edits to IDs or references can still break their links.");
                GUILayout.BeginHorizontal();
                GUILayout.Label("Filter path or value", GUILayout.Width(130));
                _advancedFilter = GUILayout.TextField(_advancedFilter);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.Space(CardSpacing);
            }
            if (_page == 7 && _project != null)
            {
                GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
                GUILayout.Label("MAP DATA", _sectionStyle!);
                GUILayout.Label(_project.MapFields.Count + " map-linked XML values. Changes are shared with Advanced and included in the created world mod.", _mutedStyle!);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Filter map fields", GUILayout.Width(130));
                _advancedFilter = GUILayout.TextField(_advancedFilter);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            if (_page == 1 && _project != null)
            {
                var atmosphereAvailableWidth = Mathf.Max(1f, _window.width - 190f - CardSpacing - 30f);
                var atmosphereCardWidth = Mathf.Min(atmosphereAvailableWidth, Mathf.Max(520f, atmosphereAvailableWidth * 0.42f));
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(_cardStyle!, GUILayout.Width(atmosphereCardWidth));
                GUILayout.Label("ATMOSPHERE", _sectionStyle!);
                DrawGasControls();
                GUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(CardSpacing);
                DrawTemperatureChart();
                GUILayout.Space(CardSpacing);
                DrawCurveKeyEditor(_project.AtmosphereFields, "Atmosphere");
                GUILayout.Space(CardSpacing);
                DrawWorldEnvironmentCard();
            }
            if (_page == 2 && _project != null)
            {
                DrawWeatherControls();
                GUILayout.Space(CardSpacing);
                GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
                GUILayout.Label("EVENT SCHEDULING", _sectionStyle!);
                DrawWeatherTimeline();
                GUILayout.EndVertical();
                GUILayout.Space(CardSpacing);
                DrawCurveKeyEditor(_project.WeatherFields, "Weather");
            }
            if (_page == 3 && _project != null) DrawOrbitPreview();
            if (_page == 7 && _project != null) { GUILayout.Space(CardSpacing); DrawMapPreview(); }
            if (_page == 8 && _project != null) DrawStartConditions();
            if (_page == 9 && _project != null) DrawAssetLibrary();
            if (_project != null)
            {
                var fields = _page >= 8 || _page == 3 || _page == 5 ? new List<WorldProject.Field>() : Fields();
                var weatherFieldsRendered = false;
                if (_page == 1 || _page == 2)
                {
                    var curveParents = GetCurveKeyParents(fields);
                    fields = fields.Where(f => !curveParents.Any(parent => f.Label.StartsWith(parent + " / ", StringComparison.Ordinal))).ToList();
                }
                if (_page == 2 && fields.Count > 0)
                {
                    DrawWeatherFieldGroups(fields);
                    weatherFieldsRendered = true;
                }
                if ((_page == 6 || _page == 7) && _advancedFilter.Length > 0)
                    fields = fields.Where(f => f.Label.IndexOf(_advancedFilter, StringComparison.OrdinalIgnoreCase) >= 0 || f.Value.IndexOf(_advancedFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                if (_page == 0) fields = fields.Where(f => !f.Label.StartsWith("Overview/", StringComparison.Ordinal) && !f.Label.StartsWith("Environment/", StringComparison.Ordinal) && !f.Label.StartsWith("Gravity", StringComparison.Ordinal) &&
                    !f.Label.EndsWith(" / Latitude", StringComparison.Ordinal) && !f.Label.EndsWith(" / Longitude", StringComparison.Ordinal)).ToList();
                if (_page == 6 || _page == 7) GUILayout.Label("Showing " + fields.Count + " fields");
                if (fields.Count == 0 && _page < 8 && _page != 3 && _page != 5 && !weatherFieldsRendered) GUILayout.Label("No values are defined for this template in this section.");
                if (!weatherFieldsRendered && fields.Count > 0)
                {
                    if (_page == 0 || _page == 4 || _page == 6 || _page == 7) GUILayout.Space(CardSpacing);
                    DrawFieldCards(fields);
                }
            }
            if (_page == 1 && _project != null)
            {
                GUILayout.Space(CardSpacing);
                DrawOptionalSettings("/GlobalAtmosphere/");
            }
            if (_page == 6 && _project != null)
            {
                GUILayout.Space(CardSpacing);
                DrawOptionalSettings(null);
            }
            GUILayout.EndVertical();
            GUILayout.Space(10f);
            GUILayout.Space(ScrollContentRightPadding);
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUILayout.Space(CardSpacing);
            GUILayout.BeginHorizontal();
            GUILayout.Space(10f);
            GUILayout.BeginHorizontal(_cardStyle!);
            GUILayout.Label(_status, _mutedStyle!, GUILayout.ExpandWidth(true));
            if (NativeButton("Create world mod", _primaryButtonStyle!, GUILayout.Width(170), GUILayout.Height(34)) && _project != null) Export();
            if (NativeButton("Back", _buttonStyle!, GUILayout.Width(110), GUILayout.Height(34)))
            {
                if (HasUnsavedChanges()) _pendingClose = true;
                else _pendingScreen = 0;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(contentRightInset);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(30f);
            GUILayout.EndHorizontal();
            DrawSidebar();
        }

        private bool HasUnsavedChanges() => _project != null && (_project.HasChanges || _editedMapLayers.Count > 0 || _id != "WC_MyWorld" || _name != "My World");

        private void DrawFieldCards(IReadOnlyList<WorldProject.Field> fields)
        {
            foreach (var group in fields.GroupBy(FieldGroupName))
            {
                GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
                GUILayout.Label(group.Key, _sectionStyle!);
                var groupedFields = group.ToArray();
                for (var i = 0; i < groupedFields.Length; i += 2)
                {
                    GUILayout.BeginHorizontal();
                    DrawGroupedField(groupedFields[i]);
                    if (i + 1 < groupedFields.Length) DrawGroupedField(groupedFields[i + 1]);
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
                GUILayout.Space(CardSpacing);
            }
        }

        private void DrawGroupedField(WorldProject.Field field)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            var fieldLabel = new GUIContent(ReadableFieldLabel(field.Label, _page == 6 || _page == 7) + "  ⓘ", field.Tooltip);
            GUILayout.Label(fieldLabel, _mutedStyle!);
            RegisterHoverTooltip(GUILayoutUtility.GetLastRect(), field.Tooltip);
            if (field.IsBoolean)
                field.Value = GUILayout.Toggle(bool.TryParse(field.Value, out var current) && current, "Enabled") ? "true" : "false";
            else
            {
                var value = GUILayout.TextField(field.Value, _inputStyle!, GUILayout.ExpandWidth(true));
                field.Value = field.IsNumeric ? MaskNumericInput(value) : value;
            }
            GUILayout.Space(5);
            GUILayout.EndVertical();
        }

        private void DrawOverviewSummary()
        {
            GUILayout.BeginHorizontal();
            DrawOverviewTile("ORBIT PERIOD", _project!.OrbitFields.FirstOrDefault(f => f.Label.EndsWith(" / Period", StringComparison.Ordinal))?.Value ?? "—", "Configured around the primary body");
            GUILayout.Space(CardSpacing);
            DrawOverviewTile("ATMOSPHERE GASES", _project.AtmosphereFields.Count(f => f.Label.Contains("/Gas/Gas") && f.Label.EndsWith(" / Quantity", StringComparison.Ordinal) && f.Value != "0").ToString(CultureInfo.InvariantCulture), "Species with non-zero amount");
            GUILayout.Space(CardSpacing);
            DrawOverviewTile("START OPTIONS", _project.StartConditions.Count.ToString(CultureInfo.InvariantCulture), "Starting conditions");
            GUILayout.Space(CardSpacing);
            DrawOverviewTile("LIBRARY ASSETS", _project.ProjectAssets.Count.ToString(CultureInfo.InvariantCulture), "Texture and terrain references");
            GUILayout.EndHorizontal();
        }

        private void DrawOverviewTile(string title, string value, string note)
        {
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true), GUILayout.MinHeight(76));
            GUILayout.Label(title, _mutedStyle!);
            GUILayout.Label(value, _titleStyle!);
            GUILayout.Label(note, _mutedStyle!);
            GUILayout.EndVertical();
        }

        private void DrawOverviewShortcuts()
        {
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            GUILayout.Label("CONTINUE BUILDING", _sectionStyle!);
            GUILayout.Label("Open a section to edit its settings.", _mutedStyle!);
            var shortcuts = new[] { ("Orbit & seasons", 3, "Shape the orbit and landing site"), ("Environment", 1, "Set gases and temperature curves"), ("Starting conditions", 8, "Choose spawn profiles and gear"), ("Map studio", 7, "Preview and paint map regions"), ("Weather", 2, "Configure weather events and effects"), ("Resources", 4, "Adjust regional ore abundance") };
            foreach (var item in shortcuts)
            {
                if (NativeButton(item.Item1 + "    ·    " + item.Item3 + "    ›", _buttonStyle!, GUILayout.Height(38))) _pendingPage = item.Item2;
            }
            GUILayout.EndVertical();
        }

        private void DrawWorldIdentityCard()
        {
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            GUILayout.Label("WORLD IDENTITY", _sectionStyle!);
            GUILayout.Label("Basic information used by the world menu and generated mod.", _mutedStyle!);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label(new GUIContent("Display name  ⓘ", "Player-facing name shown in the Stationeers world selection menu."), _mutedStyle!);
            _name = TooltipTextField(_name, "Player-facing name shown in the Stationeers world selection menu.", 0, 30);
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label(new GUIContent("World ID  ⓘ", "Unique XML identifier for the world mod. It must start with WC_ and contain only letters, numbers or underscores."), _mutedStyle!);
            _id = TooltipTextField(_id, "Unique XML identifier for the world mod. It must start with WC_ and contain only letters, numbers or underscores.", 0, 30, 48);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            var descriptionHelp = "Description shown for this world. It is saved into the generated world.xml.";
            GUILayout.Label(new GUIContent("Description  ⓘ", descriptionHelp), _mutedStyle!);
            RegisterHoverTooltip(GUILayoutUtility.GetLastRect(), descriptionHelp);
            _project!.SetWorldDescription(GUILayout.TextArea(_project.WorldDescription, _multilineStyle!, GUILayout.Height(76), GUILayout.ExpandWidth(true)));
            var gravity = _project!.WorldFields.FirstOrDefault(field => field.Label.StartsWith("Gravity", StringComparison.Ordinal));
            if (gravity != null)
            {
                GUILayout.Label("Gravity", _mutedStyle!);
                DrawWorldFieldRows(new[] { gravity }, "");
            }
            GUILayout.EndVertical();
        }

        private static string FieldGroupName(WorldProject.Field field)
        {
            var label = field.Label;
            var separator = Math.Max(label.LastIndexOf('/'), label.LastIndexOf(" / ", StringComparison.Ordinal));
            if (separator < 0) return "General settings";
            var parent = label.Substring(0, separator).TrimEnd(' ', '/');
            var parentSeparator = parent.LastIndexOf('/');
            if (parentSeparator >= 0) parent = parent.Substring(parentSeparator + 1);
            return string.IsNullOrWhiteSpace(parent) ? "General settings" : parent.Trim();
        }

        private void DrawOptionalSettings(string? pathFilter)
        {
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            GUILayout.Label("OPTIONAL SETTING GROUPS", _sectionStyle!);
            var candidates = _project!.OptionalSettings
                .Where(option => pathFilter == null || option.Path.Contains(pathFilter, StringComparison.OrdinalIgnoreCase))
                .Where(option => _optionalSettingFilter.Length == 0 || option.Path.IndexOf(_optionalSettingFilter, StringComparison.OrdinalIgnoreCase) >= 0 || option.SourceWorld.IndexOf(_optionalSettingFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            GUILayout.Label("Add optional setting groups found in the installed world templates. They are copied from the listed world and remain editable below.", _mutedStyle!);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Find optional setting", GUILayout.Width(145));
            _optionalSettingFilter = GUILayout.TextField(_optionalSettingFilter, _inputStyle!);
            GUILayout.EndHorizontal();
            GUILayout.Label(candidates.Count + " optional setting groups available.", _mutedStyle!);
            if (candidates.Count == 0)
            {
                GUILayout.Label("No matching optional groups. Clear the search to browse available groups.", _mutedStyle!);
                GUILayout.EndVertical();
                return;
            }
            foreach (var option in candidates)
            {
                if (NativeButton("Add " + option.Path + "  ← " + option.SourceWorld, _buttonStyle!, GUILayout.Height(28)))
                {
                    try
                    {
                        _project!.AddOptionalSetting(option.Path);
                        _status = "Added " + option.Path + " from " + option.SourceWorld + ". Review it before creating the world mod.";
                    }
                    catch (Exception e) { _status = e.Message; }
                }
            }
            GUILayout.EndVertical();
        }

        private void DrawSidebar()
        {
            const float top = 82f;
            var height = Mathf.Max(160f, _window.height - top - 62f);
            GUI.BeginGroup(new Rect(12f, top, 190f, height));
            GUI.Box(new Rect(0f, 0f, 190f, height), GUIContent.none, _sidebarStyle!);
            const float left = 14f;
            const float width = 162f;
            var y = 16f;
            GUI.Label(new Rect(left, y, width, 22f), "STATIONEERS", _sectionStyle!);
            y += 24f;
            GUI.Label(new Rect(left, y, width, 22f), "WORLD CREATOR", _mutedStyle!);
            y += 42f;
            GUI.Label(new Rect(left, y, width, 22f), "NAVIGATION", _mutedStyle!);
            y += 22f;
            for (var i = 0; i < Pages.Length; i++)
            {
                if (NativeButtonAt(new Rect(left, y, width, 34f), Pages[i], i == _page ? _navActiveStyle! : _navStyle!)) _pendingPage = i;
                y += 44f;
            }
            GUI.EndGroup();
        }

        private void DrawTopbar()
        {
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(160));
            GUILayout.Label("WORLD PROJECT", _sectionStyle!);
            GUILayout.Label(_projectOrigin, _mutedStyle!);
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label(new GUIContent("Display name  ⓘ", "Player-facing name shown in the Stationeers world selection menu."), _mutedStyle!);
            _name = TooltipTextField(_name, "Player-facing name shown in the Stationeers world selection menu.", 0, 30);
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUILayout.Width(240));
            GUILayout.Label(new GUIContent("World ID  ⓘ", "Unique XML identifier for the world mod. It must start with WC_ and contain only letters, numbers or underscores."), _mutedStyle!);
            _id = TooltipTextField(_id, "Unique XML identifier for the world mod. It must start with WC_ and contain only letters, numbers or underscores.", 0, 30, 48);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private static string ShortLabel(string label)
        {
            var slash = label.LastIndexOf('/');
            if (slash > 0 && slash + 1 < label.Length && label[slash - 1] == 'm' && label[slash + 1] == 's') slash = -1;
            return slash >= 0 ? HumanizeXmlName(label.Substring(slash + 1).Replace("@", "")) + "   ·   " + HumanizeXmlPath(label.Substring(0, slash)) : HumanizeXmlName(label);
        }

        private static string ReadableFieldLabel(string label, bool includeContext)
        {
            var divider = label.LastIndexOf(" / ", StringComparison.Ordinal);
            var pathSlash = label.LastIndexOf('/');
            if (pathSlash > 0 && pathSlash + 1 < label.Length && label[pathSlash - 1] == 'm' && label[pathSlash + 1] == 's')
                pathSlash = label.LastIndexOf('/', pathSlash - 1);
            var leaf = divider >= 0 ? label.Substring(divider + 3) : pathSlash >= 0 ? label.Substring(pathSlash + 1) : label;
            var prefix = divider >= 0 ? label.Substring(0, divider) : pathSlash >= 0 ? label.Substring(0, pathSlash) : string.Empty;
            var friendly = HumanizeXmlName(leaf.TrimStart('@'));
            switch (leaf.TrimStart('@'))
            {
                case "SemiMajorAxisAu": friendly = "Average orbital distance (AU)"; break;
                case "SemiMajorAxisKm": friendly = "Average orbital distance (km)"; break;
                case "Eccentricity": friendly = "Orbit shape (eccentricity)"; break;
                case "Period": friendly = "Orbital period (game days)"; break;
                case "Inclination": friendly = "Orbit inclination (°)"; break;
                case "ArgumentOfPeriapsis": friendly = "Closest approach angle (°)"; break;
                case "LongitudeOfAscendingNode": friendly = "Orbit plane angle (°)"; break;
                case "LongitudeAtEpoch": friendly = "Position at orbital epoch (°)"; break;
                case "CoolDown": friendly = "Weather cooldown"; break;
                case "StartDelay": friendly = "Delay before weather starts"; break;
                case "Duration": friendly = "Weather duration"; break;
                case "OreDensity": friendly = "Ore abundance multiplier"; break;
                case "WindStrength": friendly = "Wind strength"; break;
            }
            if (prefix.Length == 0 || !includeContext) return friendly;
            var context = HumanizeXmlPath(prefix);
            return friendly + "   ·   " + context;
        }

        private static string HumanizeXmlPath(string path)
        {
            var rawParts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var parts = new List<string>();
            foreach (var rawPart in rawParts)
            {
                var part = HumanizeXmlName(rawPart);
                if (parts.Count == 0 || !string.Equals(parts[parts.Count - 1], part, StringComparison.OrdinalIgnoreCase)) parts.Add(part);
            }
            if (parts.Count > 3) parts = parts.Skip(parts.Count - 3).ToList();
            return string.Join(" / ", parts);
        }

        private static string HumanizeXmlName(string name)
        {
            var value = Regex.Replace(name.Replace("_", " "), "([a-z0-9])([A-Z])", "$1 $2");
            value = Regex.Replace(value, "([A-Z])([A-Z][a-z])", "$1 $2");
            return value.Replace("Au", "AU").Replace("Km", "km").Replace("Mmol", "mmol").Replace("Poi", "POI");
        }

        private static List<(string Path, int Index, WorldProject.Field Time, WorldProject.Field Value)> CurveKeys(IEnumerable<WorldProject.Field> fields)
        {
            var result = new List<(string, int, WorldProject.Field, WorldProject.Field)>();
            foreach (var group in fields.Where(f => f.Label.Contains("/Key #") &&
                         (f.Label.EndsWith(" / Time", StringComparison.Ordinal) || f.Label.EndsWith(" / Value", StringComparison.Ordinal)))
                     .GroupBy(f => f.Label.Substring(0, f.Label.LastIndexOf(" / ", StringComparison.Ordinal))))
            {
                var marker = group.Key.LastIndexOf("/Key #", StringComparison.Ordinal);
                if (marker < 0 || !int.TryParse(group.Key.Substring(marker + 6), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)) continue;
                var time = group.FirstOrDefault(f => f.Label.EndsWith(" / Time", StringComparison.Ordinal));
                var value = group.FirstOrDefault(f => f.Label.EndsWith(" / Value", StringComparison.Ordinal));
                if (time != null && value != null) result.Add((group.Key, index, time, value));
            }
            return result;
        }

        private static List<string> GetCurveKeyParents(IEnumerable<WorldCreator.Mod.WorldProject.Field> fields) =>
            CurveKeys(fields).Select(key => key.Path).ToList();

        private void DrawCurveKeyEditor(List<WorldProject.Field> fields, string section)
        {
            var keys = CurveKeys(fields);
            foreach (var curve in keys.GroupBy(key => key.Path.Substring(0, key.Path.LastIndexOf("/Key #", StringComparison.Ordinal))))
            {
                var curvePath = curve.Key;
                var isBaseTemperature = curvePath.EndsWith("/Temperature", StringComparison.Ordinal);
                var isSolar = curvePath.IndexOf("SolarRadiationTemperature", StringComparison.OrdinalIgnoreCase) >= 0;
                var isGreenhouse = curvePath.IndexOf("GHGTemperatureOffset", StringComparison.OrdinalIgnoreCase) >= 0;
                var isDensity = curvePath.IndexOf("DensityOffset", StringComparison.OrdinalIgnoreCase) >= 0;
                var timeLabel = isBaseTemperature ? "Solar angle (°)" : isSolar ? "Irradiance position (0–100)" : isGreenhouse ? "Greenhouse index" : isDensity ? "Gas density (mmol/L)" : "Curve input";
                var valueLabel = isBaseTemperature ? "Temperature (K)" : isSolar || isGreenhouse || isDensity ? "Temperature offset (K)" : "Curve value";
                var curveTitle = isBaseTemperature ? "Baseline temperature by sun angle" :
                    isSolar ? "Solar-radiation offset · " + CurveSide(curvePath) :
                    isGreenhouse ? "Greenhouse offset · " + CurveSide(curvePath) :
                    isDensity ? "Density offset · " + CurveSide(curvePath) :
                    CurveName(curvePath);

                GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
                GUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent(curveTitle + "  ⓘ", curve.First().Time.Tooltip + " " + curve.First().Value.Tooltip), _sectionStyle!);
                GUILayout.FlexibleSpace();
                if (NativeButton("+ Add key", _buttonStyle!, GUILayout.Width(105)))
                {
                    try
                    {
                        _project!.AddCurveKey(fields, section, curvePath);
                        _status = "Added a key to " + curveTitle + ".";
                    }
                    catch (Exception e) { _status = e.Message; }
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    return;
                }
                GUILayout.EndHorizontal();
                GUILayout.Label(section == "Atmosphere"
                    ? "Edit the meaningful input and output only. Unity interpolation tangents and weights remain in Advanced XML and are preserved."
                    : "Edit curve input and output using the units shown. Unity interpolation tangents and weights remain in Advanced XML and are preserved.", _mutedStyle!);
                var keyCount = curve.Count();
                foreach (var key in curve.OrderBy(k => k.Index))
                {
                    GUILayout.BeginHorizontal(_rowStyle!);
                var pointLabel = CurvePointLabel(key.Time.Value, timeLabel, isBaseTemperature);
                    GUILayout.Label(new GUIContent(pointLabel, "This point is set at " + pointLabel + ". The game interpolates between the surrounding points."), _sectionStyle!, GUILayout.Width(160));
                if (isBaseTemperature) DrawCurveNumber(key.Time, timeLabel, 135, 0, 180);
                else DrawCurveNumber(key.Time, timeLabel, 135);
                    if (isBaseTemperature) DrawTemperatureValue(key.Value, valueLabel, 135);
                    else DrawCurveNumber(key.Value, valueLabel, 135);
                    if (keyCount > 1 && NativeButton("Remove", _buttonStyle!, GUILayout.Width(82)))
                    {
                        try
                        {
                            _project!.RemoveCurveKey(fields, section, curvePath, key.Index);
                            _status = "Removed key " + key.Index.ToString(CultureInfo.InvariantCulture) + " from " + curveTitle + ".";
                        }
                        catch (Exception e) { _status = e.Message; }
                        GUILayout.EndHorizontal();
                        GUILayout.EndVertical();
                        return;
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
                GUILayout.Space(CardSpacing);
            }
        }

        private string CurvePointLabel(string input, string inputLabel, bool isBaseTemperature)
        {
            if (!double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var coordinate))
                return inputLabel + " " + input;
            var value = coordinate.ToString("0.##", CultureInfo.InvariantCulture);
            return isBaseTemperature ? value + "°" : inputLabel + " " + value;
        }

        private void DrawCurveNumber(WorldCreator.Mod.WorldProject.Field field, string label, float width, double? minimum = null, double? maximum = null)
        {
            GUILayout.BeginVertical(GUILayout.Width(width));
            GUILayout.Label(new GUIContent(label + "  ⓘ", field.Tooltip), _mutedStyle!);
            var rect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true), GUILayout.Height(28));
            RegisterHoverTooltip(rect, field.Tooltip);
            var value = MaskNumericInput(GUI.TextField(rect, field.Value, _inputStyle!));
            if (minimum.HasValue && maximum.HasValue && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
                field.Value = Math.Max(minimum.Value, Math.Min(maximum.Value, numeric)).ToString("0.##", CultureInfo.InvariantCulture);
            else field.Value = value;
            GUILayout.EndVertical();
        }

        private void DrawTemperatureValue(WorldProject.Field field, string label, float width)
        {
            GUILayout.BeginVertical(GUILayout.Width(width));
            GUILayout.Label(new GUIContent(label + "  ⓘ", field.Tooltip + " Stored in world XML as kelvin."), _mutedStyle!);
            var rect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true), GUILayout.Height(28));
            RegisterHoverTooltip(rect, field.Tooltip + " Stored in world XML as kelvin.");
            var kelvin = double.TryParse(field.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var stored) ? stored : 273.15;
            var value = ConvertTemperatureFromKelvin(kelvin, _temperatureUnit).ToString("0.##", CultureInfo.InvariantCulture);
            if (_temperatureValueDrafts.TryGetValue(field.Label, out var draft)) value = draft;
            var edited = GUI.TextField(rect, value, _inputStyle!);
            if (edited != value)
            {
                var masked = MaskNumericInput(edited);
                if (double.TryParse(masked, NumberStyles.Float, CultureInfo.InvariantCulture, out var display))
                {
                    field.Value = ConvertTemperatureToKelvin(display, _temperatureUnit).ToString("0.########", CultureInfo.InvariantCulture);
                    _temperatureValueDrafts.Remove(field.Label);
                }
                else _temperatureValueDrafts[field.Label] = masked;
            }
            GUILayout.EndVertical();
        }

        private static double ConvertTemperatureFromKelvin(double kelvin, int unit) => unit == 1 ? kelvin - 273.15 : unit == 2 ? (kelvin - 273.15) * 9 / 5 + 32 : kelvin;
        private static double ConvertTemperatureToKelvin(double value, int unit) => unit == 1 ? value + 273.15 : unit == 2 ? (value - 32) * 5 / 9 + 273.15 : value;
        private static string TemperatureUnitName(int unit) => unit == 1 ? "°C" : unit == 2 ? "°F" : "K";

        private string TooltipTextField(string value, string tooltip, float width, float height, int maxLength = 0, bool numeric = false)
        {
            var rect = width > 0
                ? GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height))
                : GUILayoutUtility.GetRect(0, height, GUILayout.ExpandWidth(true), GUILayout.Height(height));
            RegisterHoverTooltip(rect, tooltip);
            var edited = maxLength > 0 ? GUI.TextField(rect, value, maxLength, _inputStyle!) : GUI.TextField(rect, value, _inputStyle!);
            return numeric ? MaskNumericInput(edited) : edited;
        }

        private void RegisterHoverTooltip(Rect rect, string tooltip)
        {
            // Last-rect coordinates and the current event share the active
            // IMGUI group's origin, including nested cards and scroll views.
            if (!rect.Contains(Event.current.mousePosition)) return;
            _hoverTooltip = tooltip;
        }

        private static string CurveSide(string path)
        {
            if (path.EndsWith("/Day", StringComparison.OrdinalIgnoreCase)) return "Day";
            if (path.EndsWith("/Night", StringComparison.OrdinalIgnoreCase)) return "Night";
            return "curve";
        }

        private static string CurveName(string path)
        {
            var start = path.LastIndexOf('/') + 1;
            return path.Substring(start).Replace("Offset", " ").Replace("Curve", " curve");
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _backgroundTexture = CreateTexture(new Color(0, 0, 0, 0));
            _sidebarTexture = CreateRoundedTexture(Sidebar, new Color(0.31f, 0.30f, 0.34f));
            _panelTexture = CreateRoundedTexture(Panel, new Color(0.34f, 0.33f, 0.37f));
            _raisedTexture = CreateRoundedTexture(PanelRaised, new Color(0.30f, 0.29f, 0.33f));
            var fieldTexture = CreateRoundedTexture(PanelRaised, new Color(0.34f, 0.33f, 0.37f), 5);
            var rowTexture = CreateRoundedTexture(PanelRaised, new Color(0.34f, 0.33f, 0.37f), 6);
            var uiScale = Mathf.Clamp(Screen.height / 1440f, 0.75f, 1.35f);
            _windowStyle = new GUIStyle(GUI.skin.window) { normal = { background = _panelTexture, textColor = Color.white }, onNormal = { background = _panelTexture, textColor = Color.white }, border = new RectOffset(10, 10, 10, 10), padding = new RectOffset(12, 12, 18, 12) };
            _dialogStyle = new GUIStyle(_windowStyle) { normal = { background = _panelTexture, textColor = Color.white }, onNormal = { background = _panelTexture, textColor = Color.white }, border = new RectOffset(12, 12, 12, 12), padding = new RectOffset(18, 18, 18, 14), fontSize = Mathf.RoundToInt(18 * uiScale), fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
            _sidebarStyle = new GUIStyle(GUI.skin.box) { normal = { background = _sidebarTexture }, border = new RectOffset(10, 10, 10, 10), padding = new RectOffset(14, 14, 16, 14), margin = new RectOffset(0, 8, 0, 0), alignment = TextAnchor.UpperLeft };
            var insetCardTexture = CreateInsetCardTexture(PanelRaised, new Color(0.34f, 0.33f, 0.37f));
            _cardStyle = new GUIStyle(GUI.skin.box) { normal = { background = insetCardTexture }, border = new RectOffset(9, 9, 9, 9), padding = new RectOffset(15, 15, 12, 12), margin = new RectOffset(0, 0, 0, 0) };
            _rowStyle = new GUIStyle(GUI.skin.box) { normal = { background = rowTexture }, border = new RectOffset(6, 6, 6, 6), padding = new RectOffset(10, 10, 7, 7), margin = new RectOffset(0, 0, 3, 3) };
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(27 * uiScale), fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, margin = new RectOffset(0, 0, 0, 4) };
            _sectionStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * uiScale), fontStyle = FontStyle.Bold, normal = { textColor = Accent }, margin = new RectOffset(0, 0, 0, 3) };
            _mutedStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(14 * uiScale), wordWrap = true, normal = { textColor = Muted } };
            _navStyle = MakeButtonStyle(Color.white, PanelRaised, TextAnchor.MiddleLeft);
            _navActiveStyle = MakeButtonStyle(Color.white, MenuBlue, TextAnchor.MiddleLeft);
            _templateStyle = MakeButtonStyle(new Color(0.82f, 0.87f, 0.88f), Panel, TextAnchor.MiddleLeft);
            _templateActiveStyle = MakeButtonStyle(Color.white, MenuBlue, TextAnchor.MiddleLeft);
            _buttonStyle = MakeButtonStyle(Color.white, PanelRaised, TextAnchor.MiddleCenter);
            _primaryButtonStyle = MakeButtonStyle(Color.white, MenuBlue, TextAnchor.MiddleCenter);
            _inputStyle = new GUIStyle(GUI.skin.textField) { normal = { textColor = Color.white, background = fieldTexture }, focused = { textColor = Color.white, background = fieldTexture }, border = new RectOffset(5, 5, 5, 5), padding = new RectOffset(9, 9, 6, 6), fontSize = Mathf.RoundToInt(15 * uiScale) };
            _multilineStyle = new GUIStyle(_inputStyle) { wordWrap = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset(9, 9, 8, 8) };
            _tooltipStyle = new GUIStyle(GUI.skin.box) { normal = { background = _raisedTexture, textColor = Color.white }, border = new RectOffset(8, 8, 8, 8), wordWrap = true, alignment = TextAnchor.UpperLeft, fontSize = Mathf.RoundToInt(13 * uiScale), padding = new RectOffset(10, 10, 8, 8) };
            _stylesReady = true;
        }

        private void DrawHoverTooltip(string tooltip)
        {
            if (string.IsNullOrWhiteSpace(tooltip) || _tooltipStyle == null) return;
            var content = new GUIContent(tooltip);
            var width = Mathf.Min(340, Screen.width - 24);
            var height = Mathf.Min(180, _tooltipStyle.CalcHeight(content, width) + 16);
            var mouse = new Vector2(_pointerPosition.x, Screen.height - _pointerPosition.y);
            var x = Mathf.Clamp(mouse.x + 18, 8, Screen.width - width - 8);
            var y = Mathf.Clamp(mouse.y + 20, 8, Screen.height - height - 8);
            GUI.Box(new Rect(x, y, width, height), content, _tooltipStyle);
        }

        private static string MaskNumericInput(string value)
        {
            var chars = value.Where(c => char.IsDigit(c) || c == '.' || c == '-' || c == '+' || c == 'e' || c == 'E').ToArray();
            return new string(chars);
        }

        private GUIStyle MakeButtonStyle(Color text, Color fill, TextAnchor alignment)
        {
            var outline = new Color(0.38f, 0.37f, 0.42f);
            var normalTexture = CreateMenuButtonTexture(fill, outline, 0);
            var hoverTexture = CreateMenuButtonTexture(Color.Lerp(fill, Color.white, 0.10f), Color.Lerp(outline, Color.white, 0.20f), 1);
            var activeTexture = CreateMenuButtonTexture(Color.Lerp(fill, Color.black, 0.12f), outline, 2);
            var uiScale = Mathf.Clamp(Screen.height / 1440f, 0.75f, 1.35f);
            var style = new GUIStyle(GUI.skin.button) { alignment = alignment, fontSize = Mathf.RoundToInt(17 * uiScale), fontStyle = FontStyle.Normal, border = new RectOffset(8, 8, 8, 8), padding = new RectOffset(10, 10, 6, 6), margin = new RectOffset(2, 2, 2, 2) };
            style.normal.background = normalTexture;
            style.hover.background = hoverTexture;
            style.active.background = activeTexture;
            style.focused.background = hoverTexture;
            style.onNormal.background = normalTexture;
            style.onHover.background = hoverTexture;
            style.onActive.background = activeTexture;
            style.onFocused.background = hoverTexture;
            style.normal.textColor = text;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            style.focused.textColor = Color.white;
            return style;
        }

        private Texture2D CreateTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            _styleTextures.Add(texture);
            return texture;
        }

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

        private Texture2D CreateMenuButtonTexture(Color fill, Color outline, int state)
        {
            const int width = 64;
            const int height = 32;
            const int radius = 7;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "World Creator native menu button", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[width * height];
            var top = Color.Lerp(fill, Color.white, state == 0 ? .12f : .20f);
            var bottom = Color.Lerp(fill, Color.black, state == 2 ? .34f : .22f);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var cx = Mathf.Clamp(x, radius, width - radius - 1);
                var cy = Mathf.Clamp(y, radius, height - radius - 1);
                var edge = radius - Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                if (edge < 0) { pixels[y * width + x] = Color.clear; continue; }
                var color = Color.Lerp(bottom, top, y / (float)(height - 1));
                if (edge <= 1.4f) color = outline;
                else if (state != 2 && y >= height - 3) color = Color.Lerp(color, Color.white, .22f);
                else if (y <= 2) color = Color.Lerp(color, Color.black, .35f);
                if (state == 2) color = Color.Lerp(color, Color.black, .10f);
                pixels[y * width + x] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            _styleTextures.Add(texture);
            return texture;
        }

        private Texture2D CreateInsetCardTexture(Color fill, Color outline)
        {
            const int size = 32;
            const int radius = 7;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "World Creator recessed card", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            var inner = Color.Lerp(fill, Sidebar, .25f);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var cx = Mathf.Clamp(x, radius, size - radius - 1);
                var cy = Mathf.Clamp(y, radius, size - radius - 1);
                var edge = radius - Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                if (edge < 0) { pixels[y * size + x] = Color.clear; continue; }
                var color = inner;
                if (edge <= 1.5f) color = outline;
                else if (y >= size - 4 || x <= 2) color = Color.Lerp(inner, Color.white, .08f);
                else if (y <= 2 || x >= size - 3) color = Color.Lerp(inner, Color.black, .22f);
                pixels[y * size + x] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            _styleTextures.Add(texture);
            return texture;
        }

        private void DrawGasControls()
        {
            if (!_project!.HasTemperatureCurve && NativeButton("Add a constant temperature curve (273.15 K)"))
            {
                try { _project.AddTemperatureCurve(); _status = "Added a temperature curve. Edit its key values below."; }
                catch (Exception e) { _status = e.Message; }
            }
            GUILayout.Label(new GUIContent("Add gas  ⓘ", "Add a gas species with a quantity in moles per 8,000-litre atmosphere grid cell. Every species shares the environment temperature; the temperature curve controls the world atmosphere, while this amount changes gas composition."), _mutedStyle!);
            var availableGases = Gases.Where(g => !_project.AtmosphereFields.Any(field => field.Label.Contains("[" + g.Id + "]"))).ToArray();
            GUILayout.BeginHorizontal();
            if (availableGases.Length == 0) GUILayout.Label("All listed gases are already added.", _mutedStyle!);
            else
            {
                _gasSelection = Mathf.Clamp(_gasSelection, 0, availableGases.Length - 1);
                var gasLabels = availableGases.Select(gas => gas.Name).ToArray();
                _gasSelection = NativeDropdown("gas-species", _gasSelection, gasLabels, _buttonStyle!, GUILayout.Width(250));
                GUILayout.Label("Amount (mol)", _mutedStyle!, GUILayout.Width(88));
                _newGasQuantity = TooltipTextField(_newGasQuantity, "Initial moles of this gas per 8,000-litre atmosphere grid cell. The atmosphere's temperature is set by the world temperature curve, not per gas.", 95, 28, numeric: true);
                if (NativeButton("Add gas", _buttonStyle!, GUILayout.Width(100)))
                {
                    if (!double.TryParse(_newGasQuantity, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) || amount < 0 || double.IsNaN(amount) || double.IsInfinity(amount))
                        _status = "Enter a valid non-negative gas amount in moles.";
                    else
                    {
                        try { _project.AddGas(availableGases[_gasSelection].Id, amount); _status = "Added " + availableGases[_gasSelection].Name + " at " + amount.ToString("0.##", CultureInfo.InvariantCulture) + " mol per cell."; }
                        catch (Exception e) { _status = e.Message; }
                    }
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Pressure estimate at temperature (K)  ⓘ", "This reference temperature is used only for the ideal-gas pressure estimate shown here; it does not change the world's temperature curve."), _mutedStyle!);
            _referenceTemperature = TooltipTextField(_referenceTemperature, "Reference temperature for the ideal-gas estimate only. This field does not change world settings.", 100, 26, numeric: true);
            if (double.TryParse(_referenceTemperature, NumberStyles.Float, CultureInfo.InvariantCulture, out var temperature) && temperature > 0 && !double.IsInfinity(temperature))
            {
                var total = 0d;
                var valid = true;
                foreach (var field in _project!.AtmosphereFields.Where(f => f.Label.Contains("/Gas/Gas") && f.Label.EndsWith(" / Quantity")))
                {
                    var gas = GasForField(field);
                    if (gas == null || !gas.IsKnown) { valid = false; break; }
                    if (!double.TryParse(field.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var quantity) || quantity < 0 || double.IsNaN(quantity) || double.IsInfinity(quantity)) { valid = false; break; }
                    if (!gas.IsLiquid) total += quantity;
                }
                GUILayout.Label(valid ? "~" + (total * 8.314462618 * temperature / 8000).ToString("0.##", CultureInfo.InvariantCulture) + " kPa (gaseous species only)" : "Estimate unavailable for this mixture");
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("This ideal-gas estimate excludes liquid species. Real pressure also depends on phase changes and the game's atmosphere simulation.");
            DrawGasComposition();
        }

        private void DrawGasComposition()
        {
            var mixture = new List<(string Id, string Name, double Quantity)>();
            foreach (var field in _project!.AtmosphereFields.Where(f => f.Label.Contains("/Gas/Gas") && f.Label.EndsWith(" / Quantity")))
            {
                var gas = GasForField(field);
                if (gas == null || !double.TryParse(field.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var quantity) ||
                    quantity < 0 || double.IsNaN(quantity) || double.IsInfinity(quantity)) continue;
                mixture.Add((gas.Id, gas.Name, quantity));
            }
            var total = mixture.Sum(g => g.Quantity);
            GUILayout.Label("Gas composition by quantity");
            if (total <= 0) { GUILayout.Label("No gas quantity to display."); return; }
            GUILayout.BeginHorizontal();
            var compositionWidth = Mathf.Min(600f, Mathf.Max(280f, _window.width * .28f));
            GUILayout.BeginVertical(GUILayout.Width(compositionWidth));
            var bar = GUILayoutUtility.GetRect(100, 14, GUILayout.ExpandWidth(true), GUILayout.Height(14));
            GUI.Box(bar, "");
            var x = bar.x + 2;
            var width = Math.Max(1, bar.width - 4);
            var previous = GUI.color;
            for (var i = 0; i < mixture.Count; i++)
            {
                var segment = (float)(width * mixture[i].Quantity / total);
                GUI.color = GasColor(mixture[i].Id);
                GUI.DrawTexture(new Rect(x, bar.y + 2, segment, bar.height - 4), Texture2D.whiteTexture);
                x += segment;
            }
            GUI.color = previous;
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            foreach (var gas in mixture.OrderByDescending(g => g.Quantity))
            {
                GUILayout.BeginHorizontal();
                var swatch = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12), GUILayout.Height(12));
                GUI.color = GasColor(gas.Id);
                GUI.DrawTexture(swatch, Texture2D.whiteTexture);
                GUI.color = previous;
                GUILayout.Space(6);
                GUILayout.Label(gas.Name + "  " + (100 * gas.Quantity / total).ToString("0.0", CultureInfo.InvariantCulture) + "%  (" + gas.Quantity.ToString("0.##", CultureInfo.InvariantCulture) + " mol)", _mutedStyle!);
                GUILayout.EndHorizontal();
            }
        }

        private static GasOption? GasForField(WorldProject.Field field)
        {
            var known = Gases.FirstOrDefault(gas => field.Label.Contains("[" + gas.Id + "]"));
            if (known != null) return known;
            var start = field.Label.LastIndexOf('[', field.Label.Length - 1);
            var end = field.Label.LastIndexOf(']');
            return start >= 0 && end > start ? new GasOption(field.Label.Substring(start + 1, end - start - 1), false) : null;
        }

        private static Color GasColor(string gas)
        {
            var index = Array.FindIndex(Gases, option => option.Id == gas);
            return index >= 0 && index < GasColors.Length
                ? GasColors[index]
                : Color.HSVToRGB((Mathf.Max(0, index) * .618034f) % 1f, .62f, .92f);
        }

        private void DrawWeatherControls()
        {
            var options = _project!.WeatherOptions.ToArray();
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            GUILayout.Label("WEATHER EVENTS", _sectionStyle!);
            GUILayout.Label("Add installed events, create a new effect, or reuse one saved for future projects.", _mutedStyle!);
            if (options.Length > 0)
            {
                _weatherSelection = Math.Min(_weatherSelection, options.Length - 1);
                GUILayout.Label("Installed and saved effects");
                var optionLabels = options.Select(_project.WeatherName).ToArray();
                _weatherSelection = NativeSelectionGrid(_weatherSelection, optionLabels, 3);
                var selected = options[_weatherSelection];
                var selectedName = _project.WeatherName(selected);
                GUILayout.BeginHorizontal();
                if (NativeButton((_project.HasWeather(selected) ? "Remove " : "Add ") + selectedName, _buttonStyle!))
                {
                    try
                    {
                        if (_project.HasWeather(selected)) _project.RemoveWeather(selected); else _project.AddWeather(selected);
                        _status = (_project.HasWeather(selected) ? "Added " : "Removed ") + selectedName + ".";
                    }
                    catch (Exception e) { _status = e.Message; }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(10);
            GUILayout.Label("CREATE A WEATHER EFFECT", _sectionStyle!);
            GUILayout.BeginHorizontal();
            GUILayout.Label("ID", GUILayout.Width(35)); _newWeatherId = GUILayout.TextField(_newWeatherId, _inputStyle!);
            GUILayout.Label("Name", GUILayout.Width(45)); _newWeatherName = GUILayout.TextField(_newWeatherName, _inputStyle!);
            if (NativeButton("Create blank", _primaryButtonStyle!, GUILayout.Width(130)))
                try { _project.CreateWeather(_newWeatherId.Trim(), _newWeatherName.Trim()); _status = "Created weather effect. Add visual/audio blocks, adjust its values, then save it to your library."; }
                catch (Exception e) { _status = e.Message; }
            if (options.Length > 0 && NativeButton("Create from selected", _buttonStyle!, GUILayout.Width(170)))
                try { _project.CreateWeather(_newWeatherId.Trim(), _newWeatherName.Trim(), options[Mathf.Clamp(_weatherSelection, 0, options.Length - 1)]); _status = "Created a custom effect from the selected event. Its values can now be edited independently."; }
                catch (Exception e) { _status = e.Message; }
            GUILayout.EndHorizontal();
            GUILayout.Label("IDs must start with WCWeather_. Blank effects include default timing, fog, temperature, wind, solar and movement values.", _mutedStyle!);
            GUILayout.Label("SAVED FOR FUTURE PROJECTS", _sectionStyle!);
            var saved = _project.SavedWeatherEffects;
            if (saved.Count == 0) GUILayout.Label("No saved effects yet. Save a custom effect to keep it in your personal weather library.", _mutedStyle!);
            foreach (var effect in saved)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(effect.Name + "  ·  " + effect.Id, _mutedStyle!);
                if (NativeButton(_project.HasWeather(effect.Id) ? "Added" : "Add to world", _buttonStyle!, GUILayout.Width(130)) && !_project.HasWeather(effect.Id))
                    try { _project.AddWeather(effect.Id); _status = "Added saved effect " + effect.Name + " to this project."; } catch (Exception e) { _status = e.Message; }
                if (NativeButton("Remove saved copy", _buttonStyle!, GUILayout.Width(150))) _pendingWeatherArchiveId = effect.Id;
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("The library is stored in Documents\\My Games\\Stationeers\\WorldCreator\\WeatherLibrary and is separate from project exports. Save a custom event from its card below.", _mutedStyle!);
            GUILayout.EndVertical();
        }

        private void DrawWeatherTimeline()
        {
            var events = _project!.WeatherTimings;
            if (events.Count == 0) { GUILayout.Label("No weather events are assigned to this world."); return; }
            GUILayout.Label("Cooldown is the minimum days between events (0 does not disable an event); 1 means at least one day between events. Remove an event above to exclude it. Start delay and duration are seconds. In this game build, natural weather is gated until DaysPast > 7 × StartingWeatherMultiplier (default 1, first eligible on Day 8 from Day 0). This gate is separate from these event settings; FirstStormDelay appears unused.");
            foreach (var timing in events)
            {
                GUILayout.Label(timing.Id + "  ·  cooldown " + timing.CooldownMin.ToString("0.##", CultureInfo.InvariantCulture) + "–" + timing.CooldownMax.ToString("0.##", CultureInfo.InvariantCulture) + " days");
                GUILayout.Label("Start delay " + timing.DelayMinSeconds.ToString("0.#", CultureInfo.InvariantCulture) + "–" + timing.DelayMaxSeconds.ToString("0.#", CultureInfo.InvariantCulture) + " s   |   Active " + timing.DurationMinSeconds.ToString("0.#", CultureInfo.InvariantCulture) + "–" + timing.DurationMaxSeconds.ToString("0.#", CultureInfo.InvariantCulture) + " s");
                var max = Math.Max(1, timing.DelayMaxSeconds + timing.DurationMaxSeconds);
                var rect = GUILayoutUtility.GetRect(100, 28, GUILayout.ExpandWidth(true));
                GUI.Box(rect, "");
                DrawTimingScenario(rect, timing.DelayMinSeconds, timing.DurationMinSeconds, max, 7, "Min");
                DrawTimingScenario(rect, timing.DelayMaxSeconds, timing.DurationMaxSeconds, max, 18, "Max");
            }
        }

        private void DrawAssetLibrary()
        {
            if (!_assetLibraryLoaded)
            {
                try
                {
                    _assetLibraryItems.Clear();
                    _assetLibraryItems.AddRange(AssetLibrary.Load());
                    _assetLibraryError = null;
                }
                catch (Exception e) { _assetLibraryError = e.Message; }
                _assetLibraryLoaded = true;
            }

            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            GUILayout.Label("Reusable asset library", _sectionStyle!);
            GUILayout.Label("Library folder: " + AssetLibrary.DirectoryPath, _mutedStyle!);
            GUILayout.Label("Original files are copied into the library and left unchanged.", _mutedStyle!);
            GUILayout.Space(5);
            _assetPath = GUILayout.TextField(_assetPath, _inputStyle!, GUILayout.ExpandWidth(true));
            GUILayout.Label("Enter a full file path. Supported: PNG, JPG, JPEG, DDS, TIF, TIFF, RAW, DATA, DAT.", _mutedStyle!);
            _assetCategorySelection = Mathf.Clamp(_assetCategorySelection, 0, AssetCategories.Length - 1);
            _assetCategorySelection = NativeSelectionGrid(_assetCategorySelection, AssetCategories, 5, _buttonStyle!);
            if (NativeButton("+ Import file to library", _primaryButtonStyle!, GUILayout.Width(210)))
            {
                try
                {
                    var item = AssetLibrary.Import(_assetPath, AssetCategories[_assetCategorySelection]);
                    _assetPath = "";
                    _assetLibraryLoaded = false;
                    _status = "Copied " + item.Name + " into the reusable asset library.";
                }
                catch (Exception e) { _status = e.Message; }
            }
            GUILayout.EndVertical();

            if (!string.IsNullOrEmpty(_assetLibraryError))
            {
                GUILayout.Label(_assetLibraryError, _mutedStyle!);
                return;
            }

            GUILayout.Space(CardSpacing);
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            GUILayout.Label("Browse library", _sectionStyle!);
            _assetTypeFilter = NativeSelectionGrid(_assetTypeFilter, AssetTypeFilters, 3, _buttonStyle!);
            var categoryOptions = new[] { "All categories" }.Concat(AssetCategories).ToArray();
            _assetCategoryFilter = Mathf.Clamp(_assetCategoryFilter, 0, categoryOptions.Length - 1);
            _assetCategoryFilter = NativeSelectionGrid(_assetCategoryFilter, categoryOptions, 3, _buttonStyle!);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", GUILayout.Width(58));
            _assetSearch = GUILayout.TextField(_assetSearch, _inputStyle!, GUILayout.ExpandWidth(true));
            GUILayout.Label(_assetLibraryItems.Count + " in library", _mutedStyle!);
            GUILayout.EndHorizontal();

            var assets = _assetLibraryItems.Where(asset =>
                (_assetTypeFilter == 2 || (_assetTypeFilter == 0) == asset.Kind.StartsWith("Texture", StringComparison.Ordinal)) &&
                (_assetCategoryFilter == 0 || asset.Category == categoryOptions[_assetCategoryFilter]) &&
                (string.IsNullOrWhiteSpace(_assetSearch) || asset.Name.IndexOf(_assetSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    asset.Category.IndexOf(_assetSearch, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
            var pendingAsset = _assetLibraryItems.FirstOrDefault(asset => asset.Id == _pendingAssetRemoveId);
            if (pendingAsset != null)
            {
                GUILayout.BeginHorizontal(_rowStyle!);
                GUILayout.Label("Remove the library copy of " + pendingAsset.Name + "? The original and any project copy will remain unchanged.", _mutedStyle!, GUILayout.ExpandWidth(true));
                if (NativeButton("Keep", _buttonStyle!, GUILayout.Width(90))) _pendingAssetRemoveId = null;
                if (NativeButton("Remove copy", _buttonStyle!, GUILayout.Width(120)))
                {
                    try
                    {
                        AssetLibrary.Remove(pendingAsset.Id);
                        _pendingAssetRemoveId = null;
                        _assetLibraryLoaded = false;
                        _status = "Removed the library copy of " + pendingAsset.Name + ". Existing project copies are unchanged.";
                    }
                    catch (Exception e) { _status = e.Message; }
                }
                GUILayout.EndHorizontal();
            }
            if (assets.Length == 0) GUILayout.Label("No files match this view yet. Import an original file above.", _mutedStyle!);
            foreach (var asset in assets)
            {
                GUILayout.BeginHorizontal(_rowStyle!);
                GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                GUILayout.Label(asset.Name, _mutedStyle!);
                GUILayout.Label(asset.Category + "  ·  " + asset.Kind + "  ·  " + new FileInfo(asset.FullPath).Length.ToString("N0", CultureInfo.InvariantCulture) + " bytes", _mutedStyle!);
                GUILayout.EndVertical();
                var inProject = _project!.ProjectAssets.Any(item => item.Id == asset.Id);
                if (NativeButton(inProject ? "Added" : "Add to project", _buttonStyle!, GUILayout.Width(130)) && !inProject)
                {
                    try { _project.AddLibraryAsset(asset); _status = "Copied " + asset.Name + " into this world project."; }
                    catch (Exception e) { _status = e.Message; }
                }
                if (NativeButton("Remove from library", _buttonStyle!, GUILayout.Width(160))) _pendingAssetRemoveId = asset.Id;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            GUILayout.Space(CardSpacing);
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            GUILayout.Label("Assets in this world", _sectionStyle!);
            if (_project!.ProjectAssets.Count == 0) GUILayout.Label("Add a library file to include it in this world mod.", _mutedStyle!);
            foreach (var asset in _project.ProjectAssets.ToArray())
            {
                GUILayout.BeginHorizontal(_rowStyle!);
                GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                GUILayout.Label(asset.Name + "  ·  " + asset.Category, _mutedStyle!);
                GUILayout.Label(asset.RelativePath(_id), _mutedStyle!);
                GUILayout.EndVertical();
                if (NativeButton("Remove from project", _buttonStyle!, GUILayout.Width(180)))
                    try { _project.RemoveProjectAsset(asset.Id); _status = "Removed " + asset.Name + " from this world project."; } catch (Exception e) { _status = e.Message; }
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("Project copies export under GameData/Worlds/<world ID>/Assets. Use the displayed path in the relevant XML asset field; categorizing a file does not assign it to a game setting.", _mutedStyle!);
            GUILayout.EndVertical();
        }

        private void DrawStartConditions()
        {
            var options = _project!.StartConditionOptions.ToArray();
            var optionLabels = _project.StartConditionLabels.ToArray();
            GUILayout.BeginVertical(_cardStyle!);
            GUILayout.Label("Start from an installed profile", _sectionStyle!);
            GUILayout.Label("Copies the selected game's spawn references into a new world-specific StartCondition. Existing game definitions remain untouched.", _mutedStyle!);
            if (options.Length > 0)
            {
                _startConditionSelection = Mathf.Clamp(_startConditionSelection, 0, options.Length - 1);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Installed profile", _mutedStyle!, GUILayout.Width(140));
                _startConditionSelection = NativeDropdown("start-profile", _startConditionSelection, optionLabels, _buttonStyle!, GUILayout.MinWidth(240), GUILayout.ExpandWidth(true));
                if (NativeButton("Add copy to this world", _primaryButtonStyle!, GUILayout.Width(230)))
                {
                    try
                    {
                        _project.AddStartConditionFrom(options[_startConditionSelection]);
                        _status = "Copied " + options[_startConditionSelection] + " into this world. Set it as default or edit its spawn references below.";
                    }
                    catch (Exception e) { _status = e.Message; }
                }
                GUILayout.EndHorizontal();
            }
            else GUILayout.Label("No installed StartCondition definitions were found.", _mutedStyle!);
            if (NativeButton("+ New blank condition", _buttonStyle!, GUILayout.Width(230)))
                try { _project.CreateBlankStartCondition(); _status = "Created a new condition from the default profile. Edit its spawns or add a supply crate below."; }
                catch (Exception e) { _status = e.Message; }
            GUILayout.EndVertical();
            GUILayout.Space(CardSpacing);

            GUILayout.Label("Starting options in this world", _sectionStyle!);
            var conditions = _project.StartConditions;
            if (conditions.Count == 0)
                GUILayout.Label("This world has no start option. Add a copy of an installed profile above.", _mutedStyle!);
            foreach (var condition in conditions)
            {
                GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
                GUILayout.BeginHorizontal();
                GUILayout.Label(condition.Name + (condition.IsDefault ? "  ·  Default" : "") + (condition.IsCustom ? "  ·  Custom copy" : "  ·  Installed profile"), _sectionStyle!);
                GUILayout.FlexibleSpace();
                if (!condition.IsDefault && NativeButton("Make default", _buttonStyle!, GUILayout.Width(130)))
                    try { _project.SetDefaultStartCondition(condition.Id); _status = condition.Id + " is now the default start."; } catch (Exception e) { _status = e.Message; }
                if (!condition.IsCustom && NativeButton("Customize", _buttonStyle!, GUILayout.Width(110)))
                    try { _project.CustomizeStartCondition(condition.Id); _status = "Created a world-specific copy of " + condition.Id + "."; } catch (Exception e) { _status = e.Message; }
                if (NativeButton("Remove", _buttonStyle!, GUILayout.Width(90)))
                    try { _project.RemoveStartCondition(condition.Id); _status = "Removed " + condition.Id + " from this world."; } catch (Exception e) { _status = e.Message; }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Display name", _mutedStyle!, GUILayout.Width(110));
                var conditionName = GUILayout.TextField(condition.Name, _inputStyle!, GUILayout.ExpandWidth(true));
                if (condition.IsCustom && conditionName != condition.Name)
                    try { _project.SetStartConditionName(condition.Id, conditionName); } catch (Exception e) { _status = e.Message; }
                GUILayout.EndHorizontal();
                if (condition.IsCustom)
                {
                    GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
                    GUILayout.Label("PROFILE OPTIONS", _sectionStyle!);
                    GUILayout.Label("These flags control special rules for this starting profile.", _mutedStyle!);
                    GUILayout.BeginHorizontal();
                    var brutal = GUILayout.Toggle(condition.IsBrutal,
                        new GUIContent("Brutal mode", "Enables the game's brutal start-condition rules for this profile."), _buttonStyle!, GUILayout.Width(170));
                    GUILayout.Label("Uses the game's harsher survival rules.", _mutedStyle!);
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    var terrainEdit = GUILayout.Toggle(condition.IsTerrainEdit,
                        new GUIContent("Terrain edit", "Enables this profile's terrain-edit start behavior."), _buttonStyle!, GUILayout.Width(170));
                    GUILayout.Label("Marks this start as the terrain-editing mode.", _mutedStyle!);
                    GUILayout.EndHorizontal();
                    if (brutal != condition.IsBrutal || terrainEdit != condition.IsTerrainEdit)
                        try { _project.SetStartConditionFlags(condition.Id, brutal, terrainEdit); } catch (Exception e) { _status = e.Message; }
                    GUILayout.EndVertical();
                }
                foreach (var spawn in condition.Spawns.Select((value, index) => (value, index)))
                {
                    GUILayout.BeginVertical(_rowStyle!);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Spawn ID", _mutedStyle!, GUILayout.Width(90));
                    var spawnId = GUILayout.TextField(spawn.value.Id, _inputStyle!, GUILayout.MinWidth(180), GUILayout.ExpandWidth(true));
                    var eventIndex = Array.IndexOf(StartEvents, spawn.value.Event);
                    var nextEventIndex = eventIndex < 0
                        ? -1
                        : NativeSelectionGrid(eventIndex, StartEventLabels, 3, _buttonStyle!, GUILayout.Width(390));
                    if (eventIndex < 0) GUILayout.Label("Unrecognized event: " + spawn.value.Event, _mutedStyle!, GUILayout.Width(210));
                    if (NativeButton("Remove", _buttonStyle!, GUILayout.Width(90)))
                    {
                        try { _project.RemoveStartConditionSpawn(condition.Id, spawn.index); _status = "Removed spawn reference."; } catch (Exception e) { _status = e.Message; }
                        GUILayout.EndHorizontal();
                        GUILayout.EndVertical();
                        break;
                    }
                    if (condition.IsCustom && ((spawnId.Length > 0 && spawnId != spawn.value.Id) || (nextEventIndex >= 0 && nextEventIndex != eventIndex)))
                        try { _project.SetStartConditionSpawn(condition.Id, spawn.index, spawnId, StartEvents[nextEventIndex]); } catch (Exception e) { _status = e.Message; }
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                }
                if (condition.IsCustom)
                {
                    GUILayout.Space(5);
                    GUILayout.Label("Supply crates & kits", _mutedStyle!);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Find game item", _mutedStyle!, GUILayout.Width(110));
                    _startItemSearch = GUILayout.TextField(_startItemSearch, _inputStyle!, GUILayout.ExpandWidth(true));
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Prefab ID", _mutedStyle!, GUILayout.Width(110));
                    _newStartItemId = GUILayout.TextField(_newStartItemId, _inputStyle!, GUILayout.ExpandWidth(true));
                    GUILayout.Label("Qty", _mutedStyle!, GUILayout.Width(30));
                    _newStartItemQuantity = GUILayout.TextField(_newStartItemQuantity, _inputStyle!, GUILayout.Width(70));
                    GUILayout.EndHorizontal();
                    if (_startItemSearch.Length > 0)
                    {
                        var matches = ItemPrefabCatalog.Entries.Where(item => item.Name.IndexOf(_startItemSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            item.Id.IndexOf(_startItemSearch, StringComparison.OrdinalIgnoreCase) >= 0).Take(8).ToArray();
                        foreach (var item in matches)
                            if (NativeButton(item.Name + "  ·  " + item.Id, _templateStyle!, GUILayout.Height(26))) _newStartItemId = item.Id;
                        if (matches.Length == 0) GUILayout.Label("No matching vanilla items. You can enter a custom or modded prefab ID above.", _mutedStyle!);
                    }
                    foreach (var crate in condition.Crates)
                    {
                        GUILayout.BeginVertical(_rowStyle!);
                        GUILayout.BeginHorizontal();
                        var crateName = GUILayout.TextField(crate.Name, _inputStyle!, GUILayout.Width(220));
                        if (crateName != crate.Name)
                            try { _project.SetStartConditionCrateName(crate.Id, crateName); } catch (Exception e) { _status = e.Message; }
                        GUILayout.Label(crate.Id, _mutedStyle!);
                        GUILayout.FlexibleSpace();
                        if (NativeButton("Remove crate", _buttonStyle!, GUILayout.Width(120)))
                            try { _project.RemoveStartConditionCrate(condition.Id, crate.Id); _status = "Removed crate " + crate.Name + "."; } catch (Exception e) { _status = e.Message; }
                        GUILayout.EndHorizontal();
                        foreach (var item in crate.Items.Select((value, index) => (value, index)))
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Item prefab ID", _mutedStyle!, GUILayout.Width(110));
                            var itemId = GUILayout.TextField(item.value.Id, _inputStyle!, GUILayout.ExpandWidth(true));
                            GUILayout.Label("Qty", _mutedStyle!, GUILayout.Width(30));
                            var quantity = GUILayout.TextField(item.value.Quantity.ToString(CultureInfo.InvariantCulture), _inputStyle!, GUILayout.Width(70));
                            if (int.TryParse(quantity, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedQuantity) &&
                                (itemId != item.value.Id || parsedQuantity != item.value.Quantity))
                                try { _project.SetStartConditionCrateItem(crate.Id, item.index, itemId, parsedQuantity); } catch (Exception e) { _status = e.Message; }
                            if (NativeButton("Remove", _buttonStyle!, GUILayout.Width(90)))
                                try { _project.RemoveStartConditionCrateItem(crate.Id, item.index); _status = "Removed crate item."; } catch (Exception e) { _status = e.Message; }
                            GUILayout.EndHorizontal();
                        }
                        GUILayout.BeginHorizontal();
                        if (NativeButton("+ Add selected item", _buttonStyle!, GUILayout.Width(180)))
                        {
                            if (!int.TryParse(_newStartItemQuantity, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity) || quantity < 1)
                                _status = "Item quantity must be at least one.";
                            else try { _project.AddStartConditionCrateItem(crate.Id, _newStartItemId, quantity); _status = "Added " + _newStartItemId + " to " + crate.Name + "."; }
                                catch (Exception e) { _status = e.Message; }
                        }
                        GUILayout.EndHorizontal();
                        GUILayout.Label("Use the exact game item prefab ID; modded item IDs are accepted too.", _mutedStyle!);
                        GUILayout.EndVertical();
                        GUILayout.Space(CardSpacing);
                    }
                    GUILayout.BeginHorizontal();
                    _newStartCrateName = GUILayout.TextField(_newStartCrateName, _inputStyle!, GUILayout.ExpandWidth(true));
                    if (NativeButton("+ Add supply crate", _buttonStyle!, GUILayout.Width(170)))
                        try { _project.AddStartConditionCrate(condition.Id, _newStartCrateName); _status = "Added a new supply crate. Add item prefab IDs to fill it."; }
                        catch (Exception e) { _status = e.Message; }
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    _newStartSpawnId = GUILayout.TextField(_newStartSpawnId, _inputStyle!, GUILayout.MinWidth(180), GUILayout.ExpandWidth(true));
                    _newStartEventSelection = Mathf.Clamp(_newStartEventSelection, 0, StartEvents.Length - 1);
                    _newStartEventSelection = NativeSelectionGrid(_newStartEventSelection, StartEventLabels, 3, _buttonStyle!, GUILayout.Width(390));
                    if (NativeButton("+ Add spawn", _buttonStyle!, GUILayout.Width(120)))
                        try { _project.AddStartConditionSpawn(condition.Id, _newStartSpawnId, StartEvents[_newStartEventSelection]); _status = "Added spawn reference. Confirm that its ID exists in the game or an enabled mod."; } catch (Exception e) { _status = e.Message; }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
                GUILayout.Space(CardSpacing);
            }
            GUILayout.Label("Supply crates export as game Spawn definitions with nested DynamicThing and Item records. Lander and player spawn references still point to existing game or mod definitions.", _mutedStyle!);
        }

        private void DrawWeatherFieldGroups(IReadOnlyList<WorldProject.Field> fields)
        {
            foreach (var eventGroup in fields.GroupBy(f => f.Label.Split('/')[0]))
            {
                var eventId = eventGroup.Key;
                GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
                GUILayout.BeginHorizontal();
                GUILayout.Label(_project!.WeatherName(eventId) + "  ·  " + eventId, _sectionStyle!);
                GUILayout.FlexibleSpace();
                if (_project.IsUserWeather(eventId) && NativeButton("Save for future projects", _buttonStyle!, GUILayout.Width(190)))
                {
                    try
                    {
                        _project.SaveWeatherToLibrary(eventId);
                        _status = "Saved " + _project.WeatherName(eventId) + " for future projects.";
                    }
                    catch (Exception e) { _status = e.Message; }
                }
                GUILayout.EndHorizontal();

                DrawWeatherSection("Event identity & availability", eventGroup.Where(f => IsWeatherIdentityField(f, eventId)), eventId, null);
                DrawWeatherSection("Timing", eventGroup.Where(f => IsWeatherTimingField(f, eventId)), eventId, null);
                DrawWeatherSection("Surface conditions & gameplay", eventGroup.Where(f => IsWeatherConditionField(f, eventId)), eventId, null);

                if (_project!.IsUserWeather(eventId) && DrawWeatherEffectControls(eventId))
                {
                    GUILayout.EndVertical();
                    return;
                }

                var visualFields = eventGroup.Where(f => IsWeatherVisualField(f, eventId)).ToArray();
                foreach (var blockGroup in visualFields.GroupBy(f => WeatherVisualBlockKey(f, eventId)).Where(g => g.Key != null))
                {
                    var blockKey = blockGroup.Key!;
                    var separator = blockKey.IndexOf(' ');
                    var block = separator < 0 ? blockKey : blockKey.Substring(0, separator);
                    var instanceText = separator < 0 ? "1" : new string(blockKey.Substring(separator + 2).TakeWhile(char.IsDigit).ToArray());
                    var instanceIndex = int.TryParse(instanceText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedIndex) ? parsedIndex - 1 : 0;
                    var title = block == "StormEffect" ? "Storm effect" : block == "DirectionalLight" ? "Directional light" : "Fog";
                    if (blockGroup.Count() > 0)
                    {
                        if (DrawWeatherEffectCard(title + (blockKey.Contains(" #") ? " " + blockKey.Substring(separator + 1) : ""), blockGroup.ToArray(), eventId, block, instanceIndex))
                        {
                            GUILayout.EndVertical();
                            return;
                        }
                    }
                }
                if (visualFields.Select(f => WeatherVisualBlockKey(f, eventId)).Count(key => key != null && key.StartsWith("Fog #", StringComparison.Ordinal)) > 1)
                    GUILayout.Label("Multiple Fog cards are editable, but this installed game schema exposes one Fog slot per event; extra Fog blocks may not appear as separate in-game layers.", _mutedStyle!);

                if (DrawWeatherAudio(eventId, eventGroup.Where(f => IsWeatherAudioField(f, eventId)), _project.IsUserWeather(eventId)))
                {
                    GUILayout.EndVertical();
                    return;
                }
                var knownFields = eventGroup.Where(f => IsWeatherIdentityField(f, eventId) || IsWeatherTimingField(f, eventId) ||
                    IsWeatherConditionField(f, eventId) || IsWeatherVisualField(f, eventId) || IsWeatherAudioField(f, eventId)).ToHashSet();
                DrawWeatherSection("Other settings", eventGroup.Where(f => !knownFields.Contains(f)), eventId, null);
                GUILayout.EndVertical();
                GUILayout.Space(CardSpacing);
            }
        }

        private bool DrawWeatherEffectControls(string eventId)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Visual effects", _mutedStyle!);
            GUILayout.FlexibleSpace();
            foreach (var block in new[] { "Fog", "StormEffect", "DirectionalLight" })
            {
                var title = block == "StormEffect" ? "Storm effect" : block == "DirectionalLight" ? "Directional light" : block;
                if (!NativeButton("+ " + title, _buttonStyle!, GUILayout.MinWidth(100))) continue;
                try
                {
                    _project!.AddWeatherBlock(eventId, block);
                    _status = "Added " + title + " card to " + _project.WeatherName(eventId) + ".";
                }
                catch (Exception e) { _status = e.Message; }
                GUILayout.EndHorizontal();
                return true;
            }
            GUILayout.EndHorizontal();
            return false;
        }

        private bool DrawWeatherAudio(string eventId, IEnumerable<WorldProject.Field> source, bool editable)
        {
            var fields = source.ToArray();
            if (fields.Length == 0 && !editable) return false;
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal();
            GUILayout.Label("AUDIO & EVENT FLAGS", _sectionStyle!);
            GUILayout.FlexibleSpace();
            if (editable && NativeButton("+ Add shell sound", _buttonStyle!, GUILayout.Width(175)))
            {
                try
                {
                    _project!.AddWeatherSound(eventId);
                    _status = "Added a shell sound slot to " + _project.WeatherName(eventId) + ".";
                }
                catch (Exception e) { _status = e.Message; }
            }
            GUILayout.EndHorizontal();

            var soundGroups = fields.Select(field => (Field: field, Slot: WeatherShellSoundSlot(field, eventId)))
                .Where(entry => entry.Slot.HasValue).GroupBy(entry => entry.Slot!.Value).OrderBy(group => group.Key).ToArray();
            foreach (var soundGroup in soundGroups)
            {
                GUILayout.BeginVertical(_rowStyle!, GUILayout.ExpandWidth(true));
                GUILayout.BeginHorizontal();
                GUILayout.Label("Shell sound " + soundGroup.Key, _mutedStyle!);
                GUILayout.FlexibleSpace();
                if (editable && NativeButton("Remove", _buttonStyle!, GUILayout.Width(92)))
                {
                    try
                    {
                        _project!.RemoveWeatherSound(eventId, soundGroup.Key);
                        _status = "Removed shell sound " + soundGroup.Key + " from " + _project.WeatherName(eventId) + ".";
                    }
                    catch (Exception e) { _status = e.Message; }
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    GUILayout.EndVertical();
                    return true;
                }
                GUILayout.EndHorizontal();
                foreach (var entry in soundGroup) DrawWeatherValue(entry.Field, eventId, null);
                GUILayout.EndVertical();
                GUILayout.Space(CardSpacing);
            }

            var flags = fields.Where(field => !WeatherShellSoundSlot(field, eventId).HasValue).ToArray();
            DrawWeatherSection("Wind and event flags", flags, eventId, null);
            if (fields.Length == 0) GUILayout.Label("No shell sounds or event audio flags are set.", _mutedStyle!);
            GUILayout.EndVertical();
            GUILayout.Space(CardSpacing);
            return false;
        }

        private static int? WeatherShellSoundSlot(WorldProject.Field field, string eventId)
        {
            var root = WeatherRelativePath(field, eventId).Split('/')[0];
            if (root.Length != 11 || !root.StartsWith("Shell", StringComparison.Ordinal) || !root.EndsWith("Sound", StringComparison.Ordinal) ||
                !int.TryParse(root.Substring(5, 1), NumberStyles.None, CultureInfo.InvariantCulture, out var slot) || slot < 1 || slot > 4) return null;
            return slot;
        }

        private bool DrawWeatherEffectCard(string title, IReadOnlyList<WorldProject.Field> fields, string eventId, string block, int instanceIndex)
        {
            GUILayout.BeginVertical(_rowStyle!, GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal();
            GUILayout.Label(title, _sectionStyle!);
            GUILayout.FlexibleSpace();
            if (NativeButton("Remove", _buttonStyle!, GUILayout.Width(92)))
            {
                try
                {
                    _project!.RemoveWeatherBlock(eventId, block, instanceIndex);
                    _status = "Removed " + title + " from " + _project.WeatherName(eventId) + ".";
                }
                catch (Exception e) { _status = e.Message; }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                return true;
            }
            GUILayout.EndHorizontal();
            for (var i = 0; i < fields.Count; i += 2)
            {
                GUILayout.BeginHorizontal();
                DrawWeatherValue(fields[i], eventId, block);
                if (i + 1 < fields.Count) DrawWeatherValue(fields[i + 1], eventId, block);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
            GUILayout.Space(CardSpacing);
            return false;
        }

        private void DrawWeatherSection(string title, IEnumerable<WorldProject.Field> source, string eventId, string? visualBlock)
        {
            var sectionFields = source.ToArray();
            if (sectionFields.Length == 0) return;
            GUILayout.Space(4);
            GUILayout.Label(title, _mutedStyle!);
            for (var i = 0; i < sectionFields.Length; i += 2)
            {
                GUILayout.BeginHorizontal();
                DrawWeatherValue(sectionFields[i], eventId, visualBlock);
                if (i + 1 < sectionFields.Length) DrawWeatherValue(sectionFields[i + 1], eventId, visualBlock);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawWeatherValue(WorldProject.Field field, string eventId, string? visualBlock)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            var label = new GUIContent(WeatherFieldLabel(field, eventId, visualBlock) + "  ⓘ", field.Tooltip);
            GUILayout.Label(label, _mutedStyle!);
            var rect = GUILayoutUtility.GetRect(80, 28, GUILayout.ExpandWidth(true), GUILayout.Height(28));
            RegisterHoverTooltip(rect, field.Tooltip);
            if (field.IsBoolean)
                field.Value = GUI.Toggle(rect, bool.TryParse(field.Value, out var current) && current, new GUIContent("", field.Tooltip)) ? "true" : "false";
            else
            {
                var value = GUI.TextField(rect, field.Value, _inputStyle!);
                field.Value = field.IsNumeric ? MaskNumericInput(value) : value;
            }
            GUILayout.EndVertical();
        }

        private static string WeatherRelativePath(WorldProject.Field field, string eventId)
        {
            var prefix = eventId + "/";
            var relativePath = field.Label.StartsWith(prefix, StringComparison.Ordinal) ? field.Label.Substring(prefix.Length) : field.Label;
            return relativePath.Replace(" / ", "/").Trim();
        }

        private static bool WeatherPathHas(WorldProject.Field field, string eventId, string segment) =>
            WeatherRelativePath(field, eventId).IndexOf("/" + segment + "/", StringComparison.OrdinalIgnoreCase) >= 0 ||
            WeatherRelativePath(field, eventId).StartsWith(segment + "/", StringComparison.OrdinalIgnoreCase);

        private static string? WeatherVisualBlockKey(WorldProject.Field field, string eventId)
        {
            var root = WeatherRelativePath(field, eventId).Split('/')[0];
            foreach (var block in new[] { "Fog", "StormEffect", "DirectionalLight" })
                if (root == block || root.StartsWith(block + " #", StringComparison.Ordinal)) return root;
            return null;
        }

        private static bool IsWeatherIdentityField(WorldProject.Field field, string eventId)
        {
            var path = WeatherRelativePath(field, eventId);
            return path.Contains("/Name/", StringComparison.OrdinalIgnoreCase) || path.EndsWith("/ActiveInOrbit", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWeatherTimingField(WorldProject.Field field, string eventId) =>
            WeatherPathHas(field, eventId, "CoolDown") || WeatherPathHas(field, eventId, "StartDelay") || WeatherPathHas(field, eventId, "Duration");

        private static bool IsWeatherConditionField(WorldProject.Field field, string eventId)
        {
            var path = WeatherRelativePath(field, eventId);
            return new[] { "TemperatureOffset", "SolarRatio", "WindStrength", "MovementSpeedMultiplier", "DamageMultiplier", "Direction" }
                .Any(segment => WeatherPathHas(field, eventId, segment));
        }

        private static bool IsWeatherVisualField(WorldProject.Field field, string eventId) =>
            new[] { "Fog", "StormEffect", "DirectionalLight" }.Any(segment => WeatherPathHas(field, eventId, segment));

        private static bool IsWeatherAudioField(WorldProject.Field field, string eventId)
        {
            var path = WeatherRelativePath(field, eventId);
            return path.IndexOf("Sound", StringComparison.OrdinalIgnoreCase) >= 0 || path.EndsWith("/WindSound", StringComparison.OrdinalIgnoreCase);
        }

        private static string WeatherFieldLabel(WorldProject.Field field, string eventId, string? visualBlock)
        {
            var path = WeatherRelativePath(field, eventId);
            if (path.IndexOf("/CoolDown/", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Cooldown " + WeatherRangeEnd(path) + " (days)";
            if (path.IndexOf("/StartDelay/", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Start delay " + WeatherRangeEnd(path) + " (seconds)";
            if (path.IndexOf("/Duration/", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Duration " + WeatherRangeEnd(path) + " (seconds)";
            if (path.IndexOf("/TemperatureOffset/Day/", StringComparison.OrdinalIgnoreCase) >= 0) return "Day temperature offset (K)";
            if (path.IndexOf("/TemperatureOffset/Night/", StringComparison.OrdinalIgnoreCase) >= 0) return "Night temperature offset (K)";
            if (WeatherPathHas(field, eventId, "SolarRatio")) return "Sunlight (solar ratio)";
            if (WeatherPathHas(field, eventId, "WindStrength")) return "Wind strength";
            if (WeatherPathHas(field, eventId, "MovementSpeedMultiplier")) return "Movement speed multiplier";
            if (WeatherPathHas(field, eventId, "DamageMultiplier")) return "Damage multiplier";
            if (WeatherPathHas(field, eventId, "Direction")) return "Wind direction";
            if (path.EndsWith("/WindSound", StringComparison.OrdinalIgnoreCase)) return "Wind sound enabled";
            if (path.EndsWith("/ActiveInOrbit", StringComparison.OrdinalIgnoreCase)) return "Can occur in orbit";
            if (path.IndexOf("/Name/", StringComparison.OrdinalIgnoreCase) >= 0) return "Game name key";
            var soundSlot = path.Split('/').FirstOrDefault(part => part.StartsWith("Shell", StringComparison.OrdinalIgnoreCase) && part.EndsWith("Sound", StringComparison.OrdinalIgnoreCase));
            if (soundSlot != null) return soundSlot.Substring(0, soundSlot.Length - "Sound".Length) + " sound (game audio ID)";
            var leaf = ReadableFieldLabel(field.Label, false);
            if (visualBlock != null)
            {
                var visualPath = path;
                var start = visualPath.IndexOf(visualBlock + "/", StringComparison.OrdinalIgnoreCase);
                if (start >= 0) visualPath = visualPath.Substring(start + visualBlock.Length + 1);
                var parts = visualPath.Split('/').Where(p => p.Length > 0).ToArray();
                var names = parts.Select(p => HumanizeXmlName(p.TrimStart('@'))).ToArray();
                if (parts.Length > 0)
                {
                    var channelIndex = parts.Length - 1;
                    var channel = parts[channelIndex].TrimStart('@');
                    if (channel.Equals("r", StringComparison.OrdinalIgnoreCase)) names[channelIndex] = "Red";
                    else if (channel.Equals("g", StringComparison.OrdinalIgnoreCase)) names[channelIndex] = "Green";
                    else if (channel.Equals("b", StringComparison.OrdinalIgnoreCase)) names[channelIndex] = "Blue";
                    else if (channel.Equals("a", StringComparison.OrdinalIgnoreCase)) names[channelIndex] = "Alpha";
                }
                if (names.Length > 1) leaf = string.Join(" / ", names.Skip(Math.Max(0, names.Length - 2)));
                else if (names.Length == 1) leaf = names[0];
            }
            return leaf;
        }

        private static string WeatherRangeEnd(string path)
        {
            if (path.EndsWith("/Min", StringComparison.OrdinalIgnoreCase)) return "minimum";
            if (path.EndsWith("/Max", StringComparison.OrdinalIgnoreCase)) return "maximum";
            return "value";
        }

        private static void DrawTimingScenario(Rect rect, double delay, double duration, double max, float y, string label)
        {
            var left = rect.x + 44;
            var width = Math.Max(1, rect.width - 52);
            var delayWidth = (float)(width * Math.Max(0, delay) / max);
            var durationWidth = (float)(width * Math.Max(0, duration) / max);
            var previous = GUI.color;
            GUI.color = new Color(0.55f, 0.62f, 0.7f, 0.8f);
            GUI.DrawTexture(new Rect(left, rect.y + y, delayWidth, 4), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.42f, 0.12f, 0.95f);
            GUI.DrawTexture(new Rect(left + delayWidth, rect.y + y, durationWidth, 4), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(new Rect(rect.x + 3, rect.y + y - 7, 38, 18), label);
        }

        private void DrawTemperatureChart()
        {
            var keys = _project!.AtmosphereFields.Where(f => f.Label.Contains("/Temperature/Key") && (f.Label.EndsWith(" / Time") || f.Label.EndsWith(" / Value"))).ToList();
            var points = new List<(WorldProject.Field Time, WorldProject.Field Value, Vector2 Position)>();
            foreach (var group in keys.GroupBy(f => f.Label.Substring(0, f.Label.LastIndexOf(" / ", System.StringComparison.Ordinal))))
            {
                var time = group.FirstOrDefault(f => f.Label.EndsWith(" / Time"));
                var value = group.FirstOrDefault(f => f.Label.EndsWith(" / Value"));
                if (time != null && value != null &&
                    double.TryParse(time.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                    double.TryParse(value.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
                    !double.IsNaN(x) && !double.IsInfinity(x) && !double.IsNaN(y) && !double.IsInfinity(y) && x >= 0 && x <= 180)
                    points.Add((time, value, new Vector2((float)x, (float)ConvertTemperatureFromKelvin(y, _temperatureUnit))));
            }
            if (points.Count == 0) return;
            points.Sort((a, b) => a.Position.x.CompareTo(b.Position.x));
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                var kelvin = double.TryParse(point.Value.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 273.15;
                points[i] = (point.Time, point.Value, new Vector2(point.Position.x, (float)ConvertTemperatureFromKelvin(kelvin, _temperatureUnit)));
            }
            var dataMaxKelvin = points.Max(p => ConvertTemperatureToKelvin(p.Position.y, _temperatureUnit));
            if (!_temperatureScaleTopEditing && double.TryParse(_temperatureAxisMaximum, NumberStyles.Float, CultureInfo.InvariantCulture, out var savedTop) && savedTop < dataMaxKelvin)
                _temperatureAxisMaximum = dataMaxKelvin.ToString("0.##", CultureInfo.InvariantCulture);
            var min = (float)ConvertTemperatureFromKelvin(0, _temperatureUnit);
            var autoMaxKelvin = (Math.Floor(dataMaxKelvin / 20) + 1) * 20;
            var topKelvin = autoMaxKelvin;
            var topEntry = _temperatureScaleTopEditing ? _temperatureScaleTopDraft : _temperatureAxisMaximum;
            if (double.TryParse(topEntry, NumberStyles.Float, CultureInfo.InvariantCulture, out var requestedTop) && requestedTop > 0 && !double.IsNaN(requestedTop) && !double.IsInfinity(requestedTop))
                topKelvin = Math.Max(requestedTop, dataMaxKelvin);
            var max = (float)ConvertTemperatureFromKelvin(topKelvin, _temperatureUnit);
            if (_temperatureDragPath != null)
            {
                min = _temperatureDragMin;
                max = _temperatureDragMax;
            }

            var displayPoints = points;
            var horizontalRange = 180f;

            var controlsOnRight = Screen.width >= 1400;
            if (controlsOnRight) GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            GUILayout.Label(new GUIContent("BASE TEMPERATURE CURVE  ⓘ", "Absolute atmosphere temperature in kelvin, sampled against Stationeers' solar-angle input from 0° to 180°. Each solar angle has one curve point."), _sectionStyle!);
            GUILayout.Label("Temperature (" + TemperatureUnitName(_temperatureUnit) + ")", _mutedStyle!);
            var chart = GUILayoutUtility.GetRect(100, 520, GUILayout.ExpandWidth(true));
            GUI.Box(chart, "");
            var plot = new Rect(chart.x + 72, chart.y + 20, chart.width - 94, chart.height - 88);
            var axisLabelStyle = new GUIStyle(_mutedStyle!) { alignment = TextAnchor.MiddleRight };
            var tickTopKelvin = ConvertTemperatureToKelvin(max, _temperatureUnit);
            var labelIntervalKelvin = tickTopKelvin > 240 ? 100d : 20d;
            for (var tickKelvin = 0d; tickKelvin <= tickTopKelvin + 0.001; tickKelvin += 20)
            {
                var tickValue = (float)ConvertTemperatureFromKelvin(tickKelvin, _temperatureUnit);
                var y = plot.y + (max - tickValue) / (max - min) * plot.height;
                if (y < plot.y - 0.5f || y > plot.yMax + 0.5f) continue;
                var majorTick = tickKelvin % labelIntervalKelvin < 0.001;
                GUI.color = new Color(1f, 1f, 1f, majorTick ? 0.22f : 0.07f); GUI.DrawTexture(new Rect(plot.x, y, plot.width, 1), Texture2D.whiteTexture); GUI.color = Color.white;
                if (majorTick || tickKelvin == 0) GUI.Label(new Rect(chart.x + 3, y - 9, 62, 18), tickValue.ToString("0.#", CultureInfo.InvariantCulture), axisLabelStyle);
            }
            GUI.color = new Color(1f, 1f, 1f, 0.7f); GUI.DrawTexture(new Rect(plot.x, plot.y, 1, plot.height), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(plot.x, plot.yMax, plot.width, 1), Texture2D.whiteTexture); GUI.color = Color.white;
            var xAxis = new[] { (0f, "0°"), (0.5f, "90°"), (1f, "180°") };
            foreach (var tick in xAxis) GUI.Label(new Rect(plot.x + tick.Item1 * plot.width - 30, plot.yMax + 2, 60, 18), tick.Item2, _mutedStyle!);
            GUI.Label(new Rect(plot.x, plot.yMax + 20, plot.width, 20),
                "Solar angle · sun overhead (0°) through horizon (90°) to night (180°)", _mutedStyle!);
            var control = GUIUtility.GetControlID(FocusType.Passive, chart);
            var evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && chart.Contains(evt.mousePosition))
            {
                var nearest = displayPoints.OrderBy(p => Vector2.Distance(p.Position, new Vector2(
                    (evt.mousePosition.x - plot.x) / plot.width * horizontalRange,
                    max - (evt.mousePosition.y - plot.y) / plot.height * (max - min)))).First();
                var pointX = plot.x + nearest.Position.x / horizontalRange * plot.width;
                var pointY = plot.y + (max - nearest.Position.y) / (max - min) * plot.height;
                if (Vector2.Distance(evt.mousePosition, new Vector2(pointX, pointY)) <= 12)
                {
                    _temperatureDragPath = nearest.Time.Label.Substring(0, nearest.Time.Label.LastIndexOf(" / ", System.StringComparison.Ordinal));
                    _temperatureDragMin = min;
                    _temperatureDragMax = max;
                    GUIUtility.hotControl = control;
                    evt.Use();
                }
            }
            if (evt.type == EventType.MouseDrag && GUIUtility.hotControl == control && _temperatureDragPath != null)
            {
                var point = points.FirstOrDefault(p => string.Equals(
                    p.Time.Label.Substring(0, p.Time.Label.LastIndexOf(" / ", System.StringComparison.Ordinal)),
                    _temperatureDragPath, System.StringComparison.Ordinal));
                if (point.Time != null && point.Value != null)
                {
                    var horizontalValue = Mathf.Clamp((evt.mousePosition.x - plot.x) / plot.width * horizontalRange, 0, horizontalRange);
                    var x = horizontalValue;
                    var y = _temperatureDragMax - Mathf.Clamp((evt.mousePosition.y - plot.y) / plot.height, 0, 1) * (_temperatureDragMax - _temperatureDragMin);
                    point.Time.Value = x.ToString("0.##", CultureInfo.InvariantCulture);
                    point.Value.Value = ConvertTemperatureToKelvin(y, _temperatureUnit).ToString("0.########", CultureInfo.InvariantCulture);
                    _temperatureValueDrafts.Remove(point.Value.Label);
                }
                evt.Use();
            }
            if (evt.type == EventType.MouseUp && GUIUtility.hotControl == control)
            {
                GUIUtility.hotControl = 0;
                _temperatureDragPath = null;
                evt.Use();
            }
            if (evt.type == EventType.MouseDown && evt.button == 0 && plot.Contains(evt.mousePosition) && GUIUtility.hotControl != control)
            {
                var clickedExisting = displayPoints.Any(p => Vector2.Distance(evt.mousePosition, new Vector2(plot.x + p.Position.x / horizontalRange * plot.width, plot.y + (max - p.Position.y) / (max - min) * plot.height)) <= 12);
                if (!clickedExisting)
                {
                    var horizontalValue = Mathf.Clamp((evt.mousePosition.x - plot.x) / plot.width * horizontalRange, 0, horizontalRange);
                    var x = horizontalValue;
                    var y = max - Mathf.Clamp((evt.mousePosition.y - plot.y) / plot.height, 0, 1) * (max - min);
                    try { _project.AddTemperatureKey(_project.AtmosphereFields, "Atmosphere/GlobalAtmosphere/Temperature", x, ConvertTemperatureToKelvin(y, _temperatureUnit)); _status = "Added a base temperature point."; }
                    catch (Exception e) { _status = e.Message; }
                    evt.Use();
                }
            }
            var oldColor = GUI.color;
            GUI.color = new Color(TemperatureOrange.r, TemperatureOrange.g, TemperatureOrange.b, 0.8f);
            for (var i = 1; i < displayPoints.Count; i++) DrawChartLine(plot, displayPoints[i - 1].Position, displayPoints[i].Position, horizontalRange, min, max);
            GUI.color = oldColor;
            foreach (var point in displayPoints)
            {
                var x = plot.x + point.Position.x / horizontalRange * plot.width;
                var y = plot.y + (max - point.Position.y) / (max - min) * plot.height;
                GUI.DrawTexture(new Rect(x - 4, y - 4, 8, 8), Texture2D.whiteTexture);
            }
            GUILayout.Label("Click an empty point to add a key or drag an existing point. The horizontal axis uses the game's 0°–180° solar-angle input. Temperature values are saved as kelvin.", _mutedStyle!);
            GUILayout.EndVertical();
            if (controlsOnRight) GUILayout.Space(CardSpacing);
            if (controlsOnRight) GUILayout.BeginVertical(GUILayout.Width(300));
            DrawTemperatureUnitCard();
            GUILayout.Space(CardSpacing);
            DrawTemperatureAxisCard();
            GUILayout.Space(CardSpacing);
            DrawTemperatureScaleCard(dataMaxKelvin);
            if (controlsOnRight)
            {
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
        }

        private void DrawTemperatureUnitCard()
        {
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            GUILayout.Label("TEMPERATURE UNITS", _sectionStyle!);
            GUILayout.Label("Display temperature values as", _mutedStyle!);
            var selected = NativeSelectionGrid(_temperatureUnit, new[] { "Kelvin (K)", "Celsius (°C)", "Fahrenheit (°F)" }, 1, _buttonStyle!);
            if (selected != _temperatureUnit)
            {
                _temperatureUnit = selected;
                _temperatureValueDrafts.Clear();
            }
            GUILayout.Label("World values remain stored in kelvin.", _mutedStyle!);
            GUILayout.EndVertical();
        }

        private void DrawTemperatureAxisCard()
        {
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            GUILayout.Label("HORIZONTAL AXIS", _sectionStyle!);
            GUILayout.Label("Solar angle (°)", _mutedStyle!);
            GUILayout.Label("Stationeers input: 0° overhead to 180° night. Each angle appears once.", _mutedStyle!);
            GUILayout.EndVertical();
        }

        private void DrawTemperatureScaleCard(double maximumCurveKelvin)
        {
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            GUILayout.Label("VERTICAL SCALE", _sectionStyle!);
            GUILayout.Label("Scale top (K)", _mutedStyle!);
            var rect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true), GUILayout.Height(28));
            GUI.SetNextControlName("WorldCreatorTemperatureScaleTop");
            var shown = _temperatureScaleTopEditing ? _temperatureScaleTopDraft : _temperatureAxisMaximum;
            var edited = GUI.TextField(rect, shown, _inputStyle!);
            if (edited != shown)
            {
                _temperatureScaleTopDraft = edited;
                _temperatureScaleTopEditing = true;
            }
            var evt = Event.current;
            var enterPressed = evt.type == EventType.KeyDown && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter);
            var commit = _temperatureScaleTopEditing && (enterPressed || (evt.type == EventType.MouseDown && !rect.Contains(evt.mousePosition)) || (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Tab));
            if (commit)
            {
                if (string.IsNullOrWhiteSpace(_temperatureScaleTopDraft)) _temperatureAxisMaximum = "";
                else if (double.TryParse(_temperatureScaleTopDraft, NumberStyles.Float, CultureInfo.InvariantCulture, out var entered) && entered > 0 && !double.IsNaN(entered) && !double.IsInfinity(entered))
                    _temperatureAxisMaximum = Math.Max(entered, maximumCurveKelvin).ToString("0.##", CultureInfo.InvariantCulture);
                else _status = "Enter a valid Kelvin scale top above zero.";
                _temperatureScaleTopDraft = "";
                _temperatureScaleTopEditing = false;
                if (enterPressed) { GUI.FocusControl(null); evt.Use(); }
            }
            GUILayout.Label("The top is in kelvin. It cannot go below the highest curve point. Leave blank to scale automatically. Ticks are 20 K apart.", _mutedStyle!);
            GUILayout.Label("Highest curve value: " + maximumCurveKelvin.ToString("0.##", CultureInfo.InvariantCulture) + " K", _mutedStyle!);
            GUILayout.EndVertical();
        }

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

        private void DrawOrbitPreview()
        {
            var distance = _project!.OrbitFields.FirstOrDefault(f => f.Label.EndsWith(" / SemiMajorAxisAu") || f.Label.EndsWith(" / SemiMajorAxisKm"));
            var eccentricity = _project.OrbitFields.FirstOrDefault(f => f.Label.EndsWith(" / Eccentricity"));
            if (distance == null || eccentricity == null ||
                !double.TryParse(distance.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var a) ||
                !double.TryParse(eccentricity.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var e) ||
                double.IsNaN(a) || double.IsInfinity(a) || double.IsNaN(e) || double.IsInfinity(e) || a <= 0 || e < 0 || e >= 1) return;
            var sideBySide = UnityEngine.Screen.width >= 1400;
            if (sideBySide) GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            var unit = distance.Label.EndsWith("Au") ? "AU" : "km";
            var periapsis = a * (1 - e);
            var apoapsis = a * (1 + e);
            GUILayout.Label("Periapsis: " + periapsis.ToString("0.###", CultureInfo.InvariantCulture) + " " + unit + "    Apoapsis: " + apoapsis.ToString("0.###", CultureInfo.InvariantCulture) + " " + unit);
            var period = _project.OrbitFields.FirstOrDefault(f => f.Label.EndsWith(" / Period"));
            if (period != null && double.TryParse(period.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var days) && !double.IsNaN(days) && !double.IsInfinity(days))
                GUILayout.Label("Orbital period: " + days.ToString("0.###", CultureInfo.InvariantCulture) + " simulated days");
            if (unit == "AU")
            {
                var farSunlight = 1367 / Math.Pow(apoapsis, 2);
                var nearSunlight = 1367 / Math.Pow(periapsis, 2);
                if (!double.IsInfinity(farSunlight) && !double.IsInfinity(nearSunlight))
                    GUILayout.Label("Estimated sunlight: " + farSunlight.ToString("0.#", CultureInfo.InvariantCulture) + "–" + nearSunlight.ToString("0.#", CultureInfo.InvariantCulture) + " W/m² (1367 W/m² at 1 AU)");
            }

            GUILayout.BeginHorizontal();
            var selectedOrbitView = NativeSelectionGrid(_orbitView, new[] { "Above (XZ)", "Side (XY)", "3D", "Landing site" }, 4, _buttonStyle!, GUILayout.MaxWidth(540));
            _orbitView = selectedOrbitView;
            GUILayout.FlexibleSpace();
            if (NativeButton("−", _buttonStyle!, GUILayout.Width(34))) _orbitZoom = Mathf.Clamp(_orbitZoom - 0.2f, 0.6f, 2.4f);
            GUILayout.Label(Mathf.RoundToInt(_orbitZoom * 100f) + "%", _mutedStyle!, GUILayout.Width(44));
            if (NativeButton("+", _buttonStyle!, GUILayout.Width(34))) _orbitZoom = Mathf.Clamp(_orbitZoom + 0.2f, 0.6f, 2.4f);
            if (NativeButton("Reset view", _buttonStyle!, GUILayout.Width(100)))
            {
                _orbitZoom = 1f;
                _orbitGlobeRotation = DefaultOrbitRotation();
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (NativeButton(_orbitSimulationPlaying ? "Pause preview" : "▶ Simulate", _primaryButtonStyle!, GUILayout.Width(150))) _orbitSimulationPlaying = !_orbitSimulationPlaying;
            GUILayout.Label("Simulation speed · 1× = 1 orbit/min", _mutedStyle!, GUILayout.Width(230));
            var speedOptions = new[] { "1×", "10×" };
            var speedIndex = Mathf.Clamp(Array.IndexOf(new[] { 1f, 10f }, _orbitSimulationSpeed), 0, 1);
            var selectedSpeed = NativeSelectionGrid(speedIndex, speedOptions, 2, _buttonStyle!, GUILayout.Width(125));
            _orbitSimulationSpeed = new[] { 1f, 10f }[Mathf.Clamp(selectedSpeed, 0, 1)];
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (_orbitView == 3)
                GUILayout.Label("Landing site · drag to rotate the globe and light reference; simulation moves the light around the stationary globe", _mutedStyle!);
            else
            {
                GUILayout.Label("Position along orbit: " + Mathf.RoundToInt(_orbitPhase) + "°" + (_orbitView == 2 ? "   ·   Drag the preview to rotate" : "") + (_orbitSimulationPlaying ? "   ·   Simulating at " + _orbitSimulationSpeed.ToString("0", CultureInfo.InvariantCulture) + "×" : ""), _mutedStyle!);
                _orbitPhase = GUILayout.HorizontalSlider(_orbitPhase, 0f, 360f);
            }

            // Keep the orbit map large while placing editable values alongside it on wide displays.
            var chartHeight = Mathf.Clamp(UnityEngine.Screen.height * 0.38f, 240f, 560f);
            var chart = GUILayoutUtility.GetRect(100, chartHeight, GUILayout.ExpandWidth(true));
            GUI.Box(chart, "");
            GUI.BeginGroup(chart);
            var localChart = new Rect(0, 0, chart.width, chart.height);
            if (_orbitView == 3)
            {
                var landingPointHidden = DrawLandingSiteSurface(localChart, WorldAngle("Latitude"), WorldAngle("Longitude"));
                GUI.EndGroup();
                if (landingPointHidden)
                    GUILayout.Label("The selected landing point is on the far side; drag the globe to bring it into view.", _mutedStyle!);
                GUILayout.Label("The red marker is the configured landing site. This globe is a coordinate preview; terrain shape and the game's body-axis conventions are not simulated.", _mutedStyle!);
            }
            else
            {
                var inclination = OrbitAngle("Inclination");
                var node = OrbitAngle("LongitudeOfAscendingNode");
                var periapsisAngle = OrbitAngle("ArgumentOfPeriapsis");
                var points = new Vector2[121];
                var maxExtent = 1d;
                for (var i = 0; i < points.Length; i++)
                {
                    var eccentricAnomaly = i * Math.PI * 2 / (points.Length - 1);
                    points[i] = ProjectOrbitPoint(eccentricAnomaly, e, inclination, node, periapsisAngle, _orbitView, _orbitGlobeRotation);
                    maxExtent = Math.Max(maxExtent, Math.Max(Math.Abs(points[i].x), Math.Abs(points[i].y)));
                }
                var margin = 22f;
                var scale = Mathf.Min((chart.width - margin * 2) / (float)(maxExtent * 2), (chart.height - margin * 2) / (float)(maxExtent * 2)) * _orbitZoom;
                var center = new Vector2(chart.width / 2f, chart.height / 2f);
                Vector2 Screen(Vector2 p) => new Vector2(center.x + p.x * scale, center.y - p.y * scale);
                for (var i = 1; i < points.Length; i++) DrawOrbitLine(Screen(points[i - 1]), Screen(points[i]), MenuBlue, 2f);

                var origin = Screen(Vector2.zero);
                if (_orbitView == 2) DrawOrbitAxisTriad(center, scale);
                else
                {
                    DrawOrbitLine(new Vector2(margin, center.y), new Vector2(chart.width - margin, center.y), new Color(.42f, .42f, .46f, .5f), 1f);
                    DrawOrbitLine(new Vector2(center.x, margin), new Vector2(center.x, chart.height - margin), new Color(.42f, .42f, .46f, .5f), 1f);
                }
                DrawOrbitSphere(origin, 30f * _orbitZoom, sun: true);
                var meanAnomaly = _orbitPhase * Math.PI / 180d;
                var eccentricAnomalyAtPhase = SolveEccentricAnomaly(meanAnomaly, e);
                var playable = Screen(ProjectOrbitPoint(eccentricAnomalyAtPhase, e, inclination, node, periapsisAngle, _orbitView, _orbitGlobeRotation));
                DrawOrbitSphere(playable, 19f * _orbitZoom, sun: false);
                var radius = a * (1 - e * Math.Cos(eccentricAnomalyAtPhase));
                GUI.EndGroup();
                GUILayout.Label("Primary body  ·  playable body  ·  Current distance " + radius.ToString("0.###", CultureInfo.InvariantCulture) + " " + unit, _mutedStyle!);
                GUILayout.Label("Illustrative projection from the configured orbital elements; phase and camera controls do not change world XML. Confirm runtime timing and coordinate conventions in game.", _mutedStyle!);
            }
            HandleOrbitPreviewDrag(chart);
            GUILayout.EndVertical();
            GUILayout.Space(CardSpacing);
            if (sideBySide) GUILayout.BeginVertical(_cardStyle!, GUILayout.Width(Mathf.Clamp(UnityEngine.Screen.width * 0.27f, 320f, 520f)), GUILayout.ExpandHeight(true));
            else GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            DrawOrbitSettings();
            GUILayout.EndVertical();
            if (sideBySide) GUILayout.EndHorizontal();
        }

        private void DrawOrbitSphere(Vector2 center, float size, bool sun)
        {
            EnsureOrbitSpherePreviews();
            var texture = sun ? _orbitSunRenderTexture : _orbitPlanetRenderTexture;
            if (texture != null) GUI.DrawTexture(new Rect(center.x - size / 2f, center.y - size / 2f, size, size), texture, ScaleMode.ScaleToFit, true);
        }

        private void EnsureOrbitSpherePreviews()
        {
            var primaryId = _project?.PrimaryBodyId ?? "Sun";
            var primaryTexturePath = _project?.GetPrimaryBodyTexturePath(primaryId);
            var primaryIsBlackHole = _project?.IsBlackHolePrimary(primaryId) ?? false;
            var primaryIsStar = !primaryIsBlackHole && (_project?.IsStarPrimary(primaryId) ?? false);
            if (!string.Equals(primaryId, _orbitPrimaryBodyIdRendered, StringComparison.Ordinal)
                || !string.Equals(primaryTexturePath, _orbitPrimaryTexturePath, StringComparison.OrdinalIgnoreCase))
            {
                if (_orbitPrimarySurfaceTexture != null) Destroy(_orbitPrimarySurfaceTexture);
                var primaryData = LoadOrbitTextureFile(primaryTexturePath);
                _orbitPrimarySurfaceTexture = primaryData ?? (primaryIsBlackHole
                    ? CreateBlackHoleTexture()
                    : primaryIsStar ? CreateSunTexture() : CreatePrimaryBodyTexture(_project?.GetPrimaryBodyColor(primaryId)));
                _orbitPrimaryBodyIdRendered = primaryId;
                _orbitPrimaryTexturePath = primaryTexturePath;
                _orbitPrimaryIsBlackHole = primaryIsBlackHole;
                _orbitPrimaryIsStar = primaryIsStar;
                _orbitSphereTexturesDirty = true;
            }

            var surfaceAsset = _project?.ProjectAssets
                .Where(asset => asset.Category.Equals("Surface", StringComparison.OrdinalIgnoreCase)
                    && (asset.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                        || asset.Extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                        || asset.Extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(asset => asset.Name.IndexOf("ground", StringComparison.OrdinalIgnoreCase) >= 0
                    || asset.Name.IndexOf("surface", StringComparison.OrdinalIgnoreCase) >= 0
                    || asset.Name.IndexOf("terrain", StringComparison.OrdinalIgnoreCase) >= 0)
                .FirstOrDefault();
            var surfaceData = LoadOrbitWorldGroundTexture() ?? surfaceAsset?.Data;
            if (_orbitPlanetSurfaceTexture == null || !ReferenceEquals(surfaceData, _orbitSourceSurfaceData))
            {
                if (_orbitPlanetSurfaceTexture != null) Destroy(_orbitPlanetSurfaceTexture);
                _orbitPlanetSurfaceTexture = surfaceData == null ? CreateBluePlanetTexture() : LoadOrbitTexture(surfaceData) ?? CreateBluePlanetTexture();
                _orbitSourceSurfaceData = surfaceData;
                _orbitSphereTexturesDirty = true;
            }

            if (_orbitPrimarySurfaceTexture == null) _orbitPrimarySurfaceTexture = CreateSunTexture();
            if (_orbitSunRenderTexture == null) _orbitSunRenderTexture = CreateOrbitRenderTexture("World Creator sun sphere");
            if (_orbitPlanetRenderTexture == null) _orbitPlanetRenderTexture = CreateOrbitRenderTexture("World Creator planet sphere");
            if (_orbitSphereCamera == null)
            {
                var cameraObject = new GameObject("World Creator orbit preview camera") { hideFlags = HideFlags.HideAndDontSave, layer = 31 };
                _orbitSphereCamera = cameraObject.AddComponent<Camera>();
                _orbitSphereCamera.enabled = false;
                _orbitSphereCamera.clearFlags = CameraClearFlags.SolidColor;
                _orbitSphereCamera.backgroundColor = new Color(0, 0, 0, 0);
                _orbitSphereCamera.orthographic = true;
                _orbitSphereCamera.orthographicSize = 1.08f;
                _orbitSphereCamera.nearClipPlane = .1f;
                _orbitSphereCamera.farClipPlane = 10f;
                _orbitSphereCamera.cullingMask = 1 << 31;
                cameraObject.transform.position = new Vector3(0, 0, -4);
                cameraObject.transform.rotation = Quaternion.identity;

                _orbitSphereObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _orbitSphereObject.name = "World Creator orbit preview sphere";
                _orbitSphereObject.hideFlags = HideFlags.HideAndDontSave;
                _orbitSphereObject.layer = 31;
                _orbitSphereObject.transform.position = Vector3.zero;
                _orbitSphereObject.transform.localScale = Vector3.one * 1.8f;
                var collider = _orbitSphereObject.GetComponent("SphereCollider");
                if (collider != null) Destroy(collider);
                _orbitSphereRenderer = _orbitSphereObject.GetComponent<MeshRenderer>();
                _orbitSphereRenderer.enabled = false;
                var shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Diffuse");
                if (shader != null) _orbitSphereMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                if (_orbitSphereMaterial != null) _orbitSphereRenderer.sharedMaterial = _orbitSphereMaterial;
                if (_orbitSphereMaterial != null)
                {
                    if (_orbitSphereMaterial.HasProperty("_Metallic")) _orbitSphereMaterial.SetFloat("_Metallic", 0f);
                    if (_orbitSphereMaterial.HasProperty("_Glossiness")) _orbitSphereMaterial.SetFloat("_Glossiness", .12f);
                }

                var lightObject = new GameObject("World Creator orbit preview light") { hideFlags = HideFlags.HideAndDontSave, layer = 31 };
                _orbitSphereLight = lightObject.AddComponent<Light>();
                _orbitSphereLight.type = LightType.Spot;
                _orbitSphereLight.renderMode = LightRenderMode.ForcePixel;
                _orbitSphereLight.intensity = 24f;
                _orbitSphereLight.range = 8f;
                _orbitSphereLight.spotAngle = 55f;
                _orbitSphereLight.cullingMask = 1 << 31;
            }

            if (_orbitSphereCamera == null || _orbitSphereObject == null || _orbitSphereRenderer == null || _orbitSphereMaterial == null) return;
            var spin = _orbitView == 3 ? 0f : _orbitSpinPhase;
            var viewRotation = _orbitGlobeRotation;
            var sphereRotation = _orbitGlobeRotation * Quaternion.Euler(0f, spin, 0f);
            var sourceDirection = CurrentPrimaryLightDirection();
            var lightDirectionView = (viewRotation * sourceDirection).normalized;
            if (!_orbitSphereTexturesDirty
                && Quaternion.Angle(_orbitSphereRenderedRotation, _orbitGlobeRotation) < .01f
                && Mathf.Approximately(_orbitSphereRenderedSpin, spin)
                && Vector3.Angle(_orbitRenderedLightDirectionView, lightDirectionView) < .01f) return;
            _orbitSphereObject.transform.rotation = sphereRotation;
            _orbitLightDirectionView = lightDirectionView;
            _orbitSphereLight!.transform.position = _orbitLightDirectionView * 3.5f;
            _orbitSphereLight.transform.rotation = Quaternion.LookRotation(-_orbitLightDirectionView);
            _orbitSphereRenderer.enabled = true;
            RenderOrbitSphere(_orbitPrimarySurfaceTexture!, _orbitSunRenderTexture!, emissive: _orbitPrimaryIsBlackHole || _orbitPrimaryIsStar);
            RenderOrbitSphere(_orbitPlanetSurfaceTexture!, _orbitPlanetRenderTexture!, emissive: false);
            _orbitSphereRenderer.enabled = false;
            _orbitSphereRenderedSpin = spin;
            _orbitSphereRenderedRotation = _orbitGlobeRotation;
            _orbitRenderedLightDirectionView = _orbitLightDirectionView;
            _orbitSphereTexturesDirty = false;
        }

        private byte[]? LoadOrbitWorldGroundTexture()
        {
            var sourcePath = _project?.MiniMapAssetPath;
            if (string.IsNullOrWhiteSpace(sourcePath)) return null;
            var relativePath = sourcePath.Replace('/', Path.DirectorySeparatorChar);
            var path = sourcePath;
            if (!Path.IsPathRooted(sourcePath))
            {
                var modPath = string.IsNullOrEmpty(_project?.SourceAssetRoot) ? string.Empty : Path.Combine(_project.SourceAssetRoot, relativePath);
                var gameDataPath = string.IsNullOrEmpty(_project?.SourceGameDataRoot) ? string.Empty : Path.Combine(_project.SourceGameDataRoot, relativePath);
                var gamePath = Path.Combine(Application.dataPath, "StreamingAssets", relativePath);
                path = File.Exists(modPath) ? modPath : File.Exists(gameDataPath) ? gameDataPath : gamePath;
            }
            if (string.Equals(path, _orbitGroundTexturePath, StringComparison.OrdinalIgnoreCase)) return _orbitGroundTextureData;
            _orbitGroundTexturePath = path;
            try { _orbitGroundTextureData = File.Exists(path) ? File.ReadAllBytes(path) : null; }
            catch { _orbitGroundTextureData = null; }
            return _orbitGroundTextureData;
        }

        private void RenderOrbitSphere(Texture2D surface, RenderTexture target, bool emissive)
        {
            _orbitSphereMaterial!.mainTexture = surface;
            _orbitSphereMaterial.color = Color.white;
            if (_orbitSphereMaterial.HasProperty("_EmissionColor"))
            {
                _orbitSphereMaterial.SetColor("_EmissionColor", emissive ? new Color(.75f, .31f, .04f) : Color.black);
                if (emissive) _orbitSphereMaterial.EnableKeyword("_EMISSION");
            }
            _orbitSphereCamera!.targetTexture = target;
            _orbitSphereCamera.Render();
            _orbitSphereCamera.targetTexture = null;
        }

        private static RenderTexture CreateOrbitRenderTexture(string name)
        {
            var texture = new RenderTexture(128, 128, 16, RenderTextureFormat.ARGB32)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                antiAliasing = 2
            };
            texture.Create();
            return texture;
        }

        private static Texture2D? LoadOrbitTexture(byte[] data)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            if (texture.LoadImage(data)) return texture;
            Destroy(texture);
            return null;
        }

        private static Texture2D? LoadOrbitTextureFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try { return File.Exists(path) ? LoadOrbitTexture(File.ReadAllBytes(path)) : null; }
            catch { return null; }
        }

        private static Texture2D CreatePrimaryBodyTexture((float R, float G, float B)? bodyColor)
        {
            const int width = 256;
            const int height = 128;
            var baseColor = bodyColor.HasValue ? new Color(bodyColor.Value.R, bodyColor.Value.G, bodyColor.Value.B) : new Color(.43f, .38f, .31f);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { name = "World Creator primary-body surface", hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var noise = Mathf.PerlinNoise(x * .045f, y * .045f) * .28f + Mathf.PerlinNoise(x * .012f + 11f, y * .012f) * .32f;
                pixels[y * width + x] = Color.Lerp(baseColor * .65f, baseColor * 1.35f, noise);
            }
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Texture2D CreateBlackHoleTexture()
        {
            const int width = 256;
            const int height = 128;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { name = "World Creator black-hole event horizon", hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var latitude = Math.Abs((y + .5f) / height - .5f) * 2f;
                var band = Mathf.Clamp01(1f - Mathf.Abs(latitude - .12f) / .11f);
                var turbulence = Mathf.PerlinNoise(x * .09f, y * .045f);
                pixels[y * width + x] = Color.Lerp(new Color(.008f, .006f, .015f), new Color(1f, .21f, .035f), band * (.55f + turbulence * .45f));
            }
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Texture2D CreateBluePlanetTexture()
        {
            const int width = 256;
            const int height = 128;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { name = "World Creator blue planet surface", hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var noise = Mathf.PerlinNoise(x * .035f, y * .035f) * .12f + Mathf.PerlinNoise(x * .012f + 13.7f, y * .012f) * .1f;
                var land = Mathf.PerlinNoise(x * .022f + 21f, y * .022f + 5f) > .63f;
                var shade = .86f + noise;
                pixels[y * width + x] = land
                    ? new Color(.20f * shade, .54f * shade, .67f * shade)
                    : new Color(.12f * shade, .43f * shade, .62f * shade);
            }
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Texture2D CreateSunTexture()
        {
            const int width = 256;
            const int height = 128;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true) { name = "World Creator solar photosphere", hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var grain = Mathf.PerlinNoise(x * .075f, y * .075f) * .5f + Mathf.PerlinNoise(x * .025f + 7f, y * .025f + 11f) * .5f;
                pixels[y * width + x] = Color.Lerp(new Color(1f, .24f, .025f), new Color(1f, .82f, .22f), .35f + grain * .65f);
            }
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private void DrawOrbitSettings()
        {
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            GUILayout.Label("ORBITAL ELEMENTS", _sectionStyle!);
            foreach (var field in _project!.OrbitFields.Where(field => !field.Label.StartsWith("Celestial body/", StringComparison.Ordinal)))
                DrawOrbitField(field);
            GUILayout.EndVertical();
            GUILayout.Space(CardSpacing);
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            GUILayout.Label("CELESTIAL BODY", _sectionStyle!);
            GUILayout.Label("Reference position, appearance, and sunrise timing for the playable body.", _mutedStyle!);
            foreach (var field in _project.OrbitFields.Where(field => field.Label.StartsWith("Celestial body/", StringComparison.Ordinal)))
                DrawOrbitField(field);
            GUILayout.EndVertical();
            GUILayout.Space(8);
            GUILayout.Label("LANDING SITE", _sectionStyle!);
            GUILayout.Label("Coordinates on the playable body's surface.", _mutedStyle!);
            DrawLandingCoordinate("Latitude", "Latitude", "−90° to 90°");
            DrawLandingCoordinate("Longitude", "Longitude", "−180° to 180°");
        }

        private void DrawOrbitField(WorldProject.Field field)
        {
            GUILayout.BeginVertical(_rowStyle!);
            var content = new GUIContent(ReadableFieldLabel(field.Label, false) + "  ⓘ", field.Tooltip);
            GUILayout.Label(content, _mutedStyle!);
            RegisterHoverTooltip(GUILayoutUtility.GetLastRect(), field.Tooltip);
            if (field.IsBoolean)
                field.Value = GUILayout.Toggle(bool.TryParse(field.Value, out var current) && current, new GUIContent("Enabled", field.Tooltip)) ? "true" : "false";
            else
            {
                var value = GUILayout.TextField(field.Value, _inputStyle!, GUILayout.ExpandWidth(true));
                field.Value = field.IsNumeric ? MaskNumericInput(value) : value;
            }
            GUILayout.EndVertical();
        }

        private void DrawLandingCoordinate(string suffix, string label, string range)
        {
            var field = _project!.WorldFields.FirstOrDefault(f => f.Label.EndsWith(" / " + suffix, StringComparison.Ordinal));
            if (field == null) return;
            GUILayout.BeginVertical(_rowStyle!);
            GUILayout.Label(label + " (°)   " + range, _mutedStyle!);
            var value = GUILayout.TextField(field.Value, _inputStyle!, GUILayout.ExpandWidth(true));
            field.Value = MaskNumericInput(value);
            GUILayout.EndVertical();
        }

        private void DrawOrbitAxisTriad(Vector2 center, float scale)
        {
            var length = Mathf.Min(0.7f * scale, 84f);
            Vector2 ProjectAxis(double x, double y, double z)
            {
                var view = _orbitGlobeRotation * new Vector3((float)x, (float)y, (float)z);
                return new Vector2(view.x * length, -view.y * length);
            }
            var origin = center;
            void Axis(double x, double y, double z, string label, Color color)
            {
                var delta = ProjectAxis(x, y, z);
                var end = origin + delta;
                DrawOrbitLine(origin, end, color, 2f);
                DrawOrbitMarker(end, 5f, color);
                GUI.Label(new Rect(end.x + 3, end.y - 9, 20, 18), label, _mutedStyle!);
            }
            Axis(1, 0, 0, "X", new Color(.95f, .38f, .38f));
            Axis(0, 1, 0, "Y", new Color(.35f, .58f, 1f));
            Axis(0, 0, 1, "Z", new Color(.38f, .83f, .51f));
        }

        private double WorldAngle(string suffix)
        {
            var field = _project!.WorldFields.FirstOrDefault(f => f.Label.EndsWith(" / " + suffix, StringComparison.Ordinal));
            return field != null && double.TryParse(field.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
                !double.IsNaN(value) && !double.IsInfinity(value) ? value : 0d;
        }

        private bool DrawLandingSiteSurface(Rect chart, double latitude, double longitude)
        {
            EnsureOrbitSpherePreviews();
            var radius = Mathf.Min(Mathf.Max(18f, Mathf.Min(chart.width, chart.height) * .39f * _orbitZoom), Mathf.Min(chart.width, chart.height) * .47f);
            var center = new Vector2(chart.center.x, chart.center.y);
            if (_orbitPlanetRenderTexture != null)
                GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2), _orbitPlanetRenderTexture, ScaleMode.StretchToFill, true);
            var surfaceRadius = radius * (0.9f / 1.08f);
            var lightScreenDirection = new Vector2(_orbitLightDirectionView.x, -_orbitLightDirectionView.y);
            var lightMarker = center + lightScreenDirection * (surfaceRadius + 15f);
            DrawOrbitLine(center + lightScreenDirection * (surfaceRadius * .88f), lightMarker, new Color(1f, .76f, .28f, .8f), 2f);
            DrawOrbitMarker(lightMarker, 9f, new Color(1f, .76f, .28f));
            var dim = new Color(.72f, .84f, .87f, .48f);
            for (var meridian = -150; meridian <= 180; meridian += 30)
                DrawGlobeMeridian(center, surfaceRadius, meridian, dim, 1f);
            for (var latitudeLine = -60; latitudeLine <= 60; latitudeLine += 30)
                DrawGlobeLatitude(center, surfaceRadius, latitudeLine, dim, 1f);
            DrawGlobeMeridian(center, surfaceRadius, longitude, Accent, 2f);
            DrawGlobeLatitude(center, surfaceRadius, latitude, TemperatureOrange, 2f);

            var marker = ProjectGlobePoint(latitude, longitude, center, surfaceRadius);
            if (marker.Depth <= 0)
            {
                DrawOrbitMarker(marker.Point, 9f, new Color(1f, .2f, .24f));
                return false;
            }
            return true;
        }

        private void DrawGlobeMeridian(Vector2 center, float radius, double longitude, Color color, float thickness)
        {
            var hasPrevious = false;
            var previous = Vector2.zero;
            for (var latitude = -90; latitude <= 90; latitude += 3)
            {
                var projected = ProjectGlobePoint(latitude, longitude, center, radius);
                if (projected.Depth > 0) { hasPrevious = false; continue; }
                if (hasPrevious) DrawOrbitLine(previous, projected.Point, color, thickness);
                previous = projected.Point;
                hasPrevious = true;
            }
        }

        private void DrawGlobeLatitude(Vector2 center, float radius, double latitude, Color color, float thickness)
        {
            var hasPrevious = false;
            var previous = Vector2.zero;
            for (var longitude = 0; longitude <= 360; longitude += 3)
            {
                var projected = ProjectGlobePoint(latitude, longitude, center, radius);
                if (projected.Depth > 0) { hasPrevious = false; continue; }
                if (hasPrevious) DrawOrbitLine(previous, projected.Point, color, thickness);
                previous = projected.Point;
                hasPrevious = true;
            }
        }

        private (Vector2 Point, double Depth) ProjectGlobePoint(double latitude, double longitude, Vector2 center, float radius)
        {
            var lat = latitude * Math.PI / 180d;
            var lon = longitude * Math.PI / 180d;
            var x = Math.Cos(lat) * Math.Sin(lon);
            var y = Math.Sin(lat);
            var z = Math.Cos(lat) * Math.Cos(lon);
            var rotated = _orbitGlobeRotation * new Vector3((float)x, (float)y, (float)z);
            return (new Vector2(center.x + rotated.x * radius, center.y - rotated.y * radius), rotated.z);
        }

        private static Quaternion DefaultOrbitRotation()
        {
            return Quaternion.AngleAxis(28f, Vector3.right) * Quaternion.AngleAxis(35f, Vector3.up);
        }

        private void HandleOrbitPreviewDrag(Rect chart)
        {
            var current = Event.current;
            var control = GUIUtility.GetControlID(FocusType.Passive, chart);
            if (_orbitView >= 2 && current.type == EventType.MouseDown && current.button == 0 && chart.Contains(current.mousePosition))
            {
                _orbitDragging = true;
                _orbitDragPosition = current.mousePosition;
                GUIUtility.hotControl = control;
                current.Use();
            }
            else if (_orbitDragging && GUIUtility.hotControl == control && current.type == EventType.MouseDrag)
            {
                var delta = current.mousePosition - _orbitDragPosition;
                _orbitDragPosition = current.mousePosition;
                // Rotate around screen axes, so front-facing surface points follow the pointer at every globe orientation.
                var dragAxis = new Vector3(-delta.y, -delta.x, 0f);
                if (dragAxis.sqrMagnitude > 0.000001f)
                    _orbitGlobeRotation = (Quaternion.AngleAxis(delta.magnitude * .6f, dragAxis.normalized) * _orbitGlobeRotation).normalized;
                current.Use();
            }
            else if (_orbitDragging && GUIUtility.hotControl == control && (current.type == EventType.MouseUp || current.type == EventType.MouseLeaveWindow))
            {
                _orbitDragging = false;
                GUIUtility.hotControl = 0;
                current.Use();
            }
        }

        private double OrbitAngle(string suffix)
        {
            var field = _project!.OrbitFields.FirstOrDefault(f => f.Label.EndsWith(" / " + suffix, StringComparison.Ordinal));
            return field != null && double.TryParse(field.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && !double.IsNaN(value) && !double.IsInfinity(value) ? value * Math.PI / 180d : 0d;
        }

        private Vector3 CurrentPrimaryLightDirection()
        {
            var eccentricityField = _project!.OrbitFields.FirstOrDefault(f => f.Label.EndsWith(" / Eccentricity", StringComparison.Ordinal));
            if (eccentricityField == null || !double.TryParse(eccentricityField.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var eccentricity)
                || double.IsNaN(eccentricity) || double.IsInfinity(eccentricity) || eccentricity < 0 || eccentricity >= 1)
                return Vector3.back;
            var position = CalculateOrbitPosition(
                SolveEccentricAnomaly(_orbitPhase * Math.PI / 180d, eccentricity),
                eccentricity, OrbitAngle("Inclination"), OrbitAngle("LongitudeOfAscendingNode"), OrbitAngle("ArgumentOfPeriapsis"));
            return new Vector3((float)-position.x, (float)-position.z, (float)-position.y).normalized;
        }

        private static double SolveEccentricAnomaly(double meanAnomaly, double eccentricity)
        {
            var eccentricAnomaly = meanAnomaly;
            for (var i = 0; i < 8; i++)
                eccentricAnomaly -= (eccentricAnomaly - eccentricity * Math.Sin(eccentricAnomaly) - meanAnomaly) / (1 - eccentricity * Math.Cos(eccentricAnomaly));
            return eccentricAnomaly;
        }

        private static Vector3 CalculateOrbitPosition(double eccentricAnomaly, double eccentricity, double inclination, double node, double periapsis)
        {
            var x = Math.Cos(eccentricAnomaly) - eccentricity;
            var y = Math.Sqrt(1 - eccentricity * eccentricity) * Math.Sin(eccentricAnomaly);
            var cosPeriapsis = Math.Cos(periapsis);
            var sinPeriapsis = Math.Sin(periapsis);
            var inPlaneX = cosPeriapsis * x - sinPeriapsis * y;
            var inPlaneY = sinPeriapsis * x + cosPeriapsis * y;
            var cosNode = Math.Cos(node);
            var sinNode = Math.Sin(node);
            var cosInclination = Math.Cos(inclination);
            var sinInclination = Math.Sin(inclination);
            var orbitX = cosNode * inPlaneX - sinNode * cosInclination * inPlaneY;
            var orbitY = sinNode * inPlaneX + cosNode * cosInclination * inPlaneY;
            var orbitZ = sinInclination * inPlaneY;
            return new Vector3((float)orbitX, (float)orbitY, (float)orbitZ);
        }

        private static Vector2 ProjectOrbitPoint(double eccentricAnomaly, double eccentricity, double inclination, double node, double periapsis, int view, Quaternion viewRotation)
        {
            var orbit = CalculateOrbitPosition(eccentricAnomaly, eccentricity, inclination, node, periapsis);
            // Stationeers' preview uses Unity axes: X/Z is the orbital plane and
            // Y is its normal. Above therefore shows the full orbit; Side shows tilt.
            if (view == 0) return new Vector2(orbit.x, orbit.y);
            if (view == 1) return new Vector2(orbit.x, orbit.z);
            var rotated = viewRotation * new Vector3(orbit.x, orbit.z, orbit.y);
            return new Vector2(rotated.x, rotated.y);
        }

        private static void DrawOrbitMarker(Vector2 point, float size, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(point.x - size / 2f, point.y - size / 2f, size, size), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void DrawOrbitLine(Vector2 from, Vector2 to, Color color, float thickness)
        {
            var delta = to - from;
            if (delta.sqrMagnitude < 0.01f) return;
            var previousMatrix = GUI.matrix;
            var previousColor = GUI.color;
            try
            {
                GUI.color = color;
                GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
                GUI.DrawTexture(new Rect(from.x, from.y - thickness / 2f, delta.magnitude, thickness), Texture2D.whiteTexture);
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
            }
        }

        private List<WorldProject.Field> Fields()
        {
            switch (_page)
            {
                case 1: return _project!.AtmosphereFields;
                case 2: return _project!.WeatherFields;
                case 3: return _project!.OrbitFields;
                case 4: return _project!.ResourceFields;
                case 6: return _project!.AdvancedFields;
                case 7: return _project!.MapFields;
                default: return _project!.WorldFields;
            }
        }

        private string PageDescription()
        {
            switch (_page)
            {
                case 1: return "Gas quantities: moles per 8,000 L. Temperature values: kelvin; curve Time is the input coordinate.";
                case 2: return "Add or remove weather events, then adjust their timing and effects.";
                case 3: return "Distance uses AU or km as labelled. Changes create a private celestial body; verify orbit in-game.";
                case 4: return "OreDensity multipliers come from this template's world-region entries.";
                case 5: return "Source maps can be compiled with the game's Terrain Tools; compiled chunks can be packaged with this world.";
                case 6: return "Advanced XML-backed fields include strings and booleans as well as numbers. Unchanged values and unrecognized nodes are preserved.";
                case 7: return "Preview and paint region masks. Painting edits this new world copy and includes a custom mask texture in the created mod.";
                case 8: return "Choose an installed starting profile, copy it into this world, and adjust its spawn roles without changing the installed game data.";
                case 9: return "Store reusable terrain and texture files locally, then add copies to this world mod for XML settings to reference.";
                default: return "Review the blank world's identity and presentation settings, or apply an original world preset above.";
            }
        }

        private void LoadMapPreview()
        {
            if (_mapPreview != null) Destroy(_mapPreview);
            _mapPreview = null;
            foreach (var texture in _mapLayerPreviews.Values) if (texture != null) Destroy(texture);
            _mapLayerPreviews.Clear();
            _mapLayerPixels.Clear();
            _mapSourcePng.Clear();
            _editedMapLayers.Clear();
            _createdMapLayers.Clear();
            _poiPositions.Clear();
            _selectedMapLayers.Clear();
            _paintLayerId = null;
            _paintRegionId = null;
            _paintMode = false;
            _eraseMapMode = false;
            _brushRadius = 7f;
            _mapUndoPng = null;
            _mapUndoLayerId = null;
            var relative = _project?.MiniMapAssetPath;
            if (string.IsNullOrWhiteSpace(relative)) return;
            var path = Path.Combine(Application.dataPath, "StreamingAssets", relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) return;
            var minimapTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!minimapTexture.LoadImage(File.ReadAllBytes(path))) { Destroy(minimapTexture); return; }
                minimapTexture.name = "World Creator minimap";
                _mapPreview = minimapTexture;
            }
            catch (Exception e)
            {
                Destroy(minimapTexture);
                Debug.LogWarning("[World Creator] Could not load minimap preview: " + e.Message);
            }
        }

        private void DrawMapPreview()
        {
            GUILayout.BeginVertical(_rowStyle!);
            GUILayout.Label("WORLD OVERVIEW", _sectionStyle!);
            GUILayout.Label("World-size setting: " + _project!.TerrainWorldSize + "    ·    Points of interest: " + _project.PointOfInterestCount, _mutedStyle!);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            var baseMapImage = _mapReferencePreview ?? _mapPreview;
            if (baseMapImage == null) GUILayout.Label("Blank geographic canvas. Add a PNG/JPG from the Asset library if you want a visual guide.", _mutedStyle!);
            {
                var rect = GUILayoutUtility.GetRect(100, 300, GUILayout.ExpandWidth(true));
                GUI.Box(rect, "");
                var aspect = baseMapImage == null ? 1f : (float)baseMapImage.width / Math.Max(1, baseMapImage.height);
                var width = Math.Min(rect.width - 16, (rect.height - 16) * aspect);
                var height = width / aspect;
                var mapRect = new Rect(rect.center.x - width / 2, rect.center.y - height / 2, width, height);
                if (baseMapImage != null) GUI.DrawTexture(mapRect, baseMapImage, ScaleMode.ScaleToFit);
                else
                {
                    var previousColor = GUI.color;
                    GUI.color = new Color(0.13f, 0.16f, 0.18f, 1f);
                    GUI.DrawTexture(mapRect, Texture2D.whiteTexture);
                    GUI.color = previousColor;
                }
                foreach (var layer in _project.MapLayers.Where(layer => _selectedMapLayers.Contains(layer.Id)))
                    if (_mapLayerPreviews.TryGetValue(layer.Id, out var overlay) && overlay != null)
                    {
                        var previous = GUI.color;
                        GUI.color = new Color(1, 1, 1, 0.48f);
                        GUI.DrawTexture(mapRect, overlay, ScaleMode.StretchToFill);
                        GUI.color = previous;
                    }
                foreach (var poi in _project.PointsOfInterest)
                {
                    var poiLayer = _project.MapLayers.FirstOrDefault(layer => layer.Regions.Any(region => region.Id == poi.RegionId));
                    if (poiLayer == null || !_selectedMapLayers.Contains(poiLayer.Id) || !_poiPositions.TryGetValue(poi.RegionId, out var position)) continue;
                    var point = new Vector2(mapRect.x + position.x * mapRect.width, mapRect.y + position.y * mapRect.height);
                    var marker = new Rect(point.x - 5, point.y - 5, 10, 10);
                    var oldColor = GUI.color;
                    GUI.color = new Color(1f, 0.78f, 0.25f, 1f);
                    GUI.DrawTexture(marker, Texture2D.whiteTexture);
                    GUI.color = oldColor;
                    GUI.Label(new Rect(point.x + 7, point.y - 9, 150, 18), ShortLabel(poi.Id), _mutedStyle!);
                }
                DrawMapPaint(mapRect);
            }
            GUILayout.Label(_mapReferencePreview == null
                ? "Painted masks are included as world-specific PNG assets when you create the world mod; built-in template files stay unchanged."
                : "The library image is a visual guide only. Painted masks are included as world-specific PNG assets in the created mod.", _mutedStyle!);
            GUILayout.EndVertical();
            GUILayout.Space(CardSpacing);
            GUILayout.BeginVertical(_cardStyle!, GUILayout.Width(270));
            GUILayout.Label("MAP LAYERS", _sectionStyle!);
            if (_project.CanAddGeographicLayer && NativeButton("+ Create geographic layer", _buttonStyle!, GUILayout.Height(28))) CreateGeographicLayer();
            _mapLayerScroll = GUILayout.BeginScrollView(_mapLayerScroll, GUILayout.Height(210));
            foreach (var layer in _project.MapLayers)
            {
                var selected = _selectedMapLayers.Contains(layer.Id);
                var next = GUILayout.Toggle(selected, layer.Id);
                if (next != selected)
                {
                    if (next) { _selectedMapLayers.Add(layer.Id); LoadMapLayerPreview(layer); if (!_createdMapLayers.Contains(layer.Id)) _eraseMapMode = false; }
                    else
                    {
                        _selectedMapLayers.Remove(layer.Id);
                        if (_paintLayerId == layer.Id) { _paintLayerId = null; _paintRegionId = null; _eraseMapMode = false; }
                    }
                }
            }
            GUILayout.EndScrollView();
            var referenceAssets = GetMapImageAssets();
            GUILayout.Label("MAP REFERENCE IMAGE", _sectionStyle!);
            if (referenceAssets.Length == 0)
                GUILayout.Label("Import a PNG, JPG or JPEG into the Asset library to use it as a visual guide.", _mutedStyle!);
            else
            {
                _mapReferenceSelection = Mathf.Clamp(_mapReferenceSelection, 0, referenceAssets.Length - 1);
                GUILayout.BeginHorizontal();
                if (NativeButton("‹", _buttonStyle!, GUILayout.Width(32))) _mapReferenceSelection = (_mapReferenceSelection + referenceAssets.Length - 1) % referenceAssets.Length;
                GUILayout.Label(referenceAssets[_mapReferenceSelection].Name, _mutedStyle!, GUILayout.ExpandWidth(true));
                if (NativeButton("›", _buttonStyle!, GUILayout.Width(32))) _mapReferenceSelection = (_mapReferenceSelection + 1) % referenceAssets.Length;
                GUILayout.EndHorizontal();
                if (NativeButton("Use as visual guide", _buttonStyle!, GUILayout.Height(28))) SetMapReferenceImage(referenceAssets[_mapReferenceSelection]);
                if (_mapReferencePreview != null && NativeButton("Clear visual guide", _buttonStyle!, GUILayout.Height(26)))
                {
                    Destroy(_mapReferencePreview);
                    _mapReferencePreview = null;
                    _status = "Cleared the editor-only map reference image.";
                }
                GUILayout.Label("This image is only an editor guide; it is not included in the world mod or used as a game region mask.", _mutedStyle!);
            }
            foreach (var layer in _project.MapLayers.Where(layer => _selectedMapLayers.Contains(layer.Id)))
            {
                GUILayout.Label(layer.Id + " regions", _mutedStyle!);
                _mapRegionScroll = GUILayout.BeginScrollView(_mapRegionScroll, GUILayout.Height(Math.Min(160, Math.Max(50, layer.Regions.Count * 20))));
                foreach (var region in layer.Regions)
                {
                    GUILayout.BeginHorizontal();
                    var swatch = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14), GUILayout.Height(14));
                    var oldColor = GUI.color;
                    GUI.color = new Color(region.R / 255f, region.G / 255f, region.B / 255f, 1);
                    GUI.DrawTexture(swatch, Texture2D.whiteTexture);
                    GUI.color = oldColor;
                    var regionName = _project.GetMapRegionName(region);
                    if (region.NameKey != null) _project.SetMapRegionName(region, GUILayout.TextField(regionName, _inputStyle!, GUILayout.MinWidth(80)));
                    var selectedRegion = _paintLayerId == layer.Id && _paintRegionId == region.Id;
                    if (NativeButton((selectedRegion ? "Selected  " : "Select  ") + region.Id, _buttonStyle!, GUILayout.Height(24)))
                    {
                        _paintLayerId = layer.Id;
                        _paintRegionId = region.Id;
                        if (!_createdMapLayers.Contains(layer.Id)) _eraseMapMode = false;
                        if (_selectedMapLayers.Add(layer.Id)) LoadMapLayerPreview(layer);
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
                if (_project.IsGeographicLayer(layer.Id) && NativeButton("+ Add named region", _buttonStyle!, GUILayout.Height(26)))
                {
                    try
                    {
                        var regionId = _project.AddGeographicRegion(layer.Id);
                        _paintLayerId = layer.Id;
                        _paintRegionId = regionId;
                        _status = "Added " + regionId + ". Rename it, then paint it onto the map.";
                    }
                    catch (Exception e) { _status = "Could not add region: " + e.Message; }
                }
            }
            var pngAssets = GetMapMaskAssets();
            GUILayout.Label("STARTING MASK", _sectionStyle!);
            if (!string.IsNullOrEmpty(_assetLibraryError)) GUILayout.Label(_assetLibraryError, _mutedStyle!);
            if (pngAssets.Length == 0)
                GUILayout.Label("Import a PNG into the Asset library to use it as a map mask.", _mutedStyle!);
            else
            {
                _mapMaskAssetSelection = Mathf.Clamp(_mapMaskAssetSelection, 0, pngAssets.Length - 1);
                GUILayout.BeginHorizontal();
                if (NativeButton("‹", _buttonStyle!, GUILayout.Width(32))) _mapMaskAssetSelection = (_mapMaskAssetSelection + pngAssets.Length - 1) % pngAssets.Length;
                GUILayout.Label(pngAssets[_mapMaskAssetSelection].Name, _mutedStyle!, GUILayout.ExpandWidth(true));
                if (NativeButton("›", _buttonStyle!, GUILayout.Width(32))) _mapMaskAssetSelection = (_mapMaskAssetSelection + 1) % pngAssets.Length;
                GUILayout.EndHorizontal();
                var guiWasEnabled = GUI.enabled;
                GUI.enabled = guiWasEnabled && _paintLayerId != null && _selectedMapLayers.Contains(_paintLayerId) && _project.MapLayers.Any(layer => layer.Id == _paintLayerId);
                if (NativeButton("Use PNG as selected layer mask", _buttonStyle!, GUILayout.Height(28)))
                    ImportMapMask(pngAssets[_mapMaskAssetSelection]);
                GUI.enabled = guiWasEnabled;
                GUILayout.Label("Select a layer and region above. Visible pixels must use its exact region colors; transparent pixels are allowed.", _mutedStyle!);
            }
            var restoreWasEnabled = GUI.enabled;
            GUI.enabled = restoreWasEnabled && _paintLayerId != null && _selectedMapLayers.Contains(_paintLayerId) && _mapSourcePng.ContainsKey(_paintLayerId);
            if (NativeButton("Restore source mask", _buttonStyle!, GUILayout.Height(28))) RestoreMapMask();
            GUI.enabled = restoreWasEnabled;
            GUILayout.Label("Restores the original game mask, or the blank base of a newly created geographic layer.", _mutedStyle!);
            var exportWasEnabled = GUI.enabled;
            GUI.enabled = exportWasEnabled && _paintLayerId != null && _selectedMapLayers.Contains(_paintLayerId);
            if (NativeButton("Save selected mask as PNG", _buttonStyle!, GUILayout.Height(28))) ExportMapMaskPng();
            GUI.enabled = exportWasEnabled;
            GUILayout.Label("PNG copies are saved under Documents/My Games/Stationeers/WorldCreator/MapExports.", _mutedStyle!);
            _paintMode = GUILayout.Toggle(_paintMode, "Paint selected region on map");
            if (_paintMode)
            {
                GUILayout.Label("Brush radius: " + Mathf.RoundToInt(_brushRadius) + " preview pixels", _mutedStyle!);
                _brushRadius = GUILayout.HorizontalSlider(_brushRadius, 2f, 24f);
                var eraseLayer = _paintLayerId == null ? null : _project.MapLayers.FirstOrDefault(layer => layer.Id == _paintLayerId);
                if (eraseLayer != null && _createdMapLayers.Contains(eraseLayer.Id) && eraseLayer.Regions.Count > 0)
                    _eraseMapMode = GUILayout.Toggle(_eraseMapMode, "Erase to base region (" + _project.GetMapRegionName(eraseLayer.Regions[0]) + ")");
                GUILayout.Label(_paintLayerId == null || (_paintRegionId == null && !_eraseMapMode)
                    ? "Select a layer and region first."
                    : _eraseMapMode && _paintLayerId != null && _createdMapLayers.Contains(_paintLayerId)
                        ? "Left-drag on the map to restore pixels to the base region color."
                        : "Left-drag on the map to paint " + _paintRegionId + ". This changes the mask used in the created world mod.", _mutedStyle!);
            }
            if (_mapUndoPng != null && NativeButton("Undo last map change", _buttonStyle!, GUILayout.Height(28))) UndoMapStroke();
            GUILayout.Label("POINTS OF INTEREST", _sectionStyle!);
            foreach (var poi in _project.PointsOfInterest)
                GUILayout.Label(poi.Id + "  ·  " + poi.RegionId, _mutedStyle!);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void CreateGeographicLayer()
        {
            Texture2D? texture = null;
            try
            {
                texture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                texture.name = "World Creator blank geographic map";
                var pixels = new Color32[512 * 512];
                for (var i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 255, 0, 255);
                texture.SetPixels32(pixels);
                texture.Apply();
                var initialPng = texture.EncodeToPNG();
                var id = _project!.AddGeographicLayer(initialPng);
                _mapLayerPreviews[id] = texture;
                _mapLayerPixels[id] = pixels;
                _mapSourcePng[id] = initialPng;
                _createdMapLayers.Add(id);
                _selectedMapLayers.Add(id);
                _editedMapLayers.Add(id);
                _paintLayerId = id;
                _paintRegionId = _project.MapLayers.Last(layer => layer.Id == id).Regions[0].Id;
                _mapUndoPng = null;
                CachePoiPositions(_project.MapLayers.Last(layer => layer.Id == id), texture);
                _status = "Created an RGB24 geographic region layer. Add named regions and paint them before creating the world mod.";
                texture = null;
            }
            catch (Exception e)
            {
                _status = "Could not create geographic layer: " + e.Message;
                Debug.LogWarning("[World Creator] Could not create geographic layer: " + e);
            }
            finally { if (texture != null) Destroy(texture); }
        }

        private void ExportMapMaskPng()
        {
            var layer = _project!.MapLayers.FirstOrDefault(item => item.Id == _paintLayerId && _selectedMapLayers.Contains(item.Id));
            if (layer == null) { _status = "Select a map layer before saving its PNG."; return; }
            try
            {
                LoadMapLayerPreview(layer);
                if (!_mapLayerPreviews.TryGetValue(layer.Id, out var texture) || texture == null)
                    throw new FileNotFoundException("The selected map layer image could not be loaded.");
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Stationeers", "WorldCreator", "MapExports");
                Directory.CreateDirectory(folder);
                var stem = Regex.Replace((_id ?? "World") + "_" + layer.Id, "[^A-Za-z0-9_-]", "_");
                var path = Path.Combine(folder, stem + ".png");
                for (var suffix = 2; File.Exists(path); suffix++) path = Path.Combine(folder, stem + "_" + suffix.ToString(CultureInfo.InvariantCulture) + ".png");
                File.WriteAllBytes(path, texture.EncodeToPNG());
                _status = "Saved the selected map mask to " + path;
            }
            catch (Exception e)
            {
                _status = "Could not save map mask: " + e.Message;
                Debug.LogWarning("[World Creator] Could not export map mask PNG: " + e);
            }
        }

        private void EnsureAssetLibraryLoaded()
        {
            if (!_assetLibraryLoaded)
            {
                try
                {
                    _assetLibraryItems.Clear();
                    _assetLibraryItems.AddRange(AssetLibrary.Load());
                    _assetLibraryError = null;
                }
                catch (Exception e) { _assetLibraryError = e.Message; }
                _assetLibraryLoaded = true;
            }
        }

        private LibraryAsset[] GetMapMaskAssets()
        {
            EnsureAssetLibraryLoaded();
            return _assetLibraryItems.Where(asset => string.Equals(Path.GetExtension(asset.FileName), ".png", StringComparison.OrdinalIgnoreCase))
                .OrderBy(asset => asset.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private LibraryAsset[] GetMapImageAssets()
        {
            EnsureAssetLibraryLoaded();
            return _assetLibraryItems.Where(asset =>
                string.Equals(Path.GetExtension(asset.FileName), ".png", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(asset.FileName), ".jpg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetExtension(asset.FileName), ".jpeg", StringComparison.OrdinalIgnoreCase))
                .OrderBy(asset => asset.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private void SetMapReferenceImage(LibraryAsset asset)
        {
            var preview = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!preview.LoadImage(File.ReadAllBytes(asset.FullPath))) throw new InvalidDataException("The library image could not be decoded.");
                preview.name = "World Creator map reference " + asset.Name;
                if (_mapReferencePreview != null) Destroy(_mapReferencePreview);
                _mapReferencePreview = preview;
                _status = "Using " + asset.Name + " as an editor-only map guide.";
            }
            catch (Exception e)
            {
                Destroy(preview);
                _status = "Could not load map reference image: " + e.Message;
            }
        }

        private void ImportMapMask(LibraryAsset asset)
        {
            var layer = _project!.MapLayers.FirstOrDefault(item => item.Id == _paintLayerId);
            if (layer == null) { _status = "Select a map layer before applying a mask."; return; }
            var replacement = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!replacement.LoadImage(File.ReadAllBytes(asset.FullPath))) throw new InvalidDataException("The library image could not be decoded as a PNG.");
                var pixels = replacement.GetPixels32();
                var regionColors = new HashSet<uint>(layer.Regions.Select(region => ColorKey(region.R, region.G, region.B)));
                var invalidPixels = pixels.Count(pixel => pixel.a != 0 && !regionColors.Contains(ColorKey(pixel.r, pixel.g, pixel.b)));
                if (invalidPixels > 0)
                    throw new InvalidDataException(invalidPixels + " visible pixels do not match this layer's exact region colors. Use a non-antialiased mask with the defined palette.");

                replacement.name = "World Creator layer " + layer.Id + " (library mask)";
                CaptureMapUndo(layer.Id);
                if (_mapLayerPreviews.TryGetValue(layer.Id, out var previous) && previous != null) Destroy(previous);
                _mapLayerPreviews[layer.Id] = replacement;
                _mapLayerPixels[layer.Id] = pixels;
                _selectedMapLayers.Add(layer.Id);
                _editedMapLayers.Add(layer.Id);
                CachePoiPositions(layer, replacement);
                _status = "Applied " + asset.Name + " to " + layer.Id + ". The original library PNG is unchanged.";
            }
            catch (Exception e)
            {
                Destroy(replacement);
                _status = "Could not use map mask: " + e.Message;
            }
        }

        private void LoadMapLayerPreview(WorldProject.MapLayer layer)
        {
            if (_mapLayerPreviews.ContainsKey(layer.Id)) return;
            var path = Path.Combine(Application.dataPath, "StreamingAssets", layer.TexturePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) return;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                var imageBytes = File.ReadAllBytes(path);
                if (!texture.LoadImage(imageBytes)) { Destroy(texture); return; }
                texture.name = "World Creator layer " + layer.Id;
                _mapLayerPreviews.Add(layer.Id, texture);
                _mapSourcePng[layer.Id] = imageBytes;
                _mapLayerPixels[layer.Id] = texture.GetPixels32();
                CachePoiPositions(layer, texture);
            }
            catch (Exception e)
            {
                Destroy(texture);
                Debug.LogWarning("[World Creator] Could not load map layer " + layer.Id + ": " + e.Message);
            }
        }

        private void CachePoiPositions(WorldProject.MapLayer layer, Texture2D texture)
        {
            foreach (var region in layer.Regions) _poiPositions.Remove(region.Id);
            var regionIndexes = new Dictionary<uint, int>();
            for (var i = 0; i < layer.Regions.Count; i++)
            {
                var region = layer.Regions[i];
                regionIndexes[ColorKey(region.R, region.G, region.B)] = i;
            }
            if (!_mapLayerPixels.TryGetValue(layer.Id, out var pixels))
            {
                pixels = texture.GetPixels32();
                _mapLayerPixels[layer.Id] = pixels;
            }
            var counts = new int[layer.Regions.Count];
            var xTotals = new long[layer.Regions.Count];
            var yTotals = new long[layer.Regions.Count];
            for (var i = 0; i < pixels.Length; i++)
            {
                var pixel = pixels[i];
                if (!regionIndexes.TryGetValue(ColorKey(pixel.r, pixel.g, pixel.b), out var regionIndex)) continue;
                xTotals[regionIndex] += i % texture.width;
                yTotals[regionIndex] += i / texture.width;
                counts[regionIndex]++;
            }
            for (var i = 0; i < layer.Regions.Count; i++)
                if (counts[i] > 0)
                    _poiPositions[layer.Regions[i].Id] = new Vector2((float)xTotals[i] / counts[i] / Math.Max(1, texture.width - 1), 1f - (float)yTotals[i] / counts[i] / Math.Max(1, texture.height - 1));
        }

        private static uint ColorKey(int r, int g, int b) => ((uint)r << 16) | ((uint)g << 8) | (uint)b;

        private void DrawMapPaint(Rect mapRect)
        {
            if (!_paintMode || _paintLayerId == null || (_paintRegionId == null && !_eraseMapMode)) return;
            var layer = _project!.MapLayers.FirstOrDefault(item => item.Id == _paintLayerId);
            var region = layer == null ? null : _eraseMapMode && _createdMapLayers.Contains(layer.Id)
                ? layer.Regions.FirstOrDefault()
                : layer.Regions.FirstOrDefault(item => item.Id == _paintRegionId);
            if (layer == null || region == null || !_selectedMapLayers.Contains(layer.Id) || !_mapLayerPreviews.ContainsKey(layer.Id)) return;

            var controlId = GUIUtility.GetControlID(FocusType.Passive);
            var current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0 && mapRect.Contains(current.mousePosition))
            {
                CaptureMapUndo(layer.Id);
                GUIUtility.hotControl = controlId;
                PaintMapAt(layer, region, mapRect, current.mousePosition);
                current.Use();
            }
            else if (current.type == EventType.MouseDrag && GUIUtility.hotControl == controlId)
            {
                if (mapRect.Contains(current.mousePosition)) PaintMapAt(layer, region, mapRect, current.mousePosition);
                current.Use();
            }
            else if (current.type == EventType.MouseUp && GUIUtility.hotControl == controlId)
            {
                GUIUtility.hotControl = 0;
                CachePoiPositions(layer, _mapLayerPreviews[layer.Id]);
                current.Use();
            }
        }

        private void PaintMapAt(WorldProject.MapLayer layer, WorldProject.MapLayer.Region region, Rect mapRect, Vector2 mouse)
        {
            if (!_mapLayerPreviews.TryGetValue(layer.Id, out var texture) || !_mapLayerPixels.TryGetValue(layer.Id, out var pixels)) return;
            var centerX = Mathf.Clamp(Mathf.RoundToInt((mouse.x - mapRect.x) / mapRect.width * (texture.width - 1)), 0, texture.width - 1);
            var centerY = Mathf.Clamp(Mathf.RoundToInt((1f - (mouse.y - mapRect.y) / mapRect.height) * (texture.height - 1)), 0, texture.height - 1);
            var radiusX = Mathf.Max(1, Mathf.RoundToInt(_brushRadius * texture.width / mapRect.width));
            var radiusY = Mathf.Max(1, Mathf.RoundToInt(_brushRadius * texture.height / mapRect.height));
            var minX = Mathf.Max(0, centerX - radiusX);
            var maxX = Mathf.Min(texture.width - 1, centerX + radiusX);
            var minY = Mathf.Max(0, centerY - radiusY);
            var maxY = Mathf.Min(texture.height - 1, centerY + radiusY);
            var blockWidth = maxX - minX + 1;
            var blockHeight = maxY - minY + 1;
            var block = new Color32[blockWidth * blockHeight];
            var color = new Color32((byte)region.R, (byte)region.G, (byte)region.B, 255);
            var normalizedRadiusX = Math.Max(1f, radiusX);
            var normalizedRadiusY = Math.Max(1f, radiusY);
            for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX; x++)
            {
                var dx = (x - centerX) / normalizedRadiusX;
                var dy = (y - centerY) / normalizedRadiusY;
                var index = (y - minY) * blockWidth + x - minX;
                var pixelIndex = y * texture.width + x;
                block[index] = dx * dx + dy * dy <= 1f ? color : pixels[pixelIndex];
            }
            texture.SetPixels32(minX, minY, blockWidth, blockHeight, block);
            texture.Apply(false, false);
            for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX; x++)
                pixels[y * texture.width + x] = block[(y - minY) * blockWidth + x - minX];
            _editedMapLayers.Add(layer.Id);
        }

        private void UndoMapStroke()
        {
            if (_mapUndoPng == null || _mapUndoLayerId == null || !_mapLayerPreviews.TryGetValue(_mapUndoLayerId, out var texture)) return;
            if (!texture.LoadImage(_mapUndoPng))
            {
                _status = "Could not restore the previous map mask.";
                return;
            }
            _mapLayerPixels[_mapUndoLayerId] = texture.GetPixels32();
            if (_mapUndoLayerHadEdits) _editedMapLayers.Add(_mapUndoLayerId);
            else _editedMapLayers.Remove(_mapUndoLayerId);
            var layer = _project!.MapLayers.First(item => item.Id == _mapUndoLayerId);
            CachePoiPositions(layer, texture);
            _mapUndoPng = null;
            _mapUndoLayerId = null;
            _status = "Restored the map mask before the last change.";
        }

        private void CaptureMapUndo(string layerId)
        {
            if (!_mapLayerPreviews.TryGetValue(layerId, out var texture) || texture == null) return;
            _mapUndoPng = texture.EncodeToPNG();
            _mapUndoLayerId = layerId;
            _mapUndoLayerHadEdits = _editedMapLayers.Contains(layerId);
        }

        private void RestoreMapMask()
        {
            if (_paintLayerId == null || !_mapSourcePng.TryGetValue(_paintLayerId, out var sourcePng)) return;
            var layer = _project!.MapLayers.FirstOrDefault(item => item.Id == _paintLayerId);
            if (layer == null) { _status = "Select a map layer before restoring its mask."; return; }
            var replacement = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!replacement.LoadImage(sourcePng)) throw new InvalidDataException("The source map mask could not be decoded as a PNG.");
                CaptureMapUndo(layer.Id);
                replacement.name = "World Creator layer " + layer.Id + " (source mask)";
                if (_mapLayerPreviews.TryGetValue(layer.Id, out var previous) && previous != null) Destroy(previous);
                _mapLayerPreviews[layer.Id] = replacement;
                _mapLayerPixels[layer.Id] = replacement.GetPixels32();
                if (_createdMapLayers.Contains(layer.Id))
                {
                    _project.SetMapLayerTexture(layer.Id, sourcePng);
                    _editedMapLayers.Add(layer.Id);
                }
                else
                {
                    _project.ClearMapLayerTexture(layer.Id);
                    _editedMapLayers.Remove(layer.Id);
                }
                CachePoiPositions(layer, replacement);
                _status = "Restored the source mask for " + layer.Id + ". Undo is available.";
                replacement = null!;
            }
            catch (Exception e)
            {
                _status = "Could not restore source mask: " + e.Message;
                Debug.LogWarning("[World Creator] Could not restore source map mask: " + e);
            }
            finally { if (replacement != null) Destroy(replacement); }
        }

        private void DrawResourceControls()
        {
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            GUILayout.Label("REGIONAL RESOURCES", _sectionStyle!);
            GUILayout.Label("Edit ore abundance for each listed region. Other resource profiles are shared game data and are preserved as-is.", _mutedStyle!);
            GUILayout.EndVertical();
            GUILayout.Space(CardSpacing);
        }

        private void DrawTerrainInfo()
        {
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            GUILayout.Label("TERRAIN SOURCE", _sectionStyle!);
            GUILayout.Label("Terrain settings copied from the selected world:");
            if (_project!.TerrainSummary.Count == 0)
            {
                GUILayout.Label("No TerrainSettings entry is defined.");
                GUILayout.EndVertical();
                return;
            }
            foreach (var item in _project.TerrainSummary) GUILayout.Label(item);
            GUILayout.Space(8);
            GUILayout.Label(_project.TerrainDescription);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Source type", GUILayout.Width(100));
            _terrainKind = GUILayout.TextField(_terrainKind, GUILayout.Width(110));
            GUILayout.Label("File path");
            _terrainPath = GUILayout.TextField(_terrainPath);
            GUILayout.EndHorizontal();
            GUILayout.Label("Type: Heightmap (.raw/.data), Crust (.png), Cracks (.png), or Lava (.png). Heightmap must match this template's terrain size.");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Compiled terrain folder", GUILayout.Width(150));
            _compiledTerrainPath = GUILayout.TextField(_compiledTerrainPath);
            if (NativeButton("Use compiled terrain", GUILayout.Width(160)))
            {
                try
                {
                    _project.SetCompiledTerrainFolder(_compiledTerrainPath);
                    _status = "Validated " + _project.CompiledTerrainFiles.Count + " indexed terrain files for world-specific packaging.";
                }
                catch (Exception e) { _status = "Compiled terrain rejected: " + e.Message; }
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Choose the folder saved by Terrain Tools. The export copies indexed Terrain<number>.dat files into this mod.");
            GUILayout.BeginHorizontal();
            if (NativeButton("Validate and add source"))
            {
                try
                {
                    var kind = new[] { "heightmap", "crust", "cracks", "lava" }.FirstOrDefault(k => string.Equals(k, _terrainKind, StringComparison.OrdinalIgnoreCase));
                    if (kind == null) throw new InvalidOperationException("Use Heightmap, Crust, Cracks, or Lava as source type.");
                    var name = char.ToUpperInvariant(kind[0]) + kind.Substring(1);
                    _project.SetTerrainSource(name, _terrainPath);
                    LoadTerrainPreview(name, _terrainPath);
                    _status = name + " source validated and will be copied into TerrainSource in the created world mod.";
                }
                catch (Exception e) { _status = "Terrain source rejected: " + e.Message; }
            }
            if (NativeButton("Open Terrain Tools", _buttonStyle!)) OpenTerrainTools();
            GUILayout.EndHorizontal();
            GUILayout.Space(CardSpacing);
            GUILayout.Label("Terrain Tools is the game's own terrain utility. World Creator hides while it is open; reopen World Creator from the main menu when you're ready to continue this project.", _mutedStyle!);
            if (_terrainPreview != null) GUILayout.Label(_terrainPreview, GUILayout.Width(240), GUILayout.Height(160));
            foreach (var pair in _project.TerrainSourceFiles)
                GUILayout.Label(pair.Key + ": " + Path.GetFileName(pair.Value));
            GUILayout.EndVertical();
        }

        private void OpenTerrainTools()
        {
            var terrainWindow = UI.ImGuiUi.ImGuiTerrainUtilityWindow.Window;
            EditorWindow.Hide();
            if (!terrainWindow.IsShowing)
                UI.ImGuiUi.ImGuiWindows.ImGuiWindowManager.Open(terrainWindow);
            _status = "Opened the game's Terrain Tools. Reopen World Creator from the main menu to continue this project.";
        }

        private void LoadTerrainPreview(string kind, string path)
        {
            if (_terrainPreview != null) Destroy(_terrainPreview);
            _terrainPreview = null;
            if (kind == "Heightmap") return;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
            {
                Destroy(texture);
                throw new InvalidOperationException("Could not decode PNG image.");
            }
            texture.name = "World Creator " + kind + " preview";
            _terrainPreview = texture;
        }

        private void Export()
        {
            try
            {
                var mods = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Stationeers", "mods");
                foreach (var layerId in _editedMapLayers)
                    _project!.SetMapLayerTexture(layerId, _mapLayerPreviews[layerId].EncodeToPNG());
                _status = "Created " + _project!.Export(mods, _id, _name) + ". Enable the mod and restart the game.";
            }
            catch (Exception e)
            {
                _status = "Could not create world: " + e.Message;
                Debug.LogError("[World Creator] Export failed: " + e);
            }
        }
    }
}
