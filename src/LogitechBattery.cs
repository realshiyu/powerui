using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PowerUI
{
    internal static class LogitechBattery
    {
        internal static bool IsSupported(HidDevice device)
        {
            return device.VendorId == 0x046d
                && ((device.ProductId == 0xc548 && device.Capabilities.UsagePage == 0xff00 && device.Capabilities.Usage == 2)
                    || (device.ProductId == 0xb034 && device.Capabilities.UsagePage == 0xff43 && device.Capabilities.Usage == 0x0202))
                && device.InputReportIds == "17" && device.OutputReportIds == "17"
                && device.Capabilities.InputLength == 20 && device.Capabilities.OutputLength == 20;
        }

        internal static BatteryReading Read(HidDevice device, int timeoutMilliseconds, Action<string> trace, byte preferredIndex = 0)
        {
            var clock = Stopwatch.StartNew();
            BatteryReading result = null;
            bool bluetooth = device.ProductId == 0xb034;
            // Direct Bluetooth uses index FF; Bolt routes devices through paired slots 1..6.
            byte[] slots = bluetooth ? new byte[] { 0xff } : new byte[] { 1, 2, 3, 4, 5, 6 };
            slots = slots.OrderByDescending(slot => slot == preferredIndex).ToArray();
            string transport = bluetooth ? "Bluetooth" : "Bolt";
            foreach (byte slot in slots)
            {
                if (clock.ElapsedMilliseconds >= timeoutMilliseconds) throw new IOException("HID++ discovery timed out before all device indices were checked.");
                using (var handle = HidNative.CreateFile(device.Path, 0xc0000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero))
                {
                    if (handle.IsInvalid) throw new System.ComponentModel.Win32Exception();
                    using (var stream = new FileStream(handle, FileAccess.ReadWrite, 20, true))
                    {
                        try
                        {
                            Func<byte, byte, byte[], byte[]> query = (feature, function, args) =>
                                Query(stream, slot, feature, function, args, clock, timeoutMilliseconds);
                            byte nameFeature = query(0, 0, new byte[] { 0, 5 })[4];
                            if (nameFeature == 0) continue;
                            int nameLength = query(nameFeature, 0, new byte[0])[4];
                            if (nameLength == 0 || nameLength > 80) continue;
                            var nameBytes = new byte[nameLength];
                            for (int offset = 0; offset < nameLength; offset += 16)
                            {
                                byte[] part = query(nameFeature, 0x10, new byte[] { (byte)offset });
                                Array.Copy(part, 4, nameBytes, offset, Math.Min(16, nameLength - offset));
                            }
                            string name = Encoding.UTF8.GetString(nameBytes).TrimEnd('\0');
                            if (trace != null) trace(transport + " device index=" + slot + " name=" + name);
                            if (!string.Equals(name, "MX Master 3S", StringComparison.OrdinalIgnoreCase)) continue;
                            byte batteryFeature = query(0, 0, new byte[] { 0x10, 4 })[4];
                            if (batteryFeature == 0) throw new IOException("MX Master 3S does not expose Unified Battery.");
                            byte[] caps = query(batteryFeature, 0, new byte[0]);
                            if ((caps[5] & 2) == 0) throw new IOException("Device does not report a battery percentage.");
                            byte[] status = query(batteryFeature, 0x10, new byte[0]);
                            if (trace != null) trace(string.Format("Unified Battery feature={0:X2}; percent={1}; charging status={2}", batteryFeature, status[4], status[6]));
                            if (status[4] > 100 || status[6] > 3) throw new IOException("Invalid battery percentage or charging status.");
                            result = new BatteryReading { Percent = status[4], Charging = status[6] == 1 || status[6] == 2,
                                ReadAtUtc = DateTime.UtcNow, ReceiverPid = device.ProductId, ReceiverVid = device.VendorId,
                                DeviceName = name, DeviceIndex = slot, DeviceId = device.Identity };
                            return result;
                        }
                        catch (TimeoutException)
                        {
                            if (trace != null) trace(transport + " device index=" + slot + " unavailable or did not respond before timeout.");
                        }
                    }
                }
            }
            return result;
        }

        private static byte[] Query(FileStream stream, byte slot, byte feature, byte function, byte[] args, Stopwatch clock, int timeout)
        {
            var report = new byte[20];
            report[0] = 0x11;
            report[1] = slot;
            report[2] = feature;
            report[3] = (byte)(function | 0x0b);
            Array.Copy(args, 0, report, 4, args.Length);
            var exchange = Exchange(stream, report);
            int remaining = Math.Min(600, timeout - (int)clock.ElapsedMilliseconds);
            try
            {
                if (remaining <= 0 || Task.WaitAny(new Task[] { exchange }, remaining) < 0) throw new TimeoutException();
                return exchange.GetAwaiter().GetResult();
            }
            catch
            {
                stream.Dispose();
                exchange.ContinueWith(t => { var observed = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                throw;
            }
        }

        private static async Task<byte[]> Exchange(FileStream stream, byte[] request)
        {
            await stream.WriteAsync(request, 0, request.Length).ConfigureAwait(false);
            var response = new byte[20];
            while (true)
            {
                int length = await stream.ReadAsync(response, 0, response.Length).ConfigureAwait(false);
                if (length == 0) throw new IOException("HID++ device stopped returning reports.");
                if (length != 20 || response[0] != 0x11 || response[1] != request[1]) continue;
                if (response[2] == request[2] && response[3] == request[3]) return response;
                if (response[2] == 0xff && response[3] == request[2] && response[4] == request[3])
                    throw new TimeoutException("HID++ request unavailable: " + response[5]);
            }
        }
    }

    internal static class DeviceBattery
    {
        internal static BatteryReading Read(int timeoutMilliseconds, bool keyboard = false, string preferredId = null,
            ushort preferredVid = 0, ushort preferredPid = 0, byte preferredIndex = 0, Action<string> trace = null)
        {
            var all = HidDevice.EnumerateSupported();
            if (keyboard)
            {
                var keyboards = all.Where(VgnBattery.IsSupported).ToList();
                if (keyboards.Count != 1) throw new IOException(keyboards.Count == 0
                    ? "No supported keyboard interface is accessible." : "Multiple S99 receivers found; refusing ambiguous selection.");
                return VgnBattery.Read(keyboards[0], timeoutMilliseconds, null);
            }
            var devices = all.Where(d => RapooBattery.IsSupported(d) || LogitechBattery.IsSupported(d)).ToList();
            if (devices.Count == 0) throw new IOException("No supported mouse interface is accessible.");
            devices = devices.OrderByDescending(d => preferredId != null && d.Identity == preferredId)
                .ThenByDescending(d => d.VendorId == preferredVid && d.ProductId == preferredPid).ToList();
            // Give each endpoint its own bounded read so an idle receiver cannot starve another mouse.
            var pending = devices.Select(device => Task.Run(() =>
            {
                try
                {
                    var reading = LogitechBattery.IsSupported(device)
                        ? LogitechBattery.Read(device, timeoutMilliseconds, trace, preferredIndex)
                        : RapooBattery.Read(device, all, timeoutMilliseconds, false);
                    if (trace != null) trace(string.Format("Mouse endpoint {0:X4}:{1:X4}: {2}", device.VendorId, device.ProductId,
                        reading == null ? "no valid battery response" : "valid battery response"));
                    return reading;
                }
                catch (Exception error)
                {
                    if (trace != null) trace(string.Format("Mouse endpoint {0:X4}:{1:X4}: {2}", device.VendorId, device.ProductId, error.GetBaseException().Message));
                    return null;
                }
            })).ToArray();
            Task.WaitAll(pending);
            var selected = pending.Select(task => task.Result).FirstOrDefault(reading => reading != null);
            if (selected != null) return selected;
            // Receiver detection identifies the model, but never invents a fresh battery/status reading.
            var detected = devices.FirstOrDefault(RapooBattery.IsSupported);
            return detected == null ? null : new BatteryReading { Percent = -1, HasStatus = false,
                ReceiverVid = detected.VendorId, ReceiverPid = detected.ProductId, DeviceId = detected.Identity };
        }
    }
}
