using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PeskyGizmo
{
    internal static class GamepadRumble
    {
        private static float _stopAt;
        private static int _lastGamepadId;
        private static string _lastName = "";
        private static IntPtr _hidHandle = new IntPtr(-1);
        private static ushort _hidPid;
        private static ushort _hidOutLen;
        private static byte _dsBtSeq;

        public static void Update()
        {
            if (_stopAt > 0f && Time.unscaledTime >= _stopAt)
            {
                _stopAt = 0f;
                Send(_lastGamepadId, _lastName, 0f, 0f);
            }
        }

        public static void SetVibration(int gamepadId, string name, float leftMotor, float rightMotor, float duration)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

            _lastGamepadId = gamepadId;
            _lastName = name ?? "";
            _stopAt = duration > 0f ? Time.unscaledTime + duration : 0f;
            Send(gamepadId, _lastName, Mathf.Clamp01(leftMotor), Mathf.Clamp01(rightMotor));
        }

        private static void Send(int gamepadId, string name, float left, float right)
        {
            ushort l16 = (ushort)(left * 65535f);
            ushort r16 = (ushort)(right * 65535f);

            bool isPlayStation = name.IndexOf("PS", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Dual", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Sony", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Wireless Controller", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isPlayStation)
            {
                var vib = new XInputVibration { LeftMotorSpeed = l16, RightMotorSpeed = r16 };
                try { XInputSetState((uint)Math.Clamp(gamepadId, 0, 3), ref vib); }
                catch { /* xinput1_4 unavailable */ }
                return;
            }

            SendPlayStationHid((byte)(left * 255f), (byte)(right * 255f));
        }

        private static void SendPlayStationHid(byte left, byte right)
        {
            if (_hidHandle == new IntPtr(-1) || _hidHandle == IntPtr.Zero)
            {
                OpenSonyHid();
                if (_hidHandle == new IntPtr(-1) || _hidHandle == IntPtr.Zero) return;
            }

            bool isDualSense = _hidPid == 0x0CE6 || _hidPid == 0x0DF2;
            bool isBt = _hidOutLen >= 78;
            byte[] buf;

            if (!isDualSense)
            {
                if (!isBt)
                {
                    buf = new byte[32];
                    buf[0] = 0x05;
                    buf[1] = 0x07;
                    buf[4] = right;
                    buf[5] = left;
                }
                else
                {
                    buf = new byte[78];
                    buf[0] = 0x11;
                    buf[1] = 0xC0;
                    buf[3] = 0x07;
                    buf[6] = right;
                    buf[7] = left;
                    WriteBtCrc(buf);
                }
            }
            else
            {
                if (!isBt)
                {
                    buf = new byte[48];
                    buf[0] = 0x02;
                    buf[1] = 0x03;
                    buf[2] = 0x15;
                    buf[3] = right;
                    buf[4] = left;
                }
                else
                {
                    buf = new byte[78];
                    buf[0] = 0x31;
                    buf[1] = (byte)(((_dsBtSeq++ & 0x0F) << 4) | 0x02);
                    buf[2] = 0x13;
                    buf[3] = 0x15;
                    buf[4] = right;
                    buf[5] = left;
                    WriteBtCrc(buf);
                }
            }

            if (!WriteFile(_hidHandle, buf, (uint)buf.Length, out _, IntPtr.Zero))
            {
                CloseHandle(_hidHandle);
                _hidHandle = new IntPtr(-1);
            }
        }

        private static void WriteBtCrc(byte[] buf)
        {
            uint crc = Crc32Step(0xFFFFFFFFu, 0xA2);
            for (int i = 0; i < 74; i++)
                crc = Crc32Step(crc, buf[i]);
            crc = ~crc;
            buf[74] = (byte)crc;
            buf[75] = (byte)(crc >> 8);
            buf[76] = (byte)(crc >> 16);
            buf[77] = (byte)(crc >> 24);
        }

        private static uint Crc32Step(uint crc, byte b)
        {
            crc ^= b;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            return crc;
        }

        private static void OpenSonyHid()
        {
            HidD_GetHidGuid(out Guid hidGuid);
            IntPtr devInfo = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, 0x12); // DIGCF_PRESENT | DIGCF_DEVICEINTERFACE
            if (devInfo == new IntPtr(-1)) return;

            try
            {
                var iface = new SpDeviceInterfaceData { cbSize = Marshal.SizeOf<SpDeviceInterfaceData>() };
                for (uint idx = 0; SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref hidGuid, idx, ref iface); idx++)
                {
                    SetupDiGetDeviceInterfaceDetail(devInfo, ref iface, IntPtr.Zero, 0, out uint reqSize, IntPtr.Zero);
                    if (reqSize == 0 || reqSize > 1024) continue;

                    IntPtr detail = Marshal.AllocHGlobal((int)reqSize);
                    try
                    {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        if (!SetupDiGetDeviceInterfaceDetail(devInfo, ref iface, detail, reqSize, out _, IntPtr.Zero))
                            continue;

                        string? path = Marshal.PtrToStringAuto(detail + 4);
                        if (string.IsNullOrEmpty(path)) continue;

                        IntPtr h = CreateFile(path, 0xC0000000u, 3u, IntPtr.Zero, 3u, 0u, IntPtr.Zero);
                        if (h == new IntPtr(-1) || h == IntPtr.Zero) continue;

                        var attr = new HidAttributes { Size = Marshal.SizeOf<HidAttributes>() };
                        if (HidD_GetAttributes(h, ref attr) && attr.VendorID == 0x054C &&
                            (attr.ProductID == 0x05C4 || attr.ProductID == 0x09CC || attr.ProductID == 0x0BA0 ||
                             attr.ProductID == 0x0CE6 || attr.ProductID == 0x0DF2))
                        {
                            ushort outLen = 32;
                            if (HidD_GetPreparsedData(h, out IntPtr preparsed))
                            {
                                if (HidP_GetCaps(preparsed, out HidCaps caps) >= 0 && caps.OutputReportByteLength > 0)
                                    outLen = caps.OutputReportByteLength;
                                HidD_FreePreparsedData(preparsed);
                            }
                            _hidHandle = h;
                            _hidPid = attr.ProductID;
                            _hidOutLen = outLen;
                            return;
                        }
                        CloseHandle(h);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(detail);
                    }
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(devInfo);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputVibration { public ushort LeftMotorSpeed; public ushort RightMotorSpeed; }

        [StructLayout(LayoutKind.Sequential)]
        private struct SpDeviceInterfaceData { public int cbSize; public Guid InterfaceClassGuid; public uint Flags; public IntPtr Reserved; }

        [StructLayout(LayoutKind.Sequential)]
        private struct HidAttributes { public int Size; public ushort VendorID; public ushort ProductID; public ushort VersionNumber; }

        [StructLayout(LayoutKind.Sequential)]
        private struct HidCaps
        {
            public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices;
            public ushort NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
        }

        [DllImport("xinput1_4.dll")]
        private static extern uint XInputSetState(uint dwUserIndex, ref XInputVibration pVibration);

        [DllImport("hid.dll")]
        private static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetAttributes(IntPtr handle, ref HidAttributes attributes);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetPreparsedData(IntPtr handle, out IntPtr preparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

        [DllImport("hid.dll")]
        private static extern int HidP_GetCaps(IntPtr preparsedData, out HidCaps capabilities);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, uint memberIndex, ref SpDeviceInterfaceData deviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SpDeviceInterfaceData deviceInterfaceData, IntPtr deviceInterfaceDetailData, uint deviceInterfaceDetailDataSize, out uint requiredSize, IntPtr deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
    }
}

