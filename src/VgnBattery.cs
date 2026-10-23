using System;
using System.IO;
using System.Threading.Tasks;

namespace PowerUI
{
    internal static class VgnBattery
    {
        internal static bool IsSupported(HidDevice device)
        {
            return device.VendorId == 0x320f && device.ProductId == 0x5088
                && device.Product == "VGN S99 2.4G Dongle"
                && device.Capabilities.UsagePage == 0xff1c && device.Capabilities.Usage == 0x0092
                && device.InputReportIds == "4" && device.OutputReportIds == "4"
                && device.Capabilities.InputLength == 64 && device.Capabilities.OutputLength == 64;
        }

        internal static BatteryReading Read(HidDevice device, int timeoutMilliseconds, Action<string> trace)
        {
            if (!IsSupported(device)) throw new InvalidOperationException("Not a matched S99 receiver interface.");
            using (var handle = HidNative.CreateFile(device.Path, 0xc0000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero))
            {
                if (handle.IsInvalid) throw new System.ComponentModel.Win32Exception();
                using (var stream = new FileStream(handle, FileAccess.ReadWrite, 64, true))
                {
                    var exchange = Exchange(stream, trace);
                    if (Task.WaitAny(new Task[] { exchange }, timeoutMilliseconds) < 0)
                    {
                        stream.Dispose();
                        exchange.ContinueWith(t => { var observed = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                        throw new TimeoutException("S99 did not return a valid battery response. It may be sleeping.");
                    }
                    return exchange.GetAwaiter().GetResult();
                }
            }
        }

        private static async Task<BatteryReading> Exchange(FileStream stream, Action<string> trace)
        {
            // Official WebHID getElectricQuantity payload, padded to the native report length.
            var request = new byte[64];
            request[0] = 4;
            request[3] = 0x1a;
            request[4] = 6;
            if (trace != null) trace("TX battery query: " + BitConverter.ToString(request));
            await stream.WriteAsync(request, 0, request.Length).ConfigureAwait(false);
            var response = new byte[64];
            while (true)
            {
                int length = await stream.ReadAsync(response, 0, response.Length).ConfigureAwait(false);
                if (length == 0) throw new IOException("S99 receiver stopped returning reports.");
                if (length < 10 || response[0] != 4 || response[1] != 0 || response[2] != 0 || response[3] != 0x1a) continue;
                if (trace != null) trace("RX battery response: " + BitConverter.ToString(response, 0, length));
                if (response[8] > 100 || response[9] > 1) throw new IOException("Invalid S99 battery percentage or charge flag.");
                return new BatteryReading { Percent = response[8], Charging = response[9] == 1,
                    ReadAtUtc = DateTime.UtcNow, ReceiverVid = 0x320f, ReceiverPid = 0x5088, DeviceName = "VGN S99" };
            }
        }
    }
}
