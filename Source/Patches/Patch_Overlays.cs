using HarmonyLib;
using RimWorld;

namespace PeskyGizmo
{
    [HarmonyPatch(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))]
    public static class Patch_SuppressVanillaOverlays
    {
        public static bool Prefix()
        {
            return !ControllerState.ControllerModeEnabled;
        }
    }
}
