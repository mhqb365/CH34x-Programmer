namespace T48Sdk;

public sealed record UsbTransferLogEntry(
    DateTimeOffset Timestamp,
    UsbTransferDirection Direction,
    byte PipeId,
    byte[] Data,
    int Transferred,
    TimeSpan Elapsed);

public enum UsbTransferDirection
{
    Out,
    In,
    Control
}
