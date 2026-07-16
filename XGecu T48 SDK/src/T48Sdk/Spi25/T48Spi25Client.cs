namespace T48Sdk.Spi25;

public sealed class T48Spi25Client
{
    public const int ReadBlockSize = 16 * 1024;
    public const int PageProgramSize = 256;
    public static readonly TimeSpan DefaultChipEraseProgressEstimate = TimeSpan.FromSeconds(45);
    public static readonly TimeSpan DefaultEraseResponseTimeout = TimeSpan.FromSeconds(60);

    private static readonly byte[] ProbeCommand = T48RawFrame.FromHex("3E00300027010700");
    private static readonly byte[] Spi25AlgorithmBlock = T48RawFrame.FromHex(
        "03030200010091010000000188130000000000010001000001000000030000000000000000000000000900880040000001000000000000007842500000000000");
    private static readonly byte[] RunAlgorithmCommand = T48RawFrame.FromHex("3903020001009101");
    private static readonly byte[] StandaloneReadIdCommand = T48RawFrame.FromHex("0500030000000000");
    private static readonly byte[] OperationReadIdCommand = T48RawFrame.FromHex("0501030000000000");
    private static readonly byte[] CleanupCommand = T48RawFrame.FromHex("0401030000000000");
    private static readonly byte[] IdleCleanupCommand = T48RawFrame.FromHex("0400000000000000");
    private static readonly byte[] EraseChipCommand = T48RawFrame.FromHex("0E00030000000000");
    private static readonly byte[] BeginWriteCommand = T48RawFrame.FromHex("1800030000000000");

    private readonly T48UsbDevice _device;

    public T48Spi25Client(T48UsbDevice device)
    {
        _device = device;
    }

    public T48Spi25DeviceId ReadJedecId()
    {
        RunSpi25Setup();

        _device.Write(StandaloneReadIdCommand);
        var response = _device.Read(32);

        _device.Write(CleanupCommand);

        if (response.Length < 5 || response[0] != 0x05 || response[1] != 0x03)
        {
            throw new T48Exception($"Unexpected SPI25 Read ID response: {Convert.ToHexString(response)}");
        }

        return new T48Spi25DeviceId(response[2], response[3], response[4], response);
    }

    public byte[] ReadFlash(uint offset, int length, IProgress<T48Progress>? progress = null)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must not be negative.");
        }

        if (length == 0)
        {
            progress?.Report(new T48Progress("Read", 0, 0, "No data requested"));
            return Array.Empty<byte>();
        }

        progress?.Report(new T48Progress("Read", 0, length, "Preparing SPI25 read"));
        RunSpi25Setup();

        _device.Write(OperationReadIdCommand);
        var idResponse = _device.Read(32);
        if (idResponse.Length < 5 || idResponse[0] != 0x05 || idResponse[1] != 0x03)
        {
            throw new T48Exception($"Unexpected SPI25 Read ID response before read: {Convert.ToHexString(idResponse)}");
        }

        _device.Write(T48RawFrame.FromHex("0400000000000000"));
        RunSpi25Setup();

        var alignedOffset = offset / ReadBlockSize * ReadBlockSize;
        var prefixSkip = checked((int)(offset - alignedOffset));
        var bytesToFetch = checked(prefixSkip + length);
        var blockCount = checked((bytesToFetch + ReadBlockSize - 1) / ReadBlockSize);
        var readBuffer = new byte[checked(blockCount * ReadBlockSize)];

        for (var block = 0; block < blockCount; block++)
        {
            var address = checked(alignedOffset + (uint)(block * ReadBlockSize));
            _device.Write(CreateReadBlockCommand(address));
            var data = _device.ReadExact(0x82, ReadBlockSize);
            data.CopyTo(readBuffer.AsSpan(block * ReadBlockSize));
            var completed = Math.Min(length, Math.Max(0, (block + 1) * ReadBlockSize - prefixSkip));
            progress?.Report(new T48Progress("Read", completed, length, $"Read block at 0x{address:X6}"));

            if (block == 0)
            {
                // Xgpro polls command 0x39 after the first read block in the
                // captures. Keep the same handshake while the protocol is young.
                _device.Write(T48RawFrame.FromHex("3900000000000000"));
                _device.Read(32);
            }
        }

        _device.Write(T48RawFrame.FromHex("0400000000000000"));

        var result = new byte[length];
        readBuffer.AsSpan(prefixSkip, length).CopyTo(result);
        progress?.Report(new T48Progress("Read", length, length, "Read complete"));
        return result;
    }

    public T48BlankCheckResult BlankCheck(uint offset, int length, IProgress<T48Progress>? progress = null)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must not be negative.");
        }

        progress?.Report(new T48Progress("BlankCheck", 0, length, "Starting blank check"));

        if (length == 0)
        {
            progress?.Report(new T48Progress("BlankCheck", 0, 0, "Blank check complete"));
            return new T48BlankCheckResult(true, null, null);
        }

        RunSpi25Setup();
        EnsureReadIdResponse();
        _device.Write(IdleCleanupCommand);
        RunSpi25Setup();

        var alignedOffset = offset / ReadBlockSize * ReadBlockSize;
        var prefixSkip = checked((int)(offset - alignedOffset));
        var bytesToFetch = checked(prefixSkip + length);
        var blockCount = checked((bytesToFetch + ReadBlockSize - 1) / ReadBlockSize);
        var checkedBytes = 0;

        for (var block = 0; block < blockCount; block++)
        {
            var address = checked(alignedOffset + (uint)(block * ReadBlockSize));
            _device.Write(CreateReadBlockCommand(address));
            var data = _device.ReadExact(0x82, ReadBlockSize);

            var scanStart = block == 0 ? prefixSkip : 0;
            var scanEnd = Math.Min(ReadBlockSize, prefixSkip + length - block * ReadBlockSize);
            for (var i = scanStart; i < scanEnd; i++)
            {
                if (data[i] != 0xFF)
                {
                    var absolute = address + (uint)i;
                    var failedAt = checked((int)(absolute - offset));
                    progress?.Report(new T48Progress("BlankCheck", failedAt, length, $"Non-blank byte at 0x{absolute:X6}"));
                    _device.Write(IdleCleanupCommand);
                    return new T48BlankCheckResult(false, absolute, data[i]);
                }
            }

            checkedBytes = Math.Min(length, checkedBytes + scanEnd - scanStart);
            progress?.Report(new T48Progress("BlankCheck", checkedBytes, length, $"Checked 0x{address + ReadBlockSize:X6}"));

            if (block == 0)
            {
                _device.Write(T48RawFrame.FromHex("3900000000000000"));
                _device.Read(32);
            }
        }

        _device.Write(IdleCleanupCommand);
        progress?.Report(new T48Progress("BlankCheck", length, length, "Blank check complete"));
        return new T48BlankCheckResult(true, null, null);
    }

    public void EraseChip(IProgress<T48Progress>? progress = null, TimeSpan? progressEstimate = null)
    {
        progress?.Report(new T48Progress("Erase", 0, 100, "Preparing erase"));
        var setupResponse = RunSpi25Setup();
        var idResponse = EnsureReadIdResponse();
        EnsureDestructiveProbeReady(setupResponse, idResponse);
        _device.Write(IdleCleanupCommand);
        RunSpi25Setup();

        progress?.Report(new T48Progress("Erase", 20, 100, "Erase command sent"));
        _device.Write(EraseChipCommand);
        using var eraseProgress = StartSmoothEraseProgress(progress, progressEstimate ?? DefaultChipEraseProgressEstimate);
        _device.SetPipeTransferTimeout(0x81, DefaultEraseResponseTimeout);
        var response = _device.Read(8);
        eraseProgress.Stop();
        if (!IsEraseSuccessResponse(response, idResponse))
        {
            throw new T48Exception($"Unexpected SPI25 erase response: {Convert.ToHexString(response)}");
        }

        progress?.Report(new T48Progress("Erase", 96, 100, "Erase response received"));
        _device.Write(T48RawFrame.FromHex("0401000000000000"));
        progress?.Report(new T48Progress("Erase", 100, 100, "Erase complete"));
    }

    public void WriteFlash(uint offset, ReadOnlySpan<byte> data, IProgress<T48Progress>? progress = null)
    {
        if ((offset % PageProgramSize) != 0)
        {
            throw new ArgumentException("Write offset must be aligned to 256 bytes.", nameof(offset));
        }

        if ((data.Length % PageProgramSize) != 0)
        {
            throw new ArgumentException("Write length must be a multiple of 256 bytes.", nameof(data));
        }

        progress?.Report(new T48Progress("Write", 0, data.Length, "Preparing write"));
        var setupResponse = RunSpi25Setup();
        var idResponse = EnsureReadIdResponse();
        EnsureDestructiveProbeReady(setupResponse, idResponse);
        _device.Write(IdleCleanupCommand);
        RunSpi25Setup();

        _device.Write(BeginWriteCommand);

        for (var written = 0; written < data.Length; written += PageProgramSize)
        {
            var address = checked(offset + (uint)written);
            _device.Write(CreateWritePageCommand(address));
            _device.Write(0x02, data.Slice(written, PageProgramSize));

            if (written == 0)
            {
                _device.Write(T48RawFrame.FromHex("3900000000000000"));
                _device.Read(32);
            }

            progress?.Report(new T48Progress("Write", written + PageProgramSize, data.Length, $"Wrote page at 0x{address:X6}"));
        }

        _device.Write(IdleCleanupCommand);
        progress?.Report(new T48Progress("Write", data.Length, data.Length, "Write complete"));
    }

    private byte[] RunSpi25Setup()
    {
        _device.Write(ProbeCommand);
        var probeResponse = _device.Read(16);

        _device.Write(Spi25AlgorithmBlock);
        _device.Write(RunAlgorithmCommand);
        _device.Read(32);
        return probeResponse;
    }

    private byte[] EnsureReadIdResponse()
    {
        _device.Write(OperationReadIdCommand);
        var response = _device.Read(32);
        if (response.Length < 5 || response[0] != 0x05 || response[1] != 0x03)
        {
            throw new T48Exception($"Unexpected SPI25 Read ID response: {Convert.ToHexString(response)}");
        }

        return response;
    }

    private static byte[] CreateReadBlockCommand(uint address)
    {
        var command = T48RawFrame.FromHex("0D01004000000000");
        command[4] = (byte)(address & 0xFF);
        command[5] = (byte)((address >> 8) & 0xFF);
        command[6] = (byte)((address >> 16) & 0xFF);
        return command;
    }

    private static byte[] CreateWritePageCommand(uint address)
    {
        var command = T48RawFrame.FromHex("0C00000100000000");
        command[4] = (byte)(address & 0xFF);
        command[5] = (byte)((address >> 8) & 0xFF);
        command[6] = (byte)((address >> 16) & 0xFF);
        return command;
    }

    private static void EnsureDestructiveProbeReady(byte[] setupResponse, byte[] idResponse)
    {
        if (setupResponse.Length < 5 || idResponse.Length < 5)
        {
            throw new T48Exception(
                $"Unable to validate destructive operation readiness. Probe={Convert.ToHexString(setupResponse)} ID={Convert.ToHexString(idResponse)}");
        }

        if (setupResponse[0] != 0x3E ||
            setupResponse[2] != idResponse[2] ||
            setupResponse[3] != idResponse[3] ||
            setupResponse[4] != idResponse[4])
        {
            throw new T48Exception(
                "Refusing destructive operation because initial probe did not confirm the selected SPI flash. " +
                $"Probe={Convert.ToHexString(setupResponse)} ID={Convert.ToHexString(idResponse)}");
        }
    }

    private static bool IsEraseSuccessResponse(byte[] response, byte[] idResponse)
    {
        return response.Length >= 5 &&
               response[0] == 0x0E &&
               response[1] == 0x00 &&
               response[2] == idResponse[2] &&
               response[3] == idResponse[3] &&
               response[4] == idResponse[4];
    }

    private static SmoothProgressScope StartSmoothEraseProgress(IProgress<T48Progress>? progress, TimeSpan estimate)
    {
        return progress is null
            ? SmoothProgressScope.None
            : new SmoothProgressScope(progress, estimate);
    }

    private sealed class SmoothProgressScope : IDisposable
    {
        public static readonly SmoothProgressScope None = new();

        private readonly CancellationTokenSource? _cts;
        private readonly Task? _task;

        private SmoothProgressScope()
        {
        }

        public SmoothProgressScope(IProgress<T48Progress> progress, TimeSpan estimate)
        {
            if (estimate <= TimeSpan.Zero)
            {
                estimate = DefaultChipEraseProgressEstimate;
            }

            _cts = new CancellationTokenSource();
            _task = Task.Run(async () =>
            {
                var start = DateTimeOffset.UtcNow;
                while (!_cts.IsCancellationRequested)
                {
                    var elapsed = DateTimeOffset.UtcNow - start;
                    var ratio = Math.Clamp(elapsed.TotalMilliseconds / estimate.TotalMilliseconds, 0, 1);
                    var completed = 20 + (long)Math.Round(ratio * 75);
                    progress.Report(new T48Progress("Erase", completed, 100, "Erasing chip"));

                    try
                    {
                        await Task.Delay(250, _cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            });
        }

        public void Stop()
        {
            if (_cts is null)
            {
                return;
            }

            _cts.Cancel();
            try
            {
                _task?.Wait(TimeSpan.FromSeconds(1));
            }
            catch (AggregateException)
            {
            }
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
