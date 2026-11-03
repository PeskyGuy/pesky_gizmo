using Brutal.GlfwApi;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    [HarmonyPatch(typeof(UIRoot), nameof(UIRoot.UIRootOnGUI))]
    public static class Patch_UIRootOnGUI
    {
        public static void Postfix()
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

    [HarmonyPatch(typeof(CameraDriver), nameof(CameraDriver.Update))]
    public static class Patch_CameraDriver
    {
        public static bool Prefix() =>
            !ControllerState.ControllerModeEnabled || !ConsoleHUD.IsBlockingWindowOpen();

        public static void Postfix(CameraDriver __instance)
        {
            if (!ControllerState.ControllerModeEnabled || WorldRendererUtility.WorldSelected) return;
            if (ConsoleHUD.IsBlockingWindowOpen() && ConsoleHUD.ActivePanel != ConsolePanel.Architect) return;

            float cx = ControllerState.CameraAxisX;
            float cy = ControllerState.CameraAxisY;
            if (cx == 0f && cy == 0f) return;

            var traverse = Traverse.Create(__instance);
            Vector3 currentPos = traverse.Field("rootPos").GetValue<Vector3>();
            float speed = ControllerState.CameraSensitivity * (25f / __instance.CellSizePixels) * Time.unscaledDeltaTime;

            currentPos.x += cx * speed;
            currentPos.z -= cy * speed;
            traverse.Field("rootPos").SetValue(currentPos);
        }
    }

    [HarmonyPatch(typeof(WorldCameraDriver), nameof(WorldCameraDriver.Update))]
    public static class Patch_WorldCameraDriver_Update
    {
        public static void Prefix(WorldCameraDriver __instance)
        {
            if (!ControllerState.ControllerModeEnabled || !WorldRendererUtility.WorldSelected) return;
            if (Find.WindowStack != null && Find.WindowStack.WindowsPreventCameraMotion) return;

            float cx = ControllerState.CameraAxisX;
            float cy = ControllerState.CameraAxisY;
            if (cx != 0f || cy != 0f)
            {
                var trav = Traverse.Create(__instance);
                Vector2 raw = trav.Field("desiredRotationRaw").GetValue<Vector2>();
                float rotSpeed = ControllerState.CameraSensitivity * 0.6f * Prefs.MapDragSensitivity * 0.273f * Time.unscaledDeltaTime;
                raw.x += cx * rotSpeed;
                raw.y -= cy * rotSpeed;
                trav.Field("desiredRotationRaw").SetValue(raw);
            }

            float zoomInput = -ControllerState.CursorAxisY;
            if (Mathf.Abs(zoomInput) > 0.01f)
            {
                var trav = Traverse.Create(__instance);
                float desired = trav.Field("desiredAltitude").GetValue<float>();
                float zoomStep = (float)(__instance.config.zoomSpeed * __instance.TrueAltitude / 12.0) * 8f * Time.unscaledDeltaTime * Mathf.Abs(zoomInput);
                desired += zoomInput > 0f ? -zoomStep : zoomStep;
                trav.Field("desiredAltitude").SetValue(Mathf.Clamp(desired, WorldCameraDriver.MinAltitude, 1100f));
            }
        }
    }

    [HarmonyPatch(typeof(MainButtonsRoot), nameof(MainButtonsRoot.MainButtonsOnGUI))]
    public static class Patch_MainButtonsOnGUI
    {
        public static bool Prefix() => !ControllerState.ControllerModeEnabled;
    }

    [HarmonyPatch(typeof(MainButtonsRoot), nameof(MainButtonsRoot.HandleLowPriorityShortcuts))]
    public static class Patch_MainButtonsShortcuts
    {
        public static bool Prefix() => !ControllerState.ControllerModeEnabled;
    }

    [HarmonyPatch(typeof(MainTabWindow_Inspect), nameof(MainTabWindow_Inspect.DoWindowContents))]
    public static class Patch_MainTabWindow_Inspect_DoWindowContents
    {
        public static bool Prefix(MainTabWindow_Inspect __instance)
        {
            bool hide = ControllerState.ControllerModeEnabled;
            __instance.doWindowBackground = !hide;
            __instance.drawShadow = !hide;
            return !hide;
        }
    }

    [HarmonyPatch(typeof(MainTabWindow_Inspect), nameof(MainTabWindow_Inspect.ExtraOnGUI))]
    public static class Patch_MainTabWindow_Inspect_ExtraOnGUI
    {
        public static bool Prefix() => !ControllerState.ControllerModeEnabled;
    }

    [HarmonyPatch(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))]
    public static class Patch_SuppressVanillaOverlays
    {
        public static bool Prefix() => !ControllerState.ControllerModeEnabled;
    }

    [HarmonyPatch(typeof(ActiveTip), "DrawInner")]
    public static class Patch_ActiveTip_DrawInner
    {
        public static bool Prefix() =>
            !(ControllerState.ControllerModeEnabled && ReticleController.HoveredPawn != null);
    }

    [HarmonyPatch(typeof(ActiveTip), "TipRect", MethodType.Getter)]
    public static class Patch_ActiveTip_TipRect
    {
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

    [HarmonyPatch(typeof(Input), nameof(Input.GetKey), new[] { typeof(KeyCode) })]
    public static class Patch_InputQueue
    {
        public static void Postfix(KeyCode key, ref bool __result)
        {
            if (!__result && ControllerState.ControllerModeEnabled &&
                (key == KeyCode.LeftShift || key == KeyCode.RightShift) &&
                ControllerState.IsButtonDown((int)GlfwGamepadButton.LeftBumper))
            {
                __result = true;
            }
        }
    }
}

