# VGN S99 Battery Protocol Research

Date: 2026-10-23. Source evidence only; no device writes performed by this research task.

## Query

[BatteryMonitor implementation](https://github.com/JKWTCN/BatteryMonitor/blob/master/src/providers/hid/VgnHidProvider.cpp#L489-L524)
identifies S99 as the Weisheng family. Its device table explicitly includes
`320F:5088` and Report ID `0x04` at lines 68-72.

- Transport: Output Report, then Input Report.
- Request payload: `00 00 1A 06 00 00 00`.
- HID packet including Report ID: `04 00 00 1A 06 00 00 00`.
- No checksum in this query.
- Response, excluding Report ID: command at offset 2 must equal `0x1A`;
  percentage is offset 7; charging flag is offset 8.
- Source interprets any nonzero charging flag as charging and retries up to five
  times. Its generic read timeout is 500 ms.

## Verification Boundary

These bytes are verified against source, not S99 hardware. The project's
[README](https://github.com/JKWTCN/BatteryMonitor#readme) lists an ATK V75X sharing
these VID/PIDs as hardware-tested, not the S99 itself. Shared VID/PIDs alone do
not establish identical hardware. Match the product string, vendor collection,
report ID and actual response before accepting a value. Reject values over 100.

Use descriptor-sized Windows buffers rather than copying the source's 16-byte
read buffer. Do not substitute Feature Report writes or send unrelated commands.

## Official Implementation And Local Result

The [official download API](https://www.vgnlab.com.cn/api/drive/sites) points to
[VGN Web HUB](https://drive.vgnlab.com.cn/). Its public
[script](https://drive.vgnlab.com.cn/assets/index-gX9tcLNJ.js), inspected on
2026-10-23, maps the S99 dongle to VID `0x320F`, PID `0x5088`, usage
`FF1C:0092`, Report ID 4. The Weisheng controller's `getElectricQuantity`
sends `[0,0,26,6,0,0,0]`. Its `readBuffer` checks command 26, takes `slice(7)`
and reads the first byte as battery percentage. This confirms the percentage
offset independently of the open-source adapter. The inspected official code
does not parse a charging flag in this branch; charging interpretation remains
based on the open-source adapter, not a real charging transition test.

Local native HID enumeration matched product `VGN S99 2.4G Dongle`, version
`0101`, usage `FF1C:0092`, Report ID 4, Input/Output length 64. The diagnostic
reader only opens this exact vendor-defined interface, not key input interfaces.

Built with `./build.ps1 -IncludeProbe -OutputDirectory artifacts/diagnostics`.
Ran `./artifacts/diagnostics/PowerUI.Probe.exe --vgn-battery` twice, at
2026-10-23 09:41:29 and 09:41:46 Asia/Shanghai. Both returned a 64-byte reply:

```text
04 00 00 1A 06 00 00 00 37 00 [remaining bytes zero]
```

The command/header matched; battery `0x37` is **55%**. The adapter's charge
field was zero. Only the read-only battery request was sent, with zero padding
to the descriptor's 64-byte native report size. No configuration/firmware
commands were sent.

## Monitoring Integration

The application now monitors S99 independently of the existing mouse reader,
each with a 60-second polling timer and 5-second per-read budget. S99 state,
color and low-battery alert deduplication are stored in
`PowerUI.keyboard.state.xml`; the legacy mouse state file is preserved.
The global tray mode is stored in the mouse state file and can be Separate,
Mouse or Keyboard. Hiding an icon does not stop its monitor. Each menu can open
the other device's panel. Existing per-monitor 5-minute stale-reading handling
and alert threshold/recovery behavior are reused.

On 2026-10-23, the actual application read S99 at 54% and MX Master 3S at 95%
concurrently. Both state timestamps advanced again after the 60-second polling
interval. A light-theme settings capture at 200% DPI showed both device trays
visible and no text overlap. Real charging/sleep/unplug transitions and delivery
of low-battery notifications are not yet verified.
