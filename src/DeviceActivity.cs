using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace PowerUI
{
    internal sealed class DeviceActivity : IDisposable
    {
        private readonly object sync = new object();
        private readonly Dictionary<string, string> identities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<IntPtr, string> sources = new Dictionary<IntPtr, string>();
        private readonly Dictionary<string, long> lastInputs = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        internal readonly bool Registered;
        internal readonly string Error;
        internal int MouseInputs, KeyboardInputs;
        internal int Messages, HeaderErrors, UnmappedInputs;
        internal uint LastHeaderBytes, LastHeaderSize;

        internal DeviceActivity(IntPtr window)
        {
            var devices = new[] {
                new RawDevice { UsagePage = 1, Usage = 2, Flags = 0x2100, Target = window },
                new RawDevice { UsagePage = 1, Usage = 6, Flags = 0x2100, Target = window }
            };
            Registered = RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf(typeof(RawDevice)));
            if (!Registered) Error = "无法监听设备输入：" + new System.ComponentModel.Win32Exception().Message;
        }

        internal void UpdateDevices(List<HidDevice> devices)
        {
            lock (sync)
            {
                identities.Clear();
                foreach (var device in devices)
                {
                    bool keyboard = device.Capabilities.UsagePage == 1 && device.Capabilities.Usage == 6;
                    bool mouse = device.Capabilities.UsagePage == 1 && device.Capabilities.Usage == 2;
                    if ((keyboard || mouse) && device.ContainerId != null)
                        identities[CollectionPath(device.Path)] = device.ContainerId + (keyboard ? ":keyboard" : ":mouse");
                }
                sources.Clear();
                var present = new HashSet<string>(identities.Values, StringComparer.OrdinalIgnoreCase);
                foreach (string key in new List<string>(lastInputs.Keys))
                    if (!present.Contains(key)) lastInputs.Remove(key);
            }
        }

        internal bool CanQuery(HidDevice device, bool keyboard, int minutes)
        {
            if (!Registered || device.ContainerId == null) return false;
            lock (sync)
            {
                long last;
                return lastInputs.TryGetValue(device.ContainerId + (keyboard ? ":keyboard" : ":mouse"), out last)
                    && (Stopwatch.GetTimestamp() - last) / (double)Stopwatch.Frequency < minutes * 60;
            }
        }

        // Read keyboard headers only; mouse payloads are inspected solely to reject empty reports.
        internal int RecordInput(IntPtr input)
        {
            Messages++;
            uint size = (uint)Marshal.SizeOf(typeof(RawHeader));
            RawHeader header;
            uint bytes = GetRawInputHeader(input, 0x10000005, out header, ref size, size);
            LastHeaderBytes = bytes;
            LastHeaderSize = size;
            if (bytes != (uint)Marshal.SizeOf(typeof(RawHeader))) { HeaderErrors++; return -1; }
            if (header.Device == IntPtr.Zero || header.Type > 1) return -1;
            if (header.Type == 0)
            {
                MousePacket mouse;
                uint packetSize = (uint)Marshal.SizeOf(typeof(MousePacket));
                if (GetRawMouseInput(input, 0x10000003, out mouse, ref packetSize, size) != packetSize
                    || (mouse.Mouse.X == 0 && mouse.Mouse.Y == 0 && mouse.Mouse.ButtonFlags == 0)) return -1;
            }
            lock (sync)
            {
                string identity;
                if (!sources.TryGetValue(header.Device, out identity))
                {
                    uint length = 0;
                    if (GetRawInputDeviceInfo(header.Device, 0x20000007, null, ref length) == uint.MaxValue || length == 0) return -1;
                    var path = new StringBuilder((int)length);
                    if (GetRawInputDeviceInfo(header.Device, 0x20000007, path, ref length) == uint.MaxValue) return -1;
                    identities.TryGetValue(CollectionPath(path.ToString()), out identity);
                    sources[header.Device] = identity;
                }
                if (identity == null || !identity.EndsWith(header.Type == 1 ? ":keyboard" : ":mouse", StringComparison.Ordinal))
                { UnmappedInputs++; return -1; }
                lastInputs[identity] = Stopwatch.GetTimestamp();
                if (header.Type == 1) KeyboardInputs++; else MouseInputs++;
                return (int)header.Type;
            }
        }

        internal void ForgetInputs()
        {
            lock (sync) { sources.Clear(); lastInputs.Clear(); }
        }

        private static string CollectionPath(string path)
        {
            // Raw Input uses mouse/keyboard class GUIDs; SetupAPI uses the HID class GUID.
            int suffix = path.LastIndexOf("#{", StringComparison.Ordinal);
            Guid interfaceClass;
            return suffix >= 0 && Guid.TryParse(path.Substring(suffix + 1), out interfaceClass) ? path.Substring(0, suffix) : path;
        }

        public void Dispose()
        {
            if (!Registered) return;
            var devices = new[] {
                new RawDevice { UsagePage = 1, Usage = 2, Flags = 1 },
                new RawDevice { UsagePage = 1, Usage = 6, Flags = 1 }
            };
            RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf(typeof(RawDevice)));
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RawDevice { internal ushort UsagePage, Usage; internal uint Flags; internal IntPtr Target; }
        [StructLayout(LayoutKind.Sequential)]
        private struct RawHeader { internal uint Type, Size; internal IntPtr Device, WParam; }
        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct RawMouse
        {
            [FieldOffset(4)] internal ushort ButtonFlags;
            [FieldOffset(12)] internal int X;
            [FieldOffset(16)] internal int Y;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct MousePacket { internal RawHeader Header; internal RawMouse Mouse; }
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterRawInputDevices(RawDevice[] devices, uint count, uint size);
        [DllImport("user32.dll", EntryPoint = "GetRawInputData")]
        private static extern uint GetRawInputHeader(IntPtr input, uint command, out RawHeader data, ref uint size, uint headerSize);
        [DllImport("user32.dll", EntryPoint = "GetRawInputData")]
        private static extern uint GetRawMouseInput(IntPtr input, uint command, out MousePacket data, ref uint size, uint headerSize);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetRawInputDeviceInfoW")]
        private static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, StringBuilder data, ref uint size);
    }
}
