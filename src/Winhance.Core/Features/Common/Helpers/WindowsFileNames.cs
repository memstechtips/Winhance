namespace Winhance.Core.Features.Common.Helpers;

public static class WindowsFileNames
{
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    // Windows resolves a reserved name to the device whatever follows the first dot, so NUL.jpg discards the bytes
    // written to it and no file can be called CON.txt.
    public static bool IsReservedDeviceName(string fileName) =>
        ReservedDeviceNames.Contains(fileName.Split('.')[0].TrimEnd());
}
