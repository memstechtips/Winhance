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

        // Constants
        public const uint ACCESS_SCHEME = 16;
        public const uint ACCESS_SUBGROUP = 17;
        public const uint ACCESS_INDIVIDUAL_SETTING = 18;
        public const uint ERROR_SUCCESS = 0;
        public const uint ERROR_NO_MORE_ITEMS = 259;
    }
}
