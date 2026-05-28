using Verse;

namespace PeskyGizmo
{
    public class GizmoSettings : ModSettings
    {
        public float cameraSensitivity = 50f;
        public float cursorSensitivity = 1200f;
        public float aimAssistStrength = 0.5f;
        public bool enableUIAimAssist = true;
        public float stickCurve = 1.8f;
        public float innerDeadzone = 0.15f;
        public bool swapSticks = false;
        public bool navWrapAround = true;
        public bool hapticsEnabled = true;
        public bool showDebugOverlay = false;
        public bool calibrationComplete = false;

        public string axisLS_X = "X";
        public string axisLS_Y = "Y";
        public string axisRS_X = "Z";
        public string axisRS_Y = "R";
        public string axisTR_L = "";
        public string axisTR_R = "";

        public int btnA = 0;
        public int btnB = 1;
        public int btnX = 2;
        public int btnY = 3;
        public int btnLB = 4;
        public int btnRB = 5;
        public int btnBack = 6;
        public int btnStart = 7;
        public int btnL3 = 8;
        public int btnR3 = 9;
        public int btnLT = -1;
        public int btnRT = -1;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref cameraSensitivity, "cameraSensitivity", 50f);
            Scribe_Values.Look(ref cursorSensitivity, "cursorSensitivity", 1200f);
            Scribe_Values.Look(ref aimAssistStrength, "aimAssistStrength", 0.5f);
            Scribe_Values.Look(ref enableUIAimAssist, "enableUIAimAssist", true);
            Scribe_Values.Look(ref stickCurve, "stickCurve", 1.8f);
            Scribe_Values.Look(ref innerDeadzone, "innerDeadzone", 0.15f);
            Scribe_Values.Look(ref swapSticks, "swapSticks", false);
            Scribe_Values.Look(ref navWrapAround, "navWrapAround", true);
            Scribe_Values.Look(ref hapticsEnabled, "hapticsEnabled", true);
            Scribe_Values.Look(ref showDebugOverlay, "showDebugOverlay", false);
            Scribe_Values.Look(ref calibrationComplete, "calibrationComplete", false);

            Scribe_Values.Look(ref axisLS_X, "axisLS_X", "X");
            Scribe_Values.Look(ref axisLS_Y, "axisLS_Y", "Y");
            Scribe_Values.Look(ref axisRS_X, "axisRS_X", "Z");
            Scribe_Values.Look(ref axisRS_Y, "axisRS_Y", "R");
            Scribe_Values.Look(ref axisTR_L, "axisTR_L", "");
            Scribe_Values.Look(ref axisTR_R, "axisTR_R", "");

            Scribe_Values.Look(ref btnA, "btnA", 0);
            Scribe_Values.Look(ref btnB, "btnB", 1);
            Scribe_Values.Look(ref btnX, "btnX", 2);
            Scribe_Values.Look(ref btnY, "btnY", 3);
            Scribe_Values.Look(ref btnLB, "btnLB", 4);
            Scribe_Values.Look(ref btnRB, "btnRB", 5);
            Scribe_Values.Look(ref btnBack, "btnBack", 6);
            Scribe_Values.Look(ref btnStart, "btnStart", 7);
            Scribe_Values.Look(ref btnL3, "btnL3", 8);
            Scribe_Values.Look(ref btnR3, "btnR3", 9);
            Scribe_Values.Look(ref btnLT, "btnLT", -1);
            Scribe_Values.Look(ref btnRT, "btnRT", -1);
        }
    }
}
