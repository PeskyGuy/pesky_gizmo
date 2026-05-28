using System.Runtime.InteropServices;

namespace PeskyGizmo
{
    public static class NativeInput
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct JOYINFOEX
        {
            public uint dwSize;
            public uint dwFlags;
            public uint dwXpos;
            public uint dwYpos;
            public uint dwZpos;
            public uint dwRpos;
            public uint dwUpos;
            public uint dwVpos;
            public uint dwButtons;
            public uint dwButtonNumber;
            public uint dwPOV;
            public uint dwReserved1;
            public uint dwReserved2;
        }

        private const uint JOY_RETURNALL = 0x000000FF;
        private const int JOYSTICKID1 = 0;

        [DllImport("winmm.dll")]
        private static extern uint joyGetPosEx(int uJoyID, ref JOYINFOEX pji);

        private static JOYINFOEX _state;
        public static bool UsingUnityFallback { get; private set; }

        static NativeInput()
        {
            _state.dwSize = (uint)Marshal.SizeOf(typeof(JOYINFOEX));
            _state.dwFlags = JOY_RETURNALL;
        }

        public static bool Poll()
        {
            bool nativeSuccess = joyGetPosEx(JOYSTICKID1, ref _state) == 0;
            if (!UsingUnityFallback && !nativeSuccess)
            {
                if (HasUnityJoystick())
                {
                    UsingUnityFallback = true;
                    Verse.Log.WarningOnce("[Gizmo] Native input polling failed, falling back to Unity Input.", 992201);
                }
            }
            return nativeSuccess || (UsingUnityFallback && HasUnityJoystick());
        }

        private static bool HasUnityJoystick()
        {
            var names = UnityEngine.Input.GetJoystickNames();
            if (names.Length == 0) return false;
            for (int i = 0; i < names.Length; i++)
                if (!string.IsNullOrEmpty(names[i]))
                    return true;
            return false;
        }

        public static float RawX => UsingUnityFallback
            ? 0f
            : Normalize(_state.dwXpos);
        public static float RawY => UsingUnityFallback
            ? 0f
            : Normalize(_state.dwYpos);
        public static float RawZ => UsingUnityFallback
            ? 0f
            : Normalize(_state.dwZpos);
        public static float RawR => UsingUnityFallback
            ? 0f
            : Normalize(_state.dwRpos);
        public static float RawU => UsingUnityFallback
            ? 0f
            : Normalize(_state.dwUpos);
        public static float RawV => UsingUnityFallback
            ? 0f
            : Normalize(_state.dwVpos);

        public static uint RawButtons => UsingUnityFallback
            ? 0u
            : _state.dwButtons;

        public static bool IsButtonDown(int index)
        {
            if (UsingUnityFallback)
            {
                if (index < 0 || index >= 20)
                    return false;
                try
                {
                    return UnityEngine.Input.GetKey($"joystick button {index}");
                }
                catch (System.ArgumentException)
                {
                    return false;
                }
            }
            return (_state.dwButtons & (1u << index)) != 0;
        }

        public static int RawPOV
        {
            get
            {
                if (UsingUnityFallback)
                    return -1;
                return (_state.dwPOV > 35900) ? -1 : (int)_state.dwPOV;
            }
        }

        public static void SetVibration(float leftMotor, float rightMotor, float duration)
        {
        }

        private static float Normalize(uint val)
        {
            return (val - 32767f) / 32768f;
        }
    }
}
