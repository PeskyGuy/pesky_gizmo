using HarmonyLib;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    /// <summary>
    /// Suppresses vanilla hover tooltips while the virtual cursor is over a pawn.
    /// Patching only DrawInner leaves a dark empty background rect (the "shadow") because
    /// the background is drawn before DrawInner using TipRect.  We zero TipRect too.
    /// </summary>
    [HarmonyPatch(typeof(ActiveTip), "DrawInner")]
    public static class Patch_ActiveTip_DrawInner
    {
        // Only suppress the tooltip when the virtual cursor is hovering over a pawn.
        // UI tooltips for buttons, gizmos, and buildings should still display normally.
        public static bool Prefix() =>
            !(ControllerState.ControllerModeEnabled && ReticleController.HoveredPawn != null);
    }

    [HarmonyPatch(typeof(ActiveTip), "TipRect", MethodType.Getter)]
    public static class Patch_ActiveTip_TipRect
    {
        // Zero-out the tip rect when suppressing, so the background box is never drawn.
        public static bool Prefix(ref Rect __result)
        {
            if (ControllerState.ControllerModeEnabled && ReticleController.HoveredPawn != null)
            {
                __result = Rect.zero;
                return false;
            }
            return true;
        }
    }
}
