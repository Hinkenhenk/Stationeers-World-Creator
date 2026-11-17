using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private int _weatherSelection;
    private int _savedWeatherSelection;
    private string _newWeatherId = "WCWeather_MyEffect";
    private string _newWeatherName = "My Weather Effect";
    /// <summary>Arranges installed presets, saved custom weather and new-effect creation controls.</summary>
    private void DrawWeatherControls()
    {
        var project = _project!;
        var installed = project.WeatherOptions.Where(id => !project.IsUserWeather(id)).ToArray();
        var installedLabels = installed.Select(project.WeatherName).ToArray();
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("INSTALLED WEATHER", _sectionStyle!);
        GUILayout.Label("Choose a game or world-mod event to add to this world. Removing it here never deletes its installed definition.", _mutedStyle!);
        if (installed.Length == 0)
            GUILayout.Label("No installed weather events were found.", _mutedStyle!);
        else
        {
            _weatherSelection = Mathf.Clamp(_weatherSelection, 0, installed.Length - 1);
            GUILayout.BeginHorizontal();
            _weatherSelection = NativeDropdown("installed-weather", _weatherSelection, installedLabels, _buttonStyle!, GUILayout.Width(Mathf.Min(480, Mathf.Max(180, PageContentWidth - 220))));
            GUILayout.Space(10);
            var selected = installed[Mathf.Clamp(_weatherSelection, 0, installed.Length - 1)];
            var selectedName = project.WeatherName(selected);
            if (NativeButton(project.HasWeather(selected) ? "Remove from world" : "Add to world", _primaryButtonStyle!, GUILayout.Width(170)))
            {
                ToggleWeatherOnWorld(project, selected, selectedName);
            }

            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
        GUILayout.Space(CardSpacing);
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("MY SAVED WEATHER", _sectionStyle!);
        GUILayout.Label(
            "Your custom effects are saved on this computer and remain available after restarting the game. Built-in presets cannot be deleted here.",
            _mutedStyle!
        );
        var saved = project.SavedWeatherEffects;
        if (saved.Count == 0)
            GUILayout.Label("No saved effects yet. Create an effect, edit its card, then choose Save to My Weather.", _mutedStyle!);
        else
        {
            _savedWeatherSelection = Mathf.Clamp(_savedWeatherSelection, 0, saved.Count - 1);
            GUILayout.BeginHorizontal();
            var savedLabels = saved.Select(effect => effect.Name + "  ·  " + effect.Id).ToArray();
            _savedWeatherSelection = NativeDropdown("saved-weather", _savedWeatherSelection, savedLabels, _buttonStyle!, GUILayout.Width(Mathf.Min(600, Mathf.Max(180, PageContentWidth - 40))));
            var selectedSaved = saved[Mathf.Clamp(_savedWeatherSelection, 0, saved.Count - 1)];
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (NativeButton(project.HasWeather(selectedSaved.Id) ? "Remove from world" : "Add to world", _primaryButtonStyle!, GUILayout.Width(170)))
            {
                ToggleWeatherOnWorld(project, selectedSaved.Id, selectedSaved.Name);
            }

            GUILayout.Space(10);
            if (NativeButton("Delete saved effect", _buttonStyle!, GUILayout.Width(175)))
                ConfirmDeleteSavedWeather(selectedSaved.Id, selectedSaved.Name);
            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
        GUILayout.Space(CardSpacing);
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("CREATE WEATHER EFFECT", _sectionStyle!);
        GUILayout.Label(
            "Create a blank event or copy an installed event as a starting point. The new effect is independent and can be edited below.",
            _mutedStyle!
        );
        GUILayout.BeginHorizontal();
        GUILayout.Label("Weather ID", _mutedStyle!, GUILayout.Width(72));
        _newWeatherId = GUILayout.TextField(_newWeatherId, _inputStyle!, GUILayout.MinWidth(180), GUILayout.ExpandWidth(true));
        GUILayout.Space(10);
        GUILayout.Label("Display name", _mutedStyle!, GUILayout.Width(88));
        _newWeatherName = GUILayout.TextField(_newWeatherName, _inputStyle!, GUILayout.MinWidth(150), GUILayout.ExpandWidth(true));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (NativeButton("Create blank effect", _primaryButtonStyle!, GUILayout.Width(180)))
            try
            {
                project.CreateWeather(_newWeatherId.Trim(), _newWeatherName.Trim());
                _status = "Created a blank weather effect. Edit its card below, then save it to My Weather if you want to reuse it.";
            }
            catch (Exception e)
            {
                _status = e.Message;
            }

        if (installed.Length > 0 && NativeButton("Copy selected event", _buttonStyle!, GUILayout.Width(175)))
            try
            {
                project.CreateWeather(_newWeatherId.Trim(), _newWeatherName.Trim(), installed[Mathf.Clamp(_weatherSelection, 0, installed.Length - 1)]);
                _status = "Copied the selected event into an independent custom effect.";
            }
            catch (Exception e)
            {
                _status = e.Message;
            }

        GUILayout.EndHorizontal();
        GUILayout.Label("Weather IDs must start with WCWeather_. Blank effects get default timing, atmosphere, wind, and movement settings.", _mutedStyle!);
        GUILayout.EndVertical();
    }

    /// <summary>Explains the timing controls alongside the event settings without duplicating their values.</summary>
    private void DrawWeatherTimingDescription()
    {
        GUILayout.Space(8);
        GUILayout.Label("Cooldown sets the wait between events in days. A value of 0 allows events without a day-long cooldown; it does not disable them. Start delay is the wait before an event begins, and duration is how long it lasts; both are in seconds. The game chooses values between each minimum and maximum. Remove an event from this world to prevent it from occurring.", _mutedStyle!);
        GUILayout.Space(8);
        GUILayout.Label("The game also delays natural weather until DaysPast > 7 × StartingWeatherMultiplier. With the default multiplier of 1, weather first becomes eligible on Day 8 when counting from Day 0. This initial gate is separate from each event's timing; FirstStormDelay appears unused in this game build.", _mutedStyle!);
    }

    /// <summary>Organizes each event's identity, timing, gameplay, visuals and audio into readable sections.</summary>
    private void DrawWeatherFieldGroups(IReadOnlyList<WorldProject.Field> fields)
    {
        foreach (var eventGroup in fields.GroupBy(f => f.Label.Split('/')[0]))
        {
            var eventId = eventGroup.Key;
            GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal();
            GUILayout.Label(_project!.WeatherName(eventId) + "  ·  " + eventId, _sectionStyle!);
            GUILayout.FlexibleSpace();
            if (_project.IsUserWeather(eventId) && NativeButton("Save to My Weather", _buttonStyle!, GUILayout.Width(170)))
            {
                try
                {
                    _project.SaveWeatherToLibrary(eventId);
                    _status = "Saved " + _project.WeatherName(eventId) + " to My Weather.";
                }
                catch (Exception e)
                {
                    _status = e.Message;
                }
            }

            GUILayout.EndHorizontal();
            var sections = new[]
            {
                (Title: "Event identity & availability", Fields: eventGroup.Where(f => IsWeatherIdentityField(f, eventId)).ToArray()),
                (Title: "Timing", Fields: eventGroup.Where(f => IsWeatherTimingField(f, eventId)).ToArray()),
                (Title: "Surface conditions & gameplay", Fields: eventGroup.Where(f => IsWeatherConditionField(f, eventId)).ToArray()),
                (Title: "Audio & event flags", Fields: eventGroup.Where(f => IsWeatherAudioField(f, eventId)).ToArray())
            };
            if (DrawCardColumns(sections.Where(section => section.Fields.Length > 0 || section.Title == "Audio & event flags" && _project.IsUserWeather(eventId)).ToArray(), section =>
            {
                if (section.Title == "Audio & event flags")
                    return DrawWeatherAudio(eventId, section.Fields, _project.IsUserWeather(eventId));
                GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
                DrawWeatherSection(section.Title, section.Fields, eventId, null);
                GUILayout.EndVertical();
                return false;
            }, _cardStyle!.padding.horizontal))
            {
                GUILayout.EndVertical();
                return;
            }

            if (_project!.IsUserWeather(eventId) && DrawWeatherEffectControls(eventId))
            {
                GUILayout.EndVertical();
                return;
            }

            var visualFields = eventGroup.Where(f => IsWeatherVisualField(f, eventId)).ToArray();
            var blocks = visualFields.GroupBy(f => WeatherVisualBlockKey(f, eventId)).Where(g => g.Key != null).ToArray();
            if (DrawCardColumns(blocks, blockGroup =>
            {
                var blockKey = blockGroup.Key!;
                var separator = blockKey.IndexOf(' ');
                var block = separator < 0 ? blockKey : blockKey.Substring(0, separator);
                var instanceText = separator < 0 ? "1" : new string (blockKey.Substring(separator + 2).TakeWhile(char.IsDigit).ToArray());
                var instanceIndex = int.TryParse(instanceText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedIndex) ? parsedIndex - 1 : 0;
                var title = block == "StormEffect" ? "Storm effect" : block == "DirectionalLight" ? "Directional light" : "Fog";
                return DrawWeatherEffectCard(title + (blockKey.Contains(" #") ? " " + blockKey.Substring(separator + 1) : ""), blockGroup.ToArray(), eventId, block, instanceIndex);
            }, _cardStyle!.padding.horizontal))
            {
                GUILayout.EndVertical();
                return;
            }

            if (visualFields.Select(f => WeatherVisualBlockKey(f, eventId)).Count(key => key != null && key.StartsWith("Fog #", StringComparison.Ordinal)) > 1)
                GUILayout.Label(
                    "Multiple Fog cards are editable, but this installed game schema exposes one Fog slot per event; extra Fog blocks may not appear as separate in-game layers.",
                    _mutedStyle!
                );
            var knownFields = eventGroup.Where(f => IsWeatherIdentityField(f, eventId) || IsWeatherTimingField(f, eventId) || IsWeatherConditionField(f, eventId) || IsWeatherVisualField(f, eventId) || IsWeatherAudioField(f, eventId)).ToHashSet();
            DrawWeatherSection("Other settings", eventGroup.Where(f => !knownFields.Contains(f)), eventId, null);
            GUILayout.EndVertical();
            GUILayout.Space(CardSpacing);
        }
    }

    /// <summary>Offers compatible visual blocks that can be added independently to a custom event.</summary>
    private bool DrawWeatherEffectControls(string eventId)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Visual effects", _mutedStyle!);
        GUILayout.FlexibleSpace();
        foreach (var block in new[]
        {
            "Fog",
            "StormEffect",
            "DirectionalLight"
        }

        )
        {
            var title = block == "StormEffect" ? "Storm effect" : block == "DirectionalLight" ? "Directional light" : block;
            if (!NativeButton("+ " + title, _buttonStyle!, GUILayout.MinWidth(100)))
                continue;
            try
            {
                _project!.AddWeatherBlock(eventId, block);
                _status = "Added " + title + " card to " + _project.WeatherName(eventId) + ".";
            }
            catch (Exception e)
            {
                _status = e.Message;
            }

            GUILayout.EndHorizontal();
            return true;
        }

        GUILayout.EndHorizontal();
        return false;
    }

    /// <summary>Edits wind-sound flags and the event's individual sound slots.</summary>
    private bool DrawWeatherAudio(string eventId, IEnumerable<WorldProject.Field> source, bool editable)
    {
        var fields = source.ToArray();
        if (fields.Length == 0 && !editable)
            return false;
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
            catch (Exception e)
            {
                _status = e.Message;
            }
        }

        GUILayout.EndHorizontal();
        var soundGroups = fields.Select(field => (Field: field, Slot: WeatherShellSoundSlot(field, eventId))).Where(entry => entry.Slot.HasValue).GroupBy(entry => entry.Slot!.Value)
            .OrderBy(group => group.Key)
            .ToArray();
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
                catch (Exception e)
                {
                    _status = e.Message;
                }

                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.EndVertical();
                return true;
            }

            GUILayout.EndHorizontal();
            foreach (var entry in soundGroup)
                DrawWeatherValue(entry.Field, eventId, null);
            GUILayout.EndVertical();
            GUILayout.Space(CardSpacing);
        }

        var flags = fields.Where(field => !WeatherShellSoundSlot(field, eventId).HasValue).ToArray();
        DrawWeatherSection("Wind and event flags", flags, eventId, null);
        if (fields.Length == 0)
            GUILayout.Label("No shell sounds or event audio flags are set.", _mutedStyle!);
        GUILayout.EndVertical();
        GUILayout.Space(CardSpacing);
        return false;
    }

    /// <summary>Extracts the numbered shell-sound slot from a weather field's XML path.</summary>
    private static int? WeatherShellSoundSlot(WorldProject.Field field, string eventId)
    {
        var root = WeatherRelativePath(field, eventId).Split('/')[0];
        if (root.Length != 11 || !root.StartsWith("Shell", StringComparison.Ordinal) || !root.EndsWith("Sound", StringComparison.Ordinal) || !int.TryParse(root.Substring(5, 1), NumberStyles.None, CultureInfo.InvariantCulture, out var slot) || slot < 1 || slot > 4)
            return null;
        return slot;
    }

    /// <summary>Draws the fields and removal control for one independent visual-effect block.</summary>
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
            catch (Exception e)
            {
                _status = e.Message;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            return true;
        }

        GUILayout.EndHorizontal();
        for (var i = 0; i < fields.Count; i += 2)
        {
            GUILayout.BeginHorizontal();
            DrawWeatherValue(fields[i], eventId, block);
            if (i + 1 < fields.Count)
            {
                GUILayout.Space(10);
                DrawWeatherValue(fields[i + 1], eventId, block);
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
        GUILayout.Space(CardSpacing);
        return false;
    }

    /// <summary>Arranges one weather setting group in rows without mixing unrelated effect blocks.</summary>
    private void DrawWeatherSection(string title, IEnumerable<WorldProject.Field> source, string eventId, string? visualBlock)
    {
        var sectionFields = source.ToArray();
        if (sectionFields.Length == 0)
            return;
        GUILayout.Space(4);
        GUILayout.Label(title, _mutedStyle!);
        for (var i = 0; i < sectionFields.Length; i += 2)
        {
            GUILayout.BeginHorizontal();
            DrawWeatherValue(sectionFields[i], eventId, visualBlock);
            if (i + 1 < sectionFields.Length)
            {
                GUILayout.Space(10);
                DrawWeatherValue(sectionFields[i + 1], eventId, visualBlock);
            }
            GUILayout.EndHorizontal();
        }
    }

    /// <summary>Draws the appropriate control and readable label for a weather value.</summary>
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

    /// <summary>Removes the event-specific prefix before classifying a weather field.</summary>
    private static string WeatherRelativePath(WorldProject.Field field, string eventId)
    {
        var prefix = eventId + "/";
        var relativePath = field.Label.StartsWith(prefix, StringComparison.Ordinal) ? field.Label.Substring(prefix.Length) : field.Label;
        return relativePath.Replace(" / ", "/").Trim();
    }

    /// <summary>Checks whether a weather path contains the requested XML segment.</summary>
    private static bool WeatherPathHas(WorldProject.Field field, string eventId, string segment) => WeatherRelativePath(field, eventId).IndexOf("/" + segment + "/", StringComparison.OrdinalIgnoreCase) >= 0 || WeatherRelativePath(field, eventId).StartsWith(segment + "/", StringComparison.OrdinalIgnoreCase);
    /// <summary>Identifies the visual-effect block to which a weather field belongs.</summary>
    private static string? WeatherVisualBlockKey(WorldProject.Field field, string eventId)
    {
        var root = WeatherRelativePath(field, eventId).Split('/')[0];
        foreach (var block in new[]
        {
            "Fog",
            "StormEffect",
            "DirectionalLight"
        }

        )
            if (root == block || root.StartsWith(block + " #", StringComparison.Ordinal))
                return root;
        return null;
    }

    /// <summary>Recognizes name and availability fields that belong in event identity.</summary>
    private static bool IsWeatherIdentityField(WorldProject.Field field, string eventId)
    {
        var path = WeatherRelativePath(field, eventId);
        return path.Contains("/Name/", StringComparison.OrdinalIgnoreCase) || path.EndsWith("/ActiveInOrbit", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Recognizes cooldown, delay and duration fields for the event timing section.</summary>
    private static bool IsWeatherTimingField(WorldProject.Field field, string eventId) => WeatherPathHas(field, eventId, "CoolDown") || WeatherPathHas(field, eventId, "StartDelay") || WeatherPathHas(field, eventId, "Duration");
    /// <summary>Recognizes temperature, sunlight, wind, movement and damage modifiers.</summary>
    private static bool IsWeatherConditionField(WorldProject.Field field, string eventId)
    {
        var path = WeatherRelativePath(field, eventId);
        return new[]
        {
            "TemperatureOffset",
            "SolarRatio",
            "WindStrength",
            "MovementSpeedMultiplier",
            "DamageMultiplier",
            "Direction"
        }.Any(segment => WeatherPathHas(field, eventId, segment));
    }

    /// <summary>Recognizes fields belonging to visual-effect blocks.</summary>
    private static bool IsWeatherVisualField(WorldProject.Field field, string eventId) => new[]
    {
        "Fog",
        "StormEffect",
        "DirectionalLight"
    }.Any(segment => WeatherPathHas(field, eventId, segment));
    /// <summary>Recognizes sound settings independently of visual and gameplay settings.</summary>
    private static bool IsWeatherAudioField(WorldProject.Field field, string eventId)
    {
        var path = WeatherRelativePath(field, eventId);
        return path.IndexOf("Sound", StringComparison.OrdinalIgnoreCase) >= 0 || path.EndsWith("/WindSound", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Turns a weather XML leaf into a context-specific player-facing label.</summary>
    private static string WeatherFieldLabel(WorldProject.Field field, string eventId, string? visualBlock)
    {
        var path = WeatherRelativePath(field, eventId);
        if (path.IndexOf("/CoolDown/", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Cooldown " + WeatherRangeEnd(path) + " (days)";
        if (path.IndexOf("/StartDelay/", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Start delay " + WeatherRangeEnd(path) + " (seconds)";
        if (path.IndexOf("/Duration/", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Duration " + WeatherRangeEnd(path) + " (seconds)";
        if (path.IndexOf("/TemperatureOffset/Day/", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Day temperature offset (K)";
        if (path.IndexOf("/TemperatureOffset/Night/", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Night temperature offset (K)";
        if (WeatherPathHas(field, eventId, "SolarRatio"))
            return "Sunlight (solar ratio)";
        if (WeatherPathHas(field, eventId, "WindStrength"))
            return "Wind strength";
        if (WeatherPathHas(field, eventId, "MovementSpeedMultiplier"))
            return "Movement speed multiplier";
        if (WeatherPathHas(field, eventId, "DamageMultiplier"))
            return "Damage multiplier";
        if (WeatherPathHas(field, eventId, "Direction"))
            return "Wind direction";
        if (path.EndsWith("/WindSound", StringComparison.OrdinalIgnoreCase))
            return "Wind sound enabled";
        if (path.EndsWith("/ActiveInOrbit", StringComparison.OrdinalIgnoreCase))
            return "Can occur in orbit";
        if (path.IndexOf("/Name/", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Game name key";
        var soundSlot = path.Split('/').FirstOrDefault(part => part.StartsWith("Shell", StringComparison.OrdinalIgnoreCase) && part.EndsWith("Sound", StringComparison.OrdinalIgnoreCase));
        if (soundSlot != null)
            return soundSlot.Substring(0, soundSlot.Length - "Sound".Length) + " sound (game audio ID)";
        var leaf = ReadableFieldLabel(field.Label, false);
        if (visualBlock != null)
        {
            var visualPath = path;
            var start = visualPath.IndexOf(visualBlock + "/", StringComparison.OrdinalIgnoreCase);
            if (start >= 0)
                visualPath = visualPath.Substring(start + visualBlock.Length + 1);
            var parts = visualPath.Split('/').Where(p => p.Length > 0).ToArray();
            var names = parts.Select(p => HumanizeXmlName(p.TrimStart('@'))).ToArray();
            if (parts.Length > 0)
            {
                var channelIndex = parts.Length - 1;
                var channel = parts[channelIndex].TrimStart('@');
                if (channel.Equals("r", StringComparison.OrdinalIgnoreCase))
                    names[channelIndex] = "Red";
                else if (channel.Equals("g", StringComparison.OrdinalIgnoreCase))
                    names[channelIndex] = "Green";
                else if (channel.Equals("b", StringComparison.OrdinalIgnoreCase))
                    names[channelIndex] = "Blue";
                else if (channel.Equals("a", StringComparison.OrdinalIgnoreCase))
                    names[channelIndex] = "Alpha";
            }

            if (names.Length > 1)
                leaf = string.Join(" / ", names.Skip(Math.Max(0, names.Length - 2)));
            else if (names.Length == 1)
                leaf = names[0];
        }

        return leaf;
    }

    /// <summary>Reads one endpoint of a weather timing range for its preview.</summary>
    private static string WeatherRangeEnd(string path)
    {
        if (path.EndsWith("/Min", StringComparison.OrdinalIgnoreCase))
            return "minimum";
        if (path.EndsWith("/Max", StringComparison.OrdinalIgnoreCase))
            return "maximum";
        return "value";
    }

    /// <summary>Adds or removes a selected weather event and reports the resulting project state.</summary>
    private void ToggleWeatherOnWorld(WorldProject project, string selected, string selectedName)
    {
        try
        {
            if (project.HasWeather(selected))
                project.RemoveWeather(selected);
            else
                project.AddWeather(selected);
            _status = (project.HasWeather(selected) ? "Added " : "Removed ") + selectedName + (project.HasWeather(selected) ? " to this world." : " from this world.");
        }
        catch (Exception e)
        {
            _status = e.Message;
        }
    }
}
