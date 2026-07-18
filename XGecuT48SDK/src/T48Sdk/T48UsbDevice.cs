using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace T48Sdk;

public sealed class T48UsbDevice : IDisposable
{
    private readonly SafeFileHandle _deviceHandle;
    private readonly SafeWinUsbHandle _winUsbHandle;
    private readonly IUsbTransferLogger? _logger;

    private T48UsbDevice(
        T48DeviceInfo info,
        SafeFileHandle deviceHandle,
        SafeWinUsbHandle winUsbHandle,
        IReadOnlyList<UsbPipeInfo> pipes,
        byte bulkOutPipe,
        byte bulkInPipe,
        IUsbTransferLogger? logger)
    {
        Info = info;
        _deviceHandle = deviceHandle;
        _winUsbHandle = winUsbHandle;
        Pipes = pipes;
        BulkOutPipe = bulkOutPipe;
        BulkInPipe = bulkInPipe;
        _logger = logger;
    }

    public T48DeviceInfo Info { get; }
    public IReadOnlyList<UsbPipeInfo> Pipes { get; }
    public byte BulkOutPipe { get; }
    public byte BulkInPipe { get; }
    public static Action<string>? Diagnostics { get; set; }

    public static T48UsbDevice OpenFirst(IUsbTransferLogger? logger = null)
    {
        var info = T48DeviceDiscovery.FindConnectedDevices().FirstOrDefault()
            ?? throw new T48Exception("No XGecu T48 WinUSB device was found.");

        return Open(info, logger);
    }

    public static T48UsbDevice Open(T48DeviceInfo info, IUsbTransferLogger? logger = null)
    {
        var file = NativeMethods.CreateFileW(
            info.DevicePath,
            NativeMethods.GenericRead | NativeMethods.GenericWrite,
            NativeMethods.FileShareRead | NativeMethods.FileShareWrite,
            IntPtr.Zero,
            NativeMethods.OpenExisting,
            NativeMethods.FileAttributeNormal | NativeMethods.FileFlagOverlapped,
            IntPtr.Zero);

        Diagnostics?.Invoke($"CreateFile: isInvalid={file.IsInvalid}, handle=0x{file.DangerousGetHandle().ToInt64():X}");
        if (file.IsInvalid)
        {
            throw new T48Exception("Unable to open XGecu T48 device.", Marshal.GetLastWin32Error());
        }

        Diagnostics?.Invoke("WinUsb_Initialize: begin");
        if (!NativeMethods.WinUsb_Initialize(file.DangerousGetHandle(), out var winUsb))
        {
            var error = Marshal.GetLastWin32Error();
            file.Dispose();
            throw new T48Exception("Unable to initialize WinUSB for XGecu T48 device.", error);
        }

        Diagnostics?.Invoke("WinUsb_Initialize: ok");
        try
        {
            Diagnostics?.Invoke("WinUsb_QueryInterfaceSettings: begin");
            if (!NativeMethods.WinUsb_QueryInterfaceSettings(winUsb, 0, out var descriptor))
            {
                throw new T48Exception("Unable to query XGecu T48 USB interface.", Marshal.GetLastWin32Error());
            }

            Diagnostics?.Invoke($"WinUsb_QueryInterfaceSettings: endpoints={descriptor.NumEndpoints}");
            var pipes = new List<UsbPipeInfo>();
            byte? bulkOut = null;
            byte? bulkIn = null;

            for (byte i = 0; i < descriptor.NumEndpoints; i++)
            {
                Diagnostics?.Invoke($"WinUsb_QueryPipe[{i}]: begin");
                if (!NativeMethods.WinUsb_QueryPipe(winUsb, 0, i, out var pipe))
                {
                    throw new T48Exception("Unable to query XGecu T48 USB pipe.", Marshal.GetLastWin32Error());
                }

                Diagnostics?.Invoke($"WinUsb_QueryPipe[{i}]: pipe=0x{pipe.PipeId:X2} type={pipe.PipeType}");
                var pipeInfo = new UsbPipeInfo(pipe.PipeId, pipe.PipeType, pipe.MaximumPacketSize, pipe.Interval);
                pipes.Add(pipeInfo);

                if (pipe.PipeType == 2)
                {
                    if (pipeInfo.IsInput && bulkIn is null)
                    {
                        bulkIn = pipe.PipeId;
                    }
                    else if (pipeInfo.IsOutput && bulkOut is null)
                    {
                        bulkOut = pipe.PipeId;
                    }
                }
            }

            if (bulkIn is null || bulkOut is null)
            {
                throw new T48Exception("The XGecu T48 USB interface does not expose bulk IN and OUT pipes.");
            }

            return new T48UsbDevice(info, file, winUsb, pipes, bulkOut.Value, bulkIn.Value, logger);
        }
        catch
        {
            winUsb.Dispose();
            file.Dispose();
            throw;
        }
    }

    public int Write(ReadOnlySpan<byte> data) => Write(BulkOutPipe, data);

    public int Write(byte pipeId, ReadOnlySpan<byte> data)
    {
        var buffer = data.ToArray();
        var sw = Stopwatch.StartNew();
        if (!NativeMethods.WinUsb_WritePipe(_winUsbHandle, pipeId, buffer, (uint)buffer.Length, out var transferred, IntPtr.Zero))
        {
            throw new T48Exception($"USB write to pipe 0x{pipeId:X2} failed.", Marshal.GetLastWin32Error());
        }

        sw.Stop();
        _logger?.Log(new UsbTransferLogEntry(DateTimeOffset.Now, UsbTransferDirection.Out, pipeId, buffer, (int)transferred, sw.Elapsed));
        return (int)transferred;
    }

    public byte[] Read(int maxLength) => Read(BulkInPipe, maxLength);

    public byte[] Read(byte pipeId, int maxLength)
    {
        if (maxLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), "Read length must be positive.");
        }

        var buffer = new byte[maxLength];
        var sw = Stopwatch.StartNew();
        if (!NativeMethods.WinUsb_ReadPipe(_winUsbHandle, pipeId, buffer, (uint)buffer.Length, out var transferred, IntPtr.Zero))
        {
            throw new T48Exception($"USB read from pipe 0x{pipeId:X2} failed.", Marshal.GetLastWin32Error());
        }

        sw.Stop();
        Array.Resize(ref buffer, (int)transferred);
        _logger?.Log(new UsbTransferLogEntry(DateTimeOffset.Now, UsbTransferDirection.In, pipeId, buffer, (int)transferred, sw.Elapsed));
        return buffer;
    }

    public byte[] ReadExact(byte pipeId, int length)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Read length must be positive.");
        }

        var result = new byte[length];
        var offset = 0;
        while (offset < result.Length)
        {
            var chunk = Read(pipeId, result.Length - offset);
            if (chunk.Length == 0)
            {
                throw new T48Exception($"USB read from pipe 0x{pipeId:X2} returned no data.");
            }

            chunk.CopyTo(result.AsSpan(offset));
            offset += chunk.Length;
        }

        return result;
    }

    public byte[] Transfer(ReadOnlySpan<byte> request, int responseLength)
    {
        Write(request);
        return Read(responseLength);
    }

    public void SetPipeTransferTimeout(byte pipeId, TimeSpan timeout)
    {
        var milliseconds = checked((uint)Math.Clamp(timeout.TotalMilliseconds, 0, uint.MaxValue));
        if (!NativeMethods.WinUsb_SetPipePolicy(
            _winUsbHandle,
            pipeId,
            NativeMethods.PipeTransferTimeoutPolicy,
            sizeof(uint),
            ref milliseconds))
        {
            throw new T48Exception($"Unable to set USB timeout for pipe 0x{pipeId:X2}.", Marshal.GetLastWin32Error());
        }
    }

    public int ControlTransfer(
        byte requestType,
        byte request,
        ushort value,
        ushort index,
        Span<byte> buffer)
    {
        var packet = new WinUsbSetupPacket
        {
            RequestType = requestType,
            Request = request,
            Value = value,
            Index = index,
            Length = (ushort)buffer.Length
        };

        var temp = buffer.ToArray();
        var sw = Stopwatch.StartNew();
        if (!NativeMethods.WinUsb_ControlTransfer(_winUsbHandle, packet, temp, (uint)temp.Length, out var transferred, IntPtr.Zero))
        {
            throw new T48Exception("USB control transfer failed.", Marshal.GetLastWin32Error());
        }

        sw.Stop();
        temp.AsSpan(0, (int)Math.Min(transferred, (uint)buffer.Length)).CopyTo(buffer);
        _logger?.Log(new UsbTransferLogEntry(DateTimeOffset.Now, UsbTransferDirection.Control, 0, temp, (int)transferred, sw.Elapsed));
        return (int)transferred;
    }

    public void Dispose()
    {
        _winUsbHandle.Dispose();
        _deviceHandle.Dispose();
    }
}
