using System;
using System.Collections.Generic;
using System.IO;

namespace WorldCreator
{
    internal static class ModSearchPaths
    {
        internal static string LocalMods(string? savePath) => Path.Combine(string.IsNullOrWhiteSpace(savePath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Stationeers")
            : savePath!, "mods");

        internal static IEnumerable<(string Path, bool Workshop)> Discover(string gameDataPath, string? savePath,
            IEnumerable<(string Path, bool Workshop)> installedMods)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in installedMods)
            {
                if (!string.IsNullOrWhiteSpace(mod.Path) && seen.Add(mod.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)))
                    yield return mod;
            }

            var local = LocalMods(savePath);
            if (seen.Add(local)) yield return (local, false);

            // Also find installed items that have not yet entered the game's mod list.
            var steamApps = new DirectoryInfo(gameDataPath).Parent?.Parent?.Parent;
            if (steamApps != null)
            {
                var workshop = Path.Combine(steamApps.FullName, "workshop", "content", "544550");
                if (seen.Add(workshop)) yield return (workshop, true);
            }
        }
    }
}
