using Verse;

namespace PeskyGizmo
{
    public class GizmoSettings : ModSettings
    {
        public float cameraSensitivity = 50f;
        public float cursorSensitivity = 1000f;
        public float stickCurve = 2.0f;
        public float innerDeadzone = 0.15f;
        public bool swapSticks = false;
        public bool navWrapAround = true;
        public bool hapticsEnabled = true;
        public bool showDebugOverlay = false;
        public bool useNintendoLayout = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref cameraSensitivity, "cameraSensitivity", 50f);
            Scribe_Values.Look(ref cursorSensitivity, "cursorSensitivity", 1200f);
            Scribe_Values.Look(ref stickCurve, "stickCurve", 1.8f);
            Scribe_Values.Look(ref innerDeadzone, "innerDeadzone", 0.15f);
            Scribe_Values.Look(ref swapSticks, "swapSticks", false);
            Scribe_Values.Look(ref navWrapAround, "navWrapAround", true);
            Scribe_Values.Look(ref hapticsEnabled, "hapticsEnabled", true);
            Scribe_Values.Look(ref showDebugOverlay, "showDebugOverlay", false);
            Scribe_Values.Look(ref useNintendoLayout, "useNintendoLayout", false);
        }
    }
}
