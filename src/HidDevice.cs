using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace PowerUI
{
    internal sealed class HidDevice
    {
        internal static int InterfaceCount, SupportedPathCount, InaccessibleCount;
        internal static int LastOpenError;
        internal string Path;
        internal string ContainerId;
        internal string Identity { get { return ContainerId ?? Path; } }
        internal string Product;
        internal ushort VendorId, ProductId, Version;
        internal HidNative.Caps Capabilities;
        internal string InputReportIds, OutputReportIds, FeatureReportIds;

        private static string GetReportIds(IntPtr data, int type, ushort valueCount, ushort buttonCount)
        {
            if (valueCount == 0 && buttonCount == 0) return "-";
            IntPtr values = Marshal.AllocHGlobal(Math.Max(valueCount, buttonCount) * 72);
            try
            {
                var ids = new SortedSet<byte>();
                if (valueCount != 0 && HidNative.HidP_GetValueCaps(type, values, ref valueCount, data) == 0x110000)
                    for (int i = 0; i < valueCount; i++) ids.Add(Marshal.ReadByte(values, i * 72 + 2));
                if (buttonCount != 0 && HidNative.HidP_GetButtonCaps(type, values, ref buttonCount, data) == 0x110000)
                    for (int i = 0; i < buttonCount; i++) ids.Add(Marshal.ReadByte(values, i * 72 + 2));
                return string.Join(",", ids);
            }
            finally { Marshal.FreeHGlobal(values); }
        }

        internal static List<HidDevice> EnumerateSupported()
        {
            InterfaceCount = SupportedPathCount = InaccessibleCount = LastOpenError = 0;
            var devices = new List<HidDevice>();
            Guid guid;
            HidNative.HidD_GetHidGuid(out guid);
            IntPtr set = HidNative.SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, 0x12);
            if (set == new IntPtr(-1)) throw new Win32Exception();
            try
            {
                for (uint index = 0; ; index++)
                {
                    var info = new HidNative.InterfaceData();
                    info.Size = Marshal.SizeOf(info);
                    if (!HidNative.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, index, ref info))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == 259) break;
                        throw new Win32Exception(error);
                    }
                    uint size;
                    InterfaceCount++;
                    HidNative.SetupDiGetDeviceInterfaceDetail(set, ref info, IntPtr.Zero, 0, out size, IntPtr.Zero);
                    if (size < 8) continue;
                    IntPtr detail = Marshal.AllocHGlobal((int)size);
                    try
                    {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        var deviceInfo = new HidNative.DeviceInfoData();
                        deviceInfo.Size = Marshal.SizeOf(deviceInfo);
                        if (!HidNative.SetupDiGetDeviceInterfaceDetail(set, ref info, detail, size, out size, ref deviceInfo))
                            throw new Win32Exception();
                        string path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
                        if (path.IndexOf("vid_24ae", StringComparison.OrdinalIgnoreCase) < 0
                            && path.IndexOf("vid_320f", StringComparison.OrdinalIgnoreCase) < 0
                            && path.IndexOf("vid_046d", StringComparison.OrdinalIgnoreCase) < 0
                            && path.IndexOf("vid&02046d", StringComparison.OrdinalIgnoreCase) < 0
                            && path.IndexOf("vid&0002046d", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        SupportedPathCount++;
                        using (var handle = HidNative.CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
                        {
                            if (handle.IsInvalid)
                            {
                                InaccessibleCount++;
                                LastOpenError = Marshal.GetLastWin32Error();
                                continue;
                            }
                            var attributes = new HidNative.Attributes();
                            attributes.Size = Marshal.SizeOf(attributes);
                            if (!HidNative.HidD_GetAttributes(handle, ref attributes)
                                || (attributes.VendorId != 0x24ae && attributes.VendorId != 0x046d
                                    && attributes.VendorId != 0x320f)) continue;
                            var device = new HidDevice();
                            device.Path = path;
                            var containerKey = new HidNative.PropertyKey { FormatId = new Guid("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c"), PropertyId = 2 };
                            uint propertyType, required;
                            var container = new byte[16];
                            if (HidNative.SetupDiGetDeviceProperty(set, ref deviceInfo, ref containerKey, out propertyType,
                                container, container.Length, out required, 0) && propertyType == 13 && required == 16)
                            {
                                var id = new Guid(container);
                                if (id != Guid.Empty) device.ContainerId = id.ToString();
                            }
                            device.VendorId = attributes.VendorId;
                            device.ProductId = attributes.ProductId;
                            device.Version = attributes.Version;
                            var product = new StringBuilder(256);
                            if (HidNative.HidD_GetProductString(handle, product, 512)) device.Product = product.ToString();
                            IntPtr preparsed;
                            if (HidNative.HidD_GetPreparsedData(handle, out preparsed))
                            {
                                try
                                {
                                    if (HidNative.HidP_GetCaps(preparsed, out device.Capabilities) != 0x110000)
                                        continue;
                                    device.InputReportIds = GetReportIds(preparsed, 0, device.Capabilities.InputValues, device.Capabilities.InputButtons);
                                    device.OutputReportIds = GetReportIds(preparsed, 1, device.Capabilities.OutputValues, device.Capabilities.OutputButtons);
                                    device.FeatureReportIds = GetReportIds(preparsed, 2, device.Capabilities.FeatureValues, device.Capabilities.FeatureButtons);
                                }
                                finally { HidNative.HidD_FreePreparsedData(preparsed); }
                            }
                            devices.Add(device);
                        }
                    }
                    finally { Marshal.FreeHGlobal(detail); }
                }
            }
            finally { HidNative.SetupDiDestroyDeviceInfoList(set); }
            return devices;
        }
    }

    internal static class HidNative
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct InterfaceData { internal int Size; internal Guid Guid; internal int Flags; internal UIntPtr Reserved; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct DeviceInfoData { internal int Size; internal Guid ClassGuid; internal uint DevInst; internal UIntPtr Reserved; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct PropertyKey { internal Guid FormatId; internal uint PropertyId; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct Attributes { internal int Size; internal ushort VendorId, ProductId, Version; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct Caps
        {
            internal ushort Usage, UsagePage, InputLength, OutputLength, FeatureLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] internal ushort[] Reserved;
            internal ushort LinkNodes, InputButtons, InputValues, InputIndices;
            internal ushort OutputButtons, OutputValues, OutputIndices;
            internal ushort FeatureButtons, FeatureValues, FeatureIndices;
        }
        [DllImport("hid.dll")] internal static extern void HidD_GetHidGuid(out Guid guid);
        [DllImport("hid.dll")][return: MarshalAs(UnmanagedType.U1)] internal static extern bool HidD_GetAttributes(SafeFileHandle handle, ref Attributes attributes);
        [DllImport("hid.dll", CharSet = CharSet.Unicode)][return: MarshalAs(UnmanagedType.U1)] internal static extern bool HidD_GetProductString(SafeFileHandle handle, StringBuilder buffer, int length);
        [DllImport("hid.dll")][return: MarshalAs(UnmanagedType.U1)] internal static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr data);
        [DllImport("hid.dll")][return: MarshalAs(UnmanagedType.U1)] internal static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll")] internal static extern int HidP_GetCaps(IntPtr data, out Caps caps);
        [DllImport("hid.dll")] internal static extern int HidP_GetValueCaps(int type, IntPtr values, ref ushort count, IntPtr data);
        [DllImport("hid.dll")] internal static extern int HidP_GetButtonCaps(int type, IntPtr values, ref ushort count, IntPtr data);
        [DllImport("hid.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.U1)] internal static extern bool HidD_GetFeature(SafeFileHandle handle, byte[] report, int length);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr SetupDiGetClassDevs(ref Guid guid, string enumerator, IntPtr parent, uint flags);
        [DllImport("setupapi.dll", SetLastError = true)]
        internal static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr device, ref Guid guid, uint index, ref InterfaceData data);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref InterfaceData data, IntPtr detail, uint size, out uint required, IntPtr device);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref InterfaceData data, IntPtr detail, uint size, out uint required, ref DeviceInfoData device);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool SetupDiGetDeviceProperty(IntPtr set, ref DeviceInfoData device, ref PropertyKey key, out uint type,
            byte[] buffer, int size, out uint required, uint flags);
        [DllImport("setupapi.dll", SetLastError = true)] internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    }
}
