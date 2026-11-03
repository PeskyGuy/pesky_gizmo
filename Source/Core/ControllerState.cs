using Brutal.GlfwApi;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    public enum GlyphSet { Xbox, PlayStation, Generic }

    public static class ControllerState
    {
        public static bool ControllerConnected;
        private static bool _usingController = false;
        public static bool UsingController
        {
            get => _usingController;
            set
            {
                if (_usingController == value) return;

                if (value && Time.unscaledTime < MouseModeLockoutUntil) return;

                if (value && Time.unscaledTime < ModeSwitchCooldown) return;
                ModeSwitchCooldown = Time.unscaledTime + MODE_SWITCH_DEBOUNCE;

                _usingController = value;
                UnityEngine.Cursor.visible = !_usingController;
                if (!_usingController)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.None;
                }
                if (GizmoMod.Settings.showDebugOverlay)
                    Log.Message($"[Gizmo] UsingController {_prevUsingController}→{value} at t={Time.unscaledTime:F2}");
                _prevUsingController = value;
            }
        }
        private static bool _prevUsingController;

        public static float MouseModeLockoutUntil;

        public static bool ControllerModeEnabled => ControllerConnected && UsingController && ModeOverride != false;
        public static bool? ModeOverride;
        public static GlyphSet CurrentGlyphSet = GlyphSet.Generic;

        public static Vector3 LastMousePos;
        public static bool AnyControllerInputThisFrame;

        public static float CameraAxisX;
        public static float CameraAxisY;
        public static float CursorAxisX;
        public static float CursorAxisY;

        public static Vector2 CursorScreenPos = new Vector2(Verse.UI.screenWidth / 2f, Verse.UI.screenHeight / 2f);

        public static float RadialTrigger;
        public static float CursorTrigger;

        public static bool DpadUp, DpadDown, DpadLeft, DpadRight;
        public static bool DpadUpEdge, DpadDownEdge, DpadLeftEdge, DpadRightEdge;
        public static bool DpadUpRepeat, DpadDownRepeat, DpadLeftRepeat, DpadRightRepeat;
        public static bool ButtonAEdge, ButtonBEdge, ButtonXEdge, ButtonYEdge;
        public static bool ButtonLBEdge, ButtonRBEdge;
        public static bool ButtonStartEdge, ButtonBackEdge;
        public static bool ButtonR3Edge, ButtonL3Edge;
        public static bool ButtonLTEdge, ButtonRTEdge;
        public static bool ButtonLTReleaseEdge;
        public static float ButtonLTHoldDuration;
        public static bool ModifierConsumed_LT;

        public static bool IsSimulatingClick;
        public static float SimulatedClickCooldown;
        public static bool IsSyncingMouse;

        public static float ModeSwitchCooldown;
        private const float MODE_SWITCH_DEBOUNCE = 0.5f;

        public static bool SimLeftClick;
        public static bool SimRightClick;
        private static bool _prevSimLeft;
        private static bool _prevSimRight;

        public static bool SimLeftClickDown => SimLeftClick && !_prevSimLeft;
        public static bool SimRightClickDown => SimRightClick && !_prevSimRight;
        public static bool SimLeftClickUp => !SimLeftClick && _prevSimLeft;
        public static bool SimRightClickUp => !SimRightClick && _prevSimRight;

        private static bool _prevUp, _prevDown, _prevLeft, _prevRight;
        private static float _upHoldTime, _downHoldTime, _leftHoldTime, _rightHoldTime;
        private const float DPAD_INITIAL_DELAY = 0.35f;
        private const float DPAD_REPEAT_INTERVAL = 0.08f;

        private static bool _prevA, _prevB, _prevX, _prevY;
        private static bool _prevLB, _prevRB;
        private static bool _prevStart, _prevBack;
        private static bool _prevR3, _prevL3;
        private static bool _prevLT, _prevRT;

        public static float ButtonBHeldDuration;
        private static bool _prevBHold;

        public static float CameraSensitivity => GizmoMod.Settings?.cameraSensitivity ?? 50f;
        public static float CursorSensitivity => GizmoMod.Settings?.cursorSensitivity ?? 1200f;
        
        public static float StickCurve => GizmoMod.Settings?.stickCurve ?? 1.8f;

        public static float InnerDeadzone => GizmoMod.Settings?.innerDeadzone ?? 0.15f;

        public static void UpdateDpadEdges()
        {
            DpadUpEdge = DpadUp && !_prevUp;
            DpadDownEdge = DpadDown && !_prevDown;
            DpadLeftEdge = DpadLeft && !_prevLeft;
            DpadRightEdge = DpadRight && !_prevRight;

            DpadUpRepeat = CheckRepeat(DpadUp, _prevUp, ref _upHoldTime);
            DpadDownRepeat = CheckRepeat(DpadDown, _prevDown, ref _downHoldTime);
            DpadLeftRepeat = CheckRepeat(DpadLeft, _prevLeft, ref _leftHoldTime);
            DpadRightRepeat = CheckRepeat(DpadRight, _prevRight, ref _rightHoldTime);

            _prevUp = DpadUp;
            _prevDown = DpadDown;
            _prevLeft = DpadLeft;
            _prevRight = DpadRight;

            if (GizmoMod.Settings != null)
            {
                int aIdx = GizmoMod.Settings.useNintendoLayout ? (int)GlfwGamepadButton.B : (int)GlfwGamepadButton.A;
                int bIdx = GizmoMod.Settings.useNintendoLayout ? (int)GlfwGamepadButton.A : (int)GlfwGamepadButton.B;
                int xIdx = GizmoMod.Settings.useNintendoLayout ? (int)GlfwGamepadButton.Y : (int)GlfwGamepadButton.X;
                int yIdx = GizmoMod.Settings.useNintendoLayout ? (int)GlfwGamepadButton.X : (int)GlfwGamepadButton.Y;

                bool aDown = IsButtonDown(aIdx);
                ButtonAEdge = aDown && !_prevA;
                _prevA = aDown;

                bool bDown = IsButtonDown(bIdx);
                ButtonBEdge = bDown && !_prevB;
                _prevB = bDown;

                if (bDown) ButtonBHeldDuration += Time.unscaledDeltaTime;
                else ButtonBHeldDuration = 0f;

                bool xDown = IsButtonDown(xIdx);
                ButtonXEdge = xDown && !_prevX;
                _prevX = xDown;

                bool yDown = IsButtonDown(yIdx);
                ButtonYEdge = yDown && !_prevY;
                _prevY = yDown;

                bool lbDown = IsButtonDown((int)GlfwGamepadButton.LeftBumper);
                ButtonLBEdge = lbDown && !_prevLB;
                _prevLB = lbDown;

                bool rbDown = IsButtonDown((int)GlfwGamepadButton.RightBumper);
                ButtonRBEdge = rbDown && !_prevRB;
                _prevRB = rbDown;

                bool startDown = IsButtonDown((int)GlfwGamepadButton.Start);
                ButtonStartEdge = startDown && !_prevStart;
                _prevStart = startDown;

                bool backDown = IsButtonDown((int)GlfwGamepadButton.Back);
                ButtonBackEdge = backDown && !_prevBack;
                _prevBack = backDown;

                bool r3Down = IsButtonDown((int)GlfwGamepadButton.RightThumb);
                ButtonR3Edge = r3Down && !_prevR3;
                _prevR3 = r3Down;

                bool l3Down = IsButtonDown((int)GlfwGamepadButton.LeftThumb);
                ButtonL3Edge = l3Down && !_prevL3;
                _prevL3 = l3Down;

                bool ltDown = RadialTrigger > 0.15f;
                ButtonLTEdge = ltDown && !_prevLT;
                if (ButtonLTEdge) ModifierConsumed_LT = false;
                if (ltDown) ButtonLTHoldDuration += Time.unscaledDeltaTime;
                else ButtonLTHoldDuration = 0f;
                ButtonLTReleaseEdge = !ltDown && _prevLT;
                _prevLT = ltDown;

                bool rtDown = CursorTrigger > 0.15f;
                ButtonRTEdge = rtDown && !_prevRT;
                _prevRT = rtDown;
            }

            _prevSimLeft = SimLeftClick;
            _prevSimRight = SimRightClick;
        }

        private static bool CheckRepeat(bool isDown, bool wasDown, ref float nextFireTime)
        {
            if (isDown && !wasDown)
            {
                nextFireTime = Time.unscaledTime + DPAD_INITIAL_DELAY;
                return true;
            }
            if (!isDown)
            {
                return false;
            }
            if (Time.unscaledTime >= nextFireTime)
            {
                nextFireTime = Time.unscaledTime + DPAD_REPEAT_INTERVAL;
                return true;
            }
            return false;
        }

        public static bool ButtonBHoldEdge
        {
            get
            {
                bool hold = ButtonBHeldDuration > 0.4f;
                bool edge = hold && !_prevBHold;
                _prevBHold = hold;
                return edge;
            }
        }

        public static bool IsButtonDown(int index)
        {
            if (index < 0) return false;
            return NativeInput.IsButtonDown(index);
        }
    }
}
