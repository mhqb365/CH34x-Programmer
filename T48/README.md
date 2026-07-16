# T48Sdk

`T48Sdk` is a Windows/.NET SDK for talking directly to an XGecu T48 programmer
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
- .NET SDK/runtime matching the project target, currently `net10.0-windows`.
- Close `Xgpro.exe`, Wireshark, and `dumpcap.exe` before using the SDK. They can
  hold the USB device and cause `Access is denied`.

The SDK has no external NuGet dependencies. `NuGet.config` clears package
sources so local builds do not require network access.

## Project Layout

```text
T48Sdk/
  src/T48Sdk/              Reusable SDK library
  samples/T48Probe/        CLI probe and test tool
  tools/                   USBPcap parsing helpers
  PROTOCOL_NOTES.md        Reverse-engineering notes
```

## Build

```powershell
dotnet build T48Sdk\T48Sdk.sln
```

Built DLL:

```text
T48Sdk\src\T48Sdk\bin\Debug\net10.0-windows\T48Sdk.dll
```

## Add To Another .NET App

Preferred: add a project reference to:

```text
T48Sdk\src\T48Sdk\T48Sdk.csproj
```

Example `.csproj` reference:

```xml
<ItemGroup>
  <ProjectReference Include="..\T48Sdk\src\T48Sdk\T48Sdk.csproj" />
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
byte[] data = spi25.ReadFlash(offset: 0, length: 256);
File.WriteAllBytes("read-000000.bin", data);
```

Blank-check a range:

```csharp
var blank = spi25.BlankCheck(offset: 0, length: 16 * 1024 * 1024);
Console.WriteLine(blank.IsBlank);
```

Erase and write:

```csharp
spi25.EraseChip();

byte[] image = File.ReadAllBytes("image.bin");
spi25.WriteFlash(offset: 0, image);
```

Write constraints:

- Offset must be aligned to `256` bytes.
- Data length must be a multiple of `256` bytes.
- Use a sacrificial/test chip until your app has its own confirmation flow.

## CLI Usage

The sample CLI is useful for testing the programmer before integrating the SDK.

List devices:

```powershell
dotnet T48Sdk\samples\T48Probe\bin\Debug\net10.0-windows\T48Probe.dll list
```

Show endpoints:

```powershell
dotnet T48Sdk\samples\T48Probe\bin\Debug\net10.0-windows\T48Probe.dll pipes
```

Read ID:

```powershell
dotnet T48Sdk\samples\T48Probe\bin\Debug\net10.0-windows\T48Probe.dll read-id
```

Read 256 bytes:

```powershell
dotnet T48Sdk\samples\T48Probe\bin\Debug\net10.0-windows\T48Probe.dll read-flash 0 256 T48Sdk\read-000000.bin
```

Read full W25Q128, 16 MiB:

```powershell
dotnet T48Sdk\samples\T48Probe\bin\Debug\net10.0-windows\T48Probe.dll read-flash 0 16777216 T48Sdk\w25q128-full.bin
```

Blank-check full W25Q128:

```powershell
dotnet T48Sdk\samples\T48Probe\bin\Debug\net10.0-windows\T48Probe.dll blank-check 0 16777216 T48Sdk\blank.log
```

Erase chip:

```powershell
dotnet T48Sdk\samples\T48Probe\bin\Debug\net10.0-windows\T48Probe.dll erase-chip T48Sdk\erase.log
```

Write image:

```powershell
dotnet T48Sdk\samples\T48Probe\bin\Debug\net10.0-windows\T48Probe.dll write-flash 0 T48Sdk\w25q128-full.bin T48Sdk\write.log
```

Verify by readback and binary compare:

```powershell
dotnet T48Sdk\samples\T48Probe\bin\Debug\net10.0-windows\T48Probe.dll read-flash 0 16777216 T48Sdk\w25q128-verify-read.bin
cmd /c fc /b T48Sdk\w25q128-full.bin T48Sdk\w25q128-verify-read.bin
```

Expected successful compare:

```text
FC: no differences encountered
```

Raw transfer for protocol work:

```powershell
dotnet T48Sdk\samples\T48Probe\bin\Debug\net10.0-windows\T48Probe.dll raw "0501030000000000" 32 t48-usb.log
```

## Logs

Most CLI commands accept an optional log file as the last argument. The log
records USB direction, pipe, byte count, elapsed time, and payload hex.

Example:

```powershell
dotnet T48Sdk\samples\T48Probe\bin\Debug\net10.0-windows\T48Probe.dll read-id T48Sdk\read-id.log
```

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
T48Sdk\PROTOCOL_NOTES.md
```

USBPcap helper tools:

```powershell
python T48Sdk\tools\parse-usbpcap.py C:\Users\Windows\Desktop\t48.pcap
python T48Sdk\tools\summarize-captures.py
```

## Safety

Erase and write are destructive. Build UI-level confirmations around them.
For early testing, use a sacrificial chip and always keep a readback backup.
