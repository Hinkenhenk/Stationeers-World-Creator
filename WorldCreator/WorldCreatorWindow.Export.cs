using System;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private string? _exportIssue;
    private WorldProject.Field? _exportIssueField;
    private string? _exportIssueValue;
    private int _exportIssuePage;
    private bool _exportIssueIsWarning;
    private string? _exportIssueName;
    private string? _exportIssueItemId;

    /// <summary>Builds a new mod or a confirmed local update and exposes failures with a route to their settings.</summary>
    private void Export(bool updateExisting = false)
    {
        ClearExportIssue();
        try
        {
            if (_mapRgbDrafts.Count > 0)
            {
                _status = "Map studio → RGB: use three comma-separated integers from 0 to 255 before exporting.";
                SetExportIssue(_status, null, 7);
                return;
            }
            var mods = ModSearchPaths.LocalMods(Assets.Scripts.Serialization.Settings.CurrentData?.SavePath);
            foreach (var layerId in _editedMapLayers)
                _project!.SetMapLayerTexture(layerId, _mapLayerPreviews[layerId].EncodeToPNG());
            var target = _project!.Export(mods, _id, _name, updateExisting);
            _status = (updateExisting ? "Updated " : "Created ") + target + ". Restart Stationeers to use the exported settings.";
            if (_project.LastExportBackup != null) _status += " Backup: " + _project.LastExportBackup;
            if (_project.ExportWarnings.Count > 0)
                SetExportIssue(string.Join("\n", _project.ExportWarnings), _project.AdvancedFields.FirstOrDefault(field => field.Label.EndsWith("/PreviewButton/@Path", StringComparison.Ordinal)), 6, warning: true);
        }
        catch (Exception e)
        {
            _status = "Could not " + (updateExisting ? "update" : "create") + " world: " + e.Message;
            var fields = _project!.AdvancedFields.Concat(_project.WorldFields).Concat(_project.AtmosphereFields).Concat(_project.WeatherFields).Concat(_project.OrbitFields).Concat(_project.ResourceFields);
            var field = e is WorldProject.ExportAssetException assetError
                ? _project.AdvancedFields.FirstOrDefault(item => item.Label == assetError.FieldLabel)
                : fields.OrderByDescending(item => item.Label.Length).FirstOrDefault(item => e.Message.Contains(item.Label));
            var page = FindExportIssuePage(field, e.Message);
            SetExportIssue(_status, field, page);
            if (e is WorldProject.ExportEquipmentException equipmentError)
            {
                _exportIssueName = equipmentError.ItemName;
                _exportIssueItemId = (string?)equipmentError.Item.Attribute("Id");
            }
            if (e is System.IO.IOException || e is InvalidOperationException)
                Debug.LogWarning("[World Creator] Export blocked: " + e.Message);
            else Debug.LogError("[World Creator] Export failed: " + e);
        }
    }

    /// <summary>Requests explicit confirmation before replacing a local mod and explains the retained backup.</summary>
    private void ConfirmUpdateWorld()
    {
        ConfirmEditorAction("Update world mod?", "Update " + _name + " (" + _id + ")?\n\nReplaces this local mod's exported settings and keeps a backup. Saved play sessions are unchanged.", "Update", () => Export(updateExisting: true));
    }

    /// <summary>Locates a validation error in the collection that supplies its page, including imported equipment.</summary>
    private int FindExportIssuePage(WorldProject.Field? field, string message)
    {
        if (field != null)
        {
            if (_project!.AdvancedFields.Contains(field)) return 6;
            if (_project.AtmosphereFields.Contains(field)) return 1;
            if (_project.WeatherFields.Contains(field)) return 2;
            if (_project.OrbitFields.Contains(field)) return 3;
            if (_project.ResourceFields.Contains(field)) return 4;
        }
        if (message.IndexOf("Orbit & seasons", StringComparison.OrdinalIgnoreCase) >= 0) return 3;
        if (message.IndexOf("crate", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("spawn", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("start condition", StringComparison.OrdinalIgnoreCase) >= 0) return 8;
        return 0;
    }

    /// <summary>Records a visible error or fallback warning and the current value of its affected field.</summary>
    private void SetExportIssue(string message, WorldProject.Field? field, int page, bool warning = false)
    {
        _exportIssue = message;
        _exportIssueField = field;
        _exportIssueValue = field?.Value;
        _exportIssuePage = page;
        _exportIssueIsWarning = warning;
        _exportIssueName = field == null ? Pages[page] : ReadableFieldLabel(field.Label, true);
        _scroll = Vector2.zero;
    }

    /// <summary>Removes feedback when starting a new export, loading another world or correcting the marked field.</summary>
    private void ClearExportIssue()
    {
        _exportIssue = null;
        _exportIssueField = null;
        _exportIssueValue = null;
        _exportIssueName = null;
        _exportIssueItemId = null;
    }

    /// <summary>Shows export feedback in context with navigation to the affected page and exact Advanced field when available.</summary>
    private void DrawExportIssue()
    {
        if (_exportIssue == null) return;
        if (_exportIssueField != null && _exportIssueField.Value != _exportIssueValue)
        {
            ClearExportIssue();
            return;
        }
        GUILayout.BeginVertical(_cardStyle!);
        DrawExportName(new GUIContent(_exportIssueIsWarning ? "Export warning" : _exportIssueName), !_exportIssueIsWarning, _sectionStyle!);
        GUILayout.Label(_exportIssue, _mutedStyle!);
        if (NativeButton(_exportIssueField == null ? "Open " + Pages[_exportIssuePage] : "Show affected field", _buttonStyle!, GUILayout.Width(230), GUILayout.Height(32)))
        {
            _pendingPage = _exportIssuePage;
            if (_exportIssuePage == 6 && _exportIssueField != null) _advancedFilter = _exportIssueField.Label;
        }
        if (!_exportIssueIsWarning && _project!.CanReplaceMissingTexture(_exportIssueField)) DrawMissingTextureReplacement();
        GUILayout.EndVertical();
        GUILayout.Space(CardSpacing);
    }

    /// <summary>Renders the failure cross directly because the native game font does not contain the emoji glyph.</summary>
    private Rect DrawExportName(GUIContent name, bool failed, GUIStyle style)
    {
        var rect = GUILayoutUtility.GetRect(name, style, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
        GUI.Label(rect, name, style);
        if (!failed || Event.current.type != EventType.Repaint) return rect;
        var x = Mathf.Min(rect.x + style.CalcSize(name).x + 8, rect.xMax - 18);
        var cross = new Rect(x, rect.y + 2, 16, 16);
        var matrix = GUI.matrix;
        var color = GUI.color;
        GUI.color = new Color(1, 0.2f, 0.15f);
        GUIUtility.RotateAroundPivot(45, cross.center);
        GUI.DrawTexture(new Rect(cross.x + 6, cross.y, 4, 16), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cross.x, cross.y + 6, 16, 4), Texture2D.whiteTexture);
        GUI.matrix = matrix;
        GUI.color = color;
        return rect;
    }
}
