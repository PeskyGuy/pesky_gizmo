using System;
using Brutal.GlfwApi;

namespace PeskyGizmo
{
    public static unsafe class NativeInput
    {
        private static bool _initialized;
        private static int _gamepadId = -1;
        private static GlfwGamepadState _state;
        private static string _name = "None";

        public static bool Poll()
        {
            if (!_initialized)
            {
                _initialized = true;
                try
                {
                    // Add DualSense Edge (054c:0df2); standard PS3/PS4/PS5 mappings are built into glfw3.dll.
                    Glfw.UpdateGamepadMappings(
                        "030000004c050000f20d000000000000,PS5 DualSense Edge,a:b1,b:b2,back:b8,dpdown:h0.4,dpleft:h0.8,dpright:h0.2,dpup:h0.1,guide:b12,leftshoulder:b4,leftstick:b10,lefttrigger:a3,leftx:a0,lefty:a1,rightshoulder:b5,rightstick:b11,righttrigger:a4,rightx:a2,righty:a5,start:b9,x:b0,y:b3,platform:Windows,");
                }
                catch (Exception ex)
                {
                    Verse.Log.Warning($"[Gizmo] Failed to update GLFW gamepad mappings: {ex.Message}");
                }
            }

            GamepadRumble.Update();

            if (_gamepadId >= 0 && !Glfw.GetGamepadState(_gamepadId, out _state))
            {
                _gamepadId = -1;
                _name = "None";
            }

            if (_gamepadId < 0)
            {
                for (int i = 0; i <= Glfw.Constants.GLFW_JOYSTICK_LAST; i++)
                {
                    if (Glfw.JoystickPresent(i) && Glfw.JoystickIsGamepad(i) && Glfw.GetGamepadState(i, out _state))
                    {
                        _gamepadId = i;
                        _name = Glfw.GetGamepadName(i) ?? Glfw.GetJoystickName(i) ?? "Unknown Gamepad";
                        Verse.Log.Message($"[Gizmo] GLFW controller connected at index {i}: {_name}");
                        break;
                    }
                }
            }

            return _gamepadId >= 0;
        }

        private static float Axis(GlfwGamepadAxis axis)
        {
            if (_gamepadId < 0) return 0f;
            var s = _state;
            return s.Axes[(int)axis];
        }

        private static bool Btn(GlfwGamepadButton btn)
        {
            if (_gamepadId < 0) return false;
            var s = _state;
            return s.Buttons[(int)btn] != 0;
        }

        public static float RawX => Axis(GlfwGamepadAxis.LeftX);
        public static float RawY => Axis(GlfwGamepadAxis.LeftY);
        public static float RawZ => Axis(GlfwGamepadAxis.RightX);
        public static float RawR => Axis(GlfwGamepadAxis.RightY);
        public static float RawU => _gamepadId < 0 ? 0f : (Axis(GlfwGamepadAxis.LeftTrigger) + 1f) * 0.5f;
        public static float RawV => _gamepadId < 0 ? 0f : (Axis(GlfwGamepadAxis.RightTrigger) + 1f) * 0.5f;

        public static string ControllerName => _gamepadId < 0 ? "None" : _name;

        public static uint RawButtons
        {
            get
            {
                if (_gamepadId < 0) return 0;
                var s = _state;
                uint b = 0;
                for (int i = 0; i <= (int)GlfwGamepadButton.RightThumb; i++)
                {
                    if (s.Buttons[i] != 0)
                        b |= (1u << i);
                }
                return b;
            }
        }

        public static bool IsButtonDown(int index) => (RawButtons & (1u << index)) != 0;

        public static int RawPOV
        {
            get
            {
                if (_gamepadId < 0) return -1;
                bool up = Btn(GlfwGamepadButton.DpadUp);
                bool down = Btn(GlfwGamepadButton.DpadDown);
                bool left = Btn(GlfwGamepadButton.DpadLeft);
                bool right = Btn(GlfwGamepadButton.DpadRight);

                if (up && right) return 4500;
                if (down && right) return 13500;
                if (down && left) return 22500;
                if (up && left) return 31500;
                if (up) return 0;
                if (right) return 9000;
                if (down) return 18000;
                if (left) return 27000;
                return -1;
            }
        }

        public static void SetVibration(float leftMotor, float rightMotor, float duration)
        {
            if (_gamepadId >= 0)
                GamepadRumble.SetVibration(_gamepadId, _name, leftMotor, rightMotor, duration);
        }
    }
}
