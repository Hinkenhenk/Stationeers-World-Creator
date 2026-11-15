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

    /// <summary>Selects one physical container or player kit and exposes its actual native slots.</summary>
    private void DrawEquipmentContainers(string conditionId)
    {
        var containers = _project!.GetEquipmentEntries(conditionId)
            .Where(row => EquipmentParent(row.Element, row.Event)?.Slots?.Count > 0).ToArray();
        if (containers.Length == 0)
        {
            GUILayout.Label("No containers yet. Add a supply crate or lander, or inspect the equipment overview.", _mutedStyle!);
            return;
        }
        var index = Array.FindIndex(containers, row => row.Element == _equipmentContainer && _equipmentProfile == conditionId);
        if (index < 0)
        {
            _pendingEquipmentChange ??= () => SelectEquipmentContainer(conditionId, containers[0].Element);
            return;
        }
        GUILayout.Label("Container / player kit", _mutedStyle!);
        var labels = containers.Select((row, i) => (i + 1) + ". " + row.Event + " · " +
            (row.Element.Name == "Spawn" ? row.Label : EquipmentDisplayName(row.Element))).ToArray();
        var next = NativeDropdown("equipment-container-" + conditionId, index, labels, _buttonStyle!, GUILayout.Height(32));
        if (next != index)
            _pendingEquipmentChange = () => SelectEquipmentContainer(conditionId, containers[next].Element);
        var selected = containers[index];
        var parent = EquipmentParent(selected.Element, selected.Event)!;
        DrawEquipmentContainerActions(conditionId, selected.Element);
        IReadOnlyList<(XElement Item, int Slot)> assignments;
        try
        {
            assignments = EquipmentPrefabRules.Assign(parent, _project.GetEquipmentContents(selected.Element));
        }
        catch (Exception error)
        {
            GUILayout.Label(error.Message + " Use the equipment overview to inspect or remove unsupported contents.", _mutedStyle!);
            return;
        }
        GUILayout.Label(assignments.Count + " occupied / " + parent.Slots.Count + " slots · " + parent.DisplayName, _mutedStyle!);
        for (var slot = 0; slot < parent.Slots.Count; slot++)
        {
            DrawEquipmentSlot(conditionId, selected.Element, parent, slot, assignments);
            if (_equipmentItemSearch && slot == _equipmentSearchSlot)
                DrawEquipmentItemPicker(conditionId, selected.Element);
        }
    }

    /// <summary>Renames or removes the selected occurrence while preserving installed definitions.</summary>
    private void DrawEquipmentContainerActions(string conditionId, XElement container)
    {
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        if (container.Name != "Spawn")
        {
            GUILayout.Label("Container name", _mutedStyle!, GUILayout.Width(120));
            _equipmentNameDraft = GUILayout.TextField(_equipmentNameDraft, _inputStyle!, GUILayout.MaxWidth(340));
            GUILayout.Space(10);
            if (NativeButton("Rename", _buttonStyle!, GUILayout.Width(110), GUILayout.Height(30)))
                _pendingEquipmentChange = () => _project!.SetEquipmentName(conditionId, container, _equipmentNameDraft);
        }
        GUILayout.FlexibleSpace();
        if (NativeButton("Remove container / kit", _buttonStyle!, GUILayout.Width(210), GUILayout.Height(30)))
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
        GUILayout.Label("Slot " + (index + 1) + (string.IsNullOrWhiteSpace(slot.StringKey) ? "" : " · " + slot.StringKey), _mutedStyle!, GUILayout.Width(200));
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
        if (prefab != null)
            DrawEquipmentQuantity(conditionId, item, prefab);
        if (prefab?.Slots?.Count > 0 && NativeButton("Contents", _buttonStyle!, GUILayout.Width(110), GUILayout.Height(32)))
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
        var name = _project!.GetEquipmentName(item);
        var id = (string?)item.Attribute("Id") ?? "";
        return name == id ? EquipmentPrefabRules.Find(id)?.DisplayName ?? name : name;
    }
}
