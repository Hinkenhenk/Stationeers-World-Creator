using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Items;
using Assets.Scripts.Objects.Clothing;
using UnityEngine;

namespace WorldCreator;

internal static class EquipmentPrefabRules
{
    private static Dictionary<string, Thing>? _prefabs;
    private static readonly Dictionary<Slot, Slot> _assetSlotRules = new Dictionary<Slot, Slot>();
    /// <summary>Reads prefab assets without spawning objects or requiring a running world.</summary>
    internal static IReadOnlyDictionary<string, Thing> Prefabs
    {
        get
        {
            if (_prefabs != null && _prefabs.Count > 0)
                return _prefabs;
            var sources = Prefab.AllPrefabs.AsEnumerable();
            if (WorldManager.Instance != null)
                sources = sources.Concat(WorldManager.Instance.SourcePrefabs);
            sources = sources.Concat(Resources.FindObjectsOfTypeAll<Thing>());
            return _prefabs = sources.Where(p => p != null && !string.IsNullOrEmpty(p.PrefabName)).GroupBy(p => p.PrefabName, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        }
    }

    /// <summary>Finds the installed prefab defining the selected container's slots.</summary>
    internal static Thing? Find(string id) => Prefabs.TryGetValue(id, out var prefab) ? prefab : null;
    /// <summary>Checks native type, specific-prefab restrictions and the item's own insertion rules.</summary>
    internal static bool Accepts(Thing parent, Slot slot, Thing prefab)
    {
        if (parent is SuitBase suit && slot == suit.BackSlot)
        {
            if (prefab is Backpack backpack && !suit.IsAllowedBackpackPrefab(backpack))
                return false;
            if (prefab is WearableItem wearable && !suit.AllowedInBack(wearable))
                return false;
        }

        slot = RuleSlot(parent, slot);
        if (!(prefab is DynamicThing item) || slot.IsLocked)
            return false;
        return slot.IsAllowedType(item) && slot.TakesSpecificItem(item.PrefabHash) && item.CanEnter(slot);
    }

    /// <summary>Supplies parent context for uninitialized assets without writing to the game's prefab slots.</summary>
    private static Slot RuleSlot(Thing parent, Slot slot)
    {
        if (slot.Parent != null)
            return slot;
        if (_assetSlotRules.TryGetValue(slot, out var rules))
            return rules;
        rules = new Slot
        {
            Parent = parent,
            Type = slot.Type,
            SpecificTypePrefabHashes = slot.SpecificTypePrefabHashes,
            AllowDragging = slot.AllowDragging,
            IsLocked = slot.IsLocked,
            StringKey = slot.StringKey,
            StringHash = slot.StringHash,
            Size = slot.Size
        };
        _assetSlotRules.Add(slot, rules);
        return rules;
    }

    /// <summary>Returns the native stack limit, or one for equipment that cannot stack.</summary>
    internal static int MaximumQuantity(Thing prefab) => prefab is Stackable stack ? Math.Max(1, stack.MaxQuantity) : 1;
    /// <summary>Resolves existing XML selectors in execution order without modifying prefab occupancy.</summary>
    internal static List<(XElement Item, int Slot)> Assign(Thing parent, IReadOnlyList<XElement> contents)
    {
        var slots = parent.Slots ?? new List<Slot>();
        var result = new List<(XElement Item, int Slot)>();
        var used = new HashSet<int>();
        foreach (var item in contents)
        {
            var id = (string? )item.Attribute("Id") ?? "";
            var prefab = Find(id) ?? throw new InvalidOperationException("Cannot verify slots until prefab '" + id + "' is available.");
            var explicitIndex = (string? )item.Attribute("SlotIndex");
            var key = (string? )item.Attribute("SlotId");
            var type = (string? )item.Attribute("SlotClass");
            if (type == "None")
                type = null;
            int index;
            if (explicitIndex != null && !int.TryParse(explicitIndex, out _))
                throw new InvalidOperationException("Invalid loaded slot index for " + id + ". Remove that item first.");
            if (explicitIndex != null && int.Parse(explicitIndex) >= 0)
            {
                if (!int.TryParse(explicitIndex, out index) || index < 0 || index >= slots.Count)
                    throw new InvalidOperationException("Invalid loaded slot index for " + id + ". Remove that item first.");
            }
            else
                index = Enumerable.Range(0, slots.Count).Where(i => !used.Contains(i) && (key != null ? slots[i].StringKey == key : type != null ? slots[i].Type.ToString() == type : RuleSlot(parent, slots[i]).IsAllowedType(prefab as DynamicThing))).DefaultIfEmpty(-1).First();
            if (index < 0 || !used.Add(index))
                throw new InvalidOperationException("Loaded contents conflict with this container's slots. Remove an item before adding another.");
            result.Add((item, index));
        }

        return result;
    }
}
