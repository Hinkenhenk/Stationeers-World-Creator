using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WorldCreator;

internal sealed partial class WorldProject
{
    private string? _exportDirectory;
    internal string? ExportedWorldId { get; private set; }
    internal string? LastExportBackup { get; private set; }

    /// <summary>Recognizes an exported world without granting permission to replace unrelated or Workshop mods.</summary>
    private void RecognizeExportedWorld()
    {
        var id = (string?)_world.Attribute("Id");
        if (id == null || _sourceAssetRoot == null || !IsGeneratedWorld(_sourceAssetRoot, id)) return;
        ExportedWorldId = id;
        _exportDirectory = Path.GetFullPath(_sourceAssetRoot);
    }

    /// <summary>Offers updates only for this draft's loaded or previously exported local mod and unchanged ID.</summary>
    internal bool CanUpdateExport(string modsDirectory, string id)
    {
        if (!IsValidWorldId(id) || id != ExportedWorldId || _exportDirectory == null) return false;
        var target = Path.GetFullPath(Path.Combine(modsDirectory, "WorldCreator_" + id));
        return string.Equals(target.TrimEnd(Path.DirectorySeparatorChar), _exportDirectory.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(target);
    }

    /// <summary>Checks both the generated mod identity and its world definition before permitting replacement.</summary>
    private static bool IsGeneratedWorld(string directory, string id)
    {
        try
        {
            var metadata = XDocument.Load(Path.Combine(directory, "About", "About.xml")).Root;
            var world = XDocument.Load(Path.Combine(directory, "GameData", "world.xml")).Descendants("World").SingleOrDefault();
            return (string?)metadata?.Element("ModID") == "worldcreator.world." + id && (string?)world?.Attribute("Id") == id;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is System.Xml.XmlException || e is InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Preserves the Workshop handle, description and artwork when updating a generated local mod.</summary>
    private static void PreserveExportMetadata(string target, string temporary, string displayName)
    {
        CopyImportedDirectory(Path.Combine(target, "About"), Path.Combine(temporary, "About"));
        var metadata = XDocument.Load(Path.Combine(target, "About", "About.xml"));
        metadata.Root!.SetElementValue("Name", displayName.Trim());
        metadata.Save(Path.Combine(temporary, "About", "About.xml"));
    }

    /// <summary>Replaces a fully validated package while retaining the previous mod outside the scanned mods directory.</summary>
    private void PublishExport(string temporary, string target, bool updateExisting)
    {
        LastExportBackup = null;
        if (!updateExisting)
        {
            MoveExportDirectory(temporary, target);
            return;
        }
        if (!IsGeneratedWorld(target, ExportedWorldId!))
            throw new IOException("The existing mod changed since it was loaded. Reload it before updating.");
        var backupRoot = Path.Combine(Directory.GetParent(Path.GetDirectoryName(target)!)!.FullName, "WorldCreator", "ExportBackups");
        Directory.CreateDirectory(backupRoot);
        var backup = Path.Combine(backupRoot, Path.GetFileName(target) + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N"));
        MoveExportDirectory(target, backup);
        LastExportBackup = backup;
        try
        {
            MoveExportDirectory(temporary, target);
        }
        catch
        {
            MoveExportDirectory(backup, target);
            LastExportBackup = null;
            throw;
        }
        LastExportBackup = backup;
    }

    /// <summary>Allows brief Windows file-scanner locks to clear without retrying permanent errors or replacing an existing destination.</summary>
    internal static void MoveExportDirectory(string source, string destination)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                Directory.Move(source, destination);
                return;
            }
            catch (IOException error) when (attempt < 3 && ((error.HResult & 0xffff) == 5 || (error.HResult & 0xffff) == 32)
                && Directory.Exists(source) && !Directory.Exists(destination))
            {
                System.Threading.Thread.Sleep(75 << attempt);
            }
        }
    }
}
