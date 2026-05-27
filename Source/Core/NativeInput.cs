using System.Runtime.InteropServices;

namespace PeskyCommandable
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

        static NativeInput()
        {
            _state.dwSize = (uint)Marshal.SizeOf(typeof(JOYINFOEX));
            _state.dwFlags = JOY_RETURNALL;
        }

        public static bool Poll()
        {
            return joyGetPosEx(JOYSTICKID1, ref _state) == 0;
        }

        public static float RawX => Normalize(_state.dwXpos);
        public static float RawY => Normalize(_state.dwYpos);
        public static float RawZ => Normalize(_state.dwZpos);
        public static float RawR => Normalize(_state.dwRpos);
        public static float RawU => Normalize(_state.dwUpos);
        public static float RawV => Normalize(_state.dwVpos);

        public static uint RawButtons => _state.dwButtons;
        public static bool IsButtonDown(int index) => (_state.dwButtons & (1u << index)) != 0;

        public static int RawPOV => (_state.dwPOV > 35900) ? -1 : (int)_state.dwPOV;

        private static float Normalize(uint val)
        {
            return (val - 32767f) / 32768f;
        }
    }
}
