namespace T48Sdk;

public readonly record struct UsbPipeInfo(byte PipeId, int PipeType, ushort MaximumPacketSize, byte Interval)
{
    public bool IsInput => (PipeId & 0x80) != 0;
    public bool IsOutput => !IsInput;
}
