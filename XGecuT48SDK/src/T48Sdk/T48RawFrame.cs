namespace T48Sdk;

public static class T48RawFrame
{
    public static byte[] FromHex(string hex)
    {
        var normalized = new string(hex.Where(Uri.IsHexDigit).ToArray());
        if ((normalized.Length & 1) != 0)
        {
            throw new FormatException("Hex string must contain an even number of hex digits.");
        }

        return Convert.FromHexString(normalized);
    }

    public static string ToHex(ReadOnlySpan<byte> bytes) => Convert.ToHexString(bytes);
}
