using System.Globalization;

namespace T48Sdk;

public sealed class FileUsbTransferLogger : IUsbTransferLogger, IDisposable
{
    private readonly StreamWriter _writer;

    public FileUsbTransferLogger(string path)
    {
        _writer = new StreamWriter(File.Open(path, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        };
    }

    public void Log(UsbTransferLogEntry entry)
    {
        var hex = Convert.ToHexString(entry.Data, 0, Math.Min(entry.Transferred, entry.Data.Length));
        _writer.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{entry.Timestamp:O} {entry.Direction} pipe=0x{entry.PipeId:X2} bytes={entry.Transferred} elapsed_ms={entry.Elapsed.TotalMilliseconds:F3} data={hex}"));
    }

    public void Dispose() => _writer.Dispose();
}
