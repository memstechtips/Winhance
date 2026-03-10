using System.Runtime.InteropServices;

namespace Winhance.Core.Features.Common.Native;

public static class SrClientApi
{
    [DllImport("SrClient.dll", CharSet = CharSet.Unicode)]
    public static extern bool SRSetRestorePointW(
        ref RESTOREPOINTINFO pRestorePtSpec,
        out STATEMGRSTATUS pSMgrStatus);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct RESTOREPOINTINFO
    {
        public int dwEventType;
        public int dwRestorePtType;
        public long llSequenceNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szDescription;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct STATEMGRSTATUS
    {
        public int nStatus;
        public long llSequenceNumber;
    }

    public const int BEGIN_SYSTEM_CHANGE = 100;
    public const int MODIFY_SETTINGS = 12;

    // Status codes returned in STATEMGRSTATUS.nStatus
    public const int ERROR_SUCCESS = 0;
    public const int ERROR_SERVICE_DISABLED = 1058;
    public const int ERROR_DISK_FULL = 112;
    public const int ERROR_INTERNAL_ERROR = 1359;
    public const int ERROR_TIMEOUT = 1460;

    /// <summary>
    /// Returns a human-readable description for the given SRSetRestorePointW status code.
    /// </summary>
    public static string GetStatusDescription(int statusCode)
    {
        return statusCode switch
        {
            ERROR_SUCCESS => "Success",
            ERROR_SERVICE_DISABLED => "System Restore service is disabled",
            ERROR_DISK_FULL => "Insufficient disk space for restore point",
            ERROR_INTERNAL_ERROR => "Internal error in System Restore service",
            ERROR_TIMEOUT => "Operation timed out",
            _ => $"Unknown status code ({statusCode})"
        };
    }
}
