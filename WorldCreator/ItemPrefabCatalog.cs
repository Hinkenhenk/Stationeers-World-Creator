using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace WorldCreator.Mod
{
    internal static class ItemPrefabCatalog
    {
        internal sealed class Entry
        {
            internal readonly string Id;
            internal readonly string Name;
            internal Entry(string id, string name) { Id = id; Name = name; }
        }

        private static Entry[]? _entries;
        internal static IReadOnlyList<Entry> Entries
        {
            get
            {
                if (_entries != null) return _entries;
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WorldCreator.Mod.ItemCatalog.xml"))
                {
                    if (stream == null) return _entries = new Entry[0];
                    _entries = XDocument.Load(stream).Root?.Elements("Item")
                        .Select(item => new Entry((string?)item.Attribute("Id") ?? string.Empty, (string?)item.Attribute("Name") ?? string.Empty))
                        .Where(item => item.Id.Length > 0 && item.Name.Length > 0)
                        .OrderBy(item => item.Name, System.StringComparer.OrdinalIgnoreCase).ToArray() ?? new Entry[0];
                }
                return _entries;
            }
        }
    }
}
