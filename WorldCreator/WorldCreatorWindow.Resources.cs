using UnityEngine;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    /// <summary>Shows the resource-distribution summary before the editable regional settings.</summary>
    private void DrawResourceControls()
    {
        GUILayout.BeginVertical(_cardStyle!, GUILayout.ExpandWidth(true));
        GUILayout.Label("HOW RESOURCES ARE GENERATED", _sectionStyle!);
        GUILayout.Label("The cards below refer to resource profiles used by this world. A profile defines what can be generated; its conditions and linked map regions determine where it applies. Surface ore veins and deep-mining deposits use separate profiles.", _mutedStyle!);
        GUILayout.Space(10);
        GUILayout.Label("Ore density controls the number of vein-generation attempts for a surface profile: 1 keeps the baseline, 0 generates no veins from that profile, and 2 doubles the number of attempts. The game supports values from 0 to 10. This is not a percentage or a guarantee of twice as much usable ore, because placement still depends on terrain and other conditions.", _mutedStyle!);
        GUILayout.Space(10);
        GUILayout.Label("Profile IDs link to existing resource definitions. Changing an ID does not create a new ore mix. Deep-mining profiles control separate deposits and are not changed by the surface ore-density multiplier. Use Map Studio to inspect the region masks that place linked resource profiles.", _mutedStyle!);
        if (_project!.ResourceFields.Count == 0)
        {
            GUILayout.Space(10);
            GUILayout.Label("This world has no resource-profile attributes to edit here. Load a world with resource definitions to use it as a starting point.", _mutedStyle!);
        }
        GUILayout.EndVertical();
        GUILayout.Space(CardSpacing);
    }
}
