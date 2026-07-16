# XGecu T48 SDK

`XGecu T48 SDK` is a Windows/.NET SDK for talking directly to an XGecu T48 programmer
through WinUSB. It was built from USB captures of the official Xgpro software
and currently targets SPI 25-series flash workflows tested with a W25Q128-class
chip.

## Status

Working on real hardware:

- Detect and open the T48 over WinUSB.
- Query USB bulk endpoints.
- Read SPI25 JEDEC ID.
- Read SPI25 flash ranges.
- Blank-check SPI25 flash ranges.
- Erase SPI25 chip.
- Write SPI25 flash pages.
- Refuse destructive operations when the initial probe does not confirm the
  selected SPI flash ID.

Validated workflow:

```text
Read ID -> Read full chip -> Erase -> Write -> Read back -> Compare
```

Tested JEDEC ID:

```text
EF4018
```

## Device Facts

Read from `drv/Xgprowinusb.inf`:

- USB VID: `0xA466`
- USB PID: `0x0A53`
- Driver: WinUSB
- Interface GUID: `{E7E8BA13-2A81-446E-A11E-72398FBDA82F}`
- Device manager class: `XGecu USB Devices`

The SDK uses this GUID to enumerate and open the programmer.

## Requirements

- Windows.
- XGecu WinUSB driver installed.
- .NET SDK/runtime matching the project target. The library currently targets
  `net8.0-windows` and `net10.0-windows`; the `T48Probe` sample targets
  `net10.0-windows`.
- Close `Xgpro.exe`, Wireshark, and `dumpcap.exe` before using the SDK. They can
  hold the USB device and cause `Access is denied`.

The SDK has no external NuGet dependencies. `NuGet.config` clears package
sources so local builds do not require network access.

## Project Layout

```text
XGecu T48 SDK/
  src/T48Sdk/              Reusable SDK library
  samples/T48Probe/        CLI probe and test tool
  tools/                   USBPcap parsing helpers
  PROTOCOL_NOTES.md        Reverse-engineering notes
```

## Build

```powershell
dotnet build ".\XGecu T48 SDK\XGecu T48 SDK.sln"
```

Built DLLs:

```text
XGecu T48 SDK\src\T48Sdk\bin\Debug\net8.0-windows\T48Sdk.dll
XGecu T48 SDK\src\T48Sdk\bin\Debug\net10.0-windows\T48Sdk.dll
```

## Add To Another .NET App

Preferred: add a project reference to:

```text
XGecu T48 SDK\src\T48Sdk\T48Sdk.csproj
```

Example `.csproj` reference:

```xml
<ItemGroup>
  <ProjectReference Include="..\XGecu T48 SDK\src\T48Sdk\T48Sdk.csproj" />
</ItemGroup>
```

Or reference the built `T48Sdk.dll` directly.

## Basic API Usage

Read JEDEC ID:

```csharp
using T48Sdk;
using T48Sdk.Spi25;

using var device = T48UsbDevice.OpenFirst();
var spi25 = new T48Spi25Client(device);

var id = spi25.ReadJedecId();
Console.WriteLine(id.JedecHex); // EF4018
```

Read flash bytes:

```csharp
var progress = new Progress<T48Progress>(p =>
{
    Console.WriteLine($"{p.Operation}: {p.Percent:F1}% {p.Message}");
});

byte[] data = spi25.ReadFlash(offset: 0, length: 256, progress);
File.WriteAllBytes("read-000000.bin", data);
```

Blank-check a range:

```csharp
var blank = spi25.BlankCheck(offset: 0, length: 16 * 1024 * 1024, progress);
Console.WriteLine(blank.IsBlank);
```

Erase and write:

```csharp
spi25.EraseChip(progress);

byte[] image = File.ReadAllBytes("image.bin");
spi25.WriteFlash(offset: 0, image, progress);
```

Write constraints:

- Offset must be aligned to `256` bytes.
- Data length must be a multiple of `256` bytes.
- Use a sacrificial/test chip until your app has its own confirmation flow.

## CLI Usage

The sample CLI is useful for testing the programmer before integrating the SDK.

List devices:

```powershell
dotnet ".\XGecu T48 SDK\samples\T48Probe\bin\Debug\net10.0-windows\T48Probe.dll" list
```

Show endpoints:

```powershell
dotnet ".\XGecu T48 SDK\samples\T48Probe\bin\Debug\net10.0-windows\T48Probe.dll" pipes
```

Read ID:

```powershell
dotnet ".\XGecu T48 SDK\samples\T48Probe\bin\Debug\net10.0-windows\T48Probe.dll" read-id
```

Read 256 bytes:

```powershell
dotnet ".\XGecu T48 SDK\samples\T48Probe\bin\Debug\net10.0-windows\T48Probe.dll" read-flash 0 256 "XGecu T48 SDK\read-000000.bin"
```

Read full W25Q128, 16 MiB:

```powershell
dotnet ".\XGecu T48 SDK\samples\T48Probe\bin\Debug\net10.0-windows\T48Probe.dll" read-flash 0 16777216 "XGecu T48 SDK\w25q128-full.bin"
```

Blank-check full W25Q128:

```powershell
dotnet ".\XGecu T48 SDK\samples\T48Probe\bin\Debug\net10.0-windows\T48Probe.dll" blank-check 0 16777216 "XGecu T48 SDK\blank.log"
```

Erase chip:

```powershell
dotnet ".\XGecu T48 SDK\samples\T48Probe\bin\Debug\net10.0-windows\T48Probe.dll" erase-chip "XGecu T48 SDK\erase.log"
```

Optional smooth erase progress estimate, in seconds:

```powershell
dotnet ".\XGecu T48 SDK\samples\T48Probe\bin\Debug\net10.0-windows\T48Probe.dll" erase-chip "XGecu T48 SDK\erase.log" 45
```

Write image:

```powershell
dotnet ".\XGecu T48 SDK\samples\T48Probe\bin\Debug\net10.0-windows\T48Probe.dll" write-flash 0 "XGecu T48 SDK\w25q128-full.bin" "XGecu T48 SDK\write.log"
```

Verify by readback and binary compare:

```powershell
dotnet ".\XGecu T48 SDK\samples\T48Probe\bin\Debug\net10.0-windows\T48Probe.dll" read-flash 0 16777216 "XGecu T48 SDK\w25q128-verify-read.bin"
cmd /c fc /b "XGecu T48 SDK\w25q128-full.bin" "XGecu T48 SDK\w25q128-verify-read.bin"
```

Expected successful compare:

```text
FC: no differences encountered
```

Raw transfer for protocol work:

```powershell
dotnet ".\XGecu T48 SDK\samples\T48Probe\bin\Debug\net10.0-windows\T48Probe.dll" raw "0501030000000000" 32 t48-usb.log
```

## Logs

Most CLI commands accept an optional log file as the last argument. The log
records USB direction, pipe, byte count, elapsed time, and payload hex.

Example:

```powershell
dotnet ".\XGecu T48 SDK\samples\T48Probe\bin\Debug\net10.0-windows\T48Probe.dll" read-id "XGecu T48 SDK\read-id.log"
```

## Progress

The SDK exposes fallback progress with `IProgress<T48Progress>`.

- Read: based on 16 KiB blocks received from endpoint `0x82`.
- Blank check: based on bytes scanned.
- Write: based on 256-byte pages sent and acknowledged.
- Erase: smooth elapsed-time fallback from 20% to 95% while waiting for the T48
  erase response, then 100% when the real response arrives. The current protocol
  capture gives done/error style responses, not a reliable real chip percentage.
  Erase success is accepted only when the response matches `0E00<JEDEC...>`.

For UI integration, treat `T48Progress.Percent` as display progress and
`T48Progress.Message` as optional status text.

## Troubleshooting

`Unable to open XGecu T48 device. Win32 error 5: Access is denied.`

Close any program holding the device:

- `Xgpro.exe`
- Wireshark
- `dumpcap.exe`

Then unplug/replug the T48 and retry.

`No XGecu T48 WinUSB device was found.`

Check:

- T48 is plugged in.
- Driver is installed.
- Device Manager shows `XGecu WinUSB Device`.
- VID/PID is `A466:0A53`.

PowerShell `fc` problem:

PowerShell aliases `fc` to `Format-Custom`. Use:

```powershell
cmd /c fc /b file1.bin file2.bin
```

Or compare hashes:

```powershell
(Get-FileHash file1.bin).Hash -eq (Get-FileHash file2.bin).Hash
```

## Protocol Notes

Low-level command frames and capture analysis live in:

```text
XGecu T48 SDK\PROTOCOL_NOTES.md
```

USBPcap helper tools:

```powershell
python ".\XGecu T48 SDK\tools\parse-usbpcap.py" C:\Users\Windows\Desktop\t48.pcap
python ".\XGecu T48 SDK\tools\summarize-captures.py"
```

## Safety

Erase and write are destructive. Build UI-level confirmations around them.
For early testing, use a sacrificial chip and always keep a readback backup.
