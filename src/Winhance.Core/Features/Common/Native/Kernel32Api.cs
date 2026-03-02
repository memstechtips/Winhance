using System.Runtime.InteropServices;

namespace Winhance.Core.Features.Common.Native;

public static class Kernel32Api
{
    public enum FirmwareType
    {
        Unknown = 0,
        Bios = 1,
        Uefi = 2,
        Max = 3
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetFirmwareType(out FirmwareType firmwareType);
}
