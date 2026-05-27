using RimWorld;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    public static class ControllerDetector
    {
        private static Map? _lastMap;
        private static int _pollCountdown = 10;
        private static float _skipMouseCheckUntil;

        public static bool IsInGracePeriod => Time.unscaledTime < _skipMouseCheckUntil;

        public static void ResetGracePeriod()
        {
            _skipMouseCheckUntil = Time.unscaledTime + 1.0f;
            Patch_Input_Global.Bypassing = true;
            try { ControllerState.LastMousePos = Input.mousePosition; }
            finally { Patch_Input_Global.Bypassing = false; }
        }

        public static string LastSafetyFailReason = "";

        public static bool IsMapSafeForInput()
        {
            try
            {
                if (Current.Game == null) { LastSafetyFailReason = "Game=null"; return false; }
                var map = Current.Game.CurrentMap;
                if (map == null) { LastSafetyFailReason = "CurrentMap=null"; return false; }
                if (map.thingGrid == null) { LastSafetyFailReason = "thingGrid=null"; return false; }
                if (RimWorld.Planet.WorldRendererUtility.WorldSelected) { LastSafetyFailReason = "WorldSelected"; return false; }
                var uiRoot = Find.UIRoot;
                if (!(uiRoot is UIRoot_Play play)) { LastSafetyFailReason = $"UIRoot not Play ({uiRoot?.GetType().Name ?? "null"})"; return false; }
                if (play.mapUI == null) { LastSafetyFailReason = "mapUI=null"; return false; }
                if (play.mapUI.selector == null) { LastSafetyFailReason = "selector=null"; return false; }
                if (Current.Camera == null) { LastSafetyFailReason = "Camera=null"; return false; }

                // If a non-immediate dialog (settings, info tabs, etc.) is on top, the player
                // is interacting with that — don't let the world reticle hover/select through it.
                var stack = Find.WindowStack;
                if (stack != null)
                {
                    for (int i = 0; i < stack.Count; i++)
                    {
                        var w = stack[i];
                        if (w == null || w is ImmediateWindow || w.IsDebug) continue;
                        if (w.layer == WindowLayer.Dialog || w.layer == WindowLayer.Super)
                        {
                            LastSafetyFailReason = $"DialogOpen({w.GetType().Name})";
                            return false;
                        }
                    }
                }

                LastSafetyFailReason = "";
                return true;
            }
            catch (System.Exception ex)
            {
                LastSafetyFailReason = $"Exception: {ex.GetType().Name}";
                return false;
            }
        }

        public static void Tick()
        {
            Map? currentMap = null;
            try { currentMap = Current.Game?.CurrentMap; } catch { }

            if (currentMap != _lastMap)
            {
                _lastMap = currentMap;
                Patch_Input_Global.Bypassing = true;
                try { ControllerState.LastMousePos = Input.mousePosition; }
                finally { Patch_Input_Global.Bypassing = false; }
                _skipMouseCheckUntil = Time.unscaledTime + 2.0f;
                Log.Message("[Gizmo] Map changed, resetting mouse baseline and entering grace period.");
            }

            if (--_pollCountdown > 0) return;
            _pollCountdown = 120;

            bool wasConnected = ControllerState.ControllerConnected;
            ControllerState.ControllerConnected = NativeInput.Poll();

            if (ControllerState.ControllerConnected && !wasConnected)
            {
                Log.Message("[Gizmo] Controller connected.");
                DetectGlyphSet();
            }
            else if (!ControllerState.ControllerConnected && wasConnected)
            {
                Log.Message("[Gizmo] Controller disconnected.");
            }
        }

        private static void DetectGlyphSet()
        {
            string[] names = Input.GetJoystickNames();
            if (names.Length == 0)
            {
                ControllerState.CurrentGlyphSet = GlyphSet.Generic;
                return;
            }

            string name = (names[0] ?? "").ToLowerInvariant();

            if (name.Contains("xbox") || name.Contains("xinput"))
                ControllerState.CurrentGlyphSet = GlyphSet.Xbox;
            else if (name.Contains("dualsense") || name.Contains("dualshock") || name.Contains("playstation") || name.Contains("sony"))
                ControllerState.CurrentGlyphSet = GlyphSet.PlayStation;
            else
                ControllerState.CurrentGlyphSet = GlyphSet.Generic;

            Log.Message($"[Gizmo] Glyph set: {ControllerState.CurrentGlyphSet} (device: \"{names[0]}\")");
        }
    }
}
