using System;
using System.Runtime.InteropServices;

namespace Winhance.Core.Features.Common.Native
{
    public static class PowerProf
    {
        // P/Invoke definitions for PowrProf.dll

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerEnumerate(
            IntPtr RootPowerKey,
            IntPtr SchemeGuid,
            IntPtr SubGroupOfPowerSettingsGuid,
            uint AccessFlags,
            uint Index,
            IntPtr Buffer,
            ref uint BufferSize);

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerEnumerate(
            IntPtr RootPowerKey,
            ref Guid SchemeGuid,
            IntPtr SubGroupOfPowerSettingsGuid,
            uint AccessFlags,
            uint Index,
            IntPtr Buffer,
            ref uint BufferSize);

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerEnumerate(
            IntPtr RootPowerKey,
            ref Guid SchemeGuid,
            ref Guid SubGroupOfPowerSettingsGuid,
            uint AccessFlags,
            uint Index,
            IntPtr Buffer,
            ref uint BufferSize);

        [DllImport("PowrProf.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint PowerReadFriendlyName(
            IntPtr RootPowerKey,
            ref Guid SchemeGuid,
            IntPtr SubGroupOfPowerSettingsGuid,
            IntPtr PowerSettingGuid,
            IntPtr Buffer,
            ref uint BufferSize);

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerReadACValueIndex(
            IntPtr RootPowerKey,
            ref Guid SchemeGuid,
            ref Guid SubGroupOfPowerSettingsGuid,
            ref Guid PowerSettingGuid,
            out uint AcValueIndex);

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerReadDCValueIndex(
            IntPtr RootPowerKey,
            ref Guid SchemeGuid,
            ref Guid SubGroupOfPowerSettingsGuid,
            ref Guid PowerSettingGuid,
            out uint DcValueIndex);

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerGetActiveScheme(
            IntPtr UserRootPowerKey,
            out IntPtr ActivePolicyGuid);

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerSetActiveScheme(
            IntPtr UserRootPowerKey,
            ref Guid SchemeGuid);

        [DllImport("Kernel32.dll", SetLastError = true)]
        public static extern IntPtr LocalFree(IntPtr hMem);

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerReadValueMin(
            IntPtr RootPowerKey,
            ref Guid SubGroupOfPowerSettingsGuid,
            ref Guid PowerSettingGuid,
            out uint ValueMinimum);

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerReadValueMax(
            IntPtr RootPowerKey,
            ref Guid SubGroupOfPowerSettingsGuid,
            ref Guid PowerSettingGuid,
            out uint ValueMaximum);

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerReadValueIncrement(
            IntPtr RootPowerKey,
            ref Guid SubGroupOfPowerSettingsGuid,
            ref Guid PowerSettingGuid,
            out uint ValueIncrement);

        // --- Write-side power scheme APIs ---

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerWriteACValueIndex(
            IntPtr RootPowerKey, ref Guid SchemeGuid,
            ref Guid SubGroupOfPowerSettingsGuid,
            ref Guid PowerSettingGuid, uint AcValueIndex);

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerWriteDCValueIndex(
            IntPtr RootPowerKey, ref Guid SchemeGuid,
            ref Guid SubGroupOfPowerSettingsGuid,
            ref Guid PowerSettingGuid, uint DcValueIndex);

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerDuplicateScheme(
            IntPtr RootPowerKey, ref Guid SourceSchemeGuid, out IntPtr DestinationSchemeGuid);

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerDeleteScheme(IntPtr RootPowerKey, ref Guid SchemeGuid);

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint PowerRestoreDefaultPowerSchemes();

        [DllImport("PowrProf.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint PowerWriteFriendlyName(
            IntPtr RootPowerKey, ref Guid SchemeGuid,
            IntPtr SubGroupOfPowerSettingsGuid,
            IntPtr PowerSettingGuid,
            [MarshalAs(UnmanagedType.LPWStr)] string Buffer,
            uint BufferSize);

        [DllImport("PowrProf.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint PowerWriteDescription(
            IntPtr RootPowerKey, ref Guid SchemeGuid,
            IntPtr SubGroupOfPowerSettingsGuid,
            IntPtr PowerSettingGuid,
            [MarshalAs(UnmanagedType.LPWStr)] string Buffer,
            uint BufferSize);

        [DllImport("PowrProf.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint PowerImportPowerScheme(
            IntPtr RootPowerKey,
            [MarshalAs(UnmanagedType.LPWStr)] string ImportFileNamePath,
            out IntPtr DestinationSchemeGuid);

        // --- System power capabilities ---

        [DllImport("PowrProf.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool GetPwrCapabilities(out SYSTEM_POWER_CAPABILITIES systemPowerCapabilities);

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_POWER_CAPABILITIES
        {
            [MarshalAs(UnmanagedType.U1)] public bool PowerButtonPresent;
            [MarshalAs(UnmanagedType.U1)] public bool SleepButtonPresent;
            [MarshalAs(UnmanagedType.U1)] public bool LidPresent;
            [MarshalAs(UnmanagedType.U1)] public bool SystemS1;
            [MarshalAs(UnmanagedType.U1)] public bool SystemS2;
            [MarshalAs(UnmanagedType.U1)] public bool SystemS3;
            [MarshalAs(UnmanagedType.U1)] public bool SystemS4;
            [MarshalAs(UnmanagedType.U1)] public bool SystemS5;
            [MarshalAs(UnmanagedType.U1)] public bool HiberFilePresent;
            [MarshalAs(UnmanagedType.U1)] public bool FullWake;
            [MarshalAs(UnmanagedType.U1)] public bool VideoDimPresent;
            [MarshalAs(UnmanagedType.U1)] public bool ApmPresent;
            [MarshalAs(UnmanagedType.U1)] public bool UpsPresent;
            [MarshalAs(UnmanagedType.U1)] public bool ThermalControl;
            [MarshalAs(UnmanagedType.U1)] public bool ProcessorThrottle;
            public byte ProcessorMinThrottle;
            public byte ProcessorMaxThrottle;
            [MarshalAs(UnmanagedType.U1)] public bool FastSystemS4;
            [MarshalAs(UnmanagedType.U1)] public bool Hiberboot;
            [MarshalAs(UnmanagedType.U1)] public bool WakeAlarmPresent;
            [MarshalAs(UnmanagedType.U1)] public bool AoAc;
            [MarshalAs(UnmanagedType.U1)] public bool DiskSpinDown;
            public byte HiberFileType;
            [MarshalAs(UnmanagedType.U1)] public bool AoAcConnectivitySupported;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[] spare3;
            [MarshalAs(UnmanagedType.U1)] public bool SystemBatteriesPresent;
            [MarshalAs(UnmanagedType.U1)] public bool BatteriesAreShortTerm;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public BATTERY_REPORTING_SCALE[] BatteryScale;
            public SYSTEM_POWER_STATE AcOnLineWake;
            public SYSTEM_POWER_STATE SoftLidWake;
            public SYSTEM_POWER_STATE RtcWake;
            public SYSTEM_POWER_STATE MinDeviceWakeState;
            public SYSTEM_POWER_STATE DefaultLowLatencyWake;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BATTERY_REPORTING_SCALE
        {
            public uint Granularity;
            public uint Capacity;
        }

        public enum SYSTEM_POWER_STATE
        {
            PowerSystemUnspecified = 0,
            PowerSystemWorking = 1,
            PowerSystemSleeping1 = 2,
            PowerSystemSleeping2 = 3,
            PowerSystemSleeping3 = 4,
            PowerSystemHibernate = 5,
            PowerSystemShutdown = 6,
            PowerSystemMaximum = 7
        }

        // --- Hibernation toggle ---

        [DllImport("PowrProf.dll", SetLastError = true)]
        public static extern uint CallNtPowerInformation(
            int InformationLevel,
            ref byte InputBuffer,
            uint InputBufferLength,
            IntPtr OutputBuffer,
            uint OutputBufferLength);

        public const int SystemReserveHiberFile = 10;

        // Constants
        public const uint ACCESS_SCHEME = 16;
        public const uint ACCESS_SUBGROUP = 17;
        public const uint ACCESS_INDIVIDUAL_SETTING = 18;
        public const uint ERROR_SUCCESS = 0;
        public const uint ERROR_NO_MORE_ITEMS = 259;
    }
}
