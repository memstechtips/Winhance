using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

// No registry value records that the revocation has applied: AvailableUpdates' 0x80 bit only requests it,
// and a cleared bit cannot tell "applied" from "never asked". Reading dbx itself is Microsoft's check.
internal sealed class FirmwareSecureBootDatabase(ILogService logService) : ISecureBootDatabase
{
    // EFI_IMAGE_SECURITY_DATABASE_GUID, the namespace db and dbx live in.
    private const string ImageSecurityDatabase = "{d719b2cb-3d3a-4596-a3bc-dad00e67656f}";

    private const int MaxDatabaseBytes = 1024 * 1024;

    // KB5025885's own test: [Text.Encoding]::ASCII.GetString((Get-SecureBootUEFI dbx).bytes) -match this name.
    private static readonly byte[] Pca2011Name = Encoding.ASCII.GetBytes("Microsoft Windows Production PCA 2011");

    public unsafe bool? RevokesPca2011()
    {
        EnableSystemEnvironmentPrivilege();

        var buffer = new byte[64 * 1024];
        while (true)
        {
            uint length;
            fixed (byte* data = buffer)
            {
                length = PInvoke.GetFirmwareEnvironmentVariableEx("dbx", ImageSecurityDatabase, data, (uint)buffer.Length, null);
            }

            if (length > 0)
            {
                return buffer.AsSpan(0, (int)length).IndexOf(Pca2011Name) >= 0;
            }

            var error = (WIN32_ERROR)Marshal.GetLastWin32Error();
            if (error == WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER && buffer.Length < MaxDatabaseBytes)
            {
                buffer = new byte[buffer.Length * 2];
                continue;
            }

            // ERROR_INVALID_FUNCTION on legacy BIOS, ERROR_ENVVAR_NOT_FOUND on firmware without Secure Boot.
            logService.LogInformation($"No Secure Boot dbx to read: {error}");
            return null;
        }
    }

    // Administrators hold this privilege disabled. When the token lacks it, AdjustTokenPrivileges still
    // succeeds and the read fails with ERROR_PRIVILEGE_NOT_HELD, which is logged like any other.
    private static unsafe void EnableSystemEnvironmentPrivilege()
    {
        if (!PInvoke.OpenProcessToken(
                PInvoke.GetCurrentProcess_SafeHandle(),
                TOKEN_ACCESS_MASK.TOKEN_ADJUST_PRIVILEGES | TOKEN_ACCESS_MASK.TOKEN_QUERY,
                out var token))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open the process token.");
        }

        using (token)
        {
            if (!PInvoke.LookupPrivilegeValue(null, PInvoke.SE_SYSTEM_ENVIRONMENT_NAME, out var luid))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not look up the firmware privilege.");
            }

            var privileges = new TOKEN_PRIVILEGES { PrivilegeCount = 1 };
            privileges.Privileges[0] = new LUID_AND_ATTRIBUTES
            {
                Luid = luid,
                Attributes = TOKEN_PRIVILEGES_ATTRIBUTES.SE_PRIVILEGE_ENABLED,
            };

            if (!PInvoke.AdjustTokenPrivileges(token, false, &privileges, 0, null, null))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not enable the firmware privilege.");
            }
        }
    }
}
