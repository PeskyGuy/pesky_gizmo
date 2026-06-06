using HarmonyLib;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    [HarmonyPatch(typeof(WorldCameraDriver), "Update")]
    public static class Patch_WorldCameraDriver_Update
    {
        public static void Prefix(WorldCameraDriver __instance)
        {
            if (!ControllerState.ControllerModeEnabled) return;
            if (!WorldRendererUtility.WorldSelected) return;
            if (Find.WindowStack != null && Find.WindowStack.WindowsPreventCameraMotion) return;

            float cx = ControllerState.CameraAxisX;
            float cy = ControllerState.CameraAxisY;

            if (cx != 0f || cy != 0f)
            {
                var trav = Traverse.Create(__instance);
                Vector2 raw = trav.Field("desiredRotationRaw").GetValue<Vector2>();

                // Scale roughly like vanilla mouse-drag rotation: TileSizeToRotationSpeed * MapDragSensitivity
                // applied per-frame. CameraSensitivity (default 50) acts as user-tunable multiplier.
                float rotSpeed = ControllerState.CameraSensitivity * 0.6f * Prefs.MapDragSensitivity * 0.273f * Time.unscaledDeltaTime;
                raw.x += cx * rotSpeed;
                raw.y -= cy * rotSpeed;

                trav.Field("desiredRotationRaw").SetValue(raw);
            }

            // Zoom mode (R3 toggle): right-stick Y drives world altitude continuously.
            // Stick up → zoom in (decrease altitude); stick down → zoom out.
            float zoomInput = -ControllerState.CursorAxisY;

            if (Mathf.Abs(zoomInput) > 0.01f)
            {
                var trav = Traverse.Create(__instance);
                float desired = trav.Field("desiredAltitude").GetValue<float>();
                float trueAlt = __instance.TrueAltitude;
                // Match vanilla PageKey zoom rate (2f) but continuous, scaled higher for controller.
                float zoomStep = (float)(__instance.config.zoomSpeed * trueAlt / 12.0) * 8f * Time.unscaledDeltaTime * Mathf.Abs(zoomInput);

                if (zoomInput > 0f)
                    desired -= zoomStep;
                else
                    desired += zoomStep;

                desired = Mathf.Clamp(desired, WorldCameraDriver.MinAltitude, 1100f);
                trav.Field("desiredAltitude").SetValue(desired);
            }
        }
    }
}
