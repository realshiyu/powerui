using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace PowerUI
{
    internal sealed class BatteryReading
    {
        internal int Percent;
        internal bool Charging;
        internal DateTime ReadAtUtc;
        internal ushort ReceiverPid;
        internal ushort ReceiverVid;
        internal byte DeviceIndex;
        internal string DeviceName;
    }

    internal static class RapooBattery
    {
        internal static int Reports, BatteryReports, LastStatus = -1, LastBattery = -1;
        internal static int FirmwareAck = -1, BatteryAck = -1;
        // Official VT_nrf54L mapping; never send mouse reports to the P5 PLUS base (4827).
        internal static bool IsSupported(HidDevice device)
        {
            return device.VendorId == 0x24ae && (device.ProductId == 0x1417 || device.ProductId == 0x1421)
                && device.Capabilities.UsagePage == 0xff00 && device.Capabilities.Usage == 0x000e
                && device.OutputReportIds == "6" && device.Capabilities.OutputLength == 33;
        }

        internal static BatteryReading Read(int timeoutMilliseconds, bool request)
        {
            Reports = BatteryReports = 0;
            LastStatus = LastBattery = -1;
            FirmwareAck = BatteryAck = -1;
            var all = HidDevice.EnumerateSupported();
            var devices = all.Where(IsSupported).ToList();
            if (devices.Count == 0) throw new IOException("No supported Rapoo mouse receiver interface is accessible.");
            if (devices.Count != 1) throw new IOException("Multiple supported receivers found; refusing ambiguous selection.");
            var device = devices[0];
            var inputs = all.Where(d => d.ProductId == device.ProductId && d.Capabilities.UsagePage == 0xff00
                && d.Capabilities.Usage == 2 && d.InputReportIds == "7" && d.Capabilities.InputLength >= 9).ToList();
            if (inputs.Count != 1) throw new IOException("Cannot unambiguously select battery input report 7 interface.");
            var input = inputs[0];
            using (var handle = HidNative.CreateFile(input.Path, 0x80000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero))
            {
                if (handle.IsInvalid) throw new System.ComponentModel.Win32Exception();
                using (var stream = new FileStream(handle, FileAccess.Read, input.Capabilities.InputLength, true))
                {
                    var task = ReadBattery(stream, input.Capabilities.InputLength);
                    try
                    {
                        if (request)
                        {
                            var features = all.Where(d => d.ProductId == device.ProductId && d.Capabilities.UsagePage == 0xff00
                                && d.Capabilities.Usage == 0x000f && d.FeatureReportIds == "8" && d.Capabilities.FeatureLength == 33).ToList();
                            if (features.Count != 1) throw new IOException("Cannot unambiguously select feature report 8 interface.");
                            FirmwareAck = SendQuery(device, features[0], 0xa3, 0);
                            BatteryAck = SendQuery(device, features[0], 0xaa);
                        }
                        if (task.Wait(timeoutMilliseconds))
                        {
                            var reading = task.GetAwaiter().GetResult();
                            reading.ReceiverPid = device.ProductId;
                            reading.ReceiverVid = device.VendorId;
                            return reading;
                        }
                        return null;
                    }
                    finally
                    {
                        stream.Dispose();
                        task.ContinueWith(t => { var observed = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                    }
                }
            }
        }

        private static int SendQuery(HidDevice device, HidDevice feature, byte command, params byte[] arguments)
        {
            using (var handle = HidNative.CreateFile(device.Path, 0x40000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero))
            {
                if (handle.IsInvalid) throw new System.ComponentModel.Win32Exception();
                using (var stream = new FileStream(handle, FileAccess.Write, 33, true))
                {
                    // Only official read-firmware/read-battery queries are used by this class.
                    var report = new byte[33];
                    report[0] = 6;
                    report[1] = 0xa5;
                    report[2] = command;
                    Array.Copy(arguments, 0, report, 3, arguments.Length);
                    var write = stream.WriteAsync(report, 0, report.Length);
                    if (!write.Wait(1500))
                    {
                        stream.Dispose();
                        write.ContinueWith(t => { var observed = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                        throw new IOException("Battery query write timed out.");
                    }
                    write.GetAwaiter().GetResult();
                }
            }
            Thread.Sleep(10);
            using (var handle = HidNative.CreateFile(feature.Path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
            {
                if (handle.IsInvalid) throw new System.ComponentModel.Win32Exception();
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    var response = new byte[33];
                    response[0] = 8;
                    if (!HidNative.HidD_GetFeature(handle, response, response.Length)) throw new System.ComponentModel.Win32Exception();
                    if (response[1] != 2) return response[1];
                    Thread.Sleep(100);
                }
            }
            return 2;
        }

        private static async Task<BatteryReading> ReadBattery(FileStream stream, int reportLength)
        {
            var buffer = new byte[reportLength];
            while (true)
            {
                int length = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                if (length == 0) throw new IOException("Receiver stopped returning reports.");
                Reports++;
                if (length >= 9 && buffer[0] == 7)
                {
                    BatteryReports++;
                    LastStatus = buffer[7];
                    LastBattery = buffer[8];
                }
                // Native HID includes the report ID; WebHID event.data does not.
                if (length < 9 || buffer[0] != 7 || (buffer[7] != 1 && buffer[7] != 2)) continue;
                if (buffer[7] == 1 && buffer[8] > 100) continue;
                // The official UI uses status 2 only as charging, not as a new percentage.
                return new BatteryReading { Percent = buffer[7] == 1 ? buffer[8] : -1, Charging = buffer[7] == 2, ReadAtUtc = DateTime.UtcNow };
            }
        }
    }
}
