using System;
using System.Linq;
using System.Xml.Linq;
using Assets.Scripts.Objects;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    /// <summary>Offers complete installed supply kits accepted by the selected empty slot, using the current item search.</summary>
    private void DrawEquipmentKitPicker(string conditionId, XElement container, Thing parent, int index)
    {
        GUILayout.Space(12);
        GUILayout.Label("PREBUILT SUPPLY KITS", _sectionStyle!);
        GUILayout.Label("Adds a separate copy with its supplied items and tank contents. Search by kit name above.", _mutedStyle!);
        var matches = _project!.EquipmentKitOptions.Where(kit =>
            (_equipmentAppliedSearch.Length == 0 || kit.Name.IndexOf(_equipmentAppliedSearch, StringComparison.OrdinalIgnoreCase) >= 0 || kit.Id.IndexOf(_equipmentAppliedSearch, StringComparison.OrdinalIgnoreCase) >= 0)
            && EquipmentPrefabRules.Find(kit.PrefabId) is DynamicThing prefab && !(prefab is Lander)
            && EquipmentPrefabRules.Accepts(parent, parent.Slots[index], prefab)).Take(12).ToArray();
        foreach (var kit in matches)
            if (NativeButton("Add kit · " + HumanizeXmlName(kit.Name), _buttonStyle!, GUILayout.Width(620), GUILayout.Height(32)))
                QueueEquipmentKitAddition(conditionId, container, parent, kit, index);
        if (matches.Length == 0) GUILayout.Label("No compatible prebuilt kits match this search.", _mutedStyle!);
    }

    /// <summary>Rechecks native occupancy and insertion restrictions before copying a kit into its chosen slot.</summary>
    private void QueueEquipmentKitAddition(string conditionId, XElement container, Thing parent, WorldProject.EquipmentKit kit, int index)
    {
        _pendingEquipmentChange = () =>
        {
            var assignments = EquipmentPrefabRules.Assign(parent, _project!.GetEquipmentContents(container));
            _project.AddEquipmentKit(conditionId, container, kit.Id, index, prefabId =>
                index >= 0 && index < parent.Slots.Count && assignments.All(assignment => assignment.Slot != index)
                && EquipmentPrefabRules.Find(prefabId) is DynamicThing prefab && !(prefab is Lander)
                && EquipmentPrefabRules.Accepts(parent, parent.Slots[index], prefab), assignments);
            _equipmentItemSearch = false;
            _status = "Added " + HumanizeXmlName(kit.Name) + " to slot " + (index + 1) + ". Its contents can now be edited independently.";
        };
    }
}
