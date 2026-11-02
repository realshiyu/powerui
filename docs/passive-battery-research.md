# Passive Battery Reporting Research

Date: 2026-11-02. The initial sections record source research; the final section
records the later zero-write hardware capture and diagnostic implementation.

## Logitech MX Master 3S

The current device exposes HID++ Unified Battery (`0x1004`). The
[Linux HID++ driver](https://github.com/torvalds/linux/blob/master/drivers/hid/hid-logitech-hidpp.c)
defines `EVENT_UNIFIED_BATTERY_STATUS_EVENT` as `0x00`; its
`hidpp20_battery_event_1004` handler matches the battery feature index and event
function byte, then reads percentage from parameter 0 and charging state from
parameter 2. This is separate from the active get-status function `0x10`.
[Solaar's notification handler](https://github.com/pwr-Solaar/Solaar/blob/master/lib/logitech_receiver/notifications.py)
also processes Unified Battery notifications with address 0.

There is therefore a protocol-level event path suitable for passive updates.
Delivery on this host's Bolt receiver and mouse firmware still requires hardware
verification. Feature discovery and an initial percentage may require active
initialization while the mouse is already awake; this is not a promise of zero
writes from application startup. Events do not guarantee a periodic heartbeat or
an immediate initial reading. A retained percentage must retain its original
timestamp when no new event arrives.

Current `src/LogitechBattery.cs` opens short query sessions and accepts matching
active responses (`request[3]`, normally `0x1B` for get-status), rather than
maintaining a persistent event listener. Removing its writes alone is not a
complete passive implementation.

## VGN S99 2.4G

The current
[BatteryMonitor provider](https://github.com/JKWTCN/BatteryMonitor/blob/master/src/providers/hid/VgnHidProvider.cpp#L489-L524)
maps dongle `320F:5088` to the Weisheng family. `queryWeisheng` writes an output
report with payload `00 00 1A 06 00 00 00` and report ID 4, then reads an input
report. It checks command `0x1A`, parses payload offset 7 as percentage, and
offset 8 as charging. This establishes a query-response path, not an unsolicited
battery notification path.

The earlier local official-Web-HUB inspection is recorded in
[s99-protocol-research.md](s99-protocol-research.md): `getElectricQuantity`
sends that same command. The historical
[official script URL](https://drive.vgnlab.com.cn/assets/index-gX9tcLNJ.js)
could not be fetched during this research; local DNS lookup also failed. Its
current contents and any newer event path were not reverified.

The [official S99/S99PRO manual](https://vgnlab.com/blogs/guides/user-manual-for-vgn-s99-s99pro-keyboard)
describes sleep after 3 minutes of inactivity and a deeper sleep after 10 minutes
that disconnects Bluetooth/2.4G until a key is pressed. It does not document a
host-side passive battery notification API or establish whether a battery query
can wake the keyboard.

No verified passive host-reporting method was found for this S99 firmware.
That is an evidence limit, not proof that passive reports are impossible. A
read-only capture on the vendor interface, without battery commands from PowerUI
or another application, could check for unsolicited reports during ordinary use,
charging transitions and wake-up. An input report received after another program
queries the keyboard is not proof of unsolicited reporting. Absence of reports
in a short capture would not prove lack of support.

## Practical Direction

- Logitech: investigate an event-first reader, with initialization only while
  already active and explicit handling of stale or absent readings.
- VGN: keep passive support unconfirmed. Activity-gated queries or longer polling
  intervals are fallback policies, not passive acquisition.
- Neither source research establishes the actual wake or battery-consumption
  effect on these particular devices.

## Local Passive Capture, 2026-11-02

Added `PowerUI.Probe.exe --vgn-listen [seconds]` (default 120, range 1..900).
It opens only the matched S99 vendor collection with `GENERIC_READ` and
`FileAccess.Read`, keeps one input stream open for the capture, and logs vendor
input reports. It does not send Output or Feature reports, flush the input queue,
or open ordinary key-input collections. Battery-shaped reports are candidates,
not automatically proof of unsolicited reporting.

Built with `./build.ps1 -IncludeProbe -OutputDirectory artifacts/vgn-passive`.
Both the probe and application compiled successfully. The tray application's
battery implementation was not changed.

Hardware: `VGN S99 2.4G Dongle`, `320F:5088`, version `0101`, vendor collection
`FF1C:0092`, input/output report ID 4, 64 bytes. PowerUI was found running and
temporarily stopped for each capture, then restarted from its original portable
path in a `finally` block. Related-process checks during both captures found no
PowerUI or matching VGN/battery-monitor/browser process. The user confirmed other
drivers/web pages were closed. This is not a USB-bus trace of every application.

Times below are Asia/Shanghai:

| Capture | Window | Duration | Vendor input reports | Battery-shaped reports | Probe Output/Feature reports sent |
| --- | --- | --- | --- | --- | --- |
| 01 | 16:05:58 to 16:08:58 | 180.014 s | 0 | 0 | 0 |
| 02 | 16:11:05 to 16:12:35 | 90.007 s | 0 | 0 | 0 |

After the first capture the user confirmed normal key presses and
`Fn+Right Ctrl`, without charging; exact action timing was not recorded. During
the second capture the user repeated those actions and confirmed completion
before the window ended (confirmation observed by 16:11:47). No report arrived
after this activity either. Logs: `artifacts/vgn-passive/capture-01.log` and
`artifacts/vgn-passive/capture-02.log`.

Result: no passive battery report was observed on the verified vendor interface
in these two windows, including confirmed key activity. This does not establish
that the firmware can never report autonomously. Charging transitions, actual
percentage changes, longer periodic reporting, deep-sleep wake-up, and other
interfaces were not covered. No active query was used as a positive control.
Passive-only monitoring is therefore not validated as a replacement for the
existing S99 query-response reader. Actual wake or power-consumption effects
remain unmeasured.
