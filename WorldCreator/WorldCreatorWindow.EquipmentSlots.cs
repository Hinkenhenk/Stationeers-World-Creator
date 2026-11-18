using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Assets.Scripts.Objects;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private readonly Dictionary<XElement, string> _equipmentQuantityDrafts = new Dictionary<XElement, string>();
    private string _equipmentNameDraft = "";
    private bool _equipmentItemSearch;

    /// <summary>Inspects the selected visible object, its location, tank contents and actual native slots.</summary>
    private void DrawEquipmentContainers(string conditionId)
    {
        var containers = _equipmentViewByElement.Values.ToArray();
        if (containers.Length == 0)
        {
            GUILayout.Label("No containers yet. Add a supply crate or lander, or inspect the equipment overview.", _mutedStyle!);
            return;
        }
        var index = Array.FindIndex(containers, row => row.Element == _equipmentContainer && _equipmentProfile == conditionId);
        if (index < 0)
        {
            var initial = containers.FirstOrDefault(node => EquipmentParent(node.Element, node.Event) is Lander) ?? containers[0];
            _pendingEquipmentChange ??= () => SelectEquipmentContainer(conditionId, initial.Element);
            return;
        }
        var selected = containers[index];
        DrawEquipmentBreadcrumbs(conditionId, selected);
        GUILayout.Label(EquipmentViewName(selected), _sectionStyle!);
        var enabled = GUI.enabled;
        GUI.enabled = enabled && _project!.CanEditEquipment(conditionId);
        DrawEquipmentContainerActions(conditionId, selected.Element);
        DrawEquipmentAtmosphere(conditionId, selected.Element);
        var parent = EquipmentParent(selected.Element, selected.Event);
        if (parent?.Slots == null || parent.Slots.Count == 0)
        {
            if (parent != null && EquipmentPrefabRules.MaximumQuantity(parent) > 1)
            {
                GUILayout.BeginHorizontal();
                DrawEquipmentQuantity(conditionId, selected.Element, parent);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else if (!EquipmentHasAtmosphere(selected.Element)) GUILayout.Label(parent == null ? "This prefab is unavailable. Existing equipment is preserved; install its source mod to edit its slots." : "This item has no equipment slots.", _mutedStyle!);
            GUI.enabled = enabled;
            return;
        }
        IReadOnlyList<(XElement Item, int Slot)> assignments;
        try
        {
            assignments = EquipmentPrefabRules.Assign(parent, _project!.GetEquipmentContents(selected.Element));
        }
        catch (Exception error)
        {
            GUILayout.Label(error.Message + " Use the equipment overview to inspect or remove unsupported contents.", _mutedStyle!);
            GUI.enabled = enabled;
            return;
        }
        GUILayout.Label(assignments.Count + " occupied / " + parent.Slots.Count + " slots · " + parent.DisplayName, _mutedStyle!);
        for (var slot = 0; slot < parent.Slots.Count; slot++)
        {
            DrawEquipmentSlot(conditionId, selected.Element, parent, slot, assignments);
            if (_equipmentItemSearch && slot == _equipmentSearchSlot)
                DrawEquipmentItemPicker(conditionId, selected.Element);
        }
        GUI.enabled = enabled;
    }

    /// <summary>Renames or removes the selected occurrence while preserving installed definitions.</summary>
    private void DrawEquipmentContainerActions(string conditionId, XElement container)
    {
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        if (container.Name != "Spawn")
        {
            GUILayout.Label("Name", _mutedStyle!, GUILayout.Width(60));
            _equipmentNameDraft = GUILayout.TextField(_equipmentNameDraft, _inputStyle!, GUILayout.MaxWidth(340));
            GUILayout.Space(10);
            if (NativeButton("Rename", _buttonStyle!, GUILayout.Width(110), GUILayout.Height(30)))
                _pendingEquipmentChange = () => _project!.SetEquipmentName(conditionId, container, _equipmentNameDraft);
        }
        GUILayout.FlexibleSpace();
        if (NativeButton("Remove selected", _buttonStyle!, GUILayout.Width(160), GUILayout.Height(30)))
            QueueEquipmentRemoval(conditionId, container);
        GUILayout.EndHorizontal();
        GUILayout.Space(10);
    }

    /// <summary>Shows an occupied, empty or locked slot and routes item search to that exact location.</summary>
    private void DrawEquipmentSlot(string conditionId, XElement container, Thing parent, int index,
        IReadOnlyList<(XElement Item, int Slot)> assignments)
    {
        var slot = parent.Slots[index];
        var item = assignments.FirstOrDefault(entry => entry.Slot == index).Item;
        GUILayout.BeginVertical(_rowStyle!);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Slot " + (index + 1) + (string.IsNullOrWhiteSpace(slot.StringKey) ? "" : " · " + slot.StringKey), _mutedStyle!, GUILayout.Width(160));
        if (item != null)
            DrawOccupiedEquipmentSlot(conditionId, item);
        else
        {
            GUILayout.Label(slot.IsLocked ? "Locked" : "Empty", _mutedStyle!);
            GUILayout.FlexibleSpace();
            if (!slot.IsLocked && NativeButton("Search items", _buttonStyle!, GUILayout.Width(150), GUILayout.Height(32)))
                _pendingEquipmentChange = () =>
                {
                    _equipmentSearchSlot = index;
                    _equipmentSearch = "";
                    _equipmentItemSearch = true;
                };
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        GUILayout.Space(8);
    }

    /// <summary>Edits an existing stack within the prefab limit and opens nested bags or crates.</summary>
    private void DrawOccupiedEquipmentSlot(string conditionId, XElement item)
    {
        GUILayout.Label(EquipmentDisplayName(item), _mutedStyle!, GUILayout.MinWidth(140));
        GUILayout.FlexibleSpace();
        var prefab = EquipmentPrefabRules.Find((string?)item.Attribute("Id") ?? "");
        if (prefab != null && EquipmentPrefabRules.MaximumQuantity(prefab) > 1)
            DrawEquipmentQuantity(conditionId, item, prefab);
        if ((prefab?.Slots?.Count > 0 || EquipmentHasAtmosphere(item)) && NativeButton("Contents", _buttonStyle!, GUILayout.Width(110), GUILayout.Height(32)))
            _pendingEquipmentChange = () => SelectEquipmentContainer(conditionId, item);
        GUILayout.Space(10);
        if (NativeButton("Remove", _buttonStyle!, GUILayout.Width(110), GUILayout.Height(32)))
            QueueEquipmentRemoval(conditionId, item);
    }

    /// <summary>Keeps incomplete quantity text as a draft and validates only when Apply is pressed.</summary>
    private void DrawEquipmentQuantity(string conditionId, XElement item, Thing prefab)
    {
        if (!_equipmentQuantityDrafts.TryGetValue(item, out var draft))
            draft = (string?)item.Element("Quantity")?.Attribute("Value") ?? "1";
        GUILayout.Label("Qty", _mutedStyle!, GUILayout.Width(30));
        draft = GUILayout.TextField(draft, _inputStyle!, GUILayout.Width(65));
        _equipmentQuantityDrafts[item] = draft;
        GUILayout.Space(10);
        if (NativeButton("Apply", _buttonStyle!, GUILayout.Width(85), GUILayout.Height(32)))
        {
            if (!int.TryParse(draft, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity))
                _status = "Enter a whole-number quantity.";
            else
                _pendingEquipmentChange = () => _project!.SetEquipmentQuantity(conditionId, item, quantity, EquipmentPrefabRules.MaximumQuantity(prefab));
        }
        GUILayout.Space(10);
    }

    /// <summary>Uses native readable names for containers absent from the item catalog while retaining custom labels.</summary>
    private string EquipmentDisplayName(XElement item)
    {
        if (_equipmentViewByElement.TryGetValue(item, out var visible)) return EquipmentViewName(visible);
        var name = _project!.GetEquipmentName(item);
        var id = (string?)item.Attribute("Id") ?? "";
        return name == id ? EquipmentPrefabRules.Find(id)?.DisplayName ?? name : name;
    }
}
