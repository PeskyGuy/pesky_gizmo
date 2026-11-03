using HarmonyLib;
using UnityEngine;
using Verse;
using RimWorld;

namespace PeskyGizmo
{
    internal static class GizmoLog
    {
        public static void WarningOnce(string message, int key) => Log.WarningOnce("[Gizmo] " + message, key);
        public static void ErrorOnce(string message, int key) => Log.ErrorOnce("[Gizmo] " + message, key);
        public static void Message(string message) => Log.Message("[Gizmo] " + message);
    }

    public class GizmoMod : Mod
    {
        public static GizmoMod? Instance { get; private set; }
        public static GizmoSettings Settings => Instance!._settings;

        private readonly GizmoSettings _settings;

        public GizmoMod(ModContentPack content) : base(content)
        {
            Instance = this;
            _settings = GetSettings<GizmoSettings>();

            var harmony = new Harmony("Pesky.Gizmo");
            harmony.PatchAll();



            Log.Message("[Gizmo] Initialised.");
        }

        public override string SettingsCategory() => "Pesky's Gizmo";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label($"Camera Sensitivity: {Settings.cameraSensitivity:F0}");
            Settings.cameraSensitivity = listing.Slider(Settings.cameraSensitivity, 5f, 200f);

            listing.Label($"Cursor Sensitivity: {Settings.cursorSensitivity:F0} px/s");
            Settings.cursorSensitivity = listing.Slider(Settings.cursorSensitivity, 200f, 3000f);

            listing.Gap();
            listing.Label($"Stick Response Curve: {Settings.stickCurve:F1}");
            string curveLabel = Settings.stickCurve <= 1.3f ? "Linear"
                : Settings.stickCurve <= 2.0f ? "Curved" : "Aggressive";
            listing.Label($"  ({curveLabel})");
            Settings.stickCurve = listing.Slider(Settings.stickCurve, 1.0f, 3.0f);

            listing.Gap();
            listing.Label($"Inner Deadzone: {Settings.innerDeadzone:P0}");
            Settings.innerDeadzone = listing.Slider(Settings.innerDeadzone, 0.05f, 0.40f);

            listing.Gap();
            listing.CheckboxLabeled("Swap Left/Right Sticks", ref Settings.swapSticks);
            listing.CheckboxLabeled("Wrap Navigation at Edges", ref Settings.navWrapAround);
            listing.CheckboxLabeled("Haptic Feedback", ref Settings.hapticsEnabled);

            if (listing.ButtonText("Test Vibration"))
                HapticManager.Trigger(HapticTier.MediumRumble);

            listing.CheckboxLabeled("Show Debug Overlay", ref Settings.showDebugOverlay);

            listing.GapLine();
            listing.CheckboxLabeled("Use Nintendo Button Layout (Swap A/B and X/Y)", ref Settings.useNintendoLayout);

            listing.End();
        }
    }
}
