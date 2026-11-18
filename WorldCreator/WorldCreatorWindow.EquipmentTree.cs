using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private readonly HashSet<XElement> _expandedEquipment = new HashSet<XElement>();
    private readonly HashSet<XElement> _collapsedEquipmentRoots = new HashSet<XElement>();
    private IReadOnlyList<WorldProject.EquipmentViewNode> _equipmentView = new WorldProject.EquipmentViewNode[0];
    private readonly Dictionary<XElement, WorldProject.EquipmentViewNode> _equipmentViewByElement = new Dictionary<XElement, WorldProject.EquipmentViewNode>();
    private bool _showEquipmentRespawns;

    /// <summary>Builds the presentation once per draw pass instead of rebuilding it separately for every slot label.</summary>
    private void RefreshEquipmentView(string conditionId)
    {
        _equipmentView = _project!.GetEquipmentView(conditionId);
        _equipmentViewByElement.Clear();
        foreach (var node in WorldProject.FlattenEquipmentView(_equipmentView)) _equipmentViewByElement[node.Element] = node;
    }

    /// <summary>Shows a collapsible hierarchy of player kits and physical objects, with technical references hidden.</summary>
    private void DrawEquipmentTree(string conditionId, bool editable)
    {
        _equipmentScrolls.TryGetValue(conditionId, out var scroll);
        _equipmentScrolls[conditionId] = GUILayout.BeginScrollView(scroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar,
            GUILayout.Height(Mathf.Clamp(_window.height - 420f, 360f, 680f)));
        foreach (var root in _equipmentView.Where(node => node.Event != "RespawnPlayerKit")) DrawEquipmentTreeNode(conditionId, root, 0);
        var respawns = _equipmentView.Where(node => node.Event == "RespawnPlayerKit").ToArray();
        if (respawns.Length > 0)
        {
            GUILayout.Space(12);
            if (NativeButton((_showEquipmentRespawns ? "−" : "+") + " Respawn equipment (" + respawns.Length + ")", _buttonStyle!, GUILayout.Height(32)))
                _pendingEquipmentChange = () => _showEquipmentRespawns = !_showEquipmentRespawns;
            if (_showEquipmentRespawns) foreach (var root in respawns) DrawEquipmentTreeNode(conditionId, root, 0);
        }
        if (_equipmentView.Count == 0) GUILayout.Label("No starting equipment. Add a lander or an installed starting profile.", _mutedStyle!);
        GUILayout.EndScrollView();
    }

    /// <summary>Draws one selectable object and only expands its descendants when requested.</summary>
    private void DrawEquipmentTreeNode(string conditionId, WorldProject.EquipmentViewNode node, int depth)
    {
        var expanded = node.Parent == null && !node.IsPlayerKit ? !_collapsedEquipmentRoots.Contains(node.Element) : _expandedEquipment.Contains(node.Element);
        GUILayout.BeginHorizontal();
        GUILayout.Space(Mathf.Min(depth, 16) * 16);
        if (node.Children.Count > 0)
        {
            if (NativeButton(expanded ? "−" : "+", _buttonStyle!, GUILayout.Width(30), GUILayout.Height(30)))
                _pendingEquipmentChange = () => ToggleEquipmentBranch(node);
        }
        else GUILayout.Space(34);
        var selected = _equipmentProfile == conditionId && _equipmentContainer == node.Element;
        var name = EquipmentViewName(node);
        var quantity = (string?)node.Element.Element("Quantity")?.Attribute("Value") ?? "1";
        if (node.Element.Name != "Spawn" && node.Children.Count == 0 && !EquipmentHasAtmosphere(node.Element)) name += " × " + quantity;
        var marked = _exportIssueItemId != null && (string?)node.Element.Attribute("Id") == _exportIssueItemId;
        if (marked) DrawExportName(new GUIContent(name), true, _sectionStyle!);
        else if (NativeButton(name, selected ? _primaryButtonStyle! : _buttonStyle!, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true), GUILayout.Height(30)))
            _pendingEquipmentChange = () => SelectEquipmentContainer(conditionId, node.Element);
        if (selected && Event.current.type == EventType.Repaint)
            GUI.DrawTexture(GUILayoutUtility.GetLastRect(), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, Accent, 2, 5);
        GUILayout.EndHorizontal();
        GUILayout.Space(5);
        if (expanded) foreach (var child in node.Children) DrawEquipmentTreeNode(conditionId, child, depth + 1);
    }

    /// <summary>Changes expansion separately from selection so browsing never edits equipment.</summary>
    private void ToggleEquipmentBranch(WorldProject.EquipmentViewNode node)
    {
        var set = node.Parent == null && !node.IsPlayerKit ? _collapsedEquipmentRoots : _expandedEquipment;
        if (!set.Remove(node.Element)) set.Add(node.Element);
    }

    /// <summary>Uses native prefab names only when the visible node has no custom or supply-kit label.</summary>
    private string EquipmentViewName(WorldProject.EquipmentViewNode node)
    {
        var id = (string?)node.Element.Attribute("Id") ?? "";
        return node.Name == id && node.Element.Name != "Spawn" ? EquipmentPrefabRules.Find(id)?.DisplayName ?? node.Name : HumanizeXmlName(node.Name);
    }

    /// <summary>Renders clickable ancestors and the current location, wrapping long paths within the editor card.</summary>
    private void DrawEquipmentBreadcrumbs(string conditionId, WorldProject.EquipmentViewNode selected)
    {
        var path = new Stack<WorldProject.EquipmentViewNode>();
        for (var node = selected; node != null; node = node.Parent) path.Push(node);
        GUILayout.Space(12);
        GUILayout.Label("Location", _mutedStyle!);
        var available = Mathf.Max(500, (PageContentWidth - CardSpacing) * 0.5f - 50);
        var used = 0f;
        GUILayout.BeginHorizontal();
        foreach (var node in path)
        {
            var label = EquipmentViewName(node);
            var width = Mathf.Min(available, Mathf.Max(75, _buttonStyle!.CalcSize(new GUIContent(label)).x + 18));
            if (used + width + 20 > available && used > 0) { GUILayout.FlexibleSpace(); GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); used = 0; }
            if (used > 0) { GUILayout.Label("›", _mutedStyle!, GUILayout.Width(16)); used += 20; }
            if (NativeButton(label, node.Element == selected.Element ? _primaryButtonStyle! : _buttonStyle!, GUILayout.Width(width), GUILayout.Height(30)))
                _pendingEquipmentChange = () => SelectEquipmentContainer(conditionId, node.Element);
            if (node.Element == selected.Element && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(GUILayoutUtility.GetLastRect(), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, Accent, 2, 5);
            used += width + 4;
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(12);
    }
}
