using HarmonyLib;
using RimWorld;

namespace PeskyGizmo
{
    [HarmonyPatch(typeof(MainButtonsRoot), "MainButtonsOnGUI")]
    public static class Patch_MainButtonsOnGUI
    {
        public static bool Prefix()
        {
            if (ControllerState.ControllerModeEnabled)
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(MainButtonsRoot), "HandleLowPriorityShortcuts")]
    public static class Patch_MainButtonsShortcuts
    {
        public static bool Prefix()
        {
            if (ControllerState.ControllerModeEnabled)
                return false;
            return true;
        }
    }
}
