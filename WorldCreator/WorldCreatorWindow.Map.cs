using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WorldCreator
{
    public sealed partial class WorldCreatorWindow
    {
        private readonly Dictionary<string, Vector2> _mapRegionScrolls = new Dictionary<string, Vector2>();
        private void DrawMapFieldCards(IReadOnlyList<WorldProject.Field> fields)
        {
            // Keep each world record together; identical Region #1 paths in different sets are unrelated.
            foreach (var record in fields.GroupBy(MapRecordPath))
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
                    GUILayout.Space(CardSpacing);
                    GUILayout.BeginVertical(_rowStyle!, GUILayout.ExpandWidth(true));
                    var groupId = group.FirstOrDefault(field => field.Label == group.Key + "/@Id")?.Value;
                    var groupTitle = group.Key == record.Key ? "General settings" : HumanizeXmlName(group.Key.Substring(record.Key.Length + 1));
                    GUILayout.Label(groupTitle + (string.IsNullOrEmpty(groupId) ? "" : " · " + groupId), _sectionStyle!);
                    var values = group.ToArray();
                    for (var i = 0; i < values.Length; i += 2)
                    {
                        GUILayout.BeginHorizontal();
                        DrawGroupedField(values[i], false);
                        if (i + 1 < values.Length) { GUILayout.Space(CardSpacing); DrawGroupedField(values[i + 1], false); }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndVertical();
                GUILayout.Space(CardSpacing);
            }
        }

        private static string MapRecordPath(WorldProject.Field field)
        {
            var segments = field.Label.Split('/');
            var depth = 0;
            while (depth < segments.Length && segments[depth] == "World") depth++;
            return string.Join("/", segments.Take(depth + 1));
        }
    }
}
