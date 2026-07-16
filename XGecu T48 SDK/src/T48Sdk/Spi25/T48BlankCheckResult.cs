namespace T48Sdk.Spi25;

public sealed record T48BlankCheckResult(bool IsBlank, uint? FirstNonBlankOffset, byte? FirstNonBlankValue);
