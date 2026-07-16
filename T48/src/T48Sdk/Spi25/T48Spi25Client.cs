namespace T48Sdk.Spi25;

public sealed class T48Spi25Client
{
    public const int ReadBlockSize = 16 * 1024;
    public const int PageProgramSize = 256;

    private static readonly byte[] ProbeCommand = T48RawFrame.FromHex("3E00300027010700");
    private static readonly byte[] Spi25AlgorithmBlock = T48RawFrame.FromHex(
        "03030200010091010000000188130000000000010001000001000000030000000000000000000000000900880040000001000000000000007842500000000000");
    private static readonly byte[] RunAlgorithmCommand = T48RawFrame.FromHex("3903020001009101");
    private static readonly byte[] ReadIdCommand = T48RawFrame.FromHex("0501030000000000");
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

        _device.Write(ReadIdCommand);
        var response = _device.Read(32);

        _device.Write(CleanupCommand);

        if (response.Length < 5 || response[0] != 0x05 || response[1] != 0x03)
        {
            throw new T48Exception($"Unexpected SPI25 Read ID response: {Convert.ToHexString(response)}");
        }

        return new T48Spi25DeviceId(response[2], response[3], response[4], response);
    }

    public byte[] ReadFlash(uint offset, int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must not be negative.");
        }

        if (length == 0)
        {
            return Array.Empty<byte>();
        }

        RunSpi25Setup();

        _device.Write(ReadIdCommand);
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
        return result;
    }

    public T48BlankCheckResult BlankCheck(uint offset, int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must not be negative.");
        }

        const int chunkSize = ReadBlockSize;
        var remaining = length;
        var current = offset;

        while (remaining > 0)
        {
            var count = Math.Min(chunkSize, remaining);
            var data = ReadFlash(current, count);
            for (var i = 0; i < data.Length; i++)
            {
                if (data[i] != 0xFF)
                {
                    return new T48BlankCheckResult(false, current + (uint)i, data[i]);
                }
            }

            current += (uint)count;
            remaining -= count;
        }

        return new T48BlankCheckResult(true, null, null);
    }

    public void EraseChip()
    {
        RunSpi25Setup();
        EnsureReadIdResponse();
        _device.Write(IdleCleanupCommand);
        RunSpi25Setup();

        _device.Write(EraseChipCommand);
        var response = _device.Read(8);
        if (response.Length < 2 || response[0] != 0x0E)
        {
            throw new T48Exception($"Unexpected SPI25 erase response: {Convert.ToHexString(response)}");
        }

        _device.Write(T48RawFrame.FromHex("0401000000000000"));
    }

    public void WriteFlash(uint offset, ReadOnlySpan<byte> data)
    {
        if ((offset % PageProgramSize) != 0)
        {
            throw new ArgumentException("Write offset must be aligned to 256 bytes.", nameof(offset));
        }

        if ((data.Length % PageProgramSize) != 0)
        {
            throw new ArgumentException("Write length must be a multiple of 256 bytes.", nameof(data));
        }

        RunSpi25Setup();
        EnsureReadIdResponse();
        _device.Write(IdleCleanupCommand);
        RunSpi25Setup();

        _device.Write(BeginWriteCommand);

        for (var written = 0; written < data.Length; written += PageProgramSize)
        {
            var address = checked(offset + (uint)written);
            _device.Write(CreateWritePageCommand(address));
            _device.Write(0x02, data.Slice(written, PageProgramSize));

            _device.Write(T48RawFrame.FromHex("3900000000000000"));
            _device.Read(32);
        }

        _device.Write(IdleCleanupCommand);
    }

    private void RunSpi25Setup()
    {
        _device.Write(ProbeCommand);
        _device.Read(16);

        _device.Write(Spi25AlgorithmBlock);
        _device.Write(RunAlgorithmCommand);
        _device.Read(32);
    }

    private byte[] EnsureReadIdResponse()
    {
        _device.Write(ReadIdCommand);
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
}
