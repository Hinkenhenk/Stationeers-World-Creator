using HarmonyLib;

namespace WorldCreator;

/// <summary>Keeps the game's Cancel action inside a visible creator overlay.</summary>
[HarmonyPatch(typeof(Assets.Scripts.GameManager), "HandleCancelAction")]
internal static class MenuCancelPatch
{
    /// <summary>Leaves all native cancellation behaviour intact when no creator overlay is open.</summary>
    private static bool Prefix() => !EditorWindow.TryCloseOverlay();
}
