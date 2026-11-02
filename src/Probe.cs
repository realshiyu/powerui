using System;
using System.Diagnostics;
using System.IO;

namespace PowerUI
{
    internal static class Probe
    {
        internal static int Main(string[] args)
        {
            try
            {
                if (args.Length >= 1 && args[0] == "--vgn-listen")
                {
                    int seconds = 120;
                    if (args.Length > 2 || (args.Length == 2 && !int.TryParse(args[1], out seconds)) || seconds < 1 || seconds > 900)
                        throw new ArgumentException("Usage: PowerUI.Probe.exe --vgn-listen [seconds: 1..900]");
                    return ListenVgn(seconds);
                }
                if (args.Length == 1 && args[0] == "--mouse")
                {
                    Console.WriteLine("Auto-selecting a supported mouse using read-only battery responses; no settings changed.");
                    var reading = DeviceBattery.Read(5000, trace: Console.WriteLine);
                    if (reading == null) { Console.WriteLine("No supported mouse returned a valid battery response."); return 4; }
                    Console.WriteLine("Selected={0:X4}:{1:X4} Device={2} DeviceIndex={3} Battery={4} Charging={5} HasStatus={6}",
                        reading.ReceiverVid, reading.ReceiverPid, reading.DeviceName ?? "Rapoo mouse", reading.DeviceIndex,
                        reading.Percent < 0 ? "unknown" : reading.Percent + "%", reading.Charging, reading.HasStatus);
                    return reading.HasStatus ? 0 : 4;
                }
                if (args.Length == 1 && args[0] == "--vgn-battery")
                {
                    var keyboards = HidDevice.EnumerateSupported().FindAll(VgnBattery.IsSupported);
                    if (keyboards.Count != 1) throw new InvalidOperationException("Expected exactly one matched VGN S99 receiver interface.");
                    var reading = VgnBattery.Read(keyboards[0], 2000, Console.WriteLine);
                    Console.WriteLine("Device={0} Battery={1}% Charging={2} ReadAtUtc={3:o}",
                        reading.DeviceName, reading.Percent, reading.Charging, reading.ReadAtUtc);
                    return 0;
                }
                if (args.Length == 1 && args[0] == "--logitech")
                {
                    var logitechDevices = HidDevice.EnumerateSupported().FindAll(LogitechBattery.IsSupported);
                    if (logitechDevices.Count != 1) throw new InvalidOperationException("Expected exactly one accessible Logitech Bolt or MX Master 3S Bluetooth HID++ long-report interface.");
                    Console.WriteLine("Read-only HID++ device-name, feature discovery and battery queries; no settings changed.");
                    var reading = LogitechBattery.Read(logitechDevices[0], 8000, Console.WriteLine);
                    if (reading == null) { Console.WriteLine("No MX Master 3S battery response. Device may be sleeping."); return 4; }
                    Console.WriteLine("Endpoint={0:X4}:{1:X4} Device={2} DeviceIndex={3} Battery={4}% Charging={5} ReadAtUtc={6:o}",
                        reading.ReceiverVid, reading.ReceiverPid, reading.DeviceName, reading.DeviceIndex, reading.Percent, reading.Charging, reading.ReadAtUtc);
                    return 0;
                }
                if (args.Length == 1 && (args[0] == "--listen" || args[0] == "--query"))
                {
                    bool request = args[0] == "--query";
                    Console.WriteLine(request ? "Sending the official read-battery query to a matched mouse receiver, then listening for 8 seconds."
                        : "Listening for battery-only input report 7 for 8 seconds. No output reports sent.");
                    var reading = RapooBattery.Read(8000, request, Console.WriteLine);
                    if (reading == null)
                    {
                        Console.WriteLine("No valid battery report received before timeout. This does not mean disconnected.");
                        Console.WriteLine("Reports={0}; battery reports={1}; last battery status={2}; last battery field={3}",
                            RapooBattery.Reports, RapooBattery.BatteryReports, RapooBattery.LastStatus, RapooBattery.LastBattery);
                        Console.WriteLine("Receiver ACK={0}; mouse firmware ACK={1}; battery-query ACK={2}",
                            RapooBattery.ReceiverAck, RapooBattery.FirmwareAck, RapooBattery.BatteryAck);
                        if (RapooBattery.BatteryReports > 0 && RapooBattery.LastStatus == 0)
                            Console.WriteLine("Mouse reported invalid battery status 0. The battery field is not a valid percentage; official A HUB also hides the percentage for this status.");
                        if (request && RapooBattery.ReceiverAck == 1 && RapooBattery.FirmwareAck == 0)
                            Console.WriteLine("Receiver responds locally, but the mouse firmware query failed. Check mouse wake/connection and compare with official A HUB.");
                        return 4;
                    }
                    Console.WriteLine("Receiver=24AE:{0:X4} Battery={1} Charging={2} ReadAtUtc={3:o}",
                        reading.ReceiverPid, reading.Percent < 0 ? "unknown" : reading.Percent + "%", reading.Charging, reading.ReadAtUtc);
                    return 0;
                }
                bool vgn = args.Length == 1 && args[0] == "--vgn";
                if (args.Length != 0 && !vgn) throw new ArgumentException("Usage: PowerUI.Probe.exe [--mouse|--listen|--query|--logitech|--vgn|--vgn-battery|--vgn-listen [seconds]]");
                var devices = HidDevice.EnumerateSupported();
                Console.WriteLine("HID interfaces: {0}; supported vendor paths: {1}; inaccessible paths: {2}; last open error: {3}",
                    HidDevice.InterfaceCount, HidDevice.SupportedPathCount, HidDevice.InaccessibleCount, HidDevice.LastOpenError);
                Console.WriteLine("Selected vendor HID collections: {0}", devices.Count);
                foreach (var device in devices)
                {
                    var caps = device.Capabilities;
                    Console.WriteLine("VID={0:X4} PID={1:X4} Version={2:X4} Usage={3:X4}:{4:X4} Input={5} Output={6} Feature={7} Product={8}",
                        device.VendorId, device.ProductId, device.Version, caps.UsagePage, caps.Usage,
                        caps.InputLength, caps.OutputLength, caps.FeatureLength, device.Product);
                    if (caps.UsagePage >= 0xff00)
                        Console.WriteLine("  Report IDs: Input={0} Output={1} Feature={2}", device.InputReportIds, device.OutputReportIds, device.FeatureReportIds);
                }
                Console.WriteLine("Enumeration only. No HID reports sent. Device paths and serial numbers omitted.");
                return devices.Count == 0 ? 2 : 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("HID enumeration failed: " + error.Message);
                return 3;
            }
        }

        private static int ListenVgn(int seconds)
        {
            var keyboards = HidDevice.EnumerateSupported().FindAll(VgnBattery.IsSupported);
            if (keyboards.Count != 1) throw new InvalidOperationException("Expected exactly one matched VGN S99 receiver interface.");
            var device = keyboards[0];
            Console.WriteLine("Passive VGN S99 capture: VID={0:X4} PID={1:X4} Version={2:X4} Usage={3:X4}:{4:X4}; duration={5}s",
                device.VendorId, device.ProductId, device.Version, device.Capabilities.UsagePage, device.Capabilities.Usage, seconds);
            Console.WriteLine("Read access only. No Output/Feature reports sent. Ordinary key-input interfaces are not opened.");
            Console.WriteLine("Other battery monitors must be closed. Initial queued reports alone do not prove unsolicited reporting.");
            int reports = 0, candidates = 0;
            var clock = Stopwatch.StartNew();
            using (var handle = HidNative.CreateFile(device.Path, 0x80000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero))
            {
                if (handle.IsInvalid) throw new System.ComponentModel.Win32Exception();
                using (var stream = new FileStream(handle, FileAccess.Read, device.Capabilities.InputLength, true))
                {
                    Console.WriteLine("Capture started at UTC={0:o}", DateTime.UtcNow);
                    var buffer = new byte[device.Capabilities.InputLength];
                    while (clock.ElapsedMilliseconds < seconds * 1000)
                    {
                        var read = stream.ReadAsync(buffer, 0, buffer.Length);
                        int remaining = seconds * 1000 - (int)clock.ElapsedMilliseconds;
                        if (remaining <= 0 || !read.Wait(remaining))
                        {
                            stream.Dispose();
                            read.ContinueWith(t => { var observed = t.Exception; }, System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
                            break;
                        }
                        int length = read.GetAwaiter().GetResult();
                        if (length == 0) throw new IOException("S99 receiver stopped returning reports; capture incomplete.");
                        reports++;
                        Console.WriteLine("UTC={0:o} Elapsed={1:F3}s RX={2}", DateTime.UtcNow, clock.Elapsed.TotalSeconds,
                            BitConverter.ToString(buffer, 0, length));
                        if (length >= 10 && buffer[0] == 4 && buffer[1] == 0 && buffer[2] == 0 && buffer[3] == 0x1a
                            && buffer[8] <= 100 && buffer[9] <= 1)
                        {
                            candidates++;
                            Console.WriteLine("Battery-shaped report: percent={0}; charge-field={1}. Check for external queries or queued data before calling it unsolicited.",
                                buffer[8], buffer[9]);
                        }
                    }
                }
            }
            Console.WriteLine("Capture complete at UTC={0:o}; elapsed={1:F3}s; reports={2}; battery-shaped reports={3}; output/feature reports sent=0",
                DateTime.UtcNow, clock.Elapsed.TotalSeconds, reports, candidates);
            Console.WriteLine(reports == 0 ? "No vendor input reports in this window. Passive support is not disproved."
                : "Input reports observed; unsolicited battery support still requires controlled provenance.");
            return 0;
        }
    }
}
