using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Verse;

namespace PeskyGizmo
{
    public static class MouseSimulation
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct INPUT_UNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
        }

        public struct INPUT
        {
            public uint type;
            public INPUT_UNION u;
        }

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        public static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        private const uint INPUT_MOUSE = 0;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

        public static void SyncWindowsCursor()
        {
            IntPtr hwnd = GetActiveWindow();
            if (hwnd == IntPtr.Zero) return;

            POINT pt = new POINT
            {
                X = Mathf.RoundToInt(ControllerState.CursorScreenPos.x),
                Y = Mathf.RoundToInt(Screen.height - ControllerState.CursorScreenPos.y)
            };

            ClientToScreen(hwnd, ref pt);
            SetCursorPos(pt.X, pt.Y);

            ControllerState.IsSyncingMouse = true;
            ControllerDetector.HoldMouseSync(0.15f);
        }


        public static void SendLeftDown()
        {
            SyncWindowsCursor();
            SendMouseInput(MOUSEEVENTF_LEFTDOWN);
        }

        public static void SendLeftUp()
        {
            SyncWindowsCursor();
            SendMouseInput(MOUSEEVENTF_LEFTUP);
        }

        public static void SendRightDown()
        {
            SyncWindowsCursor();
            SendMouseInput(MOUSEEVENTF_RIGHTDOWN);
        }

        public static void SendRightUp()
        {
            SyncWindowsCursor();
            SendMouseInput(MOUSEEVENTF_RIGHTUP);
        }

        private static void SendMouseInput(uint flags)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi.dwFlags = flags;
            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
    }
}
