using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private readonly Dictionary<string, Vector2> _mapRegionScrolls = new Dictionary<string, Vector2>();
    /// <summary>Groups world and region map values into separate cards.</summary>
    private void DrawMapFieldCards(IReadOnlyList<WorldProject.Field> fields)
    {
        // Keep each world record together; identical Region #1 paths in different sets are unrelated.
        var records = fields.GroupBy(MapRecordPath).ToArray();
        DrawCardColumns(records, record =>
        {
            DrawMapRecordCard(record);
            return false;
        }, minimumWidth: 560, maximumWidth: 620);
    }

    /// <summary>Edits one map record with compact region identifiers and a shared row for RGB channels.</summary>
    private void DrawMapRecordCard(IGrouping<string, WorldProject.Field> record)
    {
        var recordId = record.FirstOrDefault(field => field.Label == record.Key + "/@Id")?.Value;
        var title = HumanizeXmlName(record.Key.Substring(record.Key.LastIndexOf('/') + 1));
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label(title + (string.IsNullOrEmpty(recordId) ? "" : " · " + recordId), _sectionStyle!);
        if (record.Key.Contains("/RegionSet"))
            GUILayout.Label("This set defines one map mask. Each region below matches its own RGB color in that mask.", _mutedStyle!);
        foreach (var group in record.GroupBy(field =>
        {
            var path = field.Label.Split('/');
            var depth = record.Key.Split('/').Length;
            return path.Length > depth + 1 ? record.Key + "/" + path[depth] : record.Key;
        }))
        {
            DrawMapRegionFieldGroup(group, record.Key);
        }

        GUILayout.EndVertical();
    }

    /// <summary>Groups a region's short identifier above its three color channels without changing the XML fields.</summary>
    private void DrawMapRegionFieldGroup(IGrouping<string, WorldProject.Field> group, string recordPath)
    {
        GUILayout.Space(CardSpacing);
        var colorFields = new[] { "R", "G", "B" }
            .Select(channel => group.FirstOrDefault(field => field.Label == group.Key + "/@" + channel))
            .Where(field => field != null).Cast<WorldProject.Field>().ToArray();
        GUILayout.BeginVertical(_rowStyle!, GUILayout.ExpandWidth(true));
        var groupId = group.FirstOrDefault(field => field.Label == group.Key + "/@Id")?.Value;
        var groupTitle = group.Key == recordPath ? "General settings" : HumanizeXmlName(group.Key.Substring(recordPath.Length + 1));
        GUILayout.Label(groupTitle + (string.IsNullOrEmpty(groupId) ? "" : " · " + groupId), _sectionStyle!);
        foreach (var field in group.Except(colorFields))
        {
            if (colorFields.Length == 3 && field.Label == group.Key + "/@Id")
            {
                GUILayout.BeginVertical(GUILayout.Width(170));
                DrawGroupedField(field, false);
                GUILayout.EndVertical();
            }
            else
                DrawGroupedField(field, false);
        }
        if (colorFields.Length > 0)
        {
            GUILayout.BeginHorizontal();
            for (var channel = 0; channel < colorFields.Length; channel++)
            {
                if (channel > 0)
                    GUILayout.Space(10);
                DrawGroupedField(colorFields[channel], false);
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
    }

    /// <summary>Finds the common XML record path used to group related map fields.</summary>
    private static string MapRecordPath(WorldProject.Field field)
    {
        var segments = field.Label.Split('/');
        var depth = 0;
        while (depth < segments.Length && segments[depth] == "World")
            depth++;
        return string.Join("/", segments.Take(depth + 1));
    }
}
