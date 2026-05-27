using UnityEngine;
using Verse;

namespace PeskyCommandable
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

                // Block controller-mode re-entry while the user has just expressed mouse intent.
                if (value && Time.unscaledTime < MouseModeLockoutUntil) return;

                // Debounce only applies to controller-mode entry; exiting to mouse mode is always allowed.
                if (value && Time.unscaledTime < ModeSwitchCooldown) return;
                ModeSwitchCooldown = Time.unscaledTime + MODE_SWITCH_DEBOUNCE;

                _usingController = value;
                UnityEngine.Cursor.visible = !_usingController;
                if (!_usingController)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.None;
                    // Cancel zoom mode — its center-lock only runs in controller mode,
                    // otherwise the virtual cursor drifts off-center on next swap back.
                    ZoomMode.Exit();
                }
                Log.Message($"[Commandable] UsingController -> {value}");
            }
        }

        // When > Time.unscaledTime, InputMapper must not switch back to controller mode.
        // Set whenever the user gives explicit mouse input (movement / click).
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
        public static bool ButtonAEdge, ButtonBEdge, ButtonXEdge, ButtonYEdge;
        public static bool ButtonLBEdge, ButtonRBEdge;
        public static bool ButtonStartEdge, ButtonBackEdge;
        public static bool ButtonR3Edge, ButtonL3Edge;

        public static bool IsSimulatingClick;
        public static float SimulatedClickCooldown;
        public static bool IsSyncingMouse;

        // Debounce timer to prevent rapid mode switching
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
        private static bool _prevA, _prevB, _prevX, _prevY;
        private static bool _prevLB, _prevRB;
        private static bool _prevStart, _prevBack;
        private static bool _prevR3, _prevL3;

        public static float CameraSensitivity => CommandableMod.Settings?.cameraSensitivity ?? 50f;
        public static float CursorSensitivity => CommandableMod.Settings?.cursorSensitivity ?? 1200f;
        public static float AimAssistStrength => CommandableMod.Settings?.aimAssistStrength ?? 0.5f;
        public static float InnerDeadzone => CommandableMod.Settings?.innerDeadzone ?? 0.15f;

        public static void UpdateDpadEdges()
        {
            DpadUpEdge = DpadUp && !_prevUp;
            DpadDownEdge = DpadDown && !_prevDown;
            DpadLeftEdge = DpadLeft && !_prevLeft;
            DpadRightEdge = DpadRight && !_prevRight;

            _prevUp = DpadUp;
            _prevDown = DpadDown;
            _prevLeft = DpadLeft;
            _prevRight = DpadRight;

            if (CommandableMod.Settings != null)
            {
                bool aDown = IsButtonDown(CommandableMod.Settings.btnA);
                ButtonAEdge = aDown && !_prevA;
                _prevA = aDown;

                bool bDown = IsButtonDown(CommandableMod.Settings.btnB);
                ButtonBEdge = bDown && !_prevB;
                _prevB = bDown;

                bool xDown = IsButtonDown(CommandableMod.Settings.btnX);
                ButtonXEdge = xDown && !_prevX;
                _prevX = xDown;

                bool yDown = IsButtonDown(CommandableMod.Settings.btnY);
                ButtonYEdge = yDown && !_prevY;
                _prevY = yDown;

                bool lbDown = IsButtonDown(CommandableMod.Settings.btnLB);
                ButtonLBEdge = lbDown && !_prevLB;
                _prevLB = lbDown;

                bool rbDown = IsButtonDown(CommandableMod.Settings.btnRB);
                ButtonRBEdge = rbDown && !_prevRB;
                _prevRB = rbDown;

                bool startDown = IsButtonDown(CommandableMod.Settings.btnStart);
                ButtonStartEdge = startDown && !_prevStart;
                _prevStart = startDown;

                bool backDown = IsButtonDown(CommandableMod.Settings.btnBack);
                ButtonBackEdge = backDown && !_prevBack;
                _prevBack = backDown;

                bool r3Down = IsButtonDown(CommandableMod.Settings.btnR3);
                ButtonR3Edge = r3Down && !_prevR3;
                _prevR3 = r3Down;

                bool l3Down = IsButtonDown(CommandableMod.Settings.btnL3);
                ButtonL3Edge = l3Down && !_prevL3;
                _prevL3 = l3Down;
            }

            _prevSimLeft = SimLeftClick;
            _prevSimRight = SimRightClick;
        }

        public static bool IsButtonDown(int index)
        {
            if (index < 0) return false;
            return NativeInput.IsButtonDown(index);
        }
    }
}
