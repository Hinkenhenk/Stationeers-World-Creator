using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    internal readonly List<string> ExportWarnings = new List<string>();

    internal sealed class ExportEquipmentException : IOException
    {
        internal readonly XElement Item;
        internal readonly string ItemName;

        /// <summary>Names the exact crate item whose ID or quantity prevents exporting the starting equipment.</summary>
        internal ExportEquipmentException(XElement item, string crateName)
            : base("Starting conditions → Starting Equipment → " + crateName + " → " + EquipmentItemName(item)
                + ": check the prefab ID and Quantity. Quantity must be a finite number of zero or more; the game also supports fractional amounts.")
        {
            Item = item;
            ItemName = EquipmentItemName(item);
        }
    }

    /// <summary>Uses the familiar catalog name where possible while preserving unknown custom prefab IDs.</summary>
    private static string EquipmentItemName(XElement item)
    {
        var id = (string?)item.Attribute("Id");
        return ItemPrefabCatalog.Entries.FirstOrDefault(entry => entry.Id == id)?.Name ?? id ?? "Unnamed item";
    }

    internal sealed class ExportAssetException : IOException
    {
        internal readonly string FieldLabel;

        /// <summary>Associates a missing asset with its editable XML field so the player can find and correct it.</summary>
        internal ExportAssetException(string fieldLabel, string path)
            : base("Advanced → " + fieldLabel.Replace("World/World/", "").Replace("/@", " → ").Replace("/", " → ")
                + ": missing asset '" + path + "'. Select an available file in this field, or include the referenced file in the source mod.")
        {
            FieldLabel = fieldLabel;
        }
    }

    /// <summary>Uses a native banner when a source world's optional menu preview is missing, without discarding required assets.</summary>
    private void RepairMissingPreview(XElement world)
    {
        var preview = world.Element("PreviewButton")?.Attribute("Path");
        if (preview == null || string.IsNullOrWhiteSpace(preview.Value)) return;
        var path = preview.Value.Replace('\\', '/');
        if (File.Exists(ResolveImportedAsset(path)) || File.Exists(Path.Combine(_assets, path))) return;
        const string fallback = "Interface/preview_leo.png";
        if (!File.Exists(Path.Combine(_assets, fallback))) return;
        ExportWarnings.Add("Advanced → Preview Button → Path: the source preview '" + path + "' is missing. This export uses the installed game's default orbit preview instead.");
        preview.Value = fallback;
    }

    /// <summary>Finds the original editor path for an asset reference in the export copy.</summary>
    private string FindAssetField(XAttribute attribute, string path)
    {
        var field = AdvancedFields.FirstOrDefault(item => item.Target is XAttribute target && target.Name == attribute.Name
            && target.Parent?.Name == attribute.Parent?.Name && item.Value.Replace('\\', '/') == path);
        return field?.Label ?? "World/World/" + string.Join("/", attribute.Parent!.AncestorsAndSelf().Reverse().SkipWhile(element => element.Name != "World").Skip(1).Select(element => element.Name.LocalName)) + "/@Path";
    }
}
