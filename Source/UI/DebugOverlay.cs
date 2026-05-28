using RimWorld;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    public static class DebugOverlay
    {
        private static float _lineY;

        public static void Draw()
        {
            _lineY = 10f;
            var s = GizmoMod.Settings;

            Line("=== Gizmo Debug ===");
            Line($"Controller: {(ControllerState.ControllerConnected ? "CONNECTED" : "---")}");
            Line($"Mode: {(ControllerState.ControllerModeEnabled ? "ACTIVE" : "inactive")}");
            Line($"Glyphs: {ControllerState.CurrentGlyphSet}");
            Line("---");

            if (!NativeInput.Poll())
            {
                Line("Native Input: FAILED");
                return;
            }

            string lsLabel = s.swapSticks ? "CURSOR" : "CAMERA";
            string rsLabel = s.swapSticks ? "CAMERA" : "CURSOR";
            Line($"LS ({lsLabel}): {ControllerState.CameraAxisX:F2} / {ControllerState.CameraAxisY:F2}");
            Line($"RS ({rsLabel}): {ControllerState.CursorAxisX:F2} / {ControllerState.CursorAxisY:F2}");
            Line($"LT: {ControllerState.RadialTrigger:F2}  RT: {ControllerState.CursorTrigger:F2}");
            Line($"Cursor: {ControllerState.CursorScreenPos.x:F0}, {ControllerState.CursorScreenPos.y:F0}");
            Line("---");

            string B(int idx) => idx >= 0 && NativeInput.IsButtonDown(idx) ? $"[{idx}]" : $" {idx} ";
            Line($"A:{B(s.btnA)}  B:{B(s.btnB)}  X:{B(s.btnX)}  Y:{B(s.btnY)}");
            Line($"LB:{B(s.btnLB)} RB:{B(s.btnRB)} LT:{B(s.btnLT)} RT:{B(s.btnRT)}");

            int pov = NativeInput.RawPOV;
            string dpad = pov < 0 ? "center" : $"{pov / 100}deg";
            Line($"D-pad: {dpad}  U:{ControllerState.DpadUp} D:{ControllerState.DpadDown} L:{ControllerState.DpadLeft} R:{ControllerState.DpadRight}");
            Line("---");
            if (Find.CurrentMap == null) return;

            Line($"Cell: {ReticleController.CurrentCell}");
            string hover = ReticleController.HoveredThing?.LabelCapNoCount ?? "---";
            Line($"Hover: {hover}");
            try
            {
                int sel = Find.Selector?.NumSelected ?? 0;
                Line($"Selected: {sel}");
            }
            catch { Line("Selected: ?"); }
        }

        private static void Line(string text)
        {
            Rect r = new Rect(10f, _lineY, 400f, 22f);
            Widgets.DrawBoxSolid(r, new Color(0f, 0f, 0f, 0.6f));
            Widgets.Label(r, $"  {text}");
            _lineY += 20f;
        }
    }
}
