namespace T48Sdk.Spi25;

public sealed record T48Spi25DeviceId(
    byte ManufacturerId,
    byte MemoryType,
    byte CapacityCode,
    byte[] RawResponse)
{
    public string JedecHex => $"{ManufacturerId:X2}{MemoryType:X2}{CapacityCode:X2}";
}
