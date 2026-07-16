# T48 Protocol Notes

## Known from package analysis

The official package exposes only binary artifacts for the main application:

- `Xgpro.exe`: official GUI application.
- `InfoIC2Plus.dll`: chip database/metadata binary.
- `algorithm/*.alg`: per-family algorithm binaries.
- `UpdateT48.dat`: programmer update payload.
- `Serial25Index.dat`: SPI 25-series index data.
- `drv/Xgprowinusb.inf`: WinUSB driver metadata.

No source for the main USB protocol implementation is present in this snapshot.

## Transport layer

The Windows transport is WinUSB. `T48UsbDevice` opens the interface GUID from the
INF, queries USB endpoints, and selects the first bulk OUT and first bulk IN
pipe. If the device exposes multiple interfaces or alternate settings in a
future firmware version, the transport should be extended to select by descriptor
instead of first-match.

## Data to capture next

Use a USB capture tool while performing these official `Xgpro.exe` actions:

- App start with T48 plugged in.
- Device information or firmware version dialog.
- Pin/contact check with no chip.
- Detect on a common SPI flash, for example W25Q64.
- Read JEDEC ID.
- Read a small range, for example the first 256 bytes.
- Blank check.

For each action, record:

- Xgpro version and T48 firmware version.
- Chip model and adapter/socket state.
- All OUT frames and IN frames.
- Whether a command changes voltages or starts a timed operation.

## Promotion rule

A raw frame should become a named SDK command only after it has at least:

- A known request structure.
- A known success response.
- One observed failure response.
- A replay test on the T48 through `T48Probe`.

## Capture: `t48.pcap` Read ID

Source file: `C:\Users\Windows\Desktop\t48.pcap`

The T48 enumerated as bus `1`, device `4` in this capture. Bulk endpoints match
the SDK probe result:

- OUT: `0x01`
- IN: `0x81`

Extracted T48 bulk frames:

```text
1794 OUT ep=0x01 3E00000000000000
1797 IN  ep=0x81 3E003000270107000F000000F0000000
1798 OUT ep=0x01 03030200010091010000000188130000000000010001000001000000030000000000000000000000000900880040000001000000000000007842500000000000
1800 OUT ep=0x01 3903020001009101
1803 IN  ep=0x81 0000000000000000000000000000000000000000000000000000000000000000
1804 OUT ep=0x01 0500030000000000
1807 IN  ep=0x81 0503EF40180007000F000000F0000000323430393A3236003434413132353630
1808 OUT ep=0x01 0401030000000000
```

Observed Read ID command:

- Request: `0500030000000000`
- Response prefix: `0503EF4018`
- JEDEC ID bytes: `EF 40 18`

`EF 40 18` is consistent with a Winbond SPI flash family/device ID. The rest of
the response includes status/config bytes and ASCII metadata:

- ASCII tail: `2409:26\0 44A12560`

Open questions before promoting to a stable high-level API:

- Whether `030302...` is a chip/session setup command or a selected-chip
  algorithm upload command.
- Whether `390302...` starts the programmer-side operation and the all-zero
  `0x20` byte IN frame is an operation-complete/status response.
- Whether `040103...` is cleanup, deselect, or UI polling.

## Capture set: W25Q128 operations

Source files:

- `01-startup-device-info.pcap`
- `02-select-w25q128.pcap`
- `03-read-id-w25q128.pcap`
- `04-read-256bytes-w25q128.pcap`
- `05-blank-check-w25q128.pcap`
- `06-erase-w25q128.pcap`
- `07-write-256bytes-w25q128.pcap`
- `08-verify-w25q128.pcap`

All eight captures enumerate the T48 as VID/PID `A466:0A53`. In this capture
set the USBPcap bus/device tuple is `1/5`.

### Startup

Startup/device info uses command `0000000000000000`, followed by a second query
that embeds ASCII `F5EPK2`:

```text
OUT 0000000000000000
IN  0001300027010700323032342D30342D323430393A3236...
OUT 0000463545504B32
IN  0001300027010700323032342D30342D323430393A3236...
```

The response contains ASCII fields:

- `2024-04-2409:26`
- `44A1256074F5EPK2MOA4YFYWHD9W353336` / `...353337`

### Select chip

`02-select-w25q128.pcap` contains no T48 bulk frames after enumeration. The
official UI likely resolves chip metadata locally from `InfoIC2Plus.dll`,
`Serial25Index.dat`, and/or `algorithm/*.alg` without talking to the programmer.

### Common SPI25 setup

Most SPI25 operations start by probing the chip and loading/running the same
64-byte algorithm block:

```text
OUT 3E00............
IN  3E00<id/status bytes...>
OUT 03030200010091010000000188130000000000010001000001000000030000000000000000000000000900880040000001000000000000007842500000000000
OUT 3903020001009101
IN  0000000000000000000000000000000000000000000000000000000000000000
```

The `3E00...` request varies between captures. Its response contains the JEDEC
ID when the chip has already been identified, for example:

```text
IN 3E00EF40180007000F000000F0000000
```

### Read ID

Stable Read ID sequence:

```text
OUT 0501030000000000
IN  0503EF40180007000F000000F0000000323430393A3236003434413132353630
OUT 0401030000000000
```

Interpreted:

- Response prefix: `05 03`
- JEDEC ID: `EF 40 18`
- ASCII tail: `2409:26\0 44A12560`

This is now implemented as `T48Spi25Client.ReadJedecId()`.

### Read / verify data path

The official app reads data through command `0D010040<addr24 little-ish>`.
Each request returns `16384` bytes from bulk IN endpoint `0x82`.

Examples:

```text
OUT 0D01004000000000
IN  ep=0x82 len=16384
OUT 0D01004000400000
IN  ep=0x82 len=16384
OUT 0D01004000800000
IN  ep=0x82 len=16384
OUT 0D01004000C00000
IN  ep=0x82 len=16384
OUT 0D01004000000100
IN  ep=0x82 len=16384
```

Address bytes appear to advance by `0x4000` per block. For a W25Q128 full chip,
this produces `1024` blocks x `16384` bytes = `16777216` bytes.

Despite the filename `04-read-256bytes-w25q128.pcap`, the capture contains a
full 16 MiB readback pattern, not only 256 bytes.

Verify uses the same read command path and ends with:

```text
OUT 0800030000000001000260000000000000020000000000000002000000000000000200000000000000000000
IN  0800030018000700000260
OUT 0401004000C0FF00
```

The 44-byte `08...` command is likely the final verify/result command.

### Blank check

Blank check performs the common SPI25 setup, reads JEDEC ID, then reads one
16 KiB block via `0D01004000000000` and exits:

```text
OUT 0D01004000000000
IN  ep=0x82 len=16384
OUT 0401004000000000
```

More captures are needed to know whether it stops after the first non-blank
block, whether Xgpro requested only a small range, or whether the programmer can
perform blank check internally.

### Erase

Erase performs common setup and Read ID, then sends:

```text
OUT 0E00030000000000
IN  0E00EF4018000700
OUT 0401000000000000
```

This is likely chip erase. Do not replay automatically except on a sacrificial
chip.

### Write

Write performs common setup and erase, then streams data on endpoint `0x02`.

Important observed frames:

```text
OUT 1800030000000000
OUT 0C00000100000000
OUT ep=0x02 len=256
OUT 3900000000000000
IN  0000000000000000000000000000000000000000000000000000000000000000
OUT 0C00000100010000
OUT ep=0x02 len=256
OUT 0C00000100020000
OUT ep=0x02 len=256
...
OUT 0C00000100FFFF00
OUT ep=0x02 len=256
```

The `0C00000100NNNN00` command appears to select a 256-byte page/chunk; the data
immediately follows on bulk OUT endpoint `0x02`. The capture contains `40203`
data packets of 256 bytes, so it is not a simple 256-byte write-only capture.

After writing, the official app performs a read/verify-like operation:

```text
OUT 030302...0100020100...
OUT 3903020001009101
OUT 0D00004000000000
IN  ep=0x82 len=16384
OUT 0401004000000000
```

The exact write range and why `40203` data packets were sent still need
correlation with the file contents used for the write.

## Capture set: W25Q128 rerun, success/error separation

Source directory: `C:\Users\Windows\Desktop\T48 capture`

Important differences from the earlier capture set:

- `03-read-id-w25q128-success.pcap` standalone Read ID used
  `0500030000000000`.
- Most operation flows still used `0501030000000000` for Read ID inside the
  operation sequence.
- `04-read-256bytes-w25q128.pcap` again captured a full-chip read, because the
  official app defaults to full read.
- `07-erase-success-w25q128.pcap` and `12-erase-error-w25q128.pcap` are now a
  useful success/error pair.

### Erase success

Success flow:

```text
OUT 3E00E20008000000
IN  3E00EF40180007000F000000F0000000
...
OUT 0E00030000000000
IN  0E00EF4018000700
OUT 0401000000000000
```

The erase response arrives after roughly `28.75s` in this capture.

The SDK now treats `0E00<JEDEC...>` as erase success only when the JEDEC bytes
match the previously read ID.

### Erase error

Error flow:

```text
OUT 3E00E20008000000
IN  3E000300180007000F000000F0000000
...
OUT 0E00030000000000
OUT 0401000000000000
```

There is no `IN 0E...` response before cleanup. The initial probe response also
does not contain `EF4018`; it contains `030018` in the JEDEC-like position.

SDK behavior after this capture:

- Destructive operations (`EraseChip`, `WriteFlash`) require the initial probe
  response to confirm the same JEDEC ID as `Read ID`.
- If the probe does not match, the SDK refuses the destructive operation before
  sending erase/write.
- If erase is sent but no valid `0E00<JEDEC...>` response arrives within the
  configured timeout, the SDK throws instead of reporting success.
