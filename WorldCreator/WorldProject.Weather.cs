using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    /// <summary>Checks whether the world already references the selected weather event.</summary>
    internal bool HasWeather(string id) => _weather.Any(w => (string? )w.Event.Attribute("Id") == id);
    /// <summary>Adds an installed weather event and creates editable fields for its project copy.</summary>
    internal void AddWeather(string id)
    {
        if (HasWeather(id))
            throw new InvalidOperationException(id + " is already assigned to this world.");
        var definition = _weatherLibrary.Elements("WeatherEvent").FirstOrDefault(e => (string? )e.Attribute("Id") == id) ?? throw new InvalidOperationException("Weather event not found: " + id);
        var reference = new XElement("WeatherEvent", new XAttribute("Id", id));
        _world.Add(reference);
        var copy = new XElement(definition);
        _weather.Add((reference, copy));
        AddEditableTree(WeatherFields, copy, id);
        _structureChanged = true;
    }

    /// <summary>Checks whether a weather ID belongs to the reusable user-created weather library.</summary>
    internal bool IsUserWeather(string id) => _userWeatherIds.Contains(id);
    /// <summary>Returns a weather event's friendly label, falling back to its XML ID.</summary>
    internal string WeatherName(string id) => _weatherNames.TryGetValue(id, out var name) ? name : id;
    /// <summary>Creates a validated custom weather definition, optionally copying an existing event as its starting point.</summary>
    internal string CreateWeather(string id, string name, string? baseId = null)
    {
        if (!WeatherEffectLibrary.IsValidId(id))
            throw new InvalidOperationException("Weather ID must begin with WCWeather_ and use letters, numbers, and underscores.");
        if (_weatherLibrary.Elements("WeatherEvent").Any(e => (string? )e.Attribute("Id") == id))
            throw new InvalidOperationException("That weather ID is already in use.");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 80)
            throw new InvalidOperationException("Enter an effect name of up to 80 characters.");
        XElement definition;
        if (!string.IsNullOrEmpty(baseId))
        {
            definition = new XElement(_weatherLibrary.Elements("WeatherEvent").FirstOrDefault(e => (string? )e.Attribute("Id") == baseId) ?? throw new InvalidOperationException("Weather preset not found."));
        }
        else
        {
            definition = new XElement(
                "WeatherEvent",
                new XAttribute("Id", id),
                new XAttribute("WindSound", "false"),
                new XAttribute("ActiveInOrbit", "false"),
                new XElement("Name", new XAttribute("Key", id)),
                new XElement("CoolDown", new XAttribute("Min", "3"), new XAttribute("Max", "12")),
                new XElement("StartDelay", new XAttribute("Min", "30"), new XAttribute("Max", "60")),
                new XElement("Duration", new XAttribute("Min", "120"), new XAttribute("Max", "240")),
                new XElement(
                "Fog",
                new XElement("Color", new XAttribute("r", "0.7"), new XAttribute("g", "0.75"), new XAttribute("b", "0.8"), new XAttribute("a", "1"))
            ),
                new XElement("TemperatureOffset", new XElement("Day", new XAttribute("Value", "0")), new XElement("Night", new XAttribute("Value", "0"))),
                new XElement("SolarRatio", new XAttribute("Value", "1")),
                new XElement("WindStrength", new XAttribute("Value", "0")),
                new XElement("MovementSpeedMultiplier", new XAttribute("Value", "1")),
                new XElement("DamageMultiplier", new XAttribute("Value", "0")),
                new XElement("Direction", new XAttribute("Value", "None"))
            );
        }

        definition.SetAttributeValue("Id", id);
        var nameElement = definition.Element("Name") ?? new XElement("Name");
        if (nameElement.Parent == null)
            definition.AddFirst(nameElement);
        nameElement.SetAttributeValue("Key", id);
        _weatherLibrary.Add(new XElement(definition));
        _weatherNames[id] = name.Trim();
        _userWeatherIds.Add(id);
        AddWeather(id);
        return id;
    }

    /// <summary>Commits a custom event's pending values and saves it for reuse in other projects.</summary>
    internal void SaveWeatherToLibrary(string id)
    {
        foreach (var field in WeatherFields)
        {
            field.Validate();
            field.Apply();
        }

        var effect = _weather.FirstOrDefault(w => (string? )w.Event.Attribute("Id") == id).Event;
        if (effect == null)
            throw new InvalidOperationException("Add the weather effect to this world before saving it.");
        WeatherEffectLibrary.Save(id, WeatherName(id), effect);
    }

    /// <summary>Adds a supported visual-effect block and binds its values to the weather editor.</summary>
    internal void AddWeatherBlock(string id, string block)
    {
        var effect = _weather.FirstOrDefault(w => (string? )w.Event.Attribute("Id") == id).Event;
        if (effect == null)
            throw new InvalidOperationException("Weather effect is not assigned to this world.");
        var installed = _weatherLibrary.Descendants(block).FirstOrDefault();
        XElement node;
        switch (block)
        {
            case "Fog":
                node = installed != null ? new XElement(installed) : new XElement(
                    "Fog",
                    new XElement("Color", new XAttribute("r", "0.7"), new XAttribute("g", "0.75"), new XAttribute("b", "0.8"), new XAttribute("a", "1"))
                );
                break;
            case "StormEffect":
                node = installed != null ? new XElement(installed) : new XElement(
                    "StormEffect",
                    new XAttribute("Speed", "2"),
                    new XAttribute("Size", "12"),
                    new XElement("TextureId", new XAttribute("Value", "0")),
                    new XElement("RayMarch", new XAttribute("Size", "0.2"), new XAttribute("Growth", "0.3"), new XAttribute("Density", "1.4")),
                    new XElement("Layer1", new XAttribute("Power", "0.8"), new XAttribute("Multiplier", "1")),
                    new XElement(
                    "Layer2",
                    new XAttribute("Threshold", "0.22"),
                    new XAttribute("Power", "0.84"),
                    new XAttribute("Multiplier", "1"),
                    new XAttribute("MaskMultiplier", "1")
                ),
                    new XElement("Layer3", new XAttribute("Threshold", "0.12"), new XAttribute("Power", "1.53"), new XAttribute("Multiplier", "0.5")),
                    new XElement("Color1", new XAttribute("r", "91"), new XAttribute("g", "69"), new XAttribute("b", "61"), new XAttribute("a", "255")),
                    new XElement("Color2", new XAttribute("r", "212"), new XAttribute("g", "153"), new XAttribute("b", "113"), new XAttribute("a", "255")),
                    new XElement("MaskColor", new XAttribute("r", "0"), new XAttribute("g", "0"), new XAttribute("b", "0"), new XAttribute("a", "255"))
                );
                break;
            case "DirectionalLight":
                node = installed != null ? new XElement(installed) : new XElement(
                    "DirectionalLight",
                    new XElement("Intensity", new XAttribute("Min", "8"), new XAttribute("Max", "12")),
                    new XElement("Frequency", new XAttribute("Value", "0.3")),
                    new XElement("Color32", new XAttribute("r", "84"), new XAttribute("g", "64"), new XAttribute("b", "44"), new XAttribute("a", "255"))
                );
                break;
            default:
                throw new InvalidOperationException("Unknown weather block.");
        }

        effect.Add(node);
        AddEditableTree(WeatherFields, node, id);
        _structureChanged = true;
    }

    /// <summary>Removes the selected visual-effect occurrence and rebinds that event's fields.</summary>
    internal void RemoveWeatherBlock(string id, string block, int instanceIndex)
    {
        if (block != "Fog" && block != "StormEffect" && block != "DirectionalLight")
            throw new InvalidOperationException("Unknown weather block.");
        var effect = _weather.FirstOrDefault(w => (string? )w.Event.Attribute("Id") == id).Event;
        if (effect == null)
            throw new InvalidOperationException("Weather effect is not assigned to this world.");
        var node = effect.Elements(block).ElementAtOrDefault(instanceIndex);
        if (node == null)
            throw new InvalidOperationException("This effect block no longer exists.");
        node.Remove();
        WeatherFields.RemoveAll(field => field.Label.StartsWith(id + "/", StringComparison.Ordinal) && new[] { "Fog", "StormEffect", "DirectionalLight" }.Any(type => WeatherFieldStartsWithBlock(field, id, type)));
        foreach (var child in effect.Elements().Where(e => e.Name.LocalName == "Fog" || e.Name.LocalName == "StormEffect" || e.Name.LocalName == "DirectionalLight"))
            AddEditableTree(WeatherFields, child, id);
        _structureChanged = true;
    }

    /// <summary>Recognizes fields belonging to the selected visual-effect block.</summary>
    private static bool WeatherFieldStartsWithBlock(Field field, string id, string block)
    {
        var path = field.Label.Substring(id.Length + 1).Replace(" / ", "/");
        return path.StartsWith(block + "/", StringComparison.Ordinal) || path.StartsWith(block + " #", StringComparison.Ordinal);
    }

    /// <summary>Adds an available shell-sound slot to the selected weather event.</summary>
    internal void AddWeatherSound(string id)
    {
        var effect = _weather.FirstOrDefault(w => (string? )w.Event.Attribute("Id") == id).Event;
        if (effect == null)
            throw new InvalidOperationException("Weather effect is not assigned to this world.");
        var index = Enumerable.Range(1, 4).FirstOrDefault(i => effect.Element("Shell" + i + "Sound") == null);
        if (index == 0)
            throw new InvalidOperationException("All four shell sound slots are already present.");
        var sound = new XElement(
            "Shell" + index + "Sound",
            new XAttribute("Value", (string? )_weatherLibrary.Descendants("Shell" + index + "Sound").FirstOrDefault()?.Attribute("Value") ?? "")
        );
        effect.Add(sound);
        AddEditableTree(WeatherFields, sound, id);
        _structureChanged = true;
    }

    /// <summary>Removes one shell-sound slot and its matching editable fields.</summary>
    internal void RemoveWeatherSound(string id, int slot)
    {
        if (slot < 1 || slot > 4)
            throw new InvalidOperationException("Choose a shell sound slot from 1 to 4.");
        var effect = _weather.FirstOrDefault(w => (string? )w.Event.Attribute("Id") == id).Event;
        if (effect == null)
            throw new InvalidOperationException("Weather effect is not assigned to this world.");
        var elementName = "Shell" + slot.ToString(CultureInfo.InvariantCulture) + "Sound";
        var sound = effect.Element(elementName) ?? throw new InvalidOperationException("This shell sound slot no longer exists.");
        sound.Remove();
        WeatherFields.RemoveAll(field => field.Label.Replace(" / ", "/").StartsWith(id + "/" + elementName + "/", StringComparison.Ordinal));
        _structureChanged = true;
    }

    /// <summary>Detaches the event from this world without deleting its reusable library definition.</summary>
    internal void RemoveWeather(string id)
    {
        var index = _weather.FindIndex(w => (string? )w.Event.Attribute("Id") == id);
        if (index < 0)
            throw new InvalidOperationException(id + " is not assigned to this world.");
        _weather[index].Reference.Remove();
        _weather.RemoveAt(index);
        WeatherFields.RemoveAll(f => f.Label.StartsWith(id + "/", StringComparison.Ordinal));
        _structureChanged = true;
    }

    /// <summary>Combines native, imported and saved weather definitions, then binds the world's selected events.</summary>
    private void InitializeWeatherSettings(XElement root, string file, bool isAbsolute)
    {
        foreach (var embeddedWeather in root.Elements("WeatherEvent"))
            _weatherLibrary.Add(new XElement(embeddedWeather));
        foreach (var embeddedWeather in root.Element("GameData")?.Elements("WeatherEvent") ?? Enumerable.Empty<XElement>())
            _weatherLibrary.Add(new XElement(embeddedWeather));
        var importedWeatherFile = Path.Combine(Path.GetDirectoryName(file) ?? string.Empty, "weather.xml");
        if (isAbsolute && File.Exists(importedWeatherFile))
            ImportSourceWeatherFile(importedWeatherFile);
        ImportSourceNames(file, isAbsolute);
        foreach (var saved in WeatherEffectLibrary.Load())
        {
            if (!_weatherLibrary.Elements("WeatherEvent").Any(e => (string? )e.Attribute("Id") == saved.Id))
                _weatherLibrary.Add(new XElement(saved.Definition));
            _weatherNames[saved.Id] = saved.Name;
            _userWeatherIds.Add(saved.Id);
        }

        foreach (var reference in _world.Elements("WeatherEvent"))
        {
            var id = (string? )reference.Attribute("Id");
            var definition = _weatherLibrary.Elements("WeatherEvent").FirstOrDefault(e => (string? )e.Attribute("Id") == id);
            if (definition == null)
                continue;
            var copy = new XElement(definition);
            _weather.Add((reference, copy));
            AddEditableTree(WeatherFields, copy, id ?? "Weather");
        }
    }

    /// <summary>Imports nonduplicate events from optional source weather XML without allowing a malformed file to hide the world.</summary>
    private void ImportSourceWeatherFile(string importedWeatherFile)
    {
        try
        {
            foreach (var eventElement in XDocument.Load(importedWeatherFile).Root?.Elements("WeatherEvent") ?? Enumerable.Empty<XElement>())
                if (!_weatherLibrary.Elements("WeatherEvent").Any(e => (string? )e.Attribute("Id") == (string? )eventElement.Attribute("Id")))
                    _weatherLibrary.Add(new XElement(eventElement));
        }
        catch
        { /* An invalid optional weather.xml must not prevent importing the world itself. */
        }
    }

    /// <summary>Restores weather, starting-profile and crate labels from the source mod's English records.</summary>
    private void ImportSourceNames(string file, bool isAbsolute)
    {
        var importedEnglishFile = Path.Combine(Path.GetDirectoryName(file) ?? string.Empty, "Language", "english.xml");
        if (!isAbsolute || !File.Exists(importedEnglishFile))
            return;
        try
        {
            var interfaceRecords = XDocument.Load(importedEnglishFile).Root?.Element("Interface")?.Elements("Record") ?? Enumerable.Empty<XElement>();
            var translations = interfaceRecords.Where(r => !string.IsNullOrWhiteSpace((string? )r.Element("Key"))).GroupBy(r => (string)r.Element("Key")!, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => (string? )g.First().Element("Value") ?? g.Key, StringComparer.Ordinal);
            foreach (var eventElement in _weatherLibrary.Elements("WeatherEvent"))
            {
                var weatherId = (string? )eventElement.Attribute("Id");
                var nameKey = (string? )eventElement.Element("Name")?.Attribute("Key");
                if (weatherId != null && nameKey != null && translations.TryGetValue(nameKey, out var translatedName))
                    _weatherNames[weatherId] = translatedName;
            }

            foreach (var condition in _customStartConditions)
            {
                var id = (string? )condition.Attribute("Id");
                var nameKey = (string? )condition.Element("Name")?.Attribute("Key");
                if (id != null && nameKey != null && translations.TryGetValue(nameKey, out var translatedName))
                    _startConditionNames[id] = translatedName;
            }

            foreach (var spawn in _customSpawnDefinitions)
            {
                var id = (string? )spawn.Attribute("Id");
                var nameKey = (string? )spawn.Element("DynamicThing")?.Element("Name")?.Attribute("Key");
                if (id != null && nameKey != null && translations.TryGetValue(nameKey, out var translatedName))
                    _spawnNames[id] = translatedName;
            }
            foreach (var nameElement in _customSpawnDefinitions.SelectMany(spawn => spawn.Descendants("Name")))
            {
                var key = (string?)nameElement.Attribute("Key");
                if (key != null && key.EndsWith("Name", StringComparison.Ordinal) && translations.TryGetValue(key, out var name))
                    _spawnNames[key.Substring(0, key.Length - 4)] = name;
            }
        }
        catch
        { /* Preserve the world even when a mod's optional localization file is malformed. */
        }
    }
}
