using System.Runtime.InteropServices;

namespace T48Sdk;

public static class T48DeviceDiscovery
{
    public static IReadOnlyList<T48DeviceInfo> FindConnectedDevices()
    {
        var guid = T48DeviceInfo.WinUsbInterfaceGuid;
        var set = NativeMethods.SetupDiGetClassDevsW(
            in guid,
            null,
            IntPtr.Zero,
            NativeMethods.DigcfPresent | NativeMethods.DigcfDeviceInterface);

        if (set == IntPtr.Zero || set == new IntPtr(-1))
        {
            throw new T48Exception("Unable to get XGecu device interface list.", Marshal.GetLastWin32Error());
        }

        try
        {
            var devices = new List<T48DeviceInfo>();

            for (uint i = 0; ; i++)
            {
                var data = new SpDeviceInterfaceData
                {
                    CbSize = Marshal.SizeOf<SpDeviceInterfaceData>()
                };

                if (!NativeMethods.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, in guid, i, ref data))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == NativeMethods.ErrorNoMoreItems)
                    {
                        break;
                    }

                    throw new T48Exception("Unable to enumerate XGecu device interfaces.", error);
                }

                NativeMethods.SetupDiGetDeviceInterfaceDetailW(set, ref data, IntPtr.Zero, 0, out var size, IntPtr.Zero);
                var detail = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (!NativeMethods.SetupDiGetDeviceInterfaceDetailW(set, ref data, detail, size, out _, IntPtr.Zero))
                    {
                        throw new T48Exception("Unable to read XGecu device path.", Marshal.GetLastWin32Error());
                    }

                    var pathOffset = 4;
                    var path = Marshal.PtrToStringUni(IntPtr.Add(detail, pathOffset));
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        devices.Add(new T48DeviceInfo(
                            path,
                            T48DeviceInfo.XGecuVendorId,
                            T48DeviceInfo.T48ProductId,
                            guid));
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(detail);
                }
            }

            return devices;
        }
        finally
        {
            NativeMethods.SetupDiDestroyDeviceInfoList(set);
        }
    }
}
