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

            float lsX = GetAxis(s.axisLS_X);
            float lsY = GetAxis(s.axisLS_Y);
            float rsX = GetAxis(s.axisRS_X);
            float rsY = GetAxis(s.axisRS_Y);

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

            float axisL = Mathf.Clamp01(GetAxis(s.axisTR_L));
            float axisR = Mathf.Clamp01(GetAxis(s.axisTR_R));
            bool btnL = s.btnLT >= 0 && NativeInput.IsButtonDown(s.btnLT);
            bool btnR = s.btnRT >= 0 && NativeInput.IsButtonDown(s.btnRT);
            ControllerState.RadialTrigger = btnL ? 1f : axisL;
            ControllerState.CursorTrigger = btnR ? 1f : axisR;

            int pov = NativeInput.RawPOV;
            ControllerState.DpadUp    = pov >= 0 && (pov <= 4500 || pov >= 31500);
            ControllerState.DpadRight = pov >= 4500 && pov <= 13500;
            ControllerState.DpadDown  = pov >= 13500 && pov <= 22500;
            ControllerState.DpadLeft  = pov >= 22500 && pov <= 31500;
            ControllerState.UpdateDpadEdges();

            bool anyActiveInput = false;
            if (ControllerState.CameraAxisX != 0f || ControllerState.CameraAxisY != 0f ||
                ControllerState.CursorAxisX != 0f || ControllerState.CursorAxisY != 0f)
                anyActiveInput = true;

            if (ControllerState.DpadUp || ControllerState.DpadDown || ControllerState.DpadLeft || ControllerState.DpadRight)
                anyActiveInput = true;

            // Check for actual button presses, not just the fallback flag.
            if (NativeInput.RawButtons != 0)
                anyActiveInput = true;
            else if (NativeInput.UsingUnityFallback)
            {
                for (int i = 0; i < 20; i++)
                {
                    if (NativeInput.IsButtonDown(i))
                    {
                        anyActiveInput = true;
                        break;
                    }
                }
            }

            if (anyActiveInput && !ControllerState.UsingController)
            {
                // Capture hardware mouse position BEFORE switching to controller mode
                Patch_Input_Global.Bypassing = true;
                Vector2 hardwarePos = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
                Patch_Input_Global.Bypassing = false;

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

            ControllerState.CursorScreenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            MouseSimulation.SyncWindowsCursor();
        }


        private static float ApplyCurve(float axis, float exp)
        {
            float sign = Mathf.Sign(axis);
            float mag  = Mathf.Abs(axis);
            return sign * Mathf.Pow(mag, exp);
        }

        private static float GetAxis(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0f;
            return name switch
            {
                "X" => NativeInput.RawX,
                "Y" => NativeInput.RawY,
                "Z" => NativeInput.RawZ,
                "R" => NativeInput.RawR,
                "U" => NativeInput.RawU,
                "V" => NativeInput.RawV,
                _ => 0f
            };
        }

        private static float ApplyDeadzone(float val, float deadzone)
        {
            float abs = Mathf.Abs(val);
            if (abs < deadzone) return 0f;
            return Mathf.Sign(val) * ((abs - deadzone) / (1f - deadzone));
        }
    }
}
