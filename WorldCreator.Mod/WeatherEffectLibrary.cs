using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WorldCreator.Mod
{
    internal sealed class SavedWeatherEffect
    {
        internal string Id { get; }
        internal string Name { get; }
        internal XElement Definition { get; }

        internal SavedWeatherEffect(string id, string name, XElement definition)
        {
            Id = id;
            Name = name;
            Definition = new XElement(definition);
        }
    }

    internal static class WeatherEffectLibrary
    {
        internal static string DirectoryPath => Environment.GetEnvironmentVariable("WORLD_CREATOR_WEATHER_LIBRARY") ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Stationeers", "WorldCreator", "WeatherLibrary");

        internal static IReadOnlyList<SavedWeatherEffect> Load()
        {
            var effects = new List<SavedWeatherEffect>();
            if (!System.IO.Directory.Exists(DirectoryPath)) return effects;
            foreach (var path in System.IO.Directory.GetFiles(DirectoryPath, "*.xml", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var root = XDocument.Load(path).Root;
                    var id = (string?)root?.Attribute("Id");
                    var name = (string?)root?.Attribute("Name");
                    var definition = root?.Element("WeatherEvent");
                    if (IsValidId(id) && !string.IsNullOrWhiteSpace(name) && definition != null)
                        effects.Add(new SavedWeatherEffect(id!, name!.Trim(), definition));
                }
                catch { /* Ignore malformed library entries and keep the rest available. */ }
            }
            return effects.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        internal static void Save(string id, string name, XElement definition)
        {
            if (!IsValidId(id)) throw new InvalidOperationException("Weather ID must begin with WCWeather_ and contain only letters, numbers, and underscores.");
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 80) throw new InvalidOperationException("Enter an effect name of up to 80 characters.");
            if (definition.Name.LocalName != "WeatherEvent") throw new InvalidOperationException("The saved definition is not a Stationeers WeatherEvent.");
            System.IO.Directory.CreateDirectory(DirectoryPath);
            var target = Path.Combine(DirectoryPath, id + ".xml");
            var temporary = target + ".tmp";
            new XDocument(new XElement("WeatherPreset", new XAttribute("Id", id), new XAttribute("Name", name.Trim()),
                new XElement(definition))).Save(temporary);
            if (File.Exists(target)) File.Replace(temporary, target, null);
            else File.Move(temporary, target);
        }

        internal static void Delete(string id)
        {
            if (!IsValidId(id)) throw new InvalidOperationException("Invalid saved weather ID.");
            var source = Path.Combine(DirectoryPath, id + ".xml");
            if (!File.Exists(source)) throw new FileNotFoundException("Saved weather effect was not found.", source);
            File.Delete(source);
        }

        internal static bool IsValidId(string? id) => id != null && Regex.IsMatch(id, @"^WCWeather_[A-Za-z0-9_]{1,40}$");
    }
}
