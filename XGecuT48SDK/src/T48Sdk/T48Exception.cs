using System.ComponentModel;

namespace T48Sdk;

public sealed class T48Exception : Exception
{
    public T48Exception(string message) : base(message)
    {
    }

    public T48Exception(string message, int nativeError)
        : base($"{message} Win32 error {nativeError}: {new Win32Exception(nativeError).Message}")
    {
        NativeErrorCode = nativeError;
    }

    public int? NativeErrorCode { get; }
}
