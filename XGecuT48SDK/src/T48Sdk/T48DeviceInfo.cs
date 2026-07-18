namespace T48Sdk;

public sealed record T48DeviceInfo(
    string DevicePath,
    ushort VendorId,
    ushort ProductId,
    Guid InterfaceGuid)
{
    public static readonly Guid WinUsbInterfaceGuid = new("E7E8BA13-2A81-446E-A11E-72398FBDA82F");
    public const ushort XGecuVendorId = 0xA466;
    public const ushort T48ProductId = 0x0A53;
}
