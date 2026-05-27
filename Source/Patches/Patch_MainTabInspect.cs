using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    [HarmonyPatch(typeof(MainTabWindow_Inspect), "DoWindowContents")]
    public static class Patch_MainTabWindow_Inspect_DoWindowContents
    {
        public static bool Prefix(MainTabWindow_Inspect __instance)
        {
            if (ControllerState.ControllerModeEnabled)
            {
                __instance.doWindowBackground = false;
                __instance.drawShadow = false;
                return false;
            }
            __instance.doWindowBackground = true;
            __instance.drawShadow = true;
            return true;
        }
    }

    [HarmonyPatch(typeof(MainTabWindow_Inspect), "ExtraOnGUI")]
    public static class Patch_MainTabWindow_Inspect_ExtraOnGUI
    {
        public static bool Prefix()
        {
            return !ControllerState.ControllerModeEnabled;
        }
    }
}
