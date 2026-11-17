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
        }
    }

    /// <summary>Draws Starting Equipment and Equipment Overview as separate sibling cards.</summary>
    private void DrawStartingEquipment(string conditionId)
    {
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
        GUILayout.Label("All landers, containers and player kits. Contents opens that container in the equipment editor.", _mutedStyle!);
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
        if (editable && !_equipmentLanderPicker)
            DrawEquipmentContainers(conditionId);
        if (_equipmentProfile == conditionId && editable && _equipmentLanderPicker)
            DrawEquipmentLanderPicker(conditionId);
    }

    /// <summary>Keeps every spawn and loose item accessible, including objects without native slots.</summary>
    private void DrawEquipmentTree(string conditionId, bool editable)
    {
        _equipmentScrolls.TryGetValue(conditionId, out var scroll);
        _equipmentScrolls[conditionId] = GUILayout.BeginScrollView(scroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar,
            GUILayout.Height(Mathf.Clamp(_window.height - 420f, 360f, 680f)));
        if (!editable)
        {
            foreach (var line in _project!.GetStartingEquipment(conditionId))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(Mathf.Min(line.Depth, 10) * 12f);
                GUILayout.Label(line.Text, _mutedStyle!, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();
            }
        }
        else
            foreach (var row in _project!.GetEquipmentEntries(conditionId))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(Mathf.Min(row.Depth, 8) * 12f);
                GUILayout.Label((row.Depth == 0 ? row.Event + " · " : "") + row.Label, _mutedStyle!, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
                if (EquipmentParent(row.Element, row.Event)?.Slots?.Count > 0 && NativeButton("Contents", _buttonStyle!, GUILayout.Width(95)))
                    _pendingEquipmentChange = () => SelectEquipmentContainer(conditionId, row.Element);
                if (NativeButton("Remove", _buttonStyle!, GUILayout.Width(85)))
                {
                    var selected = row.Element;
                    QueueEquipmentRemoval(conditionId, selected);
                }

                GUILayout.EndHorizontal();
            }

        GUILayout.EndScrollView();
    }

    /// <summary>Opens search for this exact container rather than a global selected item.</summary>
    private void SelectEquipmentContainer(string conditionId, XElement container)
    {
        _equipmentProfile = conditionId;
        _equipmentContainer = container;
        _equipmentSearchSlot = -1;
        _equipmentSearch = "";
        _equipmentQuantity = "1";
        _equipmentLanderPicker = false;
        _equipmentItemSearch = false;
        _equipmentNameDraft = EquipmentDisplayName(container);
        _equipmentQuantityDrafts.Clear();
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
