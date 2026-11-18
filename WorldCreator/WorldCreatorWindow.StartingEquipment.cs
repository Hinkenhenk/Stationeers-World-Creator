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
    private readonly Dictionary<string, Vector2> _equipmentScrolls = new Dictionary<string, Vector2>();
    private Action? _pendingEquipmentChange;
    private XElement? _equipmentContainer;
    private string _equipmentProfile = "";
    private string _equipmentSearch = "";
    private string _equipmentAppliedSearch = "";
    private string _equipmentQuantity = "1";
    private int _equipmentSearchSlot = -1;
    private bool _equipmentLanderPicker;
    /// <summary>Applies tree changes before GUILayout builds its next control hierarchy.</summary>
    private void ApplyEquipmentChange()
    {
        _equipmentAppliedSearch = _equipmentSearch;
        var change = _pendingEquipmentChange;
        _pendingEquipmentChange = null;
        if (change == null)
            return;
        try
        {
            change();
        }
        catch (Exception error)
        {
            _status = error.Message;
            SetExportIssue("Starting conditions → Starting Equipment: " + error.Message, null, 8);
        }
    }

    /// <summary>Draws Starting Equipment and Equipment Overview as separate sibling cards.</summary>
    private void DrawStartingEquipment(string conditionId)
    {
        RefreshEquipmentView(conditionId);
        var editable = _project!.CanEditEquipment(conditionId);
        var available = PageContentWidth - _cardStyle!.padding.horizontal;
        var sideBySide = available >= 1400f;
        var editorWidth = sideBySide ? Mathf.Max(900f, (available - CardSpacing) * 0.55f) : available;
        if (sideBySide)
            GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(_rowStyle!, GUILayout.Width(editorWidth));
        GUILayout.Label("STARTING EQUIPMENT", _sectionStyle!);
        DrawEquipmentEditor(conditionId, editable);
        GUILayout.EndVertical();
        GUILayout.Space(CardSpacing);
        GUILayout.BeginVertical(_rowStyle!, GUILayout.Width(sideBySide ? available - editorWidth - CardSpacing : available));
        GUILayout.Label("EQUIPMENT OVERVIEW", _sectionStyle!);
        GUILayout.Label("Expand containers with +. Click a name to inspect its contents. The highlighted row is your current location.", _mutedStyle!);
        DrawEquipmentTree(conditionId, editable);
        GUILayout.EndVertical();
        if (sideBySide)
            GUILayout.EndHorizontal();
    }

    /// <summary>Edits the selected container's slots or offers a private copy of installed equipment.</summary>
    private void DrawEquipmentEditor(string conditionId, bool editable)
    {
        if (!editable)
        {
            GUILayout.Label("Edit a private copy of the loaded lander, crates and player kits. The installed world stays unchanged.", _mutedStyle!);
            if (NativeButton("Edit this equipment", _buttonStyle!, GUILayout.Width(230), GUILayout.Height(32)))
                _pendingEquipmentChange = () =>
                {
                    _equipmentProfile = _project!.EditStartingEquipment(conditionId);
                    _equipmentContainer = null;
                    _status = "Equipment copied into this world. Select a container, then choose an empty slot to add supplies.";
                };
        }
        else if (NativeButton("+ Add another lander", _buttonStyle!, GUILayout.Width(230), GUILayout.Height(32)))
            _pendingEquipmentChange = () =>
            {
                _equipmentProfile = conditionId;
                _equipmentContainer = null;
                _equipmentLanderPicker = true;
                _equipmentSearch = "";
            };
        if (!_equipmentLanderPicker)
            DrawEquipmentContainers(conditionId);
        if (_equipmentProfile == conditionId && editable && _equipmentLanderPicker)
            DrawEquipmentLanderPicker(conditionId);
    }

    /// <summary>Opens search for this exact container rather than a global selected item.</summary>
    private void SelectEquipmentContainer(string conditionId, XElement container)
    {
        RefreshEquipmentView(conditionId);
        _equipmentProfile = conditionId;
        _equipmentContainer = container;
        _equipmentSearchSlot = -1;
        _equipmentSearch = "";
        _equipmentQuantity = "1";
        _equipmentLanderPicker = false;
        _equipmentItemSearch = false;
        _equipmentNameDraft = EquipmentDisplayName(container);
        _equipmentQuantityDrafts.Clear();
        foreach (var node in _equipmentViewByElement.Values.Where(node => node.Element == container))
            for (var parent = node.Parent; parent != null; parent = parent.Parent)
            {
                _expandedEquipment.Add(parent.Element);
                _collapsedEquipmentRoots.Remove(parent.Element);
            }
    }

    /// <summary>Lists empty native slots and searches the items accepted by the chosen slot.</summary>
    private void DrawEquipmentItemPicker(string conditionId, XElement container)
    {
        GUILayout.BeginVertical(_cardStyle!);
        try
        {
            var parent = EquipmentParent(container, container.Name == "Spawn" ? "NewPlayerKit" : "") ?? throw new InvalidOperationException("The installed container prefab is unavailable.");
            GUILayout.Label("ADD ITEM · " + parent.DisplayName, _sectionStyle!);
            var assignments = EquipmentPrefabRules.Assign(parent, _project!.GetEquipmentContents(container));
            var empty = Enumerable.Range(0, parent.Slots.Count).Where(i => assignments.All(a => a.Slot != i) && !parent.Slots[i].IsLocked).ToArray();
            if (!empty.Contains(_equipmentSearchSlot))
                GUILayout.Label("This slot is no longer empty. Choose another empty slot to add an item.", _mutedStyle!);
            else
            {
                DrawCompatibleEquipmentItems(conditionId, container, parent, _equipmentSearchSlot);
            }
        }
        catch (Exception error)
        {
            GUILayout.Label(error.Message, _mutedStyle!);
        }

        GUILayout.Space(12);
        if (NativeButton("Close item search", _buttonStyle!, GUILayout.Width(230), GUILayout.Height(32)))
            _pendingEquipmentChange = () => _equipmentItemSearch = false;
        GUILayout.EndVertical();
    }

    /// <summary>Adds a separate lander without placing it inside another container.</summary>
    private void DrawEquipmentLanderPicker(string conditionId)
    {
        GUILayout.BeginVertical(_cardStyle!);
        GUILayout.Label("ADD ANOTHER LANDER", _sectionStyle!);
        GUILayout.Label("Adds an empty lander to the World event. Use its Contents button to fill its slots.", _mutedStyle!);
        _equipmentSearch = GUILayout.TextField(_equipmentSearch, _inputStyle!, GUILayout.Width(540), GUILayout.Height(32));
        GUILayout.Space(12);
        var landers = EquipmentPrefabRules.Prefabs.Values.OfType<Lander>().Where(p => p.PrefabName.IndexOf(_equipmentAppliedSearch, StringComparison.OrdinalIgnoreCase) >= 0 || p.DisplayName.IndexOf(_equipmentAppliedSearch, StringComparison.OrdinalIgnoreCase) >= 0)
            .OrderBy(p => p.DisplayName)
            .ToArray();
        foreach (var lander in landers)
            if (NativeButton("Add " + lander.DisplayName + " · " + lander.PrefabName, _buttonStyle!, GUILayout.Width(620), GUILayout.Height(32)))
                _pendingEquipmentChange = () =>
                {
                    _project!.AddEquipmentLander(conditionId, lander.PrefabName, () => EquipmentPrefabRules.Find(lander.PrefabName) is Lander);
                    _equipmentLanderPicker = false;
                    _status = "Added another lander. Its slots can be edited independently.";
                };
        if (landers.Length == 0)
            GUILayout.Label("No matching lander prefab is available.", _mutedStyle!);
        GUILayout.Space(12);
        if (NativeButton("Cancel", _buttonStyle!, GUILayout.Width(230), GUILayout.Height(32)))
            _pendingEquipmentChange = () => _equipmentLanderPicker = false;
        GUILayout.EndVertical();
    }

    /// <summary>Uses character slots for player kits and the object's own slots for physical containers.</summary>
    private static Thing? EquipmentParent(XElement element, string eventName) => element.Name == "Spawn" ? (eventName == "NewPlayerKit" || eventName == "RespawnPlayerKit" ? EquipmentPrefabRules.Find("Character") : null) : EquipmentPrefabRules.Find((string? )element.Attribute("Id") ?? "");
    /// <summary>Searches the chosen empty slot's compatible prefabs and queues a validated addition.</summary>
    private void DrawCompatibleEquipmentItems(string conditionId, XElement container, Thing parent, int index)
    {
        GUILayout.Label("Slot " + (index + 1), _sectionStyle!);
        GUILayout.Label("Search compatible items by name or prefab ID", _mutedStyle!);
        _equipmentSearch = GUILayout.TextField(_equipmentSearch, _inputStyle!, GUILayout.Width(540), GUILayout.Height(32));
        GUILayout.Space(12);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Quantity", _mutedStyle!, GUILayout.Width(80));
        _equipmentQuantity = GUILayout.TextField(_equipmentQuantity, _inputStyle!, GUILayout.Width(80));
        GUILayout.EndHorizontal();
        GUILayout.Space(12);
        var slot = parent.Slots[index];
        var matches = EquipmentPrefabRules.Prefabs.Values.Where(p => p is DynamicThing && !(p is Lander) && (_equipmentAppliedSearch.Length == 0 || p.PrefabName.IndexOf(_equipmentAppliedSearch, StringComparison.OrdinalIgnoreCase) >= 0 || p.DisplayName.IndexOf(_equipmentAppliedSearch, StringComparison.OrdinalIgnoreCase) >= 0) && EquipmentPrefabRules.Accepts(parent, slot, p))
            .OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase).Take(12)
            .ToArray();
        foreach (var item in matches)
            if (NativeButton("Add " + item.DisplayName + " · " + item.PrefabName, _buttonStyle!, GUILayout.Width(620), GUILayout.Height(32)))
            {
                if (!int.TryParse(_equipmentQuantity, NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity) || quantity < 1 || quantity > EquipmentPrefabRules.MaximumQuantity(item))
                    _status = "Quantity must be between 1 and " + EquipmentPrefabRules.MaximumQuantity(item) + " for this item.";
                else
                    QueueEquipmentAddition(conditionId, container, parent, item, quantity, index);
            }

        if (matches.Length == 0)
            GUILayout.Label("No compatible items match this search.", _mutedStyle!);
        DrawEquipmentKitPicker(conditionId, container, parent, index);
    }

    /// <summary>Defers removal of one equipment occurrence until the next layout pass.</summary>
    private void QueueEquipmentRemoval(string conditionId, XElement selected)
    {
        _pendingEquipmentChange = () =>
        {
            _project!.RemoveEquipment(conditionId, selected);
            if (_equipmentContainer == selected || !_project.GetEquipmentEntries(conditionId).Any(row => row.Element == _equipmentContainer))
                _equipmentContainer = null;
            _status = "Removed this occurrence and its contents from the project copy.";
        };
    }

    /// <summary>Rechecks the selected slot and defers an item addition until the next layout pass.</summary>
    private void QueueEquipmentAddition(string conditionId, XElement container, Thing parent, Thing item, int quantity, int index)
    {
        var slot = parent.Slots[index];
        _pendingEquipmentChange = () =>
        {
            var current = EquipmentPrefabRules.Assign(parent, _project!.GetEquipmentContents(container));
            _project.AddEquipmentItem(
                conditionId,
                container,
                item.PrefabName,
                quantity,
                index,
                () => current.All(a => a.Slot != index) && EquipmentPrefabRules.Accepts(parent, slot, item),
                current,
                !(item is Item)
            );
            _equipmentItemSearch = false;
            _status = "Added " + item.DisplayName + " to slot " + (index + 1) + ".";
        };
    }
}
