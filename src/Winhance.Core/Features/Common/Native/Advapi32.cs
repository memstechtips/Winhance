using System;
using System.Runtime.InteropServices;

namespace Winhance.Core.Features.Common.Native;

public static class Advapi32
{
    public const uint SC_MANAGER_ALL_ACCESS = 0xF003F;
    public const uint SERVICE_ALL_ACCESS = 0xF01FF;
    public const uint SERVICE_QUERY_CONFIG = 0x0001;
    public const uint SERVICE_CHANGE_CONFIG = 0x0002;
    public const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;

    // Service start types
    public const uint SERVICE_AUTO_START = 0x00000002;
    public const uint SERVICE_DEMAND_START = 0x00000003;
    public const uint SERVICE_DISABLED = 0x00000004;

    // Service failure action types
    public const int SC_ACTION_NONE = 0;
    public const int SC_ACTION_RESTART = 1;

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint dwAccess);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool ChangeServiceConfig(
        IntPtr hService, uint dwServiceType, uint dwStartType,
        uint dwErrorControl, string? lpBinaryPathName, string? lpLoadOrderGroup,
        IntPtr lpdwTagId, string? lpDependencies, string? lpServiceStartName,
        string? lpPassword, string? lpDisplayName);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool ChangeServiceConfig2(IntPtr hService, uint dwInfoLevel, ref SERVICE_FAILURE_ACTIONS lpInfo);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool CloseServiceHandle(IntPtr hSCObject);

    public const uint SERVICE_CONFIG_FAILURE_ACTIONS = 2;

    [StructLayout(LayoutKind.Sequential)]
    public struct SC_ACTION
    {
        public int Type;
        public uint Delay;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SERVICE_FAILURE_ACTIONS
    {
        public uint dwResetPeriod;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpRebootMsg;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpCommand;
        public uint cActions;
        public IntPtr lpsaActions;
    }
}
