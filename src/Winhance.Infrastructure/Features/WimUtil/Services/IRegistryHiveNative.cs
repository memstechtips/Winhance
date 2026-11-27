using Microsoft.Win32;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

// Both return the Win32 error code, 0 on success.
internal interface IRegistryHiveNative
{
    int Load(string name, string file);

    int Unload(string name);

    // keyPath is under HKEY_LOCAL_MACHINE. Opens the key with the restore privilege, so the key's own ACL does not apply.
    int SetValue(string keyPath, string valueName, object value, RegistryValueKind kind);
}
