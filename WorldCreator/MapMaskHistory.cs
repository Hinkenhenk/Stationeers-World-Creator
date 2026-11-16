using System.Collections.Generic;
using System.Linq;

namespace WorldCreator;

/// <summary>Keeps a bounded stroke history without retaining every full-resolution mask indefinitely.</summary>
internal sealed class MapMaskHistory
{
    private readonly List<Entry> _entries = new List<Entry>();
    internal int Count => _entries.Count;
    internal readonly struct Entry
    {
        internal readonly string LayerId;
        internal readonly byte[] Png;
        internal readonly bool HadEdits;
        internal readonly (string RegionId, MapRgbValue Previous)? ColourChange;
        /// <summary>Retains one independent mask snapshot and its original edited-state flag.</summary>
        internal Entry(string layerId, byte[] png, bool hadEdits, (string RegionId, MapRgbValue Previous)? colourChange) { LayerId = layerId; Png = (byte[])png.Clone(); HadEdits = hadEdits; ColourChange = colourChange; }
    }
    /// <summary>Stores up to twenty changes, trimming old snapshots above a 64 MB history budget.</summary>
    internal void Push(string layerId, byte[] png, bool hadEdits, (string RegionId, MapRgbValue Previous)? colourChange = null)
    {
        _entries.Add(new Entry(layerId, png, hadEdits, colourChange));
        while (_entries.Count > 1 && (_entries.Count > 20 || _entries.Sum(entry => (long)entry.Png.Length) > 64 * 1024 * 1024))
            _entries.RemoveAt(0);
    }
    /// <summary>Leaves a snapshot available until its image has successfully been restored.</summary>
    internal Entry Peek() => _entries[_entries.Count - 1];
    /// <summary>Consumes the newest snapshot after a successful restoration.</summary>
    internal void Pop() => _entries.RemoveAt(_entries.Count - 1);
    /// <summary>Releases all history when a different world is loaded.</summary>
    internal void Clear() => _entries.Clear();
}
