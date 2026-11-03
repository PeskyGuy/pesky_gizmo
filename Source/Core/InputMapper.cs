using RimWorld;
using Verse;
using UnityEngine;

namespace PeskyGizmo
{
    public static class InputMapper
    {
        public static void UpdateFrame()
        {
            if (!NativeInput.Poll()) return;

            var s = GizmoMod.Settings;
            float dz = ControllerState.InnerDeadzone;

            float lsX = NativeInput.RawX;
            float lsY = NativeInput.RawY;
            float rsX = NativeInput.RawZ;
            float rsY = NativeInput.RawR;

            if (s.swapSticks)
            {
                ControllerState.CameraAxisX = ApplyDeadzone(rsX, dz);
                ControllerState.CameraAxisY = ApplyDeadzone(rsY, dz);
                ControllerState.CursorAxisX = ApplyDeadzone(lsX, dz);
                ControllerState.CursorAxisY = ApplyDeadzone(lsY, dz);
            }
            else
            {
                ControllerState.CameraAxisX = ApplyDeadzone(lsX, dz);
                ControllerState.CameraAxisY = ApplyDeadzone(lsY, dz);
                ControllerState.CursorAxisX = ApplyDeadzone(rsX, dz);
                ControllerState.CursorAxisY = ApplyDeadzone(rsY, dz);
            }

            float axisL = Mathf.Clamp01(NativeInput.RawU);
            float axisR = Mathf.Clamp01(NativeInput.RawV);
            ControllerState.RadialTrigger = axisL;
            ControllerState.CursorTrigger = axisR;

            int pov = NativeInput.RawPOV;
            ControllerState.DpadUp    = pov >= 0 && (pov <= 4500 || pov >= 31500);
            ControllerState.DpadRight = pov >= 4500 && pov <= 13500;
            ControllerState.DpadDown  = pov >= 13500 && pov <= 22500;
            ControllerState.DpadLeft  = pov >= 22500 && pov <= 31500;

            if (ShouldEmulateDpadWithLeftStick())
            {
                float stickX = ControllerState.CameraAxisX;
                float stickY = ControllerState.CameraAxisY;
                const float threshold = 0.5f;
                if (stickY < -threshold) ControllerState.DpadUp = true;
                if (stickY > threshold) ControllerState.DpadDown = true;
                if (stickX < -threshold) ControllerState.DpadLeft = true;
                if (stickX > threshold) ControllerState.DpadRight = true;
            }

            ControllerState.UpdateDpadEdges();

            bool anyActiveInput = false;
            if (ControllerState.CameraAxisX != 0f || ControllerState.CameraAxisY != 0f ||
                ControllerState.CursorAxisX != 0f || ControllerState.CursorAxisY != 0f)
                anyActiveInput = true;

            if (ControllerState.DpadUp || ControllerState.DpadDown || ControllerState.DpadLeft || ControllerState.DpadRight)
                anyActiveInput = true;

            // Check for actual button presses.
            if (NativeInput.RawButtons != 0)
                anyActiveInput = true;

            if (anyActiveInput && !ControllerState.UsingController)
            {
                Vector2 hardwarePos = new Vector2(Input.mousePosition.x, Input.mousePosition.y);

                ControllerState.UsingController = true;
                ControllerDetector.ResetGracePeriod();
                ControllerState.CursorScreenPos = hardwarePos;
                ControllerState.LastMousePos = hardwarePos;
            }

            if (ControllerState.UsingController)
            {
                UpdateCursorPosition();
            }
        }

        private static void UpdateCursorPosition()
        {
            if (NavController.IsCustomUIActive || NavHelper.IsActive)
                return;

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if ((ControllerState.CursorScreenPos - center).sqrMagnitude > 0.5f)
            {
                ControllerState.CursorScreenPos = center;
                MouseSimulation.SyncWindowsCursor();
            }
        }


        private static float ApplyCurve(float axis, float exp)
        {
            float sign = Mathf.Sign(axis);
            float mag  = Mathf.Abs(axis);
            return sign * Mathf.Pow(mag, exp);
        }

        private static float ApplyDeadzone(float val, float deadzone)
        {
            float abs = Mathf.Abs(val);
            if (abs < deadzone) return 0f;
            return Mathf.Sign(val) * ((abs - deadzone) / (1f - deadzone));
        }

        private static bool ShouldEmulateDpadWithLeftStick()
        {
            if (Current.ProgramState != ProgramState.Playing)
                return true;

            if (Find.WindowStack != null && (Find.WindowStack.AnyWindowAbsorbingAllInput || NavHelper.Instance.IsOpen))
                return true;

            if ((ConsoleHUD.ActivePanel != ConsolePanel.None && ConsoleHUD.ActivePanel != ConsolePanel.Architect)
                || NavMenu.IsOpen
                || OverlayMenu.IsOpen)
            {
                return true;
            }

            return false;
        }
    }
}
