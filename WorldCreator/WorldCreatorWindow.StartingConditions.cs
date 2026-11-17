using System;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private int _startConditionSelection;
    private int _worldStartSelection;
    private bool _showStartReferences;
    private int _newStartEventSelection;
    private string _newStartSpawnId = "DefaultNewPlayer";
    private string _newStartCrateName = "Supply crate";
    /// <summary>Arranges installed-profile selection and the current world starting-condition cards.</summary>
    private void DrawStartConditions()
    {
        var options = _project!.StartConditionOptions.ToArray();
        var optionLabels = _project!.StartConditionLabels.ToArray();
        GUILayout.BeginVertical(_cardStyle!);
        GUILayout.Label("Start from an installed profile", _sectionStyle!);
        GUILayout.Label(
            "Copies the selected game's spawn references into a new world-specific StartCondition. Existing game definitions remain untouched.",
            _mutedStyle!
        );
        if (options.Length > 0)
        {
            _startConditionSelection = Mathf.Clamp(_startConditionSelection, 0, options.Length - 1);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Installed profile", _mutedStyle!, GUILayout.Width(140));
            _startConditionSelection = NativeDropdown("start-profile", _startConditionSelection, optionLabels, _buttonStyle!, GUILayout.Width(420), GUILayout.Height(32));
            GUILayout.Space(10);
            if (NativeButton("Add copy to this world", _primaryButtonStyle!, GUILayout.Width(230), GUILayout.Height(32)))
            {
                QueueInstalledStartingProfileCopy(options);
            }

            GUILayout.EndHorizontal();
        }
        else
            GUILayout.Label("No installed StartCondition definitions were found.", _mutedStyle!);
        GUILayout.Space(10);
        if (NativeButton("+ New blank condition", _buttonStyle!, GUILayout.Width(230), GUILayout.Height(32)))
            try
            {
                _pendingEquipmentChange = () =>
                {
                    _project!.CreateBlankStartCondition();
                    _worldStartSelection = _project.StartConditions.Count - 1;
                    _status = "Created a new starting condition.";
                };
            }
            catch (Exception e)
            {
                _status = e.Message;
            }

        GUILayout.EndVertical();
        GUILayout.Space(CardSpacing);
        GUILayout.Label("Starting options in this world", _sectionStyle!);
        var conditions = _project!.StartConditions;
        if (conditions.Count == 0)
            GUILayout.Label("This world has no start option. Add a copy of an installed profile above.", _mutedStyle!);
        if (conditions.Count > 0)
        {
            _worldStartSelection = Mathf.Clamp(_worldStartSelection, 0, conditions.Count - 1);
            var selected = NativeDropdown("world-start-condition", _worldStartSelection,
                conditions.Select(c => c.Name + (c.IsDefault ? " · Default" : "")).ToArray(),
                _buttonStyle!, GUILayout.Height(32), GUILayout.MaxWidth(600));
            if (selected != _worldStartSelection)
                _pendingEquipmentChange = () => _worldStartSelection = selected;
            GUILayout.Space(10);
            DrawStartingConditionCard(conditions[_worldStartSelection]);
        }
        GUILayout.Label("Equipment edits are saved as world-owned spawn definitions. Installed game and workshop source files stay unchanged.", _mutedStyle!);
    }

    /// <summary>Edits one starting profile and arranges its equipment, flags and spawn references.</summary>
    private bool DrawStartingConditionCard(WorldProject.StartConditionView condition)
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.BeginHorizontal();
        GUILayout.Label(
            condition.Name + (condition.IsDefault ? "  ·  Default" : "") + (condition.IsCustom ? "  ·  Custom copy" : "  ·  Installed profile"),
            _sectionStyle!
        );
        GUILayout.FlexibleSpace();
        if (!condition.IsDefault && NativeButton("Make default", _buttonStyle!, GUILayout.Width(130)))
            try
            {
                _pendingEquipmentChange = () =>
                {
                    _project!.SetDefaultStartCondition(condition.Id);
                    _status = condition.Id + " is now the default start.";
                };
            }
            catch (Exception e)
            {
                _status = e.Message;
            }

        if (!condition.IsCustom && NativeButton("Customize", _buttonStyle!, GUILayout.Width(110)))
            try
            {
                _pendingEquipmentChange = () =>
                {
                    _equipmentProfile = _project!.EditStartingEquipment(condition.Id);
                    _equipmentContainer = null;
                    _status = "Created a world-specific copy of " + condition.Id + ".";
                };
            }
            catch (Exception e)
            {
                _status = e.Message;
            }

        if (NativeButton("Remove", _buttonStyle!, GUILayout.Width(90)))
            try
            {
                _pendingEquipmentChange = () =>
                {
                    _project!.RemoveStartCondition(condition.Id);
                    _status = "Removed " + condition.Id + " from this world.";
                };
            }
            catch (Exception e)
            {
                _status = e.Message;
            }

        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Display name", _mutedStyle!, GUILayout.Width(110));
        var conditionName = GUILayout.TextField(condition.Name, _inputStyle!, GUILayout.ExpandWidth(true));
        if (condition.IsCustom && conditionName != condition.Name)
            try
            {
                _project!.SetStartConditionName(condition.Id, conditionName);
            }
            catch (Exception e)
            {
                _status = e.Message;
            }

        GUILayout.EndHorizontal();
        DrawStartingEquipment(condition.Id);
        if (condition.IsCustom)
        {
            DrawStartingConditionOptions(condition);
        }

        GUILayout.Space(12);
        var showReferences = GUILayout.Toggle(_showStartReferences, "Show spawn references and events", _buttonStyle!, GUILayout.Width(310), GUILayout.Height(32));
        if (showReferences != _showStartReferences)
            _pendingEquipmentChange = () => _showStartReferences = showReferences;
        if (_showStartReferences)
            foreach (var spawn in condition.Spawns.Select((value, index) => (value, index)))
                DrawStartingConditionSpawn(condition, spawn.value.Id, spawn.value.Event, spawn.index);

        if (condition.IsCustom)
        {
            DrawStartingConditionAdditions(condition);
        }

        GUILayout.EndVertical();
        return false;
    }

    /// <summary>Edits the brutal-mode and terrain-edit flags for a custom starting profile.</summary>
    private void DrawStartingConditionOptions(WorldProject.StartConditionView condition)
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("PROFILE OPTIONS", _sectionStyle!);
        GUILayout.Label("These flags control special rules for this starting profile.", _mutedStyle!);
        GUILayout.BeginHorizontal();
        var brutal = GUILayout.Toggle(
            condition.IsBrutal,
            new GUIContent("Brutal mode", "Enables the game's brutal start-condition rules for this profile."),
            _buttonStyle!,
            GUILayout.Width(170)
        );
        GUILayout.Label("Uses the game's harsher survival rules.", _mutedStyle!);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        var terrainEdit = GUILayout.Toggle(
            condition.IsTerrainEdit,
            new GUIContent("Terrain edit", "Enables this profile's terrain-edit start behavior."),
            _buttonStyle!,
            GUILayout.Width(170)
        );
        GUILayout.Label("Marks this start as the terrain-editing mode.", _mutedStyle!);
        GUILayout.EndHorizontal();
        if (brutal != condition.IsBrutal || terrainEdit != condition.IsTerrainEdit)
            try
            {
                _project!.SetStartConditionFlags(condition.Id, brutal, terrainEdit);
            }
            catch (Exception e)
            {
                _status = e.Message;
            }

        GUILayout.EndVertical();
    }

    /// <summary>Queues new supply crates or spawn references for a custom starting profile.</summary>
    private void DrawStartingConditionAdditions(WorldProject.StartConditionView condition)
    {
        GUILayout.Space(16);
        GUILayout.BeginVertical(_cardStyle!);
        GUILayout.Label("ADD EQUIPMENT AND SPAWN REFERENCES", _sectionStyle!);
        GUILayout.Label("Add a supply crate, then edit its slots through Starting Equipment above.", _mutedStyle!);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Crate name", _mutedStyle!, GUILayout.Width(110), GUILayout.Height(32));
        _newStartCrateName = GUILayout.TextField(_newStartCrateName, _inputStyle!, GUILayout.Width(320), GUILayout.Height(32));
        GUILayout.Space(10);
        if (NativeButton("+ Add supply crate", _buttonStyle!, GUILayout.Width(200), GUILayout.Height(32)))
            try
            {
                _pendingEquipmentChange = () =>
                {
                    var id = _project!.EditStartingEquipment(condition.Id);
                    var crateId = _project.AddStartConditionCrate(id, _newStartCrateName);
                    var crate = _project.GetEquipmentEntries(id).First(r => r.Element.Name == "Spawn" && (string?)r.Element.Attribute("Id") == crateId);
                    var container = _project.GetEquipmentContents(crate.Element).First();
                    SelectEquipmentContainer(id, container);
                    _status = "Added a supply crate. Choose an empty slot to add supplies.";
                };
            }
            catch (Exception e)
            {
                _status = e.Message;
            }

        GUILayout.EndHorizontal();
        GUILayout.Space(12);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Spawn ID", _mutedStyle!, GUILayout.Width(110), GUILayout.Height(32));
        _newStartSpawnId = GUILayout.TextField(_newStartSpawnId, _inputStyle!, GUILayout.Width(320), GUILayout.Height(32));
        GUILayout.Space(10);
        _newStartEventSelection = Mathf.Clamp(_newStartEventSelection, 0, StartEvents.Length - 1);
        _newStartEventSelection = NativeDropdown("new-spawn-event", _newStartEventSelection, StartEventLabels, _buttonStyle!, GUILayout.Width(200), GUILayout.Height(32));
        GUILayout.Space(10);
        if (NativeButton("+ Add spawn", _buttonStyle!, GUILayout.Width(200), GUILayout.Height(32)))
            try
            {
                _pendingEquipmentChange = () =>
                {
                    _project!.AddStartConditionSpawn(condition.Id, _newStartSpawnId, StartEvents[_newStartEventSelection]);
                    _status = "Added spawn reference. Confirm that its ID exists in the game or an enabled mod.";
                };
            }
            catch (Exception e)
            {
                _status = e.Message;
            }

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    /// <summary>Edits one spawn reference and its role, queuing removal without changing the active control tree.</summary>
    private void DrawStartingConditionSpawn(WorldProject.StartConditionView condition, string originalSpawnId, string originalEvent, int spawnIndex)
    {
        GUILayout.BeginVertical(_rowStyle!);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Spawn ID", _mutedStyle!, GUILayout.Width(90));
        var spawnId = GUILayout.TextField(originalSpawnId, _inputStyle!, GUILayout.MinWidth(180), GUILayout.ExpandWidth(true));
        var eventIndex = Array.IndexOf(StartEvents, originalEvent);
        GUILayout.Space(10);
        var nextEventIndex = eventIndex < 0 ? -1 : NativeDropdown("spawn-event-" + spawnIndex, eventIndex, StartEventLabels, _buttonStyle!, GUILayout.Width(200), GUILayout.Height(32));
        if (eventIndex < 0)
            GUILayout.Label("Unrecognized event: " + originalEvent, _mutedStyle!, GUILayout.Width(210));
        if (NativeButton("Remove", _buttonStyle!, GUILayout.Width(90)))
        {
            _pendingEquipmentChange = () =>
            {
                _project!.RemoveStartConditionSpawn(condition.Id, spawnIndex);
                _status = "Removed spawn reference.";
            };
        }

        if (condition.IsCustom && ((spawnId.Length > 0 && spawnId != originalSpawnId) || (nextEventIndex >= 0 && nextEventIndex != eventIndex)))
            try
            {
                _project!.SetStartConditionSpawn(condition.Id, spawnIndex, spawnId, StartEvents[nextEventIndex]);
            }
            catch (Exception e)
            {
                _status = e.Message;
            }

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    /// <summary>Queues the selected installed profile copy and reports selection errors without changing the active layout.</summary>
    private void QueueInstalledStartingProfileCopy(string[] options)
    {
        try
        {
            var selectedProfile = options[_startConditionSelection];
            _pendingEquipmentChange = () =>
            {
                _project!.AddStartConditionFrom(selectedProfile);
                _worldStartSelection = _project.StartConditions.Count - 1;
                _status = "Copied " + selectedProfile + " into this world.";
            };
        }
        catch (Exception e)
        {
            _status = e.Message;
        }
    }
}
