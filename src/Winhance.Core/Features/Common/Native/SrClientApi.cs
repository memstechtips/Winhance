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
}
