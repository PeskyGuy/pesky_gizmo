using HarmonyLib;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    [HarmonyPatch(typeof(CameraDriver), "Update")]
    public static class Patch_CameraDriver
    {
        public static bool Prefix()
        {
            if (!ControllerState.ControllerModeEnabled) return true;
            if (ConsoleHUD.IsBlockingWindowOpen()) return false;
            return true;
        }

        public static void Postfix(CameraDriver __instance)
        {
            if (!ControllerState.ControllerModeEnabled) return;
            if (ConsoleHUD.IsBlockingWindowOpen() && ConsoleHUD.ActivePanel != ConsolePanel.Architect) return;

            float cx = ControllerState.CameraAxisX;
            float cy = ControllerState.CameraAxisY;
            if (cx == 0f && cy == 0f) return;

            var traverse = Traverse.Create(__instance);
            Vector3 currentPos = traverse.Field("rootPos").GetValue<Vector3>();

            float zoomFactor = 25f / __instance.CellSizePixels;
            float speed = ControllerState.CameraSensitivity * zoomFactor * Time.unscaledDeltaTime;

            currentPos.x += cx * speed;
            currentPos.z -= cy * speed;

            traverse.Field("rootPos").SetValue(currentPos);
        }
    }
}
