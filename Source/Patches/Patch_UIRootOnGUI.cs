using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    [HarmonyPatch(typeof(UIRoot_Play), "UIRootOnGUI")]
    public static class Patch_UIRootPlayOnGUI
    {
        public static void Postfix() => Patch_UIRootOnGUI.SharedPostfix();
    }

    [HarmonyPatch(typeof(UIRoot_Entry), "UIRootOnGUI")]
    public static class Patch_UIRootEntryOnGUI
    {
        public static void Postfix() => Patch_UIRootOnGUI.SharedPostfix();
    }

    public static class Patch_UIRootOnGUI
    {
        public static void SharedPostfix()
        {
            if (LongEventHandler.ShouldWaitForEvent) return;
            if (!ControllerState.ControllerModeEnabled) return;

            UnityEngine.Cursor.visible = false;

            ConsoleHUD.Draw();
            ReticleDrawer.Draw(drawReticle: true);

            if (GizmoMod.Settings.showDebugOverlay)
                DebugOverlay.Draw();
        }
    }
}
