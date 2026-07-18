using T48Sdk;
using T48Sdk.Spi25;

static int Usage()
{
    Console.WriteLine("Xgecu T48 SDK CLI commands:");
    Console.WriteLine("  list");
    Console.WriteLine("  pipes");
    Console.WriteLine("  read-id [log-file]");
    Console.WriteLine("  read-flash <offset-hex> <length> <output-bin> [log-file]");
    Console.WriteLine("  blank-check <offset-hex> <length> [log-file]");
    Console.WriteLine("  erase-chip [log-file] [progress-estimate-seconds]");
    Console.WriteLine("  write-flash <offset-hex> <input-bin> [log-file]");
    Console.WriteLine("  raw <hex-request> <response-bytes> [log-file]");
    return 2;
}

static IProgress<T48Progress> ConsoleProgress()
{
    return new Progress<T48Progress>(progress =>
    {
        Console.Error.WriteLine(
            $"{progress.Operation}: {progress.Percent,6:F2}% ({progress.Completed}/{progress.Total}) {progress.Message}");
    });
}

try
{
    var command = args.FirstOrDefault()?.ToLowerInvariant();
    if (command is null)
    {
        return Usage();
    }

    if (command == "list")
    {
        foreach (var device in T48DeviceDiscovery.FindConnectedDevices())
        {
            Console.WriteLine($"{device.DevicePath}");
            Console.WriteLine($"  VID=0x{device.VendorId:X4} PID=0x{device.ProductId:X4} IF={device.InterfaceGuid}");
        }

        return 0;
    }

    if (command == "pipes")
    {
        T48UsbDevice.Diagnostics = message => Console.Error.WriteLine($"diag: {message}");
        using var device = T48UsbDevice.OpenFirst();
        Console.WriteLine(device.Info.DevicePath);
        Console.WriteLine($"Bulk OUT: 0x{device.BulkOutPipe:X2}");
        Console.WriteLine($"Bulk IN : 0x{device.BulkInPipe:X2}");
        foreach (var pipe in device.Pipes)
        {
            Console.WriteLine($"  pipe=0x{pipe.PipeId:X2} type={pipe.PipeType} maxPacket={pipe.MaximumPacketSize} interval={pipe.Interval}");
        }

        return 0;
    }

    if (command == "raw")
    {
        if (args.Length < 3 || !int.TryParse(args[2], out var responseBytes))
        {
            return Usage();
        }

        using var fileLogger = args.Length >= 4 ? new FileUsbTransferLogger(args[3]) : null;
        using var device = T48UsbDevice.OpenFirst(fileLogger);
        var request = T48RawFrame.FromHex(args[1]);
        var response = device.Transfer(request, responseBytes);
        Console.WriteLine(T48RawFrame.ToHex(response));
        return 0;
    }

    if (command == "read-id")
    {
        using var fileLogger = args.Length >= 2 ? new FileUsbTransferLogger(args[1]) : null;
        using var device = T48UsbDevice.OpenFirst(fileLogger);
        var spi25 = new T48Spi25Client(device);
        var id = spi25.ReadJedecId();
        Console.WriteLine(id.JedecHex);
        Console.WriteLine(T48RawFrame.ToHex(id.RawResponse));
        return 0;
    }

    if (command == "read-flash")
    {
        if (args.Length < 4)
        {
            return Usage();
        }

        var offsetText = args[1].StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? args[1][2..] : args[1];
        var offset = Convert.ToUInt32(offsetText, 16);
        var length = int.Parse(args[2]);
        var outputPath = args[3];

        using var fileLogger = args.Length >= 5 ? new FileUsbTransferLogger(args[4]) : null;
        using var device = T48UsbDevice.OpenFirst(fileLogger);
        var spi25 = new T48Spi25Client(device);
        var data = spi25.ReadFlash(offset, length, ConsoleProgress());
        File.WriteAllBytes(outputPath, data);
        Console.WriteLine($"read {data.Length} bytes from 0x{offset:X6} to {outputPath}");
        return 0;
    }

    if (command == "blank-check")
    {
        if (args.Length < 3)
        {
            return Usage();
        }

        var offsetText = args[1].StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? args[1][2..] : args[1];
        var offset = Convert.ToUInt32(offsetText, 16);
        var length = int.Parse(args[2]);

        using var fileLogger = args.Length >= 4 ? new FileUsbTransferLogger(args[3]) : null;
        using var device = T48UsbDevice.OpenFirst(fileLogger);
        var spi25 = new T48Spi25Client(device);
        var result = spi25.BlankCheck(offset, length, ConsoleProgress());
        if (result.IsBlank)
        {
            Console.WriteLine("blank");
        }
        else
        {
            Console.WriteLine($"not blank at 0x{result.FirstNonBlankOffset:X6}: 0x{result.FirstNonBlankValue:X2}");
            return 3;
        }

        return 0;
    }

    if (command == "erase-chip")
    {
        using var fileLogger = args.Length >= 2 ? new FileUsbTransferLogger(args[1]) : null;
        var estimate = args.Length >= 3 && double.TryParse(args[2], out var estimateSeconds)
            ? TimeSpan.FromSeconds(estimateSeconds)
            : T48Spi25Client.DefaultChipEraseProgressEstimate;
        using var device = T48UsbDevice.OpenFirst(fileLogger);
        var spi25 = new T48Spi25Client(device);
        spi25.EraseChip(ConsoleProgress(), estimate);
        Console.WriteLine("erase command completed");
        return 0;
    }

    if (command == "write-flash")
    {
        if (args.Length < 3)
        {
            return Usage();
        }

        var offsetText = args[1].StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? args[1][2..] : args[1];
        var offset = Convert.ToUInt32(offsetText, 16);
        var inputPath = args[2];
        var data = File.ReadAllBytes(inputPath);

        using var fileLogger = args.Length >= 4 ? new FileUsbTransferLogger(args[3]) : null;
        using var device = T48UsbDevice.OpenFirst(fileLogger);
        var spi25 = new T48Spi25Client(device);
        spi25.WriteFlash(offset, data, ConsoleProgress());
        Console.WriteLine($"wrote {data.Length} bytes from {inputPath} to 0x{offset:X6}");
        return 0;
    }

    return Usage();
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
