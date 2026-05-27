using HarmonyLib;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    [HarmonyPatch(typeof(Input), "GetKey", new[] { typeof(KeyCode) })]
    public static class Patch_InputQueue
    {
        public static void Postfix(KeyCode key, ref bool __result)
        {
            if (__result) return; // Already true, no need to override

            if (!ControllerState.ControllerModeEnabled) return;

            // Map LB to LeftShift for queuing orders
            if (key == KeyCode.LeftShift || key == KeyCode.RightShift)
            {
                if (ControllerState.IsButtonDown(GizmoMod.Settings.btnLB))
                {
                    __result = true;
                }
            }
        }
    }
}
