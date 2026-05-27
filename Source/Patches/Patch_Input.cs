using HarmonyLib;
using UnityEngine;

namespace PeskyGizmo
{
    [HarmonyPatch(typeof(Input), "get_mousePosition")]
    public static class Patch_Input_Global
    {
        public static bool Bypassing = false;

        [HarmonyPrefix]
        public static bool Prefix(ref Vector3 __result)
        {
            if (ControllerState.ControllerModeEnabled && !Bypassing)
            {
                // Input.mousePosition is bottom-up (Y=0 at screen bottom)
                __result = new Vector3(
                    ControllerState.CursorScreenPos.x,
                    ControllerState.CursorScreenPos.y,
                    0f);
                return false;
            }
            return true;
        }
    }
}
