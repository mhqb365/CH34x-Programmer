using System.IO;
using T48Sdk;
using T48Sdk.Spi25;

namespace NexusProgrammer;

public sealed class T48SDKProgrammer : IChipProgrammer
{
    private const int WritePageSize = T48Spi25Client.PageProgramSize;

    public string Name => "XGecu T48 SDK";

    public static bool CanOpenDevice()
    {
        try
        {
            using var device = T48UsbDevice.OpenFirst();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task<bool> DetectAsync(IProgress<int> progress) => Task.Run(() =>
    {
        progress.Report(10);
        using var device = T48UsbDevice.OpenFirst();
        progress.Report(100);
        return true;
    });

    public Task<byte[]> ReadIdAsync(ChipProfile chip, IProgress<int> progress) => Task.Run(() =>
    {
        EnsureSpi25(chip);
        using var device = T48UsbDevice.OpenFirst();
        var spi25 = new T48Spi25Client(device);
        progress.Report(50);
        var id = ReadValidJedecId(spi25);
        progress.Report(100);
        return new[] { id.ManufacturerId, id.MemoryType, id.CapacityCode };
    });

    public Task<byte[]> ReadAsync(ChipProfile chip, int startAddress, int length, IProgress<int> progress) => Task.Run(() =>
    {
        EnsureSpi25(chip);
        using var device = T48UsbDevice.OpenFirst();
        var spi25 = new T48Spi25Client(device);
        progress.Report(5);
        var data = RunT48Operation(() => spi25.ReadFlash((uint)startAddress, length, ToSdkProgress(progress), UsesLargeFlashProfile(chip)));
        if (data.Length != length)
        {
            throw new IOException($"XGecu T48 SDK returned {data.Length} byte(s), expected {length} byte(s).");
        }

        progress.Report(100);
        return data;
    });

    public Task WriteAsync(ChipProfile chip, int startAddress, byte[] data, IProgress<int> progress, bool skipBlankPages = false) => Task.Run(() =>
    {
        EnsureSpi25(chip);
        if (startAddress % WritePageSize != 0)
        {
            throw new ArgumentException("XGecu T48 write offset must be aligned to 256 bytes.");
        }

        var padded = PadForT48Write(data);
        using var device = T48UsbDevice.OpenFirst();
        var spi25 = new T48Spi25Client(device);
        progress.Report(5);
        if (skipBlankPages)
        {
            RunT48Operation(() => WriteNonBlankPages(spi25, (uint)startAddress, padded, progress, UsesLargeFlashProfile(chip)));
        }
        else
        {
            RunT48Operation(() => spi25.WriteFlash((uint)startAddress, padded, ToSdkProgress(progress), UsesLargeFlashProfile(chip)));
            progress.Report(100);
        }
    });

    public async Task<bool> VerifyAsync(ChipProfile chip, int startAddress, byte[] data, IProgress<int> progress)
    {
        var actual = await ReadAsync(chip, startAddress, data.Length, progress);
        return actual.SequenceEqual(data);
    }

    public Task UnprotectAsync(ChipProfile chip, IProgress<int> progress)
    {
        EnsureSpi25(chip);
        progress.Report(100);
        return Task.CompletedTask;
    }

    public Task EraseAsync(ChipProfile chip, IProgress<int> progress) => Task.Run(() =>
    {
        EnsureSpi25(chip);
        using var device = T48UsbDevice.OpenFirst();
        var spi25 = new T48Spi25Client(device);
        progress.Report(5);
        RunT48Operation(() => spi25.EraseChip(ToSdkProgress(progress), EstimateEraseDuration(chip), UsesLargeFlashProfile(chip)));
        progress.Report(100);
    });

    private static T48Spi25DeviceId ReadValidJedecId(T48Spi25Client spi25)
    {
        try
        {
            var id = spi25.ReadJedecId();
            if (IsPoorContactId(id))
            {
                throw PoorContactException($"Invalid IC ID {id.ManufacturerId:X2} {id.MemoryType:X2} {id.CapacityCode:X2}");
            }

            return id;
        }
        catch (T48Exception ex) when (LooksLikePoorContact(ex))
        {
            throw PoorContactException(ex.Message, ex);
        }
    }

    private static void RunT48Operation(Action operation)
    {
        try
        {
            operation();
        }
        catch (T48Exception ex) when (LooksLikePoorContact(ex))
        {
            throw PoorContactException(ex.Message, ex);
        }
    }

    private static T RunT48Operation<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (T48Exception ex) when (LooksLikePoorContact(ex))
        {
            throw PoorContactException(ex.Message, ex);
        }
    }

    private static IOException PoorContactException(string detail, Exception? inner = null) =>
        new($"Poor IC contact / {detail}", inner);

    private static bool IsPoorContactId(T48Spi25DeviceId id)
    {
        return id.ManufacturerId == 0x00 && id.MemoryType == 0x00 && id.CapacityCode == 0x00 ||
               id.ManufacturerId == 0xFF && id.MemoryType == 0xFF && id.CapacityCode == 0xFF ||
               id.ManufacturerId == 0x03 && id.MemoryType == 0x00;
    }

    private static bool LooksLikePoorContact(T48Exception ex)
    {
        var message = ex.Message;
        return message.Contains("Read ID response", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("initial probe did not confirm", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("destructive operation readiness", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("erase response", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteNonBlankPages(T48Spi25Client spi25, uint startAddress, byte[] data, IProgress<int> progress, bool useLargeFlashProfile)
    {
        var done = 0;
        while (done < data.Length)
        {
            while (done < data.Length && IsBlank(data, done, Math.Min(WritePageSize, data.Length - done)))
            {
                done += WritePageSize;
                progress.Report(data.Length == 0 ? 100 : Math.Min(100, done * 100 / data.Length));
            }

            var runStart = done;
            while (done < data.Length && !IsBlank(data, done, Math.Min(WritePageSize, data.Length - done)))
            {
                done += WritePageSize;
            }

            if (done > runStart)
            {
                spi25.WriteFlash(startAddress + (uint)runStart, data.AsSpan(runStart, done - runStart), useLargeFlashProfile: useLargeFlashProfile);
            }

            progress.Report(data.Length == 0 ? 100 : done * 100 / data.Length);
        }
    }

    private static byte[] PadForT48Write(byte[] data)
    {
        if (data.Length % WritePageSize == 0)
        {
            return data;
        }

        var paddedLength = (data.Length + WritePageSize - 1) / WritePageSize * WritePageSize;
        var padded = new byte[paddedLength];
        Array.Fill(padded, (byte)0xFF);
        Buffer.BlockCopy(data, 0, padded, 0, data.Length);
        return padded;
    }

    private static bool UsesLargeFlashProfile(ChipProfile chip) => chip.SizeBytes > 0x1000000;

    private static bool IsBlank(byte[] data, int offset, int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (data[offset + i] != 0xFF)
            {
                return false;
            }
        }

        return true;
    }

    private static IProgress<T48Progress> ToSdkProgress(IProgress<int> progress) =>
        new Progress<T48Progress>(value => progress.Report((int)Math.Round(value.Percent)));

    private static TimeSpan EstimateEraseDuration(ChipProfile chip)
    {
        var mib = Math.Max(1.0, chip.SizeBytes / 1024.0 / 1024.0);
        return TimeSpan.FromSeconds(Math.Clamp(mib * 3.0, 12.0, 90.0));
    }

    private static void EnsureSpi25(ChipProfile chip)
    {
        if (!string.Equals(chip.Protocol, "SPI", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(chip.CommandSet, "25xx", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("XGecu T48 SDK backend currently supports SPI 25xx flash only.");
        }
    }
}
