using BepInEx;
using HarmonyLib;

namespace WorldCreator;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "worldcreator.stationeers";
    public const string PluginName = "Stationeers World Creator";
    public const string PluginVersion = BuildInfo.Version;
    private Harmony? _harmony;
    /// <summary>Registers the creator's Harmony patches when BepInEx loads the plugin.</summary>
    private void Awake()
    {
        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll(typeof(Plugin).Assembly);
        Logger.LogInfo("Loaded. The Create World button will appear on the main menu.");
    }

    /// <summary>Removes this plugin's Harmony patches when it is unloaded.</summary>
    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
        NativeMenuButtonAppearance.Current?.Dispose();
        NativeMenuButtonAppearance.Back?.Dispose();
        NativeMenuButtonAppearance.Load?.Dispose();
    }
}
