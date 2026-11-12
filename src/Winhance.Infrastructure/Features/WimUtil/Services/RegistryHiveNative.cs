using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Registry;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

// RegLoadKey and RegUnLoadKey need the backup and restore privileges, which an administrator holds but has switched off.
internal sealed class RegistryHiveNative : IRegistryHiveNative
{
    public int Load(string name, string file)
    {
        using var _ = ProcessPrivilege.Hold(PInvoke.SE_BACKUP_NAME, PInvoke.SE_RESTORE_NAME);
        return (int)PInvoke.RegLoadKey(Registry.LocalMachine.Handle, name, file);
    }

    public int Unload(string name)
    {
        using var _ = ProcessPrivilege.Hold(PInvoke.SE_BACKUP_NAME, PInvoke.SE_RESTORE_NAME);
        return (int)PInvoke.RegUnLoadKey(Registry.LocalMachine.Handle, name);
    }

    // Keys like Control\Power\User belong to SYSTEM, so an administrator's ordinary open is refused. With the restore
    // privilege on, REG_OPTION_BACKUP_RESTORE opens the key for write whatever its ACL says.
    public unsafe int SetValue(string keyPath, string valueName, object value, RegistryValueKind kind)
    {
        using var _ = ProcessPrivilege.Hold(PInvoke.SE_BACKUP_NAME, PInvoke.SE_RESTORE_NAME);
        HKEY opened;
        WIN32_ERROR result;
        fixed (char* subKey = keyPath)
        {
            result = PInvoke.RegCreateKeyEx(
                new HKEY(Registry.LocalMachine.Handle.DangerousGetHandle()),
                new PCWSTR(subKey),
                0,
                default,
                REG_OPEN_CREATE_OPTIONS.REG_OPTION_BACKUP_RESTORE,
                REG_SAM_FLAGS.KEY_WRITE,
                null,
                &opened,
                null);
        }

        if (result != WIN32_ERROR.ERROR_SUCCESS)
        {
            return (int)result;
        }

        try
        {
            using var key = RegistryKey.FromHandle(new SafeRegistryHandle((nint)opened.Value, ownsHandle: true));
            key.SetValue(valueName, value, kind);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return ex.HResult;
        }
    }
}
