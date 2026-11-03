using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace WorldCreator
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "worldcreator.stationeers";
        public const string PluginName = "Stationeers World Creator";
        public const string PluginVersion = BuildInfo.Version;

        private Harmony? _harmony;

        private void Awake()
        {
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo("Loaded. The Create World button will appear on the main menu.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}

